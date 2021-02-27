/* Dataset.cs
 *
 * Copyright (c) 2021, University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
 *
 */

using System.Collections.Generic;
using UnityEngine;

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
        ///     Path of this dataset (should conform to DataPath)
        /// </summary>
        public string Path { get; }

        /// <summary>
        ///     The bounds of the original, data-scale dataset, which grow as we
        ///     add more datasets
        /// </summary>
        public Bounds DataSpaceBounds;

        public Dataset(string dataPath, Bounds bounds, Transform parent)
        {
            Path = dataPath;
        }

        public void AddKeyData(IKeyData keyData)
        {
            RawDataset rawDataset;
            ABREngine.Instance.Data.TryGetRawDataset(keyData.Path, out rawDataset);
            Bounds originalBounds = rawDataset.bounds;

            keyDataObjects[keyData.Path] = keyData;
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

        public Dictionary<string, IKeyData> GetAllKeyData()
        {
            return keyDataObjects;
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