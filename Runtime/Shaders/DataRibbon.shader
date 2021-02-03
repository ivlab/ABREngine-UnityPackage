Shader "ABR/DataTexturedRibbon"
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
			sampler2D _ColorMap;
			sampler2D _Texture;
			float _TextureAspect;
			sampler2D _TextureNRM;
			float _TextureCutoff;
			int _UseColorMap = false;
			float _ColorDataMin;
			float _ColorDataMax;
			float _RibbonBrightness;
			struct Input
			{
				float4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				float2 ScreenPos : TEXCOORD2;
			};
			half _Glossiness;
			half _Metallic;
			fixed4 _Color;
			float _Texturescale = 0.5;
			float _Blend = 1;
			int _UseLineTexture = 0;
			// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
			// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
			// #pragma instancing_options assumeuniformscaling
			UNITY_INSTANCING_BUFFER_START(Props)
				// put more per-instance properties here
			UNITY_INSTANCING_BUFFER_END(Props)
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
			void vert(inout appdata_full v, out Input o) {
				UNITY_INITIALIZE_OUTPUT(Input, o);
				o.texcoord.xy = abs(v.texcoord.xy);
				o.ScreenPos = ComputeScreenPos(v.vertex);
			}
			void surf(Input IN, inout SurfaceOutput  o)
			{
				_Texturescale = 0.5;
				float2 uv = float2((IN.texcoord.x / _TextureAspect) % 1, IN.texcoord.y);
				// Albedo comes from a texture tinted by color
				fixed4 variables = IN.color;
				float3 vColor = variables.rgb;
				float vColorNorm = clamp(Remap(vColor, _ColorDataMin,_ColorDataMax,0,1),0.01,0.99);
				if (_UseColorMap == 1)
				{
					float3 colorMapColor = tex2D(_ColorMap, float2(vColorNorm, 0.5));
					o.Albedo = colorMapColor;
				}
				else
				{
					o.Albedo =  _Color.rgb;
				}
				// Metallic and smoothness come from slider variables
				if (_UseLineTexture && tex2D(_Texture, uv).r > _TextureCutoff) {
					discard;
				}
				o.Alpha = 1.0;
			}
			ENDCG
		}
			FallBack "Diffuse"
}