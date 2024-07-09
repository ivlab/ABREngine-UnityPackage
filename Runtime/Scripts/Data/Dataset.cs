/* Dataset.cs
 *
 * Copyright (c) 2021, University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
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

using System.Linq;
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
        private Dictionary<string, KeyData> keyDataObjects = new Dictionary<string, KeyData>();

        // Dictionaries of DataPath -> variables that manage min/max values and
        // point to the above datasets
        private Dictionary<string, ScalarDataVariable> scalarVariables = new Dictionary<string, ScalarDataVariable>();
        private Dictionary<string, VectorDataVariable> vectorVariables = new Dictionary<string, VectorDataVariable>();

        /// <summary>
        ///     Path of this dataset (should conform to <see cref="DataPath"/>)
        /// </summary>
        public string Path { get; }

        /// <summary>
        ///     The bounds of the original, data-scale dataset, which grow as we
        ///     add more datasets
        /// </summary>
        public Bounds DataSpaceBounds;

        /// <summary>
        /// All <see cref="VectorDataVariable"/> objects within this dataset.
        /// NOTE: Not every VectorDataVariable applies to every KeyData object!
        /// </summary>
        public VectorDataVariable[] GetVectorVariables(KeyData associatedWith)
        {
            if (associatedWith == null)
            {
                return vectorVariables.Values.ToArray();
            }
            else
            {
                return this.vectorVariables.Values.Where(v => v.IsPartOf(associatedWith)).ToArray();
            }
        }

        public VectorDataVariable[] GetVectorVariables()
        {
            return GetVectorVariables(null);
        }

        /// <summary>
        /// All <see cref="ScalarDataVariable"/> objects within this dataset.
        /// NOTE: Not every ScalarDataVariable applies to every KeyData object!
        /// </summary>
        public ScalarDataVariable[] GetScalarVariables(KeyData associatedWith)
        {
            if (associatedWith == null)
            {
                return scalarVariables.Values.ToArray();
            }
            else
            {
                return this.scalarVariables.Values.Where(v => v.IsPartOf(associatedWith)).ToArray();
            }
        }

        public ScalarDataVariable[] GetScalarVariables()
        {
            return GetScalarVariables(null);
        }

        /// <summary>
        /// All <see cref="KeyData"/> objects within this dataset
        /// </summary>
        public KeyData[] GetKeyData()
        {
            return keyDataObjects.Values.ToArray();
        }

        public Dataset(string dataPath, Bounds bounds, Transform parent)
        {
            Path = dataPath;
        }

        public void AddKeyData(KeyData keyData)
        {
            RawDataset rawDataset;
            if (ABREngine.Instance.Data.TryGetRawDataset(keyData.Path, out rawDataset))
            {
                Bounds originalBounds = rawDataset.bounds;
                keyDataObjects[keyData.Path] = keyData;
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

        public bool TryGetScalarVar(string dataPath, out ScalarDataVariable scalarVar)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.ScalarVar);
            return scalarVariables.TryGetValue(dataPath, out scalarVar);
        }
        public bool TryGetVectorVar(string dataPath, out VectorDataVariable vectorVar)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.VectorVar);
            return vectorVariables.TryGetValue(dataPath, out vectorVar);
        }
        public bool TryGetKeyData(string dataPath, out KeyData keyData)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);
            return keyDataObjects.TryGetValue(dataPath, out keyData);
        }

        public Dictionary<string, KeyData> GetAllKeyData()
        {
            return keyDataObjects;
        }

        public Dictionary<string, ScalarDataVariable> GetAllScalarVars()
        {
            return scalarVariables;
        }

        public Dictionary<string, VectorDataVariable> GetAllVectorVars()
        {
            return vectorVariables;
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