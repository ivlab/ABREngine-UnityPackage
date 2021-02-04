/* DataPath.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
 *
 */

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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
            ScalarVar,
            VectorVar,
            KeyData,
            Dataset,
        }

        private static char separator = '/';
        private static string[] GetPathParts(string dataPath)
        {
            return dataPath.Split(separator);
        }

        public static string GetOrganization(string dataPath)
        {
            return GetPathParts(dataPath)[0];
        }

        public static string GetDataset(string dataPath)
        {
            return GetPathParts(dataPath)[1];
        }

        public static string GetPathType(string dataPath)
        {
            return GetPathParts(dataPath)[2];
        }

        public static string GetName(string dataPath)
        {
            return GetPathParts(dataPath)[3];
        }

        public static string GetOrganizationPath(string dataPath)
        {
            return GetPathParts(dataPath)[0];
        }

        public static string GetDatasetPath(string dataPath)
        {
            return Join(GetOrganizationPath(dataPath), GetPathParts(dataPath)[1]);
        }

        public static string GetPathTypePath(string dataPath)
        {
            return Join(GetDatasetPath(dataPath), GetPathParts(dataPath)[2]);
        }

        public static string GetNamePath(string dataPath)
        {
            return Join(GetPathTypePath(dataPath), GetPathParts(dataPath)[3]);
        }

        public static string Join(string path1, string path2)
        {
            if (path1.EndsWith(separator.ToString()))
            {
                return path1 + path2;
            }
            else
            {
                return path1 + separator + path2;
            }
        }

        public static string Join(string path1, DataPathType pathType)
        {
            return Join(path1, pathType.ToString());
        }

        public static bool FollowsConvention(string label, DataPathType pathType = DataPathType.KeyData)
        {
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