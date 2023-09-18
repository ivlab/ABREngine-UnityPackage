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
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace IVLab.ABREngine
{
    public class ABRBuildPostprocessor : IPostprocessBuildWithReport
    {
        private const string PackagePath = "Packages/edu.umn.cs.ivlab.abrengine/";
        private const string ABRServerFolder = "ABRServer~";

        public int callbackOrder { get { return 0; } }

        public void OnPostprocessBuild(BuildReport report)
        {
            string buildFolder = Directory.GetParent(report.summary.outputPath).FullName;
            string packageFolder = Path.GetFullPath(PackagePath);

            string buildPath = Path.Combine(buildFolder, ABRServerFolder);
            string packagePath = Path.Combine(packageFolder, ABRServerFolder);

            // Copy folder to build location
            CopyDirectory(packagePath, buildPath, true);
            Debug.Log($"ABRBuildPostprocessor: Copied folder from {packagePath} to {buildPath}");
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