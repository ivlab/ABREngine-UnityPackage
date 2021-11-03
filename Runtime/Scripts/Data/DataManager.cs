/* DataManager.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
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

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

using IVLab.Utilities;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Manager where all datasets, key data, and variables live. This class
    /// makes the connection between Datasets and RawDatasets. This class is
    /// useful for obtaining any KeyData and Variables needed to apply to Data
    /// Impressions.
    /// </summary>
    /// <example>
    /// Key data and variables can be loaded directly from the data manager:
    /// <code>
    /// // Load an example dataset
    /// await ABREngine.Instance.Data.LoadRawDataset&lt;ResourcesDataLoader&gt;("Test/Test/KeyData/Example");
    /// // Load the high-level dataset that both Contour and Points are contained within
    /// Dataset ds = null;
    /// if (!ABREngine.Instance.Data.TryGetDataset(datasetPath, out ds))
    /// {
    ///     Debug.LogError("Unable to load dataset " + datasetPath);
    ///     return;
    /// }
    /// 
    /// KeyData kd = null;
    /// // Populate the key data objects from dataset
    /// if (!ds.TryGetKeyData("Test/Test/KeyData/Example", out kd))
    /// {
    ///     Debug.LogError("Key data not found in dataset");
    ///     return;
    /// }
    /// 
    /// ScalarDataVariable s = null;
    /// // Populate the variables from dataset
    /// if (!ds.TryGetScalarVar("Test/Test/ScalarVar/ExampleVar", out s))
    /// {
    ///     Debug.LogError("Dataset does not have variable");
    ///     return;
    /// }
    /// </code>
    /// Additionally, the actual raw data can be loaded from the data manager.
    /// Generally this is not necessary, simply using the high-level variables
    /// above in conjunction with Data Impressions is usually sufficient.
    /// <code>
    /// RawDataset rds = null;
    /// if (ABREngine.Instance.Data.TryGetRawDataset("Test/Test/KeyData/Example", out rds))
    /// {
    ///     float[] var = rds.GetScalarArray("ExampleVar");
    /// }
    /// </code>
    /// </example>
    public class DataManager
    {
        private string appDataPath;

        // Dictionary of Key Data DataPath -> raw datasets that contain the
        // actual data (these are BIG)
        //
        // This can be a bit confusing because these keys follow the KeyData
        // convention and technically they are the raw key data, but they don't
        // have any sense of space / what dataset they're a part of (hence why
        // KeyData and Variables are separated into the Dataset, not the
        // RawDataset)
        //
        // RawDatasets are more analogous to KeyData than they are to Datasets.
        private Dictionary<string, RawDataset> rawDatasets = new Dictionary<string, RawDataset>();
        
        // Dictionary of Dataset DataPath -> Dataset, which contains all the key
        // data and variables for a particular dataset
        private Dictionary<string, Dataset> datasets = new Dictionary<string, Dataset>();

        public DataManager(string datasetPath)
        {
            this.appDataPath = datasetPath;
            Directory.CreateDirectory(this.appDataPath);
            Debug.Log("Dataset Path: " + appDataPath);
        }

        /// <summary>
        /// Attempt to get a RawDataset at a particular data path.
        /// </summary>
        /// <returns>
        /// Returns true if the raw dataset was found, false if not, and
        /// populates the `out RawDataset dataset` accordingly.
        /// </returns>
        public bool TryGetRawDataset(string dataPath, out RawDataset dataset)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);
            return rawDatasets.TryGetValue(dataPath, out dataset);
        }

        /// <summary>
        /// Attempt to get a lightweight dataset by its data path.
        /// </summary>
        /// <returns>
        /// Returns true if the dataset was found, and populates the `out
        /// Dataset dataset` accordingly.
        /// </returns>
        public bool TryGetDataset(string dataPath, out Dataset dataset)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.Dataset);
            return datasets.TryGetValue(dataPath, out dataset);
        }

        /// <summary>
        /// Retrieve all datasets that are currently loaded into the ABR Engine
        /// </summary>
        /// <returns>
        /// List of currently loaded Datasets
        /// </returns>
        public List<Dataset> GetDatasets()
        {
            return datasets.Values.ToList();
        }

        /// <summary>
        /// Load a raw dataset into a RawDataset object by its data path and
        /// return the rawdataset after it has been successfully imported.
        /// </summary>
        /// <examples>
        /// Datasets may be loaded from any of the following locations:
        /// <code>
        /// // From a file in the media directory
        /// await ABREngine.Instance.Data.LoadRawDataset&lt;Media&gt;("Test/Test/KeyData/Example");
        ///
        /// // From a web resource
        /// await ABREngine.Instance.Data.LoadRawDataset&lt;HttpDataLoader&gt;("Test/Test/KeyData/Example");
        /// </code>
        /// </examples>
        public async Task<RawDataset> LoadRawDataset<T>(string dataPath)
        where T : IDataLoader, new()
        {
            RawDataset ds = await (new T()).TryLoadDataAsync(dataPath);
            // Only import if there are actual data present
            if (ds != null)
            {
                await ImportRawDataset(dataPath, ds);
                return ds;
            }
            else
            {
                Debug.LogError("Unable to load Raw Dataset " + dataPath);
                return null;
            }
        }

        /// <summary>
        /// Unload a raw dataset from a RawDataset object by its data path. 
        /// </summary>
        /// <examples>
        /// Datasets may be unloaded from any of the following locations:
        /// <code>
        /// // From a file in the media directory
        /// await ABREngine.Instance.Data.UnloadRawDataset&lt;Media&gt;("Test/Test/KeyData/Example");
        ///
        /// // From a web resource
        /// await ABREngine.Instance.Data.UnloadRawDataset&lt;HttpDataLoader&gt;("Test/Test/KeyData/Example");
        /// </code>
        /// </examples> 
        public void UnloadRawDataset(string dataPath)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);
            // See what dataset this RawDataset is a part of
            string datasetPath = DataPath.GetDatasetPath(dataPath);
            rawDatasets.Remove(dataPath);
            datasets.Remove(datasetPath);
        }

        /// <summary>
        /// Import a raw dataset into ABR. This method makes the dataset
        /// available as a key data object and makes all of its scalar and
        /// vector variables available across ABR.
        /// </summary>
        public async Task ImportRawDataset(string dataPath, RawDataset importing)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);
            // See what dataset this RawDataset is a part of
            string datasetPath = DataPath.GetDatasetPath(dataPath);

            // See if we have any data from that dataset yet
            // Needs to be run in main thread because of this.transform
            await UnityThreadScheduler.Instance.RunMainThreadWork(() => {
                try
                {
                    // If we don't, create the dataset
                    Dataset dataset;
                    if (!TryGetDataset(datasetPath, out dataset))
                    {
                        Bounds dataContainer = ABREngine.Instance.Config.Info.defaultBounds.Value;
                        dataset = new Dataset(datasetPath, dataContainer, ABREngine.Instance.transform);
                    }

                    datasets[datasetPath] = dataset;
                    rawDatasets[dataPath] = importing;

                    ImportVariables(dataPath, importing, dataset);
                    ImportKeyData(dataPath, importing, dataset);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            });
        }

        /// <summary>
        /// Save a copy of the RawDataset described by `json` and `data` to the
        /// media folder.
        /// </summary>
        public async Task CacheRawDataset(string dataPath, string json, byte[] data)
        {
            Debug.Log("Saving " + dataPath + " to " + this.appDataPath);

            FileInfo jsonFile = GetRawDatasetMetadataFile(dataPath);

            if (!jsonFile.Directory.Exists)
            {
                Directory.CreateDirectory(jsonFile.DirectoryName);
            }

            using (StreamWriter file = new StreamWriter(jsonFile.FullName, false))
            {
                file.Write(json);
            }

            FileInfo binFile = new FileInfo(Path.Combine(this.appDataPath, dataPath + ".bin"));

            FileStream fs = File.Create(binFile.FullName);
            await fs.WriteAsync(data, 0, data.Length);
            fs.Close();
        }

        private FileInfo GetRawDatasetMetadataFile(string dataPath)
        {
            return new System.IO.FileInfo(System.IO.Path.Combine(this.appDataPath, dataPath + ".json"));
        }
        private FileInfo GetRawDatasetBinaryFile(string dataPath)
        {
            return new System.IO.FileInfo(System.IO.Path.Combine(this.appDataPath, dataPath + ".bin"));
        }

        // Import all the variables from a particular dataset with the path
        // `dataPath`
        //
        // This populates the dictionaries scalarVariables and vectorVariables
        private void ImportVariables(string dataPath, RawDataset rawDataset, Dataset dataset)
        {
            string datasetPath = DataPath.GetDatasetPath(dataPath);

            // Import all scalar variables
            string scalarVarRoot = DataPath.Join(datasetPath, DataPath.DataPathType.ScalarVar);
            foreach (var scalarArrayName in rawDataset.scalarArrayNames)
            {
                string scalarPath = DataPath.Join(scalarVarRoot, scalarArrayName);
                ScalarDataVariable scalarDataVariable;
                if (!dataset.TryGetScalarVar(scalarPath, out scalarDataVariable))
                {
                    // Create a new scalar variable
                    scalarDataVariable = new ScalarDataVariable(scalarPath);
                    scalarDataVariable.Range.min = rawDataset.GetScalarMin(scalarArrayName);
                    scalarDataVariable.Range.max = rawDataset.GetScalarMax(scalarArrayName);
                    dataset.AddScalarVariable(scalarDataVariable);
                }
                else
                {
                    // If this variable was already there, adjust its bounds to
                    // include the newly imported rawDataset
                    if (!scalarDataVariable.CustomizedRange)
                    {
                        scalarDataVariable.Range.min = Mathf.Min(scalarDataVariable.Range.min, rawDataset.GetScalarMin(scalarArrayName));
                        scalarDataVariable.Range.max = Mathf.Max(scalarDataVariable.Range.max, rawDataset.GetScalarMax(scalarArrayName));
                    }
                }

                scalarDataVariable.OriginalRange.min = scalarDataVariable.Range.min;
                scalarDataVariable.OriginalRange.max = scalarDataVariable.Range.max;
            }

            // Import all vector variables
            string vectorVarRoot = DataPath.Join(datasetPath, DataPath.DataPathType.VectorVar);
            foreach (var vectorArrayName in rawDataset.vectorArrayNames)
            {
                string vectorPath = DataPath.Join(vectorVarRoot, vectorArrayName);
                VectorDataVariable vectorDataVariable;
                if (!dataset.TryGetVectorVar(vectorPath, out vectorDataVariable))
                {
                    // Create a new vector variable
                    vectorDataVariable = new VectorDataVariable(vectorPath);
                    vectorDataVariable.Range.min = rawDataset.GetVectorMin(vectorArrayName);
                    vectorDataVariable.Range.max = rawDataset.GetVectorMax(vectorArrayName);
                    dataset.AddVectorVariable(vectorDataVariable);
                }
                else
                {
                    // TODO: Not implemented yet
                    vectorDataVariable.Range.min = Vector3.zero;
                    vectorDataVariable.Range.max = Vector3.zero;
                }

                vectorDataVariable.OriginalRange.min = vectorDataVariable.Range.min;
                vectorDataVariable.OriginalRange.max = vectorDataVariable.Range.max;
            }
        }

        // Build the key data associations
        private void ImportKeyData(string dataPath, RawDataset rawDataset, Dataset dataset)
        {
            // Infer the type of data from the topology
            Type dataType = KeyDataMapping.typeMap[rawDataset.dataTopology];

            // Use reflection to construct the object (should only match one)
            ConstructorInfo[] constructors = dataType.GetConstructors();

            // Construct the object with the data path argument
            string[] args = new string[] { dataPath };
            IKeyData keyData = constructors[0].Invoke(args) as IKeyData;

            dataset.AddKeyData(keyData);
        }
    }
}