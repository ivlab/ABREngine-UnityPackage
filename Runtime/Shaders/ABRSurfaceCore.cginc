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

// Number of textures in this gradient
uint _NumTex = 0;
// Blending for each texture (max of 4 per texture, tex 1 is red, tex 2 is green, tex 3 is blue, tex 4 is alpha)
sampler2D _BlendMaps;

float _PatternDataMin;
float _PatternDataMax;

float _PatternDirectionBlend = 1;
float _PatternScale = 0.0;
float _PatternSaturation = 1.0;
float _PatternIntensity = 1.0;
float _PatternBlendWidth = 0.1;

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

// Converts from general 0->1 tex coords to *actual* tex coord within texture with given index
float2 ActualTexCoord(float2 uv, int texIndex)
{
    // Divide by _NumTex to get coordinate inside base texture; textures are stacked along y-axis
    return float2(uv.x, (texIndex + uv.y) / _NumTex);
}

// Blends seams at corners
// Corners are particularly tricky because three textures need to be blended smoothly instead of two
float3 CornerBlend(float3 color, float2 xyOffset, float2 horizontalTexCoord, float2 verticalTexCoord, float2 sharedTexCoord)
{
    // Compute side blend percentage
    // We want a diagonal gradient cutting through the center of the corner
    // We first define a diagonal line of form f(t) = <a> + t*<b> going through the center of the corner
    // Then we find the distance from the current pixel position to that line in order to compute a smooth diagonal gradient
    float2 a = float2(0.5,0.5);  // Center of the corner
    float2 b = normalize(float2(1,1));  // Ray pointing diagonally through the center of the corner
    float2 x = xyOffset/_PatternBlendWidth;  // Current pixel point in "corner" uv space
    float d = length(x-(a+dot(x-a,b)*b));  // Distance from the line
    float sideBlend = d/sqrt(2);  // Scale distance to only go from 0->0.5

    // Compute corner blend percentage
    // We want a circlular gradient with its center at the tip of the corner
    float cornerBlend = clamp(1-length(1-xyOffset/_PatternBlendWidth),0,1);
    
    // Get color from neighboring tiles
    float3 xColor = tex2D(_Pattern, horizontalTexCoord);  // Horizontal neighbor
    float3 yColor = tex2D(_Pattern, verticalTexCoord);  // Vertical neighbor
    float3 cornerColor = tex2D(_Pattern, sharedTexCoord);  // Shared texcoords that smoothly interpolate between all 4 corners

    // Blend colors and return
    if (xyOffset.x > xyOffset.y)  // Horizontal blend
        color = lerp(lerp(color,cornerColor,x.y), xColor, sideBlend);
    else  // Vertical blend
        color = lerp(lerp(color,cornerColor,x.x), yColor, sideBlend);
    return lerp(color, cornerColor, cornerBlend);  // Corner blend
}

// Blends tiled textures at seams
float3 SeamBlend(float3 color, float2 uv, float2 buv, int texIndex)
{
    // Blend bottom-left
    if (uv.x < _PatternBlendWidth && uv.y < _PatternBlendWidth)
    {
        // Compute uv offset
        float2 xyOffset = float2(_PatternBlendWidth-uv.x, _PatternBlendWidth-uv.y);
        float2 horizontalTexCoord = ActualTexCoord(float2(1-xyOffset.x, buv.y), texIndex);
        float2 verticalTexCoord = ActualTexCoord(float2(buv.x, 1-xyOffset.y), texIndex);
        float2 sharedTexCoord = ActualTexCoord((uv+_PatternBlendWidth)*(1-2*_PatternBlendWidth), texIndex);
        // Compute corner blend
        color = CornerBlend(color, xyOffset, horizontalTexCoord, verticalTexCoord, sharedTexCoord);
    }
    // Blend bottom-right
    else if (uv.x > 1-_PatternBlendWidth && uv.y < _PatternBlendWidth)
    {
        // Compute uv offset
        float2 xyOffset = float2(uv.x-(1-_PatternBlendWidth), _PatternBlendWidth-uv.y);
        float2 offsetUVs = float2(xyOffset.x, 1-xyOffset.y);
        float2 horizontalTexCoord = ActualTexCoord(float2(xyOffset.x, buv.y), texIndex);
        float2 verticalTexCoord = ActualTexCoord(float2(buv.x, 1-xyOffset.y), texIndex);
        float2 sharedTexCoord = ActualTexCoord(float2(xyOffset.x, _PatternBlendWidth+uv.y)*(1-2*_PatternBlendWidth), texIndex);
        // Compute corner blend
        color = CornerBlend(color, xyOffset, horizontalTexCoord, verticalTexCoord, sharedTexCoord);
    }
    // Blend top-left
    else if (uv.x < _PatternBlendWidth && uv.y > 1-_PatternBlendWidth)
    {
        // Compute uv offset
        float2 xyOffset = float2(_PatternBlendWidth-uv.x, uv.y-(1-_PatternBlendWidth));
        float2 offsetUVs = float2(1-xyOffset.x, xyOffset.y);
        float2 horizontalTexCoord = ActualTexCoord(float2(1-xyOffset.x, buv.y), texIndex);
        float2 verticalTexCoord = ActualTexCoord(float2(buv.x, xyOffset.y), texIndex);
        float2 sharedTexCoord = ActualTexCoord(float2(_PatternBlendWidth+uv.x,xyOffset.y)*(1-2*_PatternBlendWidth), texIndex);
        /// Compute corner blend
        color = CornerBlend(color, xyOffset, horizontalTexCoord, verticalTexCoord, sharedTexCoord);
    }
    // Blend top-right
    else if (uv.x > 1-_PatternBlendWidth && uv.y > 1-_PatternBlendWidth)
    {
        // Compute uv offset
        float2 xyOffset = float2(uv.x-(1-_PatternBlendWidth), uv.y-(1-_PatternBlendWidth));
        float2 horizontalTexCoord = ActualTexCoord(float2(xyOffset.x, buv.y), texIndex);
        float2 verticalTexCoord = ActualTexCoord(float2(buv.x, xyOffset.y), texIndex);
        float2 sharedTexCoord = ActualTexCoord(xyOffset*(1-2*_PatternBlendWidth), texIndex);
        // Compute corner blend
        color = CornerBlend(color, xyOffset, horizontalTexCoord, verticalTexCoord, sharedTexCoord);
    }
    // Blend left
    else if (uv.x <= _PatternBlendWidth)
    {
        float xOffset = _PatternBlendWidth - uv.x;
        float2 texCoord = ActualTexCoord(float2(1-xOffset, buv.y), texIndex);
        float blend = 0.5*xOffset/_PatternBlendWidth;
        color = lerp(color, tex2D(_Pattern, texCoord), blend);
    }
    // Blend bottom
    else if (uv.y <= _PatternBlendWidth)
    {
        float yOffset = _PatternBlendWidth - uv.y;
        float2 texCoord = ActualTexCoord(float2(buv.x, 1-yOffset), texIndex);
        float blend = 0.5*yOffset/_PatternBlendWidth;
        color = lerp(color, tex2D(_Pattern, texCoord), blend);
    }
    // Blend right
    else if (uv.x >= 1-_PatternBlendWidth)
    {
        float xOffset = uv.x-(1-_PatternBlendWidth);
        float2 texCoord = ActualTexCoord(float2(xOffset, buv.y), texIndex);
        float blend = 0.5*xOffset/_PatternBlendWidth;
        color = lerp(color, tex2D(_Pattern, texCoord), blend);
    }
    // Blend top
    else if (uv.y >= 1-_PatternBlendWidth)
    {
        float yOffset = uv.y-(1-_PatternBlendWidth);
        float2 texCoord = ActualTexCoord(float2(buv.x, yOffset), texIndex);
        float blend = 0.5*yOffset/_PatternBlendWidth;
        color = lerp(color, tex2D(_Pattern, texCoord), blend);
    }

    // Return blended color
    return color;
}

// Vertex shader - simply compute positions and normals
void ABRSurfaceVertex(inout appdata_full v, out Input o) {
    UNITY_INITIALIZE_OUTPUT(Input, o);
    o.normal = abs(v.normal);  // Ensures weights in triplanar mapping are positive
    o.position = (v.vertex);
}

// Main calculations for a colormapped, texture-mapped surface
float4 CalculateABRTexturedSurfaceColor(Input IN)
{
    uint SupportedChannels = 4u;

    float3 poscoodinates = IN.position.xyz;

    // Variables: color variable, pattern variable, null, null
    fixed4 variables = IN.color;

    // Find number of "grouped" blend map textures
    uint numGroups = _NumTex / SupportedChannels + 1;
    float groupSize = 1.0 / numGroups;
    float groupOffset = 0.5 * groupSize;

    // DEBUG: ensure position coordinates are correct
    // return poscoodinates;

    // DEBUG: Check actual data values
    // return variables / 10;

    // DEBUG: Check that texture coordinates exist
    // return float4(IN.uv_MainTex, 0.0, 1.0);

    // Compute UV coordinates for tri-planar projection
    // Scale UVs to account for seam blending and compute blend UVs (buv)
    float3 normal = IN.normal.xyz;
    float2 uv0 = poscoodinates.yz;
    uv0.x /= _PatternScale;
    uv0.y /= _PatternScale;
    uv0 = frac(uv0);
    float2 buv0 = _PatternBlendWidth + uv0 * (1-2*_PatternBlendWidth);

    float2 uv1 = poscoodinates.xz;
    uv1.x /= _PatternScale;
    uv1.y /= _PatternScale;
    uv1 = frac(uv1);
    float2 buv1 = _PatternBlendWidth + uv1 * (1-2*_PatternBlendWidth);

    float2 uv2 = poscoodinates.xy;
    uv2.x /= _PatternScale;
    uv2.y /= _PatternScale;
    uv2 = frac(uv2);
    float2 buv2 = _PatternBlendWidth + uv2 * (1-2*_PatternBlendWidth);

    // DEBUG: UV coords
    // return fixed4(uv0, 0, 1);
    // return fixed4(uv1, 0, 1);
    // return fixed4(uv2, 0, 1);

    float a = normal.x;
    float b = normal.y;
    float c = normal.z;

    float sum = a + b + c;
    a /= sum;
    b /= sum;
    c /= sum;

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

    // Final pixel color is computed in "groups" of 4 textures (according to the blendmap)
    // support up to 16 textures (4 groups)
    float blendPercentages[16];
    for (uint i = 0; i < 16; i++) {
        blendPercentages[i] = 0.0;
    }

    // Aggregate all blend percentages for each group
    float blendMapX = clamp(Remap(variables.y, _PatternDataMin, _PatternDataMax, 0, 1), 0.001, 0.999);
    for (int group = 0; group < (int) numGroups; group++) {
        float blendMapY = group * groupSize + groupOffset;
        float4 blendPercentageGroup = tex2D(_BlendMaps, float2(blendMapX, blendMapY));
        int index = group * SupportedChannels;
        blendPercentages[index + 0] += blendPercentageGroup.r;
        blendPercentages[index + 1] += blendPercentageGroup.g;
        blendPercentages[index + 2] += blendPercentageGroup.b;
        blendPercentages[index + 3] += blendPercentageGroup.a;
    }

    float3 textureColor = 0;
    float3 norm = 0;
    for (uint texIndex = 0u; texIndex < _NumTex; texIndex++)
    {
        // Only sample texture if texture is visible
        if (blendPercentages[texIndex] > 0)
        {
            float3 colorA = float3(0,0,0);
            float3 colorB = float3(0,0,0);
            float3 colorC = float3(0,0,0);
            float3 normalA;
            float3 normalB;
            float3 normalC;

            // Compute colors / normals for tri-planar projection
            colorA = tex2D(_Pattern, ActualTexCoord(buv0, texIndex));
            colorA =  SeamBlend(colorA, uv0, buv0, texIndex);

            colorB = tex2D(_Pattern, ActualTexCoord(buv1, texIndex));
            colorB = SeamBlend(colorB, uv1, buv1, texIndex);

            colorC = tex2D(_Pattern, ActualTexCoord(buv2, texIndex));
            colorC = SeamBlend(colorC, uv2, buv2, texIndex);

            normalA = UnpackNormal(tex2D(_PatternNormal, uv0));
            normalB = UnpackNormal(tex2D(_PatternNormal, uv1));
            normalC = UnpackNormal(tex2D(_PatternNormal, uv2));

            float3 currentColor = colorA * a + colorB * b + colorC * c;
            norm = normalA * a + normalB * b + normalC * c;
            norm = normalize(norm);

            currentColor *= blendPercentages[texIndex];
            textureColor += currentColor;
        }
    }

    // Compute saturation
    float3 grayTextureColor = dot(textureColor, float3(0.3, 0.59, 0.11));
    textureColor = lerp(grayTextureColor, textureColor, _PatternSaturation);

    // Compute intensity
    textureColor = lerp(1, textureColor, _PatternIntensity);

    // Get color / pattern variable data at at this fragment
    float vColor = variables.r;
    float vPattern = variables.g;
    float vColorNorm = clamp(Remap(vColor, _ColorDataMin,_ColorDataMax,0,1),0.01,0.99);

    float3 finalColor = 0;
    // Apply colormap
    if (_UseColorMap == 1)
    {
        float3 colorMapColor = tex2D(_ColorMap, float2(vColorNorm, 0.5));
        finalColor = colorMapColor;
    }
    else
    {
        finalColor = _Color.rgb;
    }

    // Use Multiply method (could use overlay instead)
    if (_NumTex > 0) {
        finalColor = finalColor * textureColor;
        // o.Albedo = textureColor;
        //o.Albedo = overlayBlend(o.Albedo, textureColor);
    }

    float4 finalColorWithAlpha = float4(finalColor.rgb, _Color.a);
    return finalColorWithAlpha;
}