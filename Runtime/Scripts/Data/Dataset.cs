/* Dataset.cs
 *
 * Copyright (c) 2021, University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 */

using System.Collections.Generic;
using UnityEngine;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    /// <summary>
    ///     Lightweight container for a collection of KeyData objects and variables that
    ///     share a common coordinate space. Its bounding box contains all of data, and
    ///     the rendered objects are children of this object's GameObject.
    /// </summary>
    public class Dataset
    {
        // Dictionary of DataPath -> key data objects (paths will match those in
        // datasets dict)
        private Dictionary<string, IKeyData> keyDataObjects = new Dictionary<string, IKeyData>();

        // Dictionaries of DataPath -> variables that manage min/max values and
        // point to the above datasets
        private Dictionary<string, ScalarDataVariable> scalarVariables = new Dictionary<string, ScalarDataVariable>();
        private Dictionary<string, VectorDataVariable> vectorVariables = new Dictionary<string, VectorDataVariable>();

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
        public Bounds CurrentDataSpaceBounds;

        /// <summary>
        ///     GameObject to place all Data Impressions under
        /// </summary>
        public GameObject DataRoot { get; }

        public Dataset(string dataPath, Bounds bounds, Transform parent)
        {
            DataContainer = bounds;
            Path = dataPath;

            ResetBoundsAndTransformation();

            DataRoot = new GameObject("Dataset " + dataPath);
            DataRoot.transform.parent = parent;
        }

        public void AddKeyData(IKeyData keyData)
        {
            RawDataset rawDataset;
            DataManager.Instance.TryGetRawDataset(keyData.Path, out rawDataset);
            Bounds originalBounds = rawDataset.bounds;

            NormalizeWithinBounds.NormalizeAndExpand(
                DataContainer,
                originalBounds,
                ref CurrentDataBounds,
                ref CurrentDataTransformation,
                ref CurrentDataSpaceBounds
            );

            keyDataObjects[keyData.Path] = keyData;
        }

        /// <summary>
        ///     From scratch, recalculate the bounds of this dataset. Start with
        ///     a zero-size bounding box and expand until it encapsulates all
        ///     datasets.
        /// </summary>
        public void RecalculateBounds()
        {
            ResetBoundsAndTransformation();

            foreach (var keyData in keyDataObjects)
            {
                RawDataset rawDataset;
                DataManager.Instance.TryGetRawDataset(keyData.Value.Path, out rawDataset);
                Bounds originalBounds = rawDataset.bounds;

                if (CurrentDataSpaceBounds.size.magnitude <= float.Epsilon)
                {
                    // If the size is zero (first keyData), then start with its
                    // bounds (make sure to not assume we're including (0, 0, 0) in
                    // the bounds)
                    CurrentDataSpaceBounds = originalBounds;
                    NormalizeWithinBounds.Normalize(DataContainer, originalBounds, out CurrentDataTransformation, out CurrentDataBounds);
                }
                else
                {
                    NormalizeWithinBounds.NormalizeAndExpand(
                        DataContainer,
                        originalBounds,
                        ref CurrentDataBounds,
                        ref CurrentDataTransformation,
                        ref CurrentDataSpaceBounds
                    );
                }
            }
        }

        public void AddScalarVariable(ScalarDataVariable scalarVar)
        {
            DataPath.WarnOnDataPathFormat(scalarVar.Path, DataPath.DataPathType.ScalarVar);
            scalarVariables[scalarVar.Path] = scalarVar;
        }

        public void AddVectorVariable(VectorDataVariable vectorVar)
        {
            DataPath.WarnOnDataPathFormat(vectorVar.Path, DataPath.DataPathType.VectorVar);
            vectorVariables[vectorVar.Path] = vectorVar;
        }

        public void TryGetScalarVar(string dataPath, out ScalarDataVariable scalarVar)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.ScalarVar);
            scalarVariables.TryGetValue(dataPath, out scalarVar);
        }
        public void TryGetVectorVar(string dataPath, out VectorDataVariable vectorVar)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.VectorVar);
            vectorVariables.TryGetValue(dataPath, out vectorVar);
        }
        public void TryGetKeyData(string dataPath, out IKeyData keyData)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);
            keyDataObjects.TryGetValue(dataPath, out keyData);
        }

        private void ResetBoundsAndTransformation()
        {
            CurrentDataTransformation = Matrix4x4.identity;
            CurrentDataBounds = new Bounds();
            CurrentDataSpaceBounds = new Bounds();
        }
    }

    /// <summary>
    ///     Should be assigned to anything that is associated with a dataset
    ///     (e.g. KeyData, Variables, and even DataImpressions once they have
    ///     valid KeyData)
    /// </summary>
    public interface IHasDataset
    {
        Dataset GetDataset();
    }
}