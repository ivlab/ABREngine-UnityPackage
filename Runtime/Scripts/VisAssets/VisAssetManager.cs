/* VisAssetManager.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json.Linq;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public class VisAssetManager
    {
        public string appDataPath;

        public const string VISASSET_JSON = "artifact.json";

        private Dictionary<Guid, IVisAsset> _visAssets = new Dictionary<Guid, IVisAsset>();

        private IVisAssetLoader visAssetLoader;

        private IVisAssetLoader resourceVisAssetLoader;
        private bool _loadResourceVisAssets;
        
        public VisAssetManager(string visassetPath, bool loadResourceVisAssets)
        {
            this.appDataPath = visassetPath;
            Directory.CreateDirectory(this.appDataPath);
            Debug.Log("VisAsset Path: " + appDataPath);
            visAssetLoader = new FilePathVisAssetLoader(this.appDataPath);

            _loadResourceVisAssets = loadResourceVisAssets;
            // resourceVisAssetLoader = 
            if (loadResourceVisAssets)
            {
                Debug.Log("Allowing VisAsset loading from Resources/media/visassets");
            }
        }

        public void TryGetVisAsset(Guid guid, out IVisAsset visAsset)
        {
            _visAssets.TryGetValue(guid, out visAsset);
        }

        public void LoadVisAssetPalette()
        {
            string[] files = Directory.GetFiles(appDataPath, VISASSET_JSON, SearchOption.AllDirectories);
            Debug.LogFormat("Loading VisAsset Palette ({0} VisAssets)", files.Length);

            int success = 0;
            foreach (var filePath in files)
            {
                try
                {
                    string uuid = Path.GetDirectoryName(filePath);
                    LoadVisAsset(new Guid(uuid));
                    success += 1;
                }
                catch (Exception e)
                {
                    Debug.LogErrorFormat("Error loading VisAsset {0}:\n{1}", filePath, e);
                }
            }
            Debug.LogFormat("Successfully loaded {0}/{1} VisAssets", success, files.Length);
        }

        public void LoadVisAsset(Guid visAssetUUID, bool replaceExisting = false)
        {
            if (_visAssets.ContainsKey(visAssetUUID) && !replaceExisting)
            {
                Debug.LogWarningFormat("Refusing to replace VisAsset {0} which is already imported", visAssetUUID);
                return;
            }

            IVisAsset visAsset = null;
            if (_loadResourceVisAssets)
            {
                visAsset = resourceVisAssetLoader.LoadVisAsset(visAssetUUID);
            }

            // If we haven't loaded it from resources, get it from disk
            if (visAsset == null)
            {
                visAsset = visAssetLoader.LoadVisAsset(visAssetUUID);
            }

            if (visAsset != null)
            {
                _visAssets[visAssetUUID] = visAsset;
            }
        }
    }
}