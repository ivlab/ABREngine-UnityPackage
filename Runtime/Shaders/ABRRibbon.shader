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
            float _TextureAspect;
            float _TextureCutoff;
            float _RibbonBrightness;
            float _Texturescale = 0.5;
            int _UseLineTexture = 0;
            float _Blend = 1;
            float _RibbonDataMin;
            float _RibbonDataMax;

            // Colormap parameters
            sampler2D _ColorMap;
            int _UseColorMap = false;
            float4 _ScalarMin;
            float4 _ScalarMax;

            sampler2D _BlendMap; // Blending for each texture (max of 4, tex 1 is red, tex 2 is green, tex 3 is blue, tex 4 is alpha)
            int _NumTex; // Number of textures in this gradient

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
                _Texturescale = 0.5;
                float percentCoverage = 1.0 / _NumTex;

                // Compute texture coordinates (repeating) based on (width / height) of texture
                float2 uv = float2((percentCoverage * (IN.texcoord.x / _TextureAspect)) % 1, IN.texcoord.y);

                // Variables: color, ribbon, null, null
                fixed4 variables = IN.color;

                // DEBUG: Check actual data values
                // o.Albedo = (variables / 20) + 0.5;
                // return;

                // Percentages of each texture to use at this fragment
                float4 blendPercentages = tex2D(_BlendMap, float2(clamp(Remap(variables.y, _ScalarMin[1], _ScalarMax[1], 0, 1), 0.001, 0.999), 0.5));

                // DEBUG: Check how the blend map applies to the line
                // o.Albedo = blendPercentages;
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

                // Blend the various line textures to see if this fragment should be included
                float3 textureColor = 0;
                for (int texIndex = 0; texIndex < _NumTex; texIndex++)
                {
                    float2 texCoord = float2(uv.x, texIndex * percentCoverage + uv.y / _NumTex);
                    float3 currentColor = tex2D(_Texture, texCoord);
                    currentColor *= blendPercentages[texIndex];
                    textureColor += currentColor;
                }

                // DEBUG: Check texture application
                // o.Albedo = textureColor;
                // return;

                // Throw away this fragment if it's above the value of _TextureCutoff
                if (_UseLineTexture && textureColor.g > _TextureCutoff)
                {
                    discard;
                }

                o.Alpha = 1.0;
            }
            ENDCG
        }
            FallBack "Diffuse"
}