/* SimpleVolumeDataImpression.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>,
 * Matthias Broske <brosk014@umn.edu>
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
    class SimpleVolumeRenderInfo : IDataImpressionRenderInfo
    {
        public Texture3D voxelTex;
        public Vector3[] vertices;
        public int[] triangles;
        public Bounds bounds;
        public float stepCount;
    }

    /// <summary>
    /// A "Volumes" data impression that uses a user-defined transfer (opacity) map and a colormap to show volumetric data.
    /// </summary>
    /// <example>
    /// An example of creating a single volume data impression and setting its colormap and opacity map could be:
    /// <code>
    /// SimpleVolumeDataImpression gi = new SimpleVolumeDataImpression();
    /// gi.keyData = volume;
    /// gi.colorVariable = yAxis;
    /// gi.colormap = ABREngine.Instance.VisAssets.GetDefault&lt;ColormapVisAsset&gt;() as ColormapVisAsset;
    /// gi.opacityMap = PrimitiveGradient.Default();
    /// ABREngine.Instance.RegisterDataImpression(gi);
    /// </code>
    /// </example>
    [ABRPlateType("Volumes")]
    public class SimpleVolumeDataImpression : DataImpression
    {
        [ABRInput("Key Data", UpdateLevel.Geometry)]
        public KeyData keyData;


        /// <summary>
        /// Scalar color variable applied to each voxel of this data impression
        /// - affects both the <see cref="colormap"/> and the <see
        /// cref="opacitymap"/>.
        /// </summary>
        [ABRInput("Color Variable", UpdateLevel.Geometry)]
        public ScalarDataVariable colorVariable;

        /// <summary>
        /// Colormap applied to the <see cref="colorVariable"/>. This example
        /// switches between a linear white-to-green colormap and a linear
        /// black-to-white colormap.
        ///
        /// <img src="../resources/api/SimpleVolumeDataImpression/colormap.gif"/>
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
        /// The real power of <a
        /// href="https://en.wikipedia.org/wiki/Volume_rendering">volume
        /// rendering</a> is in the opacity map, or transfer function.
        ///
        /// For example, with a "spike" transfer function changing over time
        /// like this, we can achieve a sort of contour or isosurface scanning
        /// through the volume.
        ///
        /// <img src="../resources/api/SimpleVolumeDataImpression/transfer-fn.gif"/>
        /// <img src="../resources/api/SimpleVolumeDataImpression/opacitymap.gif"/>
        /// </summary>
        /// <examples>
        /// To achieve the "scanning"/"spike" effect shown in the gifs above, the
        /// following code was used inside the `Update()` function of a MonoBehaviour script:
        /// <code>
        ///     var x = 1.0f + Mathf.Sin(3.0f * Time.time);
        ///     float[] points = new float[] { 0.0f, x - 0.1f, x, x + 0.1f, 1.0f };
        ///     string b = "0%";
        ///     string[] values = new string[] { b, b, "100%", b, b };
        ///     PrimitiveGradient pg = new PrimitiveGradient(System.Guid.NewGuid(), points, values);
        ///     volumeDataImpression.opacitymap = pg;
        /// </code>
        /// </examples>
        [ABRInput("Opacitymap", UpdateLevel.Style)]
        public PrimitiveGradient opacitymap;

        /// <summary>
        /// Override the color used for NaN values in this data impression. If
        /// not supplied, will be 0% opacity.
        /// </summary>
        public PercentPrimitive nanOpacity;


        /// <summary>
        /// Brightness multiplier for the entire volume, irrespective of lighting.
        ///
        /// <img src="../resources/api/SimpleVolumeDataImpression/volumeBrightness.gif"/>
        /// </summary>
        [ABRInput("Volume Brightness", UpdateLevel.Style)]
        public PercentPrimitive volumeBrightness;

        /// <summary>
        /// Opacity multiplier for the entire volume; gets multiplied on top of
        /// the <see cref="opacitymap"/>.
        ///
        /// <img src="../resources/api/SimpleVolumeDataImpression/volumeBrightness.gif"/>
        /// </summary>
        [ABRInput("Volume Opacity Multiplier", UpdateLevel.Style)]
        public PercentPrimitive volumeOpacityMultiplier;

        /// <summary>
        /// Should the current scene's lighting affect the volume or not?
        ///
        /// <img src="../resources/api/SimpleVolumeDataImpression/volumeLighting.gif"/>
        /// </summary>
        /// <remarks>
        /// Lighting is often useful for understanding 3D structures and
        /// creating atmospheric effects, but may not be useful for nitty-gritty
        /// data interpretation.
        /// </remarks>
        [ABRInput("Volume Lighting", UpdateLevel.Style)]
        public BooleanPrimitive volumeLighting;


        /// <summary>
        ///    Compute buffer used to quickly pass per-voxel visibility flags to GPU
        /// </summary>
        private ComputeBuffer perVoxelVisibilityBuffer; 

        private Texture2D opacityMapTexture;


        protected override string[] MaterialNames { get; } = { "ABR_Volume" };

        /// <summary>
        /// Define the layer name for this Data Impression
        /// </summary>
        /// <remarks>
        /// > [!WARNING]
        /// > New Data Impressions should define a const string "LayerName"
        /// which corresponds to a Layer in Unity's Layer manager.
        /// </remarks>
        protected const string LayerName = "ABR_Volume";

        public override Dataset GetDataset() => keyData?.GetDataset();
        public override KeyData GetKeyData() => keyData;
        public override void SetKeyData(KeyData kd) => keyData = kd;
        public override DataTopology GetKeyDataTopology() => DataTopology.Voxels;

        // Users should NOT construct data impressions with `new DataImpression()`
        protected SimpleVolumeDataImpression() { }

        public override void ComputeGeometry()
        {
            SimpleVolumeRenderInfo renderInfo = null;

            if (keyData == null)
            {
                renderInfo = new SimpleVolumeRenderInfo
                {
                    voxelTex = null,
                    vertices = new Vector3[0],
                    triangles = new int[0],
                    bounds = new Bounds()
                };
            }
            else
            {
                // Get the key data
                RawDataset dataset;
                ABREngine.Instance.Data.TryGetRawDataset(keyData.Path, out dataset);

                // Initialize render info
                renderInfo = new SimpleVolumeRenderInfo
                {
                    bounds = dataset.bounds
                };

                // Resize the bounds of the volume to fit the rest of the data in the group
                DataImpressionGroup group = ABREngine.Instance.GetGroupFromImpression(this);
                Vector3 min = group.GroupToDataMatrix * renderInfo.bounds.min.ToHomogeneous();
                Vector3 max = group.GroupToDataMatrix * renderInfo.bounds.max.ToHomogeneous();
                renderInfo.bounds.SetMinMax(min, max);

                // Use new bounds to construct mesh geometry volume will be rendered on
                Vector3 center = renderInfo.bounds.center;
                Vector3 extents = renderInfo.bounds.extents;
                renderInfo.vertices = new Vector3[8] {
                    center + new Vector3(-extents.x, -extents.y, -extents.z),
                    center + new Vector3(extents.x, -extents.y, -extents.z),
                    center + new Vector3(extents.x, extents.y, -extents.z),
                    center + new Vector3(-extents.x, extents.y, -extents.z),
                    center + new Vector3(-extents.x, extents.y, extents.z),
                    center + new Vector3(extents.x, extents.y, extents.z),
                    center + new Vector3(extents.x, -extents.y, extents.z),
                    center + new Vector3(-extents.x, -extents.y, extents.z)
                };
                renderInfo.triangles = new int[36] {
                    0, 2, 1, //face front
                    0, 3, 2,
                    2, 3, 4, //face top
                    2, 4, 5,
                    1, 2, 5, //face right
                    1, 5, 6,
                    0, 7, 4, //face left
                    0, 4, 3,
                    5, 4, 7, //face back
                    5, 7, 6,
                    0, 6, 7, //face bottom
                    0, 1, 6
                };

                // Setup the 3D volume texture
                renderInfo.stepCount = dataset.dimensions.magnitude;
                Vector3Int dimensions = dataset.dimensions;
                renderInfo.voxelTex = new Texture3D(
                    dimensions.x,
                    dimensions.y,
                    dimensions.z,
                    TextureFormat.RGBAFloat,
                    false
                );
                renderInfo.voxelTex.wrapMode = TextureWrapMode.Clamp;

                // Only fully initialize 3D texture if there are scalars to create it out of
                if (colorVariable != null && colorVariable.IsPartOf(keyData))
                {
                    var colorScalars = colorVariable.GetArray(keyData);
                    // Set the pixels of the 3D texture

                    // Compatibility with Unity 2019 - Get a copy of the pixel
                    // data instead of retrieving a raw view of it.
                    // var pixels = renderInfo.voxelTex.GetPixelData<Color>(0);
                    var pixels = renderInfo.voxelTex.GetPixels(0);

                    for (int z = 0; z < dimensions.z; z++)
                    {
                        int zMinusOneOffset = z == 0 ? 0 : (z - 1) * dimensions.y * dimensions.x;
                        int zOffset = z * dimensions.y * dimensions.x;
                        int zPlusOneOffset = z == dimensions.z - 1 ? (dimensions.z - 1) * dimensions.y * dimensions.x : (z + 1) * dimensions.y * dimensions.x;
                        for (int y = 0; y < dimensions.y; y++)
                        {
                            int yMinusOneOffset = y == 0 ? 0 : (y - 1) * dimensions.x;
                            int yOffset = y * dimensions.x;
                            int yPlusOneOffset = y == dimensions.y - 1 ? (dimensions.y - 1) * dimensions.x : (y + 1) * dimensions.x;
                            for (int x = 0; x < dimensions.x; x++)
                            {
                                int xMinusOneOffset = x == 0 ? 0 : x - 1;
                                int xOffset = x;
                                int xPlusOneOffset = x == dimensions.x - 1 ? dimensions.x - 1 : x + 1;
                                // Compute partial derivatives in x, y and z
                                float xPartial = (colorScalars[xPlusOneOffset + yOffset + zOffset] - colorScalars[xMinusOneOffset + yOffset + zOffset]) / 2.0f;
                                float yPartial = (colorScalars[xOffset + yPlusOneOffset + zOffset] - colorScalars[xOffset + yMinusOneOffset + zOffset]) / 2.0f;
                                float zPartial = (colorScalars[xOffset + yOffset + zPlusOneOffset] - colorScalars[xOffset + yOffset + zMinusOneOffset]) / 2.0f;
                                // Compute scalar data value
                                float d = colorScalars[xOffset + yOffset + zOffset];
                                // Store gradient in color rgb and data in alpha
                                pixels[xOffset + yOffset + zOffset] = new Color(xPartial, yPartial, zPartial, d);
                            }
                        }
                    }

                    // Unity 2019 Compatibility: Since we made a copy of the
                    // pixel data, set it back to the texture.
                    renderInfo.voxelTex.SetPixels(pixels);

                    // Apply changes to the 3D texture
                    renderInfo.voxelTex.Apply();
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
                Debug.LogWarningFormat("Could not find layer {0} for SimpleVolumeDataImpression", LayerName);
            }

            // Populate volume mesh from calculated geometry
            var volumeRenderData = RenderInfo as SimpleVolumeRenderInfo;
            if (volumeRenderData != null)
            {
                Mesh mesh = meshFilter.mesh;
                if (mesh == null) mesh = new Mesh();
                mesh.Clear();
                meshFilter.mesh.name = "SSS:278@" + System.DateTime.Now.ToString();

                mesh.vertices = volumeRenderData.vertices;
                mesh.triangles = volumeRenderData.triangles;
                mesh.Optimize();

                meshFilter.mesh = mesh;

                // Apply the voxel texture to the mesh
                meshRenderer.material = ImpressionMaterials[0];
                meshRenderer.GetPropertyBlock(MatPropBlock);
                Texture3D.DestroyImmediate(MatPropBlock.GetTexture("_VolumeTexture"));
                MatPropBlock.SetTexture("_VolumeTexture", volumeRenderData.voxelTex);
                MatPropBlock.SetVector("_Center", volumeRenderData.bounds.center);
                MatPropBlock.SetVector("_Extents", volumeRenderData.bounds.extents);
                MatPropBlock.SetVector("_Dimensions", new Vector4(volumeRenderData.voxelTex.width, volumeRenderData.voxelTex.height, volumeRenderData.voxelTex.depth, 0));
                MatPropBlock.SetFloat("_StepCount", volumeRenderData.stepCount);
                meshRenderer.SetPropertyBlock(MatPropBlock);
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
            if (keyData == null || meshFilter == null || meshRenderer == null)
            {
                return;
            }

            // The mesh we wish to update the styling of (which we expect to exist if we've made it this far)
            Mesh mesh = meshFilter.mesh;

            // Record changes to color scalars if any occurred
            float colorVariableMin = 0;
            float colorVariableMax = 0;
            if (colorVariable != null && colorVariable.IsPartOf(keyData))
            {
                // Get keydata-specific range, if there is one
                if (colorVariable?.SpecificRanges.ContainsKey(keyData.Path) == true)
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

            // Load defaults from configuration / schema
            ABRConfig config = ABREngine.Instance.Config;
            string plateType = this.GetType().GetCustomAttribute<ABRPlateType>().plateType;
            float volumeBrightnessOut = volumeBrightness?.Value ??
                config.GetInputValueDefault<PercentPrimitive>(plateType, "Volume Brightness").Value;
            float volumeOpacityMultiplierOut = volumeOpacityMultiplier?.Value ??
                config.GetInputValueDefault<PercentPrimitive>(plateType, "Volume Opacity Multiplier").Value;
            bool volumeLightingOut = volumeLighting?.Value ??
                config.GetInputValueDefault<BooleanPrimitive>(plateType, "Volume Lighting").Value;

            // Apply changes to the mesh's shader / material
            meshRenderer.GetPropertyBlock(MatPropBlock);
            MatPropBlock.SetFloat("_ColorDataMin", colorVariableMin);
            MatPropBlock.SetFloat("_ColorDataMax", colorVariableMax);
            MatPropBlock.SetFloat("_VolumeBrightness", volumeBrightnessOut);
            MatPropBlock.SetFloat("_OpacityMultiplier", volumeOpacityMultiplierOut);
            MatPropBlock.SetInt("_UseLighting", volumeLightingOut ? 1 : 0);
            if (colormap != null)
            {
                MatPropBlock.SetInt("_UseColorMap", 1);
                MatPropBlock.SetTexture("_ColorMap", colormap.GetColorGradient());
                MatPropBlock.SetColor("_NaNColor", nanColor?.GetColorGradient().GetPixel(0, 0) ?? ABREngine.Instance.Config.defaultNanColor);
            }
            else
            {
                MatPropBlock.SetInt("_UseColorMap", 0);
            }
            if (opacitymap != null)
            {
                MatPropBlock.SetInt("_UseOpacityMap", 1);
                MatPropBlock.SetFloat("_NaNOpacity", nanOpacity?.Value ?? 0.0f);
                if (opacityMapTexture == null)
                {
                    opacityMapTexture = new Texture2D(1024, 1, TextureFormat.RGBA32, false);
                    opacityMapTexture.wrapMode = TextureWrapMode.Clamp;
                    opacityMapTexture.filterMode = FilterMode.Bilinear;
                }
                UpdateOpacityMap(opacityMapTexture);
                MatPropBlock.SetTexture("_OpacityMap", opacityMapTexture);
            }
            else
            {
                MatPropBlock.SetInt("_UseOpacityMap", 0);

            }

            // Per index/voxel visibility
            var volumeRenderData = RenderInfo as SimpleVolumeRenderInfo;
            int voxelCount = volumeRenderData.voxelTex.width * volumeRenderData.voxelTex.height * volumeRenderData.voxelTex.depth;
            if (RenderHints.HasPerIndexVisibility() && RenderHints.PerIndexVisibility.Count == voxelCount)
            {
                // Copy per-index bit array to int array so that it can be sent to GPU
                int[] perVoxelVisibility = new int[(voxelCount - 1) / sizeof(int) + 1];
                RenderHints.PerIndexVisibility.CopyTo(perVoxelVisibility, 0);
                // If the compute buffer exists but is the wrong size, then release and delete it
                if ((perVoxelVisibilityBuffer != null) && (perVoxelVisibilityBuffer.count != perVoxelVisibility.Length)) {
                    perVoxelVisibilityBuffer.Release();
                    perVoxelVisibilityBuffer = null;
                }
                // If the compute buffer does not exist, create it
                if (perVoxelVisibilityBuffer == null) {
                    perVoxelVisibilityBuffer = new ComputeBuffer(perVoxelVisibility.Length, sizeof(int), ComputeBufferType.Default);
                }
                // Set buffer data to int array and send to shader
                perVoxelVisibilityBuffer.SetData(perVoxelVisibility);
                MatPropBlock.SetBuffer("_PerVoxelVisibility", perVoxelVisibilityBuffer);
                MatPropBlock.SetInt("_HasPerVoxelVisibility", 1);
            }
            else
            {
                // dfk: While fixing bug related to compute buffer size and addressing Unity's warning that we need
                // to add .Release() to this code, I notice that it seems strange that this section of code is
                // creating a compute buffer of size 1.  I believe the reason is that something needs to be bound
                // to the compute buffer (i.e., it cannot be null), so we use a buffer of size 1 for the cases where
                // no per-index visibility flags are used.

                // If the compute buffer exists but is the wrong size, then release and delete it
                if ((perVoxelVisibilityBuffer != null) && (perVoxelVisibilityBuffer.count != 1)) {
                    perVoxelVisibilityBuffer.Release();
                    perVoxelVisibilityBuffer = null;
                }
                // If the compute buffer does not exist, create it
                if (perVoxelVisibilityBuffer == null) {
                    perVoxelVisibilityBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Default);
                }
                MatPropBlock.SetBuffer("_PerVoxelVisibility", perVoxelVisibilityBuffer);
                MatPropBlock.SetInt("_HasPerVoxelVisibility", 0);
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

        // Updates the opacity map texture by re-setting its pixels
        private void UpdateOpacityMap(Texture2D opacityMapTexture)
        {
            // Sort the points/values array
            Array.Sort(opacitymap.Points, opacitymap.Values);

            // Initialize array to store pixel colors
            var pixelColors = new Color[opacityMapTexture.width * opacityMapTexture.height];

            // Fill pixels with first control point's color up to first control point
            int firstControlPoint = (int)(opacityMapTexture.width * opacitymap.Points[0]);
            float firstControlPointValue = new PercentPrimitive(opacitymap.Values[0]).Value;
            for (int p = 0; p < firstControlPoint; p++)
            {
                pixelColors[p] = new Color(firstControlPointValue, firstControlPointValue, firstControlPointValue);
            }
            // Interpolate between values for all control points
            int prevControlPoint = firstControlPoint;
            float prevValue = firstControlPointValue;
            for (int i = 1; i < opacitymap.Points.Length; i++)
            {
                int nextControlPoint = (int)(opacityMapTexture.width * opacitymap.Points[i]);
                float nextValue = new PercentPrimitive(opacitymap.Values[i]).Value;
                for (int p = prevControlPoint; p < nextControlPoint; p++)
                {
                    float lerpValue = Mathf.Lerp(prevValue, nextValue, ((float)(p - prevControlPoint) / (nextControlPoint - prevControlPoint)));
                    pixelColors[p] = new Color(lerpValue, lerpValue, lerpValue);
                }
                prevControlPoint = nextControlPoint;
                prevValue = new PercentPrimitive(opacitymap.Values[i]).Value;
            }
            // Fill remaining pixels with final control point's color
            for (int p = prevControlPoint; p < opacityMapTexture.width; p++)
            {
                pixelColors[p] = new Color(prevValue, prevValue, prevValue);
            }

            // Repeat in each row for the rest of the texture
            for (int i = 1; i < opacityMapTexture.height; i++)
            {
                for (int j = 0; j < opacityMapTexture.width; j++)
                {
                    pixelColors[i * opacityMapTexture.width + j] = pixelColors[(i - 1) * opacityMapTexture.width + j];
                }
            }

            // Apply updated pixels to texture
            opacityMapTexture.SetPixels(pixelColors);
            opacityMapTexture.Apply();
        }

        void OnDisable()
        { 
            if (perVoxelVisibilityBuffer != null)
                perVoxelVisibilityBuffer.Release();
            perVoxelVisibilityBuffer = null;
        }
    }
}
