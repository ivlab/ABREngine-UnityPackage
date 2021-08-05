/* ABRConfig.cs
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
using System.Net.Http;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace IVLab.ABREngine
{
    public class ABRConfig
    {
        /// <summary>
        ///     Look for a file of this name in any Resources folder and load it
        ///     as the config
        /// </summary>
        public const string CONFIG_FILE = "ABRConfig";

        /// <summary>
        ///     Fall back to the defaults located in this package
        /// </summary>
        public const string CONFIG_FILE_FALLBACK = "ABRConfigDefault";

        /// <summary>
        /// Where to find the Schema online
        /// </summary>
        public const string SCHEMA_URL = "https://raw.githubusercontent.com/ivlab/abr-schema/master/ABRSchema_0-2-0.json";

        public ABRConfigDefaults Defaults { get; private set; }

        /// <summary>
        ///     The Json Schema to use for validation of ABR states
        /// </summary>
        public JSchema Schema { get; private set; } 
 
        /// <summary>
        ///     Miscellaneous info about the currently-running version of ABR
        /// </summary>
        public ABRConfigInfo Info { get; private set; }

        /// <summary>
        ///     Schema to use for internally grabbing default values
        /// </summary>
        private JObject _schema;

        public ABRConfig()
        {
            TextAsset configContents = Resources.Load<TextAsset>(CONFIG_FILE_FALLBACK);
            TextAsset configCustomizations = Resources.Load<TextAsset>(CONFIG_FILE);

            Info = JsonConvert.DeserializeObject<ABRConfigInfo>(configContents.text);
            ABRConfigInfo customizations = JsonConvert.DeserializeObject<ABRConfigInfo>(configCustomizations?.text ?? "");

            // Dynamically load any customizations if they're provided
            var assembly = Assembly.GetExecutingAssembly();
            Type configInfoType = typeof(ABRConfigInfo);
            FieldInfo[] allFields = configInfoType.GetFields();
            foreach (FieldInfo fieldInfo in allFields)
            {
                object customizedValue = fieldInfo.GetValue(customizations);
                if (customizedValue != null)
                {
                    fieldInfo.SetValue(Info, customizedValue);
                }
            }

            Debug.Log("ABR Config Loaded");

            // Load the default prefab
            GameObject defaultPrefab = GameObject.Instantiate(Resources.Load<GameObject>(Info.defaultPrefabName));
            defaultPrefab.SetActive(false);

            // Load the default bounds
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

            Defaults = new ABRConfigDefaults() {
                defaultPrefab = defaultPrefab
            };

            // Load the schema
            HttpResponseMessage resp = ABREngine.httpClient.GetAsync(SCHEMA_URL).Result;
            if (!resp.IsSuccessStatusCode)
            {
                Debug.LogErrorFormat("Unable to load schema from {0}", SCHEMA_URL);
                return;
            }
            string schemaContents = (resp.Content.ReadAsStringAsync().Result);
            Schema = JSchema.Parse(schemaContents);
            if (Schema == null)
            {
                Debug.LogErrorFormat("Unable to parse schema `{0}`.", Info.schemaName);
                return;
            }
            if (Schema.Valid ?? false)
            {
                Debug.LogErrorFormat("Schema `{0}` is invalid.", Info.schemaName);
                return;
            }

            _schema = JObject.Parse(schemaContents);
            Debug.LogFormat("Using ABR Schema, version {0}", _schema["properties"]["version"]["default"]);
        }

        /// <summary>
        ///     Get the default primitive value for a particular data
        ///     impression's parameter
        /// </summary>
        public T GetInputValueDefault<T>(string plateName, string inputName)
        where T : IPrimitive
        {
            if (_schema == null)
            {
                Debug.LogErrorFormat("Schema is null, cannot get default value {0}", inputName);
                return default(T);
            }
            string primitiveValue = _schema["definitions"]["Plates"][plateName]["properties"][inputName]["properties"]["inputValue"]["default"].ToString();
            
            Type inputType = typeof(T);
            ConstructorInfo inputCtor =
                inputType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    CallingConventions.HasThis,
                    new Type[] { typeof(string) },
                    null
            );
            string[] args = new string[] { primitiveValue };
            try
            {
                T primitive = (T) inputCtor?.Invoke(args);
                return primitive;
            }
            catch (Exception)
            {
                Debug.LogErrorFormat("Unable to create primitive {0} using value `{1}`, using default value", inputType.ToString(), primitiveValue);
                return default(T);
            }
        }

        /// <summary>
        ///     Obtain a full list of all inputs available to this plate
        /// </summary>
        public string[] GetInputNames(string plateName)
        {
            if (_schema == null)
            {
                Debug.LogErrorFormat("Schema is null, cannot get input names for {0}", plateName);
                return new string[0];
            }
            Dictionary<string, JToken> inputList = _schema["definitions"]["Plates"][plateName]["properties"].ToObject<Dictionary<string, JToken>>();
            return inputList.Keys.ToArray();
        }

    }

    public class ABRConfigInfo
    {
        /// <summary>
        ///     Version of ABR
        /// </summary>
        public string version;

        /// <summary>
        ///     The name of the default prefab to look for in any resources folder
        /// </summary>
        public string defaultPrefabName;

        /// <summary>
        ///     The schema that should be loaded
        /// </summary>
        public string schemaName;

        /// <summary>
        ///     Default bounds for datasets when showing (in Unity world coordinates)
        /// </summary>
        public Bounds? defaultBounds;

        /// <summary>
        ///     What server to connect to, if any. If provided, ABR will try to
        ///     register with the server immediately upon startup. Default: null
        /// </summary>
        public Uri serverAddress;

        /// <summary>
        ///     State url to fetch on the server; will be concatenated with
        ///     serverAddress. Note: Do not include a leading slash!
        /// </summary>
        public string statePathOnServer;

        /// <summary>
        ///     What server to obtain VisAssets from, if any. If none provided,
        ///     ABR will assume that everything is in Unity's persistentData
        ///     path. If server is provided and resource doesn't exist in
        ///     persistentData, it will be downloaded. Default: null
        /// </summary>
        public string visAssetServer;

        /// <summary>
        ///     Load any visassets located in Resources/media/visassets
        /// </summary>
        public bool loadResourceVisAssets;

        /// <summary>
        ///     What server to obtain data from, if any. If none provided,
        ///     ABR will assume that everything is in Unity's persistentData
        ///     path. If server is provided and resource doesn't exist in
        ///     persistentData, it will be downloaded. Default: null
        /// </summary>
        public string dataServer;

        /// <summary>
        ///     Port to listen for data on, if any. Useful if, for instance, you want to
        ///     have a live connection to ParaView that pushes data into ABR. Default:
        ///     null
        /// </summary>
        public int? dataListenerPort;

        /// <summary>
        ///     Local path to look for datasets and visassets at. Default:
        ///     Application.persistentDataPath
        /// </summary>
        public string mediaPath;

        public override string ToString()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            return JsonConvert.SerializeObject(this, Formatting.Indented, settings);
        }
    }

    public class ABRConfigDefaults
    {
        /// <summary>
        ///     Prefab to use for defaults in each data impression
        /// </summary>
        public GameObject defaultPrefab;
    }
}