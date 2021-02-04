/* SimpleSurfaceDataImpression.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using System;
using UnityEngine;

namespace IVLab.ABREngine
{
    class SimpleSurfaceRenderInfo : IDataImpressionRenderInfo
    {
        public Vector3[] vertices;
        public int[] indices;
        public Vector3[] normals;
        public Color[] scalars;
        public Vector4 scalarMin;
        public Vector4 scalarMax;
        public MeshTopology topology;
    }


    [ABRPlateType("Surfaces")]
    public class SimpleSurfaceDataImpression : DataImpression, IDataImpression
    {
        public Guid Uuid { get; }

        [ABRInput("Key Data", "Key Data")]
        public SurfaceKeyData keyData;

        [ABRInput("Color Variable", "Color")]
        public ScalarDataVariable colorVariable;

        [ABRInput("Colormap", "Color")]
        public ColormapVisAsset colormap;

        [ABRInput("Pattern Variable", "Pattern")]
        public ScalarDataVariable patternVariable;

        [ABRInput("Pattern", "Pattern")]
        public SurfaceTextureVisAsset pattern;

        protected override string MaterialName { get; } = "ABR_DataColoredMesh";
        protected override string LayerName { get; } = "ABR_Surface";


        // TODO add the primitive inputs
        // TODO load defaults from schema

        // Whether or not to render the back faces of the mesh
        private bool backFace = true;

        public void ComputeKeyDataRenderInfo() { }

        public SimpleSurfaceDataImpression() : base()
        {
            Uuid = Guid.NewGuid();
        }

        public void ComputeRenderInfo()
        {
            if (keyData?.Path == null)
            {
                return;
            }

            SimpleSurfaceRenderInfo renderInfo = null;
            RawDataset dataset;
            DataManager.Instance.TryGetDataset(keyData.Path, out dataset);

            if (dataset == null)
            {
                renderInfo = new SimpleSurfaceRenderInfo
                {
                    vertices = new Vector3[0],
                    normals = null,
                    indices = new int[0],
                    scalars = new Color[0],
                    topology = MeshTopology.Points
                };
            }
            else
            {
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
                    scalarMin = Vector4.zero,
                    scalarMax = Vector4.zero,
                    topology = dataset.meshTopology

                };

                int numCells = dataset.cellIndexCounts.Length;
                int cellSize = dataset.meshTopology == MeshTopology.Quads ? 4 : 3;

                for (int i = 0; i < sourceVertCount; i++)
                {
                    renderInfo.vertices[i] = keyData.DataTransform * dataset.vertexArray[i];
                }

                // Backfaces 
                for (int i = sourceVertCount, j = 0; i < numPoints; i++, j++)
                {
                    renderInfo.vertices[i] = keyData.DataTransform * dataset.vertexArray[j];
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

                if (colorVariable != null)
                {
                    var colorScalars = colorVariable.GetArray(keyData);
                    for (int i = 0; i < sourceVertCount; i++)
                        renderInfo.scalars[i][0] = colorScalars[i];

                    // Back faces
                    for (int i = sourceVertCount, j = 0; i < numPoints; i++, j++)
                        renderInfo.scalars[i][0] = colorScalars[j];

                    renderInfo.scalarMin[0] = colorVariable.MinValue;
                    renderInfo.scalarMax[0] = colorVariable.MaxValue;
                }

                if (patternVariable != null)
                {
                    var scalars = patternVariable.GetArray(keyData);
                    for (int i = 0; i < sourceVertCount; i++)
                        renderInfo.scalars[i][1] = scalars[i];

                    // Back faces
                    for (int i = sourceVertCount, j = 0; i < numPoints; i++, j++)
                        renderInfo.scalars[i][1] = scalars[j];

                    renderInfo.scalarMin[1] = patternVariable.MinValue;
                    renderInfo.scalarMax[1] = patternVariable.MaxValue;

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

        public void ApplyToGameObject(EncodedGameObject currentGameObject)
        {
            var SSrenderData = RenderInfo as SimpleSurfaceRenderInfo;

            if (currentGameObject == null)
            {
                return;
            }

            MeshFilter meshFilter;
            MeshRenderer meshRenderer;

            meshFilter = currentGameObject.GetComponent<MeshFilter>();
            meshRenderer = currentGameObject.GetComponent<MeshRenderer>();

            if (meshFilter == null)
            {
                meshFilter = currentGameObject.gameObject.AddComponent<MeshFilter>();

            }
            if (meshRenderer == null)
            {
                meshRenderer = currentGameObject.gameObject.AddComponent<MeshRenderer>();
            }

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

            if (SSrenderData != null)
            {
                Mesh mesh = meshFilter.mesh;
                if (mesh == null) mesh = new Mesh();
                mesh.Clear();
                mesh.name = "SSS:278@" + System.DateTime.Now.ToString();

                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = SSrenderData.vertices;
                mesh.colors = SSrenderData.scalars;
                mesh.SetIndices(SSrenderData.indices, SSrenderData.topology, 0);
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

                meshRenderer.GetPropertyBlock(MatPropBlock);
                MatPropBlock.SetColor("_Color", Color.white);
                MatPropBlock.SetFloat("_ColorDataMin", SSrenderData.scalarMin[0]);
                MatPropBlock.SetFloat("_ColorDataMax", SSrenderData.scalarMax[0]);
                MatPropBlock.SetFloat("_PatternDataMin", SSrenderData.scalarMin[1]);
                MatPropBlock.SetFloat("_PatternDataMax", SSrenderData.scalarMax[1]);

                // if (ABRManager.IsValidNode(patternIntensity))
                // {
                //     MatPropBlock.SetFloat("_PatternIntensity", patternIntensity.floatVal);

                // }
                // else
                // {
                    MatPropBlock.SetFloat("_PatternIntensity", 1);
                // }
                // if (ABRManager.IsValidNode(patternScale))
                // {
                //     MatPropBlock.SetFloat("_PatternScale", patternScale.floatVal);

                // }
                // else
                // {
                    MatPropBlock.SetFloat("_PatternScale", 1);
                // }

                // if (ABRManager.IsValidNode(patternDirectionBlend))
                // {
                //     MatPropBlock.SetFloat("_PatternDirectionBlend", patternDirectionBlend.floatVal);

                // }
                // else
                // {
                    MatPropBlock.SetFloat("_PatternDirectionBlend", 1);
                // }


                // if (ABRManager.IsValidNode(patternSaturation))
                // {
                //     MatPropBlock.SetFloat("_PatternSaturation", patternSaturation.floatVal);

                // }
                // else
                // {
                    MatPropBlock.SetFloat("_PatternSaturation", 1);
                // }


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
                    if (pattern?.Texture != null)
                    {
                        MatPropBlock.SetInt("_UsePattern", 1);
                        MatPropBlock.SetTexture("_Pattern", pattern?.Texture);
                        MatPropBlock.SetTexture("_PatternNormal", pattern?.NormalMap);

                    }
                    else
                    {
                        MatPropBlock.SetInt("_UsePattern", 0);
                        // MatPropBlock.SetTexture("_Pattern", VisAssetManager.GetDefaultAlbedo());
                        // MatPropBlock.SetTexture("_PatternNormal", VisAssetManager.GetDefaultNormal());
                        MatPropBlock.SetTexture("_Pattern", new Texture2D(10, 10));
                        MatPropBlock.SetTexture("_PatternNormal", new Texture2D(10, 10));

                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }

                meshRenderer.SetPropertyBlock(MatPropBlock);
            }
        }
    }
}