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
using System.Linq;
using System.IO;

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
            RawDataset ds = MeshToSurface(mesh, null);
            GameObject.Destroy(surfaceData);
            return ds;
        }

        /// <summary>
        /// Create a surfaces data object from a Unity mesh
        /// </summary>
        /// <param name="mesh">The original mesh</param>
        public static RawDataset MeshToSurface(Mesh mesh, Dictionary<string, List<float>> scalarVars)
        {
            RawDataset ds = new RawDataset();
            ds.bounds = mesh.bounds;
            ds.dataTopology = DataTopology.Triangles;

            ds.vectorArrays = new SerializableVectorArray[0];
            ds.vectorArrayNames = new string[0];

            int numScalars = scalarVars?.Count ?? 0;
            ds.scalarArrayNames = new string[numScalars];
            ds.scalarMins = new float[numScalars];
            ds.scalarMaxes = new float[numScalars];
            ds.scalarArrays = new SerializableFloatArray[numScalars];

            // Build the scalar arrays, if present
            if (scalarVars != null)
            {
                int scalarIndex = 0;
                foreach (var kv in scalarVars)
                {
                    ds.scalarArrayNames[scalarIndex] = kv.Key;
                    ds.scalarArrays[scalarIndex] = new SerializableFloatArray() { array = kv.Value.ToArray() };
                    ds.scalarMins[scalarIndex] = kv.Value.Min();
                    ds.scalarMaxes[scalarIndex] = kv.Value.Max();
                    scalarIndex += 1;
                }
            }

            ds.vertexArray = mesh.vertices;
            ds.indexArray = mesh.triangles;

            // We will not populate any data yet
            ds.cellIndexCounts = new int[mesh.triangles.Length / 3];
            ds.cellIndexOffsets = new int[mesh.triangles.Length / 3];
            return ds;
        }

        /// <summary>
        /// Create a Surface data object from a Unity primitive. By default,
        /// includes XAxis, YAxis, and ZAxis scalar variables.
        /// </summary>
        public static RawDataset UnityPrimitiveToSurface(PrimitiveType primitive)
        {
            GameObject prim = GameObject.CreatePrimitive(primitive);
            Mesh mesh = prim.GetComponent<MeshFilter>().mesh;
            GameObject.Destroy(prim);

            // Construct scalar variables
            List<float> x = new List<float>();
            List<float> y = new List<float>();
            List<float> z = new List<float>();
            foreach (Vector3 v in mesh.vertices)
            {
                x.Add(v.x);
                y.Add(v.y);
                z.Add(v.z);
            }
            Dictionary<string, List<float>> scalarVars = new Dictionary<string, List<float>>()
            {
                { "XAxis", x },
                { "YAxis", y },
                { "ZAxis", z },
            };
            return MeshToSurface(mesh, scalarVars);
        }


        /// <summary>
        /// Define a Line dataset from a bunch of points. Don't try to assume or
        /// calculate the full bounds for the imported data objects - explictly
        /// ask the user for them.
        /// </summary>
        /// <param name="lines">One, or several, lines. Each line consistes of a series of points.</param>
        public static RawDataset PointsToLine(List<List<Vector3>> lines, Bounds dataBounds, Dictionary<string, List<float>> scalarVars)
        {
            // Find out lengths of each line so we know where to split
            List<Vector3> allPoints = new List<Vector3>();
            foreach (List<Vector3> linePoints in lines)
            {
                allPoints.AddRange(linePoints);
                allPoints.Add(float.NaN * Vector3.one); // Add a NaN to separate the lines
            }

            RawDataset ds = new RawDataset();
            ds.dataTopology = DataTopology.LineStrip;
            ds.bounds = dataBounds;

            ds.vectorArrays = new SerializableVectorArray[0];
            ds.vectorArrayNames = new string[0];

            int numScalars = scalarVars?.Count ?? 0;
            ds.scalarArrayNames = new string[numScalars];
            ds.scalarMins = new float[numScalars];
            ds.scalarMaxes = new float[numScalars];
            ds.scalarArrays = new SerializableFloatArray[numScalars];

            // Build the scalar arrays, if present
            if (scalarVars != null)
            {
                int scalarIndex = 0;
                foreach (var kv in scalarVars)
                {
                    ds.scalarArrayNames[scalarIndex] = kv.Key;
                    ds.scalarArrays[scalarIndex] = new SerializableFloatArray() { array = kv.Value.ToArray() };
                    ds.scalarMins[scalarIndex] = kv.Value.Min();
                    ds.scalarMaxes[scalarIndex] = kv.Value.Max();
                    scalarIndex += 1;
                }
            }

            // Build the ribbon (line strip)'s vertices. Create several segments of
            // a ribbon if there are NaNs, instead of connecting through the NaN.
            Vector3[] vertices = new Vector3[allPoints.Count];
            int[] indices = new int[allPoints.Count];
            List<int> segmentCounts = new List<int>();
            List<int> segmentStartIndices = new List<int>();
            int lineIndex = 0;
            int segmentCount = 0;
            int segmentStartIndex = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!float.IsNaN(allPoints[i].x) && !float.IsNaN(allPoints[i].y) && !float.IsNaN(allPoints[i].z))
                {
                    if (segmentCount == 0)
                    {
                        segmentStartIndex = lineIndex;
                    }
                    vertices[lineIndex] = allPoints[i];
                    indices[lineIndex] = lineIndex;
                    lineIndex += 1;
                    segmentCount += 1;
                }
                else
                {
                    if (segmentCount > 0)
                    {
                        segmentCounts.Add(segmentCount);
                        segmentStartIndices.Add(segmentStartIndex);
                        segmentCount = 0;
                    }
                }
            }

            // Add the last segment
            if (segmentCount > 0)
            {
                segmentCounts.Add(segmentCount);
                segmentStartIndices.Add(segmentStartIndex);
                segmentCount = 0;
            }
            ds.vertexArray = vertices;
            ds.indexArray = indices;

            ds.cellIndexCounts = segmentCounts.ToArray();
            ds.cellIndexOffsets = segmentStartIndices.ToArray();

            return ds;
        }

        /// <summary>
        /// Define a Point dataset from a bunch of points. Don't try to assume or
        /// calculate the full bounds for the imported data objects - explictly
        /// ask the user for them.
        /// </summary>
        /// <param name="points">Points in a line - will be treated as a LineStrip</param>
        public static RawDataset PointsToPoints(
            List<Vector3> points,
            Bounds dataBounds,
            Dictionary<string, List<float>> scalarVars,
            Dictionary<string, List<Vector3>> vectorVars
        )
        {
            RawDataset ds = new RawDataset();
            ds.dataTopology = DataTopology.Points;
            ds.bounds = dataBounds;

            int numVectors = vectorVars?.Count ?? 0;
            ds.vectorArrayNames = new string[numVectors];
            ds.vectorArrays = new SerializableVectorArray[numVectors];

            int numScalars = scalarVars?.Count ?? 0;
            ds.scalarArrayNames = new string[numScalars];
            ds.scalarMins = new float[numScalars];
            ds.scalarMaxes = new float[numScalars];
            ds.scalarArrays = new SerializableFloatArray[numScalars];

            // Build the scalar arrays, if present
            if (scalarVars != null)
            {
                int scalarIndex = 0;
                foreach (var kv in scalarVars)
                {
                    ds.scalarArrayNames[scalarIndex] = kv.Key;
                    ds.scalarArrays[scalarIndex] = new SerializableFloatArray() { array = kv.Value.ToArray() };
                    ds.scalarMins[scalarIndex] = kv.Value.Min();
                    ds.scalarMaxes[scalarIndex] = kv.Value.Max();
                    scalarIndex += 1;
                }
            }

            if (vectorVars != null)
            {
                int vectorIndex = 0;
                foreach (var kv in vectorVars)
                {
                    ds.vectorArrayNames[vectorIndex] = kv.Key;
                    ds.vectorArrays[vectorIndex] = new SerializableVectorArray() { array = kv.Value.ToArray() };
                    vectorIndex += 1;
                }
            }

            // Build the points.
            ds.vertexArray = new Vector3[points.Count];
            ds.indexArray = new int[points.Count];
            ds.cellIndexCounts = new int[points.Count];
            ds.cellIndexOffsets = new int[points.Count];
            for (int i = 0; i < ds.vertexArray.Length; i++)
            {
                ds.vertexArray[i] = points[i];
                ds.indexArray[i] = i;
                ds.cellIndexCounts[i] = 1;
                ds.cellIndexOffsets[i] = i;
            }

            return ds;
        }

        /// <summary>
        /// Load a CSV file as a points data object. The first three columns
        /// will be interpreted as "x", "y", and "z" coordinates, respectively.
        /// </summary>
        public static RawDataset CSVToPoints(string csvFilePath, Bounds dataBounds)
        {
            RawDataset ds = new RawDataset();
            ds.dataTopology = DataTopology.Points;
            ds.bounds = dataBounds;

            // int numVectors = vectorVars?.Count ?? 0;
            int numVectors = 0;
            ds.vectorArrayNames = new string[numVectors];
            ds.vectorArrays = new SerializableVectorArray[numVectors];

            // int numScalars = scalarVars?.Count ?? 0;
            int numScalars = 0;
            ds.scalarArrayNames = new string[numScalars];
            ds.scalarMins = new float[numScalars];
            ds.scalarMaxes = new float[numScalars];
            ds.scalarArrays = new SerializableFloatArray[numScalars];

            List<Vector3> points = new List<Vector3>();
            using (StreamReader reader = new StreamReader(csvFilePath))
            {
                string line = reader.ReadLine();
                line = reader.ReadLine();
                while (line != null)
                {
                    string[] contents = line.Trim().Split(',');

                    float x = float.Parse(contents[0]);
                    float y = float.Parse(contents[1]);
                    float z = float.Parse(contents[2]);

                    points.Add(new Vector3(x, y, z));

                    line = reader.ReadLine();
                }
            }



            ds.vertexArray = new Vector3[points.Count];
            ds.indexArray = new int[points.Count];
            ds.cellIndexCounts = new int[points.Count];
            ds.cellIndexOffsets = new int[points.Count];
            for (int i = 0; i < ds.vertexArray.Length; i++)
            {
                ds.vertexArray[i] = points[i];
                ds.indexArray[i] = i;
                ds.cellIndexCounts[i] = 1;
                ds.cellIndexOffsets[i] = i;
            }

            return ds;
        }
    }
}