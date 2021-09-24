/* VisAssetManager.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json.Linq;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public class VisAssetManager
    {
        public string appDataPath;

        public const string VISASSET_JSON = "artifact.json";

        public JObject LocalVisAssets { get; set; }

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

            // First, try to fetch VisAssets from Local (state) VisAssets
            visAssetFetchers.Add(new StateLocalVisAssetFetcher());

            // Then, try the file system...
            visAssetFetchers.Add(new FilePathVisAssetFetcher(this.appDataPath));

            // Afterwards, try the resources folder if desired
            _loadResourceVisAssets = loadResourceVisAssets;
            if (loadResourceVisAssets)
            {
                Debug.Log("Allowing loading of VisAssets from Resources folder");
                visAssetFetchers.Add(new ResourceVisAssetFetcher());
            }

            // ... and lastly check out the VisAsset server, if present
            if (ABREngine.Instance.Config.Info.visAssetServer != null)
            {
                Debug.Log("Allowing loading of VisAssets from " + ABREngine.Instance.Config.Info.visAssetServer);
                visAssetFetchers.Add(new HttpVisAssetFetcher(ABREngine.Instance.Config.Info.visAssetServer, this.appDataPath));
            }
        }

        public bool TryGetVisAsset(Guid guid, out IVisAsset visAsset)
        {
            return _visAssets.TryGetValue(guid, out visAsset);
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
                    try
                    {
                        visAsset = await visAssetLoader.LoadVisAsset(visAssetUUID, fetcher);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }

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

        /// <summary>
        /// Get the UUIDs of every VisAsset that's been imported into ABR
        /// </summary>
        public List<Guid> GetVisAssets()
        {
            return _visAssets.Keys.ToList();
        }

        /// <summary>
        /// Obtain the default visasset for a particular type, if there is one.
        /// </summary>
        public IVisAsset GetDefault<T>()
        where T: IVisAsset
        {
            Type t = typeof(T);
            if (t.IsAssignableFrom(typeof(ColormapVisAsset)))
            {
                // Define a black-to-white colormap
                string colormXmlText = "<ColorMaps><ColorMap space=\"CIELAB\" indexedlookup=\"false\" name=\"ColorLoom\"><Point r=\"0\" g=\"0\" b=\"0\" x=\"0.0\"></Point><Point r=\"1\" g=\"1\" b=\"1\" x=\"1.0\"></Point></ColorMap></ColorMaps>";
                Texture2D cmapTex = ColormapUtilities.ColormapFromXML(colormXmlText, 1024, 1);
                ColormapVisAsset cmap = new ColormapVisAsset();
                cmap.Gradient = cmapTex;
                return cmap;
            }
            else
            {
                throw new NotImplementedException($"Default {t.ToString()} is not implemented");
            }
        }
    }
}