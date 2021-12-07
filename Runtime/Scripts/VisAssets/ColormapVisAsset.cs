/* ColormapVisAsset.cs
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
    public interface IColormapVisAsset
    {
        Texture2D GetColorGradient();
    }

    public class ColormapVisAsset : VisAsset, IColormapVisAsset, ITextureVisAsset
    {
        public float BlendWidth { get; } = 0.0f;

        public Texture2D Texture { get; set; } = null;

        public Color GetColorInterp(float interpAmount)
        {
            return Texture.GetPixelBilinear(interpAmount, 0.5f);
        }

        public Texture2D GetColorGradient()
        {
            return Texture;
        }

        public static ColormapVisAsset SolidColor(Color fillColor)
        {
            Texture2D gradient = new Texture2D(1, 1);
            gradient.SetPixel(0, 0, fillColor);
            gradient.Apply();
            ColormapVisAsset cmap = new ColormapVisAsset();
            cmap.Texture = gradient;
            return cmap;
        }
    }
}