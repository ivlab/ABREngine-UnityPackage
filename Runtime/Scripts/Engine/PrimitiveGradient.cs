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
        Guid InputValue { get; }
        float[] Points { get; }
        string[] Values { get; }
    }

    public class PrimitiveGradient : IPrimitiveGradient
    {
        public ABRInputGenre Genre { get; } = ABRInputGenre.PrimitiveGradient;
        public Guid InputValue { get; }
        public float[] Points { get; }
        public string[] Values { get; }

        public PrimitiveGradient(Guid inputValue, float[] points, string[] values)
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
                inputValue = InputValue.ToString("G"),
                parameterName = "",// TODO
                inputGenre = Genre.ToString("G")
            };
        }
    }
}