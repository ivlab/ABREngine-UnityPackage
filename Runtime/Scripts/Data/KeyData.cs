/* KeyData.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IVLab.ABREngine
{
    public static class KeyDataMapping
    {
        public static Dictionary<MeshTopology, Type> typeMap = new Dictionary<MeshTopology, Type>()
        {
            { MeshTopology.Points, typeof(PointKeyData) },
            { MeshTopology.Triangles, typeof(SurfaceKeyData) },
            { MeshTopology.Quads, typeof(SurfaceKeyData) },
            { MeshTopology.Lines, typeof(LineKeyData) },
            { MeshTopology.LineStrip, typeof(LineKeyData) },
        };
    }

    public interface IKeyData
    {
        /// <summary>
        ///     The DataPath that represents this KeyData
        /// </summary>
        string Path { get; }
    }

    public class KeyData : IKeyData, IHasDataset
    {
        public string Path { get; }

        public KeyData(string path)
        {
            Path = path;
        }

        public Dataset GetDataset()
        {
            string datasetPath = DataPath.GetDatasetPath(Path);
            Dataset dataset;
            DataManager.Instance.TryGetDataset(datasetPath, out dataset);
            return dataset;
        }
    }

    public class SurfaceKeyData : KeyData, IKeyData
    {
        public SurfaceKeyData(string path) : base(path) { }
    }

    public class PointKeyData : KeyData, IKeyData
    {
        public PointKeyData(string path) : base (path) { }
    }

    public class LineKeyData : KeyData, IKeyData
    {
        public LineKeyData(string path) : base(path) { }
    }

    public interface IKeyDataRenderInfo { }
}