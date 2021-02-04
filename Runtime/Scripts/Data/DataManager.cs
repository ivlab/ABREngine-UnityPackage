/* DataManager.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

using Newtonsoft.Json;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public class DataManager : Singleton<DataManager>
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

        protected override void Awake()
        {
            base.Awake();
            this.appDataPath = Path.Combine(Application.persistentDataPath, "media", "datasets");
        }

        public void TryGetRawDataset(string dataPath, out RawDataset dataset)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);
            rawDatasets.TryGetValue(dataPath, out dataset);
        }
        public void TryGetDataset(string dataPath, out Dataset dataset)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.Dataset);
            datasets.TryGetValue(dataPath, out dataset);
        }

        public void ImportRawDataset(string dataPath, RawDataset importing)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);
            // See what dataset this RawDataset is a part of
            string datasetPath = DataPath.GetDatasetPath(dataPath);

            // See if we have any data from that dataset yet
            Dataset dataset;
            TryGetDataset(datasetPath, out dataset);

            // If we don't, create the dataset
            if (dataset == null)
            {
                // TODO: import the dataset bounds and center from config
                dataset = new Dataset(datasetPath, new Bounds(Vector3.zero, 2.0f * Vector3.one), this.transform);
            }

            datasets[datasetPath] = dataset;
            rawDatasets[dataPath] = importing;

            ImportVariables(dataPath, importing, dataset);
            ImportKeyData(dataPath, importing, dataset);
        }

        public void LoadRawDatasetFromCache(string dataPath)
        {
            FileInfo jsonFile = GetRawDatasetMetadataFile(dataPath);
            if (!jsonFile.Exists)
            {
                Debug.LogErrorFormat("Data path {0} does not exist!", jsonFile.ToString());
                return;
            }
            else
            {
                Debug.Log("Loading " + dataPath + " from " + this.appDataPath);
            }

            string metadataContent = "";
            using (StreamReader file = new StreamReader(jsonFile.FullName))
            {
                metadataContent = file.ReadToEnd();
            }

            RawDataset.JsonHeader metadata = JsonUtility.FromJson<RawDataset.JsonHeader>(metadataContent);

            FileInfo binFile = GetRawDatasetBinaryFile(dataPath);
            byte[] dataBytes = File.ReadAllBytes(binFile.FullName);

            RawDataset.BinaryData data = new RawDataset.BinaryData(metadata, dataBytes);

            RawDataset ds = new RawDataset(metadata, data);
            ImportRawDataset(dataPath, ds);
        }

        public void CacheRawDataset(string dataPath, string json, byte[] data)
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
            string scalarVarRoot = DataPath.Join(datasetPath, DataPath.DataPathType.ScalarVar);
            foreach (var scalarArrayName in rawDataset.scalarArrayNames)
            {
                string scalarPath = DataPath.Join(scalarVarRoot, scalarArrayName);
                ScalarDataVariable scalarDataVariable;
                dataset.TryGetScalarVar(scalarPath, out scalarDataVariable);

                if (scalarDataVariable == null)
                {
                    // Create a new scalar variable
                    scalarDataVariable = new ScalarDataVariable(scalarPath);
                    scalarDataVariable.MinValue = rawDataset.GetScalarMin(scalarArrayName);
                    scalarDataVariable.MaxValue = rawDataset.GetScalarMax(scalarArrayName);
                    dataset.AddScalarVariable(scalarDataVariable);
                }
                else
                {
                    // If this variable was already there, adjust its bounds to
                    // include the newly imported rawDataset
                    scalarDataVariable.MinValue = Mathf.Min(scalarDataVariable.MinValue, rawDataset.GetScalarMin(scalarArrayName));
                    scalarDataVariable.MaxValue = Mathf.Max(scalarDataVariable.MaxValue, rawDataset.GetScalarMax(scalarArrayName));
                }
            }
        }

        // Build the key data associations
        private void ImportKeyData(string dataPath, RawDataset rawDataset, Dataset dataset)
        {
            // Infer the type of data from the topology
            Type dataType = KeyDataMapping.typeMap[rawDataset.meshTopology];

            // Use reflection to construct the object (should only match one)
            ConstructorInfo[] constructors = dataType.GetConstructors();

            // Construct the object with the data path argument
            string[] args = new string[] { dataPath };
            IKeyData keyData = constructors[0].Invoke(args) as IKeyData;

            Matrix4x4 dataScale = DataScaling.NormalizeDataScale(rawDataset);

            keyData.DataTransform = dataScale;

            dataset.AddKeyData(keyData);
        }
    }
}