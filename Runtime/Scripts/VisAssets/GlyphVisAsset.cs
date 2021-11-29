/* GlyphVisAsset.cs
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
using UnityEngine;
using System.Collections.Generic;

namespace IVLab.ABREngine
{
    public interface IGlyphVisAsset
    {
        Mesh GetMesh(int lod);
        Texture2D GetNormalMap(int lod);
    }

    public class GlyphVisAsset : VisAsset, IGlyphVisAsset
    {
        public override VisAssetType VisAssetType { get; } = VisAssetType.Glyph;

        public List<Mesh> MeshLods { get; set; } = new List<Mesh>();

        public List<Texture2D> NormalMapLods { get; set; } = new List<Texture2D>();

        public Mesh GetMesh(int lod = 0)
        {
            return MeshLods[lod];
        }

        public Texture2D GetNormalMap(int lod = 0)
        {
            return NormalMapLods[lod];
        }
    }

    public class GlyphGradient : VisAsset, IVisAssetGradient<GlyphVisAsset>
    {
        public override VisAssetType VisAssetType { get; } = VisAssetType.Glyph;

        public List<GlyphVisAsset> VisAssets { get; set; } = new List<GlyphVisAsset>();

        public List<float> Stops { get; set; } = new List<float>();

        public GlyphGradient()
        {
            this.Uuid = Guid.NewGuid();
        }

        public GlyphGradient(List<GlyphVisAsset> visAssets, List<float> stops)
        {
            this.Uuid = Guid.NewGuid();
            this.VisAssets = visAssets;
            this.Stops = stops;
        }

        public GlyphVisAsset Get(int index)
        {
            try
            {
                return VisAssets[index];
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        public GlyphVisAsset Get(float percentage)
        {
            for (int i = 0; i < Stops.Count; i++)
            {
                if (i >= percentage)
                {
                    return Get(i + 1);
                }
            }
            return null;
        }
    }
}

