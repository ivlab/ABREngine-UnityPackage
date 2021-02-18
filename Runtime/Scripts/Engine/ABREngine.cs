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
        private Dictionary<Guid, IDataImpression> dataImpressions = new Dictionary<Guid, IDataImpression>();
        private Dictionary<Guid, EncodedGameObject> gameObjectMapping = new Dictionary<Guid, EncodedGameObject>();

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
            Config = new ABRConfig();
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
            DataListener.StopServer();
        }

        public bool HasDataImpression(Guid uuid)
        {
            return dataImpressions.ContainsKey(uuid);
        }

        public IDataImpression GetDataImpression(Guid uuid)
        {
            return dataImpressions[uuid];
        }

        public void RegisterDataImpression(IDataImpression impression, bool allowOverwrite = true)
        {
            if (dataImpressions.ContainsKey(impression.Uuid))
            {
                if (allowOverwrite)
                {
                    dataImpressions[impression.Uuid] = impression;
                }
                else
                {
                    Debug.LogWarningFormat("Skipping register data impression (already exists): {0}", impression.Uuid);
                }
            }
            else
            {
                dataImpressions.Add(impression.Uuid, impression);
                GameObject impressionGameObject = new GameObject();
                impressionGameObject.transform.parent = this.transform;
                impressionGameObject.name = impression.GetType().ToString();

                EncodedGameObject ego = impressionGameObject.AddComponent<EncodedGameObject>();
                gameObjectMapping[impression.Uuid] = ego;

                PrepareImpression(impression);
            }
        }

        public void UnregisterDataImpression(Guid uuid)
        {
            dataImpressions.Remove(uuid);
            Destroy(gameObjectMapping[uuid].gameObject);
            gameObjectMapping.Remove(uuid);
        }

        public void ClearState()
        {
            List<Guid> toRemove = dataImpressions.Keys.ToList();
            foreach (var impressionUuid in toRemove)
            {
                UnregisterDataImpression(impressionUuid);
            }
        }

        [FunctionDebugger]
        public void RenderImpressions()
        {
            try
            {
                lock (_stateLock)
                {
                    foreach (var impression in dataImpressions)
                    {
                        PrepareImpression(impression.Value);
                    }

                    foreach (var impression in dataImpressions)
                    {
                        impression.Value.ComputeKeyDataRenderInfo();
                    }

                    foreach (var impression in dataImpressions)
                    {
                        impression.Value.ComputeRenderInfo();
                    }

                    foreach (var impression in dataImpressions)
                    {
                        Guid uuid = impression.Key;
                        impression.Value.ApplyToGameObject(gameObjectMapping[uuid]);
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
                RenderImpressions();
                lock (_stateUpdatingLock)
                {
                    stateUpdating = false;
                }
            });
        }

        private void PrepareImpression(IDataImpression impression)
        {
            Dataset dataset = impression.GetDataset();
            if (dataset != null)
            {
                // Make sure the bounding box is correct
                // Mostly matters if there's a live ParaView connection
                dataset.RecalculateBounds();

                // Make sure the parent is assigned properly
                gameObjectMapping[impression.Uuid].gameObject.transform.SetParent(dataset.DataRoot.transform, false);

                // Display the UUID in editor
                gameObjectMapping[impression.Uuid].SetUuid(impression.Uuid);
            }
        }
    }
}