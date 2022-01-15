/* ABREngine.cs
 *
 * Copyright (c) 2021 University of Minnesota
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
    /// <remarks>
    /// Many of the methods in the ABREngine must be run from Unity's Main
    /// Thread. For simple scenes this is not a problem, but when you start to
    /// integrate with an ABR Server things become more difficult. The general
    /// guideline is - if anything interacts directly with Unity, it should go
    /// in the main thread using `IVLab.Utilities.UnityThreadScheduler`. See the
    /// following example for information on how to properly handle asynchrony
    /// in ABR.
    /// </remarks>
    /// <example>
    /// Applications built on ABR should make heavy use of C#'s
    /// `System.Threading.Task` framework and
    /// `IVLab.Utilities.UnityThreadScheduler`. Here's a simple example that
    /// loads in the ABREngine, loads a state, and displays a single data
    /// impression. See <see cref="TestingABR"> for additional example calls
    /// into the ABREngine.
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
    ///         Task.Run(async () =>
    ///         {
    ///             await ABREngine.GetInstance().WaitUntilInitialized();
    ///
    ///             // By this point, the ABREngine is initialized and we can
    ///             // load a state.
    ///             await ABREngine
    ///                 .Instance
    ///                 .LoadStateAsync&lt;ResourceStateFileLoader&gt;("exampleState.json");
    ///
    ///             // At this point, all the data impressions, visassets, and
    ///             // data have been loaded into ABR, so we can retrieve them.
    ///             SimpleGlyphDataImpression gi = ABREngine
    ///                 .Instance
    ///                 .GetDataImpression(
    ///                     new Guid("48cca33b-e1ae-4998-a0d1-2eee1e75e07d")
    ///                 ) as SimpleGlyphDataImpression;
    ///
    ///             // Now that we have the impression, we can modify its
    ///             // contents (e.g. remove the colormap)
    ///             gi.colormap = null;
    ///
    ///             // Lastly, render the data impressions.... but this MUST be
    ///             // done in the Unity Main thread.
    ///             UnityThreadScheduler.Instance.RunMainThreadWork(() => 
    ///             {
    ///                 ABREngine.Instance.Render();
    ///             });
    ///         });
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
        /// See <a href="api/IVLab.ABREngine.ABREngine.html#IVLab_ABREngine_ABREngine_OnStateChanged">OnStateChanged</a> for usage.
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
                if (Config.Info.mediaPath != null)
                {
                    return Path.GetFullPath(Config.Info.mediaPath);
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
        public static readonly HttpClient httpClient = new HttpClient();

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
            Config = new ABRConfig();

            // Initialize the default DataImpressionGroup (where impressions go
            // when they have no dataset) - guid zeroed out
            _defaultGroup = AddDataImpressionGroup("Default", new Guid());

            Task.Run(async () =>
            {
                try
                {
                    if (Config.Info.serverAddress != null)
                    {
                        _notifier = new Notifier(Config.Info.serverAddress);
                        await _notifier.Init();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Unable to connect to state server " + Config.Info.serverAddress);
                    Debug.LogError(e);
                }

                try
                {
                    VisAssets = new VisAssetManager(Path.Combine(MediaPath, ABRConfig.Consts.VisAssetFolder));
                    Data = new DataManager(Path.Combine(MediaPath, ABRConfig.Consts.DatasetFolder));
                    if (Config.Info.dataListenerPort != null)
                    {
                        DataListener = new SocketDataListener(Config.Info.dataListenerPort.Value);
                        DataListener.StartServer();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }

                // Fetch the state from the server, if we're connected
                if (Config.Info.serverAddress != null &&
                        _notifier != null &&
                        Config.Info.statePathOnServer != null
                )
                {
                    LoadState<HttpStateFileLoader>(Config.Info.serverAddress + Config.Info.statePathOnServer);
                }
                IsInitialized = true;

                // If a state in streaming assets or resources is specified, load it
                if (Config.Info.loadStateOnStart != null)
                {
                    try
                    {
                        await UnityThreadScheduler.Instance.RunMainThreadWork(async () =>
                        {
                            await LoadStateAsync<PathStateFileLoader>(Path.Combine(streamingAssetsPath, Config.Info.loadStateOnStart));
                            Debug.Log($"Loaded state `{Config.Info.loadStateOnStart}` from StreamingAssets");
                        });
                    }
                    catch (Exception)
                    {
                        await LoadStateAsync<ResourceStateFileLoader>(Config.Info.loadStateOnStart);
                        Debug.Log($"Loaded state `{Config.Info.loadStateOnStart}` from Resources");
                    }
                }
            });
        }

        /// <summary>
        /// Wait until the Engine is fully initialized before proceeding to use it.
        /// </summary>
        /// <example>
        /// For example, if we want to do some ABREngine-dependant tasks in a MonoBehaviour Start():
        /// <code>
        /// using System.Threading.Tasks;
        /// using UnityEngine;
        /// using IVLab.ABREngine;
        /// 
        /// public class ABRInitializerExample : MonoBehaviour
        /// {
        ///     void Start()
        ///     {
        ///         Task.Run(async () =>
        ///         {
        ///             // Wait for the engine to initialize...
        ///             await ABREngine.Instance.WaitUntilInitialized();
        ///             // ... then print out some very important information that
        ///             // depends on ABR being initialized
        ///             Debug.Log(ABREngine.Instance.Config.Info.defaultBounds);
        ///         });
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
        /// <remarks>
        /// It is often useful to cast the result of this data impression so individual inputs can be modified:
        /// <code>SimpleSurfaceDataImpression di = ABREngine.Instance.GetDataImpression(someGuid) as SimpleSurfaceDataImpression</code>
        /// </remarks>
        public IDataImpression GetDataImpression(Guid uuid)
        {
            return dataImpressionGroups
                .Select((kv) => kv.Value)
                .FirstOrDefault((v) => v.HasDataImpression(uuid))?
                .GetDataImpression(uuid);
        }

        /// <summary>
        /// Retreive the first data impression found with a particular function crieteria
        /// </summary>
        /// <returns>
        /// A data impression if found, null otherwise.
        /// </returns>
        public IDataImpression GetDataImpression(Func<IDataImpression, bool> criteria)
        {
            return GetAllDataImpressions().FirstOrDefault(criteria);
        }

        /// <summary>
        /// Retreive the first data impression found with a particular type AND function crieteria
        /// </summary>
        public T GetDataImpression<T>(Func<T, bool> criteria)
        where T : IDataImpression
        {
            return GetDataImpressions<T>().FirstOrDefault(criteria);
        }

        /// <summary>
        /// Retreive the first data impression found with a particular type
        /// </summary>
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
            return dataImpressionGroups
                .Select((kv) => kv.Value)
                .Select((grp) => grp.GetDataImpressionsOfType<T>())
                .Aggregate((all, imps) => all.Concat(imps).ToList());
        }


        /// <summary>
        /// Retrieve all data impressions in an ABR scene that have a particular
        /// tag. Note that the ABREngine does not do anything with tags; these
        /// exist solely for application developers.
        /// </summary>
        /// <returns>
        /// A list of data impressions with a particular tag
        /// </returns>
        public List<IDataImpression> GetDataImpressionsWithTag(string tag)
        {
            return dataImpressionGroups
                .Select((kv) => kv.Value)
                .Select((grp) => grp.GetDataImpressionsWithTag(tag))
                .Aggregate((all, imps) => all.Concat(imps).ToList());
        }

        /// <summary>
        /// Retrieve all data impressions matching a particular criteria
        /// </summary>
        public List<IDataImpression> GetDataImpressions(Func<IDataImpression, bool> criteria)
        {
            return GetAllDataImpressions().Where(criteria).ToList();
        }

        /// <summary>
        /// Retrieve all data impressions of a particular type
        /// </summary>
        public List<T> GetDataImpressions<T>()
        where T : IDataImpression
        {
            return GetAllDataImpressions()
                .Where((imp) => imp.GetType().IsAssignableFrom(typeof(T)))
                .Select((imp) => (T) imp).ToList();
        }

        /// <summary>
        /// Retrieve all data impressions of a particular type AND matching criteria
        /// </summary>
        public List<T> GetDataImpressions<T>(Func<T, bool> criteria)
        where T : IDataImpression
        {
            return GetDataImpressions<T>().Where(criteria).ToList();
        }

        /// <summary>
        /// Retrieve ALL data impressions that currently exist within the
        /// Engine, over ALL data impression groups.
        /// </summary>
        public List<IDataImpression> GetAllDataImpressions()
        {
            return dataImpressionGroups
                .Select((kv) => kv.Value.GetDataImpressions().Values.ToList())
                .Aggregate((all, imps) => all.Concat(imps).ToList());
        }

        /// <summary>
        /// Retrieve the encoded game object in the Unity scene associated with
        /// a particular data impression, identified by its guid.
        /// </summary>
        /// <returns>
        /// An EncodedGameObject (MonoBehaviour) of the Data Impression as it
        /// exists in the Unity Scene, or null if no such impression exists.
        /// </returns>
        public EncodedGameObject GetEncodedGameObject(Guid impressionGuid)
        {
            return dataImpressionGroups
                .Select((kv) => kv.Value)
                .FirstOrDefault((v) => v.HasEncodedGameObject(impressionGuid))?
                .GetEncodedGameObject(impressionGuid);
        }

        /// <summary>
        /// Add a bare data impression group into the ABR scene. The group
        /// bounds defaults to the bounds found in
        /// `ABRConfig.Info.defaultBounds`, and the position/rotation default to
        /// zero.
        /// </summary>
        /// <returns>
        /// The group that has been added.
        /// </returns>
        public DataImpressionGroup AddDataImpressionGroup(string name)
        {
            return AddDataImpressionGroup(name, Guid.NewGuid(), Config.Info.defaultBounds.Value, Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// Add a new data impression group with a particular UUID. The group
        /// bounds defaults to the bounds found in
        /// `ABRConfig.Info.defaultBounds`, and the position/rotation default to
        /// zero.
        /// </summary>
        /// <returns>
        /// The group that has been added.
        /// </returns>
        public DataImpressionGroup AddDataImpressionGroup(string name, Guid uuid)
        {
            return AddDataImpressionGroup(name, uuid, Config.Info.defaultBounds.Value, Vector3.zero, Quaternion.identity);
        }


        /// <summary>
        /// Add a new data impression group with a particular UUID, bounds, position, and rotation.
        /// </summary>
        /// <returns>
        /// The group that has been added.
        /// </returns>
        public DataImpressionGroup AddDataImpressionGroup(string name, Guid uuid, Bounds bounds, Vector3 position, Quaternion rotation)
        {
            DataImpressionGroup group = new DataImpressionGroup(name, uuid, bounds, position, rotation, this.transform);
            dataImpressionGroups[group.Uuid] = group;
            return group;
        }

        /// <summary>
        /// Remove a given data impression group from the scene, destroying all
        /// of the data impressions within the group.
        /// </summary>
        public void RemoveDataImpressionGroup(Guid uuid)
        {
            dataImpressionGroups[uuid].Clear();
            Destroy(dataImpressionGroups[uuid].GroupRoot);
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

        /// <summary>
        /// Retrieve the first data impression group found that is associated with a particular dataset.
        /// </summary>
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
        /// <returns>
        /// Boolean - true if the given group exists in the ABR scene, false otherwise.
        /// </returns>
        public bool HasDataImpressionGroup(Guid uuid)
        {
            return dataImpressionGroups.ContainsKey(uuid);
        }

        /// <summary>
        ///     Get the group a particular data impression
        /// </summary>
        public DataImpressionGroup GetGroupFromImpression(IDataImpression dataImpression)
        {
            try
            {
                return dataImpressionGroups
                    .Select((kv) => kv.Value)
                    .First((v) => dataImpression != null && v.HasDataImpression(dataImpression.Uuid));
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        ///     Add a new data impression, but add it to a specific group ID.
        /// </summary>
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
                        newGroup = AddDataImpressionGroup(ds.Path);
                    }
                    else
                    {
                        newGroup = AddDataImpressionGroup(string.Format("{0}", DateTimeOffset.Now.ToUnixTimeMilliseconds()));
                    }
                }
            }
            MoveImpressionToGroup(dataImpression, newGroup, allowOverwrite);
        }


        /// <summary>
        ///     Register a new data impression, or replace an existing one. If the
        ///     data impression has a dataset, defaults to placing it inside the
        ///     existing group with the same dataset, or creating a new
        ///     DataImpressionGroup with that dataset if no group exists yet.
        /// </summary>
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
        /// Remove a data impression from the ABR scene
        /// </summary>
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

        /// <summary>
        /// Remove all data impression groups from the ABR scene (and in turn, remove all data impressions).
        /// </summary>
        public void ClearState()
        {
            List<Guid> toRemove = new List<Guid>();
            foreach (var group in dataImpressionGroups)
            {
                group.Value.Clear();
                toRemove.Add(group.Key);
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
        /// Load a state into ABR.
        /// </summary>
        /// <remarks>
        /// NOTE: it is recommended to use `ABREngine.LoadStateAsync` instead of
        /// this method; this method provides no timing guarantees (e.g. that
        /// any data impressions would be initialized by the time it finishes).
        /// </remarks>
        public void LoadState<T>(string stateName)
        where T : IABRStateLoader, new()
        {
            // Kick off a task to load the new state, and make sure it's all in
            // the main thread. TODO there's definitely a better way to do this
            // and optimize what needs to be in the main thread and what
            // doesn't. Currently takes about 10 frames to get from here to the
            // end of the `RenderImpressions()` call.
            UnityThreadScheduler.Instance.KickoffMainThreadWork(async () =>
            {
                await LoadStateAsync<T>(stateName);
            });
        }

        /// <summary>
        /// Load a state into ABR, asynchronously. This Task is finished when:
        /// (1) All Data and VisAssets from the state have been loaded, (2) The
        /// ABR scene has been rendered with all updates, and (3) the
        /// OnStateChanged callback has been fired.
        /// </summary>
        /// <example>
        /// A state may be loaded from any of the following places:
        /// <code>
        /// // A Resources folder (in Assets or in a Package)
        /// await ABREngine.Instance.LoadStateAsync&lt;ResourceStateFileLoader&gt;("exampleState.json");
        ///
        /// // A web resource
        /// await ABREngine.Instance.LoadStateAsync&lt;HttpStateFileLoader&gt;("http://localhost:8000/api/state");
        ///
        /// // A local file
        /// await ABREngine.Instance.LoadStateAsync&lt;PathStateFileLoader&gt;("C:/Users/VRDemo/Desktop/test.json");
        ///
        /// // A JSON string
        /// await ABREngine.Instance.LoadStateAsync&lt;ResourceStateFileLoader&gt;("{\"version\": \"0.2.0\", \"name\": \"test\"}");
        /// </code>
        /// </example>
        public async Task LoadStateAsync<T>(string stateName)
        where T : IABRStateLoader, new()
        {
            lock (_stateUpdatingLock)
            {
                stateUpdating = true;
            }
            await UnityThreadScheduler.Instance.RunMainThreadWork(async () =>
            {
                try
                {
                    JObject tempState = await stateParser.LoadState<T>(stateName, previouslyLoadedState);
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
            });
        }

        /// <summary>
        /// Save a state from the ABR Unity scene back to a particular destination.
        /// </summary>
        /// <remarks>
        /// The SaveState functionality is only implemented in a few
        /// `IABRStateLoader`s, namely `PathStateFileLoader` and
        /// `HttpStateFileLoader`.
        /// </remarks>
        public async Task SaveStateAsync<T>(string overrideStateName = null)
        where T : IABRStateLoader, new()
        {
            if (overrideStateName != null)
            {
                previousStateName = overrideStateName;
            }
            T loader = new T();
            try
            {
                await UnityThreadScheduler.Instance.RunMainThreadWork(async () =>
                {
                    try
                    {
                        string state = stateParser.SerializeState(previouslyLoadedState);

                        await loader.SaveState(previousStateName, state);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}