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

    /// <summary>
    /// A "Lines" data impression that uses hand-drawn line textures to depict line/flow data.
    /// </summary>
    /// <example>
    /// An example of creating a single line data impression and setting its colormap, color variable, and line texture could be:
    /// <code>
    /// SimpleLineDataImpression gi = new SimpleLineDataImpression();
    /// gi.keyData = lines;
    /// gi.colorVariable = yAxis;
    /// gi.colormap = ABREngine.Instance.VisAssets.GetDefault&lt;ColormapVisAsset&gt;() as ColormapVisAsset;
    /// gi.lineTexture = line;
    /// ABREngine.Instance.RegisterDataImpression(gi);
    /// </code>
    /// </example>
    [ABRPlateType("Ribbons")]
    public class SimpleLineDataImpression : DataImpression, IDataImpression, IHasDataset
    {
        [ABRInput("Key Data", "Key Data", UpdateLevel.Data)]
        public KeyData keyData;

        [ABRInput("Color Variable", "Color", UpdateLevel.Style)]
        public ScalarDataVariable colorVariable;

        [ABRInput("Colormap", "Color", UpdateLevel.Style)]
        public IColormapVisAsset colormap;


        [ABRInput("Texture Variable", "Texture", UpdateLevel.Style)]
        public ScalarDataVariable lineTextureVariable;

        [ABRInput("Texture", "Texture", UpdateLevel.Style)]
        public ILineTextureVisAsset lineTexture;

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

        public Vector3 defaultCurveDirection = Vector3.up;

        protected override string MaterialName { get; } = "ABR_Ribbon";
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
                    int numVerts = numPoints * 4; // 4 vertices per line segment (2 triangles)
                    int numIndices = (numPoints - 1) * 12; // 12 indices per line segment (4 triangles, front/back)
                    renderInfo.vertices[i] = new Vector3[numVerts];
                    renderInfo.normals[i] = new Vector3[numVerts];
                    renderInfo.uvs[i] = new Vector2[numVerts];
                    renderInfo.scalars[i] = new Color[numVerts];
                    renderInfo.indices[i] = new int[numIndices];

                    int indexOffset = dataset.cellIndexOffsets[i];

                    int indexEnd = indexOffset + numPoints;

                    float arclength = 0;

                    Vector3 lastV = defaultCurveDirection;
                    for (int index = indexOffset, j = 0; index < indexEnd; index++, j++)
                    {
                        pointIndex = dataset.indexArray[index];
                        // Gather previous/next point indices, default to current index if outside bounds
                        var lastPointIndex = (j > 0) ? dataset.indexArray[index - 1] : pointIndex;
                        var nextPointIndex = (j < numPoints - 1) ? dataset.indexArray[index + 1] : pointIndex;

                        // Point is the current data point from the line dataset, previous and next point
                        Vector3 point = group.GroupToDataMatrix * dataset.vertexArray[pointIndex].ToHomogeneous();
                        Vector3 lastPoint = group.GroupToDataMatrix * dataset.vertexArray[lastPointIndex].ToHomogeneous();
                        Vector3 nextPoint = group.GroupToDataMatrix * dataset.vertexArray[nextPointIndex].ToHomogeneous();

                        // Vectors pointing `last --> current --> next`
                        Vector3 fromLast = point - lastPoint;
                        Vector3 toNext = nextPoint - point;

                        Vector3 tangent;
                        Vector3 normal;
                        Vector3 bitangent;
                        Vector4 scalar = Vector4.zero;

                        // Arclength is the length we've travelled so far in this ribbon
                        arclength = arclength + fromLast.magnitude;

                        // Calculate tangent at current point (tangent of curve)
                        tangent = (fromLast + toNext).normalized;

                        // Calculate axis of curvature
                        Vector3 V = Vector3.Cross(fromLast.normalized, toNext.normalized).normalized;

                        // SPECIAL CASE: switching curvature directions
                        if (Vector3.Dot(V, lastV) < 0) V = -V;

                        // SPECIAL CASE: no curvature (default to previously found V)
                        if (V.magnitude < float.Epsilon)
                        {
                            V = lastV;
                        }

                        lastV = V;

                        // Calculate initial normal of ribbon
                        Vector3 N = Vector3.Cross(V, tangent).normalized;
                        Vector3 normalSum = N;
                        Vector3 tangentDirSum = tangent.normalized;

                        // Smooth out the normals, if desired
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

                        // Only smooth over averageCountN normals
                        smoothingNormals.Enqueue(normal);
                        while (smoothingNormals.Count > averageCountN) smoothingNormals.Dequeue();
                        while (smoothingTangents.Count > averageCountN) smoothingTangents.Dequeue();

                        // Assign scalar variables
                        if (colorVariableArray != null)
                        {
                            scalar[0] = colorVariableArray[index];
                        }

                        // Calculate a 3D basis for the point.
                        // Normal, Tangent, and Bitangent should be mutually perpendicular.
                        normal = normal.normalized;
                        tangent = tangent.normalized;
                        bitangent = -Vector3.Cross(normal, tangent).normalized;

                        // Rotate the ribbon based on user parameter
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

                        renderInfo.vertices[i][indexTopFront] = (point + bitangent * ribbonWidth);
                        renderInfo.vertices[i][indexTopBack] = (point + bitangent * ribbonWidth);
                        renderInfo.vertices[i][indexBottomFront] = (point - bitangent * ribbonWidth);
                        renderInfo.vertices[i][indexBottomBack] = (point - bitangent * ribbonWidth);

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



                        // Skip the first point since there is not a valid curvature direction (need 3 points)
                        if (j < (numPoints - 1) && j > 0)
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

            // Pack scalar min/max and get scalar data, if any
            Color[][] scalars = new Color[numLines][];
            Vector4 scalarMax = Vector4.zero;
            Vector4 scalarMin = Vector4.zero;
            float[] colorVariableArray = null;
            float[] ribbonVariableArray = null;
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
            if (lineTextureVariable != null && lineTextureVariable.IsPartOf(keyData))
            {
                ribbonVariableArray = lineTextureVariable.GetArray(keyData);
                // Get keydata-specific range, if there is one
                if (lineTextureVariable.SpecificRanges.ContainsKey(keyData.Path))
                {
                    scalarMin[1] = lineTextureVariable.SpecificRanges[keyData.Path].min;
                    scalarMax[1] = lineTextureVariable.SpecificRanges[keyData.Path].max;
                }
                else
                {
                    scalarMin[1] = lineTextureVariable.Range.min;
                    scalarMax[1] = lineTextureVariable.Range.max;
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

                // Show/hide line based on per-index visibility
                if (RenderHints.HasPerIndexVisibility() && i < RenderHints.PerIndexVisibility.Count)
                    meshRenderer.enabled = RenderHints.PerIndexVisibility[i];

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

                    // Pack scalars
                    // INDEX 0: Color variable
                    if (colorVariableArray != null)
                    {
                        scalar[0] = colorVariableArray[index];
                    }
                    // INDEX 1: Ribbon variable
                    if (ribbonVariableArray != null)
                    {
                        scalar[1] = ribbonVariableArray[index];
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
                    MatPropBlock.SetTexture("_Texture", lineTexture.BlendMaps.Textures);
                    MatPropBlock.SetTexture("_BlendMaps", lineTexture.BlendMaps.BlendMaps);
                    MatPropBlock.SetInt("_NumTex", lineTexture.VisAssetCount);
                    MatPropBlock.SetFloatArray("_TextureAspect", lineTexture.BlendMaps.AspectRatios);
                    MatPropBlock.SetFloatArray("_TextureHeightWidthAspect", lineTexture.BlendMaps.HeightWidthAspectRatios);
                    MatPropBlock.SetInt("_UseLineTexture", 1);
                }
                else
                {
                    MatPropBlock.SetInt("_UseLineTexture", 0);

                }
                MatPropBlock.SetVector("_ScalarMin", scalarMin);
                MatPropBlock.SetVector("_ScalarMax", scalarMax);
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
                if (mr !=  null)
                {
                    if (RenderHints.Visible)
                    {
                        if (RenderHints.HasPerIndexVisibility() && i < RenderHints.PerIndexVisibility.Count)
                            mr.enabled = RenderHints.PerIndexVisibility[i];
                        else
                            mr.enabled = true;
                    }
                    else
                    {
                        mr.enabled = false;
                    }
                }
            }
        }
    }
}