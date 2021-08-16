/* SimpleLineDataImpression.cs
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

using System.Reflection;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

using IVLab.Utilities;

namespace IVLab.ABREngine
{
    class SimpleLineRenderInfo : IDataImpressionRenderInfo
    {
        public Vector3[][] vertices;
        public int[][] indices;
        public Vector3[][] normals;
        public Color[][] scalars;
        public Vector2[][] uvs;
    }

    [ABRPlateType("Ribbons")]
    public class SimpleLineDataImpression : DataImpression, IDataImpression, IHasDataset
    {
        [ABRInput("Key Data", "Key Data", UpdateLevel.Data)]
        public LineKeyData keyData;

        [ABRInput("Color Variable", "Color", UpdateLevel.Style)]
        public ScalarDataVariable colorVariable;

        [ABRInput("Colormap", "Color", UpdateLevel.Style)]
        public ColormapVisAsset colormap;


        [ABRInput("Texture Variable", "Texture", UpdateLevel.Style)]
        public ScalarDataVariable lineTextureVariable;

        [ABRInput("Texture", "Texture", UpdateLevel.Style)]
        public LineTextureVisAsset lineTexture;

        [ABRInput("Texture Cutoff", "Texture", UpdateLevel.Style)]
        public PercentPrimitive textureCutoff;


        [ABRInput("Ribbon Smooth", "Ribbon", UpdateLevel.Data)]
        public IntegerPrimitive averageCount;

        [ABRInput("Ribbon Width", "Ribbon", UpdateLevel.Data)]
        public LengthPrimitive lineWidth;

        [ABRInput("Ribbon Rotation", "Ribbon", UpdateLevel.Data)]
        public AnglePrimitive ribbonRotationAngle;

        [ABRInput("Ribbon Brightness", "Ribbon", UpdateLevel.Style)]
        public PercentPrimitive ribbonBrightness;

        [ABRInput("Ribbon Curve", "Ribbon", UpdateLevel.Data)]
        public AnglePrimitive ribbonCurveAngle;

        protected override string MaterialName { get; } = "ABR_DataTextureRibbon";
        protected override string LayerName { get; } = "ABR_Line";

        /// <summary>
        ///     Construct a data impession with a given UUID. Note that this
        ///     will be called from ABRState and must assume that there's a
        ///     single string argument with UUID.
        /// </summary>
        public SimpleLineDataImpression(string uuid) : base(uuid) { }
        public SimpleLineDataImpression() : base() { }

        public override Dataset GetDataset()
        {
            return keyData?.GetDataset();
        }

        public override void ComputeGeometry()
        {
            SimpleLineRenderInfo renderInfo = null;

            if (keyData == null)
            {
                renderInfo = new SimpleLineRenderInfo
                {
                    vertices = new Vector3[0][],
                    indices = new int[0][],
                    scalars = new Color[0][],
                    normals = new Vector3[0][],
                    uvs = new Vector2[0][],
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

                // Load defaults from configuration / schema
                ABRConfig config = ABREngine.Instance.Config;

                // Width appears double what it should be, so decrease to
                // maintain the actual real world distance
                string plateType = this.GetType().GetCustomAttribute<ABRPlateType>().plateType;

                float ribbonWidth = lineWidth?.Value ??
                    config.GetInputValueDefault<LengthPrimitive>(plateType, "Ribbon Width").Value;
                ribbonWidth /= 2.0f;

                int averageCountN = averageCount?.Value ??
                    config.GetInputValueDefault<IntegerPrimitive>(plateType, "Ribbon Smooth").Value;

                float curveAngle = ribbonCurveAngle?.Value ??
                    config.GetInputValueDefault<AnglePrimitive>(plateType, "Ribbon Curve").Value;

                float ribbonRotation = ribbonRotationAngle?.Value ??
                    config.GetInputValueDefault<AnglePrimitive>(plateType, "Ribbon Rotation").Value;
                    

                int numLines = 0;
                numLines = dataset.cellIndexCounts.Length;
                renderInfo = new SimpleLineRenderInfo
                {
                    vertices = new Vector3[numLines][],
                    indices = new int[numLines][],
                    scalars = new Color[numLines][],
                    normals = new Vector3[numLines][],
                    uvs = new Vector2[numLines][],
                };

                int pointIndex = 0;

                float[] colorVariableArray = null;
                if (colorVariable != null && colorVariable.IsPartOf(keyData))
                {
                    colorVariableArray = colorVariable.GetArray(keyData);
                }

                for (int i = 0; i < numLines; i++)
                {
                    Queue<Vector3> smoothingNormals = new Queue<Vector3>(averageCountN);
                    Queue<Vector3> smoothingTangents = new Queue<Vector3>(averageCountN);

                    int numPoints = dataset.cellIndexCounts[i];
                    int numVerts = numPoints * 4;
                    int numIndices = (numPoints - 1) * 12;
                    renderInfo.vertices[i] = new Vector3[numVerts];
                    renderInfo.normals[i] = new Vector3[numVerts];
                    renderInfo.uvs[i] = new Vector2[numVerts];
                    renderInfo.scalars[i] = new Color[numVerts];
                    renderInfo.indices[i] = new int[numIndices];

                    int indexOffset = dataset.cellIndexOffsets[i];

                    int indexEnd = indexOffset + numPoints;

                    float arclength = 0;

                    Vector3 lastV = Vector3.up;
                    for (int index = indexOffset, j = 0; index < indexEnd; index++, j++)
                    {
                        pointIndex = dataset.indexArray[index];
                        var lastPointIndex = (j == 0) ? pointIndex : dataset.indexArray[index - 1];
                        var nextPointIndex = (j == numPoints - 1) ? pointIndex : dataset.indexArray[index + 1];

                        Vector3 point = group.GroupToDataMatrix * dataset.vertexArray[pointIndex].ToHomogeneous();
                        Vector3 lastPoint = group.GroupToDataMatrix * dataset.vertexArray[lastPointIndex].ToHomogeneous();
                        Vector3 nextPoint = group.GroupToDataMatrix * dataset.vertexArray[nextPointIndex].ToHomogeneous();

                        Vector3 tangent;
                        Vector3 normal;
                        Vector3 bitangent;
                        Vector3 fromLast = point - lastPoint;
                        Vector3 toNext = nextPoint - point;
                        Vector4 scalar = Vector4.zero;

                        arclength = arclength + fromLast.magnitude;
                        tangent = (fromLast + toNext).normalized;

                        Vector3 V = Vector3.Cross(fromLast.normalized, toNext.normalized).normalized;
                        if (Vector3.Dot(V, lastV) < 0) V = -V;
                        lastV = V;
                        Vector3 N = Vector3.Cross(V, tangent).normalized;
                        Vector3 normalSum = N;
                        Vector3 tangentDirSum = tangent.normalized;

                        if (smoothingNormals.Count > 0)
                        {
                            if (Vector3.Dot(smoothingNormals.Last(), N) < 0)
                                N = -N;
                            normalSum = N;

                            foreach (var n in smoothingNormals)
                            {
                                normalSum += n;
                            }
                            foreach (var t in smoothingTangents)
                            {
                                tangentDirSum += t.normalized;
                            }
                        }
                        Vector3 normalAvg = normalSum / (smoothingNormals.Count + 1);
                        Vector3 tangentAvg = tangentDirSum / (smoothingNormals.Count + 1);


                        normal = normalAvg;
                        tangent = tangentAvg;

                        smoothingNormals.Enqueue(normal);
                        while (smoothingNormals.Count > averageCountN) smoothingNormals.Dequeue();
                        while (smoothingTangents.Count > averageCountN) smoothingTangents.Dequeue();


                        if (colorVariableArray != null)
                        {
                            scalar[0] = colorVariableArray[index];
                        }

                        normal = normal.normalized;
                        tangent = tangent.normalized;
                        bitangent = -Vector3.Cross(normal, tangent).normalized;



                        normal = Quaternion.AngleAxis(ribbonRotation, tangent) * normal;
                        bitangent = Quaternion.AngleAxis(ribbonRotation, tangent) * bitangent;


                        int indexTopFront = j * 4 + 0;
                        int indexTopBack = j * 4 + 1;
                        int indexBottomFront = j * 4 + 2;
                        int indexBottomBack = j * 4 + 3;

                        int nextIndexTopFront = (j + 1) * 4 + 0;
                        int nextIndexTopBack = (j + 1) * 4 + 1;
                        int nextIndexBottomFront = (j + 1) * 4 + 2;
                        int nextIndexBottomBack = (j + 1) * 4 + 3;

                        renderInfo.vertices[i][indexTopFront] = (point + bitangent * ribbonWidth + normal * ribbonWidth);
                        renderInfo.vertices[i][indexTopBack] = (point + bitangent * ribbonWidth - normal * ribbonWidth);
                        renderInfo.vertices[i][indexBottomFront] = (point - bitangent * ribbonWidth + normal * ribbonWidth);
                        renderInfo.vertices[i][indexBottomBack] = (point - bitangent * ribbonWidth - normal * ribbonWidth);

                        renderInfo.normals[i][indexTopFront] = Quaternion.AngleAxis(curveAngle, tangent) * normal;
                        renderInfo.normals[i][indexTopBack] = Quaternion.AngleAxis(-curveAngle, tangent) * -normal;
                        renderInfo.normals[i][indexBottomFront] = Quaternion.AngleAxis(-curveAngle, tangent) * normal;
                        renderInfo.normals[i][indexBottomBack] = Quaternion.AngleAxis(curveAngle, tangent) * -normal;

                        renderInfo.scalars[i][indexTopFront] = scalar;
                        renderInfo.scalars[i][indexTopBack] = scalar;
                        renderInfo.scalars[i][indexBottomFront] = scalar;
                        renderInfo.scalars[i][indexBottomBack] = scalar;



                        renderInfo.uvs[i][indexTopFront] = new Vector2(arclength / (ribbonWidth * 2), 0);
                        renderInfo.uvs[i][indexTopBack] = new Vector2(arclength / (ribbonWidth * 2), 0);
                        renderInfo.uvs[i][indexBottomFront] = new Vector2(arclength / (ribbonWidth * 2), 1);
                        renderInfo.uvs[i][indexBottomBack] = new Vector2(arclength / (ribbonWidth * 2), 1);



                        if (j < (numPoints - 10) && j > 1)
                        {
                            renderInfo.indices[i][j * 12 + 0] = indexTopFront;
                            renderInfo.indices[i][j * 12 + 1] = nextIndexTopFront;
                            renderInfo.indices[i][j * 12 + 2] = indexBottomFront;

                            renderInfo.indices[i][j * 12 + 3] = nextIndexTopFront;
                            renderInfo.indices[i][j * 12 + 4] = nextIndexBottomFront;
                            renderInfo.indices[i][j * 12 + 5] = indexBottomFront;

                            renderInfo.indices[i][j * 12 + 6] = indexBottomBack;
                            renderInfo.indices[i][j * 12 + 7] = nextIndexTopBack;
                            renderInfo.indices[i][j * 12 + 8] = indexTopBack;

                            renderInfo.indices[i][j * 12 + 9] = indexBottomBack;
                            renderInfo.indices[i][j * 12 + 10] = nextIndexBottomBack;
                            renderInfo.indices[i][j * 12 + 11] = nextIndexTopBack;
                        }

                    }
                }
            }

            RenderInfo = renderInfo;
        }

        public override void SetupGameObject(EncodedGameObject currentGameObject)
        {
            var lineResources = RenderInfo as SimpleLineRenderInfo;
            if (currentGameObject == null || lineResources == null)
            {
                return;
            }

            // Find ABR Layer
            int layerID = LayerMask.NameToLayer(LayerName);
            if (layerID < 0)
            {
                Debug.LogWarningFormat("Could not find layer {0} for SimpleLineDataImpression", LayerName);
            }

            // Create a new GameObject for each line in the data, and return any unused ones to the pool
            int numLines = lineResources?.indices?.Length ?? 0;
            while (currentGameObject.transform.childCount < numLines)
            {
                GameObject renderObject = GenericObjectPool.Instance.GetObjectFromPool(this.GetType().Name + " meshRenderer", currentGameObject.transform, go =>
                {
                    go.name = "Line Render Object";
                });
                renderObject.transform.SetParent(currentGameObject.transform, false);
                renderObject.transform.localPosition = Vector3.zero;
                renderObject.transform.localScale = Vector3.one;
                renderObject.transform.localRotation = Quaternion.identity;
            }

            while (currentGameObject.transform.childCount > numLines)
            {
                GameObject child = currentGameObject.transform.GetChild(0).gameObject;
                GenericObjectPool.Instance.ReturnObjectToPool(child);
            }

            // Create mesh filters and renderers for each line
            for (int i = 0; i < numLines; i++)
            {
                var renderObject = currentGameObject.transform.GetChild(i).gameObject;
                MeshFilter meshFilter = null;
                MeshRenderer meshRenderer = null;
                if (!renderObject.TryGetComponent<MeshFilter>(out meshFilter))
                {
                    meshFilter = renderObject.AddComponent<MeshFilter>();
                }

                if (!renderObject.TryGetComponent<MeshRenderer>(out meshRenderer))
                {
                    meshRenderer = renderObject.AddComponent<MeshRenderer>();
                }

                if (layerID >= 0)
                {
                    renderObject.layer = layerID;
                }

                // Build line mesh from computed geometry info
                Mesh mesh = meshFilter.mesh;
                if (mesh == null) mesh = new Mesh();
                mesh.name = "SL:392@" + DateTime.Now.ToString();

                mesh.Clear();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                mesh.vertices = lineResources.vertices[i];
                mesh.normals = lineResources.normals[i];
                mesh.colors = lineResources.scalars[i];
                mesh.uv = lineResources.uvs[i];

                mesh.SetIndices(lineResources.indices[i], MeshTopology.Triangles, 0);
                mesh.UploadMeshData(false);

                meshFilter.mesh = mesh;
                meshRenderer.material = ImpressionMaterial;
            }
        }

        // Updates line styling based on scalars, color, texture and ribbon brightness,
        // but none of the other ribbon attributes (width, rotation, curve, smooth)
        // since those involve actually rebuilding geometry
        public override void UpdateStyling(EncodedGameObject currentGameObject)
        {
            // Exit immediately if the game object or key data does not exist
            if (currentGameObject == null || keyData == null)
            {
                return;
            }

            // Load the lines' dataset in order to obtain some key information about them 
            // (how many lines there are, how many points they are made out of, etc.)
            RawDataset dataset;
            if (!ABREngine.Instance.Data.TryGetRawDataset(keyData?.Path, out dataset))
            {
                return;
            }
            int numLines = dataset.cellIndexCounts.Length;

            // Initialize variables to track scalar "styling" changes
            Color[][] scalars = new Color[numLines][];
            Vector4 scalarMax = Vector4.zero;
            Vector4 scalarMin = Vector4.zero;
            float[] colorVariableArray = null;
            if (colorVariable != null && colorVariable.IsPartOf(keyData))
            {
                colorVariableArray = colorVariable.GetArray(keyData);
                // Get keydata-specific range, if there is one
                if (colorVariable.SpecificRanges.ContainsKey(keyData.Path))
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

            // Iterate through all line renderers and update their stylings
            for (int i = 0; i < numLines; i++)
            {
                // Get the current line renderer gameobject
                GameObject renderObject = currentGameObject.transform.GetChild(i).gameObject;
                // Obtain its mesh renderer and filter components
                MeshFilter meshFilter = renderObject?.GetComponent<MeshFilter>();
                MeshRenderer meshRenderer = renderObject?.GetComponent<MeshRenderer>();
                // If either of them do not exist, exit as this impression has likely not been
                // fully initialized with KeyData yet
                if (meshFilter == null || meshRenderer == null)
                {
                    return;
                }

                // We should be able to access the mesh now
                Mesh mesh = meshFilter.mesh;

                // Using the info provided by the dataset, determine the total number of points and vertices
                int numPoints = dataset.cellIndexCounts[i];
                int numVerts = numPoints * 4;

                // Initialize and update the scalar data container for the current mesh / line
                // with whatever "styling" changes have occured to scalars
                scalars[i] = new Color[numVerts];
                int indexOffset = dataset.cellIndexOffsets[i];
                int indexEnd = indexOffset + numPoints;
                for (int index = indexOffset, j = 0; index < indexEnd; index++, j++)
                {
                    Vector4 scalar = Vector4.zero;

                    if (colorVariableArray != null)
                    {
                        scalar[0] = colorVariableArray[index];
                    }

                    int indexTopFront = j * 4 + 0;
                    int indexTopBack = j * 4 + 1;
                    int indexBottomFront = j * 4 + 2;
                    int indexBottomBack = j * 4 + 3;

                    scalars[i][indexTopFront] = scalar;
                    scalars[i][indexTopBack] = scalar;
                    scalars[i][indexBottomFront] = scalar;
                    scalars[i][indexBottomBack] = scalar;
                }

                // Apply the "styling" changes to the mesh itself
                mesh.name = "LRS:368@" + DateTime.Now.ToString();
                mesh.colors = scalars[i];
                mesh.UploadMeshData(false);
                meshFilter.mesh = mesh;

                // Apply changes to the mesh's shader / material

                // Load defaults from configuration / schema
                ABRConfig config = ABREngine.Instance.Config;

                // Width appears double what it should be, so decrease to
                // maintain the actual real world distance
                string plateType = this.GetType().GetCustomAttribute<ABRPlateType>().plateType;

                float ribbonBrightnessOut = ribbonBrightness?.Value ??
                    config.GetInputValueDefault<PercentPrimitive>(plateType, "Ribbon Brightness").Value;

                float textureCutoffOut = textureCutoff?.Value ??
                    config.GetInputValueDefault<PercentPrimitive>(plateType, "Texture Cutoff").Value;

                meshRenderer.GetPropertyBlock(MatPropBlock);
                MatPropBlock.SetColor("_Color", Color.white);
                MatPropBlock.SetFloat("_TextureCutoff", textureCutoffOut);
                MatPropBlock.SetFloat("_RibbonBrightness", ribbonBrightnessOut);

                if (lineTexture != null)
                {
                    MatPropBlock.SetTexture("_Texture", lineTexture.Texture);
                    MatPropBlock.SetFloat("_TextureAspect", lineTexture.Texture.width / (float)lineTexture.Texture.height);
                    MatPropBlock.SetInt("_UseLineTexture", 1);

                }
                else
                {
                    MatPropBlock.SetInt("_UseLineTexture", 0);

                }
                MatPropBlock.SetFloat("_ColorDataMin", scalarMin[0]);
                MatPropBlock.SetFloat("_ColorDataMax", scalarMax[0]);
                if (colormap?.GetColorGradient() != null)
                {
                    MatPropBlock.SetInt("_UseColorMap", 1);
                    MatPropBlock.SetTexture("_ColorMap", colormap?.GetColorGradient());
                }
                else
                {
                    MatPropBlock.SetInt("_UseColorMap", 0);
                }
                meshRenderer.SetPropertyBlock(MatPropBlock);
            }
        }

        public override void UpdateVisibility(EncodedGameObject currentGameObject)
        {
            if (currentGameObject == null)
            {
                return;
            }
            for (int i = 0; i < currentGameObject.transform.childCount; i++)
            {
                MeshRenderer mr = currentGameObject.transform.GetChild(i).GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.enabled = RenderHints.Visible;
                }
            }
        }
    }
}