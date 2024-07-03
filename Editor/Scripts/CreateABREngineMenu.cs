/* CreateABREngineMenu.cs
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
    public class CreateABREngineMenu : MonoBehaviour
    {
        [MenuItem("GameObject/ABR/ABREngine")]
        public static void CreateABREngine(MenuCommand cmd)
        {
            GameObject parent = cmd.context as GameObject;

            GameObject engine = new GameObject("ABREngine");
            if (parent != null)
            {
                engine.transform.SetParent(parent.transform);
            }

            engine.AddComponent<ABREngine>();
        }

        [MenuItem("GameObject/ABR/ABR Data Impression Group")]
        public static void CreateDataContainer(MenuCommand cmd)
        {
            GameObject parent = cmd.context as GameObject;
            GameObject container = new GameObject("Rename Me (ABR Data Container)");
            if (parent != null)
            {
                container.transform.SetParent(parent.transform);
            }

            container.AddComponent<DataImpressionGroup>();
            container.AddComponent<ABRDataContainer>();
        }
    }
}
#endif