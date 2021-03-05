/* VisAssetLoader.cs
 *
 * Copyright (c) 2021, University of Minnesota
 *
 */

using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;

using Newtonsoft.Json.Linq;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public interface IVisAssetLoader
    {
        IVisAsset LoadVisAsset(Guid uuid);
    }

    public class FilePathVisAssetLoader : IVisAssetLoader
    {
        public const string VISASSET_JSON = "artifact.json";
        private string _appDataPath;

        public FilePathVisAssetLoader(string appDataPath)
        {
            _appDataPath = appDataPath;
        }

        public IVisAsset LoadVisAsset(Guid uuid)
        {
            var filePath = Path.Combine(
                _appDataPath,
                uuid.ToString(),
                VISASSET_JSON
            );

            StreamReader reader = new StreamReader(filePath);
            JObject jsonData = JObject.Parse(reader.ReadToEnd());
            reader.Close();

            var guid = new System.Guid(jsonData["uuid"].ToString());

            if (guid != uuid)
            {
                Debug.LogWarningFormat("Loading VisAsset {0} with non-matching UUID {1}", uuid, guid);
            }

            string type = "";
            if (jsonData.ContainsKey("artifactType"))
            {
                type = jsonData["artifactType"].ToString();
            }
            else if (jsonData.ContainsKey("type"))
            {
                type = jsonData["type"].ToString();
                Debug.LogWarning(string.Format("VisAsset {0}: Use of field `artifactType` is deprecated. Use `type` instead.", guid.ToString().Substring(0, 8)));
            }

            if (type == "colormap")
            {
                ColormapVisAsset visAsset = new ColormapVisAsset();
                visAsset.Uuid = guid;
                visAsset.ImportTime = DateTime.Now;

                string relativeColormapPath = jsonData["artifactData"]["colormap"].ToString();

                string colormapfilePath = VisAssetDataPath(filePath, relativeColormapPath);
                Texture2D texture = null;

                if (File.Exists(colormapfilePath))
                {
                    texture = ColormapUtilities.ColormapFromFile(colormapfilePath, 1024, 100);
                    visAsset.Gradient = texture;
                }

                return visAsset;
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
                    loadedObjGameObject.transform.SetParent(ABREngine.Instance.transform);
                    loadedObjGameObject.SetActive(false);
                    var loadedMesh = loadedObjGameObject.GetComponentInChildren<MeshFilter>().mesh;
                    GameObject.Destroy(loadedObjGameObject);

                    var normalData = File.ReadAllBytes(normalPath);
                    // Textures exported by Blender are in Linear color space
                    var normalMap = new Texture2D(2, 2, textureFormat: TextureFormat.RGBA32, mipChain: true, linear: true);
                    normalMap.LoadImage(normalData);

                    visAsset.MeshLods.Add(loadedMesh);
                    visAsset.NormalMapLods.Add(normalMap);
                }

                return visAsset;
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

                visAsset.Texture = texture;

                return visAsset;
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

                visAsset.Texture = texture;


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

                visAsset.NormalMap = normal;

                return visAsset;
            }
            return null;
        }

        private string VisAssetDataPath(string artifactFilePath, string relativeDataPath)
        {
            return Path.Combine(Path.GetDirectoryName(artifactFilePath), relativeDataPath);
        }
    }

    public class ResourceVisAssetLoader : IVisAssetLoader
    {
        public const string VISASSET_JSON = "artifact";
        public const string RESOURCES_PATH = "media/visassets/";

        public ResourceVisAssetLoader() { }

        public IVisAsset LoadVisAsset(Guid uuid)
        {
            // StreamReader reader = new StreamReader(filePath);
            // JObject jsonData = JObject.Parse(reader.ReadToEnd());
            // reader.Close();
            string resourcePath = Path.Combine(
                RESOURCES_PATH,
                uuid.ToString(),
                VISASSET_JSON
            );
            TextAsset artifactJson = Resources.Load<TextAsset>(resourcePath);
            JObject jsonData = JObject.Parse(artifactJson.text);

            var guid = new System.Guid(jsonData["uuid"].ToString());

            if (guid != uuid)
            {
                Debug.LogWarningFormat("Loading VisAsset {0} with non-matching UUID {1}", uuid, guid);
            }

            string type = "";
            if (jsonData.ContainsKey("artifactType"))
            {
                type = jsonData["artifactType"].ToString();
            }
            else if (jsonData.ContainsKey("type"))
            {
                type = jsonData["type"].ToString();
                Debug.LogWarning(string.Format("VisAsset {0}: Use of field `artifactType` is deprecated. Use `type` instead.", guid.ToString().Substring(0, 8)));
            }

            if (type == "colormap")
            {
                ColormapVisAsset visAsset = new ColormapVisAsset();
                visAsset.Uuid = guid;
                visAsset.ImportTime = DateTime.Now;

                string relativeColormapPath = jsonData["artifactData"]["colormap"].ToString();

                string colormapfilePath = VisAssetDataPath(resourcePath, relativeColormapPath);
                TextAsset colormapXML = Resources.Load<TextAsset>(colormapfilePath);

                Texture2D texture = ColormapUtilities.ColormapFromXML(colormapXML.text, 1024, 100);
                visAsset.Gradient = texture;

                return visAsset;
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
                    var meshPath = VisAssetDataPath(resourcePath, lodJson["mesh"].ToString());
                    var normalPath = VisAssetDataPath(resourcePath, lodJson["normal"].ToString());
                    GameObject loadedObjGameObject = null;
                    try
                    {
                        loadedObjGameObject = Resources.Load<GameObject>(meshPath);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError(e);
                    }
                    loadedObjGameObject.transform.SetParent(ABREngine.Instance.transform);
                    loadedObjGameObject.SetActive(false);
                    var loadedMesh = loadedObjGameObject.GetComponentInChildren<MeshFilter>().sharedMesh;
                    GameObject.Destroy(loadedObjGameObject);

                    Texture2D normalMap = Resources.Load<Texture2D>(normalPath);

                    visAsset.MeshLods.Add(loadedMesh);
                    visAsset.NormalMapLods.Add(normalMap);
                }

                return visAsset;
            }

            if (type == "line")
            {
                LineTextureVisAsset visAsset = new LineTextureVisAsset();
                visAsset.Uuid = guid;
                visAsset.ImportTime = DateTime.Now;

                var artifactData = jsonData["artifactData"];
                string texturePath = VisAssetDataPath(resourcePath, artifactData["horizontal"].ToString());
                Texture2D texture = Resources.Load<Texture2D>(texturePath);

                visAsset.Texture = texture;

                return visAsset;
            }


            if (type == "texture")
            {
                SurfaceTextureVisAsset visAsset = new SurfaceTextureVisAsset();
                visAsset.Uuid = guid;
                visAsset.ImportTime = DateTime.Now;

                var artifactData = jsonData["artifactData"];
                string texturePath = VisAssetDataPath(resourcePath, artifactData["horizontal"].ToString());
                Texture2D texture = Resources.Load<Texture2D>(texturePath);

                visAsset.Texture = texture;


                string normalPath = "";
                try
                {
                    normalPath = VisAssetDataPath(resourcePath, artifactData["normalmap"].ToString());
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

                visAsset.NormalMap = normal;

                return visAsset;
            }
            return null;
        }

        private string VisAssetDataPath(string artifactFilePath, string relativeDataPath)
        {
            string relPathNoExt = Path.GetFileNameWithoutExtension(relativeDataPath);
            return Path.Combine(Path.GetDirectoryName(artifactFilePath), relPathNoExt);
        }
    }

}