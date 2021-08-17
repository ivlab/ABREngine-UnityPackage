/* PrimitiveGradient.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Author: Matthias Broske <brosk014@umn.edu>
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
using UnityEngine;

namespace IVLab.ABREngine
{
    public class RawPrimitiveGradient
    {
        public List<float> points;
        public List<string> values;
    }

    public interface IPrimitiveGradient : IABRInput
    {
        IntegerPrimitive InputValue { get; }
        float[] Points { get; }
        string[] Values { get; }
    }

    public class PrimitiveGradient : IPrimitiveGradient
    {
        public ABRInputGenre Genre { get; } = ABRInputGenre.PrimitiveGradient;
        public IntegerPrimitive InputValue { get; }
        public float[] Points { get; }
        public string[] Values { get; }

        public PrimitiveGradient(IntegerPrimitive inputValue, float[] points, string[] values)
        {
            InputValue = inputValue;
            Points = points;
            Values = values;
        }

        public RawABRInput GetRawABRInput()
        {
            return new RawABRInput
            {
                inputType = this.GetType().ToString(),
                inputValue = InputValue.Value.ToString("G"),
                parameterName = "",// TODO
                inputGenre = Genre.ToString("G")
            };
        }
    }

    /*
    public enum PrimitiveGradientType
    {
        Opacitymap
    }

    public interface IFloatPrimitiveGradient : IPrimitiveGradient
    {

    }

    public class FloatPrimitiveGradient : PrimitiveGradient
    {

    }
    public class PercentPrimitive : FloatPrimitiveGradient
    {

    }
    public class BooleanPrimitive : PrimitiveGradient
    {

    }
    public class LengthPrimitive : FloatPrimitiveGradient
    {

    }
    public class AnglePrimitive : FloatPrimitiveGradient
    {

    }
    public class IntegerPrimitive : PrimitiveGradient
    {

    }*/

    /*public class OpacityMapPrimitiveGradient : PrimitiveGradient
    {
        private int width = 1024;
        private int height = 100;

        public OpacityMapPrimitiveGradient(IntegerPrimitive inputValue, float[] points, float[] values) : 
            base(inputValue, points, values)
        {
            if (points.Length == 0 || values.Length == 0)
            {
                Gradient = null;
                return;
            }

            Gradient = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Gradient.wrapMode = TextureWrapMode.Clamp;
            Gradient.filterMode = FilterMode.Bilinear;  // Is bilinear going to cause problems?

            // Sort the points/values array
            Array.Sort(points, values);

            Color[] pixelColors = new Color[width * height];

            // fill colors with zero until first control point
            int firstControlPoint = (int)(width * points[0]);
            for (int p = 0; p < firstControlPoint; p++)
            {
                pixelColors[p] = Color.black;
            }
            // interpolate
            int prevControlPoint = firstControlPoint;
            float prevValue = values[0];
            for (int i = 1; i < points.Length; i++)
            {
                int nextControlPoint = (int) (width * points[i]);
                float nextValue = values[i];
                for (int p = prevControlPoint; p < nextControlPoint; p++)
                {
                    float lerpValue = Mathf.Lerp(prevValue, nextValue, ((float)(p - prevControlPoint) / (nextControlPoint - prevControlPoint)));
                    pixelColors[p] = new Color(lerpValue, lerpValue, lerpValue);
                }
                prevControlPoint = nextControlPoint;
                prevValue = values[i];
            }
            // fill colors with zero until end of texture
            for (int p = prevControlPoint; p < width; p++)
            {
                pixelColors[p] = (p == prevControlPoint) ? new Color(prevValue, prevValue, prevValue) : Color.black;
            }

            // fill in the rest of the pixel colors
            for (int i = 1; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    pixelColors[i * width + j] = pixelColors[(i - 1) * width + j];
                }
            }

            Gradient.SetPixels(pixelColors);
            Gradient.Apply(false);
        }

        public Texture2D Gradient { get; set; } = null;
    }*/
}