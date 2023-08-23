/* KeyData.cs
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
using UnityEngine;
using System.Linq;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Indicator that a particular object has some key data attached to it -
    /// useful for most <see cref="DataImpression"/>s.
    /// </summary>
    public interface IHasKeyData
    {
        /// <summary>
        /// Get any key data object associated with this object
        /// </summary>
        KeyData GetKeyData();

        /// <summary>
        /// Set the Key Data for this object
        /// </summary>
        /// <param name="kd"></param>
        void SetKeyData(KeyData kd);

        /// <summary>
        /// Get the KeyData's expected topology
        /// </summary>
        DataTopology GetKeyDataTopology();
    }

    /// <summary>
    /// Lightweight container for a data object. From Key Data objects, scalar
    /// and vector variables can be obtained (see the example below).
    /// </summary>
    /// <example>
    /// KeyData can be used to easily get <see cref="ScalarDataVariable"/>s and
    /// <see cref="VectorDataVariable"/>s from a dataset.
    /// <code>
    /// public class KeyDataExample : MonoBehaviour
    /// {
    ///     void Start()
    ///     {
    ///         // Load some point data
    ///         string dataPath = "Demo/Wavelet/KeyData/Points";
    ///         KeyData kd = ABREngine.Instance.Data.LoadData(dataPath);
    ///
    ///         // Then, we can fetch variables:
    ///         // ALL the variables
    ///         kd.GetScalarVariables();
    ///         kd.GetVectorVariables();
    ///
    ///         // Only the names of the variables
    ///         kd.GetScalarVariableNames();
    ///         kd.GetVectorVariableNames();
    ///
    ///         // Fetch a specific scalar or vector variable by its name
    ///         kd.GetScalarVariable("XAxis");
    ///         kd.GetVectorVariable("Inward");
    ///     }
    /// }
    /// </code>
    /// </example>
    public class KeyData : IHasDataset, IABRInput
    {
        public ABRInputGenre Genre { get; } = ABRInputGenre.KeyData;
        public string Path { get; }
        public DataTopology Topology { get; }

        public KeyData(string path, DataTopology topology)
        {
            Path = path;
            Topology = topology;
        }

        /// <summary>
        /// Get all of the scalar data variables associated with this key data object
        /// </summary>
        public ScalarDataVariable[] GetScalarVariables()
        {
            return GetDataset().GetScalarVariables(this);
        }

        /// <summary>
        /// Get the names of every scalar variable associated with this key data object
        /// </summary>
        public string[] GetScalarVariableNames()
        {
            return GetDataset().GetScalarVariables(this).Select(var => DataPath.GetName(var.Path)).ToArray();
        }

        /// <summary>
        /// Get a specific scalar variable that exists within this key data object
        /// </summary>
        public ScalarDataVariable GetScalarVariable(string varName)
        {
            ScalarDataVariable[] matches = GetDataset()
                .GetScalarVariables(this)
                .Where(var => DataPath.GetName(var.Path) == varName)
                .ToArray();
            if (matches.Length > 0) {
                return matches[0];
            } else {
                return null;
            }
        }

        /// <summary>
        /// Get all of the vector data variables associated with this key data object
        /// </summary>
        public VectorDataVariable[] GetVectorVariables()
        {
            return GetDataset().GetVectorVariables(this);
        }

        /// <summary>
        /// Get the names of every vector variable associated with this key data object
        /// </summary>
        public string[] GetVectorVariableNames()
        {
            return GetDataset().GetVectorVariables(this).Select(var => DataPath.GetName(var.Path)).ToArray();
        }

        /// <summary>
        /// Get a specific vector variable that exists within this key data object
        /// </summary>
        public VectorDataVariable GetVectorVariable(string varName)
        {
            VectorDataVariable[] matches = GetDataset()
                .GetVectorVariables(this)
                .Where(var => DataPath.GetName(var.Path) == varName)
                .ToArray();
            if (matches.Length > 0) {
                return matches[0];
            } else {
                return null;
            }
        }

        public Dataset GetDataset()
        {
            string datasetPath = DataPath.GetDatasetPath(Path);
            Dataset dataset;
            ABREngine.Instance.Data.TryGetDataset(datasetPath, out dataset);
            return dataset;
        }

        public RawABRInput GetRawABRInput()
        {
            return new RawABRInput {
                inputType = this.GetType().ToString(),
                inputValue = this.Path,
                parameterName = "",// TODO
                inputGenre = Genre.ToString("G"),
            };
        }
    }

    public interface IKeyDataRenderInfo { }
}