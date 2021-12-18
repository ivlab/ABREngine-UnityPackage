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

using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using IVLab.Utilities;
using System;

namespace IVLab.ABREngine.Legends
{
    /// <summary>
    /// Generate geometry, encodings, and legend images/GameObjects for ABR
    /// states
    /// </summary>
    public class ABRLegend : MonoBehaviour
    {
        public GameObject legendEntry2DPrefab;
        public Vector3 entrySeparation;

        // List of current legend entries - names are EncodedGameObject UUIDs
        private List<GameObject> legendEntryGameObjects = new List<GameObject>();

        /// <summary>
        /// Update the legend display in Unity from the current ABR state
        /// </summary>
        public void UpdateLegend()
        {
            // Clear all existing legend entries
            for (int g = 0; g < this.transform.childCount; g++)
            {
                Destroy(this.transform.GetChild(g));
            }
            foreach (var i in legendEntryGameObjects)
            {
                ABREngine.Instance.UnregisterDataImpression(new Guid(i.name));
            }
            legendEntryGameObjects.Clear();

            int entryIndex = 0;
            // Create legend clones of each data impression type, register them
            // in the engine, set up the legend GameObject, then modify all
            // label text for each legend entry
            List<SimpleSurfaceDataImpression> surfImpressions = ABREngine.Instance.GetDataImpressions<SimpleSurfaceDataImpression>();
            foreach (var i in surfImpressions)
            {
                SimpleSurfaceDataImpression li = CreateSurfaceLegendEntry(i.colormap, i.pattern);
                ABREngine.Instance.RegisterDataImpression(li);
                ABRLegendEntry entry = SetupLegendEntry(li, entryIndex++);

                entry.SetTextLabel(ABRLegendEntry.Label.Title, DataPath.GetName(i.keyData?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.XAxis, DataPath.GetName(i.colorVariable?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.XAxisMin, i.colorVariable?.Range.min.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.XAxisMax, i.colorVariable?.Range.max.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.YAxis, DataPath.GetName(i.patternVariable?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.YAxisMin, i.patternVariable?.Range.min.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.YAxisMax, i.patternVariable?.Range.max.ToString());
            }
            List<SimpleLineDataImpression> lineImpressions = ABREngine.Instance.GetDataImpressions<SimpleLineDataImpression>();
            foreach (var i in lineImpressions)
            {
                SimpleLineDataImpression li = CreateRibbonLegendEntry(i.colormap, i.lineTexture, i.colorVariable != null, i.lineTextureVariable != null);
                ABREngine.Instance.RegisterDataImpression(li);
                ABRLegendEntry entry = SetupLegendEntry(li, entryIndex++);

                entry.SetTextLabel(ABRLegendEntry.Label.Title, DataPath.GetName(i.keyData?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.XAxis, DataPath.GetName(i.colorVariable?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.XAxisMin, i.colorVariable?.Range.min.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.XAxisMax, i.colorVariable?.Range.max.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.YAxis, DataPath.GetName(i.lineTextureVariable?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.YAxisMin, i.lineTextureVariable?.Range.min.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.YAxisMax, i.lineTextureVariable?.Range.max.ToString());
            }
            List<SimpleGlyphDataImpression> glyphImpressions = ABREngine.Instance.GetDataImpressions<SimpleGlyphDataImpression>();
            foreach (var i in glyphImpressions)
            {
                SimpleGlyphDataImpression li = CreateGlyphLegendEntry(i.colormap, i.glyph, i.colorVariable != null, i.glyphVariable != null);
                ABREngine.Instance.RegisterDataImpression(li);
                ABRLegendEntry entry = SetupLegendEntry(li, entryIndex++);

                entry.SetTextLabel(ABRLegendEntry.Label.Title, DataPath.GetName(i.keyData?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.XAxis, DataPath.GetName(i.colorVariable?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.XAxisMin, i.colorVariable?.Range.min.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.XAxisMax, i.colorVariable?.Range.max.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.YAxis, DataPath.GetName(i.glyphVariable?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.YAxisMin, i.glyphVariable?.Range.min.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.YAxisMax, i.glyphVariable?.Range.max.ToString());
            }

            ABREngine.Instance.Render();

            // Move each Data Impression GameObject underneath the legend GameObject
            foreach (var go in legendEntryGameObjects)
            {
                EncodedGameObject ego = ABREngine.Instance.GetEncodedGameObject(new Guid(go.name));
                ego.gameObject.transform.SetParent(go.transform, false);
            }
        }

        private ABRLegendEntry SetupLegendEntry(IDataImpression di, int entryIndex)
        {
            GameObject entryGo = Instantiate(legendEntry2DPrefab);
            entryGo.transform.SetParent(this.transform, false);
            entryGo.name = di.Uuid.ToString();
            entryGo.transform.localPosition += entrySeparation * entryIndex;
            legendEntryGameObjects.Add(entryGo);
            return entryGo.GetComponent<ABRLegendEntry>();
        }

        /// <summary>
        /// Construct a glyph data impression for a glyph legend entry
        /// </summary>
        public static SimpleGlyphDataImpression CreateGlyphLegendEntry(IColormapVisAsset colormap, IGlyphVisAsset glyph, bool hasColormapVar, bool hasGlyphVar)
        {
            string glyphDataPath = "ABR/Legends/KeyData/Glyphs";
            int numVars = 0;
            if (hasColormapVar) numVars++;
            if (hasGlyphVar) numVars++;

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
            gi.colorVariable = ds.GetAllScalarVars().FirstOrDefault(v => v.Key.Contains("XAxis")).Value;
            gi.glyphVariable = ds.GetAllScalarVars().FirstOrDefault(v => v.Key.Contains("ZAxis")).Value;
            gi.forwardVariable = ds.GetAllVectorVars().FirstOrDefault(v => v.Key.Contains("Forward")).Value;
            gi.upVariable = ds.GetAllVectorVars().FirstOrDefault(v => v.Key.Contains("Up")).Value;
            gi.glyphSize = new LengthPrimitive("0.3m");

            return gi;
        }

        /// <summary>
        /// Construct a ribbon data impression for a line legend entry
        /// </summary>
        public static SimpleLineDataImpression CreateRibbonLegendEntry(IColormapVisAsset colormap, ILineTextureVisAsset line, bool hasColormapVar, bool hasLineVar)
        {
            string dataPath = "ABR/Legends/KeyData/Ribbons";
            int numVars = 0;
            if (hasColormapVar) numVars++;
            if (hasLineVar) numVars++;

            RawDataset rds;
            if (!ABREngine.Instance.Data.TryGetRawDataset(dataPath, out rds))
            {
                rds = ABRLegendGeometry.Ribbons(numVars);
                ABREngine.Instance.Data.ImportRawDataset(dataPath, rds);
            }

            IKeyData kd = null;
            Dataset ds = null;
            ABREngine.Instance.Data.TryGetDataset(DataPath.GetDatasetPath(dataPath), out ds);
            ds.TryGetKeyData(dataPath, out kd);

            SimpleLineDataImpression li = new SimpleLineDataImpression();
            // Apply the artist-selected VisAssets
            li.colormap = colormap;
            li.lineTexture = line;

            // Apply legend-specific entries
            li.defaultCurveDirection = Vector3.forward;
            li.keyData = kd as LineKeyData;
            li.colorVariable = ds.GetAllScalarVars().FirstOrDefault(v => v.Key.Contains("XAxis")).Value;
            li.lineTextureVariable = ds.GetAllScalarVars().FirstOrDefault(v => v.Key.Contains("ZAxis")).Value;
            li.lineWidth = new LengthPrimitive("0.3m");

            return li;
        }

        /// <summary>
        /// Construct a surface data impression for legend entry
        /// </summary>
        public static SimpleSurfaceDataImpression CreateSurfaceLegendEntry(IColormapVisAsset colormap, ISurfaceTextureVisAsset texture)
        {
            string dataPath = "ABR/Legends/KeyData/Surfaces";
            RawDataset rds;
            if (!ABREngine.Instance.Data.TryGetRawDataset(dataPath, out rds))
            {
                rds = ABRLegendGeometry.Surface();
                ABREngine.Instance.Data.ImportRawDataset(dataPath, rds);
            }

            IKeyData kd = null;
            Dataset ds = null;
            ABREngine.Instance.Data.TryGetDataset(DataPath.GetDatasetPath(dataPath), out ds);
            ds.TryGetKeyData(dataPath, out kd);

            SimpleSurfaceDataImpression si = new SimpleSurfaceDataImpression();
            // Apply the artist-selected VisAssets
            si.colormap = colormap;
            si.pattern = texture;

            // Apply legend-specific entries
            si.keyData = kd as SurfaceKeyData;
            si.colorVariable = ds.GetAllScalarVars().FirstOrDefault(v => v.Key.Contains("XAxis")).Value;
            si.patternVariable = ds.GetAllScalarVars().FirstOrDefault(v => v.Key.Contains("ZAxis")).Value;

            return si;
        }
    }
}