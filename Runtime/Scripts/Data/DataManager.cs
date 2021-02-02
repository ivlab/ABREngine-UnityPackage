/* DataManager.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using System.IO;
using System.Collections.Generic;
using UnityEngine;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public class DataManager : Singleton<DataManager>
    {
        private string appDataPath;

        private Dictionary<string, Dataset> datasets = new Dictionary<string, Dataset>();

        void Start()
        {
            this.appDataPath = Path.Combine(Application.persistentDataPath, "media", "datasets");
        }

        public Dataset GetDataset(string dataPath)
        {
            return datasets[dataPath];
        }

        public void ImportDataset(string dataPath, Dataset dataset)
        {
            if (!DataPath.FollowsConvention(dataPath, DataPath.DataPathType.KeyData))
            {
                Debug.LogWarningFormat(
                    "Label `{0}` does not follow data path convention and" +
                    "may not be imported correctly.\nUse Convention {1}", dataPath, DataPath.GetConvention());
            }
            datasets[dataPath] = dataset;
        }

        public void LoadDatasetFromCache(string dataPath)
        {
            Debug.Log("Loading " + dataPath + " from " + this.appDataPath);

            FileInfo jsonFile = GetDatasetMetadataFile(dataPath);
            string metadataContent = "";
            using (StreamReader file = new StreamReader(jsonFile.FullName))
            {
                metadataContent = file.ReadToEnd();
            }

            Dataset.JsonHeader metadata = JsonConvert.DeserializeObject<Dataset.JsonHeader>(metadataContent);

            FileInfo binFile = GetDatasetBinaryFile(dataPath);
            byte[] dataBytes = File.ReadAllBytes(binFile.FullName);

            Dataset.BinaryData data = new Dataset.BinaryData(metadata, dataBytes);

            Dataset ds = new Dataset(metadata, data);
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
    }
}