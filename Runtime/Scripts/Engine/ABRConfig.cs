/* ABRConfig.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
 *
 */

using System;
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
            TextAsset configContents = Resources.Load<TextAsset>(CONFIG_FILE);
            if (configContents == null)
            {
                // Config not found, revert to default
                configContents = Resources.Load<TextAsset>(CONFIG_FILE_FALLBACK);
            }

            Info = JsonConvert.DeserializeObject<ABRConfigInfo>(configContents.text);
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
            TextAsset schemaContents = Resources.Load<TextAsset>(Info.schemaName);
            Schema = JSchema.Parse(schemaContents.text);
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

            _schema = JObject.Parse(schemaContents.text);
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
        public Bounds defaultBounds;
    }

    public class ABRConfigDefaults
    {
        /// <summary>
        ///     Prefab to use for defaults in each data impression
        /// </summary>
        public GameObject defaultPrefab;
    }
}