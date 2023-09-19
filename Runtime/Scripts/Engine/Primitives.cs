/* Primitives.cs
 *
 * Copyright (c) 2021, University of Minnesota
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
using System.Text.RegularExpressions;

namespace IVLab.ABREngine
{
    /// <summary>
    ///     Representative of a primitive element (usually used in ABR
    ///     Inputs). These should match the primitive input types in the schema.
    /// </summary>
    public interface IPrimitive : IABRInput
    {
        /// <summary>
        ///     The regex to use to convert this from a string. Group named "value"
        ///     should be the floating point number, group named "units" should be the
        ///     units, if any.
        /// </summary>
        Regex ParsingRegex { get; }

        /// <summary>
        ///     Convert the primitive to a string so it can be sent along with
        ///     the state
        /// </summary>
        string ToString();

        /// <summary>
        /// Set the value of the primitive from a string (similar to using the string constructor)
        /// </summary>
        void SetFromString(string value);
    }

    /// <summary>
    ///     Represents a numeric primitive value (e.g. 10deg, 96cm, or 4.6)
    /// </summary>
    public interface IFloatPrimitive : IPrimitive
    {
        float Value { get; }
        string Units { get; }
    }

    /// <summary>
    ///     Represents an integer primitive value
    /// </summary>
    public interface IIntegerPrimitive : IPrimitive
    {
        int Value { get; }
        string Units { get; }
    }

    public class IntegerPrimitive : IIntegerPrimitive
    {
        public ABRInputGenre Genre { get; } = ABRInputGenre.Primitive;
        public int Value { get; protected set; }
        public virtual string Units { get; } = "";
        public virtual Regex ParsingRegex { get; } = new Regex(@"(?<value>\d+)(?<units>)", RegexOptions.Compiled);

        public IntegerPrimitive()
        {
            Value = 0;
        }

        public IntegerPrimitive(int value)
        {
            Value = value;
        }

        public IntegerPrimitive(string value)
        {
            SetFromString(value);
        }

        public override string ToString()
        {
            return Value.ToString() + Units;
        }

        public void SetFromString(string value)
        {
            Value = int.Parse(ParsingRegex.Match(value).Groups["value"].ToString());
        }

        public RawABRInput GetRawABRInput()
        {
            return new RawABRInput {
                inputType = this.GetType().ToString(),
                inputValue = this.ToString(),
                inputGenre = Genre.ToString("G"),
            };
        }

        public static implicit operator IntegerPrimitive(int i) => new IntegerPrimitive(i);
    }

    public class FloatPrimitive : IFloatPrimitive
    {
        public ABRInputGenre Genre { get; } = ABRInputGenre.Primitive;
        public float Value { get; protected set; }
        public virtual string Units { get; } = "";
        public virtual Regex ParsingRegex { get; } = new Regex(@"(?<value>\d+(\.\d+)?)(?<units>)", RegexOptions.Compiled);

        public FloatPrimitive()
        {
            Value = 0.0f;
        }

        public FloatPrimitive(float value)
        {
            Value = value;
        }

        public FloatPrimitive(string value)
        {
            SetFromString(value);
        }

        public override string ToString()
        {
            return Value.ToString() + Units;
        }

        public virtual void SetFromString(string value)
        {
            Value = float.Parse(ParsingRegex.Match(value).Groups["value"].ToString());
        }

        public RawABRInput GetRawABRInput()
        {
            return new RawABRInput {
                inputType = this.GetType().ToString(),
                inputValue = this.ToString(),
                inputGenre = Genre.ToString("G"),
            };
        }
    }

    public class LengthPrimitive : FloatPrimitive
    {
        public override string Units { get; } = "m";
        public override Regex ParsingRegex { get; } = new Regex(@"(?<value>\d+(\.\d+)?)(?<units>m)", RegexOptions.Compiled);

        public LengthPrimitive(float value) : base(value) { }

        public LengthPrimitive(string value)
        {
            SetFromString(value);
        }

        public override void SetFromString(string value)
        {
            var match = ParsingRegex.Match(value);
            Value = float.Parse(match.Groups["value"].ToString());
            var tempUnits = match.Groups["units"].ToString();
            if (tempUnits != Units)
            {
                Debug.LogErrorFormat("Length units `{0}` are not currently supported", tempUnits);
            }
        }

        public static implicit operator LengthPrimitive(float f) => new LengthPrimitive(f);
    }

    public class AnglePrimitive : FloatPrimitive
    {
        public override string Units { get; } = "deg";
        public override Regex ParsingRegex { get; } = new Regex(@"(?<value>\d+(\.\d+)?)(?<units>deg)", RegexOptions.Compiled);

        public AnglePrimitive(float value) : base(value) { }

        public AnglePrimitive(string value)
        {
            SetFromString(value);
        }

        public override void SetFromString(string value)
        {
            var match = ParsingRegex.Match(value);
            Value = float.Parse(match.Groups["value"].ToString());
            var tempUnits = match.Groups["units"].ToString();
            if (tempUnits != Units)
            {
                Debug.LogErrorFormat("Angle units `{0}` are not currently supported", tempUnits);
            }
        }

        public static implicit operator AnglePrimitive(float f) => new AnglePrimitive(f);
    }

    public class PercentPrimitive : FloatPrimitive
    {
        public override string Units { get; } = "%";
        public override Regex ParsingRegex { get; } = new Regex(@"(?<value>\d+(\.\d+)?)(?<units>%)", RegexOptions.Compiled);

        public PercentPrimitive(float value)
        {
            Value = value / 100.0f;
        }

        public PercentPrimitive(string value)
        {
            SetFromString(value);
        }

        public override void SetFromString(string value)
        {
            var match = ParsingRegex.Match(value);
            Value = float.Parse(match.Groups["value"].ToString()) / 100.0f;
            var tempUnits = match.Groups["units"].ToString();
            if (tempUnits != Units)
            {
                Debug.LogErrorFormat("Percent units `{0}` are not currently supported", tempUnits);
            }
        }

        public override string ToString()
        {
            return (Value * 100.0f).ToString() + Units;
        }

        public static implicit operator PercentPrimitive(float f) => new PercentPrimitive(f * 100.0f);
    }

    public class BooleanPrimitive : IPrimitive
    {
        public ABRInputGenre Genre { get; } = ABRInputGenre.Primitive;
        public bool Value { get; protected set; }
        public Regex ParsingRegex { get; } = new Regex(@"^(true)|(false)$", RegexOptions.Compiled);

        public BooleanPrimitive()
        {
            Value = false;
        }

        public BooleanPrimitive(bool value)
        {
            Value = value;
        }

        public BooleanPrimitive(string value)
        {
            SetFromString(value);
        }

        public void SetFromString(string value)
        {
            Value = bool.Parse(ParsingRegex.Match(value).Value);
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public RawABRInput GetRawABRInput()
        {
            return new RawABRInput
            {
                inputType = this.GetType().ToString(),
                inputValue = this.ToString(),
                inputGenre = Genre.ToString("G"),
            };
        }

        public static implicit operator BooleanPrimitive(bool b) => new BooleanPrimitive(b);
    }
}
