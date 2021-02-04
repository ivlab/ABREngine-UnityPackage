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

        // Dictionary of DataPath -> raw datasets that contain the actual data (these are BIG)
        private Dictionary<string, RawDataset> datasets = new Dictionary<string, RawDataset>();

        // Dictionary of DataPath -> key data objects (paths will match those in
        // datasets dict)
        private Dictionary<string, IKeyData> keyDataObjects = new Dictionary<string, IKeyData>();

        // Dictionaries of DataPath -> variables that manage min/max values and
        // point to the above datasets
        private Dictionary<string, ScalarDataVariable> scalarVariables = new Dictionary<string, ScalarDataVariable>();
        private Dictionary<string, VectorDataVariable> vectorVariables = new Dictionary<string, VectorDataVariable>();

        protected override void Awake()
        {
            base.Awake();
            this.appDataPath = Path.Combine(Application.persistentDataPath, "media", "datasets");
        }

        public void TryGetRawDataset(string dataPath, out RawDataset dataset)
        {
            WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);
            datasets.TryGetValue(dataPath, out dataset);
        }
        public void TryGetScalarVar(string dataPath, out ScalarDataVariable scalarVar)
        {
            WarnOnDataPathFormat(dataPath, DataPath.DataPathType.ScalarVar);
            scalarVariables.TryGetValue(dataPath, out scalarVar);
        }
        public void TryGetVectorVar(string dataPath, out VectorDataVariable vectorVar)
        {
            WarnOnDataPathFormat(dataPath, DataPath.DataPathType.VectorVar);
            vectorVariables.TryGetValue(dataPath, out vectorVar);
        }
        public void TryGetKeyData(string dataPath, out IKeyData keyData)
        {
            WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);
            keyDataObjects.TryGetValue(dataPath, out keyData);
        }

        public void ImportDataset(string dataPath, RawDataset dataset)
        {
            WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);
            ImportVariables(dataPath, dataset);
            ImportKeyData(dataPath, dataset);

            datasets[dataPath] = dataset;
        }

        public void LoadDatasetFromCache(string dataPath)
        {

            FileInfo jsonFile = GetDatasetMetadataFile(dataPath);
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

            FileInfo binFile = GetDatasetBinaryFile(dataPath);
            byte[] dataBytes = File.ReadAllBytes(binFile.FullName);

            RawDataset.BinaryData data = new RawDataset.BinaryData(metadata, dataBytes);

            RawDataset ds = new RawDataset(metadata, data);
            ImportDataset(dataPath, ds);
        }

        public void CacheData(string dataPath, string json, byte[] data)
        {
            Debug.Log("Saving " + dataPath + " to " + this.appDataPath);

            FileInfo jsonFile = GetDatasetMetadataFile(dataPath);

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

        private FileInfo GetDatasetMetadataFile(string dataPath)
        {
            return new System.IO.FileInfo(System.IO.Path.Combine(this.appDataPath, dataPath + ".json"));
        }
        private FileInfo GetDatasetBinaryFile(string dataPath)
        {
            return new System.IO.FileInfo(System.IO.Path.Combine(this.appDataPath, dataPath + ".bin"));
        }

        // Import all the variables from a particular dataset with the path
        // `dataPath`
        //
        // This populates the dictionaries scalarVariables and vectorVariables
        private void ImportVariables(string dataPath, RawDataset dataset)
        {
            string datasetPath = DataPath.GetDatasetPath(dataPath);
            string scalarVarRoot = DataPath.Join(datasetPath, DataPath.DataPathType.ScalarVar);
            foreach (var scalarArrayName in dataset.scalarArrayNames)
            {
                string scalarPath = DataPath.Join(scalarVarRoot, scalarArrayName);
                ScalarDataVariable scalarDataVariable;
                TryGetScalarVar(scalarPath, out scalarDataVariable);

                if (scalarDataVariable == null)
                {
                    // Create a new scalar variable
                    scalarDataVariable = new ScalarDataVariable(scalarPath);
                    scalarVariables[scalarPath] = scalarDataVariable;
                    scalarDataVariable.MinValue = dataset.GetScalarMin(scalarArrayName);
                    scalarDataVariable.MaxValue = dataset.GetScalarMax(scalarArrayName);
                }
                else
                {
                    // If this variable was already there, adjust its bounds to
                    // include the newly imported dataset
                    scalarDataVariable.MinValue = Mathf.Min(scalarDataVariable.MinValue, dataset.GetScalarMin(scalarArrayName));
                    scalarDataVariable.MaxValue = Mathf.Max(scalarDataVariable.MaxValue, dataset.GetScalarMax(scalarArrayName));
                }
            }
        }

        // Build the key data associations
        private void ImportKeyData(string dataPath, RawDataset dataset)
        {
            // Infer the type of data from the topology
            Type dataType = KeyDataMapping.typeMap[dataset.meshTopology];

            // Use reflection to construct the object (should only match one)
            ConstructorInfo[] constructors = dataType.GetConstructors();

            // Construct the object with the data path argument
            string[] args = new string[] { dataPath };
            IKeyData keyData = constructors[0].Invoke(args) as IKeyData;

            Matrix4x4 dataScale = DataScaling.NormalizeDataScale(dataset);

            keyData.DataTransform = dataScale;

            keyDataObjects[dataPath] = keyData;
        }

        // Log a message if the data path doesn't follow convention
        private void WarnOnDataPathFormat(string dataPath, DataPath.DataPathType dataPathType)
        {
            if (!DataPath.FollowsConvention(dataPath, dataPathType))
            {
                Debug.LogWarningFormat(
                    "Label `{0}` does not follow data path convention and " +
                    "may not be imported correctly.\nUse {1} convention {2}",
                    dataPath,
                    dataPathType.ToString(),
                    DataPath.GetConvention(dataPathType));
            }
        }
    }
}