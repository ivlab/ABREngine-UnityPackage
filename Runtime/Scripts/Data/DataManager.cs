/* DataManager.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public class DataManager : Singleton<DataManager>
    {
        private string dataPath;

        void Start()
        {
            this.dataPath = Path.Combine(Application.persistentDataPath, "media", "datasets");
        }

        private Dictionary<string, Dataset> datasets = new Dictionary<string, Dataset>();

        public void ImportDataset(string label, ref Dataset dataset)
        {
            datasets[label] = dataset;
        }

        public void CacheData(string label, string json, byte[] data)
        {
            Debug.Log("Saving " + label + " to " + this.dataPath);

            System.IO.FileInfo jsonFile = new System.IO.FileInfo(System.IO.Path.Combine(this.dataPath, label + ".json"));

            if (!jsonFile.Directory.Exists)
            {
                System.IO.Directory.CreateDirectory(jsonFile.DirectoryName);
            }

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(jsonFile.FullName, false))
            {
                file.Write(json);
            }

            System.IO.FileInfo binFile = new System.IO.FileInfo(System.IO.Path.Combine(this.dataPath, label + ".bin"));

            System.IO.FileStream fs = System.IO.File.Create(binFile.FullName);
            fs.Write(data, 0, data.Length);
            fs.Close();
        }
    }
}