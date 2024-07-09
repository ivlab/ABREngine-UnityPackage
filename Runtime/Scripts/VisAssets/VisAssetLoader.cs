/* VisAssetLoader.cs
 *
 * Copyright (c) 2021, University of Minnesota
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

using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using UnityEngine;

using Newtonsoft.Json.Linq;
using IVLab.Utilities;
using UnityEditor;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Generic fetcher that supports fetching all types of VisAssets from a particular source.
    /// </summary>
    public interface IVisAssetFetcher
    {
        string GetArtifactJsonPath(Guid uuid);
        JObject GetArtifactJson(Guid uuid);
        [Obsolete("GetColormapTexture() is obsolete; use GetColormap() instead")]
        Texture2D GetColormapTexture(Guid uuid);
        Colormap GetColormap(Guid uuid);
        GameObject GetGlyphGameObject(Guid uuid, JObject lodJson);
        Texture2D GetGlyphNormalMapTexture(Guid uuid, JObject lodJson);
        Texture2D GetGlyphPreview(Guid uuid);
        Texture2D GetLineTexture(Guid uuid);
        Texture2D GetSurfaceTexture(Guid uuid);
        Texture2D GetSurfaceNormalMap(Guid uuid);
    }

    /// <summary>
    /// Fetch VisAssets from a URL via HTTP
    /// </summary>
    public class HttpVisAssetFetcher : IVisAssetFetcher
    {
        private string _serverUrl;
        private string _appDataPath;
        private FilePathVisAssetFetcher _fpFetcher;
        private Dictionary<Guid, JObject> _artifactJsonCache = new Dictionary<Guid, JObject>();
        public string VisAssetJson { get; }

        public HttpVisAssetFetcher(string serverUrl, string appDataPath)
        {
            VisAssetJson = ABRConfig.Consts.VisAssetJson;
            _serverUrl = serverUrl;
            if (!_serverUrl.EndsWith("/"))
            {
                serverUrl += "/";
            }
            _appDataPath = appDataPath;
            _fpFetcher = new FilePathVisAssetFetcher(appDataPath);
        }

        public string GetArtifactJsonPath(Guid uuid)
        {
            return GetArtifactPath(uuid) +  "/" + VisAssetJson;
        }

        public string GetArtifactPath(Guid uuid)
        {
            return _serverUrl + "/" + uuid.ToString();
        }

        public string GetLocalArtifactJsonPath(Guid uuid)
        {
            return Path.Combine(
                _appDataPath,
                uuid.ToString(),
                VisAssetJson
            );
        }

        public JObject GetArtifactJson(Guid uuid)
        {
            if (!_artifactJsonCache.ContainsKey(uuid))
            {
                List<string> failed = DownloadVisAsset(uuid).Result;
                if (failed.Count == 0)
                {
                    _artifactJsonCache[uuid] = _fpFetcher.GetArtifactJson(uuid);
                }
                else
                {
                    Debug.LogErrorFormat("Failed to download {0} files from VisAsset {1}", string.Join(", ", failed), uuid.ToString());
                    return null;
                }
            }
            return _artifactJsonCache[uuid];
        }

        private void CheckExists(Guid uuid)
        {
            if (!_artifactJsonCache.ContainsKey(uuid))
            {
                throw new Exception("VisAsset not downloaded yet. GetArtifactJson first.");
            }
        }

        public Texture2D GetColormapTexture(Guid uuid)
        {
            CheckExists(uuid);
            return _fpFetcher.GetColormapTexture(uuid);
        }

        public Colormap GetColormap(Guid uuid)
        {
            CheckExists(uuid);
            return _fpFetcher.GetColormap(uuid);
        }

        public GameObject GetGlyphGameObject(Guid uuid, JObject lodInfo)
        {
            CheckExists(uuid);
            return _fpFetcher.GetGlyphGameObject(uuid, lodInfo);
        }

        public Texture2D GetGlyphNormalMapTexture(Guid uuid, JObject lodInfo)
        {
            CheckExists(uuid);
            return _fpFetcher.GetGlyphNormalMapTexture(uuid, lodInfo);
        }

        public Texture2D GetGlyphPreview(Guid uuid)
        {
            CheckExists(uuid);
            return _fpFetcher.GetGlyphPreview(uuid);
        }

        public Texture2D GetLineTexture(Guid uuid)
        {
            CheckExists(uuid);
            return _fpFetcher.GetLineTexture(uuid);
        }

        public Texture2D GetSurfaceTexture(Guid uuid)
        {
            CheckExists(uuid);
            return _fpFetcher.GetSurfaceTexture(uuid);
        }

        public Texture2D GetSurfaceNormalMap(Guid uuid)
        {
            CheckExists(uuid);
            return _fpFetcher.GetSurfaceNormalMap(uuid);
        }

        // Mirrors the download_visasset function from abr_server
        private async Task<List<string>> DownloadVisAsset(Guid uuid)
        {
            Debug.LogFormat("Downloading VisAsset {0} from {1}", uuid, this._serverUrl);
            string artifactJsonPath = GetLocalArtifactJsonPath(uuid);
            string vaPath = Path.GetDirectoryName(artifactJsonPath);

            string artifactJsonUrl = GetArtifactJsonPath(uuid);
            string vaUrl = GetArtifactPath(uuid);
            if (!vaUrl.EndsWith("/"))
            {
                vaUrl += "/";
            }

            // Download the Artifact JSON
            List<string> failed = new List<string>();
            bool success = await CheckExistsAndDownload(artifactJsonUrl, artifactJsonPath);
            if (!success)
            {
                failed.Add(VisAssetJson);
                return failed; // Cannot continue without artifact json
            }

            JObject artifactJson = _fpFetcher.GetArtifactJson(uuid);

            // Download the thumbnail
            string previewImg = artifactJson["preview"].ToString();
            string previewPath = Path.Combine(vaPath, previewImg);
            success = await CheckExistsAndDownload(vaUrl + previewImg, previewPath);
            if (!success)
            {
                failed.Add(previewImg);
            }

            // Get all the files specified in artifactData
            List<string> allFiles = new List<string>();
            GetAllStringsFromJson(artifactJson["artifactData"], allFiles);

            // Download all files
            foreach (string file in allFiles)
            {
                bool got = await CheckExistsAndDownload(vaUrl + file, Path.Combine(vaPath, file));
                if (!got)
                {
                    failed.Add(file);
                }
            }
            return failed;
        }

        private async Task<bool> CheckExistsAndDownload(string url, string outputPath)
        {
            if (!File.Exists(outputPath) && !Directory.Exists(outputPath))
            {
                return await DownloadFile(url, outputPath);
            }
            else
            {
                return true;
            }
        }

        private async Task<bool> DownloadFile(string url, string outputPath)
        {
            try
            {
                HttpResponseMessage resp = await ABREngine.httpClient.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                byte[] outText = await resp.Content.ReadAsByteArrayAsync();
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                using (FileStream writer = new FileStream(outputPath, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    await writer.WriteAsync(outText, 0, outText.Length);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void GetAllStringsFromJson(JToken jsonObject, List<string> stringList)
        {
            if (jsonObject.Type == JTokenType.String)
            {
                stringList.Add(jsonObject.ToString());
            }
            else if (jsonObject.Type == JTokenType.Array)
            {
                foreach (var j in jsonObject)
                {
                    GetAllStringsFromJson(j, stringList);
                }
            }
            else if (jsonObject.Type == JTokenType.Object)
            {
                foreach (var j in jsonObject.Values())
                {
                    GetAllStringsFromJson(j, stringList);
                }
            }
        }
    }





    /// <summary>
    /// Fetch VisAsset from somewhere on local disk
    /// </summary>
    public class FilePathVisAssetFetcher : IVisAssetFetcher
    {
        private Dictionary<Guid, JObject> _artifactJsonCache = new Dictionary<Guid, JObject>();
        private string _appDataPath;
        public string VisAssetJson { get; }

        public FilePathVisAssetFetcher(string appDataPath)
        {
            _appDataPath = appDataPath;
            VisAssetJson = ABRConfig.Consts.VisAssetJson;
        }

        public string GetArtifactJsonPath(Guid uuid)
        {
            return Path.Combine(
                _appDataPath,
                uuid.ToString(),
                VisAssetJson
            );
        }

        private string VisAssetDataPath(string artifactFilePath, string relativeDataPath)
        {
            return Path.Combine(Path.GetDirectoryName(artifactFilePath), relativeDataPath);
        }

        public JObject GetArtifactJson(Guid uuid)
        {
            if (!_artifactJsonCache.ContainsKey(uuid))
            {
                if (File.Exists(GetArtifactJsonPath(uuid)))
                {
                    StreamReader reader = new StreamReader(GetArtifactJsonPath(uuid));
                    JObject jsonData = JObject.Parse(reader.ReadToEnd());
                    reader.Close();
                    _artifactJsonCache[uuid] = jsonData;
                    return jsonData;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return _artifactJsonCache[uuid];
            }
        }

        public Texture2D GetColormapTexture(Guid uuid)
        {
            JObject artifactJson = GetArtifactJson(uuid);
            string relativeColormapPath = artifactJson["artifactData"]["colormap"].ToString();
            string colormapfilePath = VisAssetDataPath(GetArtifactJsonPath(uuid), relativeColormapPath);

            if (File.Exists(colormapfilePath))
            {
                return Colormap.FromXMLFile(colormapfilePath).ToTexture2D(1024, 100);
            }
            return null;
        }

        public Colormap GetColormap(Guid uuid)
        {
            JObject artifactJson = GetArtifactJson(uuid);
            string relativeColormapPath = artifactJson["artifactData"]["colormap"].ToString();
            string colormapfilePath = VisAssetDataPath(GetArtifactJsonPath(uuid), relativeColormapPath);

            if (File.Exists(colormapfilePath))
            {
                return Colormap.FromXMLFile(colormapfilePath);
            }
            return null;
        }

        public GameObject GetGlyphGameObject(Guid uuid, JObject lodJson)
        {
            string artifactJsonPath = GetArtifactJsonPath(uuid);
            var meshPath = VisAssetDataPath(artifactJsonPath, lodJson["mesh"].ToString());
            return new IVLab.OBJImport.OBJLoader().Load(meshPath, true);
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

        public Texture2D GetGlyphPreview(Guid uuid)
        {
            JObject artifactJson = GetArtifactJson(uuid);
            string artifactJsonPath = GetArtifactJsonPath(uuid);
            string previewPath = VisAssetDataPath(artifactJsonPath, artifactJson["preview"].ToString());
            if (File.Exists(previewPath))
            {
                byte[] pngBytes = File.ReadAllBytes(previewPath);
                Texture2D preview = new Texture2D(2, 2);
                preview.LoadImage(pngBytes);
                return preview;
            }
            return null;
        }

        public Texture2D GetLineTexture(Guid uuid)
        {
            JObject artifactJson = GetArtifactJson(uuid);
            string texturePath = "";
            try
            {
                texturePath = VisAssetDataPath(GetArtifactJsonPath(uuid), artifactJson["artifactData"]["horizontal"].ToString());
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
            JObject artifactJson = GetArtifactJson(uuid);
            string texturePath = "";
            try
            {
                texturePath = VisAssetDataPath(GetArtifactJsonPath(uuid), artifactJson["artifactData"]["image"].ToString());
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
            JObject artifactJson = GetArtifactJson(uuid);
            string texturePath = "";
            try
            {
                texturePath = VisAssetDataPath(GetArtifactJsonPath(uuid), artifactJson["artifactData"]["normalmap"].ToString());
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





    /// <summary>
    /// Fetch VisAsset from a Resources folder (in an Asset or Package)
    /// </summary>
    public class ResourceVisAssetFetcher : IVisAssetFetcher
    {
        public string VisAssetJson { get; }
        public string ResourcePath { get; }

        public ResourceVisAssetFetcher()
        {
            int dotIndex = ABRConfig.Consts.VisAssetJson.IndexOf('.');
            VisAssetJson = ABRConfig.Consts.VisAssetJson.Substring(0, dotIndex);
            ResourcePath = Path.Combine(ABRConfig.Consts.MediaFolder, ABRConfig.Consts.VisAssetFolder);
        }

        private string VisAssetDataPath(string artifactFilePath, string relativeDataPath)
        {
            string relPathNoExt = Path.GetFileNameWithoutExtension(relativeDataPath);
            relPathNoExt = Path.Combine(Path.GetDirectoryName(relativeDataPath), relPathNoExt);
            return Path.Combine(Path.GetDirectoryName(artifactFilePath), relPathNoExt);
        }

        public string GetArtifactJsonPath(Guid uuid)
        {
            return Path.Combine(
                ResourcePath,
                uuid.ToString(),
                VisAssetJson
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
            JObject artifactJson = GetArtifactJson(uuid);
            string colormapfilePath = VisAssetDataPath(GetArtifactJsonPath(uuid), artifactJson["artifactData"]["colormap"].ToString());
            TextAsset colormapXML = Resources.Load<TextAsset>(colormapfilePath);
            return Colormap.FromXML(colormapXML.text).ToTexture2D(1024, 100);
        }

        public Colormap GetColormap(Guid uuid)
        {
            JObject artifactJson = GetArtifactJson(uuid);
            string colormapfilePath = VisAssetDataPath(GetArtifactJsonPath(uuid), artifactJson["artifactData"]["colormap"].ToString());
            TextAsset colormapXML = Resources.Load<TextAsset>(colormapfilePath);
            return Colormap.FromXML(colormapXML.text);
        }

        public GameObject GetGlyphGameObject(Guid uuid, JObject lodJson)
        {
            var meshPath = VisAssetDataPath(GetArtifactJsonPath(uuid), lodJson["mesh"].ToString());
            return Resources.Load<GameObject>(meshPath);
        }

        public Texture2D GetGlyphNormalMapTexture(Guid uuid, JObject lodJson)
        {
            var normalPath = VisAssetDataPath(GetArtifactJsonPath(uuid), lodJson["normal"].ToString());
            return Resources.Load<Texture2D>(normalPath);
        }

        public Texture2D GetGlyphPreview(Guid uuid)
        {
            JObject artifactJson = GetArtifactJson(uuid);
            string artifactJsonPath = GetArtifactJsonPath(uuid);
            string previewPath = VisAssetDataPath(artifactJsonPath, artifactJson["preview"].ToString());
            return Resources.Load<Texture2D>(previewPath);
        }

        public Texture2D GetLineTexture(Guid uuid)
        {
            JObject artifactJson = GetArtifactJson(uuid);
            var path = VisAssetDataPath(GetArtifactJsonPath(uuid), artifactJson["artifactData"]["horizontal"].ToString());
            return Resources.Load<Texture2D>(path);
        }

        public Texture2D GetSurfaceTexture(Guid uuid)
        {
            JObject artifactJson = GetArtifactJson(uuid);
            var path = VisAssetDataPath(GetArtifactJsonPath(uuid), artifactJson["artifactData"]["image"].ToString());
            return Resources.Load<Texture2D>(path);
        }

        public Texture2D GetSurfaceNormalMap(Guid uuid)
        {
            JObject artifactJson = GetArtifactJson(uuid);
            var path = VisAssetDataPath(GetArtifactJsonPath(uuid), artifactJson["artifactData"]["normalmap"].ToString());
            return Resources.Load<Texture2D>(path);
        }
    }


    /// <summary>
    ///     Fetch a VisAsset from the currently loaded ABR state. Currently only valid for ColormapVisAssets.
    /// </summary>
    public class StateLocalVisAssetFetcher : IVisAssetFetcher
    {
        public const string VISASSET_STATE = "localVisAssets";
        public const string VISASSET_JSON = "artifactJson";
        public const string ARTIFACT_DATA = "artifactDataContents";

        public string GetArtifactJsonPath(Guid uuid)
        {
            throw new NotImplementedException("StateLocal VisAssets don't have a path");
        }

        public JObject GetArtifactJson(Guid uuid)
        {
            try
            {
                JObject visAssets = ABREngine.Instance.VisAssets.LocalVisAssets;
                if (visAssets == null || !visAssets.ContainsKey(uuid.ToString()))
                {
                    return null as JObject;
                }
                return visAssets[uuid.ToString()][VISASSET_JSON].ToObject<JObject>();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return null as JObject;
            }
        }

        public Texture2D GetColormapTexture(Guid uuid)
        {
            try
            {
                JObject visAssets = ABREngine.Instance.VisAssets.LocalVisAssets;
                if (!visAssets.ContainsKey(uuid.ToString()))
                {
                    return null;
                }
                string colormapXML = visAssets[uuid.ToString()][ARTIFACT_DATA]["colormap.xml"].ToString();
                Texture2D texture = Colormap.FromXML(colormapXML).ToTexture2D(1024, 100);
                return texture;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return null;
            }
        }

        public Colormap GetColormap(Guid uuid)
        {
            try
            {
                JObject visAssets = ABREngine.Instance.VisAssets.LocalVisAssets;
                if (!visAssets.ContainsKey(uuid.ToString()))
                {
                    return null;
                }
                string colormapXML = visAssets[uuid.ToString()][ARTIFACT_DATA]["colormap.xml"].ToString();
                return Colormap.FromXML(colormapXML);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return null;
            }
        }

        public GameObject GetGlyphGameObject(Guid uuid, JObject lodJson)
        {
            return null;
        }

        public Texture2D GetGlyphNormalMapTexture(Guid uuid, JObject lodJson)
        {
            return null;
        }

        public Texture2D GetGlyphPreview(Guid uuid)
        {
            return null;
        }


        public Texture2D GetLineTexture(Guid uuid)
        {
            return null;
        }

        public Texture2D GetSurfaceTexture(Guid uuid)
        {
            return null;
        }

        public Texture2D GetSurfaceNormalMap(Guid uuid)
        {
            return null;
        }
    }



    /// <summary>
    /// Use a particular fetcher to try and get the VisAsset from its source,
    /// and construct the requisite Unity objects and IVisAsset to add.
    /// </summary>
    public class VisAssetLoader
    {
        public VisAssetLoader() { }

        public IVisAsset LoadVisAsset(Guid uuid)
        {
            return LoadVisAsset(uuid, new ResourceVisAssetFetcher());
        }

        public IVisAsset LoadVisAsset(Guid uuid, IVisAssetFetcher _fetcher)
        {
            JObject jsonData = null;
            try
            {
                jsonData = _fetcher.GetArtifactJson(uuid);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

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
                // Removing warning until ABR Compose is updated to follow this advice so users do not think they have
                // done something wrong.
                //Debug.LogWarning(string.Format("VisAsset {0}: Use of field `artifactType` is deprecated. Use `type` instead.", guid.ToString().Substring(0, 8)));
            }

            if (!VisAsset.IsValidVisAssetType(type))
            {
                Debug.LogError($"`{type}` is not a valid VisAsset type");
                return null;
            }

            if (type == "colormap")
            {
                Colormap cmap = _fetcher.GetColormap(guid);
                ColormapVisAsset visAsset = new ColormapVisAsset(guid, cmap);
                return visAsset;
            }

            if (type == "glyph")
            {
                var artifactData = jsonData["artifactData"];
                List<JObject> lodsList = null;
                List<Mesh> meshLods = new List<Mesh>();
                List<Texture2D> normalMapLods = new List<Texture2D>();
                Texture2D preview = null;
                try
                {
                    lodsList = artifactData["lods"].ToObject<List<JObject>>();
                }
                catch (ArgumentException)
                {
                    if (artifactData is JArray)
                    {
                        // Removing warning - just handle both cases silently
                        // Debug.LogWarning(string.Format(
                        //     "VisAsset {0}: Use of bare array in `artifactData` is deprecated. Put the array inside an object.",
                        //     guid.ToString().Substring(0, 8)
                        // ));
                        lodsList = artifactData.ToObject<List<JObject>>();
                    }
                }
                foreach (JObject lodJson in lodsList)
                {
                    GameObject prefab = _fetcher.GetGlyphGameObject(guid, lodJson);
                    GameObject loadedObjGameObject = GameObject.Instantiate(prefab);
                    loadedObjGameObject.transform.SetParent(ABREngine.Instance.transform);
                    var loadedMesh = loadedObjGameObject.GetComponentInChildren<MeshFilter>().mesh;
                    GameObject.Destroy(loadedObjGameObject);
                    try
                    {
                        // Only destroy if not imported from Resources, and fail
                        // silently if something goes wrong during destroying
                        // the prefab. It'll just look weird in the scene.
#if UNITY_EDITOR
                        if (!AssetDatabase.Contains(prefab))
#endif
                        {
                            GameObject.Destroy(prefab);
                        }
                    }
                    catch (Exception) { }
                    meshLods.Add(loadedMesh);

                    try
                    {
                        // Normal maps are not critical to the operation of ABR;
                        // so silently accept when they're not present
                        Texture2D normalMap = _fetcher.GetGlyphNormalMapTexture(uuid, lodJson);
                        normalMapLods.Add(normalMap);
                    }
                    catch (Exception) { }

                    try
                    {
                        // previews also not critical, silently accept any errors
                        preview = _fetcher.GetGlyphPreview(uuid);
                    }
                    catch { }
                }
                GlyphVisAsset visAsset = new GlyphVisAsset(guid, meshLods, normalMapLods, preview);
                return visAsset;
            }

            if (type == "line")
            {
                Texture2D texture = _fetcher.GetLineTexture(guid);
                LineTextureVisAsset visAsset = new LineTextureVisAsset(guid, texture);
                return visAsset;
            }

            if (type == "texture")
            {
                Texture2D texture = _fetcher.GetSurfaceTexture(guid);
                Texture2D normal = _fetcher.GetSurfaceNormalMap(guid);
                SurfaceTextureVisAsset visAsset = new SurfaceTextureVisAsset(guid, texture, normal);
                return visAsset;
            }
            return null;
        }
    }
}