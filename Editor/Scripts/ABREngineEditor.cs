/* ABREngineEditor.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
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

#if UNITY_EDITOR

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using IVLab.ABREngine.ExtensionMethods;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Custom editor for the ABR Engine that displays:
    /// - Player status
    /// - Loaded VisAssets
    /// - Loaded Datasets, KeyData and Variables
    /// </summary>
    [CustomEditor(typeof(ABREngine))]
    public class ABREngineEditor : Editor
    {
        private bool configToggleState = false;
        private bool visassetToggleState = false;
        private bool datasetsToggleState = false;

        private int configIndex = 0;

        void OnEnable()
        {
            // "prime" the config - make sure the engine has loaded it
            ABRConfig config = ABREngine.ConfigPrototype;
            var configs = ScriptableObjectExtensions.GetAllInstances<ABRConfig>();
            if (configs.Count > 0 && config != null)
                configIndex = configs.FindIndex(cfg => cfg.name == config.name);
            else
                configIndex = 0;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            ABREngine script = (ABREngine) target;

            // Setup
            if (!EditorApplication.isPlaying || !ABREngine.Instance.IsInitialized)
            {
                // Display and update ABREngine Configs
                EditorGUILayout.LabelField("Choose ABR Configuration:");
                var configs = ScriptableObjectExtensions.GetAllInstances<ABRConfig>();
                if (configs.Count == 0)
                {
                    EditorGUILayout.HelpBox("No ABR configurations available! Please create one first. Assets/ABR/ABR Configuration.", MessageType.Error);
                }
                else
                {
                    int newIndex = EditorGUILayout.Popup(configIndex, configs.Select(c => c.name).ToArray());
                    if (newIndex != configIndex || ABREngine.ConfigPrototype == null)
                    {
                        configIndex = newIndex;
                        ABREngine.ConfigPrototype = configs[newIndex];
                        serializedObject.ApplyModifiedProperties();
                    }
                    if (GUILayout.Button("Open Current ABR Config..."))
                    {
                        AssetDatabase.OpenAsset(configs[configIndex]);
                    }
                }
                return;
            }

            EditorGUILayout.LabelField("ABR Engine is Running");

            List<Guid> visassets = ABREngine.Instance.VisAssets.GetVisAssets();
            visassetToggleState = EditorGUILayout.BeginFoldoutHeaderGroup(visassetToggleState, "VisAssets: " + visassets.Count);
            if (visassetToggleState)
            {
                EditorGUILayout.LabelField("Loaded VisAssets:");
                foreach (Guid uuid in visassets)
                {
                    IVisAsset va = null;
                    if (ABREngine.Instance.VisAssets.TryGetVisAsset(uuid, out va))
                    {
                        EditorGUILayout.LabelField("  " + uuid.ToString());
                        EditorGUILayout.LabelField("    Type: " + va.GetType());
                        GUILayoutOption[] previewOptions = {
                            GUILayout.Width(EditorGUIUtility.currentViewWidth),
                            GUILayout.Height(30)
                        };
                        // switch (va.VisAssetType)
                        // {
                        //     case VisAssetType.Colormap:
                        //         GUILayout.Box(((ColormapVisAsset) va).Texture, previewOptions);
                        //         break;
                        //     case VisAssetType.LineTexture:
                        //         GUILayout.Box(((LineTextureVisAsset) va).Texture, previewOptions);
                        //         break;
                        //     case VisAssetType.SurfaceTexture:
                        //         GUILayout.Box(((SurfaceTextureVisAsset) va).Texture, previewOptions);
                        //         break;
                        //     case VisAssetType.Glyph:
                        //         GUILayout.Label("[No preview]");
                        //         break;
                        // }
                    }
                    GUILayout.Space(10);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            List<Dataset> datasets = ABREngine.Instance.Data.GetDatasets();
            datasetsToggleState = EditorGUILayout.BeginFoldoutHeaderGroup(datasetsToggleState, "Datasets: " + datasets.Count);
            if (datasetsToggleState)
            {
                EditorGUILayout.LabelField("Loaded Datasets:");
                foreach (Dataset ds in datasets)
                {
                    EditorGUILayout.Separator();
                    EditorGUILayout.LabelField("----- " + ds.Path + " -----");
                    Dictionary<string, KeyData> allKeyData = ds.GetAllKeyData();
                    EditorGUILayout.LabelField("Key Data:");
                    foreach (KeyData kd in allKeyData.Values)
                    {
                        string keyData = "    " + DataPath.GetName(kd.Path);
                        RawDataset rawDs = null;
                        if (ABREngine.Instance.Data.TryGetRawDataset(kd.Path, out rawDs))
                        {
                            if (rawDs.vertexArray != null)
                            {
                                keyData += $"  ({rawDs.vertexArray.Length} vertices)";
                            }
                        }
                        EditorGUILayout.LabelField(keyData, EditorStyles.boldLabel);
                    }
                    EditorGUILayout.LabelField("Scalar Variables:");
                    foreach (ScalarDataVariable s in ds.GetAllScalarVars().Values)
                    {
                        EditorGUILayout.LabelField($"    {DataPath.GetName(s.Path)} [{s.Range.min}, {s.Range.max}]", EditorStyles.boldLabel);
                    }
                    EditorGUILayout.LabelField("Vector Variables:");
                    foreach (VectorDataVariable s in ds.GetAllVectorVars().Values)
                    {
                        EditorGUILayout.LabelField("    " + DataPath.GetName(s.Path), EditorStyles.boldLabel);
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Display currently loaded ABR Configuration
            configToggleState = EditorGUILayout.BeginFoldoutHeaderGroup(configToggleState, "ABR Configuration");
            if (configToggleState)
            {
                EditorGUILayout.TextArea(ABREngine.Instance.Config.ToString());
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.LabelField("Save State", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save State to .json file"))
            {
                string path = EditorUtility.SaveFilePanel(
                    "Save ABR state file as .json",
                    "",
                    "ABRState.json",
                    "json"
                );
                ABREngine.Instance.SaveState<PathStateFileLoader>(path);
                Debug.Log("Saved state JSON to " + path);
            }

            if (ABREngine.Instance.Config.dataServerUrl.Length > 0)
            {
                if (GUILayout.Button("Save State to Server"))
                {
                    ABREngine.Instance.SaveState<HttpStateFileLoader>();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Options for ABREngine", EditorStyles.boldLabel);
            if (GUILayout.Button("Render"))
            {
                ABREngine.Instance.Render();
            }
        }
    }
}
#endif