/* ABRConfig.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
 *
 */

using UnityEngine;
using Newtonsoft.Json;
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

            Defaults = new ABRConfigDefaults() {
                defaultPrefab = defaultPrefab
            };

            // Load the schema
            TextAsset schemaContents = Resources.Load<TextAsset>(Info.schemaName);
            Schema = JSchema.Parse(schemaContents.text);
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
    }

    public class ABRConfigDefaults
    {
        /// <summary>
        ///     Prefab to use for defaults in each data impression
        /// </summary>
        public GameObject defaultPrefab;
    }
}