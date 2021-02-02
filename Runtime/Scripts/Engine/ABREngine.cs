/* ABREngine.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using System.Collections.Generic;
using IVLab.Utilities;
using UnityEngine;

namespace IVLab.ABREngine
{
    [RequireComponent(typeof(DataManager), typeof(VisAssetManager))]
    public class ABREngine : Singleton<ABREngine>
    {
        private List<DataImpression> dataImpressions = new List<DataImpression>();
    }
}