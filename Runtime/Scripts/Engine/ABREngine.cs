/* ABREngine.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using IVLab.Utilities;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace IVLab.ABREngine
{
    [RequireComponent(typeof(DataManager), typeof(VisAssetManager))]
    public class ABREngine : Singleton<ABREngine>
    {
        private Dictionary<Guid, IDataImpression> dataImpressions = new Dictionary<Guid, IDataImpression>();
        private Dictionary<Guid, EncodedGameObject> gameObjectMapping = new Dictionary<Guid, EncodedGameObject>();

        private JToken previouslyLoadedState = null;
        private string previousStateName = "Untitled";

        private object _stateLock = new object();
        private object _stateUpdatingLock = new object();
        private bool stateUpdating = false;

        public ABRConfig Config { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Config = new ABRConfig();
        }

        public bool HasDataImpression(Guid uuid)
        {
            return dataImpressions.ContainsKey(uuid);
        }

        public void RegisterDataImpression(IDataImpression impression, bool allowOverwrite=true)
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
            foreach (var impression in dataImpressions)
            {
                UnregisterDataImpression(impression.Key);
            }
        }

        public void ReloadState()
        {
            ClearState();
            LoadStateFromResources(previousStateName);
        }

        public void RenderImpressions()
        {
            lock (_stateLock)
            {
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

                    PrepareImpression(impression.Value);
                }
            }
        }

        public void LoadStateFromResources(string stateName)
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
            UnityThreadScheduler.Instance.KickoffMainThreadWork(async () => {
                ABRStateParser parser = ABRStateParser.GetParser<ResourceStateFileLoader>();
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
                gameObjectMapping[impression.Uuid].gameObject.transform.parent = dataset.DataRoot.transform;

                // Display the UUID in editor
                gameObjectMapping[impression.Uuid].SetUuid(impression.Uuid);
            }
        }
    }
}