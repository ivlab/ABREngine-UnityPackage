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

Shader "ABR/InstancedGlyphsOutline" {
    Properties{
    }
    SubShader{
        Tags { "RenderType" = "Opaque"  }
        LOD 200

        // Pass 1: Render the basic object
        Pass {
            // However, don't show the object to the screen or make it depth-intersect anything
            Blend Zero One
            ZWrite Off

            // Write a bunch of 1's to the stencil buffer where the object is
            Stencil {
                Ref 1
                Comp Always
                Pass Replace
            }

            CGPROGRAM

            #include "UnityCG.cginc"
            #include "ABRGlyphsCore.cginc"
            // Physically based Standard lighting model
            // #pragma surface surf Standard addshadow fullforwardshadows

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            struct appdata
            {
                float4 vertex : POSITION;
                uint instanceId : SV_InstanceID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID // use this to access instanced properties in the fragment shader.
            };

            v2f vert(appdata v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_TARGET {
                UNITY_SETUP_INSTANCE_ID(i);
                return 1;
            }

            ENDCG
        }

        // Pass 2: Render outline
        Pass {
            // Only show outline on backfaces
            Cull Front

            // Only show the outline OUTSIDE the original object (where we
            // DIDN'T write 1's to the stencil buffer)
            Stencil {
                Ref 1
                Comp NotEqual
            }

            CGPROGRAM

            #include "UnityCG.cginc"
            #include "ABRGlyphsCore.cginc"
            #pragma vertex VertexProgram
            #pragma fragment FragmentProgram
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            half _OutlineWidth;
            half4 _OutlineColor;

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f VertexProgram(appdata v) {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 clipNormal = mul((float3x3) UNITY_MATRIX_VP, mul((float3x3) UNITY_MATRIX_M, v.normal));
                float4 clipPosition = UnityObjectToClipPos(v.vertex);
                clipPosition.xyz += normalize(clipNormal) * _OutlineWidth;

                o.normal = clipNormal;
                o.vertex = clipPosition;

                return o;
            }

            half4 FragmentProgram(in v2f i) : SV_TARGET {
                UNITY_SETUP_INSTANCE_ID(i);

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

                return _OutlineColor;
            }

            ENDCG
        }
    }
    FallBack "Diffuse"
}