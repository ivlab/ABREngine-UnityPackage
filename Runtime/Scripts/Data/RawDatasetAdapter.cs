/* RawDatasetAdapter.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
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
using IVLab.OBJImport;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Adapter for converting other formats to ABRDataFormat. For example, OBJs
    /// => surfaces, or lists of points => ribbons, or lists of points =>
    /// glyphs.
    /// </summary>
    public static class RawDatasetAdapter
    {
        /// <summary>
        /// Load data from the data source.
        /// </summary>
        /// <param name="filePath">Data source file</param>
        public static RawDataset ObjToSurface(string filePath)
        {
            OBJLoader loader = new OBJLoader();
            GameObject surfaceData = loader.Load(filePath, true);

            Mesh mesh = surfaceData.GetComponentInChildren<MeshFilter>()?.mesh;

            RawDataset ds = new RawDataset();
            ds.bounds = mesh.bounds;
            ds.meshTopology = MeshTopology.Triangles;

            ds.vectorArrays = new SerializableVectorArray[0];
            ds.vectorArrayNames = new string[0];

            ds.scalarArrayNames = new string[0];
            ds.scalarMins = new float[0];
            ds.scalarMaxes = new float[0];
            ds.scalarArrays = new SerializableFloatArray[1];

            ds.vertexArray = mesh.vertices;
            ds.indexArray = mesh.triangles;

            // We will not populate any data yet
            ds.cellIndexCounts = new int[mesh.triangles.Length / 3];
            ds.cellIndexOffsets = new int[mesh.triangles.Length / 3];

            GameObject.Destroy(surfaceData);
            return ds;
        }
    }
}