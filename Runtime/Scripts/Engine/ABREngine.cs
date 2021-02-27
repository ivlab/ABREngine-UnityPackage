/* ABREngine.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using IVLab.Utilities;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace IVLab.ABREngine
{
    public class ABREngine : Singleton<ABREngine>
    {
        private Dictionary<Guid, DataImpressionGroup> dataImpressionGroups = new Dictionary<Guid, DataImpressionGroup>();

        private JToken previouslyLoadedState = null;
        private string previousStateName = "Untitled";

        private object _stateLock = new object();
        private object _stateUpdatingLock = new object();
        private bool stateUpdating = false;

        private StateSubscriber _notifier;
        public VisAssetManager VisAssets { get; private set; }
        public DataManager Data { get; private set; }
        public SocketDataListener DataListener { get; private set; }

        // Save this for threading purposes (can't be accessed from non-main-thread)
        private string persistentDataPath = null;

        private DataImpressionGroup _defaultGroup = null;

        /// <summary>
        ///     If the Engine is connected to a local server, use that server's
        ///     data path, otherwise use our persistent data path.
        /// </summary>
        public string DataPath
        {
            get
            {
                if (_notifier != null && _notifier.serverIsLocal)
                {
                    return _notifier.subscriberInfo.localDataPath;
                }
                else
                {
                    return Path.Combine(persistentDataPath, "media");
                }
            }
        }

        public ABRConfig Config { get; private set; }

        public static readonly HttpClient httpClient = new HttpClient();

        protected override void Awake()
        {
            UnityThreadScheduler.GetInstance();
            persistentDataPath = Application.persistentDataPath;
            base.Awake();

            // Initialize the configuration from ABRConfig.json
            Config = new ABRConfig();

            // Initialize the default DataImpressionGroup (where impressions go
            // when they have no dataset)
            _defaultGroup = AddDataImpressionGroup("Default");

            Task.Run(async () =>
            {
                try
                {
                    if (Config.Info.serverAddress != null)
                    {
                        _notifier = new StateSubscriber(Config.Info.serverAddress);
                        await _notifier.Init();
                    }
                }
                catch (Exception)
                {
                    Debug.LogError("Unable to connect to state server " + Config.Info.serverAddress);
                }

                try
                {
                    VisAssets = new VisAssetManager(Path.Combine(DataPath, "visassets"));
                    Data = new DataManager(Path.Combine(DataPath, "datasets"));
                    if (Config.Info.dataListenerPort != null)
                    {
                        DataListener = new SocketDataListener(Config.Info.dataListenerPort.Value);
                        DataListener.StartServer();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }

                // Fetch the state from the server, if we're connected
                if (Config.Info.serverAddress != null &&
                        _notifier != null &&
                        Config.Info.statePathOnServer != null
                )
                {
                    LoadState<HttpStateFileLoader>(Config.Info.serverAddress + Config.Info.statePathOnServer);
                }
            });
        }

        void OnDestroy()
        {
            _notifier?.Stop();
            DataListener?.StopServer();
        }

        public bool HasDataImpression(Guid uuid)
        {
            return dataImpressionGroups
                .Select((kv) => kv.Value)
                .Where((v) => v.HasDataImpression(uuid)).ToList().Count > 0;
        }

        public IDataImpression GetDataImpression(Guid uuid)
        {
            return dataImpressionGroups
                .Select((kv) => kv.Value)
                .First((v) => v.HasDataImpression(uuid))
                .GetDataImpression(uuid);
        }

        public DataImpressionGroup AddDataImpressionGroup(string name)
        {
            DataImpressionGroup group = new DataImpressionGroup(name, Config.Info.defaultBounds.Value, this.transform);
            dataImpressionGroups[group.Uuid] = group;
            return group;
        }

        public void RemoveDataImpressionGroup(Guid uuid)
        {
            Debug.LogFormat("Removing data impression group {0}", uuid);
            dataImpressionGroups[uuid].Clear();
            Destroy(dataImpressionGroups[uuid].GroupRoot);
            dataImpressionGroups.Remove(uuid);
        }

        public Dictionary<Guid, DataImpressionGroup> GetDataImpressionGroups()
        {
            return dataImpressionGroups;
        }

        /// <summary>
        ///     Get the group a particular data impression
        /// </summary>
        public DataImpressionGroup GetGroupFromImpression(IDataImpression dataImpression)
        {
            try
            {
                return dataImpressionGroups
                    .Select((kv) => kv.Value)
                    .First((v) => v.HasDataImpression(dataImpression.Uuid));
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        ///     Register a new data impression, or replace an existing one. If the
        ///     data impression has a dataset, defaults to creating a new
        ///     DataImpressionGroup with that dataset. If we create a new
        ///     DataImpressionGroup, we need to move/delete the impression from the
        ///     old group.
        /// </summary>
        public void RegisterDataImpression(IDataImpression dataImpression, bool allowOverwrite = true)
        {
            Dataset ds = dataImpression.GetDataset();
            if (ds != null)
            {
                // See if it's a part of a group already
                DataImpressionGroup oldGroup = GetGroupFromImpression(dataImpression);

                // Find an existing DataImpressionGroup with the same dataset, if any
                DataImpressionGroup newGroup = null;
                foreach (var group in dataImpressionGroups)
                {
                    if (group.Value.GetDataset()?.Path == ds.Path)
                    {
                        // Add it to the first one we find, if we find one
                        newGroup = group.Value;
                    }
                }

                // If the new and old groups are different, remove from old group
                bool oldGroupEmpty = false;
                if (oldGroup != null && newGroup != null && newGroup.Uuid != oldGroup.Uuid)
                {
                    oldGroupEmpty = oldGroup.RemoveDataImpression(dataImpression.Uuid);
                }

                // If the old group is empty, remove it
                if (oldGroupEmpty && oldGroup.Uuid != _defaultGroup.Uuid)
                {
                    RemoveDataImpressionGroup(oldGroup.Uuid);
                }

                // Create a new group if it doesn't exist
                if (newGroup == null)
                {
                    newGroup = AddDataImpressionGroup(ds.Path);
                }
                newGroup.AddDataImpression(dataImpression, allowOverwrite);
            }
            else
            {
                // If there's no data yet, put it in the default group
                _defaultGroup.AddDataImpression(dataImpression, allowOverwrite);
            }
        }

        public void UnregisterDataImpression(Guid uuid)
        {
            var toRemove = new List<Guid>();
            foreach (var group in dataImpressionGroups)
            {
                // Remove the impression from any groups its in (should only be one)
                group.Value.RemoveDataImpression(uuid);

                // Also remove the group if it becomes empty, unless it's the default group
                if (group.Value.GetDataImpressions().Count == 0 && group.Key != _defaultGroup.Uuid)
                {
                    toRemove.Add(group.Key);
                }
            }

            foreach (var guid in toRemove)
            {
                RemoveDataImpressionGroup(guid);
            }
        }

        public void ClearState()
        {
            List<Guid> toRemove = new List<Guid>();
            foreach (var group in dataImpressionGroups)
            {
                group.Value.Clear();
                toRemove.Add(group.Key);
            }
            foreach (var r in toRemove)
            {
                dataImpressionGroups.Remove(r);
            }
        }

        [FunctionDebugger]
        public void Render()
        {
            try
            {
                lock (_stateLock)
                {
                    foreach (var group in dataImpressionGroups)
                    {
                        group.Value.RenderImpressions();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error while rendering impressions");
                Debug.LogError(e);
            }
        }

        public void LoadState<T>(string stateName)
        where T : IABRStateLoader, new()
        {
            // Kick off a task to load the new state, and make sure it's all in
            // the main thread. TODO there's definitely a better way to do this
            // and optimize what needs to be in the main thread and what
            // doesn't. Currently takes about 10 frames to get from here to the
            // end of the `RenderImpressions()` call.
            lock (_stateUpdatingLock)
            {
                stateUpdating = true;
            }
            UnityThreadScheduler.Instance.KickoffMainThreadWork(async () =>
            {
                ABRStateParser parser = ABRStateParser.GetParser<T>();
                JToken tempState = await parser.LoadState(stateName, previouslyLoadedState);
                lock (_stateLock)
                {
                    previousStateName = stateName;
                    previouslyLoadedState = tempState;
                }
                Render();
                lock (_stateUpdatingLock)
                {
                    stateUpdating = false;
                }
            });
        }

    }
}