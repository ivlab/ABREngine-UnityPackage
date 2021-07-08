/* VisAsset.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
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

namespace IVLab.ABREngine
{
    public enum VisAssetType
    {
        Colormap,
        Glyph,
        LineTexture,
        SurfaceTexture,
        Invalid,
    }

    public interface IVisAsset : IABRInput
    {
        Guid Uuid { get; set; }
        DateTime ImportTime { get; set;}
        VisAssetType VisAssetType { get; }
    }

    public class VisAsset : IVisAsset
    {
        public ABRInputGenre Genre { get; } = ABRInputGenre.VisAsset;
        public Guid Uuid { get; set; }
        public DateTime ImportTime { get; set; }
        public virtual VisAssetType VisAssetType { get; }

        public RawABRInput GetRawABRInput()
        {
            return new RawABRInput {
                inputType = this.GetType().ToString(),
                inputValue = this.Uuid.ToString(),
                parameterName = "",// TODO
                inputGenre = Genre.ToString("G"),
            };
        }
    }
}