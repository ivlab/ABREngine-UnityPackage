using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IVLab.ABREngine.Examples
{
    /// <summary>
    /// This sample contains examples of using more advanced parts of ABR, including: 
    /// 1. Using the <see cref="RawDatasetAdapter"/> to create ABR-compatible data from C# data structures
    /// 2. Using <see cref="DataImpressionGroup"/>s to control individual positioning of data in the scene
    /// 3. Use of the <see cref="RenderHints.PerIndexVisibility"/> bit array
    /// 4. Using <see cref="VisAssetGradient"/>s in multivariate visualizations
    ///
    /// Data are sourced from the USGS.
    /// </summary>
    public class AdvancedUsage : MonoBehaviour
    {
        [SerializeField, Tooltip("Data container for Mount St. Helens Data. Must be assigned to start.")]
        private MtStHelensData data;

        private Guid surfaceColormap = new Guid("30de3664-c36b-475e-a12d-3a3739ceb876"); // in this example
        private Guid tex1Uuid = new Guid("35a12d70-72cd-11ea-8d65-005056bae6d8"); // in this example
        private Guid tex2Uuid = new Guid("3bbc2064-6a0a-11ea-9014-005056bae6d8"); // already in ABREngine Resources

        // Important data impressions to save for later
        private SimpleGlyphDataImpression beforePoints = null;
        private SimpleGlyphDataImpression beforePointsHighlighted = null;
        private SimpleSurfaceDataImpression afterSurface = null;
        private SimpleSurfaceDataImpression beforeSurface = null;

        // Definitions for data paths
        private const string Dataset = "USGS/MtStHelens";
        private const string AfterSurfacePath = Dataset + "/KeyData/AfterSurface";
        private const string BeforeSurfacePath = Dataset + "/KeyData/BeforeSurface";
        private const string PointsPath = Dataset + "/KeyData/BeforePoints";
        private const string ElevationChangeName = "ElevationChange";

        // Time counter for animated highlighting
        private float highlightTimeout = 0.0f;
        private int maxIndexHighlight = 0;

        // Start is called before the first frame update
        void Start()
        {
            CreateDataImpressions();

            ABREngine.Instance.Render();
        }

        // Update is called once per frame
        void Update()
        {
            // ********************************************************************************
            // Point 3: Using Per Index Visibility (this "animates" per-index
            // visibility) once per second
            // ********************************************************************************

            // Change highlight 1x / second
            if (highlightTimeout > 1.0f)
            {
                const int NumPointsToHighlight = 500;
                for (int i = maxIndexHighlight; i < maxIndexHighlight + NumPointsToHighlight && i < beforePoints.RenderHints.PerIndexVisibility.Length; i++)
                {
                    beforePoints.RenderHints.PerIndexVisibility[i] = false;
                    beforePointsHighlighted.RenderHints.PerIndexVisibility[i] = true;
                }
                maxIndexHighlight = (maxIndexHighlight + NumPointsToHighlight) % beforePoints.RenderHints.PerIndexVisibility.Length;

                beforePoints.RenderHints.StyleChanged = true;
                beforePointsHighlighted.RenderHints.StyleChanged = true;

                ABREngine.Instance.Render();
                highlightTimeout = 0.0f;
            }

            highlightTimeout += Time.deltaTime;
        }

        // The general process for creating a data impression w/some data is:
        //  1. Obtain a RawDataset by using the RawDatasetAdapter
        //  2. Import the raw dataset into ABR
        //  3. Create your data impression(s) and style them with size, color, texture, etc.
        //  4. Register the data impressions with the ABREngine
        void CreateDataImpressions()
        {
            // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            // Create glyph data impressions
            // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

            // ********************************************************************************
            // Point 1: using the RawDatasetAdapter
            // ********************************************************************************

            // Convert the point lists into ABR format
            RawDataset beforePointsRaw = RawDatasetAdapter.PointsToPoints(data.beforePointList, data.pointsBounds, null, null);

            // Import the point data into ABR (NEW: define the data path)
            KeyData beforeInfo = ABREngine.Instance.Data.ImportRawDataset(PointsPath, beforePointsRaw);

            // Create a layer for non-highlighted points
            beforePoints = new SimpleGlyphDataImpression();
            beforePoints.useRandomOrientation = false;
            beforePoints.keyData = beforeInfo;
            beforePoints.glyph = ABREngine.Instance.VisAssets.GetDefault<GlyphVisAsset>() as GlyphVisAsset;
            beforePoints.glyphSize = 0.002f;
            beforePoints.colormap = ColormapVisAsset.SolidColor(Color.black);
            beforePoints.RenderHints.PerIndexVisibility = new BitArray(data.beforePointList.Count, true);

            // Create a layer for highlighted points
            beforePointsHighlighted = ABREngine.Instance.DuplicateDataImpression(beforePoints) as SimpleGlyphDataImpression;
            beforePointsHighlighted.colormap = ColormapVisAsset.SolidColor(new Color(0.22f, 0.74f, 1.0f));
            beforePointsHighlighted.glyphSize = 0.005f;
            beforePointsHighlighted.RenderHints.PerIndexVisibility = new BitArray(data.beforePointList.Count, false);

            // Register impression with the engine
            ABREngine.Instance.RegisterDataImpression(beforePoints);
            ABREngine.Instance.RegisterDataImpression(beforePointsHighlighted);


            // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            // Create surface data impressions
            // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

            // Load a VisAsset from the project's Resources folder
            ColormapVisAsset cmap = ABREngine.Instance.VisAssets.GetVisAsset<ColormapVisAsset>(surfaceColormap);

            // ********************************************************************************
            // Point 4: Using vis asset gradients
            // ********************************************************************************
            // Load a couple textures from the Resources folder and create a
            // SurfaceTextureGradient. Later, figure out where "zero" is in the data
            // and map it so there's one texture above zero and one texture
            // below zero.
            SurfaceTextureVisAsset tex1 = ABREngine.Instance.VisAssets.GetVisAsset<SurfaceTextureVisAsset>(tex1Uuid);
            SurfaceTextureVisAsset tex2 = ABREngine.Instance.VisAssets.GetVisAsset<SurfaceTextureVisAsset>(tex2Uuid);
            SurfaceTextureGradient grad = new SurfaceTextureGradient();
            grad.Initialize(
                Guid.NewGuid(),
                new List<SurfaceTextureVisAsset>() { tex1, tex2 },
                new List<float>() { 0.5f } // default to the middle, figure out where "zero" is later
            );

            // ********************************************************************************
            // Point 1: using the RawDatasetAdapter
            // ********************************************************************************
            // Add surface for before/after eruption (including the ElevationChange scalar data variable)
            RawDataset beforeSurfaceData = RawDatasetAdapter.GridPointsToSurface(
                data.beforePointList,
                new Vector2Int(MtStHelensData.gridX, MtStHelensData.gridY),
                data.pointsBounds,
                new Dictionary<string, List<float>> {{ ElevationChangeName, data.differences }}
            );
            RawDataset afterSurfaceData = RawDatasetAdapter.GridPointsToSurface(
                data.afterPointList,
                new Vector2Int(MtStHelensData.gridX, MtStHelensData.gridY),
                data.pointsBounds,
                new Dictionary<string, List<float>> {{ ElevationChangeName, data.differences }}
            );
            
            // Define the data path when importing
            KeyData beforeSurfaceInfo = ABREngine.Instance.Data.ImportRawDataset(BeforeSurfacePath, beforeSurfaceData);
            KeyData afterSurfaceInfo = ABREngine.Instance.Data.ImportRawDataset(AfterSurfacePath, afterSurfaceData);

            // Create surface data impressions
            afterSurface = new SimpleSurfaceDataImpression();
            afterSurface.keyData = afterSurfaceInfo;
            beforeSurface = new SimpleSurfaceDataImpression();
            beforeSurface.keyData = beforeSurfaceInfo;

            // Get scalar variable by name
            afterSurface.colorVariable = afterSurfaceInfo.GetScalarVariable(ElevationChangeName);
            afterSurface.colormap = cmap;

            ScalarDataVariable beforeElevation = beforeSurfaceInfo.GetScalarVariable(ElevationChangeName);
            // beforeSurface.colorVariable = beforeElevation;
            // beforeSurface.colormap = ABREngine.Instance.VisAssets.GetDefault<ColormapVisAsset>() as ColormapVisAsset;

            // Find the "zero" point in real data, and normalize it
            var range = beforeElevation.Range;
            float normalizedZero = (-range.min) / (range.max - range.min);
            grad.Stops[0] = normalizedZero;
            beforeSurface.patternVariable = beforeElevation;
            beforeSurface.pattern = grad;

            // ********************************************************************************
            // Point 2: Using DataImpressionGroups to move data impressions around
            // ********************************************************************************

            // Create a data impression group for the "before" surface so we can move it off to the side
            DataImpressionGroup beforeGroup = ABREngine.Instance.CreateDataImpressionGroup("Side by side vis", new Vector3(5.0f, 0.0f, 0.0f));

            // Register impressions with the engine
            ABREngine.Instance.RegisterDataImpression(afterSurface);

            // Register "before" group with new data impression
            ABREngine.Instance.RegisterDataImpression(beforeSurface, beforeGroup);
        }
    }
}