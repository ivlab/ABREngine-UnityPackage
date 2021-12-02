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
using System.Collections.Generic;
using System.Linq;

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

    public interface ITextureVisAsset
    {
        Texture2D Texture { get; }
    }

    public interface IGeometryVisAsset
    {
        Mesh Mesh { get; }
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

    /// <summary>
    /// A gradient consisting of VisAssets of any type.
    /// </summary>
    public class VisAssetGradient<T> : VisAsset
    where T : IVisAsset
    {
        /// <summary>
        /// Type of all visassets in this gradient
        /// </summary>
        public override VisAssetType VisAssetType { get; }

        /// <summary>
        /// List of all VisAssets inside this gradient
        /// </summary>
        public List<T> VisAssets { get; } = new List<T>();

        /// <summary>
        /// List of gradient stops (length of VisAssets - 1)
        /// </summary>
        public List<float> Stops { get; } = new List<float>();

        public VisAssetGradient(T singleVisAsset) : this(Guid.NewGuid(), new T[] { singleVisAsset }.ToList(), new List<float>()) { }

        public VisAssetGradient(List<T> visAssets, List<float> stops) : this(Guid.NewGuid(), visAssets, stops) { }

        public VisAssetGradient(Guid uuid, List<T> visAssets, List<float> stops)
        {
            this.Uuid = uuid;
            if (visAssets.Count != stops.Count + 1)
            {
                throw new ArgumentException("VisAssetGradient: `visAssets` must have exactly ONE more element than `stops`.");
            }
            this.VisAssets = visAssets;
            this.Stops = stops;
            if (visAssets.Count > 0)
            {
                VisAssetType = visAssets[0].VisAssetType;
            }
            else
            {
                VisAssetType = VisAssetType.Invalid;
            }
        }

        /// <summary>
        /// Get the VisAsset at a particular index in the gradient (e.g. get the
        /// 3rd glyph in this set)
        /// </summary>
        public T Get(int index)
        {
            try
            {
                return VisAssets[index];
            }
            catch (IndexOutOfRangeException)
            {
                return default;
            }
        }

        /// <summary>
        /// Get the VisAsset a particular percentage of the way through the
        /// gradient (e.g. get the glyph that's at 50% through the gradient)
        /// </summary>
        public T Get(float percentage)
        {
            for (int i = 0; i < Stops.Count; i++)
            {
                if (i >= percentage)
                {
                    return Get(i + 1);
                }
            }
            return default;
        }
    }
}