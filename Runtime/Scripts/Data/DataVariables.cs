/* DataVariables.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using UnityEngine;

namespace IVLab.ABREngine
{
    public interface IDataVariable<T>
    {
        /// <summary>
        ///     The DataPath that represents this variable
        /// </summary>
        string Path { get; }

        /// <summary>
        ///     MinValue is calculated by the DataManager when it imports a new
        ///     dataset. MinValue is the smallest value encountered across every
        ///     instance of this variable, across all datasets.
        /// </summary>
        T MinValue { get; set; }

        /// <summary>
        ///     MaxValue is calculated by the DataManager when it imports a new
        ///     dataset. MaxValue is the largest value encountered across every
        ///     instance of this variable, across all datasets.
        /// </summary>
        T MaxValue { get; set; }

        /// <summary>
        ///     Get the actual data values in the context of this particular Key
        ///     Data object
        /// </summary>
        T[] GetArray(IKeyData keyData);
    }

    public class ScalarDataVariable : IDataVariable<float>, IHasDataset
    {
        public string Path { get; }
        public float MinValue { get; set; }
        public float MaxValue { get; set; }

        public ScalarDataVariable(string path)
        {
            Path = path;
        }

        public float[] GetArray(IKeyData keyData) {
            // Get the actual name of this variable
            string varName = DataPath.GetName(Path);

            // Get the raw dataset
            RawDataset dataset;
            DataManager.Instance.TryGetRawDataset(keyData.Path, out dataset);

            // Return the scalar array
            return dataset?.GetScalarArray(varName);
        }

        public Dataset GetDataset()
        {
            string datasetPath = DataPath.GetDatasetPath(Path);
            Dataset dataset;
            DataManager.Instance.TryGetDataset(datasetPath, out dataset);
            return dataset;
        }
    }

    public class VectorDataVariable : IDataVariable<Vector3>, IHasDataset
    {
        public string Path { get; }
        public Vector3 MinValue { get; set; }
        public Vector3 MaxValue { get; set; }

        public Vector3[] GetArray(IKeyData keyData) {
            return new Vector3[0];
        }

        public Dataset GetDataset()
        {
            string datasetPath = DataPath.GetDatasetPath(Path);
            Dataset dataset;
            DataManager.Instance.TryGetDataset(datasetPath, out dataset);
            return dataset;
        }
    }
}