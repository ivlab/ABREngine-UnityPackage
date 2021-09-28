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
    public interface IABRStateLoader
    {
        Task<JObject> GetState(string stateText);

        Task SaveState(string name, string serializedState);
    }

    public class ResourceStateFileLoader : IABRStateLoader
    {
        public ResourceStateFileLoader() { }

        public async Task<JObject> GetState(string fileName)
        {
            Debug.LogFormat("Loading state from resources: {0}", fileName);
            TextAsset textAsset = null;
            await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
            {
                string name = Path.GetFileNameWithoutExtension(fileName);
                textAsset = Resources.Load<TextAsset>(name);
            });
            return JObject.Parse(textAsset?.text);
        }

        public async Task SaveState(string name, string serializedState)
        {
            throw new NotImplementedException("States cannot be saved to Resources folder");
        }
    }

    public class HttpStateFileLoader : IABRStateLoader
    {
        public HttpStateFileLoader() { }

        public async Task<JObject> GetState(string url)
        {
            HttpResponseMessage stateResponse = await ABREngine.httpClient.GetAsync(url);
            stateResponse.EnsureSuccessStatusCode();
            string fullStateJson = await stateResponse.Content.ReadAsStringAsync();
            return JObject.Parse(fullStateJson)["state"].ToObject<JObject>();
        }

        public async Task SaveState(string name, string serializedState)
        {
            string stateUrl = ABREngine.Instance.Config.Info.serverAddress + ABREngine.Instance.Config.Info.statePathOnServer;
            ByteArrayContent content = new ByteArrayContent(Encoding.UTF8.GetBytes(serializedState));
            HttpResponseMessage stateResponse = await ABREngine.httpClient.PutAsync(stateUrl, content);
            stateResponse.EnsureSuccessStatusCode();
        }
    }

    public class TextStateFileLoader : IABRStateLoader
    {
        public TextStateFileLoader() { }

        public async Task<JObject> GetState(string jsonText)
        {
            return await Task.Run(() => JObject.Parse(jsonText));
        }

        public async Task SaveState(string name, string serializedState)
        {
            throw new NotImplementedException("States cannot be saved to text");
        }
    }

    public class PathStateFileLoader : IABRStateLoader
    {
        public PathStateFileLoader() { }

        public async Task<JObject> GetState(string stateFilePath)
        {
            using (StreamReader reader = new StreamReader(stateFilePath))
            {
                string stateText = await reader.ReadToEndAsync();
                return JObject.Parse(stateText);
            }
        }

        public async Task SaveState(string outPath, string serializedState)
        {
            using (StreamWriter writer = new StreamWriter(outPath))
            {
                await writer.WriteAsync(serializedState);
            }
        }
    }
}