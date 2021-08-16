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

using System.Reflection;
using UnityEngine;

using IVLab.Utilities;

namespace IVLab.ABREngine
{
    class SimpleGlyphRenderInfo : IDataImpressionRenderInfo
    {
        public Matrix4x4[] transforms;
        public Vector4[] scalars;
        public float colorVariableMin;
        public float colorVariableMax;
        public Bounds bounds;
    }

    public class PointRenderInfo : IKeyDataRenderInfo
    {
        public Vector3[] positions;
        public Quaternion[] orientations;
        public Vector4[] scalars;
        public float colorVariableMin;
        public float colorVariableMax;
        public Bounds bounds;
    }


    [ABRPlateType("Glyphs")]
    public class SimpleGlyphDataImpression : DataImpression, IDataImpression
    {
        [ABRInput("Key Data", "Key Data", UpdateLevel.Data)]
        public PointKeyData keyData;

        [ABRInput("Color Variable", "Color", UpdateLevel.Style)]
        public ScalarDataVariable colorVariable;

        [ABRInput("Colormap", "Color", UpdateLevel.Style)]
        public ColormapVisAsset colormap;

        [ABRInput("Glyph Variable", "Glyph", UpdateLevel.Style)]
        public ScalarDataVariable glyphVariable;

        [ABRInput("Glyph", "Glyph", UpdateLevel.Style)]
        public GlyphVisAsset glyph;

        [ABRInput("Glyph Size", "Glyph", UpdateLevel.Style)]
        public LengthPrimitive glyphSize;

        [ABRInput("Glyph Density", "Glyph", UpdateLevel.Style)]
        public PercentPrimitive glyphDensity;

        [ABRInput("Forward Variable", "Direction", UpdateLevel.Style)]
        public VectorDataVariable forwardVariable;

        [ABRInput("Up Variable", "Direction", UpdateLevel.Style)]
        public VectorDataVariable upVariable;

        public int glyphLod = 1;

        protected override string MaterialName { get; } = "ABR_DataGlyphs";
        protected override string LayerName { get; } = "ABR_Glyph";

        /// <summary>
        ///     Construct a data impession with a given UUID. Note that this
        ///     will be called from ABRState and must assume that there's a
        ///     single string argument with UUID.
        /// </summary>
        public SimpleGlyphDataImpression(string uuid) : base(uuid) { }
        public SimpleGlyphDataImpression() : base() { }

        public override Dataset GetDataset()
        {
            return keyData?.GetDataset();
        }

        public override void ComputeKeyDataRenderInfo()
        {
            if (keyData?.Path == null)
            {
                return;
            }

            PointRenderInfo renderInfo;

            if (keyData == null)
            {
                renderInfo = new PointRenderInfo
                {
                    positions = new Vector3[0],
                    orientations = new Quaternion[0],
                    scalars = new Vector4[0],
                    colorVariableMin = 0,
                    colorVariableMax = 0,
                    bounds = new Bounds()
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

                float colorMin, colorMax;
                colorMin = colorVariable?.MinValue ?? 0.0f;
                colorMax = colorVariable?.MaxValue ?? 0.0f;
                int numPoints = dataset.vertexArray.Length;
                renderInfo = new PointRenderInfo
                {
                    positions = new Vector3[numPoints],
                    orientations = new Quaternion[numPoints],
                    scalars = new Vector4[numPoints],
                    colorVariableMin = colorMin,
                    colorVariableMax = colorMax,
                };
                for (int i = 0; i < numPoints; i++)
                {
                    renderInfo.positions[i] = group.GroupToDataMatrix * dataset.vertexArray[i].ToHomogeneous();
                }

                if (colorVariable != null && colorVariable.IsPartOf(keyData))
                {
                    var colorScalars = colorVariable.GetArray(keyData);
                    for (int i = 0; i < numPoints; i++)
                        renderInfo.scalars[i][0] = colorScalars[i];

                }
                else { } // Leave the scalars as 0

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
                        dataForwards[i] = new Vector3(
                            (float)rand.NextDouble() * 2 - 1,
                            (float)rand.NextDouble() * 2 - 1,
                            (float)rand.NextDouble() * 2 - 1);
                        // dataForwards[i] = new Vector3(0, 0, 1);
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
                        dataUp[i] = new Vector3(
                            (float)rand.NextDouble() * 2 - 1,
                            (float)rand.NextDouble() * 2 - 1,
                            (float)rand.NextDouble() * 2 - 1);
                        // dataUp[i] = new Vector3(0, 1, 0);
                    }
                }

                if (upVariable != null && forwardVariable != null)
                { // Treat up as the more rigid constraint
                    for (int i = 0; i < numPoints; i++)
                    {
                        Vector3 rightAngleForward = Vector3.Cross(
                        Vector3.Cross(dataUp[i], dataForwards[i]).normalized,
                        dataUp[i]).normalized;

                        Quaternion orientation = Quaternion.LookRotation(rightAngleForward, dataUp[i]) * Quaternion.Euler(0, 180, 0);
                        renderInfo.orientations[i] = orientation;
                    }
                }
                else // Treat forward as the more rigid constraint
                {
                    for (int i = 0; i < numPoints; i++)
                    {

                        Vector3 rightAngleUp = Vector3.Cross(
                            Vector3.Cross(dataForwards[i], dataUp[i]).normalized,
                            dataForwards[i]).normalized;

                        Quaternion orientation = Quaternion.LookRotation(dataForwards[i], rightAngleUp) * Quaternion.Euler(0, 180, 0);
                        renderInfo.orientations[i] = orientation;
                    }
                }

                renderInfo.bounds = dataset?.bounds ?? new Bounds();
            }

            KeyDataRenderInfo = renderInfo;
        }

        public override void ComputeRenderInfo()
        {
            var dataRenderInfo = KeyDataRenderInfo as PointRenderInfo;
            if (dataRenderInfo == null) {
                return;
            }

            int numPoints = dataRenderInfo.scalars.Length;

            var encodingRenderInfo = new SimpleGlyphRenderInfo()
            {
                transforms = new Matrix4x4[numPoints],
                scalars = dataRenderInfo.scalars,
                colorVariableMin = dataRenderInfo.colorVariableMin,
                colorVariableMax = dataRenderInfo.colorVariableMax
            };

            // Load defaults from configuration / schema
            ABRConfig config = ABREngine.Instance.Config;

            // Width appears double what it should be, so decrease to
            // maintain the actual real world distance
            string plateType = this.GetType().GetCustomAttribute<ABRPlateType>().plateType;

            float glyphScale = glyphSize?.Value ??
                config.GetInputValueDefault<LengthPrimitive>(plateType, "Glyph Size").Value;


            for (int i = 0; i < numPoints; i++)
            {

                encodingRenderInfo.transforms[i] = Matrix4x4.TRS(dataRenderInfo.positions[i], dataRenderInfo.orientations[i], Vector3.one * glyphScale);

            }
            encodingRenderInfo.bounds = dataRenderInfo.bounds;
            RenderInfo = encodingRenderInfo;
        }

        public override void ApplyToGameObject(EncodedGameObject currentGameObject)
        {
            var SSrenderData = RenderInfo as SimpleGlyphRenderInfo;
            if (currentGameObject == null)
            {
                return;
            }

            int layerID = LayerMask.NameToLayer(LayerName);
            if (layerID >= 0)
            {
                currentGameObject.gameObject.layer = layerID;
            }
            else
            {
                Debug.LogWarningFormat("Could not find layer {0} for SimpleGlyphDataImpression", LayerName);
            }

            MeshRenderer mr = currentGameObject.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                mr = currentGameObject.gameObject.AddComponent<MeshRenderer>();
            }


            if (colormap != null)
            {
                MatPropBlock.SetInt("_UseColorMap", 1);
                MatPropBlock.SetTexture("_ColorMap", colormap.GetColorGradient());
            }
            else
            {
                MatPropBlock.SetInt("_UseColorMap", 0);

            }



            InstancedMeshRenderer imr = currentGameObject.GetComponent<InstancedMeshRenderer>();
            if (imr == null)
            {
                imr = currentGameObject.gameObject.AddComponent<InstancedMeshRenderer>();
            }
            imr.enabled = RenderHints.Visible;
            imr.bounds = SSrenderData?.bounds ?? new Bounds();

            int lod = glyphLod;
            // if (ABRManager.IsValidNode(glyphLod))
            // {
            //     lod = (int)glyphLod.floatVal;
            // }
            if (glyph != null)
            {

                imr.instanceMesh = glyph.GetMesh(lod);
                MatPropBlock.SetTexture("_Normal", glyph.GetNormalMap(lod));
            }
            else
            {
                Mesh mesh = ABREngine.Instance.Config.Defaults.defaultPrefab.GetComponent<MeshFilter>().mesh;
                imr.instanceMesh = mesh;
            }



            if (SSrenderData != null)
            {
                MatPropBlock.SetFloat("_ColorDataMin", SSrenderData.colorVariableMin);
                MatPropBlock.SetFloat("_ColorDataMax", SSrenderData.colorVariableMax);
                MatPropBlock.SetColor("_Color", Color.white);

                imr.instanceLocalTransforms = SSrenderData.transforms;

                // Initialize "render info" -- stores scalar values and info on whether
                // or not glyphs should be rendered
                Vector4[] glyphRenderInfo = SSrenderData.scalars;
                // Get the glyph density
                ABRConfig config = ABREngine.Instance.Config;
                string plateType = this.GetType().GetCustomAttribute<ABRPlateType>().plateType;
                float glyphDensityOut = glyphDensity?.Value ??
                    config.GetInputValueDefault<PercentPrimitive>(plateType, "Glyph Density").Value;
                glyphDensityOut = Mathf.Clamp01(glyphDensityOut);
                // Sample based on density
                SampleGlyphs(glyphRenderInfo, (int)(glyphRenderInfo.Length * glyphDensityOut));
                // Apply scalar/density changes to the instanced mesh renderer
                imr.instanceDensity = glyphDensityOut;
                imr.renderInfo = glyphRenderInfo;

                if (colormap?.GetColorGradient() != null)
                {
                    MatPropBlock.SetInt("_UseColorMap", 1);
                    MatPropBlock.SetTexture("_ColorMap", colormap?.GetColorGradient());
                }
                else
                {
                    MatPropBlock.SetInt("_UseColorMap", 0);
                }
            }
            else
            {
                imr.instanceLocalTransforms = new Matrix4x4[0];
            }

            imr.block = MatPropBlock;

            imr.instanceMaterial = ImpressionMaterial;

            imr.cachedInstanceCount = -1;
        }

        public override void UpdateStyling(EncodedGameObject currentGameObject)
        {
            // Exit immediately if the game object or instanced mesh renderer relevant to this
            // impression do not yet exist
            InstancedMeshRenderer imr = currentGameObject?.GetComponent<InstancedMeshRenderer>();
            if (imr == null)
            {
                return;
            }

            // Determine the number of points / glyphs via the number of transforms the
            // instanced mesh renderer is currently tracking
            int numPoints = imr.instanceLocalTransforms.Length;

            // We might as well exit if there are no glyphs to update
            if (numPoints <= 0)
            {
                return;
            }

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
            if (glyph != null)
            {
                imr.instanceMesh = glyph.GetMesh(glyphLod);
                MatPropBlock.SetTexture("_Normal", glyph.GetNormalMap(glyphLod));
            }
            else
            {
                Mesh mesh = ABREngine.Instance.Config.Defaults.defaultPrefab.GetComponent<MeshFilter>().mesh;
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

            // Initialize variables to track scalar "styling" changes
            float colorVariableMin = colorVariable?.MinValue ?? 0.0f;
            float colorVariableMax = colorVariable?.MaxValue ?? 0.0f;
            if (colorVariable != null && colorVariable.IsPartOf(keyData))
            {
                var colorScalars = colorVariable.GetArray(keyData);
                for (int i = 0; i < numPoints; i++)
                {
                    // Set the scalar value of the glyph used to apply colormap in shader
                    glyphRenderInfo[i][0] = colorScalars[i];
                }
            }

            // Apply scalar/density changes to the instanced mesh renderer
            imr.instanceDensity = glyphDensityOut;
            imr.renderInfo = glyphRenderInfo;

            // Apply changes to the mesh's shader / material
            MatPropBlock.SetFloat("_ColorDataMin", colorVariableMin);
            MatPropBlock.SetFloat("_ColorDataMax", colorVariableMax);
            MatPropBlock.SetColor("_Color", Color.white);

            if (colormap?.GetColorGradient() != null)
            {
                MatPropBlock.SetInt("_UseColorMap", 1);
                MatPropBlock.SetTexture("_ColorMap", colormap?.GetColorGradient());
            }
            else
            {
                MatPropBlock.SetInt("_UseColorMap", 0);
            }

            imr.block = MatPropBlock;

            imr.cachedInstanceCount = -1;
        }

        public override void UpdateVisibility(EncodedGameObject currentGameObject)
        {
            InstancedMeshRenderer imr = currentGameObject?.GetComponent<InstancedMeshRenderer>();
            if (imr != null)
            {
                imr.enabled = RenderHints.Visible;
            }
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
                int j = Random.Range(0, i + 1);
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
    }
}