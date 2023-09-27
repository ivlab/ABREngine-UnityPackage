/* ABRAttributes.cs
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

namespace IVLab.ABREngine
{
    /// <summary>
    /// How "deep" a particular update needs to go to fully address this ABR Input
    /// </summary>
    public enum UpdateLevel
    {
        /// <summary>
        /// Data updates generally need to address geometric information and/or
        /// populate data on a per-vertex basis, hence they are usually slow/expensive.
        /// </summary>
        Geometry,
        /// <summary>
        /// Style updates are generally lightweight and only consist of updating
        /// uniforms on the GPU, for example changing the colormap or glyph size.
        /// </summary>
        Style
    }

    /// <summary>
    ///     Input attribute used for annotating an ABR input to a data
    ///     impression (VisAsset, DataVariable, etc.)
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class ABRInputAttribute : System.Attribute
    {
        /// <summary>
        /// Name of the input - should match the name in the ABR Schema (see <see
        /// cref="abr-schema.md"/>).
        /// </summary>
        public string inputName;

        /// <summary>
        /// What <see cref="UpdateLevel"/> does this input necessitate when changed?
        /// </summary>
        public UpdateLevel updateLevel;

        public ABRInputAttribute(string inputName, UpdateLevel updateLevel)
        {
            this.inputName = inputName;
            this.updateLevel = updateLevel;
        }
    }

    /// <summary>
    ///     Attribute to match up this class with the string plate name from the
    ///     ABR Schema
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class ABRPlateType : System.Attribute
    {
        public string plateType;
        public ABRPlateType(string plateType)
        {
            this.plateType = plateType;
        }
    }
}