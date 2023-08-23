/* ICoordSpaceConverter.cs
 *
 * Copyright (c) 2023 University of Minnesota
 * Authors: Daniel F. Keefe <dfk@umn.edu>, Bridger Herman <herma582@umn.edu>
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
    public interface ICoordSpaceConverter
    {
        /// <summary>
        /// Returns the spatial bounding box of the data in Unity's world space.
        /// </summary>
        Bounds BoundsInWorldSpace { get; }

        /// <summary>
        /// Returns the spatial bounding box of the data in the original
        /// coordinate system of the data.  Data Space coordinates are
        /// typically based upon real world units, like meters.
        /// </summary>
        Bounds BoundsInDataSpace { get; }

        /// <summary>
        /// Converts a point in Unity world coordinates to a point within the
        /// original data coordinate space.  The data coordinate space is
        /// typically defined in real-world units, like cm or meters.  Those
        /// coordinates are often scaled or repositioned within Unity World
        /// space as we zoom into the data or place multiple datasets
        /// side-by-side or do other visualization tasks.
        /// </summary>
        Vector3 WorldSpacePointToDataSpace(Vector3 worldSpacePoint);

        /// <summary>
        /// Converts a point in the original data coordinate space, which is
        /// typically defined in real-world units, like meters, to its current
        /// position in Unity's World coordinate system, which might include a
        /// scale or translation or other transformation based on how the
        /// visualization is designed.
        /// </summary>
        Vector3 DataSpacePointToWorldSpace(Vector3 dataSpacePoint);

        /// <summary>
        /// Returns true if the point in Unity World coordinates lies within the
        /// bounds of the data.
        /// </summary>
        bool ContainsWorldSpacePoint(Vector3 worldSpacePoint);

        /// <summary>
        /// Returns true if the point in data coordinates lies within the
        /// volume. Data coordinates are typically defined in real-world units,
        /// like meters.
        /// </summary>
        bool ContainsDataSpacePoint(Vector3 dataSpacePoint);

        // not implementing these yet... closest "data" is ambiguous.
        // We could get points, lines, surfaces, voxels, ...
        // How do we define what data to get?
        // could specify another param (dataType = DataTopology.xxx)
        // Or, use cell/index terminology (students may not be as familiar)
        //
        // actually, these probably belong in IDataAccessor anyway.
        // - GetClosestDataInWorldSpace
        // - GetClosestDataInDataSpace
        // - GetNearbyDataInWorldSpace
        // - GetNearbyDataInDataSpace
    }

    public interface IVolumeCoordSpaceConverter
    {
        /// <summary>
        /// The x,y,z dimensions of the raw voxel data (i.e., the number of
        /// voxels in each direction) -- read only.
        /// </summary>
        Vector3Int VolumeDimensions { get; }

        /// <summary>
        /// Converts a point in Unity world coordinates to a point within the
        /// data coordinate space and then to a corresponding point in voxel
        /// coordinate space.  The voxel space is defined in units of voxels,
        /// but the coordinates can be fractional so we can represent a point
        /// within a voxel, not just at the center of each voxel.
        /// </summary>
        Vector3 WorldSpacePointToVoxelSpace(Vector3 worldSpacePoint);

        /// <summary>
        /// Converts a point in the original data coordinate space, which is
        /// typically defined in real-world units, like meters, to a point
        /// within the voxel coordinate space.  The voxel space is defined in
        /// units of voxels, but the coordinates can be fractional so we can
        /// represent a point within a voxel, not just at the center of each
        /// voxel.
        /// </summary>
        Vector3 DataSpacePointToVoxelSpace(Vector3 dataSpacePoint);

        /// <summary>
        /// Converts a point in voxel space to the data coordinate space.
        /// Typically this transforms the voxels, which are like pixels in an
        /// image, into a real-world coordinate space like meters.
        /// </summary>
        Vector3 VoxelSpacePointToDataSpace(Vector3 voxelSpacePoint);

        /// <summary>
        /// Converts a point in voxel space (can include fractions) to the
        /// point's current position in Unity's World coordinate system.
        /// </summary>
        Vector3 VoxelSpacePointToWorldSpace(Vector3 voxelSpacePoint);

        /// <summary>
        /// Returns true if the point in voxel space lies within the volume.
        /// Since voxel space is defined in units of voxels, this simply checks
        /// to see if the point lies within the dimensions of the volume.
        /// </summary>
        bool ContainsVoxelSpacePoint(Vector3 voxelSpacePoint);
    }
}