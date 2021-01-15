using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Xml;

namespace IVLab.ABREngine {
    // Inspired by ColorLoom:
    // https://github.umn.edu/ABRAM308/SculptingVisWebApp/blob/master/applets/static/color-loom/sketch.js
    class ColormapInternal {
        public List<Tuple<float, Color>> entries = new List<Tuple<float, Color>>();

        public ColormapInternal() { }

        public void AddControlPoint(float dataVal, Color color)
        {
            this.entries.Add(Tuple.Create(dataVal, color));
            this.entries.Sort((e1, e2) => {
                if (e1.Item1 < e2.Item1)
                {
                    return -1;
                }
                else if (e1.Item1 > e2.Item1)
                {
                    return 1;
                }
                else {
                    return 0;
                }
            });
        }

        public Color LookupColor(float dataVal)
        {
            if (this.entries.Count == 0)
            {
                return Color.black;
            }
            else if (this.entries.Count == 1)
            {
                return this.entries[0].Item2;
            }
            else
            {
                float minVal = this.entries[0].Item1;
                float maxVal = this.entries[this.entries.Count - 1].Item1;

                // check bounds
                if (dataVal >= maxVal)
                {
                    return this.entries[this.entries.Count - 1].Item2;
                }
                else if (dataVal <= minVal)
                {
                    return this.entries[0].Item2;
                }
                else
                {  // value within bounds

                    // make i = upper control pt and (i-1) = lower control point
                    int i = 1;
                    while (this.entries[i].Item1 < dataVal)
                    {
                        i++;
                    }

                    // convert the two control points to lab space, interpolate
                    // in lab space, then convert back to rgb space
                    Color c1 = this.entries[i - 1].Item2;
                    Color c2 = this.entries[i].Item2;

                    List<float> rgb1 = Lab2Rgb.color2list(c1);
                    List<float> rgb2 = Lab2Rgb.color2list(c2);
                    List<float> lab1 = Lab2Rgb.rgb2lab(rgb1);
                    List<float> lab2 = Lab2Rgb.rgb2lab(rgb2);

                    float v1 = this.entries[i - 1].Item1;
                    float v2 = this.entries[i].Item1;
                    float alpha = (dataVal - v1) / (v2 - v1);

                    List<float> labFinal = new List<float> {
                        lab1[0] * (1.0f - alpha) + lab2[0] * alpha,
                        lab1[1] * (1.0f - alpha) + lab2[1] * alpha,
                        lab1[2] * (1.0f - alpha) + lab2[2] * alpha
                    };

                    List<float> rgbFinal = Lab2Rgb.lab2rgb(labFinal);

                    return Lab2Rgb.list2color(rgbFinal);
                }
            } 
        }
    }

    public class ColormapUtilities
    {
        public static Texture2D ColormapFromFile(string filePath, int texWidth=1024, int texHeight=100)
        {
            string extention = Path.GetExtension(filePath);

            // Read PNG file and produce a Texture2D
            Texture2D image;
            if (extention.ToUpper() == ".PNG")
            {
                // Create and return the image
                image = new Texture2D(1, 1);
                image.LoadImage(File.ReadAllBytes(filePath));
            }
            else if (extention.ToUpper() == ".XML")
            {
                // Read XML file and produce a Texture2D
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);

                XmlNode colormapNode = doc.DocumentElement.SelectSingleNode("/ColorMaps/ColorMap");
                if (colormapNode == null)
                    colormapNode = doc.DocumentElement.SelectSingleNode("/ColorMap");
                string name = colormapNode.Attributes.GetNamedItem("name") != null ? colormapNode.Attributes.GetNamedItem("name").Value : Path.GetFileName(filePath);

                ColormapInternal colormap = new ColormapInternal();

                foreach (XmlNode pointNode in colormapNode.SelectNodes("Point"))
                {
                    float x = float.Parse(pointNode.Attributes.GetNamedItem("x").Value);
                    float r = float.Parse(pointNode.Attributes.GetNamedItem("r").Value);
                    float g = float.Parse(pointNode.Attributes.GetNamedItem("g").Value);
                    float b = float.Parse(pointNode.Attributes.GetNamedItem("b").Value);

                    Color toAdd = new Color(r, g, b, 1.0f);
                    colormap.AddControlPoint(x, toAdd);
                }

                // Create and return the image
                image = CreateTextureFromColormap(colormap, texWidth, texHeight);
            }
            else
            {
                throw new System.Exception("Colormap must be a .png or a .xml");
                // Read JSON file and produce a Texture2D
                // TODO properly support
            }

            return image;
        }

        // Given a colormap, create a texture by drawing a bunch of little
        // rectangles
        private static Texture2D CreateTextureFromColormap(ColormapInternal colormap, int texWidth=1024, int texHeight=1)
        {
            // Create our texture and get its width
            Texture2D image = new Texture2D(texWidth, texHeight);

            // Initialize to black
            List<Color> pixels = new List<Color>();
            pixels.AddRange(Enumerable.Repeat(Color.black, texWidth * texHeight));

            // Add the first row to the texture
            for (int col = 0; col < texWidth; col++)
            {
                pixels[col] = colormap.LookupColor(col / (float) texWidth);
            }

            // Copy to the rest of the image if greater than one row
            for (int row = 1; row < texHeight; row++)
            {
                for (int col = 0; col < texWidth; col++)
                {
                    pixels[col + row * texWidth] = pixels[col];
                }
            }

            image.SetPixels(pixels.ToArray());
            image.Apply();
            return image;
        }

        public static void SaveTextureAsPng(string path, Texture2D texture)
        {
            byte[] pngBytes = texture.EncodeToPNG();
            File.WriteAllBytes(path, pngBytes);
        }
    }


    // ----- BEGIN EXTERNAL CODE FOR RGB-LAB CONVERSION -----
    // https://github.com/antimatter15/rgb-lab

    /*
    MIT License
    Copyright (c) 2014 Kevin Kwok <antimatter15@gmail.com>
    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:
    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.
    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
    */

    // the following functions are based off of the pseudocode
    // found on www.easyrgb.com

    class Lab2Rgb {
        public static List<float> color2list(Color color)
        {
            return new List<float> {
                color.r,
                color.g,
                color.b
            };
        }

        public static Color list2color(List<float> list)
        {
            return new Color(list[0], list[1], list[2], 1.0f);
        }

        public static List<float> lab2rgb(List<float> lab) {
            float y = (lab[0] + 16.0f) / 116.0f,
                x = lab[1] / 500.0f + y,
                z = y - lab[2] / 200.0f,
                r, g, b;

            x = 0.95047f * ((x * x * x > 0.008856f) ? x * x * x : (x - 16.0f / 116.0f) / 7.787f);
            y = 1.00000f * ((y * y * y > 0.008856f) ? y * y * y : (y - 16.0f / 116.0f) / 7.787f);
            z = 1.08883f * ((z * z * z > 0.008856f) ? z * z * z : (z - 16.0f / 116.0f) / 7.787f);

            r = x * 3.2406f + y * -1.5372f + z * -0.4986f;
            g = x * -0.96890f + y * 1.8758f + z * 0.0415f;
            b = x * 0.05570f + y * -0.2040f + z * 1.0570f;

            r = (r > 0.0031308f) ? (1.055f * Mathf.Pow(r, 1.0f / 2.4f) - 0.055f) : 12.92f * r;
            g = (g > 0.0031308f) ? (1.055f * Mathf.Pow(g, 1.0f / 2.4f) - 0.055f) : 12.92f * g;
            b = (b > 0.0031308f) ? (1.055f * Mathf.Pow(b, 1.0f / 2.4f) - 0.055f) : 12.92f * b;

            // Colors are already 0-1 in Unity
            return new List<float> {
                Mathf.Max(0.0f, Mathf.Min(1.0f, r)),
                Mathf.Max(0.0f, Mathf.Min(1.0f, g)),
                Mathf.Max(0.0f, Mathf.Min(1.0f, b))
            };
        }


        public static List<float> rgb2lab(List<float> rgb) {
            // Colors are already 0-1 in Unity
            // var r = rgb[0] / 255,
            //     g = rgb[1] / 255,
            //     b = rgb[2] / 255,
            float r = rgb[0],
                g = rgb[1],
                b = rgb[2],
                x, y, z;

            r = (r > 0.04045f) ? Mathf.Pow((r + 0.055f) / 1.055f, 2.4f) : r / 12.92f;
            g = (g > 0.04045f) ? Mathf.Pow((g + 0.055f) / 1.055f, 2.4f) : g / 12.92f;
            b = (b > 0.04045f) ? Mathf.Pow((b + 0.055f) / 1.055f, 2.4f) : b / 12.92f;

            x = (r * 0.4124f + g * 0.3576f + b * 0.1805f) / 0.95047f;
            y = (r * 0.2126f + g * 0.7152f + b * 0.0722f) / 1.00000f;
            z = (r * 0.0193f + g * 0.1192f + b * 0.9505f) / 1.08883f;

            x = (x > 0.008856f) ? Mathf.Pow(x, 1.0f / 3.0f) : (7.787f * x) + 16.0f / 116.0f;
            y = (y > 0.008856f) ? Mathf.Pow(y, 1.0f / 3.0f) : (7.787f * y) + 16.0f / 116.0f;
            z = (z > 0.008856f) ? Mathf.Pow(z, 1.0f / 3.0f) : (7.787f * z) + 16.0f / 116.0f;

            return new List<float> {
                (116.0f * y) - 16.0f,
                500.0f * (x - y),
                200.0f * (y - z)
            };
        }
    }

}