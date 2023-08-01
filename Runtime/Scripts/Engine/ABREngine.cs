/* ABREngine.cs
 *
 * Copyright (c) 2022 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
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

using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using IVLab.Utilities;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace IVLab.ABREngine
{
    /// <summary>
    /// The ABREngine class is the main operational MonoBehaviour Singleton for
    /// the ABREngine-UnityPackage. It is in charge of kicking off all startup
    /// processes for ABR, including setting up connections with the server, the
    /// data listener, VisAssets and Data managers, etc.
    /// </summary>
    /// <example>
    /// Most methods of the ABREngine can be accessed through its singleton
    /// `Instance` without needing to do a `GetComponent`:
    /// <code>
    /// string mediaPath = ABREngine.Instance.MediaPath;
    /// </code>
    /// </example>
    /// <example>
    /// This example shows how to quickly get up and running with a
    /// custom-defined dataset and building your own data impressions. The
    /// general process for making a visualization programmatically with ABR is:
    /// <ol>
    ///     <li>Define your data in some `List`s.</li>
    ///     <li>Use the <see cref="RawDatasetAdapter"/> to convert the `List` into an ABR <see cref="RawDataset"/>, or import an existing ABR RawDataset using <see cref="DataManager.LoadRawDataset"/>.</li>
    ///     <li>Import that <see cref="RawDataset"/> into ABR using <see cref="DataManager.ImportRawDataset"/> (optionally, giving the dataset a data path identifier for easier semantic access later).</li>
    ///     <li>Optionally, import any <see cref="VisAsset"/>s you want to use using <see cref="VisAssetManager.GetVisAsset"/>.</li>
    ///     <li>Create a <see cref="DataImpression"/> to combine the data and visuals together.</li>
    ///     <li>Use <see cref="ABREngine.RegisterDataImpression"/> to add the impression to the engine.</li>
    ///     <li>Render the data and visuals to the screen using <see cref="ABREngine.Render"/>.</li>
    /// </ol>
    /// <code>
    /// using System;
    /// using System.Threading.Tasks;
    /// using UnityEngine;
    /// using IVLab.ABREngine;
    /// using IVLab.Utilities;
    ///
    /// public class SimpleABRExample : MonoBehaviour
    /// {
    ///     void Start()
    ///     {
    ///         // STEP 1: Define data
    ///         // 9 points in 3D space
    ///         List&lt;Vector3&gt; vertices = new List&lt;Vector3&gt;
    ///         {
    ///             new Vector3(0.0f, 0.5f, 0.0f),
    ///             new Vector3(0.0f, 0.6f, 0.1f),
    ///             new Vector3(0.0f, 0.4f, 0.2f),
    ///             new Vector3(0.1f, 0.3f, 0.0f),
    ///             new Vector3(0.1f, 0.2f, 0.1f),
    ///             new Vector3(0.1f, 0.3f, 0.2f),
    ///             new Vector3(0.2f, 0.0f, 0.0f),
    ///             new Vector3(0.2f, 0.3f, 0.1f),
    ///             new Vector3(0.2f, 0.1f, 0.2f),
    ///         };
    ///
    ///         // Data values for those points
    ///         List&lt;float&gt; data = new List&lt;float&gt;();
    ///         for (int i = 0; i &lt; vertices.Count; i++) data.Add(i);
    ///
    ///         // Named scalar variable
    ///         Dictionary&lt;string, List&lt;float&gt;&gt; scalarVars = new Dictionary&lt;string, List&lt;float&gt;&gt; {{ "someData", data }};
    ///
    ///         // Define some generous bounds
    ///         Bounds b = new Bounds(Vector3.zero, Vector3.one);
    ///
    ///         // STEP 2: Convert the point list into ABR Format
    ///         RawDataset abrPoints = RawDatasetAdapter.PointsToPoints(vertices, b, scalarVars, null);
    ///
    ///         // STEP 3: Import the point data into ABR so we can use it
    ///         KeyData pointsKD = ABREngine.Instance.Data.ImportRawDataset(abrPoints);
    ///
    ///         // STEP 4: Import a colormap visasset
    ///         ColormapVisAsset cmap = ABREngine.Instance.VisAssets.LoadVisAsset&lt;ColormapVisAsset&gt;(new System.Guid("66b3cde4-034d-11eb-a7e6-005056bae6d8"));
    ///
    ///         // STEP 5: Create a Data Impression (layer) for the points, and assign some key data and styling
    ///         SimpleGlyphDataImpression di = new SimpleGlyphDataImpression();
    ///         di.keyData = pointsInfo;                               // Assign key data (point geometry)
    ///         di.colorVariable = pointsInfo.GetScalarVariables()[0]; // Assign scalar variable "someData"
    ///         di.colormap = cmap;                                    // Apply colormap
    ///         di.glyphSize = 0.002f;                                 // Apply glyph size styling
    ///
    ///         // STEP 6: Register impression with the engine
    ///         ABREngine.Instance.RegisterDataImpression(di);
    ///
    ///         // STEP 7: Render the visualization
    ///         ABREngine.Instance.Render();
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <example>
    /// You may also wish to create a data impression but place in a different
    /// position than the original one (e.g., to create a side-by-side
    /// visualization). This example shows how to use the <see
    /// cref="ABREngine.CreateDataImpressionGroup(string, Vector3)"/> and the
    /// <see cref="ABREngine.DuplicateDataImpression(IDataImpression)"/> method
    /// to create a side-by-side visualization.
    /// <code>
    /// public class ABREngineExample
    /// {
    ///     void Start()
    ///     {
    ///         // Let's say we've imported some fancy point data that we want to compare
    ///         KeyData fancyData1 = // ... some import
    ///         KeyData fancyData2 = // ... some import
    /// 
    ///         // We can construct a data impression for the first data. Maybe we've
    ///         // applied a bunch of styling to this that we want to copy...
    ///         SimpleGlyphDataImpression di = new SimpleGlyphDataImpression();
    ///         di.keyData = fancyData1;
    ///         di.colorVariable = fancyData1.GetScalarVariables()[0];
    ///         di.colormap = ABREngine.Instance.VisAssets.GetDefault&lt;ColormapVisAsset&gt;() as ColormapVisAsset;
    ///         di.glyphSize = 0.002f;
    /// 
    ///         // Register the first impression
    ///         ABREngine.Instance.RegisterDataImpression(di);
    /// 
    ///         // Then, duplicate the data impression:
    ///         SimpleGlyphDataImpression other = ABREngine.Instance.DuplicateDataImpression(di) as SimpleGlyphDataImpression;
    /// 
    ///         // Change the data and a little styling
    ///         other.keyData = fancyData2;
    ///         other.glyphSize = 0.5f;
    /// 
    ///         // Create a new impression group centered just to the right of the first one
    ///         DataImpressionGroup newGroup = ABREngine.Instance.CreateDataImpressionGroup("OffsetGroup", new Vector3(0.5f, 0.0f, 0.0f));
    /// 
    ///         // And, register the impression with the new group
    ///         ABREngine.Instance.RegisterDataImpression(other, newGroup);
    ///     }
    /// }
    /// </code>
    /// </example>
    public class ABREngine : Singleton<ABREngine>
    {
        private Dictionary<Guid, DataImpressionGroup> dataImpressionGroups = new Dictionary<Guid, DataImpressionGroup>();

        /// <summary>
        /// JSON representation of the state that has been previously loaded into ABR
        /// </summary>
        public JObject State { get { return previouslyLoadedState; }}
        private JObject previouslyLoadedState = null;
        private string previousStateName = "Untitled";
        private ABRStateParser stateParser = null;

        private object _stateLock = new object();
        private object _stateUpdatingLock = new object();
        private bool stateUpdating = false;

        private Notifier _notifier;

        [SerializeField]
        public ABRConfig configPrototype;

        /// <summary>
        /// System-wide manager for VisAssets (visual elements used in the visualization)
        /// </summary>
        public VisAssetManager VisAssets { get; private set; }

        /// <summary>
        /// System-wide manager for Data (the geometry and variables that make up the visualization)
        /// </summary>
        public DataManager Data { get; private set; }

        /// <summary>
        /// A listener for data connections (e.g., Send2ABR from ParaView)
        /// </summary>
        public SocketDataListener DataListener { get; private set; }

        /// <summary>
        /// Delegate callback that is called whenever the ABRState is updated.
        /// This is useful for applications that build on ABR and need to know
        /// when the state has been updated.
        /// <see cref="ABREngine.OnStateChanged"/> for usage.
        /// </summary>
        public delegate void StateChangeDelegate(JObject state);

        /// <summary>
        /// Delegate that is called whenever ABRState is updated.
        /// </summary>
        /// <example>
        /// Developers may need to use this if they want to know when the state has been updated:
        /// <code>
        /// using UnityEngine;
        /// using IVLab.ABREngine;
        /// public class ABRStateExample : MonoBehaviour
        /// {
        ///     void Start()
        ///     {
        ///         ABREngine.Instance.OnStateChanged += ExampleOnStateChanged;
        ///     }
        ///     void ExampleOnStateChanged(JObject state)
        ///     {
        ///         Debug.Log(state["version"]);
        ///     }
        /// }
        /// </code>
        /// </example>
        public StateChangeDelegate OnStateChanged;

        // Save this for threading purposes (can't be accessed from non-main-thread)
        private string persistentDataPath = null;
        private string streamingAssetsPath = null;

        private DataImpressionGroup _defaultGroup = null;

        /// <summary>
        /// True if the ABREngine is set up and completely ready to begin rendering. Only returns true once all setup has finished, including:
        /// <ul>
        ///     <li>Loading the ABRConfig</li>
        ///     <li>Connecting to the server (if applicable)</li>
        ///     <li>Loading any initial state specified by the programmer, or the remote state from the server</li>
        /// </ul>
        /// </summary>
        public bool IsInitialized { get; private set; } = false;

        /// <summary>
        /// Media path where all datasets and visassets are located. If the
        /// media path is provided in the ABRConfig, use that media path.
        /// Otherwise, use Unity's <a
        /// href="https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html">Application.persistentDataPath</a>.
        /// </summary>
        public string MediaPath
        {
            get
            {
                if (Config.mediaPath != null)
                {
                    return Path.GetFullPath(Config.mediaPath);
                }
                else
                {
                    return Path.Combine(persistentDataPath, "media");
                }
            }
        }

        /// <summary>
        /// Cached, readonly version of the ABREngine transform so it can be accessed in a non-main thread
        /// </summary>
        public Transform ABRTransform { get; private set; }

        /// <summary>
        /// Provides access to all of the ABRConfig options that were loaded in at startup
        /// </summary>
        public ABRConfig Config { get; private set; }

        /// <summary>
        /// Client for internal application usage to make web requests.
        /// </summary>
        internal static readonly HttpClient httpClient = new HttpClient();

        protected override void Awake()
        {
            // Enable depth texture write on main cam so that volume rendering
            // functions correctly
            Camera.main.depthTextureMode = DepthTextureMode.Depth;

            UnityThreadScheduler.GetInstance();
            persistentDataPath = Application.persistentDataPath;
            streamingAssetsPath = Application.streamingAssetsPath;
            ABRTransform = this.transform;
            base.Awake();

            // Initialize state parser
            stateParser = new ABRStateParser();

            // Initialize the configuration from ABRConfig.json
            if (configPrototype != null)
            {
                Config = Instantiate(configPrototype);
            }
            else
            {
                throw new Exception("ABR configuration not found, please create one first and select it in the ABREngine inspector.");
            }

            // Initialize the default DataImpressionGroup (where impressions go
            // when they have no dataset) - guid zeroed out
            _defaultGroup = CreateDataImpressionGroup("Default", new Guid());

            try
            {
                if (Config.serverUrl.Length > 0)
                {
                    _notifier = new Notifier(Config.ServerUrl);
                    _notifier.Init();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Unable to connect to state server " + Config.serverUrl);
                Debug.LogError(e);
            }

            try
            {
                VisAssets = new VisAssetManager(Path.Combine(MediaPath, ABRConfig.Consts.VisAssetFolder));
                Data = new DataManager(Path.Combine(MediaPath, ABRConfig.Consts.DatasetFolder));
                if (Config.dataListenerPort != 0)
                {
                    DataListener = new SocketDataListener(Config.dataListenerPort);
                    DataListener.StartServer();
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            // Fetch the state from the server, if we're connected
            if (Config.serverUrl != null && _notifier != null)
            {
                LoadState<HttpStateFileLoader>(Config.serverUrl.ToString());
            }
            IsInitialized = true;

            // If a state in streaming assets or resources is specified, load it
            if (Config.loadStateOnStart.Length > 0)
            {
                try
                {
                    LoadState<ResourceStateFileLoader>(Config.loadStateOnStart);
                    if (previouslyLoadedState == null)
                    {
                        throw new Exception();
                    }
                    else
                    {
                        Debug.Log($"Loaded state `{Config.loadStateOnStart}` from Resources");
                    }
                }
                catch (Exception)
                {
                    LoadState<PathStateFileLoader>(Path.Combine(streamingAssetsPath, Config.loadStateOnStart));
                    Debug.Log($"Loaded state `{Config.loadStateOnStart}` from StreamingAssets");
                }
            }
        }

        /// <summary>
        /// Wait until the Engine is fully initialized before proceeding to use it.
        /// </summary>
        /// <example>
        /// For example, if we want to do some ABREngine-dependant tasks in a MonoBehaviour Start():
        /// <code>
        /// using UnityEngine;
        /// using IVLab.ABREngine;
        /// 
        /// public class ABRInitializerExample : MonoBehaviour
        /// {
        ///     void Start()
        ///     {
        ///         // Wait for the engine to initialize...
        ///         while (!ABREngine.Instance.IsInitialized);
        ///
        ///         // ... then print out some very important information that
        ///         // depends on ABR being initialized
        ///         Debug.Log(ABREngine.Instance.Config.defaultBounds);
        ///     }
        /// }
        /// </code>
        /// </example>
        public async Task WaitUntilInitialized()
        {
            while (!IsInitialized)
            {
                await Task.Delay(10);
            }
        }

        void OnDisable()
        {
            _notifier?.Stop();
            DataListener?.StopServer();
        }

        /// <summary>
        /// Check to see if the data impression with a given uuid exists
        /// </summary>
        /// <param name="uuid">Unique identifier (UUID) of the requisite data impression</param>
        /// <returns>
        /// A boolean whether or not this data impression is present in this ABR state
        /// </returns>
        public bool HasDataImpression(Guid uuid)
        {
            return dataImpressionGroups
                .Select((kv) => kv.Value)
                .Where((v) => v.HasDataImpression(uuid)).ToList().Count > 0;
        }

        /// <summary>
        /// Retreive a particular data impression from the Engine
        /// </summary>
        /// <returns>
        /// A data impression if found, null otherwise.
        /// </returns>
        /// <param name="uuid">Unique identifier (UUID) of the data impression to be fetched from the engine</param>
        /// <remarks>
        /// It is often more useful to use the <see
        /// cref="ABREngine.GetDataImpression{T}"/> method since it returns an
        /// actual data impression instead of a <see cref="IDataImpression"/>.
        /// </remarks>
        public IDataImpression GetDataImpression(Guid uuid)
        {
            return dataImpressionGroups?
                .Select((kv) => kv.Value)
                .FirstOrDefault((v) => v.HasDataImpression(uuid))?
                .GetDataImpression(uuid);
        }

        /// <summary>
        /// Returns the first data impression that is associated with the keyDataPath.
        /// Although it is often the case that there will be only one data impression per keyDataPath,
        /// this is not always the case.  GetDataImpressions(string keyDataPath) can be used to get all
        /// of the data impressions with the same keyDataPath.
        /// </summary>
        public IDataImpression GetDataImpression(string keyDataPath)
        {
            return GetDataImpressions(keyDataPath).FirstOrDefault();
        }

        /// <summary>
        /// Returns all of the data impressions associated with the specified keyDataPath
        /// </summary>
        public List<IDataImpression> GetDataImpressions(string keyDataPath)
        {
            return GetDataImpressions(di => { return di.GetKeyData().Path == keyDataPath; });
        }

        /// <summary>
        /// Returns the first data impression within the group that is associated with the keyDataPath.
        /// Although it is often the case that there will be only one data impression per keyDataPath,
        /// this is not always the case.  GetDataImpressions(string keyDataPath) can be used to get all
        /// of the data impressions with the same keyDataPath.
        /// </summary>
        public T GetDataImpression<T>(string keyDataPath) where T : IDataImpression
        {
            return GetDataImpressions<T>(keyDataPath).FirstOrDefault();
        }

        /// <summary>
        /// Returns all of the data impressions associated with the specified keyDataPath
        /// </summary>
        public List<T> GetDataImpressions<T>(string keyDataPath) where T : IDataImpression
        {
            return GetDataImpressions<T>(di => { return di.GetKeyData().Path == keyDataPath; });
        }


        /// <summary>
        /// Retreive the first data impression found with a particular function crieteria (similar to a "filter" or Linq-esque "where" operation).
        /// </summary>
        /// <param name="criteria">Function that takes each data impression of any type and returns a boolean.</param>
        /// <returns>
        /// The first data impression of any type that matches criteria, null otherwise.
        /// </returns>
        public IDataImpression GetDataImpression(Func<IDataImpression, bool> criteria)
        {
            return GetAllDataImpressions().FirstOrDefault(criteria);
        }

        /// <summary>
        /// Retreive the first data impression found with a particular type AND function crieteria (similar to a "filter" or Linq-esque "where" operation).
        /// </summary>
        /// <param name="criteria">Function that takes each data impression matching type `T` and returns a boolean.</param>
        /// <typeparam name="T">Any data impression type implementing <see cref="IDataImpression"/></typeparam>
        /// <returns>
        /// The first data impression of type `T` that matches criteria, null otherwise.
        /// </returns>
        public T GetDataImpression<T>(Func<T, bool> criteria)
        where T : IDataImpression
        {
            return GetDataImpressions<T>().FirstOrDefault(criteria);
        }

        /// <summary>
        /// Retreive the first data impression found with a particular type
        /// </summary>
        /// <returns>
        /// The first data impression of matching type `T`, null otherwise.
        /// </returns>
        /// <typeparam name="T">Any data impression type implementing <see cref="IDataImpression"/></typeparam>
        public T GetDataImpression<T>()
        where T : IDataImpression
        {
            return GetDataImpressions<T>().FirstOrDefault();
        }

        /// <summary>
        /// Retrieve all data impressions in an ABR state of a given impression
        /// type (e.g., all `SimpleSurfaceDataImpression`s)
        /// </summary>
        /// <returns>
        /// A list of data impressions that have a particular type
        /// </returns>
        [Obsolete("GetDataImpressionsOfType<T> is obsolete, use GetDataImpressions<T> instead")]
        public List<T> GetDataImpressionsOfType<T>()
        where T : IDataImpression
        {
            return dataImpressionGroups?
                .Select((kv) => kv.Value)
                .Select((grp) => grp.GetDataImpressionsOfType<T>())
                .Aggregate((all, imps) => all.Concat(imps).ToList());
        }


        /// <summary>
        /// Retrieve all data impressions in an ABR scene that have a particular
        /// tag. Note that the ABREngine does not do anything with tags; these
        /// exist solely for application developers.
        /// </summary>
        /// <param name="tag">The tag to check for</param>
        /// <returns>
        /// A list of data impressions with a particular tag
        /// </returns>
        public List<IDataImpression> GetDataImpressionsWithTag(string tag)
        {
            return dataImpressionGroups?
                .Select((kv) => kv.Value)
                .Select((grp) => grp.GetDataImpressionsWithTag(tag))
                .Aggregate((all, imps) => all.Concat(imps).ToList());
        }

        /// <summary>
        /// Retreive the all data impressions found of any type matching function crieteria (similar to a "filter" or Linq-esque "where" operation).
        /// </summary>
        /// <param name="criteria">Function that takes each data impression and returns a boolean.</param>
        /// <returns>
        /// All data impressions of any type that matches criteria, null otherwise.
        /// </returns>
        public List<IDataImpression> GetDataImpressions(Func<IDataImpression, bool> criteria)
        {
            return GetAllDataImpressions()?.Where(criteria).ToList();
        }

        /// <summary>
        /// Retreive the all data impressions found of a particular type
        /// </summary>
        /// <typeparam name="T">Any data impression type implementing <see cref="IDataImpression"/></typeparam>
        /// <returns>
        /// All data impressions of type `T`.
        /// </returns>
        public List<T> GetDataImpressions<T>()
        where T : IDataImpression
        {
            return GetAllDataImpressions()?
                .Where((imp) => imp.GetType().IsAssignableFrom(typeof(T)))
                .Select((imp) => (T) imp).ToList();
        }

        /// <summary>
        /// Retreive the all data impressions found of a particular type matching function crieteria (similar to a "filter" or Linq-esque "where" operation).
        /// </summary>
        /// <param name="criteria">Function that takes each data impression of type `T` and returns a boolean.</param>
        /// <typeparam name="T">Any data impression type implementing <see cref="IDataImpression"/></typeparam>
        /// <returns>
        /// All data impressions of type `T` that match the criteria.
        /// </returns>
        public List<T> GetDataImpressions<T>(Func<T, bool> criteria)
        where T : IDataImpression
        {
            return GetDataImpressions<T>()?.Where(criteria).ToList();
        }

        /// <summary>
        /// Retrieve ALL data impressions that currently exist within the
        /// Engine, over ALL data impression groups.
        /// </summary>
        /// <returns>
        /// All data impressions that exist in the ABREngine
        /// </returns>
        public List<IDataImpression> GetAllDataImpressions()
        {
            return dataImpressionGroups?
                .Select((kv) => kv.Value.GetDataImpressions().Values.ToList())
                .Aggregate((all, imps) => all.Concat(imps).ToList());
        }

        /// <summary>
        /// Retrieve the encoded game object in the Unity scene associated with
        /// a particular data impression, identified by its guid.
        /// </summary>
        /// <param name="impressionGuid">Unique identifier (UUID) of the data impression to get the GameObject for</param>
        /// <returns>
        /// An <see cref="EncodedGameObject"/> (MonoBehaviour) of the Data Impression as it
        /// exists in the Unity Scene, or null if no such impression exists.
        /// </returns>
        public EncodedGameObject GetEncodedGameObject(Guid impressionGuid)
        {
            return dataImpressionGroups?
                .Select((kv) => kv.Value)
                .FirstOrDefault((v) => v.HasEncodedGameObject(impressionGuid))?
                .GetEncodedGameObject(impressionGuid);
        }


        /// <summary>
        /// Add a new data impression group with a particular name
        /// </summary>
        /// <param name="name">Name of the new data impression group that will be created</param>
        /// <returns>
        /// The group that has been added.
        /// </returns>
        public DataImpressionGroup CreateDataImpressionGroup(string name)
        {
            return CreateDataImpressionGroup(name, Guid.NewGuid(), null, Matrix4x4.identity);
        }

        /// <summary>
        /// Add a new data impression group with a particular UUID
        /// </summary>
        /// <param name="name">Name of the new data impression group that will be created</param>
        /// <param name="uuid">UUID to use for the new data impression group</param>
        /// <returns>
        /// The group that has been added.
        /// </returns>
        public DataImpressionGroup CreateDataImpressionGroup(string name, Guid uuid)
        {
            return CreateDataImpressionGroup(name, uuid, null, Matrix4x4.identity);
        }

        /// <summary>
        /// Add a new data impression group with a particular UUID, bounds, and transformation
        /// </summary>
        /// <param name="name">Name of the new data impression group that will be created</param>
        /// <param name="uuid">UUID to use for the new data impression group</param>
        /// <param name="containerBounds">Default bounds to use for this data impression group. Data will be "squished" inside this bounding box.</param>
        /// <param name="transformMatrix">Default position/rotation/scale (in Unity coordinates) to use for the data impression group.</param>
        /// <returns>
        /// The group that has been added.
        /// </returns>
        public DataImpressionGroup CreateDataImpressionGroup(string name, Guid uuid, Bounds? containerBounds, Matrix4x4? transformMatrix)
        {
            DataImpressionGroup group = DataImpressionGroup.Create(name, uuid, containerBounds, transformMatrix.GetValueOrDefault(Matrix4x4.identity));
            dataImpressionGroups[group.Uuid] = group;
            return group;
        }

        /// <summary>
        /// Remove a given data impression group from the scene, destroying all
        /// of the data impressions within the group.
        /// </summary>
        /// <param name="uuid">UUID of the data impression group that should be removed.</param>
        public void RemoveDataImpressionGroup(Guid uuid)
        {
            dataImpressionGroups[uuid].Clear();
            Destroy(dataImpressionGroups[uuid]);
            dataImpressionGroups.Remove(uuid);
        }

        /// <summary>
        /// Retrieve all data impression groups that currently exist in the
        /// Unity ABR scene.
        /// </summary>
        /// <returns>
        /// Dictionary mapping of (uuid => `DataImpressionGroup`)
        /// </returns>
        public Dictionary<Guid, DataImpressionGroup> GetDataImpressionGroups()
        {
            return dataImpressionGroups;
        }

        /// <summary>
        /// Retrieve a particular data impression group from the scene
        /// </summary>
        /// <param name="uuid">UUID of the data impression group that should be retrieved.</param>
        /// <returns>
        /// A data impression with a given UUID
        /// </returns>
        public DataImpressionGroup GetDataImpressionGroup(Guid uuid)
        {
            DataImpressionGroup g = null;
            if (dataImpressionGroups.TryGetValue(uuid, out g))
            {
                return g;
            }
            else
            {
                return null;
            }
        }


        public DataImpressionGroup GetDataImpressionGroup(string name)
        {
            return dataImpressionGroups.Values.FirstOrDefault(g => g.name == name);
        }


        /// <summary>
        /// Retrieve the first data impression group found that is associated with a particular <see cref="Dataset"/>.
        /// </summary>
        /// <param name="ds">Dataset that should be matched with</param>
        /// <returns>
        /// A data impression with the given dataset.
        /// </returns>
        public DataImpressionGroup GetDataImpressionGroupByDataset(Dataset ds)
        {
            return dataImpressionGroups.Values.FirstOrDefault((g) => g.GetDataset()?.Path == ds?.Path);
        }

        /// <summary>
        /// Check if a particular data impression group exists.
        /// </summary>
        /// <param name="uuid">UUID to check existence of</param>
        /// <returns>
        /// Boolean - true if the given group exists in the current ABR state, false otherwise.
        /// </returns>
        public bool HasDataImpressionGroup(Guid uuid)
        {
            return dataImpressionGroups.ContainsKey(uuid);
        }

        /// <summary>
        /// Get the data impression group a particular data impression
        /// </summary>
        /// <param name="dataImpression">Data impression to find out the group of</param>
        /// <returns>
        /// The data impression group `dataImpression` belongs to, otherwise null.
        /// </returns>
        public DataImpressionGroup GetGroupFromImpression(IDataImpression dataImpression)
        {
            try
            {
                return dataImpressionGroups?
                    .Select((kv) => kv.Value)
                    .First((v) => dataImpression != null && v.HasDataImpression(dataImpression.Uuid));
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// Register a new data impression with the ABREngine and add it to a
        /// specific <see cref="DataImpressionGroup"/>.
        /// </summary>
        /// <param name="dataImpression">The data impression to register with the engine</param>
        /// <param name="newGroup">Group to add this data impression to</param>
        /// <param name="allowOverwrite">Should we destroy any existing data impressions that have this UUID already?</param>
        public void RegisterDataImpression(IDataImpression dataImpression, DataImpressionGroup newGroup, bool allowOverwrite = true)
        {
            // Create a new group if it doesn't exist
            // OR, if it's in the default group but now has a dataset, move it to its proper group
            if (newGroup == null || newGroup == _defaultGroup)
            {
                // First, check if there's already a group associated with this dataset
                Dataset ds = dataImpression.GetDataset();
                DataImpressionGroup dsGroup = GetDataImpressionGroupByDataset(ds);
                // If so, add it to that group
                if (dsGroup != null)
                {
                    newGroup = dsGroup;
                }
                // If not, proceed to make a new group
                else
                {
                    // Name it according to the impression's dataset, if there is one
                    if (ds?.Path != null)
                    {
                        newGroup = CreateDataImpressionGroup(ds.Path);
                    }
                    else
                    {
                        newGroup = CreateDataImpressionGroup(string.Format("{0}", DateTimeOffset.Now.ToUnixTimeMilliseconds()));
                    }
                }
            }
            MoveImpressionToGroup(dataImpression, newGroup, allowOverwrite);
        }


        /// <summary>
        /// Register a new data impression, or replace an existing one. If the
        /// data impression has a dataset, defaults to placing it inside the
        /// existing group with the same dataset, or creating a new
        /// <see cref="DataImpressionGroup"/> with that dataset if no group exists yet.
        /// </summary>
        /// <param name="dataImpression">The data impression to register with the engine</param>
        /// <param name="allowOverwrite">Should we destroy any existing data impressions that have this UUID already?</param>
        public void RegisterDataImpression(IDataImpression dataImpression, bool allowOverwrite = true)
        {
            Dataset ds = dataImpression.GetDataset();
            if (ds != null)
            {
                // Find an existing DataImpressionGroup with the same dataset, if any
                DataImpressionGroup newGroup = null;
                foreach (var group in dataImpressionGroups)
                {
                    if (group.Value.GetDataset()?.Path == ds.Path)
                    {
                        // Add it to the first one we find, if we find one
                        newGroup = group.Value;
                    }
                }

                RegisterDataImpression(dataImpression, newGroup, allowOverwrite);
            }
            else
            {
                // It's possible that this impression previously had key data, in which case we must
                // remove and unregister it from whatever group it was a part of due to that key data
                // now that it has none
                UnregisterDataImpression(dataImpression.Uuid);
                
                // Since there's no data, put it in the default group
                _defaultGroup.AddDataImpression(dataImpression, allowOverwrite);
            }
        }

        /// <summary>
        /// Remove a data impression from the ABR state.
        /// </summary>
        /// <param name="uuid">The UUID data impression to remove (unregister) from the ABREngine</param>
        public void UnregisterDataImpression(Guid uuid)
        {
            var toRemove = new List<Guid>();
            foreach (var group in dataImpressionGroups)
            {
                // Remove the impression from any groups its in (should only be one)
                bool groupIsEmpty = group.Value.RemoveDataImpression(uuid);

                // Also remove the group if it becomes empty, unless it's the default group
                if (groupIsEmpty && group.Key != _defaultGroup.Uuid)
                {
                    toRemove.Add(group.Key);
                }
            }

            foreach (var guid in toRemove)
            {
                RemoveDataImpressionGroup(guid);
            }
        }

        /// <summary>
        /// Create and return a duplicate copy of the data impression with a
        /// given UUID. All inputs in the new data impression are identical to
        /// the one being copied. By default duplicate data impressions will be
        /// placed in their default groups (grouped by dataset).
        /// </summary>
        /// <param name="uuid">UUID of the data impression that should be duplicated.</param>
        /// <returns>
        /// The new data impression.
        /// </returns>
        public IDataImpression DuplicateDataImpression(Guid uuid)
        {
            return DuplicateDataImpression(GetDataImpression(uuid));
        }

        /// <summary>
        /// Create and return a duplicate copy of the given data impression.
        /// All inputs in the new data impression are identical to
        /// the one being copied. By default duplicate data impressions will be
        /// placed in their default groups (grouped by dataset).
        /// </summary>
        /// <param name="impression">The data impression that should be duplicated.</param>
        /// <returns>
        /// The new data impression.
        /// </returns>
        public IDataImpression DuplicateDataImpression(IDataImpression impression)
        {
            return DuplicateDataImpression(impression, null);
        }

        /// <summary>
        /// Create and return a duplicate copy of the given data impression.
        /// The data impression will be placed within the specified
        /// `DataImpressionGroup group`. If `group` is null, the default group
        /// will be used (either conforming to the input dataset that the data
        /// impression has, or the default empty group)
        /// </summary>
        /// <param name="dataImpression">The data impression that should be duplicated.</param>
        /// <param name="group">The <see cref="DataImpressionGroup"/> that the new data impression should be placed into.</param>
        /// <returns>
        /// The new data impression.
        /// </returns>
        public IDataImpression DuplicateDataImpression(IDataImpression dataImpression, DataImpressionGroup group)
        {
            IDataImpression newDataImpression = dataImpression.Copy();
            newDataImpression.Uuid = Guid.NewGuid();

            if (group == null)
            {
                RegisterDataImpression(newDataImpression);
            }
            else
            {
                RegisterDataImpression(newDataImpression, group, false);
            }

            return newDataImpression;
        }

        /// <summary>
        /// Create and return a duplicate copy of the given data impression, but
        /// ensure that the copy is within the same data impression group as its
        /// source.
        /// </summary>
        /// <param name="dataImpression">The data impression that should be duplicated.</param>
        /// <param name="retainGroup">Ensure the copy of the data impression
        /// will exist within the same group as the original, regardless of
        /// whether different data have been applied.</param>
        /// <returns>
        /// The new data impression.
        /// </returns>
        public IDataImpression DuplicateDataImpression(IDataImpression dataImpression, bool retainGroup = true)
        {
            if (retainGroup)
            {
                DataImpressionGroup group = GetGroupFromImpression(dataImpression);
                return DuplicateDataImpression(dataImpression, group);
            }
            else
            {
                return DuplicateDataImpression(dataImpression, null);
            }
        }

        /// <summary>
        /// Move a data impression from its current group to a new group.
        /// </summary>
        /// <param name="dataImpression">The data impression that should be moved.</param>
        /// <param name="newGroup">The group to place the data impression into.</param>
        /// <param name="allowOverwrite">Should we destroy any existing data impressions with this UUID within the `newGroup`?</param>
        public void MoveImpressionToGroup(IDataImpression dataImpression, DataImpressionGroup newGroup, bool allowOverwrite = true)
        {
            // See if it's a part of a group already
            DataImpressionGroup oldGroup = GetGroupFromImpression(dataImpression);

            // If the new and old groups are different, remove from old group
            bool oldGroupEmpty = false;
            if (oldGroup != null && newGroup != null && newGroup.Uuid != oldGroup.Uuid)
            {
                oldGroupEmpty = oldGroup.RemoveDataImpression(dataImpression.Uuid);
            }

            // If the old group is empty, remove it
            if (oldGroupEmpty && oldGroup.Uuid != _defaultGroup.Uuid)
            {
                RemoveDataImpressionGroup(oldGroup.Uuid);
            }

            newGroup.AddDataImpression(dataImpression, allowOverwrite);
        }



        public IKeyData GetKeyData(string keyDataPath)
        {
            List<Dataset> allDatasets = ABREngine.Instance.Data.GetDatasets();
            foreach (var ds in allDatasets) {
                Dictionary<string, IKeyData> keyDatas = ds.GetAllKeyData();
                foreach (var kd in keyDatas) {
                    if (kd.Key == keyDataPath) {
                        return kd.Value;
                    }
                }
            }
            return null;
        }



        public Dictionary<string, IKeyData> GetKeyDataStartsWith(string keyDataPathStartsWith)
        {
            Dictionary<string, IKeyData> results = new Dictionary<string, IKeyData>();
            List<Dataset> allDatasets = ABREngine.Instance.Data.GetDatasets();
            foreach (var ds in allDatasets) {
                Dictionary<string, IKeyData> keyDatas = ds.GetAllKeyData();
                foreach (var kd in keyDatas) {
                    if (kd.Key.StartsWith(keyDataPathStartsWith)) {
                        results.Add(kd.Key, kd.Value);
                    }
                }
            }
            return results;
        }



        /// <summary>
        /// Remove all data impression groups from the ABR scene (and in turn, remove all data impressions).
        /// </summary>
        /// <remarks>
        /// This method does remove all data impressions, but it does not clean
        /// up other state values like custom colormaps, lighting, and
        /// gradients.
        /// </remarks>
        public void ClearState()
        {
            List<Guid> toRemove = new List<Guid>();
            foreach (var group in dataImpressionGroups)
            {
                group.Value.Clear();
                if (group.Key != _defaultGroup.Uuid)
                {
                    toRemove.Add(group.Key);
                }
            }
            foreach (var r in toRemove)
            {
                dataImpressionGroups.Remove(r);
            }
        }

        /// <summary>
        /// Go through every data impression group's impressions and render
        /// them. Each impression intelligently decides if the entire geometry
        /// needs to be recomputed (slow), or if only the style has changed (fast).
        /// </summary>
        public void Render()
        {
            try
            {
                lock (_stateLock)
                {
                    foreach (var group in dataImpressionGroups)
                    {
                        group.Value.RenderImpressions();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error while rendering impressions");
                Debug.LogError(e);
            }
        }

        /// <summary>
        /// Load a state into ABR. This includes the following steps:
        /// <ol>
        ///     <li>All Data and VisAssets from the state have been loaded.</li>
        ///     <li>The ABR scene has been rendered with all updates (including data impressions, lighting, etc.).</li>
        ///     <li>The <see cref="ABREngine.OnStateChanged"/> callback has been fired.</li>
        /// </ol>
        /// </summary>
        /// <remarks>
        /// Data impressions that have not changed will not be re-rendered. 
        /// </remarks>
        /// <example>
        /// A state may be loaded from any of the following places:
        /// <code>
        /// // A Resources folder (in Assets or in a Package)
        /// ABREngine.Instance.LoadState&lt;ResourceStateFileLoader&gt;("exampleState.json");
        ///
        /// // A local file
        /// ABREngine.Instance.LoadState&lt;PathStateFileLoader&gt;("C:/Users/VRDemo/Desktop/test.json");
        ///
        /// // A JSON string
        /// ABREngine.Instance.LoadState&lt;ResourceStateFileLoader&gt;("{\"version\": \"0.2.0\", \"name\": \"test\"}");
        ///
        /// // A web resource
        /// ABREngine.Instance.LoadState&lt;HttpStateFileLoader&gt;("http://localhost:8000/api/state");
        /// </code>
        /// </example>
        public void LoadState<T>(string stateName)
        where T : IABRStateLoader, new()
        {
            lock (_stateUpdatingLock)
            {
                stateUpdating = true;
            }
            try
            {
                JObject tempState = stateParser.LoadState<T>(stateName, previouslyLoadedState);
                lock (_stateLock)
                {
                    previousStateName = stateName;
                    previouslyLoadedState = tempState;
                }
                Render();
                lock (_stateUpdatingLock)
                {
                    stateUpdating = false;
                }
                if (OnStateChanged != null)
                {
                    OnStateChanged(previouslyLoadedState);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        /// <summary>
        /// Save a state from the ABR Unity scene back to a particular JSON destination.
        /// </summary>
        /// <remarks>
        /// The SaveState functionality is only implemented in a few
        /// <see cref="IABRStateLoader"/> classes, namely <see cref="PathStateFileLoader"/> and
        /// <see cref="HttpStateFileLoader"/>.
        ///
        /// This method generally takes a long time so should not be called
        /// frequently. Also, since this method manually looks through
        /// GameObjects in the scene to reverse-engineer a JSON state, so the
        /// resulting JSON state may not always be complete.
        /// </remarks>
        /// <example>
        /// ABR states can be saved like this:
        /// <code>
        /// // Save the current state to a file on your computer
        /// ABREngine.Instance.SaveState&lt;PathStateFileLoader&gt;("C:/Users/VRDemo/Desktop/exampleState.json");
        ///
        /// // Save the current state to a web resource
        /// ABREngine.Instance.SaveState&lt;HttpStateFileLoader&gt;("http://localhost:8000/api/state");
        /// </code>
        /// </example>
        public void SaveState<T>(string overrideStateName = null)
        where T : IABRStateLoader, new()
        {
            if (overrideStateName != null)
            {
                previousStateName = overrideStateName;
            }
            T loader = new T();
            try
            {
                string state = stateParser.SerializeState(previouslyLoadedState);
                loader.SaveState(previousStateName, state);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}