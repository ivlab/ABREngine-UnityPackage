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
using PlasticPipe.PlasticProtocol.Messages;
using System.Reflection;

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

            bool changed = false;
            foreach (string inputName in di.InputIndexer.InputNames)
            {
                var value = di.InputIndexer.GetInputValue(inputName);
                EditorGUILayout.LabelField($"{inputName}:");
                changed = changed || ParameterField(di, inputName, value);
            }

            EditorGUILayout.LabelField("Render Hints:", EditorStyles.boldLabel);

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

        private bool ParameterField(DataImpression di, string inputName, IABRInput input)
        {
            bool changed = false;
            var fieldInfo = di.InputIndexer.GetInputField(inputName);
            ABRInputAttribute abrAttr = fieldInfo.GetCustomAttributes(false).ToList().Find(att => att.GetType() == typeof(ABRInputAttribute)) as ABRInputAttribute;

            RawABRInput rawInput = input?.GetRawABRInput();
            RawABRInput changedInput = null;
            EditorGUILayout.BeginHorizontal();
            if (fieldInfo.FieldType.IsAssignableFrom(typeof(KeyData)))
                changedInput = KeyDataField(rawInput, di);
            else if (fieldInfo.FieldType.IsAssignableFrom(typeof(ScalarDataVariable)))
                changedInput = ScalarVariableField(rawInput, di);
            else if (fieldInfo.FieldType.IsAssignableFrom(typeof(ColormapVisAsset)))
                changedInput = ColormapField(rawInput);
            else
                EditorGUILayout.LabelField("        " + rawInput?.inputValue);

            changed = changedInput != null;
            if (changed)
            {
                rawInput = changedInput;
            }

            if (GUILayout.Button("x", GUILayout.Width(20)) && rawInput != null)
            {
                rawInput.inputValue = null;
                changed = true;
            }
            EditorGUILayout.EndHorizontal();

            if (changed)
            {
                if (rawInput.inputValue != null)
                {
                    IABRInput newInput = rawInput.ToABRInput();
                    Debug.Log("assigned" + rawInput.inputValue);
                    di.InputIndexer.AssignInput(inputName, newInput);
                }
                else
                {
                    Debug.Log("Assigned null");
                    di.InputIndexer.AssignInput(inputName, null);
                }
                if (abrAttr.updateLevel == UpdateLevel.Data)
                    di.RenderHints.DataChanged = true;
                if (abrAttr.updateLevel == UpdateLevel.Style)
                    di.RenderHints.StyleChanged = true;
            }
            return changed;
        }

        private RawABRInput ColormapField(RawABRInput input)
        {
            Colormap cmap = ABREngine.Instance.VisAssets.GetVisAsset<ColormapVisAsset>(new Guid(input.inputValue)).Colormap;
            Gradient newGradient = EditorGUILayout.GradientField(cmap.ToUnityGradient());
            Colormap newCmap = Colormap.FromUnityGradient(newGradient);
            // TODO: Compare cmap == newCmap
            return null;
        }

        private RawABRInput KeyDataField(RawABRInput input, DataImpression di)
        {
            int currentlySelected = -1;
            var allKeyData = ABREngine.Instance.Data.GetAllKeyData().Where(kd => kd.Topology == di.GetKeyDataTopology());
            string[] keyDataPaths = allKeyData.Select(kd => kd.Path).ToArray();
            if (input != null)
            {
                KeyData currentKeyData = ABREngine.Instance.Data.GetKeyData(input.inputValue);
                currentlySelected = Array.IndexOf(keyDataPaths, input.inputValue);
            }
            int newlySelected = EditorGUILayout.Popup(currentlySelected, keyDataPaths);
            if (currentlySelected != newlySelected)
            {
                if (input == null)
                {
                    input = new RawABRInput()
                    {
                        inputGenre = ABRInputGenre.KeyData.ToString(),
                        inputType = typeof(KeyData).ToString(),
                    };
                }
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
            KeyData kd = di.GetKeyData();
            if (kd == null)
                return null;

            int currentlySelected = -1;
            string[] allScalarVars = kd.GetScalarVariableNames();
            if (input != null && input.inputValue != null)
            {
                ScalarDataVariable v = kd.GetScalarVariable(DataPath.GetName(input.inputValue));
                currentlySelected = Array.IndexOf(allScalarVars, DataPath.GetName(v.Path));
            }

            int newlySelected = EditorGUILayout.Popup(currentlySelected, allScalarVars);
            if (currentlySelected != newlySelected)
            {
                if (input == null)
                {
                    input = new RawABRInput()
                    {
                        inputGenre = ABRInputGenre.Variable.ToString(),
                        inputType = typeof(ScalarDataVariable).ToString(),
                    };
                }
                string newVarName = allScalarVars[newlySelected];
                input.inputValue = DataPath.Join(DataPath.GetDatasetPath(kd.Path), DataPath.DataPathType.ScalarVar, newVarName);
                return input;
            }
            else
            {
                return null;
            }
        }
    }
}