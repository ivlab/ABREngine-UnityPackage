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
    /// This Scriptable Object controls the ABR configuration before startup. On
    /// engine startup, a copy is instantiated for use at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "ABRConfig", menuName = "ABR/ABR Configuration")]
    public class ABRConfig : ScriptableObject
    {
        [Header("Common Configuration Options (hover for more info)")]

        /// <summary>
        ///     Local path to look for datasets and visassets at. Default:
        ///     Application.persistentDataPath
        /// </summary>
        [Tooltip("Location on this computer where VisAssets and Datasets are located. Relative paths (e.g. `../media`) are also acceptable.")]
        public string mediaPath;

        /// <summary>
        ///     What server to connect to, if any. If provided, ABR will try to
        ///     register with the server immediately upon startup. Default: null
        /// </summary>
        [Tooltip("Full URL of the ABR server / visualization manager that this app should connect to. Leave blank for no server.")]
        public string serverUrl;

        /// <summary>
        /// Load a state from resources on ABREngine startup
        /// </summary>
        [Tooltip("Load a state from Resources or StreamingAssets folder on ABREngine startup. Example: `testState.json` Leave blank for no startup state.")]
        public string loadStateOnStart;

        /// <summary>
        ///     Default bounds for datasets when showing (in Unity world coordinates)
        /// </summary>
        [Tooltip("Unity world-space container to 'squish' all data into to avoid overflowing Unity coordinates")]
        public Bounds dataContainer;

        [Header("Styling Defaults")]

        /// <summary>
        ///     The default prefab to look for in any resources folder
        /// </summary>
        [Tooltip("Default shape/color for glyphs in the Glyph layer")]
        public GameObject defaultGlyph;

        [Tooltip("Default color for geometries that have not had a colormap applied yet")]
        public Color defaultColor;

        [Tooltip("Default color for NaN values")]
        public Color defaultNanColor;

        [Tooltip("Default texture for NaN values on surfaces")]
        public Texture2D defaultNanTexture;

        [Tooltip("Default line texture for NaN values on ribbons")]
        public Texture2D defaultNanLine;

        [Header("Network-Based VisAssets and Data Configuration")]

        /// <summary>
        ///     What server to obtain VisAssets from, if any. If none provided,
        ///     ABR will assume that everything is in Unity's persistentData
        ///     path. If server is provided and resource doesn't exist in
        ///     persistentData, it will be downloaded. Default: null
        /// </summary>
        [Tooltip("Server to load VisAssets from (e.g. `http://192.168.137.1:8000/media/visassets`")]
        public string visAssetServerUrl;

        /// <summary>
        ///     What server to obtain data from, if any. If none provided,
        ///     ABR will assume that everything is in Unity's persistentData
        ///     path. If server is provided and resource doesn't exist in
        ///     persistentData, it will be downloaded. Default: null
        /// </summary>
        [Tooltip("Server to load VisAssets from (e.g. `http://192.168.137.1:8001/api`")]
        public string dataServerUrl;

        /// <summary>
        ///     Port to listen for data on, if any. Useful if, for instance, you want to
        ///     have a live connection to ParaView that pushes data into ABR. Default:
        ///     null
        /// </summary>
        [Tooltip("Port to listen for data connections (e.g. from ParaView on). A port `0` is assumed to mean no connection.")]
        public int dataListenerPort;

        /// <summary>
        ///     The Json Schema to use for validation of ABR states
        /// </summary>
        public JSchema Schema { get; private set; } 
 
        /// <summary>
        ///     Schema to use for internally grabbing default values
        /// </summary>
        public JObject SchemaJson { get; private set; }

        /// <summary>
        ///     Actual URI-ified URL of the server that ABR should connect to
        /// </summary>
        public Uri ServerUrl { get => new Uri(serverUrl); }

        /// <summary>
        /// Where to find the Schema online
        /// </summary>
        private const string SchemaUrl = "https://raw.githubusercontent.com/ivlab/abr-schema/master/";
        private string schemaName = "ABRSchema_0-2-0.json";

        void Reset()
        {
            mediaPath = Application.persistentDataPath;
            serverUrl = "";
            loadStateOnStart = "";
            dataContainer = new Bounds(Vector3.zero, Vector3.one * 2.0f);

            defaultGlyph = Resources.Load<GameObject>("DefaultSphere");
            defaultGlyph.SetActive(false);
            defaultColor = Color.white;
            defaultNanColor = Color.yellow;
            defaultNanTexture = null;
            defaultNanLine = null;

            visAssetServerUrl = "";
            dataServerUrl = "";
            dataListenerPort = 0;
        }

        void Awake()
        {
            // Debug.Log("ABR Config Loaded");

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
            HttpResponseMessage resp = ABREngine.httpClient.GetAsync(SchemaUrl + schemaName).Result;
            string schemaContents = null;
            if (!resp.IsSuccessStatusCode)
            {
                Debug.LogErrorFormat("Unable to load schema from {0}, using backup schema {1}", SchemaUrl + schemaName, backupSchema);
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
                Debug.LogErrorFormat("Unable to parse schema `{0}`.", schemaName);
                return;
            }
            if (Schema.Valid ?? false)
            {
                Debug.LogErrorFormat("Schema `{0}` is invalid.", schemaName);
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

        public override string ToString()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            return JsonConvert.SerializeObject(this, Formatting.Indented, settings);
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


        /// <summary>
        /// Global access to constants in the ABR Engine
        /// </summary>
        public static class Consts
        {
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
    }
}