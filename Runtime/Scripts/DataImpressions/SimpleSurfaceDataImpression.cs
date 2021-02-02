/* SimpleSurfaceDataImpression.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

namespace IVLab.ABREngine
{
    [ABRPlateType("Surfaces")]
    public class SimpleSurfaceDataImpression : DataImpression
    {
        [ABRInput("Key Data", "Key Data")]
        public SurfaceKeyData keyData;

        [ABRInput("Color Variable", "Color")]
        public ScalarDataVariable colorVariable;

        [ABRInput("Colormap", "Color")]
        public ColormapVisAsset colormap;

        [ABRInput("Pattern Variable", "Pattern")]
        public ScalarDataVariable patternVariable;

        [ABRInput("Pattern", "Pattern")]
        public SurfaceTextureVisAsset pattern;


        // TODO add the primitive inputs
    }
}
