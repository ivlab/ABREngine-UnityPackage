/* ABRConfig.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
 *
 */

using UnityEngine;
using Newtonsoft.Json;

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

        public ABRConfig()
        {
            TextAsset configContents = Resources.Load<TextAsset>(CONFIG_FILE);
            if (configContents == null)
            {
                // Config not found, revert to default
                configContents = Resources.Load<TextAsset>(CONFIG_FILE_FALLBACK);
            }

            ABRConfigInfo info = JsonConvert.DeserializeObject<ABRConfigInfo>(configContents.text);
            Debug.Log("ABR Config Loaded");

            // Load the default prefab
            GameObject defaultPrefab = GameObject.Instantiate(Resources.Load<GameObject>(info.defaultPrefabName));
            defaultPrefab.SetActive(false);

            Defaults = new ABRConfigDefaults() {
                defaultPrefab = defaultPrefab
            };
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