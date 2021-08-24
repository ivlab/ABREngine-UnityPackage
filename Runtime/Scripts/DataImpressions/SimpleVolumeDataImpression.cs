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
        public Color[] scalars;
        public float colorVariableMin;
        public float colorVariableMax;
        public Bounds bounds;
        public Vector3Int dimensions;
    }

    [ABRPlateType("Volumes")]
    public class SimpleVolumeDataImpression : DataImpression, IDataImpression
    {
        [ABRInput("Key Data", "Key Data", UpdateLevel.Data)]
        public PointKeyData keyData;


        [ABRInput("Color Variable", "Color", UpdateLevel.Style)]
        public ScalarDataVariable colorVariable;

        [ABRInput("Colormap", "Color", UpdateLevel.Style)]
        public ColormapVisAsset colormap;

        [ABRInput("Opacitymap", "Color", UpdateLevel.Style)]
        public PrimitiveGradient opacitymap;


        [ABRInput("Volume Brightness", "Volume", UpdateLevel.Style)]
        public PercentPrimitive volumeBrightness;

        [ABRInput("Volume Opacity Multiplier", "Volume", UpdateLevel.Style)]
        public PercentPrimitive volumeOpacityMultiplier;

        [ABRInput("Volume Lighting", "Volume", UpdateLevel.Style)]
        public BooleanPrimitive volumeLighting;


        protected override string MaterialName { get; } = "ABR_DataVolume";
        protected override string LayerName { get; } = "ABR_Volume";

        /// <summary>
        ///     Construct a data impression with a given UUID. Note that this
        ///     will be called from ABRState and must assume that there's a
        ///     single string argument with UUID.
        /// </summary>
        public SimpleVolumeDataImpression(string uuid) : base(uuid) { }
        public SimpleVolumeDataImpression() : base() { }

        public override Dataset GetDataset()
        {
            return keyData?.GetDataset();
        }

        public override void ComputeKeyDataRenderInfo() { }

        public override void ComputeRenderInfo()
        {
            SimpleVolumeRenderInfo renderInfo = null;

            if (keyData == null)
            {
                renderInfo = new SimpleVolumeRenderInfo
                {
                    voxelTex = null,
                    scalars = new Color[0],
                    colorVariableMin = 0,
                    colorVariableMax = 0,
                    bounds = new Bounds(),
                    dimensions = Vector3Int.zero
                };
            }
            else
            {
                // Get the key data
                RawDataset dataset;
                ABREngine.Instance.Data.TryGetRawDataset(keyData.Path, out dataset);

                // Determine the number of voxels in key data
                int sourceVoxelCount = dataset.vertexArray.Length;

                // Initialize render info
                renderInfo = new SimpleVolumeRenderInfo
                {
                    scalars = new Color[sourceVoxelCount],
                    colorVariableMin = 0,
                    colorVariableMax = 0,
                    bounds = dataset.bounds,
                    dimensions = dataset.dimensions
                };

                // Initialize color scalars
                if (colorVariable != null && colorVariable.IsPartOf(keyData))
                {
                    var colorScalars = colorVariable.GetArray(keyData);
                    for (int i = 0; i < sourceVoxelCount; i++)
                        renderInfo.scalars[i][0] = colorScalars[i];

                    renderInfo.colorVariableMin = colorVariable.MinValue;
                    renderInfo.colorVariableMax = colorVariable.MaxValue;
                }

                // Setup the new texture
                renderInfo.voxelTex = new Texture3D(
                    renderInfo.dimensions.x,
                    renderInfo.dimensions.y,
                    renderInfo.dimensions.z,
                    TextureFormat.RGBAFloat,
                    false
                );
                renderInfo.voxelTex.wrapMode = TextureWrapMode.Clamp;

                // Set the pixels of the new texture
                Color[] pixels = new Color[renderInfo.dimensions.x * renderInfo.dimensions.y * renderInfo.dimensions.z];
                for (int z = 0; z < renderInfo.dimensions.z; z++)
                {
                    int zMinusOneOffset = z == 0 ? 0 : (z - 1) * renderInfo.dimensions.y * renderInfo.dimensions.x;
                    int zOffset = z * renderInfo.dimensions.y * renderInfo.dimensions.x;
                    int zPlusOneOffset = z == renderInfo.dimensions.z - 1 ? (renderInfo.dimensions.z - 1) * renderInfo.dimensions.y * renderInfo.dimensions.x : (z + 1) * renderInfo.dimensions.y * renderInfo.dimensions.x;
                    int zOffsetFlipped = (renderInfo.dimensions.z - z - 1) * renderInfo.dimensions.y * renderInfo.dimensions.x;
                    for (int y = 0; y < renderInfo.dimensions.y; y++)
                    {
                        int yMinusOneOffset = y == 0 ? 0 : (y - 1) * renderInfo.dimensions.x;
                        int yOffset = y * renderInfo.dimensions.x;
                        int yPlusOneOffset = y == renderInfo.dimensions.y - 1 ? (renderInfo.dimensions.y - 1) * renderInfo.dimensions.x : (y + 1) * renderInfo.dimensions.x;
                        for (int x = 0; x < renderInfo.dimensions.x; x++)
                        {
                            int xMinusOneOffset = x == 0 ? 0 : x - 1;
                            int xOffset = x;
                            int xPlusOneOffset = x == renderInfo.dimensions.x - 1 ? renderInfo.dimensions.x - 1 : x + 1;
                            // Compute partial derivatives in x, y and z
                            float xPartial = (renderInfo.scalars[xPlusOneOffset + yOffset + zOffset][0] - renderInfo.scalars[xMinusOneOffset + yOffset + zOffset][0]) / 2.0f;
                            float yPartial = (renderInfo.scalars[xOffset + yPlusOneOffset + zOffset][0] - renderInfo.scalars[xOffset + yMinusOneOffset + zOffset][0]) / 2.0f;
                            float zPartial = (renderInfo.scalars[xOffset + yOffset + zPlusOneOffset][0] - renderInfo.scalars[xOffset + yOffset + zMinusOneOffset][0]) / 2.0f;
                            // Compute scalar data value
                            float d = renderInfo.scalars[xOffset + yOffset + zOffset][0];
                            // Store gradient in color rgb and data in alpha
                            // (flip z to account for RH -> LH coordinate system conversion)
                            pixels[xOffset + yOffset + zOffsetFlipped] = new Color(xPartial, yPartial, -zPartial, d);
                        }
                    }
                }
                renderInfo.voxelTex.SetPixels(pixels);

                // Apply changes to the texture
                renderInfo.voxelTex.Apply();
            }
            RenderInfo = renderInfo;
        }

        public override void ApplyToGameObject(EncodedGameObject currentGameObject) {
            // Obtain the now populated render info
            var volumeRenderData = RenderInfo as SimpleVolumeRenderInfo;

            if (currentGameObject == null)
            {
                return;
            }

            // Get/add mesh-related components
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
            meshRenderer.enabled = RenderHints.Visible;

            // Set layer and name
            int layerID = LayerMask.NameToLayer(LayerName);
            if (layerID >= 0)
            {
                currentGameObject.gameObject.layer = layerID;
            }
            else
            {
                Debug.LogWarningFormat("Could not find layer {0} for SimpleVolumeDataImpression", LayerName);
            }
            currentGameObject.name = this + " volume";

            // Apply render data
            if (volumeRenderData != null)
            {
                // Resize the bounds of the volume to fit the rest of the data in the group
                DataImpressionGroup group = ABREngine.Instance.GetGroupFromImpression(this);
                Vector3 min = group.GroupToDataMatrix * volumeRenderData.bounds.min.ToHomogeneous();
                Vector3 max = group.GroupToDataMatrix * volumeRenderData.bounds.max.ToHomogeneous();
                volumeRenderData.bounds.SetMinMax(min, max);

                // Use new bounds to construct mesh volume will be rendered on
                Vector3 center = volumeRenderData.bounds.center;
                Vector3 extents = volumeRenderData.bounds.extents;
                Vector3[] verts = {
                    center + new Vector3(-extents.x, -extents.y, -extents.z),
                    center + new Vector3(extents.x, -extents.y, -extents.z),
                    center + new Vector3(extents.x, extents.y, -extents.z),
                    center + new Vector3(-extents.x, extents.y, -extents.z),
                    center + new Vector3(-extents.x, extents.y, extents.z),
                    center + new Vector3(extents.x, extents.y, extents.z),
                    center + new Vector3(extents.x, -extents.y, extents.z),
                    center + new Vector3(-extents.x, -extents.y, extents.z)
                };
                int[] tris = {
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
                Mesh mesh = meshFilter.mesh;
                mesh.Clear();
                mesh.vertices = verts;
                mesh.triangles = tris;
                mesh.Optimize();
                mesh.RecalculateNormals();
                meshFilter.mesh = mesh;
                meshFilter.mesh.name = "SSS:278@" + System.DateTime.Now.ToString();


                // Set/get the volume rendering material
                meshRenderer.material = ImpressionMaterial;
                meshRenderer.GetPropertyBlock(MatPropBlock);

                // Set the the voxel texture
                MatPropBlock.SetTexture("_VolumeTexture", volumeRenderData.voxelTex);

                // Set the bounds
                MatPropBlock.SetVector("_Center", volumeRenderData.bounds.center);
                MatPropBlock.SetVector("_Extents", volumeRenderData.bounds.extents);

                // Set the colormap
                if (colormap != null)
                {
                    MatPropBlock.SetInt("_UseColorMap", 1);
                    MatPropBlock.SetTexture("_ColorMap", colormap.GetColorGradient());
                }
                else
                {
                    MatPropBlock.SetInt("_UseColorMap", 0);

                }
                MatPropBlock.SetFloat("_ColorDataMin", volumeRenderData.colorVariableMin);
                MatPropBlock.SetFloat("_ColorDataMax", volumeRenderData.colorVariableMax);

                // Set the opacitymap
                if (opacitymap != null)
                {
                    MatPropBlock.SetInt("_UseOpacityMap", 1);
                    MatPropBlock.SetTexture("_OpacityMap", GenerateOpacityMap());
                }
                else
                {
                    MatPropBlock.SetInt("_UseOpacityMap", 0);

                }

                ABRConfig config = ABREngine.Instance.Config;
                string plateType = this.GetType().GetCustomAttribute<ABRPlateType>().plateType;

                // Set the brightness
                float volumeBrightnessOut = volumeBrightness?.Value ??
                    config.GetInputValueDefault<PercentPrimitive>(plateType, "Volume Brightness").Value;
                MatPropBlock.SetFloat("_VolumeBrightness", volumeBrightnessOut);

                // Set the opacity multiplier
                float volumeOpacityMultiplierOut = volumeOpacityMultiplier?.Value ??
                    config.GetInputValueDefault<PercentPrimitive>(plateType, "Volume Opacity Multiplier").Value;
                MatPropBlock.SetFloat("_OpacityMultiplier", volumeOpacityMultiplierOut);

                // Set the lighting toggle
                bool volumeLightingOut = volumeLighting?.Value ??
                    config.GetInputValueDefault<BooleanPrimitive>(plateType, "Volume Lighting").Value;
                MatPropBlock.SetInt("_UseLighting", volumeLightingOut ? 1 : 0);

                // Apply the material properties
                meshRenderer.SetPropertyBlock(MatPropBlock);
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
            mesh.name = "SSS:278@" + System.DateTime.Now.ToString();

            // Determine the number of voxels in the dataset
            RawDataset dataset;
            ABREngine.Instance.Data.TryGetRawDataset(keyData.Path, out dataset);
            int sourceVoxelCount = dataset.vertexArray.Length;

            // Initialize variables to track scalar "styling" changes
            Color[] scalars = new Color[sourceVoxelCount];
            float colorVariableMin = 0;
            float colorVariableMax = 0;

            // Record changes to color scalars if any occurred
            if (colorVariable != null && colorVariable.IsPartOf(keyData))
            {
                var colorScalars = colorVariable.GetArray(keyData);
                for (int i = 0; i < sourceVoxelCount; i++)
                    scalars[i][0] = colorScalars[i];

                colorVariableMin = colorVariable.MinValue;
                colorVariableMax = colorVariable.MaxValue;
            }

            // Get the material
            meshRenderer.material = ImpressionMaterial;
            meshRenderer.GetPropertyBlock(MatPropBlock);

            // Update the colormap
            if (colormap != null)
            {
                MatPropBlock.SetInt("_UseColorMap", 1);
                MatPropBlock.SetTexture("_ColorMap", colormap.GetColorGradient());
            }
            else
            {
                MatPropBlock.SetInt("_UseColorMap", 0);

            }
            MatPropBlock.SetFloat("_ColorDataMin", colorVariableMin);
            MatPropBlock.SetFloat("_ColorDataMax", colorVariableMax);

            // Update the opacitymap
            if (opacitymap != null)
            {
                MatPropBlock.SetInt("_UseOpacityMap", 1);
                MatPropBlock.SetTexture("_OpacityMap", GenerateOpacityMap());
            }
            else
            {
                MatPropBlock.SetInt("_UseOpacityMap", 0);

            }

            ABRConfig config = ABREngine.Instance.Config;
            string plateType = this.GetType().GetCustomAttribute<ABRPlateType>().plateType;

            // Update the brightness
            float volumeBrightnessOut = volumeBrightness?.Value ??
                config.GetInputValueDefault<PercentPrimitive>(plateType, "Volume Brightness").Value;
            MatPropBlock.SetFloat("_VolumeBrightness", volumeBrightnessOut);

            // Update the opacity multiplier
            float volumeOpacityMultiplierOut = volumeOpacityMultiplier?.Value ??
                config.GetInputValueDefault<PercentPrimitive>(plateType, "Volume Opacity Multiplier").Value;
            MatPropBlock.SetFloat("_OpacityMultiplier", volumeOpacityMultiplierOut);

            // Update the lighting toggle
            bool volumeLightingOut = volumeLighting?.Value ??
                config.GetInputValueDefault<BooleanPrimitive>(plateType, "Volume Lighting").Value;
            MatPropBlock.SetInt("_UseLighting", volumeLightingOut ? 1 : 0);

            // Apply the material properties
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

        private Texture2D GenerateOpacityMap()
        {
            int width = 1024;
            int height = 10;

            if (opacitymap.Points.Length == 0 || opacitymap.Values.Length == 0)
            {
                return new Texture2D(0, 0);
            }

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.wrapMode = TextureWrapMode.Clamp;
            result.filterMode = FilterMode.Bilinear;  // Is bilinear filtering going to cause problems?

            // Sort the points/values array
            Array.Sort(opacitymap.Points, opacitymap.Values);

            Color[] pixelColors = new Color[width * height];

            // Fill pixels with black until first control point
            int firstControlPoint = (int)(width * opacitymap.Points[0]);
            for (int p = 0; p < firstControlPoint; p++)
            {
                pixelColors[p] = Color.black;
            }
            // Interpolate between values for all control points
            int prevControlPoint = firstControlPoint;
            float prevValue = new FloatPrimitive(opacitymap.Values[0]).Value;
            for (int i = 1; i < opacitymap.Points.Length; i++)
            {
                int nextControlPoint = (int)(width * opacitymap.Points[i]);
                float nextValue = new FloatPrimitive(opacitymap.Values[i]).Value;
                for (int p = prevControlPoint; p < nextControlPoint; p++)
                {
                    float lerpValue = Mathf.Lerp(prevValue, nextValue, ((float)(p - prevControlPoint) / (nextControlPoint - prevControlPoint)));
                    pixelColors[p] = new Color(lerpValue, lerpValue, lerpValue);
                }
                prevControlPoint = nextControlPoint;
                prevValue = new FloatPrimitive(opacitymap.Values[i]).Value;
            }
            // Fill remaining pixels with black
            for (int p = prevControlPoint; p < width; p++)
            {
                pixelColors[p] = (p == prevControlPoint) ? new Color(prevValue, prevValue, prevValue) : Color.black;
            }

            // Repeat in each row for the rest of the texture
            for (int i = 1; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    pixelColors[i * width + j] = pixelColors[(i - 1) * width + j];
                }
            }

            // Apply pixels to shader
            result.SetPixels(pixelColors);
            result.Apply(false);

            return result;
        }
    }
}
