/* SimpleSurfaceDataImpression.cs
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

namespace IVLab.ABREngine
{
    public class SimpleSurfaceRenderInfo : IDataImpressionRenderInfo
    {
        public Vector3[] vertices;
        public int[] indices;
        public Vector3[] normals;
        public Color[] scalars;
        public DataTopology topology;
    }


    /// <summary>
    /// A "Surfaces" data impression that uses hand-drawn textures and color to show surface data.
    /// </summary>
    /// <example>
    /// An example of creating a single surface data impression and setting its colormap, color variable, and texture could be:
    /// <code>
    /// SimpleSurfaceDataImpression gi = new SimpleSurfaceDataImpression();
    /// gi.keyData = surfs;
    /// gi.colorVariable = yAxis;
    /// gi.colormap = ABREngine.Instance.VisAssets.GetDefault&lt;ColormapVisAsset&gt;() as ColormapVisAsset;
    /// gi.lineTexture = tex;
    /// ABREngine.Instance.RegisterDataImpression(gi);
    /// </code>
    /// </example>
    [ABRPlateType("Surfaces")]
    public class SimpleSurfaceDataImpression : DataImpression
    {
        [ABRInput("Key Data", "Key Data", UpdateLevel.Data)]
        public KeyData keyData;

        /// <summary>
        /// Scalar color variable applied to each point of this data impression.
        /// This example switches between X-axis monotonically increasing and
        /// Y-axis monotonically increasing.
        ///
        /// <img src="../resources/api/SimpleSurfaceDataImpression/colorVariable.gif"/>
        /// </summary>
        [ABRInput("Color Variable", "Color", UpdateLevel.Style)]
        public ScalarDataVariable colorVariable;

        /// <summary>
        /// Colormap applied to the <see cref="colorVariable"/>. This example
        /// switches between a linear white-to-green colormap and a linear
        /// black-to-white colormap.
        ///
        /// <img src="../resources/api/SimpleSurfaceDataImpression/colormap.gif"/>
        /// </summary>
        [ABRInput("Colormap", "Color", UpdateLevel.Style)]
        public IColormapVisAsset colormap;

        /// <summary>
        /// Override the color used for NaN values in this data impression. If
        /// not supplied, will use the <see cref="ABRConfig.defaultNanColor"/>.
        /// </summary>
        public IColormapVisAsset nanColor;


        /// <summary>
        /// Scalar variable used to vary the pattern across the surface.
        /// </summary>
        [ABRInput("Pattern Variable", "Pattern", UpdateLevel.Style)]
        public ScalarDataVariable patternVariable;

        /// <summary>
        /// The pattern/texture applied to the surface - can also be a <see cref="SurfaceTextureGradient"/>.
        ///
        /// <img src="../resources/api/SimpleSurfaceDataImpression/pattern.gif"/>
        /// </summary>
        [ABRInput("Pattern", "Pattern", UpdateLevel.Style)]
        public ISurfaceTextureVisAsset pattern;

        /// <summary>
        /// Override the pattern/texture used for NaN values in this data impression. If
        /// not supplied, will use the <see cref="ABRConfig.defaultNanTexture"/>.
        /// </summary>
        public ISurfaceTextureVisAsset nanPattern;

        /// <summary>
        /// How large, in Unity meters, to make each "tile" of the
        /// texture/pattern on the surface. This example goes from 0.5m to 1m.
        ///
        /// <img src="../resources/api/SimpleSurfaceDataImpression/patternSize.gif"/>
        /// </summary>
        [ABRInput("Pattern Size", "Pattern", UpdateLevel.Style)]
        public LengthPrimitive patternSize;

        /// <summary>
        /// Percentage to "blend" textures together at the seams to minimize the
        /// tiling effect. This example goes from 0% seam blend to 20% seam
        /// blend.
        ///
        /// <img src="../resources/api/SimpleSurfaceDataImpression/patternSeamBlend.gif"/>
        /// </summary>
        [ABRInput("Pattern Seam Blend", "Pattern", UpdateLevel.Style)]
        public PercentPrimitive patternSeamBlend;

        /// <summary>
        /// Edit the saturation of the pattern(s) - 100% is full color, 0% is
        /// full grayscale.
        ///
        /// <img src="../resources/api/SimpleSurfaceDataImpression/patternSaturation.gif"/>
        /// </summary>
        [ABRInput("Pattern Saturation", "Pattern", UpdateLevel.Style)]
        public PercentPrimitive patternSaturation;

        /// <summary>
        /// Edit the intensity which the pattern is overlaid on the surface. 0%
        /// is not present at all, 10% is very faint, and 100% is full overlay.
        ///
        /// <img src="../resources/api/SimpleSurfaceDataImpression/patternIntensity.gif"/>
        /// </summary>
        [ABRInput("Pattern Intensity", "Pattern", UpdateLevel.Style)]
        public PercentPrimitive patternIntensity;

        // TODO: Integrate this with schema.
        // TODO: Opacity is not 100% correct, especially when working in tandem
        // with Volumes (volumes always render in front of transparent surfaces)
        /// <summary>
        /// Opacity of the surface - how see-through the surface is.
        ///
        /// <img src="../resources/api/SimpleSurfaceDataImpression/opacity.gif"/>
        /// </summary>
        public PercentPrimitive opacity;

        // TODO: There's not yet a good way to display a transparent surface
        // w/outline (not sure if we care about this, probs not)
        /// <summary>
        /// Show/hide outline on this data impression (show the outline AND the
        /// actual surface)
        ///
        /// <img src="../resources/api/SimpleSurfaceDataImpression/showOutline.gif"/>
        /// </summary>
        /// <remarks>
        /// NOTE: Outlines work best on convex objects. The wavelet in this
        /// example shows some artifacts due to its concavity.
        /// </remarks>
        public BooleanPrimitive showOutline;

        /// <summary>
        /// Width (in Unity world coords) of the outline
        ///
        /// <img src="../resources/api/SimpleSurfaceDataImpression/outlineWidth.gif"/>
        /// </summary>
        public LengthPrimitive outlineWidth;

        /// <summary>
        /// Color of the outline
        ///
        /// <img src="../resources/api/SimpleSurfaceDataImpression/outlineColor.gif"/>
        /// </summary>
        public Color outlineColor;

        /// <summary>
        /// ONLY show the outline (don't show the actual surface)
        ///
        /// <img src="../resources/api/SimpleSurfaceDataImpression/onlyOutline.gif"/>
        /// </summary>
        public BooleanPrimitive onlyOutline;

        protected override string[] MaterialNames { get; } = { "ABR_SurfaceOpaque", "ABR_SurfaceTransparent", "ABR_SurfaceOutlineOnly", "ABR_SurfaceOutline" };

        /// <summary>
        /// Define the layer name for this Data Impression
        /// </summary>
        /// <remarks>
        /// > [!WARNING]
        /// > New Data Impressions should define a const string "LayerName"
        /// which corresponds to a Layer in Unity's Layer manager.
        /// </remarks>
        protected const string LayerName = "ABR_Surface";


        // Whether or not to render the back faces of the mesh
        private bool backFace = true;

        public override Dataset GetDataset() => keyData?.GetDataset();
        public override KeyData GetKeyData() => keyData;
        public override void SetKeyData(KeyData kd) => keyData = kd;

        public override void ComputeGeometry()
        {
            SimpleSurfaceRenderInfo renderInfo = null;

            if (keyData == null)
            {
                renderInfo = new SimpleSurfaceRenderInfo
                {
                    vertices = new Vector3[0],
                    normals = null,
                    indices = new int[0],
                    scalars = new Color[0],
                    topology = DataTopology.Points
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

                int sourceVertCount = dataset.vertexArray.Length;
                int sourceIndexCount = dataset.indexArray.Length;

                int numPoints = sourceVertCount;
                int numIndices = sourceIndexCount;

                if (backFace == true)
                {
                    numPoints *= 2;
                    numIndices *= 2;
                }


                renderInfo = new SimpleSurfaceRenderInfo
                {
                    vertices = new Vector3[numPoints],
                    indices = new int[numIndices],
                    scalars = new Color[numPoints],
                    normals = null,
                    topology = dataset.dataTopology
                };

                int numCells = dataset.cellIndexCounts.Length;
                int cellSize = dataset.dataTopology == DataTopology.Quads ? 4 : 3;

                for (int i = 0; i < sourceVertCount; i++)
                {
                    renderInfo.vertices[i] = group.GroupToDataMatrix * dataset.vertexArray[i].ToHomogeneous();
                }

                // Backfaces 
                for (int i = sourceVertCount, j = 0; i < numPoints; i++, j++)
                {
                    renderInfo.vertices[i] = group.GroupToDataMatrix * dataset.vertexArray[j].ToHomogeneous();
                }

                Vector3[] dataNormals = null;
                // Vector3[] meshNormals = null;
                // if (ABRManager.IsValidNode(normalVariable))
                // {
                //     dataNormals = normalVariable.GetVectorArray(dataset);
                // }
                //else if((generateNormals?.floatVal??0) <= 0.0f)
                //{
                //    dataNormals = dataset.GetVectorArray("Normals");
                //}

                if (dataNormals != null)
                {
                    renderInfo.normals = new Vector3[numPoints];

                    for (int i = 0; i < sourceVertCount; i++)
                        renderInfo.normals[i] = dataNormals[i];

                    // Backfaces 
                    for (int i = sourceVertCount, j = 0; i < numPoints; i++, j++)
                        renderInfo.normals[i] = -dataNormals[j];

                }

                if (colorVariable != null && colorVariable.IsPartOf(keyData))
                {
                    var colorScalars = colorVariable.GetArray(keyData);
                    for (int i = 0; i < sourceVertCount; i++)
                        renderInfo.scalars[i][0] = colorScalars[i];

                    // Back faces
                    for (int i = sourceVertCount, j = 0; i < numPoints; i++, j++)
                        renderInfo.scalars[i][0] = colorScalars[j];
                }

                if (patternVariable != null && patternVariable.IsPartOf(keyData))
                {
                    var scalars = patternVariable.GetArray(keyData);
                    for (int i = 0; i < sourceVertCount; i++)
                        renderInfo.scalars[i][1] = scalars[i];

                    // Back faces
                    for (int i = sourceVertCount, j = 0; i < numPoints; i++, j++)
                        renderInfo.scalars[i][1] = scalars[j];
                }

                for (int c = 0, i = 0; c < numCells; c++)
                {
                    for (int p = 0; p < cellSize; p++, i++)
                    {
                        renderInfo.indices[i] = dataset.indexArray[i];
                    }
                }

                //meshIndices[(i + numCells) * cellSize + j] = dataset.indexArray[(i) * cellSize + (cellSize - 1 - j)] + dataset.vertexArray.Length;


                if (backFace)
                {
                    for (int c = 0, i = sourceIndexCount; c < numCells; c++)
                    {
                        for (int p = 0, rev_p = cellSize - 1; p < cellSize; p++, rev_p--, i++)
                        {
                            renderInfo.indices[i] = dataset.indexArray[c * cellSize + rev_p] + sourceVertCount;
                        }

                    }
                }
            }
            RenderInfo = renderInfo;
        }

        public override void SetupGameObject()
        {
            if (gameObject == null)
            {
                // should never get here
                return;
            }

            // Setup mesh renderer and mesh filter
            MeshFilter meshFilter = null;
            MeshRenderer meshRenderer = null;
            if (!gameObject.TryGetComponent<MeshFilter>(out meshFilter))
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            if (!gameObject.TryGetComponent<MeshRenderer>(out meshRenderer))
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            // Ensure we have a layer to work with
            int layerID = LayerMask.NameToLayer(LayerName);
            if (layerID >= 0)
            {
                gameObject.layer = layerID;
            }
            else
            {
                Debug.LogWarningFormat("Could not find layer {0} for SimpleSurfaceDataImpression", LayerName);
            }

            // Populate surface mesh from calculated geometry
            var SSrenderData = RenderInfo as SimpleSurfaceRenderInfo;
            if (SSrenderData != null)
            {
                Mesh mesh = meshFilter.mesh;
                if (mesh == null) mesh = new Mesh();
                mesh.Clear();
                mesh.name = "SS:289@" + System.DateTime.Now.ToString();

                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = SSrenderData.vertices;
                mesh.colors = SSrenderData.scalars;
                mesh.SetIndices(SSrenderData.indices, (MeshTopology)SSrenderData.topology, 0);
                if (SSrenderData.normals != null)
                {
                    mesh.normals = SSrenderData.normals;
                }
                else
                {
                    mesh.RecalculateNormals();
                }
                mesh.RecalculateTangents();
                mesh.UploadMeshData(false);

                meshFilter.mesh = mesh;
            }
        }

        public override void UpdateStyling()
        {
            // Return immediately if the game object, mesh filter, or mesh renderer do not exist
            // (this should only really happen if the gameobject/renderers for this impression have not yet been initialized,
            // which equivalently indicates that KeyData has yet to be applied to this impression and therefore there 
            // is no point in styling it anyway)
            MeshFilter meshFilter = gameObject?.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = gameObject?.GetComponent<MeshRenderer>();
            if (meshFilter == null || meshRenderer == null)
            {
                return;
            }

            // The mesh we wish to update the styling of (which we expect to exist if we've made it this far)
            Mesh mesh = meshFilter.mesh;    

            // Following the same convention as ComputeRenderInfo() above, we determine the number of
            // vertices in the mesh (taking into account whether back faces are enabled)
            int numPoints = mesh.vertexCount;
            int sourceVertCount = numPoints;
            if (backFace == true)
            {
                sourceVertCount /= 2;
            }

            // Initialize variables to track scalar "styling" changes
            Color[] scalars = new Color[numPoints];
            Vector4 scalarMin = Vector4.zero;
            Vector4 scalarMax = Vector4.zero;

            // Record changes to color scalars if any occured
            if (colorVariable != null && colorVariable.IsPartOf(keyData))
            {
                var colorScalars = colorVariable.GetArray(keyData);
                for (int i = 0; i < sourceVertCount; i++)
                    scalars[i][0] = colorScalars[i];

                // Back faces
                for (int i = sourceVertCount, j = 0; i < numPoints; i++, j++)
                    scalars[i][0] = colorScalars[j];

                // Get keydata-specific range, if there is one
                if (colorVariable?.SpecificRanges.ContainsKey(keyData.Path) == true)
                {
                    scalarMin[0] = colorVariable.SpecificRanges[keyData.Path].min;
                    scalarMax[0] = colorVariable.SpecificRanges[keyData.Path].max;
                }
                else
                {
                    scalarMin[0] = colorVariable.Range.min;
                    scalarMax[0] = colorVariable.Range.max;
                }
            }

            // Record changes to pattern scalars if any occured
            if (patternVariable != null && patternVariable.IsPartOf(keyData))
            {
                var patternScalars = patternVariable.GetArray(keyData);
                for (int i = 0; i < sourceVertCount; i++)
                    scalars[i][1] = patternScalars[i];

                // Back faces
                for (int i = sourceVertCount, j = 0; i < numPoints; i++, j++)
                    scalars[i][1] = patternScalars[j];

                // Get keydata-specific range, if there is one
                if (patternVariable?.SpecificRanges.ContainsKey(keyData.Path) == true)
                {
                    scalarMin[1] = patternVariable.SpecificRanges[keyData.Path].min;
                    scalarMax[1] = patternVariable.SpecificRanges[keyData.Path].max;
                }
                else
                {
                    scalarMin[1] = patternVariable.Range.min;
                    scalarMax[1] = patternVariable.Range.max;
                }
            }

            // Update the mesh to match recorded scalar changes
            mesh.name = "SSS:278@" + System.DateTime.Now.ToString();
            mesh.colors = scalars;
            mesh.UploadMeshData(false);
            meshFilter.mesh = mesh;

            // Opacity currently just uses the alpha channel of the shader's
            // _Color input
            Color defaultColor = ABREngine.Instance.Config.defaultColor;

            bool useOpaqueShader = true;

            // If we want to show both the actual surface AND outline, use regular outline shader
            if (showOutline != null && showOutline.Value)
            {
                // Use the "outline-only" shader if that's selected
                if (onlyOutline != null && onlyOutline.Value)
                    meshRenderer.material = ImpressionMaterials[2];
                else 
                    meshRenderer.material = ImpressionMaterials[3];
                useOpaqueShader = false;
            }

            // Set material based on opacity - if 100% opaque, use the regular
            // opaque shader. If <100% opaque, use transparent shader.
            if (opacity != null && opacity.Value < 1.0f)
            {
                meshRenderer.material = ImpressionMaterials[1];
                defaultColor.a = opacity.Value;
                useOpaqueShader = false;
            }

            // Use a regular opaque surface shader if nothing else is selected
            if (useOpaqueShader)
            {
                meshRenderer.material = ImpressionMaterials[0];
            }

            // Apply changes to the mesh's shader / material
            meshRenderer.GetPropertyBlock(MatPropBlock);
            MatPropBlock.SetColor("_Color", defaultColor);
            MatPropBlock.SetFloat("_ColorDataMin", scalarMin[0]);
            MatPropBlock.SetFloat("_ColorDataMax", scalarMax[0]);
            MatPropBlock.SetFloat("_PatternDataMin", scalarMin[1]);
            MatPropBlock.SetFloat("_PatternDataMax", scalarMax[1]);

            // Load defaults from configuration / schema
            ABRConfig config = ABREngine.Instance.Config;

            // Width appears double what it should be, so decrease to
            // maintain the actual real world distance
            string plateType = this.GetType().GetCustomAttribute<ABRPlateType>().plateType;

            float patternSizeOut = patternSize?.Value ??
                config.GetInputValueDefault<LengthPrimitive>(plateType, "Pattern Size").Value;

            float patternIntensityOut = patternIntensity?.Value ??
                config.GetInputValueDefault<PercentPrimitive>(plateType, "Pattern Intensity").Value;

            float patternSeamBlendOut = patternSeamBlend?.Value ??
                config.GetInputValueDefault<PercentPrimitive>(plateType, "Pattern Seam Blend").Value;

            float patternSaturationOut = patternSaturation?.Value ??
                config.GetInputValueDefault<PercentPrimitive>(plateType, "Pattern Saturation").Value;

            MatPropBlock.SetFloat("_PatternScale", patternSizeOut);
            MatPropBlock.SetFloat("_PatternIntensity", patternIntensityOut);
            MatPropBlock.SetFloat("_PatternDirectionBlend", 1.0f);
            MatPropBlock.SetFloat("_PatternBlendWidth", patternSeamBlendOut/2);
            MatPropBlock.SetFloat("_PatternSaturation", patternSaturationOut);

            MatPropBlock.SetColor("_OutlineColor", outlineColor);
            MatPropBlock.SetFloat("_OutlineWidth", outlineWidth?.Value ?? 0.0f);

            MatPropBlock.SetColor("_NaNColor", nanColor?.GetColorGradient().GetPixel(0, 0) ?? ABREngine.Instance.Config.defaultNanColor);

            if (patternVariable != null)
            {
                MatPropBlock.SetInt("_UsePatternVariable", 1);
            }
            else
            {
                MatPropBlock.SetInt("_UsePatternVariable", 0);

            }
            if (colormap?.GetColorGradient() != null)
            {
                MatPropBlock.SetInt("_UseColorMap", 1);
                MatPropBlock.SetTexture("_ColorMap", colormap?.GetColorGradient());
            }
            else
            {
                MatPropBlock.SetInt("_UseColorMap", 0);
            }
            try
            {
                if (pattern != null)
                {
                    MatPropBlock.SetInt("_UsePattern", 1);
                    MatPropBlock.SetTexture("_Pattern", pattern.BlendMaps.Textures);
                    MatPropBlock.SetTexture("_BlendMaps", pattern.BlendMaps.BlendMaps);
                    MatPropBlock.SetInt("_NumTex", pattern.VisAssetCount);

                    Texture2D nanFinal = nanPattern?.BlendMaps.Textures ?? ABREngine.Instance.Config.defaultNanTexture;
                    if (nanFinal != null)
                    {
                        MatPropBlock.SetTexture("_NaNPattern", nanFinal);
                        MatPropBlock.SetInt("_HasNaNPattern", 1);
                    }
                    else
                    {
                        MatPropBlock.SetInt("_HasNaNPattern", 0);
                    }
                    // MatPropBlock.SetTexture("_PatternNormal", pattern?.NormalMap);
                }
                else
                {
                    MatPropBlock.SetInt("_UsePattern", 0);
                    MatPropBlock.SetTexture("_Pattern", new Texture2D(10, 10));
                    // MatPropBlock.SetTexture("_PatternNormal", new Texture2D(10, 10));
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            meshRenderer.SetPropertyBlock(MatPropBlock);
        }

        public override void UpdateVisibility()
        {
            MeshRenderer mr = gameObject?.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = RenderHints.Visible;
            }
        }
    }
}