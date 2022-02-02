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
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace IVLab.ABREngine
{
    /// <summary>
    /// The ABRConfig class provides access throughtout ABR to everything that
    /// is loaded in from a user's / developer's ABRConfig.json file. This is
    /// mainly useful for modifying the behaviour of the ABREngine internally,
    /// but can be occasionally helpful in other situations - for instance, if a
    /// developer needs to get access to the default bounding box / container
    /// that ABR is using.
    /// </summary>
    /// <example>
    /// <code>
    /// Bounds defaultBounds = ABREngine.Instance.Config.Info.defaultBounds.Value;
    /// // ... do something fancy based on `defaultBounds`
    /// </code>
    /// </example>
    public class ABRConfig
    {
        /// <summary>
        /// Global access to constants in the ABR Engine
        /// </summary>
        public static class Consts
        {
            /// <summary>
            ///     User configuration file to be placed in the StreamingAssets
            ///     folder (editor) or Data folder (build)
            /// </summary>
            public const string ConfigFile = "ABRConfig.json";

            /// <summary>
            ///     Fall back to the defaults located in this package (Located in ABREngine Resources folder)
            /// </summary>
            public const string ConfigFileFallback = "ABRConfigDefault";

            /// <summary>
            /// Where to find the Schema online
            /// </summary>
            public const string SchemaUrl = "https://raw.githubusercontent.com/ivlab/abr-schema/master/ABRSchema_0-2-0.json";

            /// <summary>
            /// VisAsset folder within media folder
            /// </summary>
            public const string VisAssetFolder = "visassets";

            /// <summary>
            /// Dataset folder within media folder
            /// </summary>
            public const string DatasetFolder = "datasets";

            /// <summary>
            /// Default name for the media folder
            /// </summary>
            public const string MediaFolder = "media";

            /// <summary>
            /// Name of VisAsset JSON specifier
            /// </summary>
            public const string VisAssetJson = "artifact.json";
        }

        public ABRConfigDefaults Defaults { get; private set; }

        /// <summary>
        /// The actual path that ABRConfig.json is located at, if any
        /// </summary>
        public string ABRConfigFile
        {
            get
            {
                string configFolder = Application.streamingAssetsPath;
                string configUserFile = Path.Combine(configFolder, ABRConfig.Consts.ConfigFile);
                if (File.Exists(configUserFile))
                {
                    return configUserFile;
                }
                else
                {
                    return null;
                }
            }
        }

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
        public JObject SchemaJson { get; private set; }

        public ABRConfig()
        {
            // Load defaults from ABREngine Resources
            TextAsset configContents = Resources.Load<TextAsset>(ABRConfig.Consts.ConfigFileFallback);
            Info = JsonConvert.DeserializeObject<ABRConfigInfo>(configContents.text);

            // Load any customizations the user has made
            ABRConfigInfo customizations = new ABRConfigInfo();
            if (ABRConfigFile != null)
            {
                using (StreamReader reader = new StreamReader(ABRConfigFile))
                {
                    customizations = JsonConvert.DeserializeObject<ABRConfigInfo>(reader.ReadToEnd());
                }
            }

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

            // Check for a backed up schema
            string backupSchemaDir = Path.Combine(Application.streamingAssetsPath, "schemas");
            string backupSchema = null;
            try
            {
                List<string> schemas = Directory.GetFiles(backupSchemaDir).Where(f => f.EndsWith(".json")).ToList();
                schemas.Sort();
                schemas.Reverse();
                backupSchema = schemas[0];
            }
            catch
            {
                Debug.LogErrorFormat("Unable to find a backup schema in {0}", backupSchemaDir);
            }

            if (backupSchema != null)
            {
                backupSchema = Path.Combine(backupSchemaDir, backupSchema);
            }

            // Load the schema
            HttpResponseMessage resp = ABREngine.httpClient.GetAsync(ABRConfig.Consts.SchemaUrl).Result;
            string schemaContents = null;
            if (!resp.IsSuccessStatusCode)
            {
                Debug.LogErrorFormat("Unable to load schema from {0}, using backup schema {1}", ABRConfig.Consts.SchemaUrl, backupSchema);
                using (StreamReader reader = new StreamReader(backupSchema))
                {
                    schemaContents = reader.ReadToEnd();
                }
            }
            else
            {
                schemaContents = (resp.Content.ReadAsStringAsync().Result);
            }

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
            SchemaJson = JObject.Parse(schemaContents);

            // Save a copy if needed
            bool needBackup = true;
            if (backupSchema != null) {
                using (StreamReader reader = new StreamReader(backupSchema))
                {
                    string bakContents = reader.ReadToEnd();
                    if (bakContents == schemaContents)
                    {
                        needBackup = false;
                    }
                }
            }
            if (needBackup)
            {
                string schemaName = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Replace(":", "_") + ".json";
                if (!Directory.Exists(backupSchemaDir))
                {
                    Directory.CreateDirectory(backupSchemaDir);
                }
                string schemaBakPath = Path.Combine(backupSchemaDir, schemaName);
                using (StreamWriter writer = new StreamWriter(schemaBakPath))
                {
                    writer.Write(schemaContents);
                }
                Debug.Log("Saved backup schema to " + schemaBakPath);
            }


            Debug.LogFormat("Using ABR Schema, version {0}", SchemaJson["properties"]["version"]["default"]);
        }

        /// <summary>
        ///     Get the default primitive value for a particular data
        ///     impression's parameter
        /// </summary>
        public T GetInputValueDefault<T>(string plateName, string inputName)
        where T : IPrimitive
        {
            if (SchemaJson == null)
            {
                Debug.LogErrorFormat("Schema is null, cannot get default value {0}", inputName);
                return default(T);
            }
            string primitiveValue = SchemaJson["definitions"]["Plates"][plateName]["properties"][inputName]["properties"]["inputValue"]["default"].ToString();
            
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
            if (SchemaJson == null)
            {
                Debug.LogErrorFormat("Schema is null, cannot get input names for {0}", plateName);
                return new string[0];
            }
            Dictionary<string, JToken> inputList = SchemaJson["definitions"]["Plates"][plateName]["properties"].ToObject<Dictionary<string, JToken>>();
            return inputList.Keys.ToArray();
        }

    }

    /// <summary>
    /// These options, when declared in an `ABRConfig.json` file, change the
    /// behaviour of the ABREngine. JSON naming convention is analagous to field
    /// names seen here.
    /// </summary>
    /// <example>
    /// This example shows a simple `ABRConfig.json` file which will be
    /// converted to an `ABRConfigInfo` object when the ABREngine is started.
    /// <code>
    /// {
    ///     "serverAddress": "http://localhost:8000",
    ///     "statePathOnServer": "api/state",
    ///     "mediaPath": "./media",
    ///     "dataListenerPort": 1900
    /// }
    /// </code>
    /// When the ABREngine is running, we would be able to access the config:
    /// <code>
    /// void Start()
    /// {
    ///     Debug.Log(ABREngine.Instance.Config.Info.serverAddress);
    ///     // prints "http://localhost:8000"
    /// }
    /// </code>
    /// </example>
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

        /// <summary>
        /// Load a state from resources on ABREngine startup
        /// </summary>
        public string loadStateOnStart;


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