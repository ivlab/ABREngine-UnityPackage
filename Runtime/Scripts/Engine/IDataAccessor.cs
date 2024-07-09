/* IDataAccessor.cs
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

using System.Collections.Generic;
using UnityEngine;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Interface to implement to share easier access to data within ABR.
    /// </summary>
    public interface IDataAccessor
    {
        /// <summary>
        /// Finds the closest data to the given point, in world space.
        /// </summary>
        /// <returns>A <see cref="DataPoint"/> with the closest data to a given point</returns>
        DataPoint GetClosestDataInWorldSpace(Vector3 worldSpacePoint);

        /// <summary>
        /// Finds the closest data to the given point, in data space.
        /// </summary>
        /// <returns>A <see cref="DataPoint"/> with the closest data to a given point</returns>
        DataPoint GetClosestDataInDataSpace(Vector3 dataSpacePoint);

        /// <summary>
        /// Finds the data within a certain radius of the given point, in world space.
        /// </summary>
        /// <returns>A list of <see cref="DataPoint"/> within a given radius of a given point</returns>
        List<DataPoint> GetNearbyDataInWorldSpace(Vector3 worldSpacePoint, float radiusInWorldSpace);

        /// <summary>
        /// Finds the data within a certain radius of the given point, in data space.
        /// </summary>
        /// <returns>A list of <see cref="DataPoint"/> within a given radius of a given point</returns>
        List<DataPoint> GetNearbyDataInDataSpace(Vector3 dataSpacePoint, float radiusInDataSpace);

        /// <summary>
        /// Get a scalar value from a particular data variable at the closest
        /// point in world space. If `keyData` is null, this method will look at
        /// every key data object that has the given variable.
        /// </summary>
        float GetScalarValueAtClosestWorldSpacePoint(Vector3 point, ScalarDataVariable variable, KeyData keyData = null);
        float GetScalarValueAtClosestWorldSpacePoint(Vector3 point, string variableName, KeyData keyData = null);

        /// <summary>
        /// Get a scalar value from a particular data variable at the closest
        /// point in data space. If `keyData` is null, this method will look at
        /// every key data object that has the given variable.
        /// </summary>
        float GetScalarValueAtClosestDataSpacePoint(Vector3 point, ScalarDataVariable variable, KeyData keyData = null);
        float GetScalarValueAtClosestDataSpacePoint(Vector3 point, string variableName, KeyData keyData = null);

        /// <summary>
        /// Get a vector value from a particular data variable at the closest
        /// point in data space. If `keyData` is null, this method will look at
        /// every key data object that has the given variable.
        /// </summary>
        Vector3 GetVectorValueAtClosestWorldSpacePoint(Vector3 point, VectorDataVariable variable, KeyData keyData = null);
        Vector3 GetVectorValueAtClosestWorldSpacePoint(Vector3 point, string variableName, KeyData keyData = null);

        /// <summary>
        /// Get a vector value from a particular data variable at the closest
        /// point in data space. If `keyData` is null, this method will look at
        /// every key data object that has the given variable.
        /// </summary>
        Vector3 GetVectorValueAtClosestDataSpacePoint(Vector3 point, VectorDataVariable variable, KeyData keyData = null);
        Vector3 GetVectorValueAtClosestDataSpacePoint(Vector3 point, string variableName, KeyData keyData = null);

        /// <summary>
        /// Normalize a data value from 0 to 1 based on the given key data with a scalar data variable
        /// </summary>
        float NormalizeScalarValue(float value, KeyData keyData, ScalarDataVariable variable);


        // Bridger's design notes from 2023-08-23
        // not implementing these yet... closest "data" is ambiguous.
        // We could get points, lines, surfaces, voxels, ...
        // How do we define what data to get?
        // could specify another param (dataType = DataTopology.xxx)
        // Or, use cell/index terminology (students may not be as familiar)
        // Points:
        // - *Get closest point. (vertices)
        // Lines:
        // - *Get closest line (cells)
        // - *Get closest point on line (vertices)
        // Surfaces
        // - *Get closest vertex (vertices)
        // - Get closest (enclosed) surface
        // Volumes
        // - *Get closest voxel (voxel)
        // - Get closest volume structure
        //
        // how do we make this generic enough?
        // generic T, implements IDataAccessStrategy
        //
        // what's the simplest approach? just implement ONE method. the only one
        // where it could become an immediate problem is the Line case
        //
        // actually, these probably belong in IDataAccessor anyway.
        // - GetClosestDataInWorldSpace
        // - GetClosestDataInDataSpace
        // - GetNearbyDataInWorldSpace
        // - GetNearbyDataInDataSpace
    }

    /// <summary>
    /// A <see cref="DataPoint"/> describes a point in a dataset. Often, this is
    /// useful when querying a dataset, for example, to find the closest data
    /// point to a given point in world space.
    /// </summary>
    public class DataPoint
    {
        /// <summary>
        /// Key data that the data point belongs to.
        /// </summary>
        public KeyData keyData;

        /// <summary>
        /// The coordinates of the point, in data space
        /// </summary>
        public Vector3 dataSpacePoint;

        /// <summary>
        /// The coordinates of the point, in Unity world space
        /// </summary>
        public Vector3 worldSpacePoint;

        /// <summary>
        /// "Cell" index that this point belongs to. "Cells" are implemented
        /// differently for different types of data. See the table below to see
        /// what cells represent in each data type / DataImpression type:
        /// 
        /// | <see cref="DataImpression"/> Type | <see cref="DataTopology"/> Type | Cell Description |
        /// | --- | --- | --- |
        /// | <see cref="SimpleSurfaceDataImpression"/> | <see cref="DataTopology.Triangles"/> or <see cref="DataTopology.Quads"/> | Cells are the indices of triangles or quads |
        /// | <see cref="SimpleLineDataImpression"/> | <see cref="DataTopology.LineStrip"/> | Cells represent individual lines |
        /// | <see cref="SimpleGlyphDataImpression"/> | <see cref="DataTopology.Points"/> | Cells are unused, will always be `0` |
        /// | <see cref="InstancedSurfaceDataImpression"/> | <see cref="DataTopology.Points"/> | Cells are unused, will always be `0` |
        /// </summary>
        public int cellIndex;

        /// <summary>
        /// "Vertex" index that this point belongs to.
        /// </summary>
        public int vertexIndex;
    }

    /// <summary>
    /// Interface to implement to share easier access to volume data within ABR.
    /// </summary>
    public interface IVolumeDataAccessor
    {
        /// <summary>
        /// Looks up the value in the original voxel data based upon a position
        /// specified in Unity World coordinates.
        /// </summary>
        float GetScalarValueAtWorldSpacePoint(Vector3 worldSpacePoint);

        /// <summary>
        /// Looks up the value in the original voxel data based upon a position specified in data space coordinates
        /// (i.e., real-world units).
        /// </summary>
        float GetScalarValueAtDataSpacePoint(Vector3 dataSpacePoint);

        /// <summary>
        /// Looks up the value in the original voxel data based upon a point in
        /// voxel space.  In the raw volume data each voxel contains one data
        /// value.  If we think of those being the values of the data at a point
        /// right in the center of each voxel, then the point we are querying
        /// will rarely align perfectly with those center points; rather, it
        /// will fall somewhere between them.  That is why the pointInVoxelSpace
        /// is allowed to include a fractional component.  This function uses
        /// the fractional portion to perform a tri-linear interpolation of the
        /// data from the eight surrounding voxels to estimate the data value at
        /// the exact 3D location requested.  See also
        /// GetValueAtNearestVoxel(), which is faster but less precise because
        /// it does not use interpolation.  Or, GetValueAtVoxel(), which is
        /// even faster but does not support fractional voxel coordinates.
        /// </summary>
        float GetScalarValueAtVoxelSpacePoint(Vector3 voxelSpacePoint);

        /// <summary>
        /// Looks up the value in the original voxel data based upon a point in voxel space.  This function does
        /// not use interpolation.  It simply returns that data value stored for the center point of the closest
        /// voxel.
        /// </summary>
        float GetScalarValueAtNearestVoxel(Vector3Int voxelCoords);

        /// <summary>
        /// Uses the min and max values in the volume data to remap a data value to
        /// a normalized range between 0 and 1.
        /// </summary>
        float NormalizeScalarValue(float value, KeyData keyData);
    }
}