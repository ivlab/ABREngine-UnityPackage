/* DataImpression.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using System;
using UnityEngine;

namespace IVLab.ABREngine
{
    /// <summary>
    ///     Public interface for a single ABR visualization layer
    /// </summary>
    public interface IDataImpression
    {
        /// <summary>
        ///     Unique identifier for this Data Impression
        ///
        ///     Assigned on object creation
        /// </summary>
        Guid Uuid { get; }

        /// <summary>
        ///     Populates rendering information (Geometry) for the
        ///     DataImpression
        /// </summary>
        void LoadRenderInfo();

        /// <summary>
        ///     Applies a DataImpression to a particular GameObject
        /// </summary>
        void ApplyToGameObject(EncodedGameObject currentGameObject);
    }

    /// <summary>
    ///     Private data for a single data impression
    ///
    ///     Should contain properties with attributes for all of the inputs
    /// </summary>
    public class DataImpression
    {
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
        ///     The layer to put this data impression in
        ///
        ///     Warning: layer must exist in the Unity project!
        /// </summary>
        protected virtual string LayerName { get; } = "ABR";

        public DataImpression()
        {
            MatPropBlock = new MaterialPropertyBlock();
            Debug.Log(Resources.FindObjectsOfTypeAll(typeof(Material)).Length);
            Debug.Log(MaterialName);
            ImpressionMaterial = Resources.Load<Material>(MaterialName);
            Debug.Log(ImpressionMaterial);
        }
    }


    public interface IDataImpressionRenderInfo { }
}