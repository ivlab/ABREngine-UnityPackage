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
using TMPro;

namespace IVLab.ABREngine.Legends
{
    /// <summary>
    /// Controller for 3D GameObject-based legends for ABR
    /// </summary>
    public class ABRLegendEntry : MonoBehaviour
    {
        // GameObject names for text to update
        public enum Label
        {
            TextLabels,
            Title,
            XAxis,
            YAxis,
            XAxisMin,
            YAxisMin,
            XAxisMax,
            YAxisMax,
        }

        public void SetTextLabel(Label labelName, string text)
        {
            GameObject matchingText = GetTextLabel(labelName);
            if (text == null)
            {
                matchingText.SetActive(false);
            }
            else
            {
                TextMeshPro textObj = matchingText.GetComponent<TextMeshPro>();
                textObj.text = text;
            }
        }

        private GameObject GetTextLabel(Label labelName)
        {
            GameObject textParent = null;
            foreach (Transform tf in this.transform)
            {
                if (tf.gameObject.name == Label.TextLabels.ToString("G")) {
                    textParent = tf.gameObject;
                    break;
                }
            }
            foreach (Transform tf in textParent.transform)
            {
                if (tf.gameObject.name == labelName.ToString("G")) {
                    return tf.gameObject;
                }
            }
            return null;
        }
    }
}