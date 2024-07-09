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
using System;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Adapter for converting other formats to ABRDataFormat. For example,
    /// lists of points => ribbons, or lists of points => glyphs. See the examples below for usage of each of these methods.
    /// </summary>
    /// <remarks>
    /// Note: None of these methods will actually import your data into ABR!
    /// These are simply a convenience for converting data into ABR format.
    /// After you call one of the RawDatasetAdapter methods, you MUST import it
    /// using `ABREngine.Instance.Data.ImportRawDataset(...)` to be able to use
    /// it in ABR!
    /// </remarks>
    public static class RawDatasetAdapter
    {
        /// <summary>
        /// Load data from the data source.
        /// </summary>
        /// <param name="filePath">Data source file</param>
        /// <example>
        /// In this example, we load in a 3D model in OBJ format and convert it into ABR format.
        /// <code>
        /// public class RawDatasetAdapterExample : MonoBehaviour
        /// {
        ///     void Start()
        ///     {
        ///         RawDataset objSurface = RawDatasetAdapter.ObjToSurface("C:/Users/me/Desktop/cube.obj");
        ///     }
        /// }
        /// </code>
        /// </example>
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
        /// <example>
        /// In this example, we load a triangle GameObject from our existing Unity scene and associate some data with it.
        /// <code>
        /// public class RawDatasetAdapterExample : MonoBehaviour
        /// {
        ///     void Start()
        ///     {
        ///         Mesh m = GameObject.Find("SomeTriangle").GetComponent&lt;MeshFilter&gt;().mesh;
        /// 
        ///         // 3 vertices with scalar data values (assumed to have same number of vertices as the mesh, and the same order too)
        ///         List&lt;float&gt; someVariable = new List&lt;float&gt; { 0.0f, 1.0f, 0.5f };
        ///         Dictionary&lt;string, List&lt;float&gt;&gt; scalarVars = new Dictionary&lt;string, List&lt;float&gt;&gt; {{ "someVariable", someVariable }};
        /// 
        ///         RawDataset meshSurface = RawDatasetAdapter.MeshToSurface(m, scalarVars);
        ///     }
        /// }
        /// </code>
        /// </example>
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
        /// <example>
        /// In this example, we create a surface from a Cube GameObject primitive.
        /// <code>
        /// public class RawDatasetAdapterExample : MonoBehaviour
        /// {
        ///     void Start()
        ///     {
        ///         RawDataset cubeSurface = RawDatasetAdapter.UnityPrimitiveToSurface(PrimitiveType.Cube);
        ///     }
        /// }
        /// </code>
        /// </example>
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
        /// ask the user for them. This method is a shortcut for a single connected line.
        /// </summary>
        /// <param name="lines">A line. Each line consists of a series of points.</param>
        /// <param name="dataBounds">The center and extents of the data in the original coordinate space</param>
        /// <param name="scalarVars">Mapping of <em>variable name</em> =>
        /// <em>array of floating point numbers</em> for each scalar variable
        /// attached to the lines. Values will be applied at each point along
        /// each segment of each line.</param>
        /// <example>
        /// In this example, we create a single line from a series of vertices that have data values associated with them.
        /// <code>
        /// public class RawDatasetAdapterExample : MonoBehaviour
        /// {
        ///     void Start()
        ///     {
        ///         List&lt;Vector3&gt; points = new List&lt;Vector3&gt;
        ///         {
        ///             new Vector3(0.0f, 0.0f, 0.0f),
        ///             new Vector3(0.1f, 0.1f, 0.0f),
        ///             new Vector3(0.2f, 0.2f, 0.0f),
        ///             new Vector3(0.3f, 0.3f, 0.0f),
        ///             new Vector3(0.4f, 0.4f, 0.0f),
        ///             new Vector3(0.5f, 0.5f, 0.0f)
        ///         };
        ///
        ///         // Each data point corresponds with a vertex above
        ///         List&lt;float&gt; data = new List&lt;float&gt;
        ///         {
        ///             0.0f,
        ///             1.0f,
        ///             2.0f,
        ///             3.0f,
        ///             4.0f,
        ///             5.0f
        ///         };
        ///
        ///         // Save the scalar var so we can use it
        ///         Dictionary&lt;string, List&lt;float&gt;&gt; scalarVars = new Dictionary&lt;string, List&lt;float&gt;&gt; {{ "someData", data }};
        ///
        ///         // Provide a generous bounding box
        ///         Bounds b = new Bounds(Vector3.zero, Vector3.one);
        ///
        ///         RawDataset abrLine = RawDatasetAdapter.PointsToLine(points, b, scalarVars);
        ///
        ///         // Or, if you don't have any variables:
        ///         RawDataset abrLine2 = RawDatasetAdapter.PointsToLine(points, b, null);
        ///     }
        /// }
        /// </code>
        /// </example>
        public static RawDataset PointsToLine(List<Vector3> line, Bounds dataBounds, Dictionary<string, List<float>> scalarVars)
        {
            return PointsToLine(new List<List<Vector3>> { line }, dataBounds, scalarVars);
        }

        /// <summary>
        /// Define a Line dataset from a bunch of points. Don't try to assume or
        /// calculate the full bounds for the imported data objects - explictly
        /// ask the user for them.
        /// </summary>
        /// <param name="lines">One, or several, lines. Each line consists of a series of points.</param>
        /// <param name="dataBounds">The center and extents of the data in the original coordinate space</param>
        /// <param name="scalarVars">Mapping of <em>variable name</em> =>
        /// <em>array of floating point numbers</em> for each scalar variable
        /// attached to the lines. Values will be applied at each point along
        /// each segment of each line.</param>
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
        /// <param name="points">Source points in the original coordinate space</param>
        /// <param name="dataBounds">Center and extent of the data, in the original coordinate space</param>
        /// <param name="scalarVars">Mapping of <em>variable name</em> =>
        /// <em>array of floating point numbers</em> for each scalar variable
        /// attached to these points. Values will be applied at each point of
        /// the dataset.</param>
        /// <param name="vectorVars">Mapping of <em>variable name</em> =>
        /// <em>array of Vector3</em> for each vector variable
        /// attached to these points. Values will be applied at each point of
        /// the dataset.</param>
        /// <example>
        /// In this example, we create a points data object from a series of vertices.
        /// <code>
        /// public class RawDatasetAdapterExample : MonoBehaviour
        /// {
        ///     void Start()
        ///     {
        ///         List&lt;Vector3&gt; points = new List&lt;Vector3&gt;
        ///         {
        ///             new Vector3(0.0f, 0.0f, 0.0f),
        ///             new Vector3(0.1f, 0.1f, 0.0f),
        ///             new Vector3(0.2f, 0.2f, 0.0f),
        ///             new Vector3(0.3f, 0.3f, 0.0f),
        ///             new Vector3(0.4f, 0.4f, 0.0f),
        ///             new Vector3(0.5f, 0.5f, 0.0f)
        ///         };
        ///
        ///         // Each data point corresponds with a vertex above
        ///         List&lt;float&gt; data = new List&lt;float&gt;
        ///         {
        ///             0.0f,
        ///             1.0f,
        ///             2.0f,
        ///             3.0f,
        ///             4.0f,
        ///             5.0f
        ///         };
        ///
        ///         // Some vector data corresponding with each vertex
        ///         List&lt;Vector3&gt; vectorData = new List&lt;Vector3&gt;
        ///         {
        ///             Vector3.up,
        ///             Vector3.up,
        ///             Vector3.up,
        ///             Vector3.down,
        ///             Vector3.down,
        ///             Vector3.down,
        ///         };
        ///
        ///         // Save the vars so we can use them
        ///         Dictionary&lt;string, List&lt;float&gt;&gt; scalarVars = new Dictionary&lt;string, List&lt;float&gt;&gt; {{ "someData", data }};
        ///         Dictionary&lt;string, List&lt;Vector3&gt;&gt; vectorVars = new Dictionary&lt;string, List&lt;Vector3&gt;&gt; {{ "someVectorData", vectorData }};
        ///
        ///         // Provide a generous bounding box
        ///         Bounds b = new Bounds(Vector3.zero, Vector3.one);
        ///
        ///         RawDataset abrPoints = RawDatasetAdapter.PointsToPoints(points, b, scalarVars, vectorVars);
        ///
        ///         // Or, if you don't have any variables:
        ///         RawDataset abrPoints2 = RawDatasetAdapter.PointsToPoints(points, b, null, null);
        ///     }
        /// }
        /// </code>
        /// </example>
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
        /// Convert a grid (2.5D) of points into an ABR surface data object.
        /// </summary>
        /// <param name="points">Vertices of the desired mesh. Points are assumed to be in reverse column-major order, i.e. starting from -x, -z and ending at +x +z.</param>
        /// <param name="gridDimension">Dimensions of the mesh grid that the points make up (x vertex count and z vertex count).</param>
        /// <param name="dataBounds">The bounds of the actual vertices of the data.</param>
        /// <param name="scalarVars">Mapping from name => float array for every scalar variable attached to the data. Float arrays are assumed to have the same ordering as `points`.</param>
        /// <example>
        /// In this example, we create a surface from a Cube GameObject primitive.
        /// <code>
        /// public class RawDatasetAdapterExample : MonoBehaviour
        /// {
        ///      void Start()
        ///      {
        ///          // 3x3 2.5D grid of points. Note their arrangement in x-based "columns"
        ///          // -- this is a grid in the X-Z plane where only the y-coordinate is
        ///          // varying.
        ///          List&lt;Vector3&gt; gridVertices = new List&lt;Vector3&gt;
        ///          {
        ///              // column 1
        ///              new Vector3(0.0f, 0.5f, 0.0f),
        ///              new Vector3(0.0f, 0.6f, 0.1f),
        ///              new Vector3(0.0f, 0.4f, 0.2f),
        ///
        ///              // column 2
        ///              new Vector3(0.1f, 0.3f, 0.0f),
        ///              new Vector3(0.1f, 0.2f, 0.1f),
        ///              new Vector3(0.1f, 0.3f, 0.2f),
        ///
        ///              // column 3
        ///              new Vector3(0.2f, 0.0f, 0.0f),
        ///              new Vector3(0.2f, 0.3f, 0.1f),
        ///              new Vector3(0.2f, 0.1f, 0.2f),
        ///          };
        ///
        ///          // Dimenisions of the grid vertices (3x3)
        ///          Vector2Int dimensions = new Vector2Int(3, 3);
        ///
        ///          // Each data point corresponds with a vertex above
        ///          List&lt;float&gt; data = new List&lt;float&gt;
        ///          {
        ///              0.0f,
        ///              0.0f,
        ///              0.0f,
        ///
        ///              1.0f,
        ///              1.0f,
        ///              1.0f,
        ///
        ///              2.0f,
        ///              2.0f,
        ///              2.0f,
        ///          };
        ///
        ///          // Save the var so we can use it
        ///          Dictionary&lt;string, List&lt;float&gt;&gt; scalarVars = new Dictionary&lt;string, List&lt;float&gt;&gt; {{ "someData", data }};
        ///
        ///          // Provide a generous bounding box
        ///          Bounds b = new Bounds(Vector3.zero, Vector3.one);
        ///
        ///          RawDataset abrSurface = RawDatasetAdapter.GridPointsToSurface(gridVertices, dimensions, b, scalarVars);
        ///
        ///          // Or, if you don't have any variables:
        ///          RawDataset abrSurface2 = RawDatasetAdapter.GridPointsToSurface(gridVertices, dimensions, b, null);
        ///      }
        /// }
        /// </code>
        /// </example>
        public static RawDataset GridPointsToSurface(List<Vector3> points, Vector2Int gridDimension, Bounds dataBounds, Dictionary<string, List<float>> scalarVars)
        {
            Mesh m = new Mesh();
            m.vertices = points.ToArray();
            m.bounds = dataBounds;

            // Handle meshes with big indices / many vertices
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            int[] indices = new int[m.vertices.Length * 6];

            int i = 0;
            for (int gridX = 0; gridX < gridDimension.x - 1; gridX++)
            {
                for (int gridY = 0; gridY < gridDimension.y - 1; gridY++)
                {
                    // Construct first triangle ^
                    indices[i + 0] = gridX * gridDimension.y + gridY;
                    indices[i + 1] = gridX * gridDimension.y + (gridY + 1);
                    indices[i + 2] = (gridX + 1) * gridDimension.y + (gridY + 1);
                    i += 3;

                    // Construct second triangle v
                    indices[i + 0] = gridX * gridDimension.y + gridY;
                    indices[i + 1] = (gridX + 1) * gridDimension.y + (gridY + 1);
                    indices[i + 2] = (gridX + 1) * gridDimension.y + gridY;
                    i += 3;
                }
            }

            m.triangles = indices;
            return MeshToSurface(m, scalarVars);
        }

        /// <summary>
        /// Convert a 3D grid into an ABR volume data object. There is assumed to be a single scalar variable described by the array `voxels`.
        /// </summary>
        /// <param name="voxels">3D voxels that make up the volume. All voxels are assumed to be the same size.</param>
        /// <param name="voxelsName">Name of the variable the `voxels` are storing</param>
        /// <param name="volumeDimensions">Dimensions of the volume (number of steps in x, y, and z).</param>
        /// <param name="dataBounds">The bounds of volume in actual space.</param>
        /// <example>
        /// In this example, we create a volume from a series of voxels
        /// <code>
        /// public class RawDatasetAdapterExample : MonoBehaviour
        /// {
        ///     void Start()
        ///     {
        ///         // Define a 100x100x100 volume
        ///         int volX = 100;
        ///         int volY = 100;
        ///         int volZ = 100;
        ///         float[][][] voxels = new float[volZ][][];
        ///
        ///         // Populate voxels with "data" (x * y * z)
        ///         for (int z = 0; z &lt; volZ; z++)
        ///         {
        ///             float[][] stack = new float[volY][];
        ///             for (int y = 0; y &lt; volY; y++)
        ///             {
        ///                 float[] col = new float[volX];
        ///                 for (int x = 0; x &lt; volX; x++)
        ///                 {
        ///                     col[x] = x * y * z;
        ///                 }
        ///                 stack[y] = col;
        ///             }
        ///             voxels[z] = stack;
        ///         }
        ///
        ///         Bounds b = new Bounds(Vector3.zero, Vector3.one);
        ///
        ///         RawDataset abrVolume = RawDatasetAdapter.VoxelsToVolume(voxels, "someData", new Vector3Int(volX, volY, volZ), b);
        ///     }
        /// }
        /// </code>
        /// </example>
        public static RawDataset VoxelsToVolume(float[][][] voxels, string voxelsName, Vector3Int volumeDimensions, Bounds dataBounds)
        {
            // Copy all the voxel values into 
            float[] scalars = new float[volumeDimensions.x * volumeDimensions.y * volumeDimensions.z];
            int v = 0;
            for (int z = 0; z < volumeDimensions.z; z++)
            {
                for (int y = 0; y < volumeDimensions.y; y++)
                {
                    Array.Copy(voxels[z][y], 0, scalars, v, volumeDimensions.x);
                    v += volumeDimensions.x;
                }
            }

            return VoxelsToVolume(scalars, voxelsName, volumeDimensions, dataBounds);
        }

        /// <summary>
        /// Convert a 1D array into an ABR volume data object. There is assumed to be a single scalar variable described by the array `voxels`.
        /// </summary>
        /// <param name="voxels">Individual scalar values that make up the volume. All voxels are assumed to be the same size.</param>
        /// <param name="voxelsName">Name of the variable the `voxels` are storing</param>
        /// <param name="volumeDimensions">Dimensions of the volume (number of steps in x, y, and z).</param>
        /// <param name="dataBounds">The bounds of volume in actual space.</param>
        /// <example>
        /// In this example, we create a volume from a series of voxels
        /// <code>
        /// public class RawDatasetAdapterExample : MonoBehaviour
        /// {
        ///     void Start()
        ///     {
        ///         // Define a 100x100x100 volume
        ///         int volX = 100;
        ///         int volY = 100;
        ///         int volZ = 100;
        ///         float[] voxels = new float[volX * volY * volZ];
        ///
        ///         // Populate voxels with "data" (x * y * z)
        ///         int v = 0;
        ///         for (int v = 0; v &lt; voxels.Length; v++)
        ///         {
        ///             int z = v / (volX * volY);
        ///             int vAdjusted = v - (z * volX * volY);
        ///             int x = vAdjusted % volX;
        ///             int y = vAdjusted / volX;
        ///             voxels[v] = x * y * z;
        ///         }
        ///
        ///         Bounds b = new Bounds(Vector3.zero, Vector3.one);
        ///
        ///         RawDataset abrVolume = RawDatasetAdapter.VoxelsToVolume(voxels, "someData", new Vector3Int(volX, volY, volZ), b);
        ///     }
        /// }
        /// </code>
        /// </example>
        public static RawDataset VoxelsToVolume(float[] voxels, string voxelsName, Vector3Int volumeDimensions, Bounds dataBounds)
        {
            RawDataset ds = new RawDataset();
            ds.bounds = dataBounds;
            ds.dimensions = volumeDimensions;
            ds.scalarArrayNames = new string[] { voxelsName };

            float minm = float.MaxValue;
            float maxm = float.MinValue;
            for (int v = 0; v < voxels.Length; v++)
            {
                if (voxels[v] > maxm)
                    maxm = voxels[v];
                if (voxels[v] < minm)
                    minm = voxels[v];
            }

            ds.scalarMins = new float[] { minm };
            ds.scalarMaxes = new float[] { maxm };
            ds.scalarArrays = new SerializableFloatArray[] { new SerializableFloatArray() { array = voxels }};

            int numVectors = 0;
            ds.vectorArrayNames = new string[numVectors];
            ds.vectorArrays = new SerializableVectorArray[numVectors];
            ds.dataTopology = DataTopology.Voxels;

            return ds;
        }
    }
}