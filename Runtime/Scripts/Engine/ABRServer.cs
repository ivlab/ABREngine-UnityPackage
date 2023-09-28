/* ABRServer.cs
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

using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;

namespace IVLab.ABREngine
{
    /// <summary>
    /// The ABRServer class functions as a passthrough to the Python server. It
    /// defines several helpful constants and functions for working with the
    /// server (e.g., starting, stopping, etc.)
    /// </summary>
    public class ABRServer
    {
        /// <summary>
        /// Folder, relative to this package, where the ABR server is located.
        /// </summary>
        public const string ServerFolder = "ABRServer~";

        /// <summary>
        /// Relative path to where the ABR server is located.
        /// </summary>
        public static string ServerRootPath { get => Path.Combine(ABREngine.PackagePath, ServerFolder); }

        /// <summary>
        /// Full path to where the ABR server is located.
        /// </summary>
        public static string ServerRootFullPath { get => Path.GetFullPath(ServerRootPath); }

        /// <summary>
        /// Full path where ABR Server executables are located (created by pyinstaller)
        /// </summary>
        private static string ServerDistPath { get => Path.Combine(ServerRootFullPath, "dist"); }

        /// <summary>
        /// Working directory that pyinstaller expects everything to be in
        /// </summary>
        private static string ServerInternalPath { get => Path.Combine(ServerDistPath, ServerExeName(), "_internal"); }

        /// <summary>
        /// Path to the server executable on this platform
        /// </summary>
        public static string ServerPath { get => Path.Combine(ServerDistPath, ServerExeName(), ServerExeName() + ServerExeExtension()); }

        /// <summary>
        /// Django command to run the server
        /// </summary>
        private const string RunserverArg = "runserver";

        /// <summary>
        /// Django command to broadcast the server
        /// </summary>
        private const string BroadcastArg = "0.0.0.0:8000";

        /// <summary>
        /// Per-platform paths for server pyinstaller executable files.
        /// </summary>
        private static string ServerExeName()
        {
            string outString = "ABRServer-";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    outString += "Windows";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    outString += "OSX";

            outString += "-" + RuntimeInformation.ProcessArchitecture.ToString("G");

            return outString;
        }

        /// <summary>
        /// Any extension that is applied to the <see cref="ServerDistPath"/>.
        /// </summary>
        /// <returns></returns>
        private static string ServerExeExtension()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return ".exe";
            else
                return "";
        }

// Editor convenience functions
#if UNITY_EDITOR
        [MenuItem("ABR/Server/Start Server")]
        private static void StartServerLocal() => StartServer(false);

        [MenuItem("ABR/Server/Start Server - Broadcast (use this if you need to connect other devices over the network)")]
        private static void StartServerBroadcast() => StartServer(true);
#endif

        /// <summary>
        /// Convenience function to start the Python ABR server. Uses the
        /// pyinstaller executables.
        /// </summary>
        public static void StartServer(bool broadcast)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.FileName = ServerPath;
                startInfo.Arguments = RunserverArg;
                if (broadcast)
                    startInfo.Arguments += " " + BroadcastArg;
                startInfo.WorkingDirectory = ServerInternalPath;

                var serverProcess = new System.Diagnostics.Process();
                serverProcess.StartInfo = startInfo;
                bool started = serverProcess.Start();
                if (started)
                    Debug.Log("Started ABR Server " + ServerPath);
                else
                    Debug.Log("ABR Server already running");
            }
            catch (System.Exception e)
            {
                Debug.LogError("Unable to start ABR Server. Is the ABRServer executable build for this platform? Details:\n" + e);
            }
        }
    }
}