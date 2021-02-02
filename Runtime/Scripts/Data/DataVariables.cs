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
        string Path { get; }

        T GetMin();
        T GetMax();
        T[] GetArray();
    }

    public class ScalarDataVariable : IDataVariable<float>
    {
        public string Path { get; }

        public float GetMin() {
            return 0.0f;
        }

        public float GetMax() {
            return 0.0f;
        }

        public float[] GetArray() {
            return new float[0];
        }
    }

    public class VectorDataVariable : IDataVariable<Vector3>
    {
        public string Path { get; }

        public Vector3 GetMin() {
            return Vector3.zero;
        }

        public Vector3 GetMax() {
            return Vector3.zero;
        }

        public Vector3[] GetArray() {
            return new Vector3[0];
        }
    }
}