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
using Newtonsoft.Json;

namespace IVLab.ABREngine
{
    public class ABREngine : Singleton<ABREngine>
    {
        private Dictionary<Guid, DataImpressionGroup> dataImpressionGroups = new Dictionary<Guid, DataImpressionGroup>();

        public JObject State { get { return previouslyLoadedState; }}
        private JObject previouslyLoadedState = null;
        private string previousStateName = "Untitled";

        private object _stateLock = new object();
        private object _stateUpdatingLock = new object();
        private bool stateUpdating = false;

        private Notifier _notifier;
        public VisAssetManager VisAssets { get; private set; }
        public DataManager Data { get; private set; }
        public SocketDataListener DataListener { get; private set; }

        // Delegate callback for when state is updated
        public delegate void StateChangeDelegate(JObject state);
        public StateChangeDelegate OnStateChanged;

        // Save this for threading purposes (can't be accessed from non-main-thread)
        private string persistentDataPath = null;

        private DataImpressionGroup _defaultGroup = null;

        private bool _initialized = false;

        /// <summary>
        ///     If a specific media path is provided, use that. Otherwise, use
        ///     our persistent data path.
        /// </summary>
        public string MediaPath
        {
            get
            {
                if (Config.Info.mediaPath != null)
                {
                    return Path.GetFullPath(Config.Info.mediaPath);
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
                        _notifier = new Notifier(Config.Info.serverAddress);
                        await _notifier.Init();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Unable to connect to state server " + Config.Info.serverAddress);
                    Debug.LogError(e);
                }

                try
                {
                    VisAssets = new VisAssetManager(Path.Combine(MediaPath, "visassets"), Config.Info.loadResourceVisAssets);
                    Data = new DataManager(Path.Combine(MediaPath, "datasets"));
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
                _initialized = true;
            });
        }

        public async Task WaitUntilInitialized()
        {
            while (!_initialized)
            {
                await Task.Delay(10);
            }
        }

        void OnDisable()
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
            try
            {
                return dataImpressionGroups
                    .Select((kv) => kv.Value)
                    .First((v) => v.HasDataImpression(uuid))
                    .GetDataImpression(uuid);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public List<T> GetDataImpressionsOfType<T>()
        where T : IDataImpression
        {
            return dataImpressionGroups
                .Select((kv) => kv.Value)
                .Select((grp) => grp.GetDataImpressionsOfType<T>())
                .Aggregate((all, imps) => all.Concat(imps).ToList());
        }

        public List<IDataImpression> GetDataImpressionsWithTag(string tag)
        {
            return dataImpressionGroups
                .Select((kv) => kv.Value)
                .Select((grp) => grp.GetDataImpressionsWithTag(tag))
                .Aggregate((all, imps) => all.Concat(imps).ToList());
        }

        public EncodedGameObject GetEncodedGameObject(Guid impressionGuid)
        {
            try
            {
                return dataImpressionGroups
                    .Select((kv) => kv.Value)
                    .First((v) => v.HasEncodedGameObject(impressionGuid))
                    .GetEncodedGameObject(impressionGuid);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public DataImpressionGroup AddDataImpressionGroup(string name)
        {
            return AddDataImpressionGroup(name, Guid.NewGuid(), Config.Info.defaultBounds.Value, Vector3.zero, Quaternion.identity);
        }

        public DataImpressionGroup AddDataImpressionGroup(string name, Guid uuid, Bounds bounds, Vector3 position, Quaternion rotation)
        {
            DataImpressionGroup group = new DataImpressionGroup(name, uuid, bounds, position, rotation, this.transform);
            dataImpressionGroups[group.Uuid] = group;
            return group;
        }

        public void RemoveDataImpressionGroup(Guid uuid)
        {
            dataImpressionGroups[uuid].Clear();
            Destroy(dataImpressionGroups[uuid].GroupRoot);
            dataImpressionGroups.Remove(uuid);
        }

        public Dictionary<Guid, DataImpressionGroup> GetDataImpressionGroups()
        {
            return dataImpressionGroups;
        }

        public DataImpressionGroup GetDataImpressionGroup(Guid uuid)
        {
            DataImpressionGroup g = null;
            if (dataImpressionGroups.TryGetValue(uuid, out g))
            {
                return g;
            }
            else
            {
                return null;
            }
        }

        public bool HasDataImpressionGroup(Guid uuid)
        {
            return dataImpressionGroups.ContainsKey(uuid);
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
                    .First((v) => dataImpression != null && v.HasDataImpression(dataImpression.Uuid));
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        ///     Add a new data impression, but add it to a specific group ID.
        /// </summary>
        public void RegisterDataImpression(IDataImpression dataImpression, DataImpressionGroup newGroup, bool allowOverwrite = true)
        {
            // Create a new group if it doesn't exist
            if (newGroup == null)
            {
                // Name it according to the impression's dataset, if there is one
                Dataset ds = dataImpression.GetDataset();
                if (ds?.Path != null)
                {
                    newGroup = AddDataImpressionGroup(ds.Path);
                }
                else
                {
                    newGroup = AddDataImpressionGroup(string.Format("{0}", DateTimeOffset.Now.ToUnixTimeMilliseconds()));
                }
            }
            MoveImpressionToGroup(dataImpression, newGroup, allowOverwrite);
        }


        /// <summary>
        ///     Register a new data impression, or replace an existing one. If the
        ///     data impression has a dataset, defaults to placing it inside the
        ///     existing group with the same dataset, or creating a new
        ///     DataImpressionGroup with that dataset if no group exists yet.
        /// </summary>
        public void RegisterDataImpression(IDataImpression dataImpression, bool allowOverwrite = true)
        {
            Dataset ds = dataImpression.GetDataset();
            if (ds != null)
            {
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

                RegisterDataImpression(dataImpression, newGroup, allowOverwrite);
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

        public IDataImpression DuplicateDataImpression(Guid uuid)
        {
            return DuplicateDataImpression(GetDataImpression(uuid));
        }

        public IDataImpression DuplicateDataImpression(IDataImpression impression)
        {
            return DuplicateDataImpression(impression, null);
        }

        public IDataImpression DuplicateDataImpression(IDataImpression dataImpression, DataImpressionGroup group)
        {
            IDataImpression newDataImpression = dataImpression.Copy();
            newDataImpression.Uuid = Guid.NewGuid();

            if (group == null)
            {
                RegisterDataImpression(newDataImpression);
            }
            else
            {
                RegisterDataImpression(newDataImpression, group, false);
            }

            return newDataImpression;
        }

        public IDataImpression DuplicateDataImpression(IDataImpression dataImpression, bool retainGroup = true)
        {
            if (retainGroup)
            {
                DataImpressionGroup group = GetGroupFromImpression(dataImpression);
                return DuplicateDataImpression(dataImpression, group);
            }
            else
            {
                return DuplicateDataImpression(dataImpression, null);
            }
        }

        public void MoveImpressionToGroup(IDataImpression dataImpression, DataImpressionGroup newGroup, bool allowOverwrite = true)
        {
            // See if it's a part of a group already
            DataImpressionGroup oldGroup = GetGroupFromImpression(dataImpression);

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

            newGroup.AddDataImpression(dataImpression, allowOverwrite);
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
            UnityThreadScheduler.Instance.KickoffMainThreadWork(async () =>
            {
                await LoadStateAsync<T>(stateName);
            });
        }

        public async Task LoadStateAsync<T>(string stateName)
        where T : IABRStateLoader, new()
        {
            lock (_stateUpdatingLock)
            {
                stateUpdating = true;
            }
            await UnityThreadScheduler.Instance.RunMainThreadWork(async () =>
            {
                ABRStateParser parser = ABRStateParser.GetParser<T>();
                try
                {
                    JObject tempState = await parser.LoadState(stateName, previouslyLoadedState);
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
                    if (OnStateChanged != null)
                    {
                        OnStateChanged(previouslyLoadedState);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            });
        }

        public async Task SaveStateAsync()
        {
            HttpStateFileLoader loader = new HttpStateFileLoader();
            ABRStateParser parser = ABRStateParser.GetParser<HttpStateFileLoader>();
            try
            {
                await UnityThreadScheduler.Instance.RunMainThreadWork(async () =>
                {
                    try
                    {
                        string state = parser.SerializeState(previouslyLoadedState);

                        await loader.SaveState(state);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}