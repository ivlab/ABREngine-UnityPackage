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

        public bool TryGetRawDataset(string dataPath, out RawDataset dataset)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);
            return rawDatasets.TryGetValue(dataPath, out dataset);
        }
        public bool TryGetDataset(string dataPath, out Dataset dataset)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.Dataset);
            return datasets.TryGetValue(dataPath, out dataset);
        }
        public List<Dataset> GetDatasets()
        {
            return datasets.Values.ToList();
        }

        public async Task LoadRawDataset<T>(string dataPath)
        where T : IDataLoader, new()
        {
            RawDataset ds = await (new T()).TryLoadDataAsync(dataPath);
            await ImportRawDataset(dataPath, ds);
        }

        public async Task ImportRawDataset(string dataPath, RawDataset importing)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);
            // See what dataset this RawDataset is a part of
            string datasetPath = DataPath.GetDatasetPath(dataPath);

            // See if we have any data from that dataset yet
            // Needs to be run in main thread because of this.transform
            await UnityThreadScheduler.Instance.RunMainThreadWork(() => {
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
            });
        }

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
            Type dataType = KeyDataMapping.typeMap[rawDataset.meshTopology];

            // Use reflection to construct the object (should only match one)
            ConstructorInfo[] constructors = dataType.GetConstructors();

            // Construct the object with the data path argument
            string[] args = new string[] { dataPath };
            IKeyData keyData = constructors[0].Invoke(args) as IKeyData;

            dataset.AddKeyData(keyData);
        }
    }
}