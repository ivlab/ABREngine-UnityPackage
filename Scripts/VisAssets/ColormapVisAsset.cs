/* ColormapVisAsset.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 */

using System;
using UnityEngine;

namespace IVLab.ABREngine
{
    public class ColormapVisAsset : IVisAsset
    {
        public Guid Uuid { get; set; }

        public VisAssetType VisAssetType { get; } = VisAssetType.Colormap;

        public Texture2D Gradient { get; set; } = null;

        public Color GetColorInterp(float interpAmount)
        {
            return Gradient.GetPixelBilinear(interpAmount, 0.5f);
        }

        public Texture2D GetColorGradient()
        {
            return Gradient;
        }
    }
}