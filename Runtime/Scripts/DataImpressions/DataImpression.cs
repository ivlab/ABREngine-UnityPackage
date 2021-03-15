/* DataImpression.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using System;
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
        ///     Performs any pre-calculations necessary to render this
        ///     particular type of Key Data (for instance, the individual glyph
        ///     positions for the InstanceMeshRenderer used in glyph rendering)
        ///
        ///     Note: `ComputeKeyDataRenderInfo()`, `ComputeRenderInfo()`, and
        ///     `ApplyToGameObject()` should be run in sequence.
        /// </summary>
        void ComputeKeyDataRenderInfo();

        /// <summary>
        ///     Populates rendering information (Geometry) for the
        ///     DataImpression
        ///
        ///     Note: `ComputeKeyDataRenderInfo()`, `ComputeRenderInfo()`, and
        ///     `ApplyToGameObject()` should be run in sequence.
        /// </summary>
        void ComputeRenderInfo();

        /// <summary>
        ///     Applies a DataImpression to a particular GameObject
        ///
        ///     Note: `ComputeKeyDataRenderInfo()`, `ComputeRenderInfo()`, and
        ///     `ApplyToGameObject()` should be run in sequence.
        /// </summary>
        void ApplyToGameObject(EncodedGameObject currentGameObject);

        /// <summary>
        ///     Copy a data impression, giving a new Uuid
        /// </summary>
        IDataImpression Copy();

        /// <summary>
        ///     Return if this data impression has a particular string tag (for
        ///     external purposes only, the engine currently does nothing with tags)
        /// </summary>
        bool HasTag(string tagName);
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

        /// <summary>
        ///     A list of tags that this data impression has - solely used for
        ///     external purposes (the engine does nothing with them)
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        ///     Name of the material to use to render this DataImpression
        /// </summary>
        protected virtual string MaterialName { get; }

        /// <summary>
        ///     Slot to load the material into at runtime
        /// </summary>
        protected virtual Material ImpressionMaterial { get; }

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
        ///     Any hints to provide the rendering engine, such as if the impression
        ///     should be hidden
        /// </summary>
        public virtual RenderHints RenderHints { get; set; } = new RenderHints();

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
            ImpressionMaterial = Resources.Load<Material>(MaterialName);
            if (ImpressionMaterial == null)
            {
                Debug.LogWarningFormat("Material `{0}` not found for {1}", MaterialName, this.GetType().ToString());
            }
        }

        public bool HasTag(string tag)
        {
            return Tags.Contains(tag);
        }

        public DataImpression() : this(Guid.NewGuid().ToString()) { }

        public virtual void ComputeKeyDataRenderInfo() { }

        public virtual void ComputeRenderInfo() { }

        public virtual void ApplyToGameObject(EncodedGameObject currentGameObject) { }

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
            return di as IDataImpression;
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
        public bool visible = true;
    }
}