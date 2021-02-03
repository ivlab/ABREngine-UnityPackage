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

    public class SurfaceKeyData : IKeyData
    {
        public string Path { get; }

        public SurfaceKeyData(string path)
        {
            Path = path;
        }
    }

    public class PointKeyData : IKeyData
    {
        public string Path { get; }

        public PointKeyData(string path)
        {
            Path = path;
        }
    }

    public class LineKeyData : IKeyData
    {
        public string Path { get; }

        public LineKeyData(string path)
        {
            Path = path;
        }
    }
}