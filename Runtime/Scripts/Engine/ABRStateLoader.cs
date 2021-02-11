/* ABRStateLoader.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
 *
 */

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
        Task<JToken> GetState(string name);
    }

    public class ResourceStateFileLoader : IABRStateLoader
    {
        public ResourceStateFileLoader() { }

        public async Task<JToken> GetState(string fileName)
        {
            TextAsset textAsset = null;
            await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
            {
                string name = Path.GetFileNameWithoutExtension(fileName);
                textAsset = Resources.Load<TextAsset>(name);
            });
            return JObject.Parse(textAsset?.text);
        }
    }

    public class HttpStateFileLoader : IABRStateLoader
    {
        public HttpStateFileLoader() { }

        public async Task<JToken> GetState(string url)
        {
            HttpResponseMessage stateResponse = await ABREngine.httpClient.GetAsync(url);
            stateResponse.EnsureSuccessStatusCode();
            string fullStateJson = await stateResponse.Content.ReadAsStringAsync();
            return JObject.Parse(fullStateJson)["state"];
        }
    }
}