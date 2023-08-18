/* CreateState.cs
 *
 * Copyright (c) 2022 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
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
using UnityEngine;

namespace IVLab.ABREngine.Examples
{
    /// <summary>
    /// In this example, we construct an ABR state from scratch using API calls
    /// in to the ABR Engine. This example assumes a blank ABR Configuration.
    ///
    /// This state assumes that the following VisAssets are available (which
    /// they should be, in Runtime/Resources/media/visassets):
    /// (colormap): 66b3cde4-034d-11eb-a7e6-005056bae6d8
    /// (glyph): 1af025aa-f1ed-11e9-a623-8c85900fd4af
    ///
    /// Additionally, this will load a sample dataset from the ABREngine Resources folder:
    /// Demo/Wavelet/KeyData/Contour
    /// Demo/Wavelet/KeyData/Points
    /// Demo/Wavelet/ScalarVar/XAxis
    /// Demo/Wavelet/ScalarVar/YAxis
    /// </summary>
    public class CreateState : MonoBehaviour
    {
        private const string datasetPath = "Demo/Wavelet";
        private const string contourPath = datasetPath + "/KeyData/Contour";
        private const string pointsPath = datasetPath + "/KeyData/Points";
        private const string xAxisPath = datasetPath + "/ScalarVar/XAxis";
        private const string yAxisPath = datasetPath + "/ScalarVar/YAxis";

        private static Guid cmapUuid = new Guid("66b3cde4-034d-11eb-a7e6-005056bae6d8");
        private static Guid glyphUuid = new Guid("1af025aa-f1ed-11e9-a623-8c85900fd4af");
        private static Guid[] visAssetsToLoad = new Guid[] { cmapUuid, glyphUuid };

        private KeyData contour;
        private KeyData points;
        private ScalarDataVariable xAxis;
        private ScalarDataVariable yAxis;

        private IVisAsset cmap;
        private IVisAsset glyph;

        void Start()
        {
            // Pre-load all the necessary data/visuals
            LoadData();
            Debug.Log("Loaded Data");
            LoadVisAssets();
            Debug.Log("Loaded VisAssets");

            // Hook up the data and visuals
            CreateDataImpressions();
            Debug.Log("Created Data Impressions");

            // Lastly, render the visualization
            ABREngine.Instance.Render();
            Debug.Log("Finished rendering visualization");
        }

        void LoadData()
        {
            // Load the example data from Resources
            contour = ABREngine.Instance.Data.LoadData(contourPath);
            points = ABREngine.Instance.Data.LoadData(pointsPath);

            // Populate the variables from dataset
            xAxis = contour.GetScalarVariable("XAxis");
            yAxis = contour.GetScalarVariable("YAxis");
        }

        void LoadVisAssets()
        {
            // Best practice for loading VisAssets is now to use `GetVisAsset`,
            // as this works for both VisAssets already loaded and ones that
            // haven't been loaded yet.
            cmap = ABREngine.Instance.VisAssets.GetVisAsset<ColormapVisAsset>(cmapUuid);
            glyph = ABREngine.Instance.VisAssets.GetVisAsset<GlyphVisAsset>(glyphUuid);
        }

        void CreateDataImpressions()
        {
            // Create a data impression for the Contour
            SimpleSurfaceDataImpression di = new SimpleSurfaceDataImpression();
            di.keyData = contour as SurfaceKeyData;
            di.colorVariable = xAxis;
            di.colormap = cmap as ColormapVisAsset;
            ABREngine.Instance.RegisterDataImpression(di);

            // And create a data impression for the Points
            SimpleGlyphDataImpression gi = new SimpleGlyphDataImpression();
            gi.keyData = points as PointKeyData;
            gi.colorVariable = yAxis;
            gi.colormap = ABREngine.Instance.VisAssets.GetDefault<ColormapVisAsset>() as ColormapVisAsset;
            gi.glyph = glyph as GlyphVisAsset;
            ABREngine.Instance.RegisterDataImpression(gi);
        }
    }
}
