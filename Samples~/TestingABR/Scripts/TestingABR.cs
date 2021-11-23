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
using System.Threading.Tasks;

namespace IVLab.ABREngine.Examples
{
    /// <summary>
    /// This sample is provided as a small framework to test features that you
    /// build in the ABREngine.
    /// </summary>
    public class TestingABR : MonoBehaviour
    {
        void Start()
        {
            // Anything that depends on loading VisAssets or Data should be
            // placed inside an `async` block.
            Task.Run(async () =>
            {
                // First, wait for the ABREngine to be initialized. If you've
                // placed the ABREngine prefab in your scene, this should already
                // be taken care of, but in case you didn't:
                await ABREngine.GetInstance().WaitUntilInitialized();

                // Everything about the ABREngine is accessed through the singleton `ABREngine`.
                // For example, we can load some data that are in Runtime/Resources/media/datasets...
                RawDataset dataset = await ABREngine.Instance.Data.LoadRawDataset<ResourcesDataLoader>("Demo/Wavelet/KeyData/Contour");

                // Or, work with a VisAsset (this one exists in ABREngine Runtime/Resources/media/visassets):
                IVisAsset visAsset = await ABREngine.Instance.VisAssets.LoadVisAsset(new Guid("1af025aa-f1ed-11e9-a623-8c85900fd4af"));

                // You may also get any information from the ABR configuration,
                // such as the default room-scale data bounds or the schema
                // version:
                Bounds defaultBounds = ABREngine.Instance.Config.Info.defaultBounds.Value;
                string schema = ABREngine.Instance.Config.Info.schemaName;

                // If you want to dive into the ABR schema to get specific
                // default values, you may do so through the schema:
                float lineWidthDefault = ABREngine.Instance.Config.GetInputValueDefault<LengthPrimitive>("Ribbons", "Ribbon Width").Value;

                // You may get the media path where the ABREngine will by
                // default look for datasets and visassets:
                string mediaPath = ABREngine.Instance.MediaPath;

                // And, you may assign a delegate to listen for when the ABR
                // state has been changed by someone else - this gives the new,
                // raw state.
                ABREngine.Instance.OnStateChanged += CustomStateChangeHandler;
            });
        }

        void CustomStateChangeHandler(Newtonsoft.Json.Linq.JObject rawState)
        {
            Debug.Log("ABR State has been changed!");
        }
    }
}
