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
    class SimpleGlyphRenderInfo : IDataImpressionRenderInfo
    {
        public Matrix4x4[] transforms;
        public Vector4[] scalars;
        public Bounds bounds;
    }

    /// <summary>
    /// A "Glyphs" data impression that uses hand-sculpted geometry to depict point data.
    /// </summary>
    /// <example>
    /// An example of creating a single glyph data impression and setting its colormap, color variable, and glyph could be:
    /// <code>
    /// SimpleGlyphDataImpression gi = new SimpleGlyphDataImpression();
    /// gi.keyData = points;
    /// gi.colorVariable = yAxis;
    /// gi.colormap = ABREngine.Instance.VisAssets.GetDefault&lt;ColormapVisAsset&gt;() as ColormapVisAsset;
    /// gi.glyph = glyph;
    /// ABREngine.Instance.RegisterDataImpression(gi);
    /// </code>
    /// </example>
    [ABRPlateType("Glyphs")]
    public class SimpleGlyphDataImpression : DataImpression
    {
        [ABRInput("Key Data", "Key Data", UpdateLevel.Data)]
        public KeyData keyData;

        /// <summary>
        /// Scalar color variable applied to each point of this data impression.
        /// This example switches between X-axis monotonically increasing and
        /// Y-axis monotonically increasing.
        ///
        /// <img src="../resources/api/SimpleGlyphDataImpression/colorVariable.gif"/>
        /// </summary>
        [ABRInput("Color Variable", "Color", UpdateLevel.Style)]
        public ScalarDataVariable colorVariable;

        /// <summary>
        /// Colormap applied to the <see cref="colorVariable"/>. This example
        /// switches between a linear white-to-green colormap and a linear
        /// black-to-white colormap.
        ///
        /// <img src="../resources/api/SimpleGlyphDataImpression/colormap.gif"/>
        /// </summary>
        [ABRInput("Colormap", "Color", UpdateLevel.Style)]
        public IColormapVisAsset colormap;

        /// <summary>
        /// Override the color used for NaN values in this data impression. If
        /// not supplied, will use the <see cref="ABRConfig.defaultNanColor"/>.
        /// </summary>
        public IColormapVisAsset nanColor;

        /// <summary>
        /// Variable used to determine which glyph to render at which data
        /// values. This only has any effect if <see cref="glyph"/> is a <see
        /// cref="GlyphGradient"/>.
        /// </summary>
        [ABRInput("Glyph Variable", "Glyph", UpdateLevel.Style)]
        public ScalarDataVariable glyphVariable;

        /// <summary>
        /// What glyph(s) to apply to the dataset. This can also take a <see
        /// cref="GlyphGradient"/>. This example alternates between spherical
        /// and thin cylindrical glyphs.
        ///
        /// <img src="../resources/api/SimpleGlyphDataImpression/glyph.gif"/>
        /// </summary>
        [ABRInput("Glyph", "Glyph", UpdateLevel.Data)]
        public IGlyphVisAsset glyph;

        /// <summary>
        /// Adjust the size of the glyphs (in Unity-space meters).
        ///
        /// <img src="../resources/api/SimpleGlyphDataImpression/glyphSize.gif"/>
        /// </summary>
        [ABRInput("Glyph Size", "Glyph", UpdateLevel.Style)]
        public LengthPrimitive glyphSize;

        /// <summary>
        /// Tweak the density of glyphs - subsamples the existing glyphs uniformly.
        /// </summary>
        [ABRInput("Glyph Density", "Glyph", UpdateLevel.Style)]
        public PercentPrimitive glyphDensity;

        /// <summary>
        /// "Forward" direction that glyphs should point in.
        /// </summary>
        [ABRInput("Forward Variable", "Direction", UpdateLevel.Data)]
        public VectorDataVariable forwardVariable;

        /// <summary>
        /// "Up" direction that glyphs should point in.
        /// </summary>
        [ABRInput("Up Variable", "Direction", UpdateLevel.Data)]
        public VectorDataVariable upVariable;

        /// <summary>
        /// Level of detail to use for glyph rendering (higher number = lower
        /// level of detail; most glyphs have 3 LODs)
        /// </summary>
        public int glyphLod = 1;

        /// <summary>
        /// Use random forward/up directions when no Vector variables are
        /// applied for forward/up.
        /// </summary>
        public bool useRandomOrientation = true;

        /// <summary>
        /// Show/hide outline on this data impression
        ///
        /// <img src="../resources/api/SimpleGlyphDataImpression/showOutline.gif"/>
        /// </summary>
        public BooleanPrimitive showOutline;

        /// <summary>
        /// Width (in Unity world coords) of the outline
        ///
        /// <img src="../resources/api/SimpleGlyphDataImpression/outlineWidth.gif"/>
        /// </summary>
        public LengthPrimitive outlineWidth;

        /// <summary>
        /// Color of the outline
        ///
        /// <img src="../resources/api/SimpleGlyphDataImpression/outlineColor.gif"/>
        /// </summary>
        public Color outlineColor;

        /// <summary>
        /// Force the use of <see cref="outlineColor"/> even if there's a
        /// colormap applied to the data. This example alternates between a
        /// white-to-green linear colormap (false) and a solid purple-blue
        /// (true)
        ///
        /// <img src="../resources/api/SimpleGlyphDataImpression/forceOutlineColor.gif"/>
        /// </summary>
        public BooleanPrimitive forceOutlineColor;

        /// <summary>
        ///    Compute buffer used to quickly pass per-glyph visibility flags to GPU
        /// </summary>
        private ComputeBuffer perGlyphVisibilityBuffer;

        /// <summary>
        /// Random indices to draw from in <see cref="SampleGlyphs"/>. This
        /// enables us to sample the glyphs consistently between frames.
        /// </summary>
        private int[] randomSelections;

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

                int numPoints = dataset.vertexArray.Length;

                // Compute positions for each point, in room (Unity) space
                Vector3[] positions = new Vector3[numPoints];
                for (int i = 0; i < numPoints; i++)
                {
                    positions[i] = group.GroupToDataMatrix * dataset.vertexArray[i].ToHomogeneous();
                }

                // Get up and forwards vectors at each point
                Vector3[] dataForwards = null;
                Vector3[] dataUp = null;
                if (forwardVariable != null && forwardVariable.IsPartOf(keyData))
                {
                    dataForwards = forwardVariable.GetArray(keyData);
                }
                else
                {
                    var rand = new System.Random(0);
                    dataForwards = new Vector3[numPoints];
                    for (int i = 0; i < numPoints; i++)
                    {
                        if (useRandomOrientation)
                            dataForwards[i] = new Vector3(
                                (float)rand.NextDouble() * 2 - 1,
                                (float)rand.NextDouble() * 2 - 1,
                                (float)rand.NextDouble() * 2 - 1);
                        else
                            dataForwards[i] = Vector3.forward;
                    }
                }

                if (upVariable != null && upVariable.IsPartOf(keyData))
                {
                    dataUp = upVariable.GetArray(keyData);
                }
                else
                {
                    var rand = new System.Random(1);
                    dataUp = new Vector3[numPoints];
                    for (int i = 0; i < numPoints; i++)
                    {
                        if (useRandomOrientation)
                            dataUp[i] = new Vector3(
                                (float)rand.NextDouble() * 2 - 1,
                                (float)rand.NextDouble() * 2 - 1,
                                (float)rand.NextDouble() * 2 - 1);
                        else
                            dataUp[i] = Vector3.up;
                    }
                }

                // Compute orientations for each point
                Quaternion[] orientations = new Quaternion[numPoints];
                if (upVariable != null && forwardVariable != null)
                { // Treat up as the more rigid constraint
                    for (int i = 0; i < numPoints; i++)
                    {
                        Vector3 rightAngleForward = Vector3.Cross(
                            Vector3.Cross(dataUp[i], dataForwards[i]).normalized,
                            dataUp[i]
                        ).normalized;

                        Quaternion orientation = Quaternion.LookRotation(rightAngleForward, dataUp[i]) * Quaternion.Euler(0, 180, 0);
                        orientations[i] = orientation;
                    }
                }
                else // Treat forward as the more rigid constraint
                {
                    for (int i = 0; i < numPoints; i++)
                    {
                        Vector3 rightAngleUp = Vector3.Cross(
                            Vector3.Cross(dataForwards[i], dataUp[i]).normalized,
                            dataForwards[i]
                        ).normalized;

                        Quaternion orientation = Quaternion.LookRotation(dataForwards[i], rightAngleUp) * Quaternion.Euler(0, 180, 0);
                        orientations[i] = orientation;
                    }
                }

                var encodingRenderInfo = new SimpleGlyphRenderInfo()
                {
                    transforms = new Matrix4x4[numPoints],
                    scalars = new Vector4[numPoints]
                };

                // Get glyph scale and apply to instance mesh renderer transform
                ABRConfig config = ABREngine.Instance.Config;
                string plateType = this.GetType().GetCustomAttribute<ABRPlateType>().plateType;
                float glyphScale = glyphSize?.Value ??
                    config.GetInputValueDefault<LengthPrimitive>(plateType, "Glyph Size").Value;

                for (int i = 0; i < numPoints; i++)
                {
                    encodingRenderInfo.transforms[i] = Matrix4x4.TRS(positions[i], orientations[i], Vector3.one * glyphScale);
                }

                // Apply room-space bounds to renderer
                encodingRenderInfo.bounds = group.GroupBounds;
                RenderInfo = encodingRenderInfo;

                // compute random values for SampleGlyphs
                // for use as `j` in reservoir sampling... numbers go from 0 to the number of glyphs
                randomSelections = new int[numPoints];
                for (int i = 0; i < randomSelections.Length; i++)
                {
                    randomSelections[i] = UnityEngine.Random.Range(0, numPoints);
                }
            }
        }

        public override void SetupGameObject()
        {
            var SSrenderData = RenderInfo as SimpleGlyphRenderInfo;
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

            // Return all previous renderers to pool
            while (gameObject.transform.childCount > 0)
            {
                GameObject child = gameObject.transform.GetChild(0).gameObject;
                GenericObjectPool.Instance.ReturnObjectToPool(child);
            }

            // Create pooled game objects with mesh renderer and instanced mesh renderer
            int rendererCount = glyph?.VisAssetCount - 1 ?? 0;
            for (int stopIndex = -1; stopIndex < rendererCount; stopIndex++)
            {
                GameObject childRenderer = GenericObjectPool.Instance.GetObjectFromPool(this.GetType() + "GlyphRenderer", gameObject.transform, (go) =>
                {
                    go.name = "Glyph Renderer Object " + stopIndex;
                });

                // Parent the glyph renderer to this Data Impression and ensure that it's centered correctly
                // Unsure why necessary...
                // See also: PrepareImpression method of DataImpressionGroup class
                childRenderer.transform.SetParent(gameObject.transform, false);
                childRenderer.transform.localPosition = Vector3.zero;
                childRenderer.transform.localRotation = Quaternion.identity;

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
                    Debug.LogWarning($"Instanced mesh renderer for glyph impression {this.Uuid} (stop {stopIndex}) is null, skipping");
                    return;
                }

                imr.bounds = SSrenderData.bounds;
                imr.instanceMaterial = ImpressionMaterials[0];
                imr.block = new MaterialPropertyBlock();
                imr.cachedInstanceCount = -1;
            }
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
            for (int glyphIndex = 0; glyphIndex < gameObject.transform.childCount; glyphIndex++)
            {
                // Exit immediately if the game object or instanced mesh renderer relevant to this
                // impression do not yet exist
                InstancedMeshRenderer imr = gameObject?.transform.GetChild(glyphIndex).GetComponent<InstancedMeshRenderer>();
                if (imr == null)
                    continue;

                imr.instanceLocalTransforms = SSrenderData.transforms;
                imr.renderInfo = SSrenderData.scalars;

                // Set up outline, if present
                if (showOutline != null && showOutline.Value)
                    imr.instanceMaterial = ImpressionMaterials[1];
                else
                    imr.instanceMaterial = ImpressionMaterials[0];

                // Determine the number of points / glyphs via the number of transforms the
                // instanced mesh renderer is currently tracking
                int numPoints = imr.instanceLocalTransforms.Length;

                // We might as well exit if there are no glyphs to update
                if (numPoints <= 0)
                    continue;

                // Create a new MaterialPropertyBlock for this specific glyph
                MaterialPropertyBlock block = new MaterialPropertyBlock();

                // Rescale the glyphs depending on their current "Glyph Size" input
                ABRConfig config = ABREngine.Instance.Config;
                string plateType = this.GetType().GetCustomAttribute<ABRPlateType>().plateType;
                float curGlyphScale = glyphSize?.Value ??
                    config.GetInputValueDefault<LengthPrimitive>(plateType, "Glyph Size").Value;

                // However, don't waste time rescaling the glyphs if the scale hasn't actually changed
                // (If at some point we are no longer scaling all glyphs evenly and equally, this trick
                // to determine if the scale changed will likely no longer function correctly)
                float prevGlyphScale = imr.instanceLocalTransforms[0].GetColumn(0).magnitude;
                if (!Mathf.Approximately(prevGlyphScale, curGlyphScale))
                {
                    for (int i = 0; i < imr.instanceLocalTransforms.Length; i++)
                    {
                        imr.instanceLocalTransforms[i] *= Matrix4x4.Scale(Vector3.one * curGlyphScale / prevGlyphScale);
                    }
                }

                // Update the instanced mesh renderer to use the currently selected glyph
                if (glyph != null && glyph.GetMesh(glyphIndex, glyphLod) != null)
                {
                    imr.instanceMesh = glyph.GetMesh(glyphIndex, glyphLod);
                    Texture2D normalMap = glyph.GetNormalMap(glyphIndex, glyphLod);
                    if (normalMap != null)
                    {
                        block.SetTexture("_Normal", normalMap);
                    }
                }
                else
                {
                    Mesh mesh = ABREngine.Instance.Config.defaultGlyph.GetComponent<MeshFilter>().sharedMesh;
                    imr.instanceMesh = mesh;
                }

                // Initialize "render info" -- stores scalar values and info on whether
                // or not glyphs should be rendered
                Vector4[] glyphRenderInfo = new Vector4[numPoints];

                // Re-sample based on glyph density, if it has changed
                float glyphDensityOut = glyphDensity?.Value ??
                    config.GetInputValueDefault<PercentPrimitive>(plateType, "Glyph Density").Value;
                glyphDensityOut = Mathf.Clamp01(glyphDensityOut);
                if (imr.instanceDensity != glyphDensityOut)
                {
                    // Sample number of glyphs based on density
                    int sampleSize = (int)(numPoints * glyphDensityOut);
                    SampleGlyphs(glyphRenderInfo, sampleSize);
                }
                // If the glyph density hasn't changed, use the previous sample of glyphs
                else if (imr.renderInfo?.Length == glyphRenderInfo.Length)
                {
                    glyphRenderInfo = imr.renderInfo;
                }

                // Hide/show glyphs based on per index visibility
                if (RenderHints.HasPerIndexVisibility() && RenderHints.PerIndexVisibility.Count == numPoints)
                {
                    // Copy per-index bit array to int array so that it can be sent to GPU
                    int[] perGlyphVisibility = new int[(numPoints - 1) / sizeof(int) + 1];
                    RenderHints.PerIndexVisibility.CopyTo(perGlyphVisibility, 0);
                    // Initialize the compute buffer if it is uninitialized
                    if (perGlyphVisibilityBuffer == null)
                        perGlyphVisibilityBuffer = new ComputeBuffer(perGlyphVisibility.Length, sizeof(int), ComputeBufferType.Default);
                    // Set buffer data to int array and send to shader
                    perGlyphVisibilityBuffer.SetData(perGlyphVisibility);
                    block.SetBuffer("_PerGlyphVisibility", perGlyphVisibilityBuffer);
                    block.SetInt("_HasPerGlyphVisibility", 1);
                }
                else
                {
                    block.SetInt("_HasPerGlyphVisibility", 0);
                    // Bridger 2023-07:
                    // This line generates a warning about disposing of compute buffers... commenting out for now
                    // 2023-08-29:
                    // This line being commented out causes glyphs to not work on MacOS.
                    // For now, we just check if we're on MacOS to do this line...
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                    block.SetBuffer("_PerGlyphVisibility", new ComputeBuffer(1, sizeof(int), ComputeBufferType.Default));
#endif
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

                // INDEX 1: Glyph
                if (glyphVariable != null && glyphVariable.IsPartOf(keyData))
                {
                    var glyphScalars = glyphVariable.GetArray(keyData);
                    for (int i = 0; i < numPoints; i++)
                    {
                        glyphRenderInfo[i][1] = glyphScalars[i];
                    }
                }

                // Apply scalar/density changes to the instanced mesh renderer
                imr.instanceDensity = glyphDensityOut;
                imr.renderInfo = glyphRenderInfo;

                // If we're rendering different glyphs based on a scalar variable, filter these now, otherwise leave as-is
                if (glyph?.VisAssetCount > 1 && glyphVariable != null && glyphVariable.IsPartOf(keyData))
                {
                    GlyphGradient gradient = glyph as GlyphGradient;
                    // Determine if a scalar value falls within the range of this glyph's gradient stop
                    Func<float, bool> filterData = (float scalarValue) =>
                    {
                        int stopIndex = glyphIndex - 1;
                        if (gradient.Stops.Count == 0)
                            return true;

                        if (stopIndex < 0)
                            return scalarValue < gradient.Stops[0];
                        else if (stopIndex >= gradient.Stops.Count - 1)
                            return scalarValue >= gradient.Stops[gradient.Stops.Count - 1];
                        else
                            return scalarValue >= gradient.Stops[stopIndex] && scalarValue < gradient.Stops[stopIndex + 1];
                    };
                    // Calculate subset of data (transforms) for this renderer
                    Matrix4x4[] transformsWithThisGlyph = imr.instanceLocalTransforms.Where((tf, i) =>
                    {
                        // Glyph variable is packed at index 1
                        float scalarValue = glyphRenderInfo[i][1];
                        float normalizedScalarValue = (scalarValue - glyphVariable.Range.min) / (glyphVariable.Range.max - glyphVariable.Range.min);
                        return filterData(normalizedScalarValue);
                    }).ToArray();

                    // Calculate subset of data (actual data values) for this renderer
                    Vector4[] scalarValuesWithThisGlyph = glyphRenderInfo.Where((sc, i) =>
                    {
                        // Glyph variable is packed at index 1
                        float scalarValue = glyphRenderInfo[i][1];
                        float normalizedScalarValue = (scalarValue - glyphVariable.Range.min) / (glyphVariable.Range.max - glyphVariable.Range.min);
                        return filterData(normalizedScalarValue);
                    }).ToArray();

                    // Re-apply transforms and render info for THIS specific glyph
                    imr.instanceLocalTransforms = transformsWithThisGlyph;
                    imr.renderInfo = scalarValuesWithThisGlyph;
                }

                // Apply changes to the mesh's shader / material
                block.SetFloat("_ColorDataMin", colorVariableMin);
                block.SetFloat("_ColorDataMax", colorVariableMax);
                block.SetColor("_Color", ABREngine.Instance.Config.defaultColor);
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
            perGlyphVisibilityBuffer?.Release();
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
                // int j = UnityEngine.Random.Range(0, i + 1);
                // int j = i + 1;
                // Choose `j` based on earlier random selections
                // Technically not quite correct since it's not uniformly from
                // the same distribution as Random.Range(0, i+1). But, it looks
                // decent so we're leaving it for now...
                int j = randomSelections[i] % (i + 1);

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

        void OnDisable()
        {
            if (perGlyphVisibilityBuffer != null)
                perGlyphVisibilityBuffer.Release();
            perGlyphVisibilityBuffer = null;
        }
    }
}