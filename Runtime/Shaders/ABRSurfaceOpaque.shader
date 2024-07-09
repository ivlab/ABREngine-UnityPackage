// Copyright (c) 2021, University of Minnesota
// Authors: Seth Johnson <sethalanjohnson@gmail.com>, Bridger Herman
// <herma582@umn.edu>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

Shader "ABR/SurfaceOpaque"
{
    Properties
    {
        [PerRendererData]
        _ColorMap("ColorMap",2D) = "white" {}
        [PerRendererData]
        _ColorDataMin("ColorDataMin",Float) = 0.0
        [PerRendererData]
        _ColorDataMin("ColorDataMax",Float) = 1.0
        _PatternNormal("Normal (RGB)", 2D) = "bump" {}
        _Pattern("Stacked Textures", 2D) = "white" {}
        _BlendMaps("Stacked Blend Maps", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque"  }
        LOD 200
        Cull Back

        CGPROGRAM
        #include "ABRSurfaceCore.cginc"

        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma vertex ABRSurfaceVertex
        #pragma surface surf Standard fullforwardshadows addshadow

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = CalculateABRTexturedSurfaceColor(IN);
        }

        ENDCG
    }
    FallBack "Diffuse"
}
