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

using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Collection of essential textures for making texture-based VisAsset
    /// gradients happen.
    /// </summary>
    public class GradientBlendMap
    {
        /// <summary>
        /// Red/Green/Blue/Alpha texture that describes the blending between
        /// `Stops`. For a gradient with 3 elements, this texture will look red on
        /// the left, green in the middle, and blue on the right. The transition
        /// blend is defined by `blendWidth`.
        /// </summary>
        public Texture2D BlendMap { get; private set; }

        /// <summary>
        /// Grayscale texture that describes how far along in the current stop
        /// we are, matching up with `BlendMap`. For a gradient with 3 elements,
        /// this texture will look like 3 black-to-white colormaps smooshed
        /// together.
        /// </summary>
        public Texture2D StopMap { get; private set; }

        /// <summary>
        /// The actual combined texture that contains all visassets, stacked together vertically
        /// </summary>
        public Texture2D Textures { get; private set; }

        public GradientBlendMap(Texture2D texture) : this(
            new List<Texture2D>() { texture },
            new List<float>(),
            new List<float>()
        ) { }

        public GradientBlendMap(List<Texture2D> textures, List<float> stops, float blendWidth) : this(
            textures,
            stops,
            Enumerable.Repeat(blendWidth, textures.Count).ToList()
        ) { }

        public GradientBlendMap(List<Texture2D> textures, List<float> stops, List<float> blendWidths)
        {
            if (textures.Count != stops.Count + 1)
            {
                throw new ArgumentException("GradientBlendMap: `textures` must have exactly ONE more element than `stops`.");
            }
            CalculateGradient(textures, stops, blendWidths);
        }

        /// <summary>
        /// Calculate the blend and stop maps for this gradient as well as the actual texture
        /// </summary>
        private void CalculateGradient(List<Texture2D> textures, List<float> stops, List<float> blendWidths)
        {
            float blendWidth = 0.1f;

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
            for (int stop = 0; stop < stops.Count + 1; stop++)
            {
                // Check if we're on the last (ending) stop or not
                float stopPercentage = 1.0f;
                if (stop < stops.Count)
                {
                    stopPercentage = stops[stop];
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
                int mainPartEnd = stop < stops.Count ? stopCol - halfBlendWidthPix : stopCol;
                for (; col < mainPartEnd; col++)
                {
                    float percentThroughStop = (col - previousStopCol) / (float)(stopWidthPix);
                    stopPercentPixels.Add(blendMapPercentages[stop] * percentThroughStop);
                    pixels.Add(blendMapPercentages[stop]);
                }

                // If we're not on the last stop, perform the end part of the blend
                if (stop < stops.Count)
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

            BlendMap = new Texture2D(width, height);
            BlendMap.SetPixels(pixels.ToArray());
            BlendMap.Apply();

            StopMap = new Texture2D(width, height);
            StopMap.filterMode = FilterMode.Point;
            StopMap.SetPixels(stopPercentPixels.ToArray());
            StopMap.Apply();

            Textures = TextureUtilities.MakeTextureGradientVertical(textures);
        }
    }
}