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
    /// </summary>
    [ABRPlateType("Instanced Surface")]
    public class InstancedSurfaceDataImpression : DataImpression, IDataImpression
    {
        [ABRInput("Key Data", "Key Data", UpdateLevel.Data)]
        public KeyData keyData;

        public Mesh instanceMesh;

        [ABRInput("Color Variable", "Color", UpdateLevel.Style)]
        public ScalarDataVariable colorVariable;

        [ABRInput("Colormap", "Color", UpdateLevel.Style)]
        public IColormapVisAsset colormap;

        protected override string[] MaterialNames { get; } = { "ABR_Glyphs" };
        protected override string LayerName { get; } = "ABR_Glyph";

        /// <summary>
        ///     Construct a data impession with a given UUID. Note that this
        ///     will be called from ABRState and must assume that there's a
        ///     single string argument with UUID.
        /// </summary>
        public InstancedSurfaceDataImpression(string uuid) : base(uuid) { }
        public InstancedSurfaceDataImpression() : base() { }

        public override Dataset GetDataset()
        {
            return keyData?.GetDataset();
        }

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

        public override void SetupGameObject(EncodedGameObject currentGameObject)
        {
            var SSrenderData = RenderInfo as SimpleGlyphRenderInfo;
            if (currentGameObject == null)
            {
                return;
            }

            // Ensure there's an ABR layer for this object
            int layerID = LayerMask.NameToLayer(LayerName);
            if (layerID >= 0)
            {
                currentGameObject.gameObject.layer = layerID;
            }
            else
            {
                Debug.LogWarningFormat("Could not find layer {0} for SimpleGlyphDataImpression", LayerName);
            }

            // Return all previous renderers to pool
            while (currentGameObject.transform.childCount > 0)
            {
                GameObject child = currentGameObject.transform.GetChild(0).gameObject;
                GenericObjectPool.Instance.ReturnObjectToPool(child);
            }

            // Create pooled game objects with mesh renderer and instanced mesh renderer
            GameObject childRenderer = GenericObjectPool.Instance.GetObjectFromPool(this.GetType() + "GlyphRenderer", currentGameObject.transform, (go) =>
            {
                go.name = "Glyph Renderer Object " + 0;
            });
            childRenderer.transform.parent = currentGameObject.transform;

            // Add mesh renderers to child object
            MeshRenderer mr = null;
            InstancedMeshRenderer imr = null;
            if (!childRenderer.TryGetComponent<MeshRenderer>(out mr))
            {
                mr = childRenderer.gameObject.AddComponent<MeshRenderer>();
            }
            if (!childRenderer.TryGetComponent<InstancedMeshRenderer>(out imr))
            {
                imr = childRenderer.gameObject.AddComponent<InstancedMeshRenderer>();
            }

            // Setup instanced rendering based on computed geometry
            if (SSrenderData == null)
            {
                Debug.LogWarning($"Instanced mesh renderer for Instanced Surface {this.Uuid} is null, skipping");
                return;
            }

            imr.bounds = SSrenderData.bounds;
            imr.instanceMaterial = ImpressionMaterials[0];
            imr.block = new MaterialPropertyBlock();
            imr.cachedInstanceCount = -1;
        }

        public override void UpdateStyling(EncodedGameObject currentGameObject)
        {
            // Default to using every transform in the data (re-populate and discard old transforms)
            var SSrenderData = RenderInfo as SimpleGlyphRenderInfo;

            // Go through each child glyph renderer and render it
            InstancedMeshRenderer imr = currentGameObject?.transform.GetChild(0).GetComponent<InstancedMeshRenderer>();
            if (imr == null)
                return;

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
                for (int i = 0; i < numPoints && i < RenderHints.PerIndexVisibility.Count; i++)
                {
                    if (RenderHints.PerIndexVisibility[i])
                        glyphRenderInfo[i][3] = 1;
                    else
                        glyphRenderInfo[i][3] = -1;
                }
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

            if (colormap?.GetColorGradient() != null)
            {
                block.SetInt("_UseColorMap", 1);
                block.SetTexture("_ColorMap", colormap?.GetColorGradient());
            }
            else
            {
                block.SetInt("_UseColorMap", 0);
            }

            imr.block = block;

            imr.cachedInstanceCount = -1;
        }

        public override void UpdateVisibility(EncodedGameObject currentGameObject)
        {
            foreach (InstancedMeshRenderer imr in currentGameObject?.GetComponentsInChildren<InstancedMeshRenderer>())
            {
                if (imr != null)
                {
                    imr.enabled = RenderHints.Visible;
                }
            }
        }

        public override void Cleanup(EncodedGameObject currentGameObject)
        {
            base.Cleanup(currentGameObject);
            // Return all previous renderers to pool
            while (currentGameObject.transform.childCount > 0)
            {
                GameObject child = currentGameObject.transform.GetChild(0).gameObject;
                GenericObjectPool.Instance.ReturnObjectToPool(child);
            }
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