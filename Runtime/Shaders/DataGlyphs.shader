Shader "Instanced/InstancedSurfaceShader" {
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
			// Physically based Standard lighting model
			#pragma surface surf Standard addshadow fullforwardshadows
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup

			sampler2D _MainTex;
		sampler2D _Normal;
			float4x4 _ObjectTransform;
			float4x4 _ObjectTransformInverse;

			struct Input {
				float2 uv_MainTex;
			};

		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			StructuredBuffer<float4> colorBuffer;
			StructuredBuffer<float4x4> transformBuffer;
			StructuredBuffer<float4x4> transformBufferInverse;

		#endif

			void rotate2D(inout float2 v, float r)
			{
				float s, c;
				sincos(r, s, c);
				v = float2(v.x * c - v.y * s, v.x * s + v.y * c);
			}

			void setup()
			{
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
				//float4 data = positionBuffer[unity_InstanceID];
				float4x4 matData = transformBuffer[unity_InstanceID];
				float4x4 matDataInverse = transformBufferInverse[unity_InstanceID];


				unity_ObjectToWorld = matData;

				unity_WorldToObject = matDataInverse;

				unity_ObjectToWorld = mul(_ObjectTransform, unity_ObjectToWorld);
				unity_WorldToObject = mul(unity_WorldToObject, _ObjectTransformInverse );
			#endif
			}
			float Remap(float dataValue, float from0, float to0, float from1, float to1)
			{
				return from1 + (dataValue - from0) * (to1 - from1) / (to0 - from0);
			}

			half _Glossiness;
			half _Metallic;
			float4 _Color;
			int _UseColorMap;
			sampler2D _ColorMap;
			float _ColorDataMin;
			float _ColorDataMax;
			void surf(Input IN, inout SurfaceOutputStandard o) {
				fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
				o.Albedo = _Color;

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

				c = colorBuffer[unity_InstanceID];
				float vColor = c.r;

				float vColorNorm = clamp(Remap(vColor, _ColorDataMin, _ColorDataMax, 0, 1), 0, 0.99);

				if (_UseColorMap == 1)
				{
					o.Albedo =  tex2D(_ColorMap, float2(vColorNorm,0.25 ));
				}
				else
				{
					o.Albedo = _Color.rgb;
				}

#endif

				o.Metallic = 0;
				o.Smoothness = 0.0;
				float4 map1 = tex2D(_Normal, IN.uv_MainTex);
				//map1.a = sqrt(map1.r) * 2 - 1;
				//map1.a = map1.a * 0.5 + 0.54;
				//map1.g = sqrt(map1.g) * 2 - 1;
				//map1.g = map1.g * 0.5 + 0.54;
				//
				map1.b = map1.g;
				//map1.r = 1;
				o.Normal = UnpackNormal(map1);
				o.Alpha = 1;



			}
			ENDCG
		}
			FallBack "Diffuse"
}