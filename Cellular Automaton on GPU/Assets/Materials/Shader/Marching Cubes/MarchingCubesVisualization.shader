﻿Shader "Unlit/MarchingCubesVisualization"
{
	Properties
	{
		//Part of the wtare-shader from the standard assets
		_horizonColor("Horizon color", COLOR) = (.172 , .463 , .435 , 0)
		_WaveScale("Wave scale", Range(0.02,0.15)) = .07
		[NoScaleOffset] _ColorControl("Reflective color (RGB) fresnel (A) ", 2D) = "" { }
		[NoScaleOffset] _BumpMap("Waves Normalmap ", 2D) = "" { }
		WaveSpeed("Wave speed (map1 x,y; map2 x,y)", Vector) = (19,9,-16,-7)
		///////
		_MainTex("Texture", 3D) = "white" {}
	}

	SubShader
	{
		Tags{ "LightMode" = "ForwardBase" "RenderType" = "Transparent" "Queue" = "Transparent" }
		LOD 100

		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#include "UnityLightingCommon.cginc"
			#pragma target 5.0
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag

			//Thats the ouput type of the Marching Cubes-algorithm Compute Shader
			struct Triangle
			{
				half3 vertex[3];
			};

			//Thats the ouput of the Marching Cubes-algorithm Compute Shader
			StructuredBuffer<Triangle> triangles;

			#include "UnityCG.cginc"

			uniform float4 _horizonColor;

			uniform float4 WaveSpeed;
			uniform float _WaveScale;
			uniform float4 _WaveOffset;

			struct VS_INPUT
			{
				uint vertexid : SV_VertexID;
			};

			struct GS_INPUT
			{
				float4 positions[3] : POSITION;
			};

			struct PS_INPUT
			{
				float4 position : SV_POSITION;
				float light : BLENDINDICES;
				float3 uv : TEXCOORDS;
				float2 bumpuv[2] : TEXCOORD0;
				float3 viewDir : TEXCOORD2;
			};

			sampler2D _BumpMap;
			sampler2D _ColorControl;
			sampler3D _MainTex;

			float4 scale;

			GS_INPUT vert(VS_INPUT input)
			{
				GS_INPUT o;
				o.positions[0] = float4(triangles[input.vertexid].vertex[0], 1);
				o.positions[1] = float4(triangles[input.vertexid].vertex[1], 1);
				o.positions[2] = float4(triangles[input.vertexid].vertex[2], 1);
				return o;
			}

			[maxvertexcount(3)]
			void geom(point GS_INPUT p[1], inout TriangleStream<PS_INPUT> triStream)
			{
				PS_INPUT pIn = (PS_INPUT)0;

				fixed3 normal = cross(p[0].positions[2] - p[0].positions[1], p[0].positions[0] - p[0].positions[1]);
				normal = UnityObjectToWorldNormal(-normal);
				fixed NdotL = max(0, dot(normal, _WorldSpaceLightPos0.xyz));
				fixed light = _LightColor0 * NdotL;

				fixed4 temp, wpos;

				//First point
				pIn.position = UnityObjectToClipPos(p[0].positions[2] * scale);
				pIn.light = light;
				pIn.uv = p[0].positions[2];

				wpos = mul(unity_ObjectToWorld, p[0].positions[2] * scale);
				temp.xyzw = wpos.xzxz * _WaveScale + _WaveOffset;
				pIn.bumpuv[0] = temp.xy * fixed2(.4, .45);
				pIn.bumpuv[1] = temp.wz;
				pIn.viewDir.xzy = normalize(WorldSpaceViewDir(p[0].positions[2] * scale));

				triStream.Append(pIn);

				//Second point
				pIn.position = UnityObjectToClipPos(p[0].positions[1] * scale);
				pIn.light = light;
				pIn.uv = p[0].positions[1];

				wpos = mul(unity_ObjectToWorld, p[0].positions[1] * scale);
				temp.xyzw = wpos.xzxz * _WaveScale + _WaveOffset;
				pIn.bumpuv[0] = temp.xy * fixed2(.4, .45);
				pIn.bumpuv[1] = temp.wz;
				pIn.viewDir.xzy = normalize(WorldSpaceViewDir(p[0].positions[1] * scale));

				triStream.Append(pIn);

				//Thirs point
				pIn.position = UnityObjectToClipPos(p[0].positions[0] * scale);
				pIn.light = light;
				pIn.uv = p[0].positions[0];

				wpos = mul(unity_ObjectToWorld, p[0].positions[0] * scale);
				temp.xyzw = wpos.xzxz * _WaveScale + _WaveOffset;
				pIn.bumpuv[0] = temp.xy * fixed2(.4, .45);
				pIn.bumpuv[1] = temp.wz;
				pIn.viewDir.xzy = normalize(WorldSpaceViewDir(p[0].positions[0] * scale));

				triStream.Append(pIn);

				triStream.RestartStrip();
			}

			fixed4 frag(PS_INPUT input) : SV_Target
			{
				fixed3 bump1 = UnpackNormal(tex2D(_BumpMap, input.bumpuv[0])).rgb;
				fixed3 bump2 = UnpackNormal(tex2D(_BumpMap, input.bumpuv[1])).rgb;
				fixed3 bump = (bump1 + bump2) * 0.5;

				fixed fresnel = dot(input.viewDir, bump);
				fixed4 water = tex2D(_ColorControl, fixed2(fresnel,fresnel));

				fixed4 col;
				col.rgb = tex3D(_MainTex, input.uv).xyz * lerp(water.rgb, _horizonColor.rgb, water.a);
				col.a = _horizonColor.a;

				return col;
			}
			ENDCG
		}
	}
}
