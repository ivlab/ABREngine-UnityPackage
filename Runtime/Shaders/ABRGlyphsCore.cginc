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

sampler2D _MainTex;

// Normalmap for this LOD
sampler2D _Normal;

// Transform matrix (& inverse) for this particular glyph
float4x4 _ObjectTransform;
float4x4 _ObjectTransformInverse;

// Scalar rendering info: colors, null, null, density (whether or not to render this glyph)
float4 _RenderInfo;

// Colormap parameters
int _UseColorMap;
int _ForceOutlineColor;
sampler2D _ColorMap;
float4 _Color;
float4 _NaNColor;
float _ColorDataMin;
float _ColorDataMax;

half _Glossiness;
half _Metallic;

struct Input {
    float2 uv_MainTex;
};

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
StructuredBuffer<float4> renderInfoBuffer;
StructuredBuffer<float4x4> transformBuffer;
StructuredBuffer<float4x4> transformBufferInverse;
// Per glyph visibility flags
int _HasPerGlyphVisibility;
StructuredBuffer<int> _PerGlyphVisibility;
#endif

// Set up for rendering this glyph
void setup()
{
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                float4x4 matData = transformBuffer[unity_InstanceID];
                float4x4 matDataInverse = transformBufferInverse[unity_InstanceID];

                unity_ObjectToWorld = matData;

                unity_WorldToObject = matDataInverse;

                unity_ObjectToWorld = mul(_ObjectTransform, unity_ObjectToWorld);
                unity_WorldToObject = mul(unity_WorldToObject, _ObjectTransformInverse );
#endif
}

//http://answers.unity.com/answers/1726150/view.html
float IsNaN_float(float In)
{
    return (In < 0.0 || In > 0.0 || In == 0.0) ? 0 : 1;
}

void rotate2D(inout float2 v, float r)
{
    float s, c;
    sincos(r, s, c);
    v = float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float Remap(float dataValue, float from0, float to0, float from1, float to1)
{
    return from1 + (dataValue - from0) * (to1 - from1) / (to0 - from0);
}
