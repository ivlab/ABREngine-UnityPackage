/* SurfaceTextureVisAsset.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 */

using System;
using UnityEngine;

namespace IVLab.ABREngine
{
    public class SurfaceTextureVisAsset : VisAsset
    {
        public override VisAssetType VisAssetType { get; } = VisAssetType.SurfaceTexture;

        public Texture2D Texture { get; set; } = null;

        public Texture2D NormalMap { get; set; } = null;
    }
}
