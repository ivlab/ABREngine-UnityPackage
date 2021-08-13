/* DataVariables.cs
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

using System.Collections.Generic;
using UnityEngine;

public class DataRange<T>
{
    public T min;
    public T max;
}

namespace IVLab.ABREngine
{
    public interface IDataVariable<T> : IHasDataset, IABRInput
    {
        /// <summary>
        ///     The DataPath that represents this variable
        /// </summary>
        string Path { get; }

        /// <summary>
        ///     Range is calculated by the DataManager when it imports a new
        ///     dataset. Range is calculated from the smallest/largest values
        ///     encountered across every instance of this variable, across all
        ///     datasets.
        /// </summary>
        DataRange<T> Range { get; set; }

        /// <summary>
        ///     Save the original range in case the user wants to reset it
        ///     later.
        /// </summary>
        DataRange<T> OriginalRange { get; set; }

        /// <summary>
        ///     Have this var's ranges been customized?
        /// </summary>
        bool CustomizedRange { get; set; }

        /// <summary>
        /// List of keyData paths that have specific ranges for this variable
        /// </summary>
        List<DataRange<T>> SpecificRanges { get; set; }

        /// <summary>
        ///     Get the actual data values in the context of this particular Key
        ///     Data object
        /// </summary>
        T[] GetArray(IKeyData keyData);

        /// <summary>
        ///     Determine if this variable is a part of the key data
        /// </summary>
        bool IsPartOf(IKeyData keyData);
    }

    public class ScalarDataVariable : IDataVariable<float>, IHasDataset
    {
        public ABRInputGenre Genre { get; } = ABRInputGenre.Variable;
        public string Path { get; }

        public DataRange<float> Range { get; set; }
        public DataRange<float> OriginalRange { get; set; }
        public bool CustomizedRange { get; set; }
        public List<DataRange<float>> SpecificRanges { get; set; }

        public ScalarDataVariable(string path)
        {
            Path = path;
        }

        public bool IsPartOf(IKeyData keyData)
        {
            // Get the actual name of this variable
            string varName = DataPath.GetName(Path);

            // Get the raw dataset
            RawDataset dataset;
            if (ABREngine.Instance.Data.TryGetRawDataset(keyData.Path, out dataset))
            {
                return dataset.HasScalarArray(varName);
            }
            else
            {
                return false;
            }
        }

        public float[] GetArray(IKeyData keyData) {
            // Get the actual name of this variable
            string varName = DataPath.GetName(Path);

            // Get the raw dataset
            RawDataset dataset;
            ABREngine.Instance.Data.TryGetRawDataset(keyData.Path, out dataset);

            // Return the scalar array
            return dataset?.GetScalarArray(varName);
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
                inputValue = Path,
                parameterName = "",// TODO
                inputGenre = Genre.ToString("G"),
            };
        }
    }

    public class VectorDataVariable : IDataVariable<Vector3>, IHasDataset
    {
        public ABRInputGenre Genre { get; } = ABRInputGenre.Variable;
        public string Path { get; }

        public DataRange<Vector3> Range { get; set; }
        public DataRange<Vector3> OriginalRange { get; set; }
        public bool CustomizedRange { get; set; }
        public List<DataRange<Vector3>> SpecificRanges { get; set; }

        public VectorDataVariable(string path)
        {
            Path = path;
        }

        public bool IsPartOf(IKeyData keyData)
        {
            // Get the actual name of this variable
            string varName = DataPath.GetName(Path);

            // Get the raw dataset
            RawDataset dataset;
            if (ABREngine.Instance.Data.TryGetRawDataset(keyData.Path, out dataset))
            {
                return dataset.HasVectorArray(varName);
            }
            else
            {
                return false;
            }
        }

        public Vector3[] GetArray(IKeyData keyData) {
            // Get the actual name of this variable
            string varName = DataPath.GetName(Path);

            // Get the raw dataset
            RawDataset dataset;
            ABREngine.Instance.Data.TryGetRawDataset(keyData.Path, out dataset);

            // Return the vector array
            return dataset?.GetVectorArray(varName);
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
                inputValue = Path,
                parameterName = "",// TODO
                inputGenre = Genre.ToString("G"),
            };
        }
    }
}