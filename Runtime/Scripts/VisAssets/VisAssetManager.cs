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
    /// defined in `VisAssetFetchers`. Currently, VisAssets may be loaded from any of the following:
    /// <ol>
    ///     <li>The state itself (`localVisAssets`)</li>
    ///     <li>The media directory on the machine ABR is running on</li>
    ///     <li>Any Resources folder (in Assets or in any Package)</li>
    ///     <li>A VisAsset server</li>
    /// </ol>
    /// </summary>
    /// <example>
    /// VisAssets can be loaded manually from your media folder, resources
    /// folder, or a network resource. This example loads a colormap
    /// `66b3cde4-034d-11eb-a7e6-005056bae6d8` from Resources (it's included in
    /// the ABREngine/Resources/media folder).
    /// <code>
    /// public class VisAssetManagerExample : MonoBehaviour
    /// {
    ///     void Start()
    ///     {
    ///         // Note, we could've used `LoadVisAsset` explicitly here, but
    ///         // GetVisAsset will automatically try to load the VisAsset if it
    ///         // doesn't already exist.
    ///         ColormapVisAsset cmap = ABREngine.Instance.VisAssets.GetVisAsset&lt;ColormapVisAsset&gt;(new System.Guid("66b3cde4-034d-11eb-a7e6-005056bae6d8"));
    ///     }
    ///
    ///     void Update()
    ///     {
    ///         // If you want to access the colormap later, you can use `GetVisAsset`.
    ///         ColormapVisAsset cmapInUpdate = ABREngine.Instance.VisAssets.GetVisAsset&lt;ColormapVisAsset&gt;(new System.Guid("66b3cde4-034d-11eb-a7e6-005056bae6d8");
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <example>
    /// You can also get the "default" visasset for a few VisAsset types. Keep
    /// in mind that the <see cref="GetDefault"/> method may not be defined for
    /// the VisAsset type that you want to get!
    /// <code>
    /// public class VisAssetManagerExample : MonoBehaviour
    /// {
    ///     void Start()
    ///     {
    ///         ColormapVisAsset cmap = ABREngine.Instance.VisAssets.GetDefault&lt;ColormapVisAsset&gt;() as ColormapVisAsset;
    ///     }
    /// }
    /// </code>
    /// </example>
    public class VisAssetManager
    {
        /// <summary>
        /// Application data path for internal access
        /// </summary>
        public string appDataPath;

        /// <summary>
        /// Name of the artifact.json files that each VisAsset has
        /// </summary>
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

        /// <summary>
        /// Initialize a new VisAssetManager and define all of the places VisAssets may be loaded from.
        /// </summary>
        /// <param name="visassetPath">Path to the VisAssets folder within ABR's media folder.</param>
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
            if (ABREngine.Instance.Config.visAssetServerUrl.Length > 0)
            {
                Debug.Log("Allowing loading of VisAssets from " + ABREngine.Instance.Config.visAssetServerUrl);
                visAssetFetchers.Add(new HttpVisAssetFetcher(ABREngine.Instance.Config.visAssetServerUrl, this.appDataPath));
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
        /// Get a visasset by its unique identifier.
        /// </summary>
        /// <param name="uuid">UUID of the VisAsset to get from the engine.</param>
        /// <typeparam name="T">Any <see cref="VisAsset"/> type.</typeparam>
        /// <returns>
        /// Returns the VisAsset, if found. If not found, tries to load the VisAsset then return it.
        /// </returns>
        public T GetVisAsset<T>(Guid uuid)
        where T: IVisAsset
        {
            IVisAsset va;
            if (TryGetVisAsset(uuid, out va))
            {
                return (T) va;
            }
            else
            {
                // If not found, try to load it
                return LoadVisAsset<T>(uuid);
            }
        }

        /// <summary>
        /// Load all VisAssets located in the Media directory into memory.
        /// </summary>
        [Obsolete("LoadVisAssetPalette is obsolete because it only takes into consideration VisAssets in the media directory")]
        public void LoadVisAssetPalette()
        {
            string[] files = Directory.GetFiles(appDataPath, VISASSET_JSON, SearchOption.AllDirectories);
            Debug.LogFormat("Loading VisAsset Palette ({0} VisAssets)", files.Length);

            int success = 0;
            foreach (var filePath in files)
            {
                try
                {
                    string uuid = Path.GetFileName(filePath);
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

        /// <summary>
        /// Load a VisAsset of a specific type.
        /// </summary>
        /// <param name="visAssetUUID">UUID of the VisAsset to load from any VisAsset loader.</param>
        /// <typeparam name="T">Any <see cref="VisAsset"/> type.</typeparam>
        /// <returns>
        /// Returns the VisAsset of type `T` that was loaded, or `null` if the VisAsset was not found.
        /// </returns>
        public T LoadVisAsset<T>(Guid visAssetUUID, bool replaceExisting = false)
        where T: IVisAsset
        {
            return (T) LoadVisAsset(visAssetUUID, replaceExisting);
        }

        /// <summary>
        /// Load a particular VisAsset described by its UUID.
        /// </summary>
        /// <returns>
        /// Returns the <see cref="IVisAsset"/> that was loaded, or `null` if the VisAsset was not found.
        /// </returns>
        public IVisAsset LoadVisAsset(Guid visAssetUUID, bool replaceExisting = false)
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
                if (_visAssets.ContainsKey(dependency) && (LocalVisAssets != null && !LocalVisAssets.ContainsKey(dependency.ToString())))
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
                            visAsset = visAssetLoader.LoadVisAsset(dependency, fetcher);
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
                    else
                    {
                        Debug.LogError($"Unable to find VisAsset `{visAssetUUID}`");
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

            Debug.LogError($"Unable to load VisAsset `{visAssetUUID}`");
            return null;
        }

        /// <summary>
        /// Unload a particular VisAsset described by its UUID. 
        /// </summary>
        /// <remarks>
        /// Note that this method *does not check if the VisAsset is in use*, so
        /// be careful when calling it!
        /// </remarks>
        public void UnloadVisAsset(Guid visAssetUUID)
        {
            _visAssets.Remove(visAssetUUID);
        }

        /// <summary>
        /// Get the UUIDs of every VisAsset that's been imported into ABR
        /// </summary>
        /// <returns>
        /// Returns a list containing UUIDs of each VisAsset in ABR.
        /// </returns>
        public List<Guid> GetVisAssets()
        {
            return _visAssets.Keys.ToList();
        }

        /// <summary>
        /// Obtain the default visasset for a particular type, if there is one.
        /// </summary>
        /// <remarks>
        /// If using the VisAsset immediately as the type `T`, you will likely need to do a cast (e.g. `ColormapVisAsset c = ....GetDefault&lt;ColormapVisAsset&gt;() as ColormapVisAsset`).
        /// </remarks>
        public IVisAsset GetDefault<T>()
        where T: IVisAsset
        {
            Type t = typeof(T);
            if (t.IsAssignableFrom(typeof(ColormapVisAsset)))
            {
                // Define a black-to-white colormap
                Colormap blackToWhite = new Colormap();
                blackToWhite.AddControlPt(0, Color.black);
                blackToWhite.AddControlPt(1, Color.white);
                return new ColormapVisAsset(blackToWhite);
            }
            if (t.IsAssignableFrom(typeof(GlyphVisAsset)))
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Mesh glyphMesh = cube.GetComponent<MeshFilter>().sharedMesh;
                List<Mesh> lods = new List<Mesh> { glyphMesh };
                List<Texture2D> nrms = new List<Texture2D> { Texture2D.normalTexture };
                GameObject.Destroy(cube);
                return new GlyphVisAsset(lods, nrms);
            }
            else
            {
                throw new NotImplementedException($"Default {t.ToString()} is not implemented");
            }
        }
    }
}