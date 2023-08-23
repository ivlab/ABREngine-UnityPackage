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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IVLab.ABREngine
{
    /// <summary>
    /// Main class for Data Impressions (layers) in an ABR visualization. Every
    /// Data Impression is a GameObject in the scene.
    /// </summary>
    public abstract class DataImpression : MonoBehaviour, IHasDataset, IHasKeyData
    {
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
        ///     Construct a data impession with a given UUID and name. Note that
        ///     this will be called from <see cref="ABRStateParser"/> and must
        ///     assume that there are two arguments with UUID and name - if you
        ///     override this method bad things might happen.
        /// </summary>
        public static T Create<T>(string uuid, string name)
        where T : DataImpression
        {
            GameObject go = new GameObject();
            T di = go.AddComponent<T>();
            go.name = name + " (" + di.GetType().Name + ")";

            di.InputIndexer = new ABRInputIndexerModule(di);
            di.Uuid = new Guid(uuid);
            di.MatPropBlock = new MaterialPropertyBlock();

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
            return di;
        }

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
        ///     RENDERING STEP 1. Populate rendering information (Geometry) for the
        ///     DataImpression. This is triggered by the `DataImpressionGroup`
        ///     when an `UpdateLevel.Data` happens. This step is generally *expensive*.
        /// </summary>
        public abstract void ComputeGeometry();

        /// <summary>
        ///     RENDERING STEP 2. Take geometric rendering information computed in
        ///     `ComputeGeometry()` and sets up proper game object(s) and
        ///     components for this Data Impression. Transfers geometry into
        ///     Unity format (e.g. a `Mesh`). No geometric computations should
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
        ///     Unknown why it's necessary to copy each input individually, but here
        ///     we are.
        /// </summary>
        public virtual DataImpression Copy()
        {
            DataImpression di = (DataImpression) this.MemberwiseClone();
            di.InputIndexer = new ABRInputIndexerModule(di);
            di.Tags = new List<string>(di.Tags);
            di.Uuid = Guid.NewGuid();
            di.RenderHints = this.RenderHints.Copy();
            return di;
        }

        /// <summary>
        /// Update this data impression from an existing (possibly temporary) one.
        /// </summary>
        public virtual void CopyExisting(DataImpression other)
        {
            this.Tags = new List<string>(other.Tags);
            this.RenderHints = other.RenderHints.Copy();
            foreach (string inputName in other.InputIndexer.InputNames)
            {
                IABRInput otherInput = other.InputIndexer.GetInputValue(inputName);
                this.InputIndexer.AssignInput(inputName, otherInput);
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

        /// <summary>
        ///     By default, there's no dataset. DataImpressions should only have
        ///     one dataset, and it's up to them individually to enforce that
        ///     they correctly implement this.
        /// </summary>
        public abstract Dataset GetDataset();

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
        public bool DataChanged { get; set; } = true;

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