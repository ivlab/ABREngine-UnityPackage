/* Dataset.cs
 *
 * Copyright (c) 2021, University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 */

using UnityEngine;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    /// <summary>
    ///     A collection of KeyData objects that share a common coordinate
    ///     space. Its bounding box contains all of data, and the rendered
    ///     objects are children of this object's GameObject.
    /// </summary>
    public class Dataset
    {
        /// <summary>
        ///     Room-scale (Unity rendering space) bounds that all data should
        ///     be contained within
        /// </summary>
        public Bounds DataContainer { get; }

        /// <summary>
        ///     Path of this dataset (should conform to DataPath)
        /// </summary>
        public string Path { get; }

        /// <summary>
        ///     Transformation from the original data space into the room-scale
        ///     bounds
        /// </summary>
        public Matrix4x4 CurrentDataTransformation;

        /// <summary>
        ///     The actual bounds (contained within DataContainer) of the
        ///     room-scale dataset
        /// </summary>
        public Bounds CurrentDataBounds;

        /// <summary>
        ///     The bounds of the original, data-scale dataset, which grow as we
        ///     add more datasets
        /// </summary>
        public Bounds CurrentOriginalDataBounds;

        /// <summary>
        ///     GameObject to place all Data Impressions under
        /// </summary>
        public GameObject _dataRoot;

        public Dataset(string dataPath, Bounds bounds, Transform parent)
        {
            DataContainer = bounds;
            Path = dataPath;

            CurrentDataTransformation = Matrix4x4.identity;
            CurrentDataBounds = new Bounds();
            CurrentOriginalDataBounds = new Bounds();

            _dataRoot = new GameObject("Dataset " + dataPath);
            _dataRoot.transform.parent = parent;
        }

        public void AddKeyData(IKeyData keyData)
        {
            RawDataset rawDataset;
            DataManager.Instance.TryGetRawDataset(keyData.Path, out rawDataset);
            Bounds originalBounds = rawDataset.bounds;

            // Get the scale and bounds after trying to fit the dataset within the
            // DataContainer
            Bounds containedBounds;
            Matrix4x4 transform;
            NormalizeWithinBounds.Normalize(DataContainer, originalBounds, out transform, out containedBounds);

            // If the new bounds would exceed the size of the current data
            // bounds, reassign the data transform
            if (originalBounds.size.MaxComponent() > CurrentOriginalDataBounds.size.MaxComponent())
            {
                CurrentDataBounds.Encapsulate(containedBounds);
                CurrentDataTransformation = transform;
            }
            CurrentOriginalDataBounds.Encapsulate(originalBounds);
        }
    }
}