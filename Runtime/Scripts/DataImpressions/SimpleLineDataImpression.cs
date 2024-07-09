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
    public class SimpleLineRenderInfo : IDataImpressionRenderInfo
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
    public class SimpleLineDataImpression : DataImpression, IHasDataset
    {
        [ABRInput("Key Data", UpdateLevel.Geometry)]
        public KeyData keyData;

        /// <summary>
        /// Scalar color variable applied to each point on the line of this data
        /// impression.  This example switches between X-axis monotonically
        /// increasing and Y-axis monotonically increasing.
        ///
        /// <img src="../resources/api/SimpleLineDataImpression/colorVariable.gif"/>
        /// </summary>
        [ABRInput("Color Variable", UpdateLevel.Style)]
        public ScalarDataVariable colorVariable;

        /// <summary>
        /// Colormap applied to the <see cref="colorVariable"/>. This example
        /// switches between a linear white-to-green colormap and a linear
        /// black-to-white colormap.
        ///
        /// <img src="../resources/api/SimpleLineDataImpression/colormap.gif"/>
        /// </summary>
        [ABRInput("Colormap", UpdateLevel.Style)]
        public IColormapVisAsset colormap;

        /// <summary>
        /// Override the color used for NaN values in this data impression. If
        /// not supplied, will use the <see cref="ABRConfig.defaultNanColor"/>.
        /// </summary>
        [ABRInput("NaN Color", UpdateLevel.Style)]
        public IColormapVisAsset nanColor;


        /// <summary>
        /// Scalar variable used to vary the line texture across its length.
        /// </summary>
        [ABRInput("Texture Variable", UpdateLevel.Style)]
        public ScalarDataVariable lineTextureVariable;

        /// <summary>
        /// Texture applied to the line. Light areas on the texture are
        /// discarded, dark areas are kept. Can also be a <see cref="LineTextureGradient"/>
        ///
        /// <img src="../resources/api/SimpleLineDataImpression/lineTexture.gif"/>
        /// </summary>
        [ABRInput("Texture", UpdateLevel.Style)]
        public ILineTextureVisAsset lineTexture;

        /// <summary>
        /// Override the line texture used for NaN values in this data impression. If
        /// not supplied, will use the <see cref="ABRConfig.defaultNanLine"/>.
        /// </summary>
        [ABRInput("NaN Texture", UpdateLevel.Style)]
        public ILineTextureVisAsset nanLineTexture;

        /// <summary>
        /// "Cutoff" point for discarding portions of the line. The cutoff is
        /// between 0% (fully light) and 100% (fully dark). In practice, this is
        /// performing a <a
        /// href="https://en.wikipedia.org/wiki/Thresholding_(image_processing)">threshold</a>
        /// filter.
        ///
        /// <img src="../resources/api/SimpleLineDataImpression/textureCutoff.gif"/>
        /// </summary>
        /// <remarks>
        /// NOTE: This input will have no effect if there's no <see
        /// cref="lineTexture"/> applied. It has the most effect on textures
        /// that are not fully black/white.
        /// </remarks>
        [ABRInput("Texture Cutoff", UpdateLevel.Style)]
        public PercentPrimitive textureCutoff;


        /// <summary>
        /// Number of "averaging" samples taken across the line for a smoothing effect. This example ranges from 0 to 50.
        ///
        /// <img src="../resources/api/SimpleLineDataImpression/averageCount.gif"/>
        /// </summary>
        [ABRInput("Ribbon Smooth", UpdateLevel.Geometry)]
        public IntegerPrimitive averageCount;

        /// <summary>
        /// Width of the line, in Unity world units.
        ///
        /// <img src="../resources/api/SimpleLineDataImpression/lineWidth.gif"/>
        /// </summary>
        [ABRInput("Ribbon Width", UpdateLevel.Geometry)]
        public LengthPrimitive lineWidth;

        /// <summary>
        /// Rotate the ribbon along its central axis. This example goes from 0 degrees to 90 degrees.
        ///
        /// <img src="../resources/api/SimpleLineDataImpression/ribbonRotationAngle.gif"/>
        /// </summary>
        [ABRInput("Ribbon Rotation", UpdateLevel.Geometry)]
        public AnglePrimitive ribbonRotationAngle;

        /// <summary>
        /// Manually adjust the brightness of the ribbon regardless of lighting in the scene.
        ///
        /// <img src="../resources/api/SimpleLineDataImpression/ribbonBrightness.gif"/>
        /// </summary>
        [ABRInput("Ribbon Brightness", UpdateLevel.Style)]
        public PercentPrimitive ribbonBrightness;

        /// <summary>
        /// Subtly adjust the lighting by varying the lighting normal of the ribbon
        ///
        /// <img src="../resources/api/SimpleLineDataImpression/ribbonCurveAngle.gif"/>
        /// </summary>
        [ABRInput("Ribbon Curve", UpdateLevel.Geometry)]
        public AnglePrimitive ribbonCurveAngle;

        /// <summary>
        /// Change the default curvature axis (if there are no existing tangents
        /// on the curve, this axis will be used)
        ///
        /// <img src="../resources/api/SimpleLineDataImpression/defaultCurveDirection.gif"/>
        /// </summary>
        /// <remarks>
        /// NOTE: This input mostly changes behaviour at the ends of ribbons,
        /// unless your ribbon is perfectly straight. (This setting exists
        /// because of perfectly straight ribbons which the existing ribbon has
        /// trouble with).
        /// </remarks>
        public Vector3 defaultCurveDirection = Vector3.up;

        protected override string[] MaterialNames { get; } = { "ABR_Ribbon" };

        /// <summary>
        /// Define the layer name for this Data Impression
        /// </summary>
        /// <remarks>
        /// > [!WARNING]
        /// > New Data Impressions should define a const string "LayerName"
        /// which corresponds to a Layer in Unity's Layer manager.
        /// </remarks>
        protected const string LayerName = "ABR_Line";

        public override Dataset GetDataset() => keyData?.GetDataset();
        public override KeyData GetKeyData() => keyData;
        public override void SetKeyData(KeyData kd) => keyData = kd;
        public override DataTopology GetKeyDataTopology() => DataTopology.LineStrip;

        // protected override int GetIndexForPackedScalarVariable(ScalarDataVariable variable)
        // {
        //     for (int i = 0; i < InputIndexer.InputCount; i++)
        //     {
        //         if (InputIndexer.GetInputValue(i) == variable)
        //         {
        //             return i;
        //         }
        //     }
        //     return -1;
        // }
        // Users should NOT construct data impressions with `new DataImpression()`
        protected SimpleLineDataImpression() { }

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

        public override void SetupGameObject()
        {
            var lineResources = RenderInfo as SimpleLineRenderInfo;
            if (gameObject == null || lineResources == null)
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
            int lineIndex = 0;
            while (gameObject.transform.childCount < numLines)
            {
                GameObject renderObject = GenericObjectPool.Instance.GetObjectFromPool(this.GetType().Name + " meshRenderer", gameObject.transform, go =>
                {
                    go.name = "Line Render Object " + lineIndex;
                });
                renderObject.transform.SetParent(gameObject.transform, false);
                renderObject.transform.localPosition = Vector3.zero;
                renderObject.transform.localScale = Vector3.one;
                renderObject.transform.localRotation = Quaternion.identity;
                lineIndex += 1;
            }

            while (gameObject.transform.childCount > numLines)
            {
                GameObject child = gameObject.transform.GetChild(0).gameObject;
                GenericObjectPool.Instance.ReturnObjectToPool(child);
            }

            // Create mesh filters and renderers for each line
            for (int i = 0; i < numLines; i++)
            {
                var renderObject = gameObject.transform.GetChild(i).gameObject;
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
                meshRenderer.material = ImpressionMaterials[0];
            }
        }

        // Updates line styling based on scalars, color, texture and ribbon brightness,
        // but none of the other ribbon attributes (width, rotation, curve, smooth)
        // since those involve actually rebuilding geometry
        public override void UpdateStyling()
        {
            // Exit immediately if the game object or key data does not exist
            if (gameObject == null || keyData == null)
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
                GameObject renderObject = gameObject.transform.GetChild(i).gameObject;
                // Obtain its mesh renderer and filter components
                MeshFilter meshFilter = renderObject?.GetComponent<MeshFilter>();
                MeshRenderer meshRenderer = renderObject?.GetComponent<MeshRenderer>();
                // If either of them do not exist, exit as this impression has likely not been
                // fully initialized with KeyData yet
                if (meshFilter == null || meshRenderer == null)
                {
                    Debug.LogError("SimpleLineDataImpression: GameObject not yet initialized " + i);
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
                MatPropBlock.SetColor("_Color", ABREngine.Instance.Config.defaultColor);
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

                    Texture2D defaultNanLine = ABREngine.Instance.Config.defaultNanLine;
                    Texture2D nanLine = nanLineTexture?.BlendMaps.Textures ?? defaultNanLine;
                    if (nanLine != null)
                    {
                        MatPropBlock.SetTexture("_NaNTexture", nanLineTexture?.BlendMaps.Textures ?? defaultNanLine);
                        MatPropBlock.SetFloat("_NaNTextureAspect", nanLineTexture?.BlendMaps.AspectRatios[0] ?? nanLine.width / (float)nanLine.height);
                    }
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
                    MatPropBlock.SetColor("_NaNColor", nanColor?.GetColorGradient().GetPixel(0, 0) ?? ABREngine.Instance.Config.defaultNanColor);
                }
                else
                {
                    MatPropBlock.SetInt("_UseColorMap", 0);
                }
                meshRenderer.SetPropertyBlock(MatPropBlock);
            }
        }

        public override void UpdateVisibility()
        {
            if (gameObject == null)
            {
                return;
            }
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                MeshRenderer mr = gameObject.transform.GetChild(i).GetComponent<MeshRenderer>();
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

#region IDataAccessor implementation
        // public override DataPoint GetClosestDataInWorldSpace(Vector3 worldSpacePoint)
        // {
        //     return GetClosestDataInDataSpace(WorldSpacePointToDataSpace(worldSpacePoint));
        // }

        // public override DataPoint GetClosestDataInDataSpace(Vector3 dataSpacePoint)
        // {
        //     SimpleLineRenderInfo renderInfo = RenderInfo as SimpleLineRenderInfo;
        //     DataPoint closest = null;
        //     if ((renderInfo != null) && (renderInfo.vertices.Length > 0))
        //     {
        //         closest = new DataPoint()
        //         {
        //             cellIndex = 0,
        //             dataSpacePoint = renderInfo.vertices[0][0],
        //         };
        //         // int closestLine = 0;
        //         float closestDist = (dataSpacePoint - closest.dataSpacePoint).magnitude;
        //         for (int l = 0; l < renderInfo.vertices.Length; l++)
        //         {
        //             for (int v = 0; v < renderInfo.vertices[l].Length; v++)
        //             {
        //                 float dist = (dataSpacePoint - renderInfo.vertices[l][v]).magnitude;
        //                 if (dist < closestDist)
        //                 {
        //                     closest.cellIndex = l;
        //                     closest.vertexIndex = v;
        //                     closest.dataSpacePoint = renderInfo.vertices[l][v];
        //                     closestDist = dist;
        //                 }
        //             }
        //         }
        //     }

        //     closest.worldSpacePoint = DataSpacePointToWorldSpace(closest.dataSpacePoint);
        //     return closest;
        // }

        // public override List<DataPoint> GetNearbyDataInWorldSpace(Vector3 worldSpacePoint, float radiusInWorldSpace)
        // {
        //     Vector3 radiusVecWorld = new Vector3(radiusInWorldSpace, 0, 0);
        //     Vector3 radiusVecData = WorldSpaceVectorToDataSpace(radiusVecWorld);

        //     return GetNearbyDataInDataSpace(WorldSpacePointToDataSpace(worldSpacePoint), radiusVecData.magnitude);
        // }

        // public override List<DataPoint> GetNearbyDataInDataSpace(Vector3 dataSpacePoint, float radiusInDataSpace)
        // {
        //     List<DataPoint> nearbyPoints = new List<DataPoint>();
        //     SimpleLineRenderInfo renderInfo = RenderInfo as SimpleLineRenderInfo;
        //     if ((renderInfo != null) && (renderInfo.vertices.Length > 0))
        //     {
        //         for (int l = 0; l < renderInfo.vertices.Length; l++)
        //         {
        //             for (int v = 0; v < renderInfo.vertices[l].Length; v++)
        //             {
        //                 float dist = (dataSpacePoint - renderInfo.vertices[l][v]).magnitude;
        //                 if (dist < radiusInDataSpace)
        //                 {
        //                     DataPoint point = new DataPoint()
        //                     {
        //                         cellIndex = l,
        //                         vertexIndex = v,
        //                         dataSpacePoint = renderInfo.vertices[l][v],
        //                         worldSpacePoint = DataSpacePointToWorldSpace(renderInfo.vertices[l][v]),
        //                     };
        //                     nearbyPoints.Add(point);
        //                 }
        //             }
        //         }
        //     }
        //     return nearbyPoints;
        // / }

        // public override float GetScalarValueAtClosestWorldSpacePoint(Vector3 point, ScalarDataVariable variable, KeyData keyData = null)
        // {
        //     // Vector3 closestPointInWorldSpace = GetClosestDataInWorldSpace(point).worldSpacePoint;
        // }
        // public override float GetScalarValueAtClosestWorldSpacePoint(Vector3 point, string variableName, KeyData keyData = null)
        // {

        // }

        // public override float GetScalarValueAtClosestDataSpacePoint(Vector3 point, ScalarDataVariable variable, KeyData keyData = null)
        // {
        //     DataPoint closestPointInWorldSpace = GetClosestDataInDataSpace(point);
        //     SimpleLineRenderInfo renderInfo = RenderInfo as SimpleLineRenderInfo;
        //     Color scalarsAtPoint = renderInfo.scalars[closestPointInWorldSpace.cellIndex][closestPointInWorldSpace.vertexIndex];
        // }
        // public override float GetScalarValueAtClosestDataSpacePoint(Vector3 point, string variableName, KeyData keyData = null)
        // {

        // }

        // public override Vector3 GetVectorValueAtClosestWorldSpacePoint(Vector3 point, VectorDataVariable variable, KeyData keyData = null)
        // {

        // }
        // public override Vector3 GetVectorValueAtClosestWorldSpacePoint(Vector3 point, string variableName, KeyData keyData = null);

        // {

        // }
        // public override Vector3 GetVectorValueAtClosestDataSpacePoint(Vector3 point, VectorDataVariable variable, KeyData keyData = null)
        // {

        // }
        // public override Vector3 GetVectorValueAtClosestDataSpacePoint(Vector3 point, string variableName, KeyData keyData = null)
        // {

        // }

        // public override float NormalizeScalarValue(float value, KeyData keyData, ScalarDataVariable variable)
        // {

        // }
#endregion
    }
}