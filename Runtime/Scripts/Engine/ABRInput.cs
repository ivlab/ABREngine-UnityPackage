/* ABRInput.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
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

namespace IVLab.ABREngine
{
    /// <summary>
    ///     Possible genres of a visualization input
    /// </summary>
    public enum ABRInputGenre
    {
        KeyData,
        Variable,
        VisAsset,
        Primitive,
        PrimitiveGradient,
    }

    /// <summary>
    ///     Raw string values from a state JSON being passed to ABR
    ///
    ///     Matches `InputValue` definition from ABR State Schema
    ///
    ///     Parameters can have one or more inputs
    /// </summary>
    public class RawABRInput
    {
        /// <summary>
        ///     String representation of the C# type this ABR input is
        /// </summary>
        public string inputType;

        /// <summary>
        ///     The actual value of the input (string representation)
        /// </summary>
        public string inputValue;

        /// <summary>
        ///     The name of the parent parameter this input is associated with
        /// </summary>
        public string parameterName;

        /// <summary>
        ///     What type of input is it (variable, visasset, etc.)
        /// </summary>
        public string inputGenre;
    }

    /// <summary>
    ///     Interface that includes every input to a data impression. Every type
    ///     of ABR input should fit into a specific ABRInputGenre.
    /// </summary>
    public interface IABRInput {
        ABRInputGenre Genre { get; }

        RawABRInput GetRawABRInput();
    }
}