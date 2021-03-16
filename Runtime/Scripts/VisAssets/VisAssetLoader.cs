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
    public interface IVisAssetFetcher
    {
        JObject GetArtifactJson(Guid uuid);
        Texture2D GetColormapTexture(Guid uuid);
        GameObject GetGlyphGameObject(Guid uuid, JObject lodJson);
        Texture2D GetGlyphNormalMapTexture(Guid uuid, JObject lodJson);
        Texture2D GetLineTexture(Guid uuid);
        Texture2D GetSurfaceTexture(Guid uuid);
        Texture2D GetSurfaceNormalMap(Guid uuid);
    }

    public class ResourceVisAssetFetcher : IVisAssetFetcher
    {
        public const string VISASSET_JSON = "artifact";
        public const string RESOURCES_PATH = "media/visassets/";

        private string VisAssetDataPath(string artifactFilePath, string relativeDataPath)
        {
            string relPathNoExt = Path.GetFileNameWithoutExtension(relativeDataPath);
            relPathNoExt = Path.Combine(Path.GetDirectoryName(relativeDataPath), relPathNoExt);
            return Path.Combine(Path.GetDirectoryName(artifactFilePath), relPathNoExt);
        }

        public string GetArtifactJsonPath(Guid uuid)
        {
            return Path.Combine(
                RESOURCES_PATH,
                uuid.ToString(),
                VISASSET_JSON
            );
        }

        public JObject GetArtifactJson(Guid uuid)
        {
            TextAsset artifactJson = Resources.Load<TextAsset>(GetArtifactJsonPath(uuid));
            if (artifactJson == null)
            {
                return null;
            }
            return JObject.Parse(artifactJson.text);
        }

        public Texture2D GetColormapTexture(Guid uuid)
        {
            string colormapfilePath = VisAssetDataPath(GetArtifactJsonPath(uuid), GetArtifactJson(uuid)["artifactData"]["colormap"].ToString());
            TextAsset colormapXML = Resources.Load<TextAsset>(colormapfilePath);
            Texture2D texture = ColormapUtilities.ColormapFromXML(colormapXML.text, 1024, 100);
            return texture;
        }

        public GameObject GetGlyphGameObject(Guid uuid, JObject lodJson)
        {
            var meshPath = VisAssetDataPath(GetArtifactJsonPath(uuid), lodJson["mesh"].ToString());
            GameObject loadedObjGameObject = null;
            try
            {
                loadedObjGameObject = Resources.Load<GameObject>(meshPath);
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
            return loadedObjGameObject;
        }

        public Texture2D GetGlyphNormalMapTexture(Guid uuid, JObject lodJson)
        {
            var normalPath = VisAssetDataPath(GetArtifactJsonPath(uuid), lodJson["normalmap"].ToString());
            Texture2D normalMap = Resources.Load<Texture2D>(normalPath);
            return normalMap;
        }

        public Texture2D GetLineTexture(Guid uuid)
        {
            var path = VisAssetDataPath(GetArtifactJsonPath(uuid), GetArtifactJson(uuid)["horizontal"].ToString());
            Texture2D normalMap = Resources.Load<Texture2D>(path);
            return normalMap;
        }

        public Texture2D GetSurfaceTexture(Guid uuid)
        {
            var path = VisAssetDataPath(GetArtifactJsonPath(uuid), GetArtifactJson(uuid)["image"].ToString());
            Texture2D normalMap = Resources.Load<Texture2D>(path);
            return normalMap;
        }

        public Texture2D GetSurfaceNormalMap(Guid uuid)
        {
            var path = VisAssetDataPath(GetArtifactJsonPath(uuid), GetArtifactJson(uuid)["normalmap"].ToString());
            Texture2D normalMap = Resources.Load<Texture2D>(path);
            return normalMap;
        }
    }

    public class FilePathVisAssetFetcher : IVisAssetFetcher
    {
        public const string VISASSET_JSON = "artifact.json";
        private Dictionary<Guid, JObject> _artifactJsonCache = new Dictionary<Guid, JObject>();
        private string _appDataPath;

        private string GetArtifactJsonPath(Guid uuid)
        {
            return Path.Combine(
                _appDataPath,
                uuid.ToString(),
                VISASSET_JSON
            );
        }

        public FilePathVisAssetFetcher(string appDataPath)
        {
            _appDataPath = appDataPath;
        }

        private string VisAssetDataPath(string artifactFilePath, string relativeDataPath)
        {
            return Path.Combine(Path.GetDirectoryName(artifactFilePath), relativeDataPath);
        }

        public JObject GetArtifactJson(Guid uuid)
        {
            if (!_artifactJsonCache.ContainsKey(uuid))
            {
                StreamReader reader = new StreamReader(GetArtifactJsonPath(uuid));
                JObject jsonData = JObject.Parse(reader.ReadToEnd());
                reader.Close();
                _artifactJsonCache[uuid] = jsonData;
                return jsonData;
            }
            else
            {
                return _artifactJsonCache[uuid];
            }
        }

        public Texture2D GetColormapTexture(Guid uuid)
        {
            string relativeColormapPath = GetArtifactJson(uuid)["artifactData"]["colormap"].ToString();
            string colormapfilePath = VisAssetDataPath(GetArtifactJsonPath(uuid), relativeColormapPath);

            if (File.Exists(colormapfilePath))
            {
                return ColormapUtilities.ColormapFromFile(colormapfilePath, 1024, 100);
            }
            return null;
        }

        public GameObject GetGlyphGameObject(Guid uuid, JObject lodJson)
        {
            string artifactJsonPath = GetArtifactJsonPath(uuid);
            var meshPath = VisAssetDataPath(artifactJsonPath, lodJson["mesh"].ToString());
            GameObject loadedObjGameObject = null;
            try
            {
                loadedObjGameObject = new IVLab.OBJImport.OBJLoader().Load(meshPath);
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
            return loadedObjGameObject;
        }

        public Texture2D GetGlyphNormalMapTexture(Guid uuid, JObject lodJson)
        {
            string artifactJsonPath = GetArtifactJsonPath(uuid);
            var normalPath = VisAssetDataPath(artifactJsonPath, lodJson["normal"].ToString());
            var normalData = File.ReadAllBytes(normalPath);
            // Textures exported by Blender are in Linear color space
            var normalMap = new Texture2D(2, 2, textureFormat: TextureFormat.RGBA32, mipChain: true, linear: true);
            normalMap.LoadImage(normalData);
            return normalMap;
        }

        public Texture2D GetLineTexture(Guid uuid)
        {
            string texturePath = "";
            try
            {
                texturePath = VisAssetDataPath(GetArtifactJsonPath(uuid), GetArtifactJson(uuid)["horizontal"].ToString());
                if (!File.Exists(texturePath))
                {
                    throw new ArgumentException();
                }
            }
            catch (ArgumentException e)
            {
                Debug.LogErrorFormat("VisAsset {0} missing horizontal image artifact data", uuid.ToString().Substring(0, 8));
                throw e;
            }

            var textureData = File.ReadAllBytes(texturePath);
            var texture = new Texture2D(2, 2);
            texture.LoadImage(textureData);
            return texture;
        }

        public Texture2D GetSurfaceTexture(Guid uuid)
        {
            string texturePath = "";
            try
            {
                texturePath = VisAssetDataPath(GetArtifactJsonPath(uuid), GetArtifactJson(uuid)["image"].ToString());
                if (!File.Exists(texturePath))
                {
                    throw new ArgumentException();
                }
            }
            catch (ArgumentException e)
            {
                Debug.LogErrorFormat("VisAsset {0} missing image texture", uuid.ToString().Substring(0, 8));
                throw e;
            }

            var textureData = File.ReadAllBytes(texturePath);
            var texture = new Texture2D(2, 2);
            texture.LoadImage(textureData);
            return texture;
        }

        public Texture2D GetSurfaceNormalMap(Guid uuid)
        {
            string texturePath = "";
            try
            {
                texturePath = VisAssetDataPath(GetArtifactJsonPath(uuid), GetArtifactJson(uuid)["normalmap"].ToString());
                if (!File.Exists(texturePath))
                {
                    throw new ArgumentException();
                }
            }
            catch (ArgumentException e)
            {
                Debug.LogErrorFormat("VisAsset {0} missing image texture", uuid.ToString().Substring(0, 8));
                throw e;
            }

            var textureData = File.ReadAllBytes(texturePath);
            var texture = new Texture2D(2, 2);
            texture.LoadImage(textureData);
            return texture;
        }
    }

    public class VisAssetLoader
    {
        public VisAssetLoader() { }

        public IVisAsset LoadVisAsset(Guid uuid)
        {
            return LoadVisAsset(uuid, new ResourceVisAssetFetcher());
        }

        public IVisAsset LoadVisAsset(Guid uuid, IVisAssetFetcher _fetcher)
        {
            JObject jsonData = _fetcher.GetArtifactJson(uuid);
            // Abort if there was no artifact.json
            if (jsonData == null)
            {
                return null;
            }

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
                Texture2D colormapTexture = _fetcher.GetColormapTexture(guid);
                visAsset.Gradient = colormapTexture;
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
                    GameObject loadedObjGameObject = _fetcher.GetGlyphGameObject(guid, lodJson);
                    loadedObjGameObject.transform.SetParent(ABREngine.Instance.transform);
                    loadedObjGameObject.SetActive(false);
                    var loadedMesh = loadedObjGameObject.GetComponentInChildren<MeshFilter>().mesh;
                    GameObject.Destroy(loadedObjGameObject);
                    visAsset.MeshLods.Add(loadedMesh);

                    Texture2D normalMap = _fetcher.GetGlyphNormalMapTexture(uuid, lodJson);
                    visAsset.NormalMapLods.Add(normalMap);
                }

                return visAsset;
            }

            if (type == "line")
            {
                LineTextureVisAsset visAsset = new LineTextureVisAsset();
                visAsset.Uuid = guid;
                visAsset.ImportTime = DateTime.Now;

                Texture2D texture = _fetcher.GetLineTexture(guid);
                visAsset.Texture = texture;

                return visAsset;
            }

            if (type == "texture")
            {
                SurfaceTextureVisAsset visAsset = new SurfaceTextureVisAsset();
                visAsset.Uuid = guid;
                visAsset.ImportTime = DateTime.Now;

                Texture2D texture = _fetcher.GetSurfaceTexture(guid);
                visAsset.Texture = texture;

                Texture2D normal = _fetcher.GetSurfaceNormalMap(guid);
                visAsset.NormalMap = normal;

                return visAsset;
            }
            return null;
        }
    }
}