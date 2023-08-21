/* DataImpressionEditor.cs
 *
 * Copyright (c) 2023 University of Minnesota
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

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using IVLab.Utilities;
using PlasticGui;

namespace IVLab.ABREngine
{
    // Apply same custom editor for all data impression types
    [CustomEditor(typeof(DataImpression), true)]
    public class DataImpressionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DataImpression di = (DataImpression)target;

            EditorGUILayout.LabelField($"Data Impression: {di.name}", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Parameters:", EditorStyles.boldLabel);

            foreach (string inputName in di.InputIndexer.InputNames)
            {
                var value = di.InputIndexer.GetInputValue(inputName);
                EditorGUILayout.LabelField($"    - {inputName}:");
                if (value != null)
                {
                    ParameterField(di, value.GetRawABRInput());
                }
                else
                {
                    EditorGUILayout.LabelField($"        [None]");
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ParameterField(DataImpression di, RawABRInput input)
        {
            if (input.inputType == typeof(KeyData).ToString())
            {

            }
            else if (input.inputType == typeof(ColormapVisAsset).ToString())
            {
                ColormapField(input);
            }
            else
            {
                EditorGUILayout.LabelField("        " + input.inputValue);
            }
        }

        private void ColormapField(RawABRInput input)
        {
            Colormap cmap = ABREngine.Instance.VisAssets.GetVisAsset<ColormapVisAsset>(new Guid(input.inputValue)).Colormap;
            EditorGUILayout.GradientField(cmap.ToUnityGradient());
        }

        private void KeyDataField(RawABRInput input)
        {
            // List<KeyData> allKeyData = ABREngine.Instance.Data.Data
        }
    }
}