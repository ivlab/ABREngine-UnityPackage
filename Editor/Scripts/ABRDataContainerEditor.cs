/* ABRDataContainerEditor.cs
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
    [CustomEditor(typeof(ABRDataContainer))]
    public class ABRDataBoundsEditor : Editor
    {
        // Make sure OnSceneGui actually gets called regardless of how many inspectors are open
        void OnEnable()
        {
            SceneView.duringSceneGui += SceneGUI;
        }
        
        void OnDisable()
        {
            SceneView.duringSceneGui -= SceneGUI;
        }

        void SceneGUI(SceneView sceneView)
        {
            ABRDataContainer script = (ABRDataContainer) target;

            Matrix4x4 bboxXform = Matrix4x4.Translate(script.bounds.center) * Matrix4x4.Scale(script.bounds.extents * 2.0f);
            Handles.matrix = script.transform.localToWorldMatrix * bboxXform;
            Handles.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}

#endif