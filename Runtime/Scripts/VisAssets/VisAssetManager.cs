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

using Newtonsoft.Json.Linq;

namespace IVLab.ABREngine
{
    public class VisAssetManager : IVLab.Utilities.Singleton<VisAssetManager>
    {
        public string appDataPath;

        public const string VISASSET_JSON = "artifact.json";

        private Dictionary<Guid, IVisAsset> _visAssets = new Dictionary<Guid, IVisAsset>();

        void Start()
        {
            appDataPath = Path.Combine(Application.persistentDataPath, "media", "visassets");
            Directory.CreateDirectory(appDataPath);
            LoadVisAssetPalette();
        }

        string VisAssetDataPath(string artifactFilePath, string relativeDataPath)
        {
            return Path.Combine(Path.GetDirectoryName(artifactFilePath), relativeDataPath);
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
                    LoadVisAsset(filePath);
                    success += 1;
                }
                catch (Exception e)
                {
                    Debug.LogErrorFormat("Error loading VisAsset {0}:\n{1}", filePath, e);
                }
            }
            Debug.LogFormat("Successfully loaded {0}/{1} VisAssets", success, files.Length);
        }

        public void LoadVisAsset(Guid visAssetUUID)
        {
            var visassetPath = Path.Combine(
                appDataPath,
                visAssetUUID.ToString(),
                VISASSET_JSON
            );
            LoadVisAsset(visassetPath);
        }

        public void LoadVisAsset(string filePath, bool replaceExisting = false)
        {
            StreamReader reader = new StreamReader(filePath);
            JObject jsonData = JObject.Parse(reader.ReadToEnd());
            reader.Close();

            var guid = new System.Guid(jsonData["uuid"].ToString());

            string type = "";
            if (jsonData.ContainsKey("artifactType"))
            {
                type = jsonData["artifactType"].ToString();
            }
            else if (jsonData.ContainsKey("type"))
            {
                type = jsonData["type"].ToString();
                Debug.LogWarning(string.Format("VisAsset {0}: Use of field `type` is deprecated. Use `artifactType` instead.", guid.ToString().Substring(0, 8)));
            }

            if (type == "colormap")
            {
                ColormapVisAsset visAsset;
                if (replaceExisting && _visAssets.ContainsKey(guid))
                {
                    visAsset = _visAssets[guid] as ColormapVisAsset;
                }
                else
                {
                    visAsset = new ColormapVisAsset();
                    visAsset.Uuid = guid;
                }
                visAsset.ImportTime = DateTime.Now;

                string relativeColormapPath = jsonData["artifactData"]["colormap"].ToString();

                string colormapfilePath = Path.Combine(Path.GetDirectoryName(filePath), relativeColormapPath);
                Texture2D texture = null;

                if (File.Exists(colormapfilePath))
                {
                    texture = ColormapUtilities.ColormapFromFile(colormapfilePath);
                    visAsset.Gradient = texture;
                }

                _visAssets[guid] = visAsset;
            }

            if (type == "glyph")
            {
                GlyphVisAsset visAsset = new GlyphVisAsset();
                visAsset.Uuid = guid;
                visAsset.ImportTime = DateTime.Now;

                var artifactData = jsonData["artifactData"];
                List<JObject> lodsList = null;

                try
                {
                    lodsList = artifactData["lods"].ToObject<List<JObject>>();
                }
                catch (ArgumentException)
                {
                    if (artifactData is JArray)
                    {
                        Debug.LogWarning(string.Format(
                            "VisAsset {0}: Use of bare array in `artifactData` is deprecated. Put the array inside an object.",
                            guid.ToString().Substring(0, 8)
                        ));
                        lodsList = artifactData.ToObject<List<JObject>>();
                    }
                }
                foreach (JObject lodJson in lodsList)
                {
                    var meshPath = VisAssetDataPath(filePath, lodJson["mesh"].ToString());
                    var normalPath = VisAssetDataPath(filePath, lodJson["normal"].ToString());
                    GameObject loadedObjGameObject = null;
                    try
                    {
                        loadedObjGameObject = new IVLab.OBJImport.OBJLoader().Load(meshPath);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError(e);
                    }
                    loadedObjGameObject.transform.SetParent(transform);
                    loadedObjGameObject.SetActive(false);
                    var loadedMesh = loadedObjGameObject.GetComponentInChildren<MeshFilter>().mesh;
                    GameObject.Destroy(loadedObjGameObject);

                    var normalData = File.ReadAllBytes(normalPath);
                    var normalMap = new Texture2D(2, 2);
                    normalMap.LoadImage(normalData);

                    visAsset.MeshLods.Add(loadedMesh);
                    visAsset.NormalMapLods.Add(normalMap);
                }

                _visAssets[guid] = visAsset;
            }

            if (type == "line")
            {
                LineTextureVisAsset visAsset = new LineTextureVisAsset();
                visAsset.Uuid = guid;
                visAsset.ImportTime = DateTime.Now;

                var artifactData = jsonData["artifactData"];

                string texturePath = "";
                try
                {
                    texturePath = VisAssetDataPath(filePath, artifactData["horizontal"].ToString());
                    if (!File.Exists(texturePath))
                    {
                        throw new ArgumentException();
                    }
                }
                catch (ArgumentException e)
                {
                    Debug.LogErrorFormat("VisAsset {0} missing horizontal image artifact data", guid.ToString().Substring(0, 8));
                    throw e;
                }

                var textureData = File.ReadAllBytes(texturePath);
                var texture = new Texture2D(2, 2);
                texture.LoadImage(textureData);

                visAsset.TextureArray = new Texture2D[] { texture };

                _visAssets[guid] = visAsset;
            }


            if (type == "texture")
            {
                SurfaceTextureVisAsset visAsset = new SurfaceTextureVisAsset();
                visAsset.Uuid = guid;
                visAsset.ImportTime = DateTime.Now;

                var artifactData = jsonData["artifactData"];

                string texturePath = "";
                try
                {
                    texturePath = VisAssetDataPath(filePath, artifactData["image"].ToString());
                    if (!File.Exists(texturePath))
                    {
                        throw new ArgumentException();
                    }
                }
                catch (ArgumentException e)
                {
                    Debug.LogErrorFormat("VisAsset {0} missing image texture", guid.ToString().Substring(0, 8));
                    throw e;
                }

                var textureData = File.ReadAllBytes(texturePath);
                var texture = new Texture2D(2, 2);
                texture.LoadImage(textureData);

                visAsset.TextureArray = new Texture2D[] { texture };


                string normalPath = "";
                try
                {
                    normalPath = VisAssetDataPath(filePath, artifactData["normalmap"].ToString());
                    if (!File.Exists(normalPath))
                    {
                        throw new ArgumentException();
                    }
                }
                catch (ArgumentException e)
                {
                    Debug.LogErrorFormat("VisAsset {0} missing normal map texture", guid.ToString().Substring(0, 8));
                    throw e;
                }

                var normalData = File.ReadAllBytes(normalPath);
                var normal = new Texture2D(2, 2);
                normal.LoadImage(normalData);

                visAsset.NormalMapArray = new Texture2D[] { normal };

                _visAssets[guid] = visAsset;
            }
        }
    }
}