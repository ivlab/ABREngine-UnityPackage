/* GlyphVisAsset.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 */

using System;
using UnityEngine;
using System.Collections.Generic;

namespace IVLab.ABREngine
{
    public class GlyphVisAsset : IVisAsset
    {
        public Guid Uuid { get; set; }

        public VisAssetType VisAssetType { get; } = VisAssetType.Glyph;

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
}

