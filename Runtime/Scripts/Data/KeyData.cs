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

    public interface IKeyData : IABRInput
    {
        /// <summary>
        ///     The DataPath that represents this KeyData
        /// </summary>
        string Path { get; }
    }

    public class KeyData : IKeyData, IHasDataset
    {
        public ABRInputGenre Genre { get; } = ABRInputGenre.KeyData;
        public string Path { get; }

        public KeyData(string path)
        {
            Path = path;
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