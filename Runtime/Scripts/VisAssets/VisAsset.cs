/* VisAsset.cs
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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public interface IVisAsset : IABRInput
    {
        Guid Uuid { get; set; }
        DateTime ImportTime { get; set;}
    }

    /// <summary>
    /// A VisAsset described by a texture (or, series of textures)
    /// </summary>
    public interface ITextureVisAsset
    {
        /// <summary>
        /// The main texture for this VisAsset
        /// </summary>
        Texture2D Texture { get; }

        /// <summary>
        /// In gradients of this VisAsset, how wide to make the blend?
        /// </summary>
        float BlendWidth { get; }
    }

    public interface IGeometryVisAsset
    {
        Mesh Mesh { get; }
    }

    public class VisAsset : IVisAsset
    {
        /// <summary>
        /// Typemap where we can look up ABR visasset types and convert to C#
        /// types. Keys should match the "VisAssetType" in the ABR schema.
        /// </summary>
        public static Dictionary<string, Type> VisAssetTypeMap = new Dictionary<string, Type>()
        {
            { "colormap", typeof(ColormapVisAsset) },
            { "glyph", typeof(GlyphVisAsset) },
            { "line", typeof(LineTextureVisAsset) },
            { "texture", typeof(SurfaceTextureVisAsset) },
        };

        public ABRInputGenre Genre { get; } = ABRInputGenre.VisAsset;
        public Guid Uuid { get; set; }
        public DateTime ImportTime { get; set; }

        public RawABRInput GetRawABRInput()
        {
            return new RawABRInput {
                inputType = this.GetType().ToString(),
                inputValue = this.Uuid.ToString(),
                parameterName = "",// TODO
                inputGenre = Genre.ToString("G"),
            };
        }
    }

    /// <summary>
    /// A gradient consisting of VisAssets of any type. NOTE: Texture-based
    /// gradients (Surface/Line textures and colormaps) must have 4 or fewer
    /// elements.
    /// </summary>
    public class VisAssetGradient<T> : VisAsset
    where T : IVisAsset
    {
        /// <summary>
        /// List of all VisAssets inside this gradient
        /// </summary>
        public List<T> VisAssets { get; } = new List<T>();

        /// <summary>
        /// List of gradient stops (length of VisAssets - 1)
        /// </summary>
        public List<float> Stops { get; } = new List<float>();

        /// <summary>
        /// Red/Green/Blue/Alpha texture that describes the blending between
        /// `Stops`. For a gradient with 3 elements, this texture will look red on
        /// the left, green in the middle, and blue on the right. The transition
        /// blend is defined by `blendWidth`.
        /// </summary>
        public Texture2D BlendMap
        {
            get
            {
                if (_blendMap == null)
                {
                    CalculateBlendMaps();
                }
                return _blendMap;
            }
        }

        /// <summary>
        /// Grayscale texture that describes how far along in the current stop
        /// we are, matching up with `BlendMap`. For a gradient with 3 elements,
        /// this texture will look like 3 black-to-white colormaps smooshed
        /// together.
        /// </summary>
        public Texture2D StopMap
        {
            get
            {
                if (_stopMap == null)
                {
                    CalculateBlendMaps();
                }
                return _stopMap;
            }
        }

        /// <summary>
        /// The actual combined texture that contains all visassets, stacked together vertically
        /// </summary>
        public Texture2D StackedTexture
        {
            get
            {
                if (!IsTextureGradient)
                {
                    throw new FormatException($"Gradient {Uuid} is not a texture gradient");
                }
                if (_stackedTexture == null)
                {
                    List<Texture2D> textures = VisAssets.Select((va) => (va as ITextureVisAsset).Texture).ToList();
                    _stackedTexture = TextureUtilities.MakeTextureGradientVertical(textures);
                }
                return _stackedTexture;
            }
        }

        private bool IsTextureGradient
        {
            get
            {
                return typeof(ITextureVisAsset).IsAssignableFrom(typeof(T));
            }
        }

        private Texture2D _blendMap;
        private Texture2D _stopMap;
        private Texture2D _stackedTexture;

        public VisAssetGradient(T singleVisAsset) : this(Guid.NewGuid(), new T[] { singleVisAsset }.ToList(), new List<float>()) { }

        public VisAssetGradient(List<T> visAssets, List<float> stops) : this(Guid.NewGuid(), visAssets, stops) { }

        public VisAssetGradient(Guid uuid, List<T> visAssets, List<float> stops)
        {
            this.Uuid = uuid;
            if (visAssets.Count != stops.Count + 1)
            {
                throw new ArgumentException("VisAssetGradient: `visAssets` must have exactly ONE more element than `stops`.");
            }
            this.VisAssets = visAssets;
            this.Stops = stops;
        }

        /// <summary>
        /// Get the VisAsset at a particular index in the gradient (e.g. get the
        /// 3rd glyph in this set)
        /// </summary>
        public T Get(int index)
        {
            try
            {
                return VisAssets[index];
            }
            catch (IndexOutOfRangeException)
            {
                return default;
            }
        }

        /// <summary>
        /// Get the VisAsset a particular percentage of the way through the
        /// gradient (e.g. get the glyph that's at 50% through the gradient)
        /// </summary>
        public T Get(float percentage)
        {
            for (int i = 0; i < Stops.Count; i++)
            {
                if (i >= percentage)
                {
                    return Get(i + 1);
                }
            }
            return default;
        }

        /// <summary>
        /// Calculate the blend and stop maps for this gradient
        /// </summary>
        private void CalculateBlendMaps()
        {
            if (!IsTextureGradient)
            {
                throw new FormatException($"Gradient {Uuid} is not a texture gradient");
            }
            float blendWidth = (VisAssets[0] as ITextureVisAsset).BlendWidth;

            int width = 1024;
            int height = 1;
            int halfBlendWidthPix = (int)(width * blendWidth);

            Color[] blendMapPercentages = {
                new Color(1.0f, 0.0f, 0.0f, 0.0f),
                new Color(0.0f, 1.0f, 0.0f, 0.0f),
                new Color(0.0f, 0.0f, 1.0f, 0.0f),
                new Color(0.0f, 0.0f, 0.0f, 1.0f),
            };

            List<Color> pixels = new List<Color>();
            List<Color> stopPercentPixels = new List<Color>();

            // Iterate through all gradient stops + an end stop at 100%
            int previousStopCol = 0;
            for (int stop = 0; stop < Stops.Count + 1; stop++)
            {
                // Check if we're on the last (ending) stop or not
                float stopPercentage = 1.0f;
                if (stop < Stops.Count)
                {
                    stopPercentage = Stops[stop];
                }

                int stopCol = (int)(stopPercentage * width);
                int stopWidthPix = stopCol - previousStopCol;

                // If we're not at the beginning, do the beginning part of the blend (from previous tex)
                int col = previousStopCol;
                if (stop > 0)
                {
                    for (int i = 0; i < halfBlendWidthPix; i++, col++)
                    {
                        float percentThroughStop = (col - previousStopCol) / (float)(stopWidthPix);
                        stopPercentPixels.Add(blendMapPercentages[stop] * percentThroughStop);

                        // This is the second half of the blend
                        float t = (i + halfBlendWidthPix) / (halfBlendWidthPix * 2.0f);
                        Color color1 = blendMapPercentages[stop] * (t);
                        Color color2 = blendMapPercentages[stop - 1] * (1.0f - t);

                        Color blendPixel = color1 + color2;
                        pixels.Add(blendPixel);
                    }
                }

                // Perform the main part between blends
                int mainPartEnd = stop < Stops.Count ? stopCol - halfBlendWidthPix : stopCol;
                for (; col < mainPartEnd; col++)
                {
                    float percentThroughStop = (col - previousStopCol) / (float)(stopWidthPix);
                    stopPercentPixels.Add(blendMapPercentages[stop] * percentThroughStop);
                    pixels.Add(blendMapPercentages[stop]);
                }

                // If we're not on the last stop, perform the end part of the blend
                if (stop < Stops.Count)
                {
                    for (int i = 0; i < halfBlendWidthPix; i++, col++)
                    {
                        float percentThroughStop = (col - previousStopCol) / (float)(stopWidthPix);
                        stopPercentPixels.Add(blendMapPercentages[stop] * percentThroughStop);

                        // This is the first half of the blend
                        float t = (i) / (halfBlendWidthPix * 2.0f);
                        Color color1 = blendMapPercentages[stop] * (1.0f - t);
                        Color color2 = blendMapPercentages[stop + 1] * (t);

                        Color blendPixel = color1 + color2;
                        pixels.Add(blendPixel);
                    }
                }

                previousStopCol = stopCol;
            }

            _blendMap = new Texture2D(width, height);
            _blendMap.SetPixels(pixels.ToArray());
            _blendMap.Apply();

            _stopMap = new Texture2D(width, height);
            _stopMap.filterMode = FilterMode.Point;
            _stopMap.SetPixels(stopPercentPixels.ToArray());
            _stopMap.Apply();
        }
    }

    /// <summary>
    /// Serializable version of the VisAsset gradients that interacts with
    /// state/schema
    /// </summary>
    public class RawVisAssetGradient
    {
        public string uuid;
        public string gradientScale = "continuous";
        public string gradientType;
        public float[] points;
        public string[] visAssets;

        public static RawVisAssetGradient From<T>(VisAssetGradient<T> gradient)
        where T : IVisAsset
        {
            RawVisAssetGradient grad = new RawVisAssetGradient();
            grad.uuid = gradient.Uuid.ToString();
            grad.gradientType = VisAsset.VisAssetTypeMap.FirstOrDefault((kv) => kv.Value == typeof(T)).Key;
            grad.points = gradient.Stops.ToArray();
            grad.visAssets = gradient.VisAssets.Select((va) => va.Uuid.ToString()).ToArray();
            return grad;
        }
    }
}