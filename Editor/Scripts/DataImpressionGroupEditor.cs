/* ABRDataBoundsEditor.cs
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

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

namespace IVLab.ABREngine
{
    [CustomEditor(typeof(DataImpressionGroup))]
    public class DataImpressionGroupEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Data Impression Groups contain related data impressions - " +
                "i.e., data impressions with data that share a common reference frame." +
                "Data impression groups defined in the scene will become 'templates' for ABR when a new ABR state is loaded." +
                "In editor, you can rename data impression groups and add a 'Data Container' to constrain the Unity-space data bounds.",
                MessageType.None
            );

            DataImpressionGroup group = (DataImpressionGroup) target;

            EditorGUILayout.LabelField($"Data Impression Group '{group.name}'", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Data Impressions:", EditorStyles.boldLabel);
            if (Application.isPlaying)
            {
                foreach (IDataImpression di in group.GetDataImpressions().Values)
                {
                    if (GUILayout.Button($"    {di.GetType().Name}: {di.Uuid}", EditorStyles.linkLabel))
                    {
                        // Selection.activeGameObject
                        Debug.LogWarning("Selecting Data Impressions from Groups Not implemented yet (need DataImpressions to be MonoBehaviours)");
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("    (displayed when ABR is running)");
            }

            EditorGUILayout.LabelField("Data Container:", EditorStyles.boldLabel);
            ABRDataContainer dataContainer;
            if (group.TryGetComponent<ABRDataContainer>(out dataContainer))
            {
                EditorGUILayout.LabelField("    " + dataContainer.bounds);
            }
            else
            {
                EditorGUILayout.LabelField("    No data container found.");
                if (GUILayout.Button("+ Add ABR Data Container"))
                {
                    group.gameObject.AddComponent<ABRDataContainer>();
                }
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Bounds and Matrix within Container:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(new GUIContent("    Bounds: " + group.GroupBounds, "Actual, Unity-Space bounds of the data inside the data container"));
                EditorGUILayout.LabelField(new GUIContent("    Group To Data Matrix:", "Transform matrix from group (Unity) space to Data Space"));
                Rect lastRect = GUILayoutUtility.GetLastRect();
                Rect matrixRect = new Rect(0, lastRect.y + EditorGUIUtility.singleLineHeight, EditorGUIUtility.currentViewWidth, EditorGUIUtility.singleLineHeight * 4);
                EditorGUILayout.Space(matrixRect.height);
                GUI.Label(matrixRect, group.GroupToDataMatrix.ToString("F3"), EditorStyles.centeredGreyMiniLabel);
            }
        }
    }
}

#endif
