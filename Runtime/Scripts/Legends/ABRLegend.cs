/* ABRLegend.cs
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

using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;

namespace IVLab.ABREngine.Legends
{
    /// <summary>
    /// Generate geometry, encodings, and legend images/GameObjects for ABR
    /// states
    /// </summary>
    public class ABRLegend
    {
        public static SimpleGlyphDataImpression CreateGlyphLegendEntry(IColormapVisAsset colormap, IGlyphVisAsset glyph)
        {
            string glyphDataPath = "ABR/Legends/KeyData/Glyphs";
            int numVars = 0;
            if (colormap != null) numVars++;
            if (glyph != null) numVars++;

            RawDataset rds;
            if (!ABREngine.Instance.Data.TryGetRawDataset(glyphDataPath, out rds))
            {
                rds = ABRLegendGeometry.Glyphs(numVars);
                ABREngine.Instance.Data.ImportRawDataset(glyphDataPath, rds);
            }

            IKeyData glyphKeyData = null;
            Dataset ds = null;
            ABREngine.Instance.Data.TryGetDataset(DataPath.GetDatasetPath(glyphDataPath), out ds);
            ds.TryGetKeyData(glyphDataPath, out glyphKeyData);

            SimpleGlyphDataImpression gi = new SimpleGlyphDataImpression();
            // Apply the artist-selected VisAssets
            gi.colormap = colormap;
            gi.glyph = glyph;

            // Apply legend-specific entries
            gi.keyData = glyphKeyData as PointKeyData;
            gi.colorVariable = ds.GetAllScalarVars().First(v => v.Key.Contains("XAxis")).Value;
            gi.glyphVariable = ds.GetAllScalarVars().First(v => v.Key.Contains("ZAxis")).Value;
            gi.forwardVariable = ds.GetAllVectorVars().First(v => v.Key.Contains("Forward")).Value;
            gi.upVariable = ds.GetAllVectorVars().First(v => v.Key.Contains("Up")).Value;
            gi.glyphSize = new LengthPrimitive("0.3m");

            return gi;
        }
    }
}