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
    ///     Public interface for a single ABR visualization layer
    /// </summary>
    public interface IDataImpression : IHasDataset
    {
        /// <summary>
        ///     Unique identifier for this Data Impression
        ///
        ///     Assigned on object creation
        /// </summary>
        Guid Uuid { get; set; }

        /// <summary>
        ///     Used for getting/setting ABRInputs on this DataImpression
        /// </summary>
        ABRInputIndexerModule InputIndexer { get; }

        /// <summary>
        ///     1. Populate rendering information (Geometry) for the
        ///     DataImpression. This is triggered by the `DataImpressionGroup`
        ///     when an `UpdateLevel.Data` happens. This step is generally *expensive*.
        /// </summary>
        void ComputeGeometry();

        /// <summary>
        ///     2. Take geometric rendering information computed in
        ///     `ComputeGeometry()` and sets up proper game object(s) and
        ///     components for this Data Impression. Transfers geometry into
        ///     Unity format (e.g. a `Mesh`). No geometric computations should
        ///     happen in this method, and it should generally be *lightweight*.
        /// </summary>
        void SetupGameObject(EncodedGameObject currentGameObject);

        /// <summary>
        ///     3. Update the "styling" of an impression by sending each
        ///     styling parameter to the shader. Occasionally will need to set
        ///     per-vertex items like transforms. This method should generally be *lightweight*.
        /// </summary>
        void UpdateStyling(EncodedGameObject currentGameObject);

        /// <summary>
        ///     Update the visibility of an impression (hidden or shown)
        /// </summary>
        void UpdateVisibility(EncodedGameObject currentGameObject);

        /// <summary>
        ///     Copy a data impression, giving a new Uuid
        /// </summary>
        IDataImpression Copy();

        /// <summary>
        /// Update this data impression from an existing (possibly temporary) one.
        /// </summary>
        void CopyExisting(IDataImpression other);

        /// <summary>
        /// When this data impression is done being used, clean up after itself
        /// if necessary. This method may need access to the GameObject the data
        /// impression is applied to.
        /// </summary>
        void Cleanup(EncodedGameObject encodedGameObject);

        /// <summary>
        ///     Return if this data impression has a particular string tag (for
        ///     external purposes only, the engine currently does nothing with tags)
        /// </summary>
        bool HasTag(string tagName);

        /// <summary>
        ///     Any hints to provide the rendering engine, such as if the impression
        ///     should be hidden
        /// </summary>
        RenderHints RenderHints { get; set; }
    }

    /// <summary>
    ///     Private data for a single data impression
    ///
    ///     Should contain properties with attributes for all of the inputs
    /// </summary>
    public abstract class DataImpression : IDataImpression, IHasDataset
    {
        public Guid Uuid { get; set; }

        public ABRInputIndexerModule InputIndexer { get; set; }

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
        protected virtual Material[] ImpressionMaterials { get; }

        /// <summary>
        ///     Storage for the rendering data to be sent to the shader
        /// </summary>
        protected virtual MaterialPropertyBlock MatPropBlock { get; set; }

        /// <summary>
        ///     Cache of current rendering information
        /// </summary>
        protected virtual IDataImpressionRenderInfo RenderInfo { get; set; }

        /// <summary>
        ///     Cache of current KeyData rendering information
        /// </summary>
        protected virtual IKeyDataRenderInfo KeyDataRenderInfo { get; set; }

        /// <summary>
        ///     The layer to put this data impression in
        ///
        ///     Warning: layer must exist in the Unity project!
        /// </summary>
        protected virtual string LayerName { get; } = "ABR";

        /// <summary>
        ///     Construct a data impession with a given UUID. Note that this
        ///     will be called from ABRState and must assume that there's a
        ///     single string argument with UUID - if you override this
        ///     constructor bad things might happen.
        /// </summary>
        public DataImpression(string uuid)
        {
            InputIndexer = new ABRInputIndexerModule(this);
            Uuid = new Guid(uuid);
            MatPropBlock = new MaterialPropertyBlock();

            // Initialize material list
            if (ImpressionMaterials == null)
            {
                ImpressionMaterials = new Material[MaterialNames.Length];
            }

            for (int m = 0; m < MaterialNames.Length; m++)
            {
                // Load each material, if it's not already loaded
                if (ImpressionMaterials[m] == null)
                {
                    ImpressionMaterials[m] = Resources.Load<Material>(MaterialNames[m]);
                }

                // If it's still null, that means we didn't find it
                if (ImpressionMaterials[m] == null)
                {
                    Debug.LogErrorFormat("Material `{0}` not found for {1}", MaterialNames[m], this.GetType().ToString());
                }
            }
        }

        public bool HasTag(string tag)
        {
            return Tags.Contains(tag);
        }

        public DataImpression() : this(Guid.NewGuid().ToString()) { }

        public virtual void ComputeGeometry() { }

        public virtual void SetupGameObject(EncodedGameObject currentGameObject) { }

        public virtual void UpdateStyling(EncodedGameObject currentGameObject) { }

        public virtual void UpdateVisibility(EncodedGameObject currentGameObject) { }

        /// <summary>
        ///     Unknown why it's necessary to copy each input individually, but here
        ///     we are.
        /// </summary>
        public virtual IDataImpression Copy()
        {
            DataImpression di = (DataImpression) this.MemberwiseClone();
            di.InputIndexer = new ABRInputIndexerModule(di);
            di.Tags = new List<string>(di.Tags);
            di.Uuid = Guid.NewGuid();
            di.RenderHints = this.RenderHints.Copy();
            return di as IDataImpression;
        }

        /// <summary>
        /// Update this data impression from an existing (possibly temporary) one.
        /// </summary>
        public virtual void CopyExisting(IDataImpression other)
        {
            this.Tags = new List<string>((other as DataImpression).Tags);
            this.RenderHints = other.RenderHints.Copy();
            foreach (string inputName in other.InputIndexer.InputNames)
            {
                IABRInput otherInput = other.InputIndexer.GetInputValue(inputName);
                this.InputIndexer.AssignInput(inputName, otherInput);
            }
        }

        public virtual void Cleanup(EncodedGameObject encodedGameObject)
        {
            RenderInfo = null;
        }

        /// <summary>
        ///     By default, there's no dataset. DataImpressions should only have
        ///     one dataset, and it's up to them individually to enforce that
        ///     they correctly implement this.
        /// </summary>
        public virtual Dataset GetDataset()
        {
            return null;
        }
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
        public bool DataChanged { get; set; } = false;

        /// <summary>
        ///     Has the style of the impression been changed
        /// </summary>
        public bool StyleChanged { get; set; } = false;

        /// <summary>
        ///     Has the visibility of the impression been changed (mesh renderer needs to be toggled)
        /// </summary>
        public bool VisibilityChanged { get; set; } = false;

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