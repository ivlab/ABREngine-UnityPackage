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
        private List<IDataImpression> dataImpressions = new List<IDataImpression>();
        private Dictionary<Guid, EncodedGameObject> gameObjectMapping = new Dictionary<Guid, EncodedGameObject>();

        private JObject currentState = null;

        private object _stateLock = new object();
        private object _stateUpdatingLock = new object();
        private bool stateUpdating = false;

        public void RegisterDataImpression(IDataImpression impression)
        {
            dataImpressions.Add(impression);
            GameObject impressionGameObject = new GameObject();
            impressionGameObject.transform.parent = this.transform;
            impressionGameObject.name = impression.GetType().ToString();

            EncodedGameObject ego = impressionGameObject.AddComponent<EncodedGameObject>();
            gameObjectMapping[impression.Uuid] = ego;
        }

        public void RenderImpressions()
        {
            lock (_stateLock)
            {
                foreach (var impression in dataImpressions)
                {
                    impression.ComputeKeyDataRenderInfo();
                }

                foreach (var impression in dataImpressions)
                {
                    impression.ComputeRenderInfo();
                }

                foreach (var impression in dataImpressions)
                {
                    Guid uuid = impression.Uuid;
                    impression.ApplyToGameObject(gameObjectMapping[uuid]);

                    // Make sure the parent is assigned properly
                    Dataset dataset = impression.GetDataset();
                    if (dataset != null)
                    {
                        gameObjectMapping[uuid].gameObject.transform.parent = dataset.DataRoot.transform;
                    }
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
                JObject tempState = await parser.LoadState(stateName);
                lock (_stateLock)
                {
                    currentState = tempState;
                }
                RenderImpressions();
                lock (_stateUpdatingLock) 
                {
                    stateUpdating = false;
                }
            });
        }
    }
}