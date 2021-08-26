/* EncodedGameObjectEditor.cs
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

using UnityEditor;

namespace IVLab.ABREngine
{
    [CustomEditor(typeof(EncodedGameObject))]
    public class EncodedGameObjectEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Setup (get uuid, data impression)
            EncodedGameObject script = (EncodedGameObject) target;
            IDataImpression impression = ABREngine.Instance.GetDataImpression(script.Uuid);

            EditorGUILayout.LabelField(impression.GetType().Name);
            EditorGUILayout.LabelField("UUID: " + impression.Uuid);
        }
    }
}