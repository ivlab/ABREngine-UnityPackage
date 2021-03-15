/* VisAsset.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
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