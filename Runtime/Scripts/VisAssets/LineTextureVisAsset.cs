/* LineTextureVisAsset.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 */

using System;
using UnityEngine;

namespace IVLab.ABREngine
{
    public class LineTextureVisAsset : IVisAsset
    {
        public Guid Uuid { get; set; }

        public DateTime ImportTime { get; set; }

        public VisAssetType VisAssetType { get; } = VisAssetType.SurfaceTexture;

        public Texture2D Texture { get; set; } = null;
    }
}