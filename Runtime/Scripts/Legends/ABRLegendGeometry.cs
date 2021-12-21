/* ABRLegend.cs
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
using System.Linq;
using UnityEngine;

namespace IVLab.ABREngine.Legends
{
    /// <summary>
    /// Generate legend geometry for each data impression type defined in ABR.
    /// Methods in this class can generate the following variables and ranges:
    /// <ol>
    ///     <li>XAxis [0, 1]</li>
    ///     <li>YAxis [0, 1]</li>
    ///     <li>ZAxis [0, 1]</li>
    ///     <li>Forward</li>
    ///     <li>Up</li>
    /// </ol>
    /// </summary>
    public static class ABRLegendGeometry
    {
        private static Bounds LegendBounds = new Bounds(Vector3.zero, new Vector3(1.0f, 0.2f, 1.0f));

        /// <summary>
        /// Generate a set of n glyphs to show a legend.
        /// </summary>
        /// <param name="variables">Number of variables to provide (1 var, 2 var)</param>
        public static RawDataset Glyphs(int variables)
        {
            if (variables < 0 && variables > 2)
            {
                throw new System.ArgumentException("Must provide 2 or fewer variables to Glyphs legend generator");
            }
            int numGlyphs = 5;
            Vector3 forwardTarget = LegendBounds.center - new Vector3(0, LegendBounds.extents.y / 2.0f, 0);
            List<Vector3> points = new List<Vector3>();
            List<float> xVar = new List<float>();
            List<float> zVar = new List<float>();
            List<Vector3> forward = new List<Vector3>();
            List<Vector3> up = new List<Vector3>();
            for (int g = 0; g < numGlyphs; g++)
            {
                float xt = ((g + 0.5f) / (float) numGlyphs);
                float x = Mathf.Lerp(LegendBounds.min.x, LegendBounds.max.x, xt);
                if (variables == 2)
                {
                    for (int gg = 0; gg < numGlyphs; gg++)
                    {
                        float zt = ((gg + 0.5f) / (float) numGlyphs);
                        float z = Mathf.Lerp(LegendBounds.min.z, LegendBounds.max.z, zt);
                        Vector3 point = new Vector3(x, 0, z);
                        points.Add(point);
                        xVar.Add(xt);
                        zVar.Add(zt);
                        forward.Add((forwardTarget - point).normalized);
                        up.Add(Vector3.forward);
                    }
                }
                else
                {
                    Vector3 point = new Vector3(x, 0, 0);
                    points.Add(point);
                    if (variables == 1)
                    {
                        xVar.Add(xt);
                    }
                    forward.Add((forwardTarget - point).normalized);
                    up.Add(Vector3.forward);
                }
            }
            Dictionary<string, List<float>> scalars = new Dictionary<string, List<float>>();
            if (variables >= 1)
            {
                scalars.Add("XAxis", xVar);
            }
            if (variables >= 2)
            {
                scalars.Add("ZAxis", zVar);
            }
            Dictionary<string, List<Vector3>> vectors = new Dictionary<string, List<Vector3>>()
            {
                { "Forward", forward },
                { "Up", up }
            };
            RawDataset ds = RawDatasetAdapter.PointsToPoints(points, LegendBounds, variables > 0 ? scalars : null, vectors);
            return ds;
        }

        /// <summary>
        /// Generate a set of ribbons to show in a legend.
        /// </summary>
        /// <param name="variables">Number of variables to provide (1 var, 2 var)</param>
        public static RawDataset Ribbons(int variables)
        {
            if (variables < 0 && variables > 2)
            {
                throw new System.ArgumentException("Must provide 2 or fewer variables to Glyphs legend generator");
            }
            int numRibbonPoints = 50;
            int numRibbons = variables == 2 ? 5 : 1;
            List<List<Vector3>> lines = new List<List<Vector3>>();
            List<float> xVar = new List<float>();
            List<float> zVar = new List<float>();
            for (int ribbonIndex = 0; ribbonIndex < numRibbons; ribbonIndex++)
            {
                List<Vector3> points = new List<Vector3>();
                float zt = ((ribbonIndex + 0.5f) / (float) numRibbons);
                float z = Mathf.Lerp(LegendBounds.min.z, LegendBounds.max.z, zt);
                for (int g = 0; g < numRibbonPoints; g++)
                {
                    float xt = ((g + 0.5f) / (float) numRibbonPoints);
                    float x = Mathf.Lerp(LegendBounds.min.x, LegendBounds.max.x, xt);
                    Vector3 point = new Vector3(x, Mathf.Sin(x * 15.0f) * 0.05f, z + Mathf.Sin(x * 7.0f) * 0.05f);
                    points.Add(point);
                    if (variables >= 1)
                    {
                        xVar.Add(xt);
                    }
                    if (variables >= 2)
                    {
                        zVar.Add(zt);
                    }
                }
                lines.Add(points);
            }
            Dictionary<string, List<float>> scalars = new Dictionary<string, List<float>>();
            if (variables >= 1)
            {
                scalars.Add("XAxis", xVar);
            }
            if (variables >= 2)
            {
                scalars.Add("ZAxis", zVar);
            }
            RawDataset ds = RawDatasetAdapter.PointsToLine(lines, LegendBounds, variables > 0 ? scalars : null);
            return ds;
        }

        /// <summary>
        /// Generate a surface to show a legend
        /// </summary>
        public static RawDataset Surface()
        {
            RawDataset surf = RawDatasetAdapter.UnityPrimitiveToSurface(PrimitiveType.Sphere);
            Bounds origBounds = surf.bounds;
            Vector3 scale = new Vector3(
                LegendBounds.size.x / origBounds.size.x,
                LegendBounds.size.y / origBounds.size.y,
                LegendBounds.size.z / origBounds.size.z
            );
            for (int xyz = 0; xyz < surf.scalarArrays.Length; xyz++)
            {
                for (int v = 0; v < surf.vertexArray.Length; v++)
                {
                    surf.vertexArray[v] = Matrix4x4.Scale(scale) * surf.vertexArray[v];
                    surf.scalarArrays[xyz].array[v] -= origBounds.min[xyz];
                    surf.scalarArrays[xyz].array[v] /= origBounds.size[xyz];
                }
                surf.scalarMins[xyz] = 0.0f;
                surf.scalarMaxes[xyz] = 1.0f;
            }
            return surf;
        }

        /// <summary>
        /// Generate a "spherical" volume for legends
        /// </summary>
        public static RawDataset Volume()
        {
            RawDataset vol = new RawDataset();
            vol.dimensions = new Vector3Int(20, 20, 20);
            vol.bounds = LegendBounds;
            vol.dataTopology = DataTopology.Voxels;

            int numScalars = 1;
            vol.scalarArrayNames = new string[numScalars];
            vol.scalarMins = new float[numScalars];
            vol.scalarMaxes = new float[numScalars];
            vol.scalarArrays = new SerializableFloatArray[numScalars];

            int numVectors = 0;
            vol.vectorArrayNames = new string[numVectors];
            vol.vectorArrays = new SerializableVectorArray[numVectors];

            List<float> values = new List<float>();
            for (int y = 0; y < vol.dimensions.y; y++)
            {
                for (int z = 0; z < vol.dimensions.z; z++)
                {
                    for (int x = 0; x < vol.dimensions.x; x++)
                    {
                        values.Add(x / (float)vol.dimensions.x);
                    }
                }
            }

            vol.scalarArrayNames[0] = "XAxis";
            vol.scalarArrays[0] = new SerializableFloatArray { array = values.ToArray() };
            vol.scalarMins[0] = values.Min();
            vol.scalarMaxes[0] = values.Max();

            vol.vertexArray = new Vector3[0];
            vol.indexArray = new int[0];
            vol.cellIndexCounts = new int[0];
            vol.cellIndexOffsets = new int[0];

            return vol;
        }
    }
}