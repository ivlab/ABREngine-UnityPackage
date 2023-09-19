/* SimpleGlyphDataImpression.cs
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
using UnityEngine;
using IVLab.Utilities;
using System.Linq;

namespace IVLab.ABREngine
{
    class InstancedSurfaceRenderInfo : IDataImpressionRenderInfo
    {
        public Matrix4x4[] transforms;
        public Vector4[] scalars;
        public Bounds bounds;
    }

    /// <summary>
    /// An Instanced Surface data impression (very similar to glyphs, except
    /// geometries are specified from data rather than VisAssets)
    /// 
    /// > [!NOTE]
    /// > This data impression type is not supported by the ABR design
    /// > interface, hence the lack of <see cref="ABRInput"/> annotations for its
    /// > instance variables.
    /// </summary>
    [ABRPlateType("Instanced Surface")]
    public class InstancedSurfaceDataImpression : DataImpression
    {
        /// <summary>
        /// KeyData for InstancedSurfaceDataImpression is an "unofficial" 5th
        /// type of <see cref="key-data.md"/> - instanced matrices. These key
        /// data have no geometry, only a single variable that is a series of
        /// 4x4 matrices. Key data can be changed by modifying a <see
        /// cref="RawDataset"/>'s <see cref="RawDataset.matrixArrays"/> and <see
        /// cref="RawDataset.matrixArrayNames"/>. The key data transforms (like
        /// every other key data in ABR) can be updated frame-by-frame so long
        /// as `<see cref="RenderHints.DataChanged"/>= true` is specified. For
        /// example, here we are spinning the transforms along the y axis.
        ///
        /// <img src="../resources/api/InstancedSurfaceDataImpression/keyData.gif"/>
        /// </summary>
        public KeyData keyData;

        /// <summary>
        /// The mesh to populate across all "instanced transforms" supplied by
        /// key data.
        ///
        /// <img src="../resources/api/InstancedSurfaceDataImpression/instanceMesh.gif"/>
        /// </summary>
        public Mesh instanceMesh;

        /// <summary>
        /// Override the color used for NaN values in this data impression. If
        /// not supplied, will use the <see cref="ABRConfig.defaultNanColor"/>.
        /// </summary>
        public IColormapVisAsset nanColor;

        /// <summary>
        /// Scalar color variable applied to each point of this data impression.
        /// This example switches between X-axis monotonically increasing and
        /// Y-axis monotonically increasing.
        ///
        /// <img src="../resources/api/InstancedSurfaceDataImpression/colorVariable.gif"/>
        /// </summary>
        public ScalarDataVariable colorVariable;

        /// <summary>
        /// Colormap applied to the <see cref="colorVariable"/>. This example
        /// switches between a linear white-to-green colormap and a linear
        /// black-to-white colormap.
        ///
        /// <img src="../resources/api/InstancedSurfaceDataImpression/colormap.gif"/>
        /// </summary>
        public IColormapVisAsset colormap;

        /// <summary>
        /// Show/hide outline on this data impression
        ///
        /// <img src="../resources/api/InstancedSurfaceDataImpression/showOutline.gif"/>
        /// </summary>
        public BooleanPrimitive showOutline;

        /// <summary>
        /// Width (in Unity world coords) of the outline
        ///
        /// <img src="../resources/api/InstancedSurfaceDataImpression/outlineWidth.gif"/>
        /// </summary>
        public LengthPrimitive outlineWidth;

        /// <summary>
        /// Color of the outline (when <see cref="forceOutlineColor"/> is `true`
        /// or there's no <see cref="colormap"/>/<see cref="colorVariable"/>)
        ///
        /// <img src="../resources/api/InstancedSurfaceDataImpression/outlineColor.gif"/>
        /// </summary>
        public Color outlineColor;

        /// <summary>
        /// Force the use of <see cref="outlineColor"/> even if there's a
        /// colormap applied to the data. This example alternates between a
        /// white-to-green linear colormap (false) and a solid purple-blue
        /// (true)
        ///
        /// <img src="../resources/api/InstancedSurfaceDataImpression/forceOutlineColor.gif"/>
        /// </summary>
        public BooleanPrimitive forceOutlineColor;

        /// <summary>
        ///    Compute buffer used to quickly pass per-glyph visibility flags to GPU
        /// </summary>
        private ComputeBuffer perGlyphVisibilityBuffer;

        protected override string[] MaterialNames { get; } = { "ABR_Glyphs", "ABR_GlyphsOutline" };

        /// <summary>
        /// Define the layer name for this Data Impression
        /// </summary>
        /// <remarks>
        /// > [!WARNING]
        /// > New Data Impressions should define a const string "LayerName"
        /// which corresponds to a Layer in Unity's Layer manager.
        /// </remarks>
        protected const string LayerName = "ABR_Glyph";

        public override Dataset GetDataset() => keyData?.GetDataset();
        public override KeyData GetKeyData() => keyData;
        public override void SetKeyData(KeyData kd) => keyData = kd;
        public override DataTopology GetKeyDataTopology() => DataTopology.Points;

        public override void ComputeGeometry()
        {
            if (keyData == null)
            {
                RenderInfo = new SimpleGlyphRenderInfo
                {
                    transforms = new Matrix4x4[0],
                    scalars = new Vector4[0],
                    bounds = new Bounds(),
                };
            }
            else
            {
                RawDataset dataset;
                if (!ABREngine.Instance.Data.TryGetRawDataset(keyData?.Path, out dataset))
                {
                    return;
                }
                DataImpressionGroup group = ABREngine.Instance.GetGroupFromImpression(this);

                int numPoints = dataset.matrixArrays[0].Length;

                var encodingRenderInfo = new SimpleGlyphRenderInfo()
                {
                    transforms = new Matrix4x4[numPoints],
                    scalars = new Vector4[numPoints]
                };

                Matrix4x4[] xforms = dataset.matrixArrays[0];
                for (int i = 0; i < numPoints; i++)
                {
                    encodingRenderInfo.transforms[i] = xforms[i];
                }

                // Apply room-space bounds to renderer
                encodingRenderInfo.bounds = group.GroupBounds;
                RenderInfo = encodingRenderInfo;
            }
        }

        public override void SetupGameObject()
        {
            if (gameObject == null)
            {
                // should never get here
                return;
            }

            // Ensure there's an ABR layer for this object
            int layerID = LayerMask.NameToLayer(LayerName);
            if (layerID >= 0)
            {
                gameObject.layer = layerID;
            }
            else
            {
                Debug.LogWarningFormat("Could not find layer {0} for SimpleGlyphDataImpression", LayerName);
            }

            // Add mesh renderers to child object
            if (!gameObject.TryGetComponent(out MeshRenderer mr))
            {
                mr = gameObject.AddComponent<MeshRenderer>();
            }
            if (!gameObject.TryGetComponent(out InstancedMeshRenderer imr))
            {
                imr = gameObject.AddComponent<InstancedMeshRenderer>();
            }

            // Setup instanced rendering based on computed geometry
            if (!(RenderInfo is SimpleGlyphRenderInfo SSrenderData))
            {
                Debug.LogWarning($"Instanced mesh renderer for Instanced Surface {this.Uuid} is null, skipping");
                return;
            }

            imr.bounds = SSrenderData.bounds;
            imr.instanceMaterial = ImpressionMaterials[0];
            imr.block = new MaterialPropertyBlock();
            imr.cachedInstanceCount = -1;
        }

        public override void UpdateStyling()
        {
            if (keyData == null)
            {
                return;
            }

            // Default to using every transform in the data (re-populate and discard old transforms)
            var SSrenderData = RenderInfo as SimpleGlyphRenderInfo;

            // Go through each child glyph renderer and render it
            InstancedMeshRenderer imr = gameObject?.GetComponent<InstancedMeshRenderer>();
            if (imr == null)
                return;

            // Set up outline, if present
            if (showOutline != null && showOutline.Value)
                imr.instanceMaterial = ImpressionMaterials[1];
            else
                imr.instanceMaterial = ImpressionMaterials[0];

            imr.instanceLocalTransforms = SSrenderData.transforms;
            imr.renderInfo = SSrenderData.scalars;

            // Determine the number of points / glyphs via the number of transforms the
            // instanced mesh renderer is currently tracking
            int numPoints = imr.instanceLocalTransforms.Length;

            // We might as well exit if there are no glyphs to update
            if (numPoints <= 0)
                return;

            // Create a new MaterialPropertyBlock for this specific glyph
            MaterialPropertyBlock block = new MaterialPropertyBlock();

            // Rescale the glyphs depending on their current "Glyph Size" input
            ABRConfig config = ABREngine.Instance.Config;
            string plateType = this.GetType().GetCustomAttribute<ABRPlateType>().plateType;

            // Update the instanced mesh renderer to use the currently selected glyph
            if (instanceMesh != null)
            {
                imr.instanceMesh = instanceMesh;
            }
            else
            {
                Mesh mesh = ABREngine.Instance.Config.defaultGlyph.GetComponent<MeshFilter>().sharedMesh;
                imr.instanceMesh = mesh;
            }

            // Initialize "render info" -- stores scalar values and info on whether
            // or not glyphs should be rendered
            Vector4[] glyphRenderInfo = new Vector4[numPoints];

            // Hide/show glyphs based on per index visibility
            if (RenderHints.HasPerIndexVisibility())
            {
                int[] perGlyphVisibility = new int[(numPoints - 1) / sizeof(int) + 1];
                RenderHints.PerIndexVisibility.CopyTo(perGlyphVisibility, 0);
                // Initialize the compute buffer if it is uninitialized
                if (perGlyphVisibilityBuffer == null)
                    perGlyphVisibilityBuffer = new ComputeBuffer(perGlyphVisibility.Length, sizeof(int), ComputeBufferType.Default);
                if (perGlyphVisibilityBuffer == null)
                    perGlyphVisibilityBuffer = new ComputeBuffer(perGlyphVisibility.Length, sizeof(int), ComputeBufferType.Default);
                // Set buffer data to int array and send to shader
                perGlyphVisibilityBuffer.SetData(perGlyphVisibility);
                block.SetBuffer("_PerGlyphVisibility", perGlyphVisibilityBuffer);
                block.SetInt("_HasPerGlyphVisibility", 1);
                block.SetBuffer("_PerGlyphVisibility", perGlyphVisibilityBuffer);
                block.SetInt("_HasPerGlyphVisibility", 1);
            }

            // Get keydata-specific range, if there is one
            float colorVariableMin = 0.0f;
            float colorVariableMax = 0.0f;
            if (colorVariable != null && colorVariable.IsPartOf(keyData))
            {
                if (colorVariable.SpecificRanges.ContainsKey(keyData.Path))
                {
                    colorVariableMin = colorVariable.SpecificRanges[keyData.Path].min;
                    colorVariableMax = colorVariable.SpecificRanges[keyData.Path].max;
                }
                else
                {
                    colorVariableMin = colorVariable.Range.min;
                    colorVariableMax = colorVariable.Range.max;
                }
            }

            // Apply and pack scalar variables
            // INDEX 0: Color
            if (colorVariable != null && colorVariable.IsPartOf(keyData))
            {
                var colorScalars = colorVariable.GetArray(keyData);
                for (int i = 0; i < numPoints; i++)
                {
                    glyphRenderInfo[i][0] = colorScalars[i];
                }
            }

            // Apply scalar/density changes to the instanced mesh renderer
            imr.instanceDensity = 1.0f;
            imr.renderInfo = glyphRenderInfo;

            // Apply changes to the mesh's shader / material
            block.SetFloat("_ColorDataMin", colorVariableMin);
            block.SetFloat("_ColorDataMax", colorVariableMax);
            block.SetColor("_Color", Color.white);
            block.SetColor("_OutlineColor", outlineColor);
            block.SetFloat("_OutlineWidth", outlineWidth?.Value ?? 0.0f);
            block.SetInt("_ForceOutlineColor", (forceOutlineColor?.Value ?? false) ? 1 : 0);

            if (colormap?.GetColorGradient() != null)
            {
                block.SetInt("_UseColorMap", 1);
                block.SetTexture("_ColorMap", colormap?.GetColorGradient());
                block.SetColor("_NaNColor", nanColor?.GetColorGradient().GetPixel(0, 0) ?? ABREngine.Instance.Config.defaultNanColor);
            }
            else
            {
                block.SetInt("_UseColorMap", 0);
            }

            imr.block = block;

            imr.cachedInstanceCount = -1;
        }

        public override void UpdateVisibility()
        {
            foreach (InstancedMeshRenderer imr in gameObject?.GetComponentsInChildren<InstancedMeshRenderer>())
            {
                if (imr != null)
                {
                    imr.enabled = RenderHints.Visible;
                }
            }
        }

        public override void Cleanup()
        {
            base.Cleanup();
            // Return all previous renderers to pool
            while (gameObject.transform.childCount > 0)
            {
                GameObject child = gameObject.transform.GetChild(0).gameObject;
                GenericObjectPool.Instance.ReturnObjectToPool(child);
            }
            perGlyphVisibilityBuffer.Release();
        }

        // Samples k glyphs, modifying glyph render info so that only they will be rendered
        // Uses reservoir sampling: (https://www.geeksforgeeks.org/reservoir-sampling/)
        private void SampleGlyphs(Vector4[] glyphRenderInfo, int k)
        {
            // Total number of glyphs
            int n = glyphRenderInfo.Length;

            // Index for elements in renderInfo
            int i;

            // Indices into renderInfo array for the glyphs that have been selected
            int[] idxReservoir = new int[k];


            // Select first k glyphs to begin
            for (i = 0; i < k; i++)
            {
                idxReservoir[i] = i;
                glyphRenderInfo[i][3] = 1;  // render the glyph
            }

            // Iterate through the remaining glyphs
            for (; i < n; i++)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                // Replace previous selections if the randomly
                // picked index is smaller than k
                if (j < k)
                {
                    glyphRenderInfo[idxReservoir[j]][3] = -1;  // discard the glyph
                    idxReservoir[j] = i;
                    glyphRenderInfo[i][3] = 1;  // render the glyph
                }
                // Otherwise unselect the glyph
                else
                {
                    glyphRenderInfo[i][3] = -1;  // discard the glyph
                }
            }
        }
    }
}