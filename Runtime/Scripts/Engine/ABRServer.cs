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
        /// Full path to where the ABR server is located.
        /// </summary>
        public static string ServerPath { get => Path.Combine(ABREngine.PackagePath, "ABRServer~"); }
    }
}