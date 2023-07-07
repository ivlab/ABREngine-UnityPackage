/* ABRImportUtilities.cs
 *
 * Copyright (c) 2023 University of Minnesota
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


#if UNITY_EDITOR

using UnityEngine;
using System;
using UnityEditor;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager;

namespace IVLab.ABREngine.Importer
{
    /// <summary>
    /// Editor utilities to assist developers when first importing ABR
    /// </summary>
    public class ABRImportUtilities : MonoBehaviour
    {
        private static string[] Dependencies = new string[]
        {
            "git+https://github.com/ivlab/OBJImport-UnityPackage.git",
            "git+https://github.com/ivlab/IVLab-Utilities-UnityPackage.git",
            "git+https://github.com/ivlab/JsonSchema-UnityPackage.git",
            "git+https://github.com/ivlab/JsonDiffPatch-UnityPackage.git",
        };

        private static AddRequest request;
        private static int dependencyIndex = 0;
        private static bool readyToAddNext = false;

        /// <summary>
        /// Import ABR dependencies from GitHub.
        /// </summary>
        [MenuItem("ABR/Import ABR Dependencies")]
        static void ImportABRDependencies()
        {
            Debug.Log("Importing ABR dependencies... this will take some time");

            request = Client.Add(Dependencies[dependencyIndex]);
            EditorApplication.update += Progress;
        }

        static void Progress()
        {
            if (readyToAddNext)
            {
                request = Client.Add(Dependencies[dependencyIndex]);
                readyToAddNext = false;
            }

            if (request.IsCompleted)
            {
                if (request.Status == StatusCode.Success)
                {
                    Debug.Log("Installed: " + request.Result.packageId);
                }
                else if (request.Status >= StatusCode.Failure)
                {
                    Debug.LogError(request.Error.message);
                }

                dependencyIndex += 1;
                readyToAddNext = true;
            }

            if (dependencyIndex >= Dependencies.Length)
            {
                EditorApplication.update -= Progress;
                Debug.Log("Finished importing ABR dependencies");
            }
        }
    }
}
#endif