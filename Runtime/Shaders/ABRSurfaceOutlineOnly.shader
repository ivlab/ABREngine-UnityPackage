// Copyright (c) 2022, University of Minnesota
// Authors: Bridger Herman <herma582@umn.edu>
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

Shader "ABR/SurfaceOutlineOnly" {

    Properties {

        _Color ("Color", Color) = (1, 1, 1, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0

        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.03

    }

    // Inspired By: https://www.videopoetics.com/tutorials/pixel-perfect-outline-shaders-unity/

    Subshader {

        Tags {
            "RenderType" = "Opaque"
        }

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

            #pragma vertex vert
            #pragma fragment frag

            float4 vert(float4 position : POSITION) : SV_POSITION {
                return UnityObjectToClipPos(position);
            }

            half4 frag() : SV_TARGET {
                return 1;
            }

            ENDCG
        }

        // Don't show the object to the screen
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

            #pragma vertex VertexProgram
            #pragma fragment FragmentProgram

            half _OutlineWidth;
            half4 _OutlineColor;

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
            };

            v2f VertexProgram(
                    float4 position : POSITION,
                    float3 normal : NORMAL) {

                float3 clipNormal = mul((float3x3) UNITY_MATRIX_VP, mul((float3x3) UNITY_MATRIX_M, normal));
                float4 clipPosition = UnityObjectToClipPos(position);
                clipPosition.xyz += normalize(clipNormal) * _OutlineWidth;

                v2f o;
                o.normal = clipNormal;
                o.vertex = clipPosition;

                return o;
            }

            half4 FragmentProgram(in v2f o) : SV_TARGET {
                return _OutlineColor;
            }

            ENDCG

        }

    }

}