/* VisAssetManager.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 */

using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace IVLab.ABREngine {
    public class VisAssetManager : IVLab.Utilities.Singleton<VisAssetManager>
    {
        public string appDataPath;

        public const string VISASSET_JSON = "artifact.json";

        private Dictionary<Guid, IVisAsset> _visAssets = new Dictionary<Guid, IVisAsset>();

        void Start() {
            appDataPath = Path.Combine(Application.persistentDataPath, "media", "visassets");
            Directory.CreateDirectory(appDataPath);
            LoadVisAssetPalette();
        }

        public void LoadVisAssetPalette()
        {
            string[] files = Directory.GetFiles(appDataPath, VISASSET_JSON, SearchOption.AllDirectories);

            foreach (var filePath in files)
            {
                LoadVisAsset(filePath);
            }
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

        public void LoadVisAsset(string filePath, bool replaceExisting=false)
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
                if (!replaceExisting)
                {
                    visAsset = new ColormapVisAsset();
                    visAsset.Uuid = guid;
                }
                else
                {
                    visAsset = _visAssets[guid] as ColormapVisAsset;
                }
                visAsset.ImportTime = DateTime.Now;

                try
                {
                    string relativeColormapPath = "colormap.xml";//jsonData["artifactData"]["image"].str;

                    string colormapfilePath = Path.Combine(Path.GetDirectoryName(filePath), relativeColormapPath);
                    Texture2D texture = null;

                    if (File.Exists(colormapfilePath)) // ERROR: The name 'File' does not exist in the current context?
                    {
                        texture = ColormapUtilities.ColormapFromFile(colormapfilePath);
                        visAsset.Gradient = texture;
                    }
                }
                catch (System.Exception e) { 
                    Debug.LogError("VisAsset not suppported yet: " + type + " (" + jsonData["uuid"].ToString() + ")");
                    Debug.LogError(e);
                }

            }

        //     if (type == "glyph")
        //     {
        //         GlyphVisAsset visAsset = new GlyphVisAsset();
        //         visAsset.Uuid = guid;
        //         visAsset.ImportTime = DateTime.Now;

        //         var artifactData = jsonData["artifactData"];
        //         try
        //         {
        //             JObject lodsList = null;

        //             if (artifactData.ContainsKey("lods"))
        //             {
        //                 lodsList = artifactData.GetField("lods");

        //             } else if(artifactData.IsArray)
        //             {
        //                 lodsList = artifactData;
        //             }

        //             if (lodsList != null)
        //             {

        //                 foreach (var lodJson in lodsList.list)
        //                 {
        //                     var meshPath = ArtifactDataPath(filePath, lodJson["mesh"].ToString());
        //                     var normalPath = ArtifactDataPath(filePath, lodJson["normal"].ToString());
        //                     Debug.Log("Loading mesh: " + meshPath);
        //                     GameObject loadedObjGameObject = null;
        //                     try
        //                     {
        //                         loadedObjGameObject = new OBJLoader().Load(meshPath);
        //                     }
        //                     catch (System.Exception e)
        //                     {
        //                         Debug.Log(e);
        //                     }
        //                     Debug.Log(loadedObjGameObject);
        //                     loadedObjGameObject.transform.SetParent(transform);
        //                     loadedObjGameObject.SetActive(false);
        //                     var loadedMesh = loadedObjGameObject.GetComponentInChildren<MeshFilter>().mesh;
        //                     Debug.Log(loadedMesh);
        //                     GameObject.Destroy(loadedObjGameObject);

        //                     var normalData = File.ReadAllBytes(normalPath); // ERROR: The name 'File' does not exist in the current context?
        //                     var normalMap = new Texture2D(2, 2);
        //                     normalMap.LoadImage(normalData);


        //                     visAsset.MeshLods.Add(loadedMesh);
        //                     visAsset.NormalMapLods.Add(normalMap);

        //                 }
        //             }
        //             else
        //             {
        //                 throw (new System.Exception());
        //             }

        //             visAsset.ReceiveMessage(new ABRUpdateMessage { Type = ABRUpdateMessage.UpdateType.Unknown });

        //         }
        //         catch (System.Exception e) { Debug.Log("VisAsset not suppported yet: " + type + " (" + jsonData["uuid"].ToString() + ")"); }
        //     }
        //     if (type == "line")
        //     {
        //         LineTextureVisAsset visAsset = ABRManager.CreateNode<LineTextureVisAsset>(new System.Guid(jsonData["uuid"].ToString()));
        //         visAsset.ImportTime = DateTime.Now;

        //         ABRManager.Instance.SetNodeLabel(visAsset, jsonData["name"]?.ToString() ?? "");

        //         var artifactData = jsonData["artifactData"];
        //         try
        //         {
        //             if (artifactData.ContainsKey("horizontal"))
        //             {
        //                 var texturePath = ArtifactDataPath(filePath, artifactData["horizontal"].ToString());

        //                 var textureData = File.ReadAllBytes(texturePath); // ERROR: The name 'File' does not exist in the current context?
        //                 var texture = new Texture2D(2, 2);
        //                 texture.LoadImage(textureData);

        //                 visAsset.TextureArray = new Texture2D[] { texture };
        //             }
        //             else
        //             {
        //                 throw (new System.Exception());
        //             }
        //         }
        //         catch (System.Exception e) { Debug.Log("VisAsset not suppported yet: " + type + " (" + jsonData["uuid"].ToString() + ")"); }
        //     }


        //     if (type == "texture")
        //     {
        //         SurfaceTextureVisAsset visAsset = ABRManager.CreateNode<SurfaceTextureVisAsset>(new System.Guid(jsonData["uuid"].ToString()));
        //         visAsset.ImportTime = DateTime.Now;

        //         ABRManager.Instance.SetNodeLabel(visAsset, jsonData["name"]?.ToString() ?? "");

        //         var artifactData = jsonData["artifactData"];
        //         try
        //         {
        //             if (artifactData.ContainsKey("image"))
        //             {
        //                 var texturePath = ArtifactDataPath(filePath, artifactData["image"].ToString());

        //                 var textureData = File.ReadAllBytes(texturePath); // ERROR: The name 'File' does not exist in the current context?
        //                 var texture = new Texture2D(2, 2);
        //                 texture.LoadImage(textureData);

        //                 visAsset.TextureArray = new Texture2D[] { texture };


        //                 var normalPath = ArtifactDataPath(filePath, artifactData["normalmap"].ToString());

        //                 var normalData = File.ReadAllBytes(normalPath); // ERROR: The name 'File' does not exist in the current context?
        //                 var normal = new Texture2D(2, 2);
        //                 normal.LoadImage(normalData);

        //                 visAsset.NormalMapArray = new Texture2D[] { normal };
        //             }
        //             else
        //             {
        //                 throw (new System.Exception());
        //             }
        //         }
        //         catch (System.Exception e) { Debug.Log("VisAsset not suppported yet: " + jsonData["name"]?.ToString() ?? "Untitled" + " (" + jsonData["uuid"].ToString() + ")"); }
        //     }
        }
    }
}