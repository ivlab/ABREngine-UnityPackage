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

Shader "ABR/Surface"
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
            // Physically based Standard lighting model, and enable shadows on all light types
            #pragma surface surf Standard fullforwardshadows  vertex:vert addshadow

            // Use shader model 3.0 target, to get nicer looking lighting
            #pragma target 3.0


            float Remap(float dataValue, float from0, float to0, float from1, float to1)
            {
                return from1 + (dataValue - from0) * (to1 - from1) / (to0 - from0);
            }

            // Color parameters
            sampler2D _ColorMap;
            int _UseColorMap = false;
            int _UsePatternVariable;

            float _ColorDataMin;
            float _ColorDataMax;

            // Texture / pattern parameters
            sampler2D _Pattern;
            sampler2D _PatternNormal;

            float _PatternDataMin;
            float _PatternDataMax;

            float _PatternDirectionBlend = 1;
            float _PatternScale = 0.0;
            float _PatternSaturation = 1.0;
            float _PatternIntensity = 1.0;

            struct Input
            {
                float2 uv_MainTex;
                float4 color : COLOR;
                float3 normal : NORMAL;
                float3 position;
            };

            half _Glossiness;
            half _Metallic;
            fixed4 _Color;

            // Vertex shader - simply compute positions and normals
            void vert(inout appdata_full v, out Input o) {
                UNITY_INITIALIZE_OUTPUT(Input, o);
                o.normal = abs(v.normal);
                o.position = (v.vertex);
            }

            // Blend overlay method (instead of Multiply)
            fixed3 overlayBlend(fixed3 a, fixed3 b) {
                float twiceLuminance = dot(b, fixed4(0.2126, 0.7152, 0.0722, 0)) * 2;

                fixed3 output = 0;
                // The actual Overlay/High Light method is based on the shader
                if (twiceLuminance < 1) {
                    output = lerp(fixed3(0, 0, 0), a, twiceLuminance);
                }
                else {
                    output = lerp(a, fixed3(1, 1, 1), twiceLuminance - 1);
                }
                return output;
            }

            // The surface shader
            void surf(Input IN, inout SurfaceOutputStandard o)
            {
                float3 poscoodinates = IN.position.xyz;

                // DEBUG: ensure position coordinates are correct
                // o.Albedo = poscoodinates;

                // Variables: color variable, pattern variable, null, null
                fixed4 variables = IN.color;

                // Compute UV coordinates for tri-planar projection
                float3 normal = IN.normal.xyz;
                float2 uv0 = poscoodinates.yz;
                uv0.x /= _PatternScale;
                uv0.y /= _PatternScale;
                uv0 = uv0 - floor(uv0);

                float2 uv1 = poscoodinates.xz;
                uv1.x /= _PatternScale;
                uv1.y /= _PatternScale;
                uv1 = uv1 - floor(uv1);

                float2 uv2 = poscoodinates.xy;// - floor(IN.position.xy);
                uv2.x /= _PatternScale;
                uv2.y /= _PatternScale;
                uv2 = uv2 - floor(uv2);

                float a = normal.x;
                float b = normal.y;
                float c = normal.z;

                float sum = a + b + c;
                a /= sum;
                b /= sum;
                c /= sum;

                float average = (a + b + c) / 3;
                float mx = max(a, max(b, c));

                // Compute "powerfulness" of tri-planar projection
                float degree = _PatternDirectionBlend;
                if (degree > 0.04) {
                    a = pow(-2 * a * a * a + 3 * a * a, 1 / degree);
                    b = pow(-2 * b * b * b + 3 * b * b, 1 / degree);
                    c = pow(-2 * c * c * c + 3 * c * c, 1 / degree);
                }
                else {
                    if (a < mx) a = 0; else a = 1;
                    if (b < mx) b = 0; else b = 1;
                    if (c < mx) c = 0; else c = 1;
                }

                sum = a + b + c;

                a /= sum;
                b /= sum;
                c /= sum;

                float3 colorA;
                float3 colorB;
                float3 colorC;
                float3 normalA;
                float3 normalB;
                float3 normalC;

                // Compute colors / normals for tri-planar projection
                colorA = tex2D(_Pattern, uv0);
                colorB = tex2D(_Pattern, uv1);
                colorC = tex2D(_Pattern, uv2);
                normalA = UnpackNormal(tex2D(_PatternNormal, uv0));
                normalB = UnpackNormal(tex2D(_PatternNormal, uv1));
                normalC = UnpackNormal(tex2D(_PatternNormal, uv2));

                float3 textureColor = colorA * a + colorB * b + colorC * c;
                float3 norm = normalA * a + normalB * b + normalC * c;
                norm = normalize(norm);

                // Compute saturation
                float3 grayTextureColor = dot(textureColor, float3(0.3, 0.59, 0.11));
                textureColor = lerp(grayTextureColor, textureColor, _PatternSaturation);

                // Compute intensity
                textureColor = lerp(1, textureColor, _PatternIntensity);

                // Get color / pattern variable data at at this fragment
                float vColor = variables.r;
                float vPattern = variables.g;
                float vColorNorm = clamp(Remap(vColor, _ColorDataMin,_ColorDataMax,0,1),0.01,0.99);
                float vPatternNorm = clamp(Remap(vPattern, _PatternDataMin, _PatternDataMax, 0, 1), 0.001, 0.99);

                // Apply pattern
                if (_UsePatternVariable) {
                    textureColor = lerp(fixed3(1, 1, 1), textureColor, clamp(vPatternNorm, 0, 1));
                    norm = lerp( o.Normal, norm, clamp(vPatternNorm,0,1));
                    norm = normalize(norm);
                }

                // Apply colormap
                if (_UseColorMap == 1)
                {
                    float3 colorMapColor = tex2D(_ColorMap, float2(vColorNorm, 0.5));
                    o.Albedo = colorMapColor;
                }
                else
                {
                    o.Albedo = _Color.rgb;
                }

                // Use Multiply method (could use overlay instead)
                o.Albedo = o.Albedo * textureColor;
                //o.Albedo = overlayBlend(o.Albedo, textureColor);

                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
                o.Alpha = 1;
            }
            ENDCG
        }
            FallBack "Diffuse"
}
