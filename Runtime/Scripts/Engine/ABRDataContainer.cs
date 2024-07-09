/* ABRDataContainer.cs
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

namespace IVLab.ABREngine
{
    [AddComponentMenu("ABR/ABR Data Container")]
    public class ABRDataContainer : MonoBehaviour
    {
        [SerializeField, Tooltip("Bounds to constrain the data to")]
        public Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

        [SerializeField, Tooltip("Overwrite the bounds for this group found in an ABR state file that is loaded, if any exist")]
        public bool overwriteStateBounds = false;
    }
}