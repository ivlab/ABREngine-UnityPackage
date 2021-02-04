/* NormalizeDataScale.cs
 *
 * Copyright (c) 2021, University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 */

using System;
using UnityEngine;
using System.Linq;

namespace IVLab.ABREngine
{
    /// <summary>
    ///     Inspired by the IVLab.Utilities.SpaceTransforms NormalizeMeshScale
    /// </summary>
    public static class DataScaling
    {
        // Normalized scale => all datasets will have vertex coords ranging from -1, 1
        // public float normalizedScaleMeters = 2.0f;

        // Should translate (center) the data?
        // public bool adjustToCenter = true;

        // Should rotate so biggest side faces up (like a table)?
        // public bool rotateToTable = false;

        /// <summary>
        ///     Scale this dataset down to fit in a particular bounding size
        ///     (meters)
        /// </summary>
        public static Matrix4x4 NormalizeDataScale(RawDataset dataset, float normalizedScaleMeters = 2.0f, bool adjustToCenter = false, bool rotateToTable = false)
        {
            Bounds bounds = dataset.bounds;

            // Squash the data into our maxAutoScaleMeters dimensions
            float[] boundsSize = {
                bounds.size.x,
                bounds.size.y,
                bounds.size.z,
            };

            float maxAxis = boundsSize.Max();

            float scaleFactor = normalizedScaleMeters / maxAxis;

            // Find the translation required
            Vector3 offset = bounds.center;
            Vector3 offsetScaled = offset * scaleFactor;

            // Find the rotation required if we want to rotate it to be a table
            float minAxis = boundsSize.Min();
            int minIndex = Array.IndexOf(boundsSize, minAxis);
            Vector3[] axes = {
                Vector3.right,
                Vector3.up,
                Vector3.forward,
            };
            Quaternion tableRotation = Quaternion.FromToRotation(axes[minIndex], Vector3.up);

            // Save the transformation we're about to perform
            Quaternion actualRotation = rotateToTable ? tableRotation : Quaternion.identity;
            Vector3 actualOffset = adjustToCenter ? offsetScaled : Vector3.zero;

            Matrix4x4 transform = Matrix4x4.identity;

            // If anyone can explain why this needs to be R * T * S instead of 
            // T * R * S, please let me know :)
            if (rotateToTable)
            {
                transform *= Matrix4x4.Rotate(actualRotation);
            }
            if (adjustToCenter)
            {
                transform *= Matrix4x4.Translate(-actualOffset);
            }
            transform *= Matrix4x4.Scale(Vector3.one * scaleFactor);

            return transform;
        }
    }
}
