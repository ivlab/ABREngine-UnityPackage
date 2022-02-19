/* LoadJsonState.cs
 *
 * Copyright (c) 2021 University of Minnesota
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
using IVLab.ABREngine;
using IVLab.Utilities;
using System.Threading.Tasks;

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

        private IKeyData contour;
        private IKeyData points;
        private ScalarDataVariable xAxis;
        private ScalarDataVariable yAxis;

        private IVisAsset cmap;
        private IVisAsset glyph;

        void Start()
        {
            // Kick off the async work
            Task.Run(async () =>
            {
                // Pre-load all the necessary data/visuals
                await LoadData();
                Debug.Log("Loaded Data");
                await LoadVisAssets();
                Debug.Log("Loaded VisAssets");

                // Hook up the data and visuals
                await CreateDataImpressions();
                Debug.Log("Created Data Impressions");

                // Lastly, render the visualization
                await UnityThreadScheduler.Instance.RunMainThreadWork(() =>
                {
                    ABREngine.Instance.Render();
                });
                Debug.Log("Finished rendering visualization");
            });
        }

        async Task LoadData()
        {
            // Load the example data from Resources
            await ABREngine.Instance.Data.LoadRawDataset<ResourcesDataLoader>(contourPath);
            await ABREngine.Instance.Data.LoadRawDataset<ResourcesDataLoader>(pointsPath);

            // Load the high-level dataset that both Contour and Points are contained within
            Dataset ds = null;
            if (!ABREngine.Instance.Data.TryGetDataset(datasetPath, out ds))
            {
                Debug.LogError("Unable to load dataset " + datasetPath);
                return;
            }

            // Populate the key data objects from dataset
            if (!ds.TryGetKeyData(contourPath, out contour))
            {
                Debug.LogError("Key data not found in dataset: " + contourPath);
                return;
            }
            if (!ds.TryGetKeyData(pointsPath, out points))
            {
                Debug.LogError("Key data not found in dataset: " + pointsPath);
                return;
            }

            // Populate the variables from dataset
            if (!ds.TryGetScalarVar(xAxisPath, out xAxis))
            {
                Debug.LogError("Dataset does not have variable " + xAxisPath);
                return;
            }
            if (!ds.TryGetScalarVar(yAxisPath, out yAxis))
            {
                Debug.LogError("Dataset does not have variable " + yAxisPath);
                return;
            }
        }

        async Task LoadVisAssets()
        {
            await UnityThreadScheduler.Instance.RunMainThreadWork(async () =>
            {
                await ABREngine.Instance.VisAssets.LoadVisAsset(cmapUuid);
                await ABREngine.Instance.VisAssets.LoadVisAsset(glyphUuid);

                if (!ABREngine.Instance.VisAssets.TryGetVisAsset(cmapUuid, out cmap))
                {
                    Debug.LogError("Colormap not loaded");
                }
                if (!ABREngine.Instance.VisAssets.TryGetVisAsset(glyphUuid, out glyph))
                {
                    Debug.LogError("Glyph not loaded");
                }
            });
        }

        async Task CreateDataImpressions()
        {
            // These need to be run in the Unity main thread because they
            // interact with Unity internals like Transforms
            await UnityThreadScheduler.Instance.RunMainThreadWork(() => {
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
            });
        }
    }
}
