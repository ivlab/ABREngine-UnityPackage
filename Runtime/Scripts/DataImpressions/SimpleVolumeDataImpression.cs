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
    }

    [ABRPlateType("Volumes")]
    public class SimpleVolumeDataImpression : DataImpression, IDataImpression
    {
        [ABRInput("Key Data", "Key Data", UpdateLevel.Data)]
        public PointKeyData keyData;


        [ABRInput("Color Variable", "Color", UpdateLevel.Data)]
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


        protected override string MaterialName { get; } = "ABR_Volume";
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
                    Color[] pixels = new Color[dimensions.x * dimensions.y * dimensions.z];
                    for (int z = 0; z < dimensions.z; z++)
                    {
                        int zMinusOneOffset = z == 0 ? 0 : (z - 1) * dimensions.y * dimensions.x;
                        int zOffset = z * dimensions.y * dimensions.x;
                        int zPlusOneOffset = z == dimensions.z - 1 ? (dimensions.z - 1) * dimensions.y * dimensions.x : (z + 1) * dimensions.y * dimensions.x;
                        int zOffsetFlipped = (dimensions.z - z - 1) * dimensions.y * dimensions.x;
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
                                // (flip z to account for RH -> LH coordinate system conversion)
                                pixels[xOffset + yOffset + zOffsetFlipped] = new Color(xPartial, yPartial, -zPartial, d);
                            }
                        }
                    }
                    renderInfo.voxelTex.SetPixels(pixels);

                    // Apply changes to the 3D texture
                    renderInfo.voxelTex.Apply();
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
                Debug.LogWarningFormat("Could not find layer {0} for SimpleVolumeDataImpression", LayerName);
            }
            currentGameObject.name = this + " volume";

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
                meshRenderer.material = ImpressionMaterial;
                meshRenderer.GetPropertyBlock(MatPropBlock);
                MatPropBlock.SetTexture("_VolumeTexture", volumeRenderData.voxelTex);
                MatPropBlock.SetVector("_Center", volumeRenderData.bounds.center);
                MatPropBlock.SetVector("_Extents", volumeRenderData.bounds.extents);
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
            }
            else
            {
                MatPropBlock.SetInt("_UseColorMap", 0);

            }
            if (opacitymap != null)
            {
                MatPropBlock.SetInt("_UseOpacityMap", 1);
                MatPropBlock.SetTexture("_OpacityMap", GenerateOpacityMap());
            }
            else
            {
                MatPropBlock.SetInt("_UseOpacityMap", 0);

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
            float prevValue = new PercentPrimitive(opacitymap.Values[0]).Value;
            for (int i = 1; i < opacitymap.Points.Length; i++)
            {
                int nextControlPoint = (int)(width * opacitymap.Points[i]);
                float nextValue = new PercentPrimitive(opacitymap.Values[i]).Value;
                for (int p = prevControlPoint; p < nextControlPoint; p++)
                {
                    float lerpValue = Mathf.Lerp(prevValue, nextValue, ((float)(p - prevControlPoint) / (nextControlPoint - prevControlPoint)));
                    pixelColors[p] = new Color(lerpValue, lerpValue, lerpValue);
                }
                prevControlPoint = nextControlPoint;
                prevValue = new PercentPrimitive(opacitymap.Values[i]).Value;
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
