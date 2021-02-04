/* SimpleGlyphDataImpression.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

using IVLab.Utilities.GenericObjectPool;

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
        public Guid Uuid { get; }

        [ABRInput("Key Data", "Key Data")]
        public PointKeyData keyData;

        [ABRInput("Color Variable", "Color")]
        public ScalarDataVariable colorVariable;

        [ABRInput("Colormap", "Color")]
        public ColormapVisAsset colormap;

        [ABRInput("Glyph Variable", "Glyph")]
        public ScalarDataVariable glyphVariable;

        [ABRInput("Glyph", "Glyph")]
        public GlyphVisAsset glyph;

        protected override string MaterialName { get; } = "ABR_DataGlyphs";
        protected override string LayerName { get; } = "ABR_Glyph";

        // TODO add the primitive inputs
        // TODO load defaults from schema

        public SimpleGlyphDataImpression() : base()
        {
            Uuid = Guid.NewGuid();
        }

        public void ComputeKeyDataRenderInfo()
        {
            if (keyData?.Path == null)
            {
                return;
            }

            PointRenderInfo renderInfo;

            RawDataset dataset;
            DataManager.Instance.TryGetDataset(keyData.Path, out dataset);

            if (dataset == null)
            {
                renderInfo = new PointRenderInfo
                {
                    positions = new Vector3[0],
                    orientations = new Quaternion[0],
                    scalars = new Vector4[0],
                    colorVariableMin = 0,
                    colorVariableMax = 0
                };
            }
            else
            {
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
                    colorVariableMax = colorMax
                };
                for (int i = 0; i < numPoints; i++)
                {
                    renderInfo.positions[i] = keyData.DataTransform * dataset.vertexArray[i];
                }

                if (colorVariable != null)
                {
                    var colorScalars = colorVariable.GetArray(keyData);
                    for (int i = 0; i < numPoints; i++)
                        renderInfo.scalars[i][0] = colorScalars[i];

                }
                else { } // Leave the scalars as 0

                Vector3[] dataForwards = null;
                Vector3[] dataUp = null;

                // if (ABRManager.IsValidNode(forwardVariable))
                // {
                //     dataForwards = forwardVariable.GetVectorArray(dataset);
                // }
                // else
                {
                    var rand = new System.Random(0);
                    dataForwards = new Vector3[numPoints];
                    for (int i = 0; i < numPoints; i++)
                    {
                        dataForwards[i] = new Vector3(
                            (float)rand.NextDouble() * 2 - 1,
                            (float)rand.NextDouble() * 2 - 1,
                            (float)rand.NextDouble() * 2 - 1);
                    }
                }

                // if (ABRManager.IsValidNode(upVariable))
                // {
                //     dataUp = upVariable.GetVectorArray(dataset);
                // }
                // else
                {
                    var rand = new System.Random(1);
                    dataUp = new Vector3[numPoints];
                    for (int i = 0; i < numPoints; i++)
                    {
                        dataUp[i] = new Vector3(
                            (float)rand.NextDouble() * 2 - 1,
                            (float)rand.NextDouble() * 2 - 1,
                            (float)rand.NextDouble() * 2 - 1);
                    }
                }

                // if (ABRManager.IsValidNode(upVariable) && !ABRManager.IsValidNode(forwardVariable))
                // { // Treat up as the more rigid constraint
                //     for (int i = 0; i < numPoints; i++)
                //     {
                //         Vector3 rightAngleForward = Vector3.Cross(
                //         Vector3.Cross(dataUp[i], dataForwards[i]).normalized,
                //         dataUp[i]).normalized;

                //         Quaternion orientation = Quaternion.LookRotation(rightAngleForward, dataUp[i]) * Quaternion.Euler(0, 180, 0);
                //         renderInfo.orientations[i] = orientation;
                //     }
                // }
                // else // Treat forward as the more rigid constraint
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


            }
            renderInfo.bounds = dataset?.bounds ?? new Bounds();

            KeyDataRenderInfo = renderInfo;
        }

        public void ComputeRenderInfo()
        {
            var dataRenderInfo = KeyDataRenderInfo as PointRenderInfo;

            int numPoints = dataRenderInfo.scalars.Length;

            var encodingRenderInfo = new SimpleGlyphRenderInfo()
            {
                transforms = new Matrix4x4[numPoints],
                scalars = dataRenderInfo.scalars,
                colorVariableMin = dataRenderInfo.colorVariableMin,
                colorVariableMax = dataRenderInfo.colorVariableMax
            };



            float glyphScaleFraction = 0.05f;
            // if (ABRManager.IsValidNode(glyphSize))
            // {
            //     glyphScaleFraction = glyphSize.floatVal;
            // }

            // float sceneScale = encodedObject.dataScene.GetDataBounds().size.magnitude;


            // float glyphScale = sceneScale * glyphScaleFraction;
            float glyphScale = glyphScaleFraction;


            for (int i = 0; i < numPoints; i++)
            {

                encodingRenderInfo.transforms[i] = Matrix4x4.TRS(dataRenderInfo.positions[i], dataRenderInfo.orientations[i], Vector3.one * glyphScale);

            }
            encodingRenderInfo.bounds = dataRenderInfo.bounds;
            RenderInfo = encodingRenderInfo;
        }

        public void ApplyToGameObject(EncodedGameObject currentGameObject)
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
            imr.bounds = SSrenderData?.bounds ?? new Bounds();

            int lod = 1;
            // if (ABRManager.IsValidNode(glyphLod))
            // {
            //     lod = (int)glyphLod.floatVal;
            // }
            if (glyph != null)
            {

                imr.instanceMesh = glyph.GetMesh(lod);
                MatPropBlock.SetTexture("_Normal", glyph.GetNormalMap(lod));
                //MatPropBlock.SetTexture("_NormalMap",glyph.GetNormalMap());
            }
            else
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Mesh mesh = sphere.GetComponent<MeshFilter>().mesh;
                imr.instanceMesh = mesh;
                // imr.instanceMesh = VisAssetManager.GetDefaultGlyphMesh();
                // MatPropBlock.SetTexture("_Normal", VisAssetManager.GetDefaultNormal());


            }



            if (SSrenderData != null)
            {
                MatPropBlock.SetFloat("_ColorDataMin", SSrenderData.colorVariableMin);
                MatPropBlock.SetFloat("_ColorDataMax", SSrenderData.colorVariableMax);
                MatPropBlock.SetColor("_Color", Color.white);

                imr.instanceLocalTransforms = SSrenderData.transforms;
                imr.colors = SSrenderData.scalars;
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
    }
}