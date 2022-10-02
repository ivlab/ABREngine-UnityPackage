/* EncodedGameObject.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
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
using UnityEngine;

namespace IVLab.ABREngine
{
    /// <summary>
    /// An EncodedGameObject connects a DataImpression with a Unity Game Object.
    /// Look under the ABREngine main GameObject, find the data impression group
    /// your impression exists in, and inspect the Data Impression GameObject to
    /// find the EncodedGameObject.
    /// </summary>
    /// <remarks>
    /// While this little class seems unobtrusive and unimportant, this is the
    /// class that connects all of the underlying rendering work ABR is doing
    /// with the Unity scene and geometry rendering!
    /// </remarks>
    public class EncodedGameObject : MonoBehaviour
    {
        /// <summary>
        /// The UUID of the data impression this GameObject is encoding.
        /// </summary>
        public Guid Uuid { get; set; }
    }
}