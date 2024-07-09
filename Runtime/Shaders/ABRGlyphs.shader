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

Shader "ABR/InstancedGlyphs" {
    Properties{
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
        _Normal("Normal (RGB)", 2D) = "bump" {}

    }
    SubShader{
        Tags { "RenderType" = "Opaque"  }
        LOD 200

        CGPROGRAM
        #include "ABRGlyphsCore.cginc"
        // Physically based Standard lighting model
        #pragma surface surf Standard addshadow fullforwardshadows
        #pragma multi_compile_instancing
        #pragma instancing_options procedural:setup

        void surf(Input IN, inout SurfaceOutputStandard o) {
            // Initialize render info
            fixed4 renderInfo;
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            renderInfo = renderInfoBuffer[unity_InstanceID];

            // Discard this glyph if it's not visible
            if (_HasPerGlyphVisibility) {
                uint glyphVisibilityIndex = unity_InstanceID / 32;
                uint glyphVisibilityRem = unity_InstanceID % 32;
                if (!(_PerGlyphVisibility[glyphVisibilityIndex] & (1 << glyphVisibilityRem)))
                    discard;
            }
#else
            renderInfo = _RenderInfo;
#endif
            // Alpha channel of render info determines whether or not to render this glyph:
            // a >= 0 -> render
            // a < 0  -> discard
            if (renderInfo.a < 0)
                discard;

            // Red channel of render info provides scalar value for this glyph
            float scalarValue = renderInfo.r;

            // Normalizing scalar allows us to use it for colormap-texture lookup
            float scalarValueNorm = clamp(Remap(scalarValue, _ColorDataMin, _ColorDataMax, 0, 1), 0.01, 0.99);
            if (_UseColorMap == 1)
            {
                if (!IsNaN_float(scalarValue))
                    o.Albedo = tex2D(_ColorMap, float2(scalarValueNorm, 0.25));
                else
                    o.Albedo = _NaNColor;
            }
            else
            {
                o.Albedo = _Color;
            }

            // Look up and unpack normal from texture
            float4 map1 = tex2D(_Normal, IN.uv_MainTex);
            o.Normal = UnpackNormal(map1);

            o.Metallic = 0;
            o.Alpha = 1;
        }

        ENDCG
    }
    FallBack "Diffuse"
}