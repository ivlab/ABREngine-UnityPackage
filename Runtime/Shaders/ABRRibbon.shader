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

Shader "ABR/Ribbon"
{
    Properties
    {
        _ColorMap("ColorMap",2D) = "white" {}
        [PerRendererData]
        _ColorDataMin("ColorDataMin",Float) = 0.0
        [PerRendererData]
        _ColorDataMin("ColorDataMax",Float) = 1.0
        _RibbonBrightness("RibbonBrightness", Float) = 1.0
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
        _Color("Color", Color) = (1,1,1,1)
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
        _BlendMaps("Blend Maps", 2D) = "white" {}
        _Texture("Textures", 2D) = "white" {}
    }
        SubShader
        {
            Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout"  }
            LOD 200
            Cull Back
            CGPROGRAM
            // Physically based Standard lighting model, and enable shadows on all light types
            #pragma surface surf SimpleLambert fullforwardshadows vertex:vert addshadow
            // Use shader model 3.0 target, to get nicer looking lighting
            #pragma target 3.0

            // Ribbon parameters
            sampler2D _Texture;
            sampler2D _TextureNRM;

            // Aspect ratio (width / height) of textures
            float _TextureAspect[16];
            // Aspect ratio (height / width) of textures
            float _TextureHeightWidthAspect[16];

            float _TextureCutoff;
            float _RibbonBrightness;
            int _UseLineTexture = 0;
            float _Blend = 1;

            // Colormap parameters
            sampler2D _ColorMap;
            int _UseColorMap = false;
            float4 _ScalarMin;
            float4 _ScalarMax;

            // Number of textures in this gradient
            uint _NumTex = 0u;
            // Blending for each texture (max of 4 per texture, tex 1 is red, tex 2 is green, tex 3 is blue, tex 4 is alpha)
            sampler2D _BlendMaps;

            struct Input
            {
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 ScreenPos : TEXCOORD2;
            };

            half _Glossiness;
            half _Metallic;
            fixed4 _Color;

            // Lambert lighting for custom lighting on ribbons (use ribbon brightness instead of actual lights)
            half4 LightingSimpleLambert(SurfaceOutput s, half3 lightDir, half atten) {
                half NdotL = _RibbonBrightness;// dot(s.Normal, lightDir);
                half4 c;
                c.rgb =  s.Albedo * _LightColor0.rgb* (atten* NdotL);
                c.a = s.Alpha;
                return c;
            }

            float Remap(float dataValue, float from0, float to0, float from1, float to1)
            {
                return from1 + (dataValue - from0) * (to1 - from1) / (to0 - from0);
            }

            // Vertex shader - just compute screen position at each vertex
            void vert(inout appdata_full v, out Input o) {
                UNITY_INITIALIZE_OUTPUT(Input, o);
                o.texcoord.xy = abs(v.texcoord.xy);
                o.ScreenPos = ComputeScreenPos(v.vertex);
            }

            void surf(Input IN, inout SurfaceOutput  o)
            {
                uint SupportedChannels = 4u;

                // Variables: color, ribbon, null, null
                fixed4 variables = IN.color;

                // Calculate how tall each texture is (percentage of the whole)
                float suma = 0;
                for (uint i = 0; i < _NumTex; i++) {
                    suma += _TextureHeightWidthAspect[i];
                }
                // Maximum of 16 textures supported (4 groups)
                float hwAspectPercent[16];
                for (uint j = 0; j < _NumTex; j++) {
                    hwAspectPercent[j] = _TextureHeightWidthAspect[j] / suma;
                }

                // Find number of "grouped" blend map textures
                uint numGroups = _NumTex / SupportedChannels + 1;
                float groupSize = 1.0 / numGroups;
                float groupOffset = 0.5 * groupSize;

                // DEBUG: Check actual data values
                // o.Albedo = (variables / 20) + 0.5;
                // return;

                // Apply colormap
                float3 vColor = variables.r;
                float vColorNorm = clamp(Remap(vColor, _ScalarMin[0], _ScalarMax[0], 0, 1),0.01,0.99);
                if (_UseColorMap == 1)
                {
                    float3 colorMapColor = tex2D(_ColorMap, float2(vColorNorm, 0.5));
                    o.Albedo = colorMapColor;
                }
                else
                {
                    o.Albedo = _Color.rgb;
                }

                if (_UseLineTexture) {
                    // Final pixel color is computed in "groups" of 4 textures (according to the blendmap)
                    // support up to 16 textures (4 groups)
                    float blendPercentages[16];
                    for (uint k = 0; k < 16; k++) {
                        blendPercentages[k] = 0.0;
                    }

                    // Aggregate all blend percentages for each group
                    float blendMapX = clamp(Remap(variables.y, _ScalarMin.y, _ScalarMax.y, 0, 1), 0.001, 0.999);
                    for (int group = 0; group < (int) numGroups; group++) {
                        float blendMapY = group * groupSize + groupOffset;
                        float4 blendPercentageGroup = tex2D(_BlendMaps, float2(blendMapX, blendMapY));
                        int index = group * SupportedChannels;
                        blendPercentages[index + 0] += blendPercentageGroup.r;
                        blendPercentages[index + 1] += blendPercentageGroup.g;
                        blendPercentages[index + 2] += blendPercentageGroup.b;
                        blendPercentages[index + 3] += blendPercentageGroup.a;
                    }

                    // DEBUG: Show blend percentages for first few textures
                    // o.Albedo.r = blendPercentages[0];
                    // o.Albedo.g = blendPercentages[1];
                    // o.Albedo.b = blendPercentages[2];

                    // Blend the various line textures to see if this fragment should be included
                    float3 textureColor = 0;
                    float vOffsetTotal = 0;
                    for (uint texIndex = 0u; texIndex < _NumTex; texIndex++)
                    {
                        // Calculate the *actual* UV coordinate within THIS texture (not all textures are the same height)
                        // X coordinate loops occasionally along the length of the line
                        float u = (IN.texcoord.x / _TextureAspect[texIndex]) % 1;
                        // Y coordinate skips up a texture each iteration of this loop
                        float vOffset = hwAspectPercent[texIndex];
                        float v = IN.texcoord.y * vOffset + vOffsetTotal;
                        vOffsetTotal += vOffset;

                        float3 currentColor = tex2D(_Texture, float2(u, v));
                        currentColor *= blendPercentages[texIndex];
                        textureColor += currentColor;
                    }

                    // DEBUG: Check texture application
                    // o.Albedo = textureColor;
                    // return;

                    // Throw away this fragment if it's above the value of _TextureCutoff
                    if (textureColor.g > _TextureCutoff)
                    {
                        discard;
                    }
                }

                o.Alpha = 1.0;
            }
            ENDCG
        }
            FallBack "Diffuse"
}