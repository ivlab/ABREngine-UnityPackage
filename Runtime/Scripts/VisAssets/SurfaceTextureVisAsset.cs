/* SurfaceTextureVisAsset.cs
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
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace IVLab.ABREngine
{
    public interface ISurfaceTextureVisAsset : IVisAsset, ITextureGradient
    {
        /// <summary>
        /// Obtain the first (or, only) texture in a multi-visasset gradient
        /// </summary>
        Texture2D GetTexture();

        /// <summary>
        /// Obtain the texture at a specific index within a multi-visasset gradient
        /// </summary>
        Texture2D GetTexture(int gradientIndex);

        /// <summary>
        /// Obtain the texture at a specific t-value (percentage) within a multi-visasset gradient
        /// </summary>
        Texture2D GetTexture(float gradientT);
    }

    public class SurfaceTextureVisAsset : VisAsset, ISurfaceTextureVisAsset
    {
        public int VisAssetCount { get; } = 1;
        public Texture2D Texture { get; } = null;
        public Texture2D NormalMap { get; } = null;
        public GradientBlendMap BlendMaps { get; }

        public SurfaceTextureVisAsset() : this(new Guid(), null, null) { }
        public SurfaceTextureVisAsset(Texture2D texture, Texture2D normalMap) : this(Guid.NewGuid(), texture, normalMap) { }
        public SurfaceTextureVisAsset(Guid uuid, Texture2D texture, Texture2D normalMap)
        {
            Uuid = uuid;
            Texture = texture;
            NormalMap = normalMap;
            ImportTime = DateTime.Now;
        }

        public Texture2D GetTexture() => Texture;
        public Texture2D GetTexture(int gradientIndex) => Texture;
        public Texture2D GetTexture(float gradientT) => Texture;
    }

    public class SurfaceTextureGradient : VisAssetGradient, ISurfaceTextureVisAsset, IVisAssetGradient<SurfaceTextureVisAsset>, ITextureGradient
    {
        public int VisAssetCount { get => VisAssets.Count; }
        public GradientBlendMap BlendMaps { get; private set; }
        public List<SurfaceTextureVisAsset> VisAssets { get; private set; }
        public List<float> Stops { get; private set; }

        public void Initialize(Guid uuid, List<SurfaceTextureVisAsset> visAssets, List<float> stops)
        {
            Uuid = uuid;
            VisAssets = visAssets;
            Stops = stops;
            BlendMaps = new GradientBlendMap(VisAssets.Select(va => va.GetTexture()).ToList(), Stops, 0.1f);
        }

        public Texture2D GetTexture() => VisAssets[0].GetTexture();
        public Texture2D GetTexture(int gradientIndex) => VisAssets[gradientIndex].GetTexture();
        public Texture2D GetTexture(float gradientT)
        {
            for (int i = 0; i < Stops.Count; i++)
            {
                if (Stops[i] >= gradientT)
                {
                    return GetTexture(i + 1);
                }
            }
            return default;
        }
    }
}
