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
    class SimpleSurfaceRenderInfo : IDataImpressionRenderInfo
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
    /// gi.keyData = surfs as SurfaceKeyData;
    /// gi.colorVariable = yAxis;
    /// gi.colormap = ABREngine.Instance.VisAssets.GetDefault&lt;ColormapVisAsset&gt;() as ColormapVisAsset;
    /// gi.lineTexture = tex as SurfaceTextureVisAsset;
    /// ABREngine.Instance.RegisterDataImpression(gi);
    /// </code>
    /// </example>
    [ABRPlateType("Surfaces")]
    public class SimpleSurfaceDataImpression : DataImpression, IDataImpression
    {
        [ABRInput("Key Data", "Key Data", UpdateLevel.Data)]
        public SurfaceKeyData keyData;

        [ABRInput("Color Variable", "Color", UpdateLevel.Style)]
        public ScalarDataVariable colorVariable;

        [ABRInput("Colormap", "Color", UpdateLevel.Style)]
        public IColormapVisAsset colormap;


        [ABRInput("Pattern Variable", "Pattern", UpdateLevel.Style)]
        public ScalarDataVariable patternVariable;

        [ABRInput("Pattern", "Pattern", UpdateLevel.Style)]
        public ISurfaceTextureVisAsset pattern;

        [ABRInput("Pattern Size", "Pattern", UpdateLevel.Style)]
        public LengthPrimitive patternSize;

        [ABRInput("Pattern Seam Blend", "Pattern", UpdateLevel.Style)]
        public PercentPrimitive patternDirectionBlend;

        [ABRInput("Pattern Saturation", "Pattern", UpdateLevel.Style)]
        public PercentPrimitive patternSaturation;

        [ABRInput("Pattern Intensity", "Pattern", UpdateLevel.Style)]
        public PercentPrimitive patternIntensity;

        protected override string MaterialName { get; } = "ABR_Surface";
        protected override string LayerName { get; } = "ABR_Surface";


        // Whether or not to render the back faces of the mesh
        private bool backFace = true;

        /// <summary>
        ///     Construct a data impession with a given UUID. Note that this
        ///     will be called from ABRState and must assume that there's a
        ///     single string argument with UUID.
        /// </summary>
        public SimpleSurfaceDataImpression(string uuid) : base(uuid) { }
        public SimpleSurfaceDataImpression() : base() { }

        public override Dataset GetDataset()
        {
            return keyData?.GetDataset();
        }

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

        public override void SetupGameObject(EncodedGameObject currentGameObject)
        {
            if (currentGameObject == null)
            {
                return;
            }

            // Setup mesh renderer and mesh filter
            MeshFilter meshFilter = null;
            MeshRenderer meshRenderer = null;
            if (!currentGameObject.TryGetComponent<MeshFilter>(out meshFilter))
            {
                meshFilter = currentGameObject.gameObject.AddComponent<MeshFilter>();
            }
            if (!currentGameObject.TryGetComponent<MeshRenderer>(out meshRenderer))
            {
                meshRenderer = currentGameObject.gameObject.AddComponent<MeshRenderer>();
            }

            // Ensure we have a layer to work with
            int layerID = LayerMask.NameToLayer(LayerName);
            if (layerID >= 0)
            {
                currentGameObject.gameObject.layer = layerID;
            }
            else
            {
                Debug.LogWarningFormat("Could not find layer {0} for SimpleSurfaceDataImpression", LayerName);
            }
            currentGameObject.name = this + " surface Mesh";

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
                meshRenderer.material = ImpressionMaterial;
            }
        }

        public override void UpdateStyling(EncodedGameObject currentGameObject)
        {
            // Return immediately if the game object, mesh filter, or mesh renderer do not exist
            // (this should only really happen if the gameobject/renderers for this impression have not yet been initialized,
            // which equivalently indicates that KeyData has yet to be applied to this impression and therefore there 
            // is no point in styling it anyway)
            MeshFilter meshFilter = currentGameObject?.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = currentGameObject?.GetComponent<MeshRenderer>();
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

            // Apply changes to the mesh's shader / material
            meshRenderer.GetPropertyBlock(MatPropBlock);
            MatPropBlock.SetColor("_Color", Color.white);
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

            float patternDirectionBlendOut = patternDirectionBlend?.Value ??
                config.GetInputValueDefault<PercentPrimitive>(plateType, "Pattern Seam Blend").Value;

            float patternSaturationOut = patternSaturation?.Value ??
                config.GetInputValueDefault<PercentPrimitive>(plateType, "Pattern Saturation").Value;

            MatPropBlock.SetFloat("_PatternScale", patternSizeOut);
            MatPropBlock.SetFloat("_PatternIntensity", patternIntensityOut);
            MatPropBlock.SetFloat("_PatternDirectionBlend", patternDirectionBlendOut);
            MatPropBlock.SetFloat("_PatternSaturation", patternSaturationOut);

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

        public override void UpdateVisibility(EncodedGameObject currentGameObject)
        {
            MeshRenderer mr = currentGameObject?.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = RenderHints.Visible;
            }
        }
    }
}