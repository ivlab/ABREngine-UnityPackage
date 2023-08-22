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
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using IVLab.Utilities;
using UnityEngine;

namespace IVLab.ABREngine
{
    // Apply same custom editor for all data impression types
    [CustomEditor(typeof(DataImpression), true)]
    public class DataImpressionEditor : Editor
    {
        /// <summary>
        /// Trigger an <see cref="ABREngine.Render"/> when a parameter is
        /// changed in editor
        /// </summary>
        public static bool ReRenderOnParameterChange = true;

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
                    ParameterField(di, inputName, value);
                }
                else
                {
                    EditorGUILayout.LabelField($"        [None]");
                }
            }

            EditorGUILayout.LabelField("Render Hints:", EditorStyles.boldLabel);

            bool changed = false;
            bool oldVisibility = di.RenderHints.Visible;
            di.RenderHints.Visible = EditorGUILayout.Toggle("Visible", di.RenderHints.Visible);
            changed = changed || (oldVisibility != di.RenderHints.Visible);


            ReRenderOnParameterChange = EditorGUILayout.Toggle("Re-Render on parameter changed in editor", ReRenderOnParameterChange);

            if (changed && ReRenderOnParameterChange)
            {
                ABREngine.Instance.Render();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ParameterField(DataImpression di, string inputName, IABRInput input)
        {
            RawABRInput originalInput = input.GetRawABRInput();
            RawABRInput changedInput = null;
            if (originalInput.inputType == typeof(KeyData).ToString())
                changedInput = KeyDataField(originalInput);
            else if (originalInput.inputType == typeof(ScalarDataVariable).ToString())
                changedInput = ScalarVariableField(originalInput, di);
            else if (originalInput.inputType == typeof(ColormapVisAsset).ToString())
                changedInput = ColormapField(originalInput);
            else
                EditorGUILayout.LabelField("        " + originalInput.inputValue);

            if (changedInput != null)
            {
                IABRInput newInput = changedInput.ToABRInput();
                di.InputIndexer.AssignInput(inputName, newInput);
                var fieldInfo = di.InputIndexer.GetInputField(inputName);
                ABRInputAttribute abrAttr = fieldInfo.GetCustomAttributes(false).ToList().Find(att => att.GetType() == typeof(ABRInputAttribute)) as ABRInputAttribute;
                if (abrAttr.updateLevel == UpdateLevel.Data)
                    di.RenderHints.DataChanged = true;
                if (abrAttr.updateLevel == UpdateLevel.Style)
                    di.RenderHints.StyleChanged = true;

                if (ReRenderOnParameterChange)
                {
                    ABREngine.Instance.Render();
                }
            }
        }

        private RawABRInput ColormapField(RawABRInput input)
        {
            Colormap cmap = ABREngine.Instance.VisAssets.GetVisAsset<ColormapVisAsset>(new Guid(input.inputValue)).Colormap;
            Gradient newGradient = EditorGUILayout.GradientField(cmap.ToUnityGradient());
            Colormap newCmap = Colormap.FromUnityGradient(newGradient);
            // TODO: Compare cmap == newCmap
            return null;
        }

        private RawABRInput KeyDataField(RawABRInput input)
        {
            KeyData currentKeyData = ABREngine.Instance.Data.GetKeyData(input.inputValue);
            var allKeyData = ABREngine.Instance.Data.GetAllKeyData().Where(kd => kd.Topology == currentKeyData.Topology);
            string[] keyDataPaths = allKeyData.Select(kd => kd.Path).ToArray();
            int currentlySelected = Array.IndexOf(keyDataPaths, input.inputValue);
            int newlySelected = EditorGUILayout.Popup(currentlySelected, keyDataPaths);
            if (currentlySelected != newlySelected)
            {
                string newKeyDataPath = keyDataPaths[newlySelected];
                input.inputValue = newKeyDataPath;
                return input;
            }
            else
            {
                return null;
            }
        }

        private RawABRInput ScalarVariableField(RawABRInput input, DataImpression di)
        {
            // KeyData kd = di.GetKeyData();
            // ScalarDataVariable v = kd.GetScalarVariable(DataPath.GetName(input.inputValue));
            // Debug.Log(input.inputValue);
            // string[] allScalarVars = kd.GetScalarVariableNames();
            // Debug.Log(string.Join(", ", allScalarVars));
            // int currentlySelected = Array.IndexOf(allScalarVars, v.Path);
            // int newlySelected = EditorGUILayout.Popup(currentlySelected, allScalarVars);
            // if (currentlySelected != newlySelected)
            // {
            //     string newVarPath = allScalarVars[newlySelected];
            //     input.inputValue = newVarPath;
            //     return input;
            // }
            // else
            // {
            //     return null;
            // }
            return null;
        }
    }
}