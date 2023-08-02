/* DataImpressionGroup.cs
 *
 * Copyright (c) 2023 University of Minnesota
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
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

using IVLab.Utilities;

namespace IVLab.ABREngine
{
    /// <summary>
    /// A DataImpressionGroup is a group of data
    /// impressions within ABR. DataImpressionGroups can be constrained within
    /// a defined bounding box (see <see cref="ABRDataContainer"/>), and can
    /// automatically rescale all of their data to stay within this container.
    /// Each time a new <see cref="KeyData"/> object is loaded into a data impression in
    /// this group, the GroupToDataMatrix and GroupBounds are updated.
    /// </summary>
    /// <remarks>
    /// DataImpressionGroups cannot be constructed directly, you MUST use the a
    /// variation of the <see cref="ABREngine.CreateDataImpressionGroup"/>
    /// method.
    /// </remarks>
    [AddComponentMenu("ABR/Data Impression Group")]
    public class DataImpressionGroup : MonoBehaviour, IHasDataset
    {
        /// <summary>
        ///     Transformation from the original data space into the room-scale
        ///     bounds. Multiply by a vector to go from group-space into data-space.
        /// </summary>
        public Matrix4x4 GroupToDataMatrix;

        /// <summary>
        ///     The actual bounds (contained within DataContainer) of the
        ///     group-scale dataset
        /// </summary>
        public Bounds GroupBounds;

        /// <summary>
        ///     Unique identifier for this group
        /// </summary>
        public Guid Uuid { get; private set; }


        private Dictionary<Guid, IDataImpression> _impressions = new Dictionary<Guid, IDataImpression>();
        private Dictionary<Guid, EncodedGameObject> gameObjectMapping = new Dictionary<Guid, EncodedGameObject>();

        internal static DataImpressionGroup Create(string name)
        {
            return Create(name, Guid.NewGuid(), null, Matrix4x4.identity);
        }

        internal static DataImpressionGroup Create(string name, Bounds? containerBounds)
        {
            return Create(name, Guid.NewGuid(), containerBounds, Matrix4x4.identity);
        }

        internal static DataImpressionGroup Create(string name, Guid uuid, Bounds? containerBounds, Matrix4x4 localMatrix)
        {
            // First, try to find any DataImpressionGroups that might exist in the scene already and see if any match name
            DataImpressionGroup groupInScene = null;
            foreach (DataImpressionGroup group in MonoBehaviour.FindObjectsOfType<DataImpressionGroup>())
            {
                if (group.name == name)
                {
                    groupInScene = group;
                }
            }

            // If it was found, we just use the position/rotation/hierarchy of the already existing GameObject.
            // If it wasn't found, create one under ABREngine. Use the defined position/rotation/parent.
            if (groupInScene == null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(ABREngine.Instance.transform);
                groupInScene = go.AddComponent<DataImpressionGroup>();

                go.transform.localPosition = localMatrix.ExtractPosition();
                go.transform.localRotation = localMatrix.ExtractRotation();
                go.transform.localScale = localMatrix.ExtractScale();
            }

            groupInScene.Uuid = uuid;
            groupInScene.name = name;

            // Set the bounds, if defined
            if (containerBounds.HasValue)
            {
                ABRDataContainer container;
                if (!groupInScene.TryGetComponent<ABRDataContainer>(out container))
                {
                    container = groupInScene.gameObject.AddComponent<ABRDataContainer>();
                }
                container.bounds = containerBounds.Value;
            }

            groupInScene.ResetBoundsAndTransformation();

            return groupInScene;
        }

        /// <summary>
        /// Add a data impression to this group. All data impressions in the
        /// same group NEED to have the same dataset, error will be displayed
        /// otherwise.
        /// </summary>
        public void AddDataImpression(IDataImpression impression, bool allowOverwrite = true)
        {
            // Make sure the new impression matches the rest of the impressions'
            // datasets. ImpressionsGroups MUST have only one dataset.
            Dataset ds = GetDataset();
            Dataset impressionDs = impression.GetDataset();
            if (impressionDs != null && ds != null && ds?.Path != impressionDs?.Path)
            {
                Debug.LogErrorFormat("Refusing to add DataImpression with a different dataset than this DataImpressionGroup's dataset:\nExpected: {0}\nGot: {1}", ds?.Path, impressionDs?.Path);
                return;
            }

            if (HasDataImpression(impression.Uuid))
            {
                if (allowOverwrite)
                {
                    // Instead of actually assigning a completely new
                    // DataImpression, copy the temporary one's inputs and let
                    // the temp be GC'd.
                    _impressions[impression.Uuid].CopyExisting(impression);
                }
                else
                {
                    Debug.LogWarningFormat("Skipping register data impression (already exists): {0}", impression.Uuid);
                    return;
                }
            }
            else
            {
                _impressions[impression.Uuid] = impression;
                GameObject impressionGameObject = new GameObject();
                impressionGameObject.transform.parent = this.transform;
                impressionGameObject.name = impression.GetType().ToString();

                EncodedGameObject ego = impressionGameObject.AddComponent<EncodedGameObject>();
                gameObjectMapping[impression.Uuid] = ego;
            }

            PrepareImpression(impression);
        }

        /// <summary>
        ///     Remove data impression, returning true if this data impression group is
        ///     empty after the removal of such impression.
        /// </summary>
        public bool RemoveDataImpression(Guid uuid)
        {
            if (_impressions.ContainsKey(uuid))
            {
                _impressions[uuid].Cleanup(gameObjectMapping[uuid]);
                _impressions.Remove(uuid);
                GameObject.Destroy(gameObjectMapping[uuid].gameObject);
                gameObjectMapping.Remove(uuid);
            }
            return _impressions.Count == 0;
        }

        /// <summary>
        /// Get a data impression by its UUID
        /// </summary>
        /// <returns>
        /// The data impression, if found, otherwise `null`
        /// </returns>
        public IDataImpression GetDataImpression(Guid uuid)
        {
            IDataImpression dataImpression = null;
            _impressions.TryGetValue(uuid, out dataImpression);
            return dataImpression;
        }

        
        /// <summary>
        /// Get a data impression matching a particular criteria
        /// </summary>
        /// <example>
        /// This method can be used to access data impressions in a functional
        /// manner, for example checking if the impression has a particular
        /// colormap assigned.
        /// <code>
        /// DataImpressionGroup group;
        /// group.GetDataImpression((di) =>
        /// {
        ///     try
        ///     {
        ///         SimpleSurfaceDataImpression sdi = di as SimpleSurfaceDataImpression;
        ///         return sdi.colormap.Uuid == new Guid("5a761a72-8bcb-11ea-9265-005056bae6d8");
        ///     }
        ///     catch
        ///     {
        ///         return null;
        ///     }
        /// });
        /// </code>
        /// </example>
        /// <returns>
        /// The data impression, if found, otherwise `null`
        /// </returns>
        public IDataImpression GetDataImpression(Func<IDataImpression, bool> criteria)
        {
            return GetDataImpressions(criteria).FirstOrDefault();
        }

        /// <summary>
        /// Get a data impression matching a type AND a particular criteria
        /// </summary>
        /// <example>
        /// This method can be used as a more elegant way to access individual
        /// types of data impressions.
        /// <code>
        /// DataImpressionGroup group;
        /// group.GetDataImpression&lt;SimpleSurfaceDataImpression&gt;((di) =>
        /// {
        ///     // di is already a SimpleSurfaceDataImpression
        ///     return sdi.colormap.Uuid == new Guid("5a761a72-8bcb-11ea-9265-005056bae6d8");
        /// });
        /// </code>
        /// </example>
        public T GetDataImpression<T>(Func<T, bool> criteria)
        where T : IDataImpression
        {
            return GetDataImpressions<T>(criteria).FirstOrDefault();
        }

        /// <summary>
        /// Get a data impression matching a type
        /// </summary>
        public T GetDataImpression<T>()
        where T : IDataImpression
        {
            return GetDataImpressions<T>().FirstOrDefault();
        }

        /// <summary>
        /// Return whether or not the data impression with a given UUID is present in this DataImpressionGroup
        /// </summary>
        public bool HasDataImpression(Guid uuid)
        {
            return _impressions.ContainsKey(uuid);
        }

        /// <summary>
        /// Return the Unity GameObject associated with this particular UUID.
        /// </summary>
        public EncodedGameObject GetEncodedGameObject(Guid uuid)
        {
            EncodedGameObject dataImpression = null;
            gameObjectMapping.TryGetValue(uuid, out dataImpression);
            return dataImpression;
        }

        /// <summary>
        /// Get all data impressions in this group that match a particular type (e.g. get all <see cref="SimpleSurfaceDataImpression"/>s).
        /// </summary>
        [Obsolete("GetDataImpressionsOfType<T> is obsolete, use GetDataImpressions<T> instead")]
        public List<T> GetDataImpressionsOfType<T>()
        where T : IDataImpression
        {
            return _impressions
                .Select((kv) => kv.Value)
                .Where((imp) => imp.GetType().IsAssignableFrom(typeof(T)))
                .Select((imp) => (T) imp).ToList();
        }

        /// <summary>
        /// Get all data impressions that have a particular tag. Tags can be any
        /// string value. They are not used internally to the engine but can be
        /// useful for keeping track of data impressions in applications that
        /// use ABR.
        /// </summary>
        public List<IDataImpression> GetDataImpressionsWithTag(string tag)
        {
            return _impressions
                .Select((kv) => kv.Value)
                .Where((imp) => imp.HasTag(tag)).ToList();
        }


        /// <summary>
        /// Check to see if a data impression with a particular UUID has a GameObject yet
        /// </summary>
        public bool HasEncodedGameObject(Guid uuid)
        {
            return gameObjectMapping.ContainsKey(uuid);
        }

        /// <summary>
        /// Return all data impressions inside this data impression group
        /// </summary>
        public Dictionary<Guid, IDataImpression> GetDataImpressions()
        {
            return _impressions;
        }

        /// <summary>
        /// Return all data impressions that match a particular criteria
        /// </summary>
        public List<IDataImpression> GetDataImpressions(Func<IDataImpression, bool> criteria)
        {
            return _impressions.Values.Where(criteria).ToList();
        }

        /// <summary>
        /// Return all data impressions that have a particular type
        /// </summary>
        public List<T> GetDataImpressions<T>()
        where T : IDataImpression
        {
            return _impressions
                .Select((kv) => kv.Value)
                .Where((imp) => imp.GetType().IsAssignableFrom(typeof(T)))
                .Select((imp) => (T) imp).ToList();
        }

        /// <summary>
        /// Return all data impressions that match a particular criteria AND have a particular type
        /// </summary>
        public List<T> GetDataImpressions<T>(Func<T, bool> criteria)
        where T : IDataImpression
        {
            return GetDataImpressions<T>().Where(criteria).ToList();
        }

        /// <summary>
        /// Remove all data impressions from this DataImpressionGroup
        /// </summary>
        public void Clear()
        {
            List<Guid> toRemove = _impressions.Keys.ToList();
            foreach (var impressionUuid in toRemove)
            {
                RemoveDataImpression(impressionUuid);
            }
        }

        /// <summary>
        ///     Get the dataset that all impressions in this DataImpressionGroup are
        ///     associated with. All DataImpressionGroups MUST have only one dataset.
        /// </summary>
        public Dataset GetDataset()
        {
            foreach (var impression in _impressions)
            {
                Dataset impressionDs = impression.Value.GetDataset();
                // Find the first one that exists and return it
                if (impressionDs != null)
                {
                    return impressionDs;
                }
            }
            return null;
        }

        /// <summary>
        /// Get the bounds of the container containing all the data in this DataImpressionGroup
        /// </summary>
        public Bounds GetContainerBounds()
        {
            // If we're using auto data containers, try to find one attached to
            // the same object as the DataImpressionGroup:
            ABRDataContainer containerInEditor = this.GetComponent<ABRDataContainer>();
            Bounds groupContainer;
            if (containerInEditor != null)
            {
                // ... if found, use it
                groupContainer = containerInEditor.bounds;
            }
            else
            {
                // ... otherwise, use default data container
                groupContainer = ABREngine.Instance.Config.defaultDataContainer;
            }
            return groupContainer;
        }

        /// <summary>
        ///     From scratch, recalculate the bounds of this DataImpressionGroup. Start with
        ///     a zero-size bounding box and expand until it encapsulates all
        ///     datasets.
        /// </summary>
        /// <returns>
        /// Returns a boolean whether or not the bounds have changed since last recalculation
        /// </returns>
        public bool RecalculateBounds()
        {
            // If user specified to not use data containers, skip the rest and
            // don't auto-calculate new bounds
            if (!ABREngine.Instance.Config.useAutoDataContainers)
            {
                GroupToDataMatrix = Matrix4x4.identity;
                return false;
            }

            Bounds groupContainer = GetContainerBounds();

            float currentBoundsSize = GroupBounds.size.magnitude;
            ResetBoundsAndTransformation();

            Dataset ds = GetDataset();
            if (ds != null)
            {
                // Build a list of keydata that are actually being used
                List<string> activeKeyDataPaths = new List<string>();
                foreach (IDataImpression impression in GetDataImpressions().Values)
                {
                    string keyDataPath = impression.InputIndexer.GetInputValue("Key Data")?.GetRawABRInput().inputValue;
                    if (keyDataPath != null && DataPath.GetDatasetPath(keyDataPath) == ds.Path)
                    {
                        activeKeyDataPaths.Add(keyDataPath);
                    }
                }

                foreach (IKeyData keyData in ds.GetAllKeyData().Values)
                {
                    if (!activeKeyDataPaths.Contains(keyData.Path))
                    {
                        continue;
                    }
                    RawDataset rawDataset;
                    if (!ABREngine.Instance.Data.TryGetRawDataset(keyData.Path, out rawDataset))
                    {
                        continue;
                    }
                    Bounds originalBounds = rawDataset.bounds;

                    if (ds.DataSpaceBounds.size.magnitude <= float.Epsilon)
                    {
                        // If the size is zero (first keyData), then start with its
                        // bounds (make sure to not assume we're including (0, 0, 0) in
                        // the bounds)
                        ds.DataSpaceBounds = originalBounds;
                        NormalizeWithinBounds.Normalize(groupContainer, originalBounds, out GroupToDataMatrix, out GroupBounds);
                    }
                    else
                    {
                        NormalizeWithinBounds.NormalizeAndExpand(
                            groupContainer,
                            originalBounds,
                            ref GroupBounds,
                            ref GroupToDataMatrix,
                            ref ds.DataSpaceBounds
                        );
                    }
                }
            }

            return Mathf.Abs(currentBoundsSize - GroupBounds.size.magnitude) > float.Epsilon;
        }

        /// <summary>
        /// Render every data impression inside this data impression group. Three levels of "update" are provided for each data impression (see <see cref="RenderHints"/> for more information):
        /// <ol>
        ///     <li>Recompute everything if the data source has changed (geometry, style, visibility)</li>
        ///     <li>Only recompute style if only the style (variables, visassets, etc.) has changed</li>
        ///     <li>Only toggle visibility if only that has changed</li>
        /// </ol>
        /// </summary>
        public void RenderImpressions()
        {
            try
            {
                // Make sure the bounding box is correct
                // Mostly matters if there's a live ParaView connection
                bool boundsChanged = RecalculateBounds();

                foreach (var impression in _impressions)
                {
                    // Fully compute render info and apply it to the impression object
                    // if (key) data was changed
                    if (boundsChanged || impression.Value.RenderHints.DataChanged)
                    {
                        PrepareImpression(impression.Value);
                        impression.Value.ComputeGeometry();
                        Guid uuid = impression.Key;
                        impression.Value.SetupGameObject(gameObjectMapping[uuid]);
                        impression.Value.UpdateStyling(gameObjectMapping[uuid]);
                        impression.Value.UpdateVisibility(gameObjectMapping[uuid]);
                        impression.Value.RenderHints.DataChanged = false;
                        impression.Value.RenderHints.StyleChanged = false;
                    }
                    // Compute and apply style info to the impression object if its
                    // styling has changed (but only if we haven't already performed 
                    // data changed computations since those inherently update styling)
                    else if (impression.Value.RenderHints.StyleChanged)
                    {
                        Guid uuid = impression.Key;
                        impression.Value.UpdateStyling(gameObjectMapping[uuid]);
                        impression.Value.RenderHints.StyleChanged = false;
                    }
                    // Set the visibility of the impression if it has been changed
                    if (impression.Value.RenderHints.VisibilityChanged)
                    {
                        Guid uuid = impression.Key;
                        impression.Value.UpdateVisibility(gameObjectMapping[uuid]);
                        impression.Value.RenderHints.VisibilityChanged = false;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error while rendering impressions");
                Debug.LogError(e);
            }
        }

        private void ResetBoundsAndTransformation()
        {
            GroupToDataMatrix = Matrix4x4.identity;
            GroupBounds = new Bounds();
            Dataset ds = GetDataset();
            if (ds != null)
            {
                ds.DataSpaceBounds = new Bounds();
            }
        }

        private void PrepareImpression(IDataImpression impression)
        {
            // Make sure the parent is assigned properly
            gameObjectMapping[impression.Uuid].gameObject.transform.SetParent(this.transform, false);
            
            // Unsure why this needs to be explicitly set but here it is,
            // zeroing position and rotation so each data impression encoded
            // game object is centered on the dataset...
            gameObjectMapping[impression.Uuid].gameObject.transform.localPosition = Vector3.zero;
            gameObjectMapping[impression.Uuid].gameObject.transform.localRotation = Quaternion.identity;

            // Display the UUID in editor
            gameObjectMapping[impression.Uuid].Uuid = impression.Uuid;
        }
    }
}
