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

using UnityEngine;
using UnityEditor;
using System.Reflection;

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
                string inputValue = value != null ? value.GetRawABRInput().inputValue : "[None]";
                EditorGUILayout.LabelField($"    - {inputName}: {inputValue}", EditorStyles.miniLabel);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}