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
    /// gradients happen. The blend maps are controlled via the <see
    /// cref="BlendMap"/> and <see cref="StopMap"/> texture arrays.
    /// </summary>
    public class GradientBlendMap
    {

        /// <summary>
        /// Maximum number of supported textures in a texture gradient
        /// </summary>
        public const int MaxSupportedTextures = 16;

        /// <summary>
        /// Red/Green/Blue/Alpha texture that describes the blending
        /// between `Stops`. For a gradient with 3 elements, this texture will
        /// look red on the left, green in the middle, and blue on the right.
        /// For a gradient with 12 elements, this texture will have 3 rows with
        /// red, green, blue, and alpha. The transition blend is defined by
        /// `blendWidth`. For a gradient with 6 elements, the BlendMap looks
        /// something like this: <img src="./resources/blendmap-combined.png"/>
        /// </summary>
        public Texture2D BlendMaps { get; private set; }

        /// <summary>
        /// Red/green/blue/alpha texture that describes how far along in the
        /// current stop we are, matching up with `BlendMap`. For a gradient
        /// with 3 elements, this texture will look like 3 black-to-white
        /// colormaps smooshed together. For a gradient with 6 elements, the StopMap looks
        /// something like this: <img src="./resources/stopmap-combined.png"/>
        /// </summary>
        public Texture2D StopMaps { get; private set; }

        /// <summary>
        /// Array of aspect ratios (width / height) of each texture
        /// </summary>
        public float[] AspectRatios { get; private set; }

        /// <summary>
        /// Array of aspect ratios (height / width) of each texture
        /// </summary>
        public float[] HeightWidthAspectRatios { get; private set; }

        /// <summary>
        /// The actual combined texture that contains all visassets, stacked
        /// together vertically. For a gradient with 5 line texture elements, it
        /// might look something like this:
        /// <img src="./resources/textures.png"/>
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
            // Find out how many blend/stop map textures we need to represent
            // this gradient (each tex has 4 channels)
            int numBlendTex = textures.Count / 4 + 1;
            const int SupportedChannels = 4;

            float blendWidth = 0.02f;

            const int TexWidth = 1024;
            int halfBlendWidthPix = (int)(TexWidth * blendWidth);
            Color[] blendMapPercentages = {
                new Color(1.0f, 0.0f, 0.0f, 0.0f),
                new Color(0.0f, 1.0f, 0.0f, 0.0f),
                new Color(0.0f, 0.0f, 1.0f, 0.0f),
                new Color(0.0f, 0.0f, 0.0f, 1.0f),
            };

            Color[] blendMapPixels = new Color[TexWidth * numBlendTex];
            Color[] stopMapPixels = new Color[TexWidth * numBlendTex];

            // Iterate through all gradient stops + an end stop at 100%
            int startCol = 0;
            for (int stop = 0; stop < stops.Count + 1; stop++)
            {
                // Check if we're on the last (ending) stop or not
                float stopPercentage = 1.0f;
                if (stop < stops.Count)
                {
                    stopPercentage = stops[stop];
                }

                // Column (in pixels) that this stop ends at
                int nextStopCol = (int)(stopPercentage * TexWidth);
                int stopWidth = nextStopCol - startCol;

                // `stop` index is *technically* equal to the texture index.
                int texIndex = stop;

                // Channel (R, G, B, A) or (0, 1, 2, 3)
                int channelIndex = texIndex % SupportedChannels;
                bool nextIsDiffGroup = channelIndex + 1 >= SupportedChannels;
                bool prevIsDiffGroup = channelIndex - 1 < 0;
                int nextChannelIndex = (channelIndex + 1) % SupportedChannels;
                int prevChannelIndex = prevIsDiffGroup ? SupportedChannels - 1 : channelIndex - 1;

                // "Texture Group" index (row of blend/stop map to put the result in)
                int groupIndex = texIndex / SupportedChannels;

                bool isFirstStop = stop == 0;
                bool isLastStop = stop >= stops.Count;

                // Debug.Log($"stop: {stop}, channelIndex: {channelIndex}, nxch: {nextChannelIndex}, pch: {prevChannelIndex}, grp: {groupIndex}");

                // Apply the main (middle) part of this texture
                int mainStart = !isFirstStop ? startCol + halfBlendWidthPix : startCol;
                int mainEnd = !isLastStop ? nextStopCol - halfBlendWidthPix : nextStopCol;
                int col = 0;
                for (col = mainStart; col < mainEnd; col++)
                {
                    int pixIndex = col + TexWidth * groupIndex;

                    // How far along in this stop we are
                    float stopT = (col - startCol) / (float) stopWidth;

                    stopMapPixels[pixIndex][channelIndex] = stopT;
                    blendMapPixels[pixIndex][channelIndex] += 1.0f;
                }

                // Apply the ending part of this tex (start of the blend, t = 0.0 -> t = 0.5)
                if (!isLastStop)
                {
                    int blendStart = nextStopCol - halfBlendWidthPix;
                    for (col = blendStart; col < nextStopCol; col++)
                    {
                        float stopT = (col - startCol) / (float) stopWidth;
                        float blendT = 0.5f * ((col - blendStart) / (float) (nextStopCol - blendStart));
                        int pixIndex = col + TexWidth * groupIndex;
                        Color cur = blendMapPercentages[channelIndex] * (1.0f - blendT);
                        Color next = blendMapPercentages[nextChannelIndex] * blendT;
                        if ((channelIndex + 1) >= SupportedChannels)
                        {
                            int pixIndexNext = col + TexWidth * (groupIndex + 1);
                            blendMapPixels[pixIndex] += cur;
                            blendMapPixels[pixIndexNext] += next;
                        }
                        else
                        {
                            Color blend = cur + next;
                            blendMapPixels[pixIndex] += blend;
                        }

                        stopMapPixels[pixIndex][channelIndex] = stopT;
                    }
                }

                // Apply the start part of this tex (end of the blend, t = 0.5 -> t = 1.0)
                if (!isFirstStop)
                {
                    int blendEnd = startCol + halfBlendWidthPix;
                    for (col = startCol; col < blendEnd; col++)
                    {
                        float stopT = (col - startCol) / (float) stopWidth;
                        float blendT = 0.5f * ((col - startCol) / (float) (blendEnd - startCol)) + 0.5f;
                        int pixIndex = col + TexWidth * groupIndex;
                        Color cur = blendMapPercentages[channelIndex] * blendT;
                        Color prev = blendMapPercentages[prevChannelIndex] * (1.0f - blendT);
                        if ((channelIndex - 1) < 0)
                        {
                            int pixIndexPrev = col + TexWidth * (groupIndex - 1);
                            blendMapPixels[pixIndex] += cur;
                            blendMapPixels[pixIndexPrev] += prev;
                        }
                        else
                        {
                            Color blend = cur + prev;
                            blendMapPixels[pixIndex] += blend;
                        }

                        stopMapPixels[pixIndex][channelIndex] = stopT;
                    }
                }

                startCol = nextStopCol;
            }

            // DEBUG: Mess with alpha for saving pixels for documentation
            // To generate stopmap.png and blendmap.png, uncomment this whole section.
            // Then, build the RGB blend by running the engine.
            // Next, build the Alpha blend by setting `buildAlpha` to true and running the engine again.
            // Uncomment the PNG saves below as necessary, and blend them together in GIMP/Photoshop.
            // for (int i = 0; i < blendMapPixels.Length; i++)
            // {
            //     // Build alpha blend
            //     bool buildAlpha = true;
            //     if (buildAlpha)
            //     {
            //         if (blendMapPixels[i].a > 0)
            //         {
            //             blendMapPixels[i].r = blendMapPixels[i].a;
            //             blendMapPixels[i].g = blendMapPixels[i].a;
            //             blendMapPixels[i].b = blendMapPixels[i].a;
            //         }
            //         else
            //         {
            //             blendMapPixels[i] = Color.black;
            //         }
            //         if (stopMapPixels[i].a > 0)
            //         {
            //             stopMapPixels[i].r = stopMapPixels[i].a;
            //             stopMapPixels[i].g = stopMapPixels[i].a;
            //             stopMapPixels[i].b = stopMapPixels[i].a;
            //         }
            //         else
            //         {
            //             stopMapPixels[i] = Color.black;
            //         }
            //     }

            //     blendMapPixels[i].a = 1.0f;
            //     stopMapPixels[i].a = 1.0f;
            // }

            BlendMaps = new Texture2D(TexWidth, numBlendTex);
            BlendMaps.SetPixels(blendMapPixels);
            BlendMaps.Apply();

            StopMaps = new Texture2D(TexWidth, numBlendTex);
            StopMaps.filterMode = FilterMode.Point;
            StopMaps.SetPixels(stopMapPixels);
            StopMaps.Apply();

            Textures = TextureUtilities.MakeTextureGradientVertical(textures);

            // DEBUG: Save blendmap and stopmap for documentation
            // System.IO.File.WriteAllBytes("./blendmap-alpha.png", BlendMaps.EncodeToPNG());
            // System.IO.File.WriteAllBytes("./stopmap-alpha.png", StopMaps.EncodeToPNG());
            // System.IO.File.WriteAllBytes("./textures.png", Textures.EncodeToPNG());

            // Calculate the aspect ratio of each texture in the set (in
            // particular for lines they may be different)
            // Always use arrays of length 16 so it doesn't become a problem
            // when adding more textures
            AspectRatios = new float[MaxSupportedTextures];
            HeightWidthAspectRatios = new float[MaxSupportedTextures];
            for (int t = 0; t < textures.Count; t++)
            {
                float aspect = textures[t].width / (float)textures[t].height;
                float hwAspect = textures[t].height / (float)textures[t].width;
                AspectRatios[t] = aspect;
                HeightWidthAspectRatios[t] = hwAspect;
            }
        }
    }
}