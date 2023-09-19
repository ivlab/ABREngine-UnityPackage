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

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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
        private static readonly HashSet<ABRInputGenre> canConvertGenres = new HashSet<ABRInputGenre>
        {
            ABRInputGenre.KeyData,
            ABRInputGenre.Variable,
            ABRInputGenre.VisAsset,
            ABRInputGenre.Primitive,
        };

        /// <summary>
        ///     String representation of the C# type this ABR input is
        /// </summary>
        public string inputType;

        /// <summary>
        ///     The actual value of the input (string representation)
        /// </summary>
        public string inputValue;

        /// <summary>
        ///     What type of input is it (variable, visasset, etc.)
        /// </summary>
        public string inputGenre;

        /// <summary>
        /// Checks if the type can be converted from a raw to actual ABR input
        /// </summary>
        public bool CanConvertToABRInput(string type)
        {
            foreach (ABRInputGenre genre in canConvertGenres)
            {
                if (genre.ToString("G") == type)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Convert a RawABRInput (strings from JSON) to a real ABR input
        /// assignable to data impressions
        /// </summary>
        /// <returns></returns>
        public IABRInput ToABRInput()
        {
            if (!CanConvertToABRInput(this.inputGenre))
            {
                return null;
            }

            IABRInput possibleInput = null;
            if (this?.inputGenre == ABRInputGenre.KeyData.ToString("G"))
            {
                KeyData keyData = ABREngine.Instance.Data.GetKeyData(this.inputValue);
                if (keyData == null)
                {
                    Debug.LogWarningFormat("Unable to find Key Data `{0}`", this.inputValue);
                    return null;
                }
                possibleInput = keyData as IABRInput;
            }
            else if (this?.inputGenre == ABRInputGenre.Variable.ToString("G"))
            {
                string datasetPath = DataPath.GetDatasetPath(this.inputValue);
                Dataset dataset;
                if (!ABREngine.Instance.Data.TryGetDataset(datasetPath, out dataset))
                {
                    Debug.LogWarningFormat("Unable to find dataset `{0}`", datasetPath);
                    return null;
                }

                if (DataPath.FollowsConvention(this.inputValue, DataPath.DataPathType.ScalarVar))
                {
                    ScalarDataVariable variable;
                    dataset.TryGetScalarVar(this.inputValue, out variable);
                    variable?.SpecificRanges.Clear(); // Will be repopulated later in state
                    possibleInput = variable as IABRInput;
                }
                else if (DataPath.FollowsConvention(this.inputValue, DataPath.DataPathType.VectorVar))
                {
                    VectorDataVariable variable;
                    dataset.TryGetVectorVar(this.inputValue, out variable);
                    variable?.SpecificRanges.Clear(); // Will be repopulated later in state
                    possibleInput = variable as IABRInput;
                }

                if (possibleInput == null)
                {
                    Debug.LogWarningFormat("Unable to find variable `{0}`", this.inputValue);
                    return null;
                }
            }
            else if (this?.inputGenre == ABRInputGenre.VisAsset.ToString("G"))
            {
                IVisAsset visAsset = null;
                ABREngine.Instance.VisAssets.TryGetVisAsset(new Guid(this.inputValue), out visAsset);
                if (visAsset == null)
                {
                    Debug.LogWarningFormat("Unable to find VisAsset `{0}`", this.inputValue);
                    return null;
                }
                possibleInput = visAsset as IABRInput;
            }
            else if (this?.inputGenre == ABRInputGenre.Primitive.ToString("G"))
            {
                // Attempt to construct the primitive from the type
                // provided in the state file
                Type inputType = Type.GetType(this.inputType);
                ConstructorInfo inputCtor =
                    inputType.GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        CallingConventions.HasThis,
                        new Type[] { typeof(string) },
                        null
                );
                string[] args = new string[] { this.inputValue };
                possibleInput = inputCtor?.Invoke(args) as IABRInput;
                if (possibleInput == null)
                {
                    Debug.LogWarningFormat("Unable to create primitive `{0}`", this.inputValue);
                }
            }
            return possibleInput;
        }

        public RawABRInput Copy()
        {
            return (RawABRInput) this.MemberwiseClone();
        }
    }

    /// <summary>
    ///     Interface that includes every input to a data impression. Every type
    ///     of ABR input should fit into a specific ABRInputGenre.
    /// </summary>
    public interface IABRInput
    {
        /// <summary>
        /// "Genre" of the input - is it Data, a visual element, or something else?
        /// </summary>
        ABRInputGenre Genre { get; }

        /// <summary>
        /// Get the "raw" ABR input - the one that is represented in the state JSON
        /// </summary>
        /// <returns></returns>
        RawABRInput GetRawABRInput();
    }
}