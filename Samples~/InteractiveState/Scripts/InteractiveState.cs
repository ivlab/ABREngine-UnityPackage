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
    /// In this example, we construct an ABR state from an example JSON file.
    /// Then, we interactively make modifications to the state, such as
    /// switching between colormaps and shrinking/growing the glyph size. This
    /// example assumes a blank ABR Configuration.
    ///
    /// This state assumes that the following VisAssets are available (which
    /// they should be, in Runtime/Resources/media/visassets):
    /// (colormap): 66b3cde4-034d-11eb-a7e6-005056bae6d8
    /// (colormap): 5a761a72-8bcb-11ea-9265-005056bae6d8
    /// (glyph): 1af025aa-f1ed-11e9-a623-8c85900fd4af
    ///
    /// Additionally, this will load a sample dataset from the ABREngine Resources folder:
    /// Demo/Wavelet/KeyData/Contour
    /// Demo/Wavelet/KeyData/Points
    /// Demo/Wavelet/ScalarVar/XAxis
    /// Demo/Wavelet/ScalarVar/YAxis
    /// </summary>
    public class InteractiveState : MonoBehaviour
    {
        // Look in this JSON file for more info on what's being loaded, and
        // where the various UUIDs come from.
        [SerializeField]
        private string stateName = "exampleState.json";

        Guid giUuid = new Guid("48cca33b-e1ae-4998-a0d1-2eee1e75e07d");
        SimpleGlyphDataImpression gi;

        Guid greenCmapUuid = new Guid("5a761a72-8bcb-11ea-9265-005056bae6d8");
        IVisAsset greenCmap;

        private float switchInterval = 1.0f;
        private float lastSwitchTime = 0.0f;

        void Start()
        {
            Debug.Log("Loading state " + stateName);
            ABREngine.Instance.LoadState<ResourceStateFileLoader>(stateName);
            gi = ABREngine.Instance.GetDataImpression(giUuid) as SimpleGlyphDataImpression;
            ABREngine.Instance.VisAssets.TryGetVisAsset(greenCmapUuid, out greenCmap);
        }

        void Update()
        {
            // Nothing to update if the data impression isn't defined
            if (gi == null)
            {
                return;
            }

            // Only update the colormap every so often
            if ((Time.realtimeSinceStartup - lastSwitchTime) > switchInterval)
            {
                lastSwitchTime = Time.realtimeSinceStartup;

                if (gi.colormap.Uuid == greenCmapUuid)
                {
                    gi.colormap = ABREngine.Instance.VisAssets.GetDefault<ColormapVisAsset>() as ColormapVisAsset;
                }
                else
                {
                    gi.colormap = greenCmap as ColormapVisAsset;
                }
            }

            // Continuously update the glyph size to be oscillating
            float glyphSize = (0.1f * (Mathf.Sin(Time.realtimeSinceStartup) + 1.0f)) + 0.05f;
            gi.glyphSize = new LengthPrimitive(glyphSize);

            // Tell the engine that the style has changed
            gi.RenderHints.StyleChanged = true;

            // Lastly update the visuals
            ABREngine.Instance.Render();
        }
    }
}
