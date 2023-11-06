/* DataImpression.cs
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

using System;
using System.Reflection;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Main class for Data Impressions (layers) in an ABR visualization. Every
    /// Data Impression is a GameObject in the scene.
    /// </summary>
    public abstract class DataImpression : MonoBehaviour, IHasDataset, IHasKeyData, ICoordSpaceConverter
    {
#region Properties
        /// <summary>
        ///     Unique identifier for this Data Impression
        ///
        ///     Assigned on object creation
        /// </summary>
        public Guid Uuid { get; set; }

        /// <summary>
        ///     Used for getting/setting ABRInputs on this DataImpression
        /// </summary>
        public ABRInputIndexerModule InputIndexer { get; set; }

        /// <summary>
        ///     Any hints to provide the rendering engine, such as if the impression
        ///     should be hidden
        /// </summary>
        public RenderHints RenderHints { get; set; } = new RenderHints();

        /// <summary>
        ///     A list of tags that this data impression has - solely used for
        ///     external purposes (the engine does nothing with them)
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        ///     Name of the material to use to render this DataImpression
        /// </summary>
        protected virtual string[] MaterialNames { get; }

        /// <summary>
        ///     Slot to load the material into at runtime
        /// </summary>
        protected virtual Material[] ImpressionMaterials { get; set; }

        /// <summary>
        ///     Storage for the rendering data to be sent to the shader
        /// </summary>
        protected virtual MaterialPropertyBlock MatPropBlock { get; set; }

        /// <summary>
        ///     Cache of current rendering information
        /// </summary>
        public virtual IDataImpressionRenderInfo RenderInfo { get; set; }

        /// <summary>
        ///     Cache of current KeyData rendering information
        /// </summary>
        protected virtual IKeyDataRenderInfo KeyDataRenderInfo { get; set; }

        /// <summary>
        /// Save this DataImpression to state. Sometimes, it's desireable to
        /// create data impressions that *only* exist in the Unity Editor and
        /// DON'T get saved to state. By default, this is `false`. For data
        /// impressions created in <see cref="ABRStateParser"/>, it is `true`.
        /// </summary>
        public bool SaveToState { get; set; }

        /// <summary>
        /// Genres of ABRInput that are "styles" - i.e., they can apply to
        /// multiple data impressions
        /// </summary>
        private HashSet<ABRInputGenre> StyleGenres
        {
            get => new HashSet<ABRInputGenre>
            {
                ABRInputGenre.VisAsset,
                ABRInputGenre.Primitive,
                ABRInputGenre.PrimitiveGradient,
            };
        }

        /// <summary>
        /// Other data impressions that depend on this DI's style
        /// </summary>
        private HashSet<DataImpression> styleDependencies;

        /// <summary>
        /// Reflection: generic create method so it can be called/invoked with
        /// the correct args. Can't just call regular create() method because
        /// MonoBehaviours can't be abstract...
        /// when cloning
        /// </summary>
        private MethodInfo createMethod;
#endregion

#region MonoBehaviour methods

        void Awake()
        {
            Type impressionType = this.GetType();
            createMethod = typeof(DataImpression).GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            createMethod = createMethod.MakeGenericMethod(new Type[] { impressionType });
        }

        void Update()
        {
            // Immediately enable/disable the GameObject based on if it has KeyData or not
            this.gameObject.SetActive(GetKeyData() != null);
        }
#endregion

#region Constructor (Create) method
        /// <summary>
        /// Construct a data impession with a given UUID and name.
        ///     
        /// > [!WARNING]
        /// > This method will be called from <see cref="ABRStateParser"/> and MUST
        /// > have the given arguments. If you override this method, bad things
        /// > might happen.
        /// </summary>
        /// <param name="name">Non-unique, human-readable identifier for this data impression</param>
        /// <param name="uuid">Unique identifier for this data impression. If left empty, will create a new UUID.</param>
        /// <param name="saveToState">Should this data impression be saved when
        /// <see cref="ABREngine.SaveState{T}(string)"/> is called?</param>
        public static T Create<T>(string name, Guid uuid = new Guid(), bool saveToState = false)
        where T : DataImpression
        {
            // accept empty
            if (uuid == Guid.Empty)
            {
                uuid = Guid.NewGuid();
            }

            // Check if a data impression with this UUID already exists in the
            // ABREngine
            GameObject go;
            T di = ABREngine.Instance.GetDataImpression<T>(d => d.Uuid == uuid);
            if (di == null)
            {
                go = new GameObject();
                di = go.AddComponent<T>();
                go.name = name;

                di.InputIndexer = new ABRInputIndexerModule(di);
                di.Uuid = uuid;
                di.MatPropBlock = new MaterialPropertyBlock();
                di.SaveToState = saveToState;
                di.styleDependencies = new HashSet<DataImpression>();

                // Initialize material list
                if (di.ImpressionMaterials == null)
                {
                    di.ImpressionMaterials = new Material[di.MaterialNames.Length];
                }

                for (int m = 0; m < di.MaterialNames.Length; m++)
                {
                    // Load each material, if it's not already loaded
                    if (di.ImpressionMaterials[m] == null)
                    {
                        di.ImpressionMaterials[m] = Resources.Load<Material>(di.MaterialNames[m]);
                    }

                    // If it's still null, that means we didn't find it
                    if (di.ImpressionMaterials[m] == null)
                    {
                        Debug.LogErrorFormat("Material `{0}` not found for {1}", di.MaterialNames[m], di.GetType().ToString());
                    }
                }
            }

            return di;
        }

        // Users should NOT construct data impressions with `new DataImpression()`
        protected DataImpression() { }
#endregion

#region DataImpression methods
        /// <summary>
        /// Check if the DataImpression has a particular tag
        /// </summary>
        /// <param name="tag">The tag</param>
        /// <returns>Returns <see langword="true"/> if the data impression's tag list contains the specified tag</returns>
        public bool HasTag(string tag)
        {
            return Tags.Contains(tag);
        }

        /// <summary>
        /// Get the group that this <see cref="DataImpression"/> is a part of.
        /// </summary>
        /// <returns></returns>
        public DataImpressionGroup GetDataImpressionGroup()
        {
            return ABREngine.Instance.GetGroupFromImpression(this);
        }

        /// <summary>
        ///     RENDERING STEP 1. Populate rendering information (Geometry) for
        ///     the DataImpression. This is triggered by the <see
        ///     cref="DataImpressionGroup"/> when an <see
        ///     cref="UpdateLevel.Geometry"/> happens. This step is generally
        ///     *expensive*.
        /// </summary>
        public abstract void ComputeGeometry();

        /// <summary>
        ///     RENDERING STEP 2. Take geometric rendering information computed in
        ///     <see cref="ComputeGeometry()"/> and sets up proper game object(s) and
        ///     components for this Data Impression. Transfers geometry into
        ///     Unity format (e.g. a <see cref="Mesh"/>). No geometric computations should
        ///     happen in this method, and it should generally be *lightweight*.
        /// </summary>
        public abstract void SetupGameObject();


        /// <summary>
        ///     RENDERING STEP 3. Update the "styling" of an impression by sending each
        ///     styling parameter to the shader. Occasionally will need to set
        ///     per-vertex items like transforms. This method should generally be *lightweight*.
        /// </summary>
        public abstract void UpdateStyling();


        /// <summary>
        ///     RENDERING STEP 4. Update the visibility of an impression (hidden or shown)
        /// </summary>
        public abstract void UpdateVisibility();

        /// <summary>
        /// Get the index for the "packed" scalar variable in the <see cref="RenderInfo"/>
        /// </summary>
        // protected abstract int GetIndexForPackedScalarVariable(ScalarDataVariable variable);

        /// <summary>
        /// Create and return a copy of this data impression (with a new UUID)
        /// </summary>
        public DataImpression Clone()
        {
            // Clone (create a new Data impression) and copy over all the fields that don't come
            // with (due to Unity Serialization)
            object[] impressionArgs = new object[] { Guid.NewGuid(), this.name + " Copy", this.SaveToState };
            DataImpression newDi = createMethod.Invoke(null, impressionArgs) as DataImpression;

            // Copy over the fields that might have values
            newDi.RenderHints = this.RenderHints.Copy();
            newDi.Tags = new List<string>(this.Tags);

            // Copy all style inputs
            foreach (string inputName in this.InputIndexer.InputNames)
            {
                IABRInput thisInput = this.InputIndexer.GetInputValue(inputName);
                newDi.InputIndexer.AssignInput(inputName, thisInput);
            }

            return newDi;
        }

        /// <summary>
        /// Create a copy of this data impression, but only its "style" inputs.
        /// </summary>
        public DataImpression CloneStyle()
        {
            // Clone (create a new Data impression) and copy over all the fields that don't come
            // with (due to Unity Serialization)
            // Need to use reflection here because we can't create abstract MonoBehaviours
            object[] impressionArgs = new object[] { Guid.NewGuid(), this.name + " StyleCopy", this.SaveToState };
            DataImpression newDi = createMethod.Invoke(null, impressionArgs) as DataImpression;

            // Copy over the fields that might have values
            newDi.RenderHints = this.RenderHints.Copy();
            newDi.Tags = new List<string>();

            // Copy all style inputs
            foreach (string inputName in this.InputIndexer.InputNames)
            {
                IABRInput thisInput = this.InputIndexer.GetInputValue(inputName);
                if (thisInput != null && StyleGenres.Contains(thisInput.Genre))
                {
                    newDi.InputIndexer.AssignInput(inputName, thisInput);
                }
                else
                {
                    newDi.InputIndexer.AssignInput(inputName, null);
                }
            }

            return newDi;
        }

        /// <summary>
        /// Clone a data impression's style and link
        /// </summary>
        public DataImpression CloneStyleLinked()
        {
            DataImpression newDi = CloneStyle();
            newDi.name = this.name + " StyleLink";
            this.styleDependencies.Add(newDi);
            return newDi;
        }

        /// <summary>
        /// Update this data impression's fields from an existing data impression.
        /// </summary>
        public void CopyFrom(DataImpression other)
        {
            this.Tags = new List<string>(other.Tags);
            this.name = other.name;
            this.RenderHints = other.RenderHints.Copy();
            foreach (string inputName in other.InputIndexer.InputNames)
            {
                IABRInput otherInput = other.InputIndexer.GetInputValue(inputName);
                this.InputIndexer.AssignInput(inputName, otherInput);
            }
        }

        /// <summary>
        /// Update the style of this data impression from another data impression's style
        /// </summary>
        public void CopyStyleFrom(DataImpression other)
        {
            foreach (string inputName in other.InputIndexer.InputNames)
            {
                IABRInput otherInput = other.InputIndexer.GetInputValue(inputName);

                if (otherInput != null && StyleGenres.Contains(otherInput.Genre))
                {
                    this.InputIndexer.AssignInput(inputName, otherInput);
                }
            }
        }

        /// <summary>
        /// Copy the style from another data impression and link their styles so
        /// this data impression updates all its style dependencies when it is
        /// changed
        /// </summary>
        public void LinkStyleFrom(DataImpression other)
        {
            // Perform an initial copy
            CopyStyleFrom(other);

            // Add this as a dependency of `other`
            other.styleDependencies.Add(this);
        }

        /// <summary>
        /// Update all the style dependencies for this data impression - ensure
        /// they have the same styling and render hints.
        /// </summary>
        public void UpdateStyleDependencies()
        {
            foreach (DataImpression dependent in styleDependencies)
            {
                dependent.CopyStyleFrom(this);
                dependent.RenderHints = this.RenderHints.Copy();
            }
        }

        /// <summary>
        /// When this data impression is done being used, clean up after itself
        /// if necessary. This method may need access to the GameObject the data
        /// impression is applied to.
        /// </summary>
        public virtual void Cleanup()
        {
            RenderInfo = null;
        }
#endregion

#region IHasDataset implementation
        /// <summary>
        ///     By default, there's no dataset. DataImpressions should only have
        ///     one dataset, and it's up to them individually to enforce that
        ///     they correctly implement this.
        /// </summary>
        public abstract Dataset GetDataset();
#endregion

#region IHasKeyData implementation
        /// <summary>
        /// By default, there's no data. DataImpressions should only have
        /// one <see cref="KeyData"/>, and it's up to them individually to enforce that
        /// they correctly implement this.
        /// </summary>
        public abstract KeyData GetKeyData();

        /// <summary>
        /// By default, there's no data. DataImpressions should only have
        /// one <see cref="KeyData"/>, and it's up to them individually to enforce that
        /// they correctly implement this.
        /// </summary>
        public abstract void SetKeyData(KeyData kd);

        /// <summary>
        /// By default, there's no data. DataImpressions should only have
        /// one <see cref="KeyData"/>, and it's up to them individually to enforce that
        /// they correctly implement this.
        /// </summary>
        public abstract DataTopology GetKeyDataTopology();
#endregion

#region ICoordSpaceConverter implementation
        public Bounds BoundsInWorldSpace
        {
            get => new Bounds(
                DataToWorldMatrix.MultiplyPoint3x4(BoundsInDataSpace.center),
                DataToWorldMatrix.MultiplyVector(BoundsInDataSpace.size)
            );
        }

        public Bounds BoundsInDataSpace
        {
            get
            {
                RawDataset rds = this.GetKeyData()?.GetRawDataset();
                if (rds != null)
                    return rds.bounds;
                else
                    return new Bounds();
            }
        }

        public Matrix4x4 WorldToDataMatrix { get => GetDataImpressionGroup().WorldToDataMatrix; }

        public Matrix4x4 DataToWorldMatrix { get => GetDataImpressionGroup().DataToWorldMatrix; }

        public Vector3 WorldSpacePointToDataSpace(Vector3 worldSpacePoint) => WorldToDataMatrix.MultiplyPoint3x4(worldSpacePoint);

        public Vector3 DataSpacePointToWorldSpace(Vector3 dataSpacePoint) => DataToWorldMatrix.MultiplyPoint3x4(dataSpacePoint);

        public Vector3 WorldSpaceVectorToDataSpace(Vector3 worldSpaceVector) => WorldToDataMatrix.MultiplyVector(worldSpaceVector);

        public Vector3 DataSpaceVectorToWorldSpace(Vector3 dataSpaceVector) => DataToWorldMatrix.MultiplyVector(dataSpaceVector);

        public bool ContainsWorldSpacePoint(Vector3 worldSpacePoint) => BoundsInWorldSpace.Contains(worldSpacePoint);

        public bool ContainsDataSpacePoint(Vector3 dataSpacePoint) => BoundsInDataSpace.Contains(dataSpacePoint);
#endregion

#region IDataAccessor implementation
        // public abstract DataPoint GetClosestDataInWorldSpace(Vector3 worldSpacePoint);

        // public abstract DataPoint GetClosestDataInDataSpace(Vector3 dataSpacePoint);

        // public abstract List<DataPoint> GetNearbyDataInWorldSpace(Vector3 worldSpacePoint, float radiusInWorldSpace);

        // public abstract List<DataPoint> GetNearbyDataInDataSpace(Vector3 dataSpacePoint, float radiusInDataSpace);

        // public abstract float GetScalarValueAtClosestWorldSpacePoint(Vector3 point, ScalarDataVariable variable, KeyData keyData = null);
        // public abstract float GetScalarValueAtClosestWorldSpacePoint(Vector3 point, string variableName, KeyData keyData = null);

        // public abstract float GetScalarValueAtClosestDataSpacePoint(Vector3 point, ScalarDataVariable variable, KeyData keyData = null);
        // public abstract float GetScalarValueAtClosestDataSpacePoint(Vector3 point, string variableName, KeyData keyData = null);

        // public abstract Vector3 GetVectorValueAtClosestWorldSpacePoint(Vector3 point, VectorDataVariable variable, KeyData keyData = null);
        // public abstract Vector3 GetVectorValueAtClosestWorldSpacePoint(Vector3 point, string variableName, KeyData keyData = null);

        // public abstract Vector3 GetVectorValueAtClosestDataSpacePoint(Vector3 point, VectorDataVariable variable, KeyData keyData = null);
        // public abstract Vector3 GetVectorValueAtClosestDataSpacePoint(Vector3 point, string variableName, KeyData keyData = null);

        // public abstract float NormalizeScalarValue(float value, KeyData keyData, ScalarDataVariable variable);
#endregion
    }


    public interface IDataImpressionRenderInfo { }

    /// <summary>
    ///     Hints for rendering, such as whether a data impression should be hidden
    /// </summary>
    public class RenderHints
    {
        /// <summary>
        ///     Has the impression been changed since the last render (needs to be re-rendered?)
        /// </summary>
        public bool GeometryChanged { get; set; } = true;

        /// <summary>
        ///     Has the style of the impression been changed
        /// </summary>
        public bool StyleChanged { get; set; } = true;

        /// <summary>
        ///     Has the visibility of the impression been changed (mesh renderer needs to be toggled)
        /// </summary>
        public bool VisibilityChanged { get; set; } = true;

        /// <summary>
        ///    Whether or not the impression is visible
        /// </summary>
        public bool Visible
        {
            get
            {
                return visible;
            }
            set
            {
                // Toggle the "VisibilityChanged" flag if the new value different from the old
                if (visible != value)
                {
                    VisibilityChanged = true;
                    visible = value;
                }
            }
        }

        private bool visible = true;


        /// <summary>
        ///    Index-level toggle to control visibility of individual parts of a data impression (e.g., points, lines).
        /// </summary>
        /// <remarks>
        /// Index-level visibility may not be implemented for every data impression.
        /// </remarks>
        /// <example>
        /// The following example shows basic usage of per-index visibility on a simple glyph data impression:
        /// <code>
        /// public class IndexVisibilityExample : MonoBehaviour
        /// {
        ///     void Start()
        ///     {
        ///         // Let's say the key data has 42 points.
        ///         KeyData pointsKd = // some data we've imported
        ///
        ///         // Create a layer for "before" points (blue)
        ///         SimpleGlyphDataImpression di = new SimpleGlyphDataImpression();
        ///         di.keyData = // some key data we've loaded previously
        ///
        ///         // Default everything to invisible (visible = false)
        ///         di.RenderHints.PerIndexVisibility = new BitArray(42, false);
        ///
        ///         // Register impression with the engine and render
        ///         ABREngine.Instance.RegisterDataImpression(di);
        ///         ABREngine.Instance.Render();
        ///
        ///         // Then, if we wanted to set some index to visible:
        ///         di.RenderHints.PerIndexVisibility[10] = true;
        ///
        ///         // Note: we need to tell the impression that its style has changed and
        ///         // call Render() again
        ///         di.RenderHints.StyleChanged = true;
        ///         ABREngine.Instance.Render();
        ///     }
        /// }
        /// </code>
        /// </example>
        public BitArray PerIndexVisibility { get; set; } = null;

        /// <summary>
        ///    Whether or not the impression currently has per-index visibility
        /// </summary>
        public bool HasPerIndexVisibility()
        {
            return (PerIndexVisibility != null) && (PerIndexVisibility.Count > 0);
        }

        public RenderHints Copy()
        {
            return (RenderHints) this.MemberwiseClone();
        }
    }
}