/* DataLoader.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
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

using UnityEngine;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;

using Newtonsoft.Json.Linq;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Generic interface to fetch a `RawDataset` from somewhere
    /// </summary>
    public interface IDataLoader
    {
        /// <summary>
        /// Load data specified by `dataPath` from a particular source
        /// </summary>
        Task<RawDataset> TryLoadDataAsync(string dataPath);
    }

    /// <summary>
    /// Load data from the ABREngine Media directory (specified in ABRConfig)
    /// </summary>
    public class MediaDataLoader : IDataLoader
    {
        public async Task<RawDataset> TryLoadDataAsync(string dataPath)
        {
            string mediaDir = Path.GetFullPath(ABREngine.Instance.MediaPath);
            FileInfo jsonFile = new FileInfo(Path.Combine(mediaDir, ABRConfig.Consts.DatasetFolder, dataPath) + ".json");
            if (!jsonFile.Exists)
            {
                Debug.LogErrorFormat("Data path {0} does not exist!", jsonFile.ToString());
                return null;
            }
            else
            {
                Debug.Log("Loading " + dataPath + " from " + mediaDir);
            }

            string metadataContent = "";
            using (StreamReader file = new StreamReader(jsonFile.FullName))
            {
                metadataContent = await file.ReadToEndAsync();
            }

            RawDataset.JsonHeader metadata = JsonUtility.FromJson<RawDataset.JsonHeader>(metadataContent);

            FileInfo binFile = new FileInfo(Path.Combine(mediaDir, ABRConfig.Consts.DatasetFolder, dataPath) + ".bin");
            // File.ReadAllBytesAsync doesn't exist in this version (2.0 Standard)
            // of .NET apparently?
            byte[] dataBytes = await Task.Run(() => File.ReadAllBytes(binFile.FullName));

            RawDataset.BinaryData data = new RawDataset.BinaryData(metadata, dataBytes);

            return new RawDataset(metadata, data);
        }
    }

    /// <summary>
    /// Load data from a remote source
    /// </summary>
    public class HttpDataLoader : IDataLoader
    {
        public async Task<RawDataset> TryLoadDataAsync(string dataPath)
        {
            string url = ABREngine.Instance.Config.Info.dataServer;
            Debug.Log("Loading " + dataPath + " from " + url);
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);

            try
            {
                HttpResponseMessage metadataResponse = await ABREngine.httpClient.GetAsync(url + "/metadata/" + dataPath);
                metadataResponse.EnsureSuccessStatusCode();
                string responseBody = await metadataResponse.Content.ReadAsStringAsync();

                JToken metadataJson = JObject.Parse(responseBody)["metadata"];
                RawDataset.JsonHeader metadata = metadataJson.ToObject<RawDataset.JsonHeader>();

                HttpResponseMessage dataResponse = await ABREngine.httpClient.GetAsync(url + "/data/" + dataPath);
                metadataResponse.EnsureSuccessStatusCode();
                byte[] dataBytes = await dataResponse.Content.ReadAsByteArrayAsync();

                RawDataset.BinaryData data = new RawDataset.BinaryData(metadata, dataBytes);
                return new RawDataset(metadata, data);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            return null;
        }
    }

    /// <summary>
    /// Load data from resources folder. NOTE: The actual data files (.bin) must
    /// have their file extension changed to .txt in order to be recognized.
    /// When data are imported, the identity of each is lost so we must guess
    /// which is which - currently guessing the larger of the two files is the
    /// "Data" and the smaller is "Metadata".
    /// </summary>
    public class ResourcesDataLoader : IDataLoader
    {
        public async Task<RawDataset> TryLoadDataAsync(string dataPath)
        {
            Debug.Log("Loading " + dataPath + " from Resources");
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);

            try
            {
                return await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
                {

                    // Fetch both files from resources
                    TextAsset[] metadataData = Resources.LoadAll<TextAsset>("media/datasets/" + dataPath);
                    if (metadataData == null || metadataData.Length != 2)
                    {
                        throw new Exception($"{dataPath} does not exist in Resources or is corrupted");
                    }

                    string metadataJson = metadataData[0].bytes.Length < metadataData[1].bytes.Length ? metadataData[0].text : metadataData[1].text;
                    JObject metadata = JObject.Parse(metadataJson);
                    RawDataset.JsonHeader meta = metadata.ToObject<RawDataset.JsonHeader>();

                    byte[] dataBytes = metadataData[0].bytes.Length < metadataData[1].bytes.Length ? metadataData[1].bytes : metadataData[0].bytes;
                    RawDataset.BinaryData data = new RawDataset.BinaryData(meta, dataBytes);
                    Resources.UnloadAsset(metadataData[0]);
                    Resources.UnloadAsset(metadataData[1]);
                    
                    return new RawDataset(meta, data);
                });
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            return null;
        }
    }
}
