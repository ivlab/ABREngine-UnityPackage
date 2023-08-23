/* DataPath.cs
 *
 * Should be kept in sync with the DataPath.py found in ABRUtilities repo
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

using UnityEngine;

namespace IVLab.ABREngine
{
    /// <summary>
    ///     A path to a data source, be it a KeyData object, a Scalar Variable,
    ///     a Vector Variable, or something else.
    /// 
    ///     Should take the form of Organization/DatasetName/*
    ///
    ///     Example: TACC/GulfOfMexico/KeyData/bathymetry
    ///
    ///     Example: TACC/GulfOfMexico/ScalarVar/temperature
    /// </summary>
    public static class DataPath
    {
        public enum DataPathType
        {
            ScalarVar = 0,
            VectorVar = 1,
            KeyData = 2,
            Dataset = 3,
        }

        private static char separator = '/';
        private static string[] GetPathParts(string dataPath)
        {
            return dataPath?.Split(separator);
        }

        public static string GetOrganization(string dataPath)
        {
            return GetPathParts(dataPath)?[0];
        }

        public static string GetDataset(string dataPath)
        {
            return GetPathParts(dataPath)?[1];
        }

        public static string GetPathType(string dataPath)
        {
            return GetPathParts(dataPath)?[2];
        }

        public static string GetName(string dataPath)
        {
            return GetPathParts(dataPath)?[3];
        }

        public static string GetOrganizationPath(string dataPath)
        {
            return GetPathParts(dataPath)?[0];
        }

        public static string GetDatasetPath(string dataPath)
        {
            return Join(GetOrganizationPath(dataPath), GetPathParts(dataPath)?[1]);
        }

        public static string GetPathTypePath(string dataPath)
        {
            return Join(GetDatasetPath(dataPath), GetPathParts(dataPath)?[2]);
        }

        public static string GetNamePath(string dataPath)
        {
            return Join(GetPathTypePath(dataPath), GetPathParts(dataPath)?[3]);
        }

        public static string Join(string path1, string path2)
        {
            if (path1 == null || path2 == null)
            {
                throw new System.ArgumentException("DataPath: path1 and path2 must not be null");
            }
            if (path1.EndsWith(separator.ToString()))
            {
                return path1 + path2;
            }
            else
            {
                return path1 + separator + path2;
            }
        }

        public static string Join(string datasetPath, DataPathType pathType3, string path4)
        {
            if (FollowsConvention(datasetPath, DataPathType.Dataset))
            {
                string path = datasetPath;
                path = Join(path, pathType3);
                path = Join(path, path4);
                return path;
            }
            else
            {
                Debug.LogWarning("Refusing to join non-dataset path with DataPathType");
                return datasetPath;
            }
        }

        public static string Join(string path1, string path2, DataPathType pathType3, string path4)
        {
            string path = Join(path1, path2);
            path = Join(path, pathType3);
            path = Join(path, path4);
            return path;
        }

        public static string Join(string path1, DataPathType pathType)
        {
            if (FollowsConvention(path1, DataPathType.Dataset))
            {
                return Join(path1, pathType.ToString());
            }
            else
            {
                Debug.LogWarning("Refusing to join non-dataset path with DataPathType");
                return path1;
            }
        }

        public static bool FollowsConvention(string label, DataPathType pathType = DataPathType.KeyData)
        {
            if (label == null)
            {
                return false;
            }
            var parts = GetPathParts(label);
            if (pathType != DataPathType.Dataset)
            {
                return parts.Length == 4 && parts[2] == pathType.ToString();
            }
            else
            {
                return parts.Length == 2;
            }
        }

        public static string GetConvention(DataPathType pathType)
        {
            if (pathType != DataPathType.Dataset)
            {
                return string.Format("Organization/Dataset/{0}/Name", pathType);
            }
            else
            {
                return "Organization/Dataset";
            }
        }

        // Log a message if the data path doesn't follow convention
        public static void WarnOnDataPathFormat(string dataPath, DataPath.DataPathType dataPathType)
        {
            if (!DataPath.FollowsConvention(dataPath, dataPathType))
            {
                Debug.LogWarningFormat(
                    "Label `{0}` does not follow data path convention and " +
                    "may not be imported correctly.\nUse {1} convention {2}",
                    dataPath,
                    dataPathType.ToString(),
                    DataPath.GetConvention(dataPathType));
            }
        }
    }
}