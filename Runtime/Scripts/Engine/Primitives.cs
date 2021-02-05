/* Primitives.cs
 *
 * Copyright (c) 2021, University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 */

using UnityEngine;
using System.Text.RegularExpressions;

namespace IVLab.ABREngine
{
    /// <summary>
    ///     Representative of a primitive element (usually used in ABR
    ///     Inputs). These should match the primitive input types in the schema.
    /// </summary>
    public interface IPrimitive
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
            Value = int.Parse(ParsingRegex.Match(value).Groups["value"].ToString());
        }

        public override string ToString()
        {
            return Value.ToString() + Units;
        }
    }

    public class FloatPrimitive : IFloatPrimitive
    {
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
            Value = float.Parse(ParsingRegex.Match(value).Groups["value"].ToString());
        }

        public override string ToString()
        {
            return Value.ToString() + Units;
        }
    }

    public class LengthPrimitive : FloatPrimitive
    {
        public override string Units { get; } = "m";
        public override Regex ParsingRegex { get; } = new Regex(@"(?<value>\d+(\.\d+)?)(?<units>m)", RegexOptions.Compiled);

        public LengthPrimitive(float value) : base(value) { }

        public LengthPrimitive(string value)
        {
            var match = ParsingRegex.Match(value);
            Value = float.Parse(match.Groups["value"].ToString());
            var tempUnits = match.Groups["units"].ToString();
            if (tempUnits != Units)
            {
                Debug.LogErrorFormat("Length units `{0}` are not currently supported", tempUnits);
            }
        }
    }

    public class AnglePrimitive : FloatPrimitive
    {
        public override string Units { get; } = "deg";
        public override Regex ParsingRegex { get; } = new Regex(@"(?<value>\d+(\.\d+)?)(?<units>deg)", RegexOptions.Compiled);

        public AnglePrimitive(float value) : base(value) { }

        public AnglePrimitive(string value)
        {
            var match = ParsingRegex.Match(value);
            Value = float.Parse(match.Groups["value"].ToString());
            var tempUnits = match.Groups["units"].ToString();
            if (tempUnits != Units)
            {
                Debug.LogErrorFormat("Angle units `{0}` are not currently supported", tempUnits);
            }
        }
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
    }
}
