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
    /// When constructing a custom dataset, you can load it directly into the
    /// engine and access its imported contents via the <see cref="KeyData"/>
    /// object returned by <see cref="ImportRawDataset"/>.
    /// <code>
    /// public class DataManagerExample : MonoBehaviour
    /// {
    ///     void Start()
    ///     {
    ///         // Generate 100 random points with "data" values
    ///         List&lt;Vector3&gt; points = new List&lt;Vector3&gt;();
    ///         List&lt;float&gt; dataValues = new List&lt;float&gt;();
    ///         for (int i = 0; i &lt; 100; i++)
    ///         {
    ///             points.Add(Random.insideUnitSphere);
    ///             dataValues.Add(i);
    ///         }
    /// 
    ///         // Create some bounds
    ///         Bounds b = new Bounds(Vector3.zero, Vector3.one);
    /// 
    ///         // Create a dictionary to name the scalar values
    ///         Dictionary&lt;string, List&lt;float&gt;&gt; scalarVars = new Dictionary&lt;string, List&lt;float&gt;&gt; {{ "someData", dataValues }};
    /// 
    ///         // Create an ABR-formatted dataset
    ///         RawDataset abrPoints = RawDatasetAdapter.PointsToPoints(points, b, scalarVars, null);
    /// 
    ///         // AND, import these data to ABR
    ///         KeyData pointsKD = ABREngine.Instance.Data.ImportRawDataset(abrPoints);
    /// 
    ///         // From here, we can access the keyData, scalarVariables, and vectorVariables
    ///         Debug.Log(pointsKD);                             // the key data (point geometry) we just imported
    ///         Debug.Log(pointsKD.GetScalarVariables().Length); // length of 1
    ///         Debug.Log(pointsKD.GetScalarVariables()[0]);     // the 'someData' variable we declared above
    ///         Debug.Log(pointsKD.GetVectorVariables().Length); // length of 0 -- we didn't declare any vector vars here.
    ///     }
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

        // Default internal path for data if path is not provided
        private const string DefaultDatasetPath = "Imported/Imported";
        private int importCount = 0;
        private string DefaultKeyDataPath
        {
            get
            {
                return DefaultDatasetPath + "/KeyData/" + importCount;
            }
        }

        private List<IDataLoader> dataLoaders = new List<IDataLoader>();

        public DataManager(string datasetPath)
        {
            this.appDataPath = datasetPath;
            Directory.CreateDirectory(this.appDataPath);
            Debug.Log("Dataset Path: " + appDataPath);

            // Determine which loaders are available to use
            // First, look in `Media` folder
            dataLoaders.Add(new MediaDataLoader());

            // Then, look in any `Resources` folder
            dataLoaders.Add(new ResourcesDataLoader());

            // Afterwards, if we're connected to a data server, look there...
            if (ABREngine.Instance.Config.dataServerUrl?.Length > 0)
            {
                Debug.Log("Allowing loading of datasets from " + ABREngine.Instance.Config.dataServerUrl);
                dataLoaders.Add(new HttpDataLoader());
            }
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
        /// <param name="dataPath">Data path to load. If loading from the media
        /// directory, you can use the relative path inside that folder (but
        /// exclude the .bin/.json extension)</param>
        /// <typeparam name="T">Any <see cref="IDataLoader"/> type</typeparam>
        /// <example>
        /// If you're working with a pre-existing dataset (i.e., one that already
        /// exists in ABR raw data format in your media folder), you can use <see
        /// cref="DataManager.LoadRawDataset"/> to obtain a <see cref="RawDataset"/>.
        /// <code>
        /// // Load from a .bin/.json file pair in the datasets folder in the
        /// // media directory. Most of the time when you're fetching an existing
        /// // dataset, this is what you'll want to do. Just make sure the
        /// // dataset actually exists in the media folder!
        /// RawDataset ds1 = ABREngine.Instance.Data.LoadRawDataset&lt;MediaDataLoader&gt;("Test/Test/KeyData/Example");
        ///
        /// // You can also load an ABR raw dataset from a web resource. This requires setting up an ABR data server.
        /// RawDataset ds2 = ABREngine.Instance.Data.LoadRawDataset&lt;HttpDataLoader&gt;("Test/Test/KeyData/Example");
        /// </code>
        /// </example>
        /// <returns>
        /// Returns the actual <see cref="RawDataset"/> if the dataset was found, `null` if not found.
        /// </returns>
        [Obsolete("It is recommended to use `LoadData` or `LoadRawDataset` instead of this method.")]
        public RawDataset LoadRawDataset<T>(string dataPath)
        where T : IDataLoader, new()
        {
            RawDataset ds = (new T()).LoadData(dataPath);
            // Only import if there are actual data present
            if (ds != null)
            {
                ImportRawDataset(dataPath, ds);
                return ds;
            }
            else
            {
                Debug.LogError("Unable to load Raw Dataset " + dataPath);
                return null;
            }
        }

        /// <summary>
        /// Load a raw dataset into a RawDataset object by its data path and
        /// return the rawdataset after it has been successfully imported.
        /// </summary>
        /// <param name="dataPath">Data path to load. If loading from the media
        /// directory, you can use the relative path inside that folder (but
        /// exclude the .bin/.json extension)</param>
        /// <example>
        /// If you're working with a pre-existing dataset (i.e., one that already
        /// exists in ABR raw data format in your media folder), you can use <see
        /// cref="DataManager.LoadRawDataset"/> to obtain a <see cref="RawDataset"/>.
        /// <code>
        /// // Load from a .bin/.json file pair in the datasets folder in the
        /// // media directory. Most of the time when you're fetching an existing
        /// // dataset, this is what you'll want to do. Just make sure the
        /// // dataset actually exists in the media folder!
        /// // Or, load from a Resources directory in Unity.
        /// // You can also load an ABR raw dataset from a web resource. This requires setting up an ABR data server.
        /// RawDataset ds1 = ABREngine.Instance.Data.LoadRawDataset("Test/Test/KeyData/Example");
        /// </code>
        /// </example>
        /// <returns>
        /// Returns the actual <see cref="RawDataset"/> if the dataset was found, `null` if not found.
        /// </returns>
        public RawDataset LoadRawDataset(string dataPath)
        {
            foreach (IDataLoader loader in dataLoaders)
            {
                try
                {
                    RawDataset ds = loader.LoadData(dataPath);
                    if (ds != null)
                    {
                        Debug.Log($"Dataset `{dataPath} loaded from " + loader.GetType().Name);
                        return ds;
                    }
                    else
                        throw new Exception();
                }
                catch
                {
                    Debug.LogWarning($"Dataset `{dataPath}` not found in " + loader.GetType().Name);
                }
            }
            Debug.LogWarning($"Dataset `{dataPath}` not found in any data loader");
            return null;
        }

        /// <summary>
        /// Attempt to load the data described in `dataPath` from any available
        /// resource, including a Resources folder, the <see
        /// cref="media-folder.md"/>, or a HTTP web resource.
        /// </summary>
        /// <param name="dataPath">Data path to load. If loading from the media
        /// directory, you can use the relative path inside that folder (but
        /// exclude the .bin/.json extension)</param>
        /// <returns>
        /// Returns the <see cref="KeyData"/> object if the dataset was found, `null` if not found.
        /// </returns>
        public KeyData LoadData(string dataPath)
        {
            RawDataset ds = LoadRawDataset(dataPath);
            KeyData kd = ImportRawDataset(dataPath, ds);
            return kd;
        }

        /// <summary>
        /// Entirely remove a RawDataset from ABR memory.
        /// </summary>
        /// <param name="dataPath">The data path / key data to be unloaded</param>
        /// <remarks>
        /// This method *does not check if the dataset is currently in use*, so utilize this method with care!
        /// </remarks>
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
        /// <returns>
        /// Returns the Key Data and variables that were just imported to
        /// this data path.
        /// </returns>
        public KeyData ImportRawDataset(RawDataset importing)
        {
            importCount++;
            return ImportRawDataset(DefaultKeyDataPath, importing);
        }

        /// <summary>
        /// Import a raw dataset into ABR. This method makes the dataset
        /// available as a key data object and makes all of its scalar and
        /// vector variables available across ABR.
        /// </summary>
        /// <returns>
        /// Returns the Key Data and variables that were just imported to
        /// this data path.
        /// </returns>
        public KeyData ImportRawDataset(string dataPath, RawDataset importing)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);
            // See what dataset this RawDataset is a part of
            string datasetPath = DataPath.GetDatasetPath(dataPath);

            // See if we have any data from that dataset yet
            try
            {
                // If we don't, create the dataset
                Dataset dataset;
                if (!TryGetDataset(datasetPath, out dataset))
                {
                    Bounds dataContainer = ABREngine.Instance.Config.dataContainer;
                    dataset = new Dataset(datasetPath, dataContainer, ABREngine.Instance.ABRTransform);
                }

                datasets[datasetPath] = dataset;
                rawDatasets[dataPath] = importing;

                ImportVariables(dataPath, importing, dataset);
                ImportKeyData(dataPath, importing, dataset);

                // Retrieve the KeyData object that was just imported
                IKeyData keyData;
                if (!dataset.TryGetKeyData(dataPath, out keyData))
                {
                    Debug.LogError($"Failed to import Key Data for {dataPath} properly");
                    return null;
                }

                return keyData as KeyData;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            return null;
        }

        /// <summary>
        /// Save a copy of a RawDataset into the media folder.
        /// </summary>
        public void CacheRawDataset(string dataPath, RawDataset rds)
        {
            Tuple<string, byte[]> dataPair = rds.ToFilePair();
            CacheRawDataset(dataPath, dataPair.Item1, dataPair.Item2);
        }

        /// <summary>
        /// Save a copy of the RawDataset described by `json` and `data` to the
        /// media folder.
        /// </summary>
        public void CacheRawDataset(string dataPath, in string json, in byte[] data)
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
            fs.Write(data, 0, data.Length);
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

                // Discard NaNs, if present, by finding the actual data range (ensure there are NEVER any NaNs in Range)
                // NOTE: this may change the data range from the one imported in the RawDataset.
                if (float.IsNaN(scalarDataVariable.Range.min) || float.IsNaN(scalarDataVariable.Range.max))
                {
                    var noNans = rawDataset.GetScalarArray(DataPath.GetName(scalarPath)).Where(v => !float.IsNaN(v));
                    scalarDataVariable.Range.min = noNans.Min();
                    scalarDataVariable.Range.max = noNans.Max();
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