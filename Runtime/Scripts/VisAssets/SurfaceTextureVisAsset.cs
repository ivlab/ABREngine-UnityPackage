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
using UnityEngine;

namespace IVLab.ABREngine
{
    public interface ISurfaceTextureVisAsset
    {
        Texture2D Texture { get; set; }
        Texture2D NormalMap { get; set; }
    }

    public class SurfaceTextureVisAsset : VisAsset, ISurfaceTextureVisAsset, ITextureVisAsset
    {
        public float BlendWidth { get; } = 0.1f;

        public Texture2D Texture { get; set; } = null;

        public Texture2D NormalMap { get; set; } = null;
    }
}
