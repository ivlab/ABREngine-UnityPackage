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

namespace IVLab.ABREngine
{
    /// <summary>
    /// Generic fetcher that supports fetching all types of VisAssets from a particular source.
    /// </summary>
    public interface IVisAssetFetcher
    {
        string GetArtifactJsonPath(Guid uuid);
        Task<JObject> GetArtifactJson(Guid uuid);
        Task<Texture2D> GetColormapTexture(Guid uuid);
        Task<GameObject> GetGlyphGameObject(Guid uuid, JObject lodJson);
        Task<Texture2D> GetGlyphNormalMapTexture(Guid uuid, JObject lodJson);
        Task<Texture2D> GetLineTexture(Guid uuid);
        Task<Texture2D> GetSurfaceTexture(Guid uuid);
        Task<Texture2D> GetSurfaceNormalMap(Guid uuid);
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

        public async Task<JObject> GetArtifactJson(Guid uuid)
        {
            if (!_artifactJsonCache.ContainsKey(uuid))
            {
                List<string> failed = await DownloadVisAsset(uuid);
                if (failed.Count == 0)
                {
                    _artifactJsonCache[uuid] = await _fpFetcher.GetArtifactJson(uuid);
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

        public async Task<Texture2D> GetColormapTexture(Guid uuid)
        {
            CheckExists(uuid);
            return await _fpFetcher.GetColormapTexture(uuid);
        }

        public async Task<GameObject> GetGlyphGameObject(Guid uuid, JObject lodInfo)
        {
            CheckExists(uuid);
            return await _fpFetcher.GetGlyphGameObject(uuid, lodInfo);
        }

        public async Task<Texture2D> GetGlyphNormalMapTexture(Guid uuid, JObject lodInfo)
        {
            CheckExists(uuid);
            return await _fpFetcher.GetGlyphNormalMapTexture(uuid, lodInfo);
        }

        public async Task<Texture2D> GetLineTexture(Guid uuid)
        {
            CheckExists(uuid);
            return await _fpFetcher.GetLineTexture(uuid);
        }

        public async Task<Texture2D> GetSurfaceTexture(Guid uuid)
        {
            CheckExists(uuid);
            return await _fpFetcher.GetSurfaceTexture(uuid);
        }

        public async Task<Texture2D> GetSurfaceNormalMap(Guid uuid)
        {
            CheckExists(uuid);
            return await _fpFetcher.GetSurfaceNormalMap(uuid);
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

            JObject artifactJson = await _fpFetcher.GetArtifactJson(uuid);

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

        public async Task<JObject> GetArtifactJson(Guid uuid)
        {
            if (!_artifactJsonCache.ContainsKey(uuid))
            {
                if (File.Exists(GetArtifactJsonPath(uuid)))
                {
                    StreamReader reader = new StreamReader(GetArtifactJsonPath(uuid));
                    JObject jsonData = JObject.Parse(await reader.ReadToEndAsync());
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

        public async Task<Texture2D> GetColormapTexture(Guid uuid)
        {
            JObject artifactJson = await GetArtifactJson(uuid);
            string relativeColormapPath = artifactJson["artifactData"]["colormap"].ToString();
            string colormapfilePath = VisAssetDataPath(GetArtifactJsonPath(uuid), relativeColormapPath);

            if (File.Exists(colormapfilePath))
            {
                return await UnityThreadScheduler.Instance.RunMainThreadWork(() => ColormapUtilities.ColormapFromFile(colormapfilePath, 1024, 100));
            }
            return null;
        }

        public async Task<GameObject> GetGlyphGameObject(Guid uuid, JObject lodJson)
        {
            string artifactJsonPath = GetArtifactJsonPath(uuid);
            var meshPath = VisAssetDataPath(artifactJsonPath, lodJson["mesh"].ToString());
            return await UnityThreadScheduler.Instance.RunMainThreadWork(() => new IVLab.OBJImport.OBJLoader().Load(meshPath, true));
        }

        public async Task<Texture2D> GetGlyphNormalMapTexture(Guid uuid, JObject lodJson)
        {
            string artifactJsonPath = GetArtifactJsonPath(uuid);
            var normalPath = VisAssetDataPath(artifactJsonPath, lodJson["normal"].ToString());
            var normalData = await Task.Run(() => File.ReadAllBytes(normalPath));
            return await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
            {
                // Textures exported by Blender are in Linear color space
                var normalMap = new Texture2D(2, 2, textureFormat: TextureFormat.RGBA32, mipChain: true, linear: true);
                normalMap.LoadImage(normalData);
                return normalMap;
            });
        }

        public async Task<Texture2D> GetLineTexture(Guid uuid)
        {
            JObject artifactJson = await GetArtifactJson(uuid);
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

            var textureData = await Task.Run(() => File.ReadAllBytes(texturePath));
            return await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
            {
                var texture = new Texture2D(2, 2);
                texture.LoadImage(textureData);
                return texture;
            });
        }

        public async Task<Texture2D> GetSurfaceTexture(Guid uuid)
        {
            JObject artifactJson = await GetArtifactJson(uuid);
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

            var textureData = await Task.Run(() => File.ReadAllBytes(texturePath));
            return await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
            {
                var texture = new Texture2D(2, 2);
                texture.LoadImage(textureData);
                return texture;
            });
        }

        public async Task<Texture2D> GetSurfaceNormalMap(Guid uuid)
        {
            JObject artifactJson = await GetArtifactJson(uuid);
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

            var textureData = await Task.Run(() => File.ReadAllBytes(texturePath));
            return await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
            {
                var texture = new Texture2D(2, 2);
                texture.LoadImage(textureData);
                return texture;
            });
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

        public async Task<JObject> GetArtifactJson(Guid uuid)
        {
            return await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
            {
                TextAsset artifactJson = Resources.Load<TextAsset>(GetArtifactJsonPath(uuid));
                if (artifactJson == null)
                {
                    return null;
                }
                return JObject.Parse(artifactJson.text);
            });
        }

        public async Task<Texture2D> GetColormapTexture(Guid uuid)
        {
            JObject artifactJson = await GetArtifactJson(uuid);
            string colormapfilePath = VisAssetDataPath(GetArtifactJsonPath(uuid), artifactJson["artifactData"]["colormap"].ToString());
            return await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
            {
                TextAsset colormapXML = Resources.Load<TextAsset>(colormapfilePath);
                Texture2D texture = ColormapUtilities.ColormapFromXML(colormapXML.text, 1024, 100);
                return texture;
            });
        }

        public async Task<GameObject> GetGlyphGameObject(Guid uuid, JObject lodJson)
        {
            var meshPath = VisAssetDataPath(GetArtifactJsonPath(uuid), lodJson["mesh"].ToString());
            return await UnityThreadScheduler.Instance.RunMainThreadWork(() => Resources.Load<GameObject>(meshPath));
        }

        public async Task<Texture2D> GetGlyphNormalMapTexture(Guid uuid, JObject lodJson)
        {
            var normalPath = VisAssetDataPath(GetArtifactJsonPath(uuid), lodJson["normal"].ToString());
            return await UnityThreadScheduler.Instance.RunMainThreadWork(() => Resources.Load<Texture2D>(normalPath));
        }

        public async Task<Texture2D> GetLineTexture(Guid uuid)
        {
            JObject artifactJson = await GetArtifactJson(uuid);
            var path = VisAssetDataPath(GetArtifactJsonPath(uuid), artifactJson["artifactData"]["horizontal"].ToString());
            return await UnityThreadScheduler.Instance.RunMainThreadWork(() => Resources.Load<Texture2D>(path));
        }

        public async Task<Texture2D> GetSurfaceTexture(Guid uuid)
        {
            JObject artifactJson = await GetArtifactJson(uuid);
            var path = VisAssetDataPath(GetArtifactJsonPath(uuid), artifactJson["artifactData"]["image"].ToString());
            return await UnityThreadScheduler.Instance.RunMainThreadWork(() => Resources.Load<Texture2D>(path));
        }

        public async Task<Texture2D> GetSurfaceNormalMap(Guid uuid)
        {
            JObject artifactJson = await GetArtifactJson(uuid);
            var path = VisAssetDataPath(GetArtifactJsonPath(uuid), artifactJson["artifactData"]["normalmap"].ToString());
            return await UnityThreadScheduler.Instance.RunMainThreadWork(() => Resources.Load<Texture2D>(path));
        }
    }


    /// <summary>
    ///     Fetch a VisAsset from the currently loaded ABR state. Currently only valid for ColormapVisAssets.
    /// </summary>
    public class StateLocalVisAssetFetcher : IVisAssetFetcher
    {
        // string GetArtifactJsonPath(Guid uuid);
        // Task<JObject> GetArtifactJson(Guid uuid);
        // Task<Texture2D> GetColormapTexture(Guid uuid);
        // Task<GameObject> GetGlyphGameObject(Guid uuid, JObject lodJson);
        // Task<Texture2D> GetGlyphNormalMapTexture(Guid uuid, JObject lodJson);
        // Task<Texture2D> GetLineTexture(Guid uuid);
        // Task<Texture2D> GetSurfaceTexture(Guid uuid);
        // Task<Texture2D> GetSurfaceNormalMap(Guid uuid);
        // public const string VISASSET_JSON = "artifact";
        // public const string RESOURCES_PATH = "media/visassets/";
        public const string VISASSET_STATE = "localVisAssets";
        public const string VISASSET_JSON = "artifactJson";
        public const string ARTIFACT_DATA = "artifactDataContents";

        public string GetArtifactJsonPath(Guid uuid)
        {
            throw new NotImplementedException("StateLocal VisAssets don't have a path");
        }

        public async Task<JObject> GetArtifactJson(Guid uuid)
        {
            return await Task.Run(() =>
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
            });
        }

        public async Task<Texture2D> GetColormapTexture(Guid uuid)
        {
            return await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
            {
                try
                {
                    JObject visAssets = ABREngine.Instance.VisAssets.LocalVisAssets;
                    if (!visAssets.ContainsKey(uuid.ToString()))
                    {
                        return null;
                    }
                    string colormapXML = visAssets[uuid.ToString()][ARTIFACT_DATA]["colormap.xml"].ToString();
                    Texture2D texture = ColormapUtilities.ColormapFromXML(colormapXML, 1024, 100);
                    return texture;
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return null;
                }
            });
        }

        public async Task<GameObject> GetGlyphGameObject(Guid uuid, JObject lodJson)
        {
            return await Task.Run(() => null as GameObject);
        }

        public async Task<Texture2D> GetGlyphNormalMapTexture(Guid uuid, JObject lodJson)
        {
            return await Task.Run(() => null as Texture2D);
        }

        public async Task<Texture2D> GetLineTexture(Guid uuid)
        {
            return await Task.Run(() => null as Texture2D);
        }

        public async Task<Texture2D> GetSurfaceTexture(Guid uuid)
        {
            return await Task.Run(() => null as Texture2D);
        }

        public async Task<Texture2D> GetSurfaceNormalMap(Guid uuid)
        {
            return await Task.Run(() => null as Texture2D);
        }
    }



    /// <summary>
    /// Use a particular fetcher to try and get the VisAsset from its source,
    /// and construct the requisite Unity objects and IVisAsset to add.
    /// </summary>
    public class VisAssetLoader
    {
        public VisAssetLoader() { }

        public async Task<IVisAsset> LoadVisAsset(Guid uuid)
        {
            return await LoadVisAsset(uuid, new ResourceVisAssetFetcher());
        }

        public async Task<IVisAsset> LoadVisAsset(Guid uuid, IVisAssetFetcher _fetcher)
        {
            JObject jsonData = null;
            try
            {
                jsonData = await _fetcher.GetArtifactJson(uuid);
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
                Debug.LogWarning(string.Format("VisAsset {0}: Use of field `artifactType` is deprecated. Use `type` instead.", guid.ToString().Substring(0, 8)));
            }

            if (type == "colormap")
            {
                ColormapVisAsset visAsset = new ColormapVisAsset();
                visAsset.Uuid = guid;
                visAsset.ImportTime = DateTime.Now;
                Texture2D colormapTexture = await _fetcher.GetColormapTexture(guid);
                visAsset.Texture = colormapTexture;
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
                    GameObject loadedObjGameObject = GameObject.Instantiate(await _fetcher.GetGlyphGameObject(guid, lodJson));
                    loadedObjGameObject.transform.SetParent(ABREngine.Instance.transform);
                    loadedObjGameObject.SetActive(false);
                    var loadedMesh = loadedObjGameObject.GetComponentInChildren<MeshFilter>().mesh;
                    GameObject.Destroy(loadedObjGameObject);
                    visAsset.MeshLods.Add(loadedMesh);

                    Texture2D normalMap = await _fetcher.GetGlyphNormalMapTexture(uuid, lodJson);
                    visAsset.NormalMapLods.Add(normalMap);
                }

                return visAsset;
            }

            if (type == "line")
            {
                LineTextureVisAsset visAsset = new LineTextureVisAsset();
                visAsset.Uuid = guid;
                visAsset.ImportTime = DateTime.Now;

                Texture2D texture = await _fetcher.GetLineTexture(guid);
                visAsset.Texture = texture;

                return visAsset;
            }

            if (type == "texture")
            {
                SurfaceTextureVisAsset visAsset = new SurfaceTextureVisAsset();
                visAsset.Uuid = guid;
                visAsset.ImportTime = DateTime.Now;

                Texture2D texture = await _fetcher.GetSurfaceTexture(guid);
                visAsset.Texture = texture;

                Texture2D normal = await _fetcher.GetSurfaceNormalMap(guid);
                visAsset.NormalMap = normal;

                return visAsset;
            }
            return null;
        }
    }
}