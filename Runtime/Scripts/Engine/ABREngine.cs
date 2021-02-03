/* ABREngine.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using System;
using System.Collections.Generic;
using IVLab.Utilities;
using UnityEngine;

namespace IVLab.ABREngine
{
    [RequireComponent(typeof(DataManager), typeof(VisAssetManager))]
    public class ABREngine : Singleton<ABREngine>
    {
        private List<IDataImpression> dataImpressions = new List<IDataImpression>();
        private Dictionary<Guid, EncodedGameObject> gameObjectMapping = new Dictionary<Guid, EncodedGameObject>();
        
        public void RegisterDataImpression(IDataImpression impression)
        {
            dataImpressions.Add(impression);
            GameObject impressionGameObject = new GameObject();
            impressionGameObject.transform.parent = this.transform;
            impressionGameObject.name = impression.GetType().ToString();

            EncodedGameObject ego = impressionGameObject.AddComponent<EncodedGameObject>();
            gameObjectMapping[impression.Uuid] = ego;
        }

        [FunctionDebugger]
        public void RenderImpressions()
        {
            foreach (var impression in dataImpressions)
            {
                impression.LoadRenderInfo();
            }

            foreach (var impression in dataImpressions)
            {
                Guid uuid = impression.Uuid;
                impression.ApplyToGameObject(gameObjectMapping[uuid]);
            }
        }
    }
}