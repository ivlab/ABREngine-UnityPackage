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
    public interface IColormapVisAsset : IVisAsset
    {
        Texture2D GetColorGradient();
    }

    public class ColormapVisAsset : VisAsset, IColormapVisAsset
    {
        public int VisAssetCount { get; } = 1;
        public Texture2D Colormap { get; } = null;

        public ColormapVisAsset() : this(new Guid(), null) { }
        public ColormapVisAsset(Texture2D colormap) : this(Guid.NewGuid(), colormap) { }
        public ColormapVisAsset(Guid uuid, Texture2D colormap)
        {
            Uuid = uuid;
            Colormap = colormap;
            ImportTime = DateTime.Now;
        }

        public Color GetColorInterp(float interpAmount) => Colormap.GetPixelBilinear(interpAmount, 0.5f);

        public Texture2D GetColorGradient() => Colormap;

        public static ColormapVisAsset SolidColor(Color fillColor)
        {
            Texture2D gradient = new Texture2D(2, 2);
            gradient.SetPixels(new Color[] { fillColor, fillColor, fillColor, fillColor });
            gradient.Apply();
            ColormapVisAsset cmap = new ColormapVisAsset(gradient);
            return cmap;
        }
    }
}