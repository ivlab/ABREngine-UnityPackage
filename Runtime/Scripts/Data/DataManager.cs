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
        private string dataPath;

        private Dictionary<string, Dataset> datasets = new Dictionary<string, Dataset>();

        void Start()
        {
            this.dataPath = Path.Combine(Application.persistentDataPath, "media", "datasets");
        }

        private FileInfo GetDatasetMetadataFile(string label)
        {
            return new System.IO.FileInfo(System.IO.Path.Combine(this.dataPath, label + ".json"));
        }
        private FileInfo GetDatasetBinaryFile(string label)
        {
            return new System.IO.FileInfo(System.IO.Path.Combine(this.dataPath, label + ".bin"));
        }

        public void ImportDataset(string label, Dataset dataset)
        {
            datasets[label] = dataset;
        }

        public void LoadDatasetFromCache(string label)
        {
            Debug.Log("Loading " + label + " from " + this.dataPath);

            FileInfo jsonFile = GetDatasetMetadataFile(label);
            string metadataContent = "";
            using (StreamReader file = new StreamReader(jsonFile.FullName))
            {
                metadataContent = file.ReadToEnd();
            }

            Dataset.JsonHeader metadata = JsonConvert.DeserializeObject<Dataset.JsonHeader>(metadataContent);

            FileInfo binFile = GetDatasetBinaryFile(label);
            byte[] dataBytes = File.ReadAllBytes(binFile.FullName);

            Dataset.BinaryData data = new Dataset.BinaryData(metadata, dataBytes);

            Dataset ds = new Dataset(metadata, data);
            ImportDataset(label, ds);
        }

        public void CacheData(string label, string json, byte[] data)
        {
            Debug.Log("Saving " + label + " to " + this.dataPath);

            FileInfo jsonFile = GetDatasetMetadataFile(label);

            if (!jsonFile.Directory.Exists)
            {
                Directory.CreateDirectory(jsonFile.DirectoryName);
            }

            using (StreamWriter file = new StreamWriter(jsonFile.FullName, false))
            {
                file.Write(json);
            }

            FileInfo binFile = new FileInfo(Path.Combine(this.dataPath, label + ".bin"));

            FileStream fs = File.Create(binFile.FullName);
            fs.Write(data, 0, data.Length);
            fs.Close();
        }
    }
}