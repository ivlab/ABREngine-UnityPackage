/* ABRConfigEditor.cs
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
    [CustomEditor(typeof(ABRConfig))]
    public class ABRConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            ABRConfig script = (ABRConfig) target;
            var configs = ABREngine.GetABRConfigs();
            var configIndexMatch = configs.FindIndex(c => c.name == target.name);
            if (configIndexMatch < 0)
            {
                EditorGUILayout.HelpBox(
                    "ABRConfigs need to be located in a folder Resources/ABRConfigs. " +
                    "To remove this warning (and make this ABRConfig usable by ABR), " +
                    "move this ABRConfig into any Resources/ABRConfigs folder. " +
                    "For example: Assets/Resources/ABRConfigs/YourABRConfig.asset.",
                    MessageType.Warning
                );
            }
            this.DrawDefaultInspector();
        }
    }
}

#endif