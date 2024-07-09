/* InitializeLayers.cs
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

// Inspiration from: https://forum.unity.com/threads/adding-layer-by-script.41970/

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Convenience class for ABR Developers - automatically creates the various
    /// layers needed for the ABREngine when the Engine is imported.
    /// </summary>
    [InitializeOnLoad]
    public class InitializeLayers
    {
        static InitializeLayers()
        {
            // Iterate through all the plate types and find out the layers that are needed
            List<string> layerNamesToAdd = new List<string>();
            var assembly = Assembly.GetExecutingAssembly();
            Type dataImpressionType = typeof(DataImpression);
            List<Type> impressionTypes = assembly.GetTypes()
                .Where((t) => t.IsSubclassOf(dataImpressionType) && t != dataImpressionType)
                .Select((t) => Type.GetType(t?.FullName))
                .ToList();

            foreach (Type plate in impressionTypes)
            {
                // Assumes that there's a field named "LayerName" in the data impression
                FieldInfo layerNameProp = plate.GetField("LayerName", BindingFlags.NonPublic | BindingFlags.Static);
                string layerNameString = layerNameProp?.GetValue(null).ToString() ?? "ABR_Layer";
                layerNamesToAdd.Add(layerNameString);
            }

            foreach (string name in layerNamesToAdd)
            {
                int layerID = LayerMask.NameToLayer(name);
                if (layerID < 0)
                {
                    int actualLayer = CreateLayer(name);
                    Debug.LogFormat("Created new layer for ABR (layer {0}): `{1}`", actualLayer, name);
                }
            }
        }

        /// <summary>
        /// Create a new layer in this Unity project at the first available slot
        /// </summary>
        public static int CreateLayer(string layerName)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty it = tagManager.GetIterator();
            bool showChildren = true;
            int actualLayer = -1;
            while (it.NextVisible(showChildren) && actualLayer < 0)
            {
                if (it.name == "layers")
                {
                    int numLayers = it.arraySize;
                    // Unity 2019: Layers < 8 are not user-definable
                    for (int i = 8; i < numLayers && actualLayer < 0; i++)
                    {
                        SerializedProperty element = it.GetArrayElementAtIndex(i);
                        // First empty element of layers list
                        if (element.stringValue.Length == 0)
                        {
                            element.stringValue = layerName;
                            actualLayer = i;
                        }
                    }
                }
            }
            tagManager.ApplyModifiedProperties();
            return actualLayer;
        }
    }
}
#endif