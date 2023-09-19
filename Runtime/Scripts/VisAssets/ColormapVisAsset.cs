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
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public interface IColormapVisAsset : IVisAsset
    {
        Texture2D GetColorGradient();
    }

    public class ColormapVisAsset : VisAsset, IColormapVisAsset
    {
        public int VisAssetCount { get; } = 1;
        public Texture2D ColormapTexture
        {
            get
            {
                if (changed || _texture == null)
                {
                    _texture = Colormap.ToTexture2D();
                    changed = false;
                }
                return _texture;
            }
        }
        private Texture2D _texture;

        private bool changed = true;

        public Colormap Colormap
        {
            get => _colormap;
            private set
            {
                _colormap = value;
                changed = true;
            }
        }
        private Colormap _colormap;

        public ColormapVisAsset() : this(new Guid(), (Colormap) null) { }

        public ColormapVisAsset(Colormap colormap) : this(Guid.NewGuid(), colormap) { }
        public ColormapVisAsset(Guid uuid, Colormap colormap)
        {
            Uuid = uuid;
            Colormap = colormap;
            ImportTime = DateTime.Now;
        }


        [Obsolete("Constructing a ColormapVisAsset from Texture2D is no longer recommended; instead use an IVLab.Utilities.Colormap like `new ColormapVisAsset(..., Colormap)`.")]
        public ColormapVisAsset(Texture2D colormap) : this(Guid.NewGuid(), colormap) { }
        [Obsolete("Constructing a ColormapVisAsset from Texture2D is no longer recommended; instead use an IVLab.Utilities.Colormap like `new ColormapVisAsset(..., Colormap)`.")]
        public ColormapVisAsset(Guid uuid, Texture2D colormap)
        {
            Uuid = uuid;
            Colormap = Colormap.FromTexture2D(colormap);
            Debug.LogWarning("Constructing a ColormapVisAsset from Texture2D is no longer recommended; instead use an IVLab.Utilities.Colormap like `new ColormapVisAsset(..., Colormap)`.");
            ImportTime = DateTime.Now;
        }
        public Color GetColorInterp(float interpAmount) => ColormapTexture.GetPixelBilinear(interpAmount, 0.5f);

        public Texture2D GetColorGradient() => ColormapTexture;

        public static ColormapVisAsset SolidColor(Color fillColor)
        {
            Colormap oneStop = new Colormap();
            oneStop.AddControlPt(0, fillColor);
            ColormapVisAsset cmap = new ColormapVisAsset(oneStop);
            return cmap;
        }
    }
}