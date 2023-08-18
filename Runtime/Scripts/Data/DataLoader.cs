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
        RawDataset LoadData(string dataPath);
    }

    /// <summary>
    /// Load data from the ABREngine Media directory (specified in ABRConfig)
    /// </summary>
    public class MediaDataLoader : IDataLoader
    {
        public RawDataset LoadData(string dataPath)
        {
            string mediaDir = Path.GetFullPath(ABREngine.Instance.MediaPath);
            FileInfo jsonFile = new FileInfo(Path.Combine(mediaDir, ABRConfig.Consts.DatasetFolder, dataPath) + ".json");
            if (!jsonFile.Exists)
            {
                return null;
            }

            string metadataContent = "";
            using (StreamReader file = new StreamReader(jsonFile.FullName))
            {
                metadataContent = file.ReadToEnd();
            }

            RawDataset.JsonHeader metadata = JsonUtility.FromJson<RawDataset.JsonHeader>(metadataContent);

            FileInfo binFile = new FileInfo(Path.Combine(mediaDir, ABRConfig.Consts.DatasetFolder, dataPath) + ".bin");
            byte[] dataBytes = File.ReadAllBytes(binFile.FullName);

            RawDataset.BinaryData data = new RawDataset.BinaryData(metadata, dataBytes);

            return new RawDataset(metadata, data);
        }
    }

    /// <summary>
    /// Load data from a remote source
    /// </summary>
    public class HttpDataLoader : IDataLoader
    {
        public RawDataset LoadData(string dataPath)
        {
            string url = ABREngine.Instance.Config.dataServerUrl;
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);

            HttpResponseMessage metadataResponse = ABREngine.httpClient.GetAsync(url + "/metadata/" + dataPath).Result;
            metadataResponse.EnsureSuccessStatusCode();
            string responseBody = metadataResponse.Content.ReadAsStringAsync().Result;

            JToken metadataJson = JObject.Parse(responseBody)["metadata"];
            RawDataset.JsonHeader metadata = metadataJson.ToObject<RawDataset.JsonHeader>();

            HttpResponseMessage dataResponse = ABREngine.httpClient.GetAsync(url + "/data/" + dataPath).Result;
            metadataResponse.EnsureSuccessStatusCode();
            byte[] dataBytes = dataResponse.Content.ReadAsByteArrayAsync().Result;

            RawDataset.BinaryData data = new RawDataset.BinaryData(metadata, dataBytes);
            return new RawDataset(metadata, data);
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
        public RawDataset LoadData(string dataPath)
        {
            DataPath.WarnOnDataPathFormat(dataPath, DataPath.DataPathType.KeyData);

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
        }
    }
}
