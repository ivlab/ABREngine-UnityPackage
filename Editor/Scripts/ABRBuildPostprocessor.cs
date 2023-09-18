/* ABRBuildPostprocessor.cs
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
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Defines an ABR-specific post-processing build step that copies some critical files to the build location.
    /// </summary>
    public class ABRBuildPostprocessor : IPostprocessBuildWithReport
    {

        public int callbackOrder { get { return 0; } }

        public void OnPostprocessBuild(BuildReport report)
        {
            try
            {
                string buildFolder = Directory.GetParent(report.summary.outputPath).FullName;

                // source => dest
                Dictionary<string, string> copyPathsOnBuild = new Dictionary<string, string>()
            {
                // ABR Server
                // { ABRServer.ServerFolder, Path.Combine(buildFolder, ABRServer.ServerPath) },

                // ABR Schemas
                { ABREngine.SchemasPath, Path.Combine(buildFolder, ABREngine.SchemasPath) },
            };

                foreach (var srcDest in copyPathsOnBuild)
                {
                    var attrs = File.GetAttributes(srcDest.Key);
                    if ((attrs & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        // Copy folder to build location
                        CopyDirectory(srcDest.Key, srcDest.Value, true);
                        Debug.Log($"ABRBuildPostprocessor: Copied folder from {srcDest.Key} to {srcDest.Value}");
                    }
                    else
                    {
                        // Copy file to build location
                        File.Copy(srcDest.Key, srcDest.Value);
                        Debug.Log($"ABRBuildPostprocessor: Copied file from {srcDest.Key} to {srcDest.Value}");
                    }
                }
            }
            catch (System.Exception e)
            {
                // don't build if there's a problem for any reason
                throw new BuildFailedException("ABRBuildPostprocessor: " + e.Message);
            }
        }

        // https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
        private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }
}
#endif