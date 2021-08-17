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
	}

	[ABRPlateType("Volumes")]
	public class SimpleVolumeDataImpression : DataImpression, IDataImpression
	{
		[ABRInput("Key Data", "Key Data", UpdateLevel.Data)]
		public VolumeKeyData keyData;

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
					colorVariableMax = 0
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
					colorVariableMax = 0
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

				// Calculate texture size
				int size = (int)Mathf.Pow(sourceVoxelCount, 1.0f / 3.0f);

				// Setup the new texture
				renderInfo.voxelTex = new Texture3D(size, size, size, TextureFormat.RGBAFloat, false);  // Could maybe support mip maps in future?
				renderInfo.voxelTex.wrapMode = TextureWrapMode.Clamp;

				// Set the pixels of the new texture
				for (int z = 0; z < size; z++)
				{
					int zOffsetMinusOne = z == 0 ? 0 : (z - 1) * size * size;
					int zOffset = z * size * size;
					int zOffsetPlusOne = z == size - 1 ? (size - 1) * size * size : (z + 1) * size * size;
					for (int y = 0; y < size; y++)
					{
						int yOffsetMinusOne = y == 0 ? 0 : (y - 1) * size;
						int yOffset = y * size;
						int yOffsetPlusOne = y == size - 1 ? (size - 1) * size : (y + 1) * size;
						for (int x = 0; x < size; x++)
						{
							int xMinusOne = x == 0 ? 0 : x - 1;
							int xPlusOne = x == size - 1 ? size - 1 : x + 1;
							// Compute partials in x, y and z
							float gradX = (renderInfo.scalars[xPlusOne + yOffset + zOffset][0] - renderInfo.scalars[xMinusOne + yOffset + zOffset][0]) / 2.0f;
							float gradY = (renderInfo.scalars[x + yOffsetPlusOne + zOffset][0] - renderInfo.scalars[x + yOffsetMinusOne + zOffset][0]) / 2.0f;
							float gradZ = (renderInfo.scalars[x + yOffset + zOffsetPlusOne][0] - renderInfo.scalars[x + yOffset + zOffsetMinusOne][0]) / 2.0f;
							// Compute scalar data value
							float d = renderInfo.scalars[x + yOffset + zOffset][0];
							// Store gradient in color rgb and data in alpha
							renderInfo.voxelTex.SetPixel(x, y, z, new Color(gradX, gradY, gradZ, d));
						}
					}
				}
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
				// Create a 2 x 2 x 2 cube mesh
				Vector3[] verts = {
					new Vector3(-1, -1, -1),
					new Vector3(1, -1, -1),
					new Vector3(1, 1, -1),
					new Vector3(-1, 1, -1),
					new Vector3(-1, 1, 1),
					new Vector3(1, 1, 1),
					new Vector3(1, -1, 1),
					new Vector3(-1, -1, 1)
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
