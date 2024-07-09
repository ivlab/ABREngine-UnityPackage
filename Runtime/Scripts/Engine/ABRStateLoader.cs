/* ABRStateLoader.cs
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

using System;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using IVLab.Utilities;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Generic state loader for ABR. Implementations should allow both
    /// retrieving a state (`GetState`) and saving a state (`SaveState`).
    /// </summary>
    public interface IABRStateLoader
    {
        /// <summary>
        /// Load a state based on some text (perhaps a JSON string, a file path, or URL)
        /// </summary>
        JObject GetState(string stateText);

        /// <summary>
        /// Save a serialized JSON state with a particular name
        /// </summary>
        void SaveState(string name, string serializedState);
    }

    /// <summary>
    /// Load a state from any Resources folder within Unity (within any Package or Asset)
    /// </summary>
    public class ResourceStateFileLoader : IABRStateLoader
    {
        public ResourceStateFileLoader() { }

        public JObject GetState(string fileName)
        {
            Debug.LogFormat("Loading state from resources: {0}", fileName);
            string name = Path.GetFileNameWithoutExtension(fileName);
            TextAsset textAsset = Resources.Load<TextAsset>(name);
            if (textAsset != null)
            {
                return JObject.Parse(textAsset.text);
            }
            else
            {
                return null;
            }
        }

        public void SaveState(string name, string serializedState)
        {
            throw new NotImplementedException("States cannot be saved to Resources folder");
        }
    }

    /// <summary>
    /// Save/Load a state from a web URL
    /// </summary>
    public class HttpStateFileLoader : IABRStateLoader
    {
        public HttpStateFileLoader() { }

        public JObject GetState(string url)
        {
            HttpResponseMessage stateResponse = ABREngine.httpClient.GetAsync(url).Result;
            stateResponse.EnsureSuccessStatusCode();
            string fullStateJson = stateResponse.Content.ReadAsStringAsync().Result;
            return JObject.Parse(fullStateJson)["state"].ToObject<JObject>();
        }

        public void SaveState(string name, string serializedState)
        {
            string stateUrl = ABREngine.Instance.Config.serverUrl.ToString();
            ByteArrayContent content = new ByteArrayContent(Encoding.UTF8.GetBytes(serializedState));
            HttpResponseMessage stateResponse = ABREngine.httpClient.PutAsync(stateUrl, content).Result;
            stateResponse.EnsureSuccessStatusCode();
        }
    }

    /// <summary>
    /// Load a state from a serialized JSON string
    /// </summary>
    public class TextStateFileLoader : IABRStateLoader
    {
        public TextStateFileLoader() { }

        public JObject GetState(string jsonText)
        {
            return JObject.Parse(jsonText);
        }

        public void SaveState(string name, string serializedState)
        {
            throw new NotImplementedException("States cannot be saved to text");
        }
    }

    /// <summary>
    /// Save/Load a state to a JSON file somewhere on disk
    /// </summary>
    public class PathStateFileLoader : IABRStateLoader
    {
        public PathStateFileLoader() { }

        public JObject GetState(string stateFilePath)
        {
            using (StreamReader reader = new StreamReader(stateFilePath))
            {
                string stateText = reader.ReadToEnd();
                return JObject.Parse(stateText);
            }
        }

        public void SaveState(string outPath, string serializedState)
        {
            using (StreamWriter writer = new StreamWriter(outPath))
            {
                writer.Write(serializedState);
            }
        }
    }
}