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
        [Tooltip("Prefab for 2D (2 variable) legend entries")]
        public GameObject legendEntry2DPrefab;

        [Tooltip("Offset for each entry of the legend")]
        public Vector3 entryOffset;

        [Tooltip("Should the legend be updated whenever the ABR state is updated?")]
        public bool updateOnABRStateChange = false;

        [Tooltip("Force an update for the legend")]
        [SerializeField] private bool forceLegendUpdate;

        // List of current legend entries - names are EncodedGameObject UUIDs
        private List<GameObject> legendEntryGameObjects = new List<GameObject>();

        // React to update on state change
        private bool _callbackRegistered = false;

        void Update()
        {
            if (updateOnABRStateChange && !_callbackRegistered)
            {
                ABREngine.Instance.OnStateChanged += UpdateLegend;
                _callbackRegistered = true;
            }
            if (!updateOnABRStateChange && _callbackRegistered)
            {
                ABREngine.Instance.OnStateChanged -= UpdateLegend;
                _callbackRegistered = false;
            }
            if (forceLegendUpdate)
            {
                UpdateLegend(null);
                forceLegendUpdate = false;
            }
        }

        /// <summary>
        /// Update the legend display in Unity from the current ABR state
        /// </summary>
        public void UpdateLegend(Newtonsoft.Json.Linq.JObject state)
        {
            // Clear all existing legend entries
            for (int g = 0; g < this.transform.childCount; g++)
            {
                Destroy(this.transform.GetChild(g).gameObject);
            }
            foreach (var i in legendEntryGameObjects)
            {
                ABREngine.Instance.UnregisterDataImpression(new Guid(i.name));
            }
            legendEntryGameObjects.Clear();

            // Obtain background color from state
            Color background = Camera.main.backgroundColor;

            int entryIndex = 0;
            // Create legend clones of each data impression type, register them
            // in the engine, set up the legend GameObject, then modify all
            // label text for each legend entry
            List<SimpleSurfaceDataImpression> surfImpressions = ABREngine.Instance.GetDataImpressions<SimpleSurfaceDataImpression>();
            foreach (var i in surfImpressions)
            {
                if (!i.RenderHints.Visible) continue;
                SimpleSurfaceDataImpression li = CreateSurfaceLegendEntry(i);
                ABREngine.Instance.RegisterDataImpression(li);
                ABRLegendEntry entry = SetupLegendEntry(li, background, entryIndex++);

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
                if (!i.RenderHints.Visible) continue;
                SimpleLineDataImpression li = CreateRibbonLegendEntry(i);
                ABREngine.Instance.RegisterDataImpression(li);
                ABRLegendEntry entry = SetupLegendEntry(li, background, entryIndex++);

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
                if (!i.RenderHints.Visible) continue;
                SimpleGlyphDataImpression li = CreateGlyphLegendEntry(i);
                ABREngine.Instance.RegisterDataImpression(li);
                ABRLegendEntry entry = SetupLegendEntry(li, background, entryIndex++);

                entry.SetTextLabel(ABRLegendEntry.Label.Title, DataPath.GetName(i.keyData?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.XAxis, DataPath.GetName(i.colorVariable?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.XAxisMin, i.colorVariable?.Range.min.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.XAxisMax, i.colorVariable?.Range.max.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.YAxis, DataPath.GetName(i.glyphVariable?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.YAxisMin, i.glyphVariable?.Range.min.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.YAxisMax, i.glyphVariable?.Range.max.ToString());
            }
            List<SimpleVolumeDataImpression> volImpressions = ABREngine.Instance.GetDataImpressions<SimpleVolumeDataImpression>();
            foreach (var i in volImpressions)
            {
                if (!i.RenderHints.Visible) continue;
                SimpleVolumeDataImpression li = CreateVolumeLegendEntry(i);
                ABREngine.Instance.RegisterDataImpression(li);
                ABRLegendEntry entry = SetupLegendEntry(li, background, entryIndex++);

                entry.SetTextLabel(ABRLegendEntry.Label.Title, DataPath.GetName(i.keyData?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.XAxis, DataPath.GetName(i.colorVariable?.Path));
                entry.SetTextLabel(ABRLegendEntry.Label.XAxisMin, i.colorVariable?.Range.min.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.XAxisMax, i.colorVariable?.Range.max.ToString());
                entry.SetTextLabel(ABRLegendEntry.Label.YAxis, null);
                entry.SetTextLabel(ABRLegendEntry.Label.YAxisMin, null);
                entry.SetTextLabel(ABRLegendEntry.Label.YAxisMax, null);
            }

            ABREngine.Instance.Render();

            // Move each Data Impression GameObject underneath the legend GameObject
            foreach (var go in legendEntryGameObjects)
            {
                EncodedGameObject ego = ABREngine.Instance.GetEncodedGameObject(new Guid(go.name));
                ego.gameObject.transform.SetParent(go.transform, false);
            }
        }

        private ABRLegendEntry SetupLegendEntry(IDataImpression di, Color backgroundColor, int entryIndex)
        {
            GameObject entryGo = Instantiate(legendEntry2DPrefab);
            entryGo.transform.SetParent(this.transform, false);
            entryGo.name = di.Uuid.ToString();
            entryGo.transform.localPosition += entryOffset * entryIndex;
            legendEntryGameObjects.Add(entryGo);
            foreach (MeshRenderer r in entryGo.GetComponentsInChildren<MeshRenderer>())
            {
                r.material.color = backgroundColor;
            }
            return entryGo.GetComponent<ABRLegendEntry>();
        }

        /// <summary>
        /// Construct a glyph data impression for a glyph legend entry
        /// </summary>
        public static SimpleGlyphDataImpression CreateGlyphLegendEntry(SimpleGlyphDataImpression i)
        {
            string glyphDataPath = "ABR/Legends/KeyData/Glyphs";
            int numVars = 0;
            if (i.colorVariable != null) numVars++;
            if (i.glyphVariable != null) numVars++;

            ABREngine.Instance.Data.UnloadRawDataset(glyphDataPath);
            RawDataset rds = ABRLegendGeometry.Glyphs(numVars);
            ABREngine.Instance.Data.ImportRawDataset(glyphDataPath, rds);

            IKeyData glyphKeyData = null;
            Dataset ds = null;
            ABREngine.Instance.Data.TryGetDataset(DataPath.GetDatasetPath(glyphDataPath), out ds);
            ds.TryGetKeyData(glyphDataPath, out glyphKeyData);

            // Copy all inputs from the actual impression
            SimpleGlyphDataImpression gi = new SimpleGlyphDataImpression();
            gi.CopyExisting(i);

            // Apply legend-specific entries
            gi.keyData = glyphKeyData as PointKeyData;
            gi.colorVariable = ds.GetAllScalarVars().FirstOrDefault(v => v.Key.Contains("XAxis")).Value;
            gi.glyphVariable = ds.GetAllScalarVars().FirstOrDefault(v => v.Key.Contains("ZAxis")).Value;
            gi.forwardVariable = ds.GetAllVectorVars().FirstOrDefault(v => v.Key.Contains("Forward")).Value;
            gi.upVariable = ds.GetAllVectorVars().FirstOrDefault(v => v.Key.Contains("Up")).Value;
            gi.glyphSize = new LengthPrimitive("0.3m");
            gi.glyphDensity = new PercentPrimitive("100%");

            return gi;
        }

        /// <summary>
        /// Construct a ribbon data impression for a line legend entry
        /// </summary>
        public static SimpleLineDataImpression CreateRibbonLegendEntry(SimpleLineDataImpression i)
        {
            string dataPath = "ABR/Legends/KeyData/Ribbons";
            int numVars = 0;
            if (i.colorVariable != null) numVars++;
            if (i.lineTextureVariable != null) numVars++;

            ABREngine.Instance.Data.UnloadRawDataset(dataPath);
            RawDataset rds = ABRLegendGeometry.Ribbons(numVars);
            ABREngine.Instance.Data.ImportRawDataset(dataPath, rds);

            IKeyData kd = null;
            Dataset ds = null;
            ABREngine.Instance.Data.TryGetDataset(DataPath.GetDatasetPath(dataPath), out ds);
            ds.TryGetKeyData(dataPath, out kd);

            // Copy all inputs from the actual impression
            SimpleLineDataImpression li = new SimpleLineDataImpression();
            li.CopyExisting(i);

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
        public static SimpleSurfaceDataImpression CreateSurfaceLegendEntry(SimpleSurfaceDataImpression i)
        {
            string dataPath = "ABR/Legends/KeyData/Surfaces";
            ABREngine.Instance.Data.UnloadRawDataset(dataPath);
            RawDataset rds = ABRLegendGeometry.Surface();
            ABREngine.Instance.Data.ImportRawDataset(dataPath, rds);

            IKeyData kd = null;
            Dataset ds = null;
            ABREngine.Instance.Data.TryGetDataset(DataPath.GetDatasetPath(dataPath), out ds);
            ds.TryGetKeyData(dataPath, out kd);

            // Copy all inputs from the actual impression
            SimpleSurfaceDataImpression si = new SimpleSurfaceDataImpression();
            si.CopyExisting(i);

            // Then, apply legend-specific entries
            si.keyData = kd as SurfaceKeyData;
            si.colorVariable = ds.GetAllScalarVars().FirstOrDefault(v => v.Key.Contains("XAxis")).Value;
            si.patternVariable = ds.GetAllScalarVars().FirstOrDefault(v => v.Key.Contains("ZAxis")).Value;

            return si;
        }

        /// <summary>
        /// Construct a volume data impression for legend entry
        /// </summary>
        public static SimpleVolumeDataImpression CreateVolumeLegendEntry(SimpleVolumeDataImpression i)
        {
            string dataPath = "ABR/Legends/KeyData/Volumes";
            ABREngine.Instance.Data.UnloadRawDataset(dataPath);
            RawDataset rds = ABRLegendGeometry.Volume();
            ABREngine.Instance.Data.ImportRawDataset(dataPath, rds);

            IKeyData kd = null;
            Dataset ds = null;
            ABREngine.Instance.Data.TryGetDataset(DataPath.GetDatasetPath(dataPath), out ds);
            ds.TryGetKeyData(dataPath, out kd);

            // Copy all inputs from the actual impression
            SimpleVolumeDataImpression si = new SimpleVolumeDataImpression();
            si.CopyExisting(i);

            // Apply legend-specific entries
            si.keyData = kd as VolumeKeyData;
            si.colorVariable = ds.GetAllScalarVars().FirstOrDefault(v => v.Key.Contains("XAxis")).Value;

            return si;
        }
    }
}