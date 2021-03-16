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
using System.Threading.Tasks;
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

        private VisAssetLoader visAssetLoader;
        private List<IVisAssetFetcher> visAssetFetchers = new List<IVisAssetFetcher>();

        private bool _loadResourceVisAssets;

        public VisAssetManager(string visassetPath, bool loadResourceVisAssets)
        {
            this.appDataPath = visassetPath;
            Directory.CreateDirectory(this.appDataPath);
            Debug.Log("VisAsset Path: " + appDataPath);
            visAssetLoader = new VisAssetLoader();
            visAssetFetchers.Add(new FilePathVisAssetFetcher(this.appDataPath));

            _loadResourceVisAssets = loadResourceVisAssets;
            if (loadResourceVisAssets)
            {
                Debug.Log("Allowing loading of VisAssets from Resources folder");
                visAssetFetchers.Add(new ResourceVisAssetFetcher());
            }

            if (ABREngine.Instance.Config.Info.visAssetServer != null)
            {
                Debug.Log("Allowing loading of VisAssets from " + ABREngine.Instance.Config.Info.visAssetServer);
                visAssetFetchers.Add(new HttpVisAssetFetcher(ABREngine.Instance.Config.Info.visAssetServer, this.appDataPath));
            }
        }

        public void TryGetVisAsset(Guid guid, out IVisAsset visAsset)
        {
            _visAssets.TryGetValue(guid, out visAsset);
        }

        public async Task LoadVisAssetPalette()
        {
            string[] files = Directory.GetFiles(appDataPath, VISASSET_JSON, SearchOption.AllDirectories);
            Debug.LogFormat("Loading VisAsset Palette ({0} VisAssets)", files.Length);

            int success = 0;
            foreach (var filePath in files)
            {
                try
                {
                    string uuid = Path.GetFileName(filePath);
                    await LoadVisAsset(new Guid(uuid));
                    success += 1;
                }
                catch (Exception e)
                {
                    Debug.LogErrorFormat("Error loading VisAsset {0}:\n{1}", filePath, e);
                }
            }
            Debug.LogFormat("Successfully loaded {0}/{1} VisAssets", success, files.Length);
        }

        public async Task LoadVisAsset(Guid visAssetUUID, bool replaceExisting = false)
        {
            if (_visAssets.ContainsKey(visAssetUUID) && !replaceExisting)
            {
                Debug.LogWarningFormat("Refusing to replace VisAsset {0} which is already imported", visAssetUUID);
                return;
            }

            try
            {
                // Try to fetch the visasset in terms of each fetcher's priority
                IVisAsset visAsset = null;
                foreach (IVisAssetFetcher fetcher in visAssetFetchers)
                {
                    visAsset = await visAssetLoader.LoadVisAsset(visAssetUUID, fetcher);

                    // If we've found it, stop looking
                    if (visAsset != null)
                    {
                        break;
                    }
                }

                if (visAsset != null)
                {
                    _visAssets[visAssetUUID] = visAsset;
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}