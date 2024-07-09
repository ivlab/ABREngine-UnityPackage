using UnityEngine;
using System.Collections.Generic;
using IVLab.Utilities;
using System.IO;
using System;

namespace IVLab.ABREngine.Examples
{
    public static class CSVToPoints
    {
        /// <summary>
        /// Import a series of 3D points from a CSV file. This method assumes that
        /// you have an "x", a "y", and a "z" as the header/first row of the CSV.
        /// </summary>
        public static List<Vector3> LoadFromCSV(string csvFilePath, CoordConversion.CoordSystem coordSystem)
        {
            List<Vector3> points = new List<Vector3>();
            using (StreamReader reader = new StreamReader(csvFilePath))
            {
                string line = reader.ReadLine();

                string[] header = line.Trim().Split(',');
                int xIndex = Array.FindIndex(header, h => h.ToLower() == "x");
                int yIndex = Array.FindIndex(header, h => h.ToLower() == "y");
                int zIndex = Array.FindIndex(header, h => h.ToLower() == "z");

                line = reader.ReadLine();
                while (line != null)
                {
                    string[] contents = line.Trim().Split(',');

                    float x = float.Parse(contents[xIndex]);
                    float y = float.Parse(contents[yIndex]);
                    float z = float.Parse(contents[zIndex]);

                    Vector3 rawPoint = new Vector3(x, y, z);
                    Vector3 transformed = CoordConversion.ToUnity(rawPoint, coordSystem);
                    points.Add(transformed);

                    line = reader.ReadLine();
                }
            }

            return points;
        }
    }
}