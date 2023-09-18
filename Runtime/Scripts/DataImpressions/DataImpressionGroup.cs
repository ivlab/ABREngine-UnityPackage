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
    public class DataImpressionGroup : MonoBehaviour, IHasDataset, ICoordSpaceConverter
    {
#region Member variables
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


        private Dictionary<Guid, DataImpression> gameObjectMapping = new Dictionary<Guid, DataImpression>();
#endregion

#region Constructor (Create) methods
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
                
                // Check if we should use the bounds found in the state or the bounds defined in-editor
                if (!container.overwriteStateBounds)
                {
                    container.bounds = containerBounds.Value;
                }
            }

            groupInScene.ResetBoundsAndTransformation();

            return groupInScene;
        }
#endregion

#region Public member methods
        /// <summary>
        /// Add a data impression to this group. All data impressions in the
        /// same group NEED to have the same dataset, error will be displayed
        /// otherwise.
        /// </summary>
        public void AddDataImpression(DataImpression impression, bool allowOverwrite = true)
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
                    gameObjectMapping[impression.Uuid].CopyExisting(impression);
                }
                else
                {
                    Debug.LogWarningFormat("Skipping register data impression (already exists): {0}", impression.Uuid);
                    return;
                }
            }
            else
            {
                gameObjectMapping[impression.Uuid] = impression;
            }

            PrepareImpression(impression);
        }

        /// <summary>
        ///     Remove data impression, returning true if this data impression group is
        ///     empty after the removal of such impression.
        /// </summary>
        public bool RemoveDataImpression(Guid uuid)
        {
            if (gameObjectMapping.ContainsKey(uuid))
            {
                // gameObjectMapping[uuid].Cleanup();
                // GameObject.Destroy(gameObjectMapping[uuid].gameObject);
                gameObjectMapping[uuid].gameObject.SetActive(false);
                gameObjectMapping.Remove(uuid);
            }
            return gameObjectMapping.Count == 0;
        }

        /// <summary>
        /// Get a data impression by its UUID
        /// </summary>
        /// <returns>
        /// The data impression, if found, otherwise `null`
        /// </returns>
        public DataImpression GetDataImpression(Guid uuid)
        {
            gameObjectMapping.TryGetValue(uuid, out DataImpression dataImpression);
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
        public DataImpression GetDataImpression(Func<DataImpression, bool> criteria)
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
        where T : DataImpression
        {
            return GetDataImpressions<T>(criteria).FirstOrDefault();
        }

        /// <summary>
        /// Get a data impression matching a type
        /// </summary>
        public T GetDataImpression<T>()
        where T : DataImpression
        {
            return GetDataImpressions<T>().FirstOrDefault();
        }

        /// <summary>
        /// Return whether or not the data impression with a given UUID is present in this DataImpressionGroup
        /// </summary>
        public bool HasDataImpression(Guid uuid)
        {
            return gameObjectMapping.ContainsKey(uuid);
        }

        /// <summary>
        /// Get all data impressions in this group that match a particular type (e.g. get all <see cref="SimpleSurfaceDataImpression"/>s).
        /// </summary>
        [Obsolete("GetDataImpressionsOfType<T> is obsolete, use GetDataImpressions<T> instead")]
        public List<T> GetDataImpressionsOfType<T>()
        where T : DataImpression
        {
            return gameObjectMapping
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
        public List<DataImpression> GetDataImpressionsWithTag(string tag)
        {
            return gameObjectMapping
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
        public Dictionary<Guid, DataImpression> GetDataImpressions()
        {
            return gameObjectMapping;
        }

        /// <summary>
        /// Return all data impressions that match a particular criteria
        /// </summary>
        public List<DataImpression> GetDataImpressions(Func<DataImpression, bool> criteria)
        {
            return gameObjectMapping.Values.Where(criteria).ToList();
        }

        /// <summary>
        /// Return all data impressions that have a particular type
        /// </summary>
        public List<T> GetDataImpressions<T>()
        where T : DataImpression
        {
            return gameObjectMapping
                .Select((kv) => kv.Value)
                .Where((imp) => imp.GetType().IsAssignableFrom(typeof(T)))
                .Select((imp) => (T) imp).ToList();
        }

        /// <summary>
        /// Return all data impressions that match a particular criteria AND have a particular type
        /// </summary>
        public List<T> GetDataImpressions<T>(Func<T, bool> criteria)
        where T : DataImpression
        {
            return GetDataImpressions<T>().Where(criteria).ToList();
        }

        /// <summary>
        /// Remove all data impressions from this DataImpressionGroup
        /// </summary>
        public void Clear()
        {
            List<Guid> toRemove = gameObjectMapping.Keys.ToList();
            foreach (var impressionUuid in toRemove)
            {
                RemoveDataImpression(impressionUuid);
            }
        }

        /// <summary>
        /// Get the bounds of the container containing all the data in this DataImpressionGroup
        /// </summary>
        public bool TryGetContainerBoundsInGroupSpace(out Bounds container)
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
                // ... otherwise, use default data container if we're using auto data containers
                if (ABREngine.Instance.Config.useAutoDataContainers)
                {
                    groupContainer = ABREngine.Instance.Config.defaultDataContainer;
                }
                else
                {
                    container = new Bounds();
                    return false;
                }
            }
            container = groupContainer;
            return true;
        }

        public bool TryGetContainerBoundsInWorldSpace(out Bounds container)
        {
            if (TryGetContainerBoundsInGroupSpace(out container))
            {
                container.center = this.transform.localToWorldMatrix.MultiplyPoint3x4(container.center);
                container.extents = this.transform.localToWorldMatrix.MultiplyVector(container.extents);
                return true;
            }
            else
                return false;
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
            Bounds groupContainer;

            // Skip recalculating bounds if there's not an ABRDataContainer on
            // this group AND the ABRConfig has auto data containers disabled
            if (!TryGetContainerBoundsInGroupSpace(out groupContainer))
            {
                GroupToDataMatrix = Matrix4x4.identity;
                return false;
            }

            float currentBoundsSize = GroupBounds.size.magnitude;
            ResetBoundsAndTransformation();

            Dataset ds = GetDataset();
            if (ds != null)
            {
                // Build a list of keydata that are actually being used
                List<string> activeKeyDataPaths = new List<string>();
                foreach (DataImpression impression in GetDataImpressions().Values)
                {
                    string keyDataPath = impression.InputIndexer.GetInputValue("Key Data")?.GetRawABRInput().inputValue;
                    if (keyDataPath != null && DataPath.GetDatasetPath(keyDataPath) == ds.Path)
                    {
                        activeKeyDataPaths.Add(keyDataPath);
                    }
                }

                foreach (KeyData keyData in ds.GetAllKeyData().Values)
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

                foreach (var impression in gameObjectMapping)
                {
                    // Fully compute render info and apply it to the impression object
                    // if (key) data was changed
                    if (boundsChanged || impression.Value.RenderHints.DataChanged)
                    {
                        PrepareImpression(impression.Value);
                        impression.Value.ComputeGeometry();
                        Guid uuid = impression.Key;
                        impression.Value.SetupGameObject();
                        impression.Value.UpdateStyling();
                        impression.Value.UpdateVisibility();
                        impression.Value.RenderHints.DataChanged = false;
                        impression.Value.RenderHints.StyleChanged = false;
                    }
                    // Compute and apply style info to the impression object if its
                    // styling has changed (but only if we haven't already performed 
                    // data changed computations since those inherently update styling)
                    else if (impression.Value.RenderHints.StyleChanged)
                    {
                        Guid uuid = impression.Key;
                        impression.Value.UpdateStyling();
                        impression.Value.RenderHints.StyleChanged = false;
                    }
                    // Set the visibility of the impression if it has been changed
                    if (impression.Value.RenderHints.VisibilityChanged)
                    {
                        Guid uuid = impression.Key;
                        impression.Value.UpdateVisibility();
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
#endregion

#region Private helper methods
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

        private void PrepareImpression(DataImpression impression)
        {
            // Make sure the parent is assigned properly
            impression.transform.SetParent(this.transform, false);
            
            // Unsure why this needs to be explicitly set but here it is,
            // zeroing position and rotation so each data impression encoded
            // game object is centered on the dataset...
            impression.transform.localPosition = Vector3.zero;
            impression.transform.localRotation = Quaternion.identity;
        }
#endregion

#region IHasDataset implementation
        /// <summary>
        ///     Get the dataset that all impressions in this DataImpressionGroup are
        ///     associated with. All DataImpressionGroups MUST have only one dataset.
        /// </summary>
        public Dataset GetDataset()
        {
            foreach (var impression in gameObjectMapping)
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
#endregion

#region ICoordSpaceConverter implementation
        /// <summary>
        /// Get the bounds of this data impression group in Unity world space.
        /// </summary>
        /// <remarks>
        /// > [!INFO]
        /// > If the data impression group has a transform applied, this returns
        /// > different bounds than those specified by the <see cref="GroupBounds"/>.
        /// </remarks>
        public Bounds BoundsInWorldSpace
        {
            get
            {
                Vector3 dataCenter = transform.localToWorldMatrix.MultiplyPoint3x4(GroupBounds.center);
                Vector3 dataSize = transform.localToWorldMatrix.MultiplyVector(GroupBounds.size);
                return new Bounds(dataCenter, dataSize);
            }
        }

        public Bounds BoundsInDataSpace
        {
            get
            {
                Vector3 dataCenter = GroupToDataMatrix.MultiplyPoint3x4(GroupBounds.center);
                Vector3 dataSize = GroupToDataMatrix.MultiplyVector(GroupBounds.size);
                return new Bounds(dataCenter, dataSize);
            }
        }

        /// <summary>
        /// Transforms from world space to data space (the Data Impression Group's dataset space)
        ///
        /// World Space ==(transform.worldToLocalMatrix)=> Group local space ==(GroupToDataMatrix)==> Data space
        /// </summary>
        public Matrix4x4 WorldToDataMatrix { get => GroupToDataMatrix.inverse * this.transform.worldToLocalMatrix; }

        /// <summary>
        /// Transforms from data space (Data Impression Group's data space) to world space
        ///
        /// Data Space ==(DataToGroupMatrix)=> Group local space ==(transform.localToWorldMatrix)==> World space
        /// </summary>
        public Matrix4x4 DataToWorldMatrix { get => this.transform.localToWorldMatrix * GroupToDataMatrix; }

        public Vector3 WorldSpacePointToDataSpace(Vector3 worldSpacePoint) => WorldToDataMatrix.MultiplyPoint3x4(worldSpacePoint);

        public Vector3 DataSpacePointToWorldSpace(Vector3 dataSpacePoint) => DataToWorldMatrix.MultiplyPoint3x4(dataSpacePoint);

        public Vector3 WorldSpaceVectorToDataSpace(Vector3 worldSpaceVector) => WorldToDataMatrix.MultiplyVector(worldSpaceVector);

        public Vector3 DataSpaceVectorToWorldSpace(Vector3 dataSpaceVector) => DataToWorldMatrix.MultiplyVector(dataSpaceVector);

        public bool ContainsWorldSpacePoint(Vector3 worldSpacePoint) => BoundsInWorldSpace.Contains(worldSpacePoint);

        public bool ContainsDataSpacePoint(Vector3 dataSpacePoint) => BoundsInDataSpace.Contains(dataSpacePoint);
#endregion

#region IDataAccessor implementation
        // public DataPoint GetClosestDataInWorldSpace(Vector3 worldSpacePoint);

        // public DataPoint GetClosestDataInDataSpace(Vector3 dataSpacePoint);

        // public List<DataPoint> GetNearbyDataInWorldSpace(Vector3 worldSpacePoint, float radiusInWorldSpace);

        // public List<DataPoint> GetNearbyDataInDataSpace(Vector3 dataSpacePoint, float radiusInDataSpace);

        // public float GetScalarValueAtClosestWorldSpacePoint(Vector3 point, ScalarDataVariable variable, KeyData keyData = null);
        // public float GetScalarValueAtClosestWorldSpacePoint(Vector3 point, string variableName, KeyData keyData = null);

        // public float GetScalarValueAtClosestDataSpacePoint(Vector3 point, ScalarDataVariable variable, KeyData keyData = null);
        // public float GetScalarValueAtClosestDataSpacePoint(Vector3 point, string variableName, KeyData keyData = null);

        // public Vector3 GetVectorValueAtClosestWorldSpacePoint(Vector3 point, VectorDataVariable variable, KeyData keyData = null);
        // public Vector3 GetVectorValueAtClosestWorldSpacePoint(Vector3 point, string variableName, KeyData keyData = null);

        // public Vector3 GetVectorValueAtClosestDataSpacePoint(Vector3 point, VectorDataVariable variable, KeyData keyData = null);
        // public Vector3 GetVectorValueAtClosestDataSpacePoint(Vector3 point, string variableName, KeyData keyData = null);

        // public float NormalizeScalarValue(float value, KeyData keyData, ScalarDataVariable variable);
#endregion
    }
}
