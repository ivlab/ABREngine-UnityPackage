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

        public Texture2D[] TextureArray { get; set; } = null;

        public Texture2D[] GetTextureList()
        {
            return TextureArray;
        }

        public Texture2D GetTexture()
        {
            if (TextureArray != null && TextureArray.Length > 0)
            {
                return TextureArray[0];
            }
            else return null;
        }

    }
}