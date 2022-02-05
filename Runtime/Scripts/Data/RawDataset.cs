/* RawDataset.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson
 * <sethalanjohnson@gmail.com>
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
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace IVLab.ABREngine
{
    [System.Serializable]
    public class SerializableFloatArray
    {
        [SerializeField]
        public float[] array;
    }
    [System.Serializable]
    public class SerializableVectorArray
    {
        [SerializeField]
        public Vector3[] array;
    }

    /// <summary>
    ///     The raw variable arrays and geometry for a Data Object. RawDatasets
    ///     can be loaded from a pair of .json and .bin files (JsonHeader and
    ///     BinaryData, respectively). This RawDataset defines the specification
    ///     for each of these files. RawDataset is not to be confused with
    ///     `Dataset`, which represents a *collection* of RawDatasets which
    ///     share a coordinate space, key data, and variables.
    /// </summary>
    /// <example>
    /// A simple 4-vertex plane with no variables can be created like this:
    /// <code>
    /// RawDataset ds = new RawDataset();
    /// ds.meshTopology = MeshTopology.Triangles;
    /// ds.bounds = new Bounds(Vector3.zero, Vector3.one * 2.0f);
    ///
    /// ds.vectorArrays = new SerializableVectorArray[0];
    /// ds.vectorArrayNames = new string[0];
    /// ds.scalarArrays = new SerializableFloatArray[0];
    /// ds.scalarArrayNames = new string[0];
    /// ds.scalarMins = new float[0];
    /// ds.scalarMaxes = new float[0];
    ///
    /// // Construct the vertices
    /// Vector3[] vertices = {
    ///     new Vector3(-1, 0, -1), // 0
    ///     new Vector3( 1, 0, -1), // 1
    ///     new Vector3(-1, 0,  1), // 2
    ///     new Vector3( 1, 0,  1), // 3
    /// };
    ///
    /// ds.vertexArray = vertices;
    /// // Construct triangle indices/faces - LEFT HAND RULE, outward-facing normals
    /// int[] indices = {
    ///     // Bottom face
    ///     0, 1, 3,
    ///     0, 3, 2
    /// };
    ///
    /// ds.indexArray = indices;
    /// // How many verts per cell are there? (each triangle is a cell)
    /// int[] cellIndexCounts = { 3, 3 };
    /// ds.cellIndexCounts = cellIndexCounts;
    ///
    /// // Where does each cell begin?
    /// int[] cellIndexOffsets = { 0, 3 };
    /// ds.cellIndexOffsets = cellIndexOffsets;
    /// </code>
    /// </example>
    [System.Serializable]
    public class RawDataset
    {
        [SerializeField]
        public Vector3[] vertexArray;

        [SerializeField]
        public SerializableVectorArray[] vectorArrays;

        [SerializeField]
        public string[] vectorArrayNames;

        [SerializeField]
        public SerializableFloatArray[] scalarArrays;

        [SerializeField]
        public string[] scalarArrayNames;


        [SerializeField]
        public float[] scalarMins;

        [SerializeField]
        public float[] scalarMaxes;

        [SerializeField]
        public int[] indexArray;

        [SerializeField]
        public int[] cellIndexOffsets;

        [SerializeField]
        public int[] cellIndexCounts;

        [SerializeField]
        public Bounds bounds;

        [SerializeField]
        public Vector3Int dimensions;

        [SerializeField]
        public DataTopology dataTopology = DataTopology.Points;

        /// <summary>
        /// Header that contains metadata for a particular RawDataset
        /// </summary>
        public class JsonHeader
        {
            public DataTopology meshTopology;
            public int num_points;
            public int num_cells;
            public int num_cell_indices;
            public string[] scalarArrayNames;
            public string[] vectorArrayNames;
            public Bounds bounds;
            public int[] dimensions;
            public float[] scalarMaxes;
            public float[] scalarMins;
        }

        /// <summary>
        /// Actual geometric representation of the data to load from a file / socket
        /// </summary>
        public class BinaryData
        {
            public float[] vertices { get; set; }
            public int[] index_array { get; set; }
            public float[][] scalar_arrays { get; set; }
            public float[][] vector_arrays { get; set; }

            public void Decode(JsonHeader bdh, byte[] bytes)
            {
                int offset = 0;
                int nbytes;

                // No vertices stored in binary for volumes
                if (bdh.meshTopology != DataTopology.Voxels)
                {
                    vertices = new float[3 * bdh.num_points];
                    nbytes = 3 * bdh.num_points * sizeof(float);
                    Buffer.BlockCopy(bytes, offset, vertices, 0, nbytes);
                    offset = offset + nbytes;
                }

                index_array = new int[bdh.num_cell_indices];
                nbytes = bdh.num_cell_indices * sizeof(int);
                Buffer.BlockCopy(bytes, offset, index_array, 0, nbytes);
                offset = offset + nbytes;

                scalar_arrays = new float[bdh.scalarArrayNames.Length][];
                nbytes = bdh.num_points * sizeof(float);
                for (int i = 0; i < bdh.scalarArrayNames.Length; i++)
                {
                    scalar_arrays[i] = new float[bdh.num_points];
                    Buffer.BlockCopy(bytes, offset, scalar_arrays[i], 0, nbytes);
                    offset = offset + nbytes;
                }

                vector_arrays = new float[bdh.vectorArrayNames.Length][];
                nbytes = 3 * bdh.num_points * sizeof(float);
                for (int i = 0; i < bdh.vectorArrayNames.Length; i++)
                {
                    vector_arrays[i] = new float[3 * bdh.num_points];
                    Buffer.BlockCopy(bytes, offset, vector_arrays[i], 0, nbytes);
                    offset = offset + nbytes;
                }
            }

            public BinaryData(JsonHeader bdh, string file)
            {
                byte[] bytes = File.ReadAllBytes(file);
                Decode(bdh, bytes);
            }

            public BinaryData(JsonHeader bdh, byte[] bytes)
            {
                Decode(bdh, bytes);
            }
        }

        public RawDataset() { }

        public RawDataset(JsonHeader jh, BinaryData bd)
        {
            dataTopology = jh.meshTopology;

            if (dataTopology == DataTopology.Voxels)
            {
                dimensions = new Vector3Int(jh.dimensions[0], jh.dimensions[1], jh.dimensions[2]);
            }
            else
            {
                vertexArray = new Vector3[jh.num_points];
                for (int i = 0; i < jh.num_points; i++)
                {
                    vertexArray[i][0] = bd.vertices[i * 3 + 0];
                    vertexArray[i][1] = bd.vertices[i * 3 + 1];
                    vertexArray[i][2] = bd.vertices[i * 3 + 2];
                }
            }

            long numIndices = 0;
            if (dataTopology == DataTopology.Points || dataTopology == DataTopology.Voxels)
                numIndices = jh.num_cells;
            else
            {
                long indx = 0;
                for (int i = 0; i < jh.num_cells; i++)
                {
                    long k = bd.index_array[indx];
                    numIndices = numIndices + bd.index_array[indx];
                    indx = indx + k + 1;
                }
            }

            cellIndexOffsets = new int[jh.num_cells];
            cellIndexCounts = new int[jh.num_cells];
            indexArray = new int[numIndices];

            int src_indx = 0;
            int dst_indx = 0;
            for (long c = 0; c < jh.num_cells; c++)
            {
                cellIndexOffsets[c] = dst_indx;
                cellIndexCounts[c] = bd.index_array[src_indx];
                src_indx = src_indx + 1;

                for (long p = 0; p < cellIndexCounts[c]; p++)
                {
                    indexArray[dst_indx] = bd.index_array[src_indx];
                    src_indx++;
                    dst_indx++;
                }
            }

            bounds = jh.bounds;
            scalarArrayNames = jh.scalarArrayNames;
            vectorArrayNames = jh.vectorArrayNames;
            scalarMins = jh.scalarMins;
            scalarMaxes = jh.scalarMaxes;

            scalarArrays = new SerializableFloatArray[jh.scalarArrayNames.Count()];
            for (int i = 0; i < scalarArrayNames.Count(); i++)
            {
                scalarArrays[i] = new SerializableFloatArray();
                scalarArrays[i].array = bd.scalar_arrays[i];
            }

            vectorArrays = new SerializableVectorArray[jh.vectorArrayNames.Count()];
            for (int i = 0; i < jh.vectorArrayNames.Count(); i++)
            {
                vectorArrays[i] = new SerializableVectorArray();
                vectorArrays[i].array = new Vector3[jh.num_points];
                for (int j = 0; j < jh.num_points; j++)
                {
                    vectorArrays[i].array[j][0] = bd.vector_arrays[i][j * 3 + 0];
                    vectorArrays[i].array[j][1] = bd.vector_arrays[i][j * 3 + 1];
                    vectorArrays[i].array[j][2] = bd.vector_arrays[i][j * 3 + 2];
                }
            }
        }

        private Dictionary<string, int> _vectorDictionary;
        private Dictionary<string, int> vectorDictionary
        {
            get
            {
                if (_vectorDictionary == null)
                {
                    _vectorDictionary = new Dictionary<string, int>();
                    for (int i = 0; i < vectorArrayNames.Length; i++)
                    {
                        _vectorDictionary[vectorArrayNames[i]] = i;
                    }
                }
                return _vectorDictionary;

            }
        }


        public Vector3[] GetVectorArray(string name)
        {
            int index;

            if (vectorDictionary.TryGetValue(name, out index))
                return vectorArrays?[index].array;
            else return null;
        }



        private Dictionary<string, int> _scalarDictionary;
        private Dictionary<string, int> scalarDictionary
        {
            get
            {
                if (_scalarDictionary == null || _scalarDictionary.Count != scalarArrayNames.Length)
                {
                    _scalarDictionary = new Dictionary<string, int>();
                    for (int i = 0; i < scalarArrayNames.Length; i++)
                    {
                        _scalarDictionary[scalarArrayNames[i]] = i;
                    }
                }
                return _scalarDictionary;

            }
        }

        public bool HasScalarArray(string name)
        {
            return scalarDictionary.ContainsKey(name);
        }

        public bool HasVectorArray(string name)
        {
            return vectorDictionary.ContainsKey(name);
        }

        public float[] GetScalarArray(string name)
        {
            int index;

            if (scalarDictionary.TryGetValue(name, out index))
                return scalarArrays[index].array;
            else
                return null;

        }
        public float GetScalarMin(string name)
        {
            int index;
            scalarDictionary.TryGetValue(name, out index);
            return scalarMins[index];
        }

        public float GetScalarMax(string name)
        {
            int index;
            scalarDictionary.TryGetValue(name, out index);
            return scalarMaxes[index];
        }

        // TODO: Not implemented in the data schema yet
        public Vector3 GetVectorMin(string name)
        {
            // int index;
            // scalarDictionary.TryGetValue(name, out index);
            // return scalarMins[index];
            return Vector3.zero;
        }

        // TODO: Not implemented in the data schema yet
        public Vector3 GetVectorMax(string name)
        {
            // int index;
            // scalarDictionary.TryGetValue(name, out index);
            // return scalarMaxes[index];
            return Vector3.zero;
        }
    }

}