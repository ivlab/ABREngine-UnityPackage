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

using Newtonsoft.Json.Linq;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    /// <summary>
    /// The VisAssetManager is where all VisAssets are stored within the
    /// ABREngine. VisAssets can be loaded and fetched from various sources
    /// defined in `VisAssetFetchers`.
    /// </summary>
    /// <example>
    /// VisAssets can be loaded manually - be mindful of async programming:
    /// <code>
    /// // Initialize the ABR Engine
    /// await ABREngine.GetInstance().WaitUntilInitialized();
    /// 
    /// Guid cmapUuid = new Guid("66b3cde4-034d-11eb-a7e6-005056bae6d8");
    /// 
    /// // Load a VisAsset (must be done in Main Thread!)
    /// ColormapVisAsset cmap = null;
    /// await UnityThreadScheduler.Instance.RunMainThreadWork(async () =>
    /// {
    ///     cmap = await ABREngine.Instance.VisAssets.LoadVisAsset(cmapUuid) as ColormapVisAsset;
    /// });
    /// </code>
    /// </example>
    public class VisAssetManager
    {
        public string appDataPath;

        public const string VISASSET_JSON = "artifact.json";

        /// <summary>
        /// Any (custom) visassets that are solely described inside the state and do not
        /// exist on disk or on a server somewhere.
        /// </summary>
        public JObject LocalVisAssets { get; set; }

        /// <summary>
        /// Any VisAsset gradients that are contained within the state (updated
        /// directly from state)
        /// </summary>
        public Dictionary<string, RawVisAssetGradient> VisAssetGradients { get; set; } = new Dictionary<string, RawVisAssetGradient>();

        private Dictionary<Guid, IVisAsset> _visAssets = new Dictionary<Guid, IVisAsset>();

        private VisAssetLoader visAssetLoader;
        private List<IVisAssetFetcher> visAssetFetchers = new List<IVisAssetFetcher>();

        private bool _loadResourceVisAssets;

        public VisAssetManager(string visassetPath)
        {
            this.appDataPath = visassetPath;
            Directory.CreateDirectory(this.appDataPath);
            Debug.Log("VisAsset Path: " + appDataPath);
            visAssetLoader = new VisAssetLoader();

            // First, try to fetch VisAssets from Local (state) VisAssets
            visAssetFetchers.Add(new StateLocalVisAssetFetcher());

            // Then, try the file system...
            visAssetFetchers.Add(new FilePathVisAssetFetcher(this.appDataPath));

            // Afterwards, try the resources folder
            visAssetFetchers.Add(new ResourceVisAssetFetcher());

            // ... and lastly check out the VisAsset server, if present
            if (ABREngine.Instance.Config.Info.visAssetServer != null)
            {
                Debug.Log("Allowing loading of VisAssets from " + ABREngine.Instance.Config.Info.visAssetServer);
                visAssetFetchers.Add(new HttpVisAssetFetcher(ABREngine.Instance.Config.Info.visAssetServer, this.appDataPath));
            }
        }

        /// <summary>
        /// Attempt to retrieve a VisAsset.
        /// </summary>
        /// <returns>
        /// Returns true if the VisAsset is currently loaded into the memory.
        /// </returns>
        public bool TryGetVisAsset(Guid guid, out IVisAsset visAsset)
        {
            return _visAssets.TryGetValue(guid, out visAsset);
        }

        /// <summary>
        /// Load all VisAssets located in the Media directory into memory.
        /// </summary>
        [Obsolete("LoadVisAssetPalette is obsolete because it only takes into consideration VisAssets in the media directory")]
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

        /// <summary>
        /// Load a particular VisAsset described by its UUID. VisAssets will
        /// automatically be loaded from any of the following places:
        /// 1. The state itself (`localVisAssets`)
        /// 2. The media directory on the machine ABR is running on
        /// 3. Any Resources folder (in Assets or in any Package)
        /// 4. A VisAsset server
        /// </summary>
        /// <returns>
        /// Returns the <see cref="IVisAsset"> that was loaded, or `null` if the VisAsset was not found.
        /// </returns>
        public async Task<IVisAsset> LoadVisAsset(Guid visAssetUUID, bool replaceExisting = false)
        {
            if (_visAssets.ContainsKey(visAssetUUID) && !replaceExisting)
            {
                Debug.LogWarningFormat("Refusing to replace VisAsset {0} which is already imported", visAssetUUID);
                return null;
            }

            // First, check if the VisAsset is a gradient and recursivly import its VisAsset dependencies
            List<Guid> dependencyUuids = new List<Guid>();
            bool isGradient = false;
            if (VisAssetGradients != null && VisAssetGradients.ContainsKey(visAssetUUID.ToString()))
            {
                isGradient = true;
                foreach (var dependency in VisAssetGradients[visAssetUUID.ToString()].visAssets)
                {
                    dependencyUuids.Add(new Guid(dependency.ToString()));
                }
            }
            else
            {
                // If it's not a gradient, just import the regular VisAsset (no dependencies)
                dependencyUuids.Add(visAssetUUID);
            }

            IVisAsset toReturn = null;
            foreach (var dependency in dependencyUuids)
            {
                // The only time a dependency would have been updated is if it's a local vis asset.
                // If dependency not updated, skip it.
                if (_visAssets.ContainsKey(dependency) && !LocalVisAssets.ContainsKey(dependency.ToString()))
                {
                    continue;
                }
                try
                {
                    // Try to fetch the visasset in terms of each fetcher's priority
                    IVisAsset visAsset = null;
                    foreach (IVisAssetFetcher fetcher in visAssetFetchers)
                    {
                        try
                        {
                            visAsset = await visAssetLoader.LoadVisAsset(dependency, fetcher);
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
                        _visAssets[dependency] = visAsset;
                        toReturn = visAsset;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }

            // Post-import, verify that all VisAssets in this gradient are of the correct type
            if (!isGradient)
            {
                return toReturn;
            }
            else if (isGradient && dependencyUuids.Count > 0)
            {
                IVisAsset[] dependencies = dependencyUuids.Select((g) => {
                    IVisAsset visAsset = null;
                    if (TryGetVisAsset(g, out visAsset))
                    {
                        return visAsset;
                    }
                    else
                    {
                        return null;
                    }
                }).Where((va) => va != null).ToArray();

                IEnumerable<Type> vaType = dependencies.Select((va) => va.GetType()).Distinct();
                if (vaType.Count() != 1)
                {
                    Debug.LogErrorFormat("VisAsset Gradient `{0}`: not all VisAsset dependency types match", visAssetUUID.ToString());
                    return null;
                }

                Type visAssetType = vaType.First();
                {
                    if (visAssetType == typeof(GlyphVisAsset))
                    {
                        GlyphGradient gradient = VisAssetGradient.FromRaw<GlyphGradient, GlyphVisAsset>(VisAssetGradients[visAssetUUID.ToString()]);
                        _visAssets[visAssetUUID] = gradient;
                        return gradient;
                    }
                    else if (visAssetType == typeof(SurfaceTextureVisAsset))
                    {
                        SurfaceTextureGradient gradient = VisAssetGradient.FromRaw<SurfaceTextureGradient, SurfaceTextureVisAsset>(VisAssetGradients[visAssetUUID.ToString()]);
                        _visAssets[visAssetUUID] = gradient;
                        return gradient;
                    }
                    else if (visAssetType == typeof(LineTextureVisAsset))
                    {
                        LineTextureGradient gradient = VisAssetGradient.FromRaw<LineTextureGradient, LineTextureVisAsset>(VisAssetGradients[visAssetUUID.ToString()]);
                        _visAssets[visAssetUUID] = gradient;
                        return gradient;
                    }
                    else
                    {
                        throw new NotImplementedException(visAssetType.ToString() + " has no gradient handler");
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Unload a particular VisAsset described by its UUID. 
        /// </summary>
        public void UnloadVisAsset(Guid visAssetUUID)
        {
            _visAssets.Remove(visAssetUUID);
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
                return new ColormapVisAsset(cmapTex);
            }
            else
            {
                throw new NotImplementedException($"Default {t.ToString()} is not implemented");
            }
        }
    }
}