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
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

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

        public override void OnInspectorGUI()
        {
            // Setup
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField("ABR Engine is Paused");
                TextAsset configFile = Resources.Load<TextAsset>(ABRConfig.Consts.ConfigFile);
                if (configFile != null)
                {
                    EditorGUILayout.LabelField("Found config: " + ABRConfig.Consts.ConfigFile);
                }
                else
                {
                    EditorGUILayout.LabelField("No config found");
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
                        EditorGUILayout.LabelField("    Type: " + va.VisAssetType);
                        GUILayoutOption[] previewOptions = {
                            GUILayout.Width(EditorGUIUtility.currentViewWidth),
                            GUILayout.Height(30)
                        };
                        switch (va.VisAssetType)
                        {
                            case VisAssetType.Colormap:
                                GUILayout.Box(((ColormapVisAsset) va).Gradient, previewOptions);
                                break;
                            case VisAssetType.LineTexture:
                                GUILayout.Box(((LineTextureVisAsset) va).Texture, previewOptions);
                                break;
                            case VisAssetType.SurfaceTexture:
                                GUILayout.Box(((SurfaceTextureVisAsset) va).Texture, previewOptions);
                                break;
                            case VisAssetType.Glyph:
                                GUILayout.Label("[No preview]");
                                break;
                        }
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
                    EditorGUILayout.LabelField(ds.Path);
                    Dictionary<string, IKeyData> allKeyData = ds.GetAllKeyData();
                    foreach (IKeyData kd in allKeyData.Values)
                    {
                        EditorGUILayout.LabelField("  " + DataPath.GetName(kd.Path));
                        RawDataset rawDs = null;
                        if (ABREngine.Instance.Data.TryGetRawDataset(kd.Path, out rawDs))
                        {
                            EditorGUILayout.LabelField($"  {rawDs.vertexArray.Length} vertices");
                        }
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Display currently loaded ABR Configuration
            configToggleState = EditorGUILayout.BeginFoldoutHeaderGroup(configToggleState, "ABR Configuration");
            if (configToggleState)
            {
                EditorGUILayout.TextArea(ABREngine.Instance.Config.Info.ToString());
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
#endif