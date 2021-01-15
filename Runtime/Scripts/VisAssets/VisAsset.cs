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
    }

    public interface IVisAsset
    {
        Guid Uuid { get; set; }
        DateTime ImportTime { get; set; }
        VisAssetType VisAssetType { get; }
    }
}