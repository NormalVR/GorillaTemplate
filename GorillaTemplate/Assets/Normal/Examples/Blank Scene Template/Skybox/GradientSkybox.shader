Shader "Normal/Examples/Gradient Skybox" {
	Properties {
		[Header(Skybox Colors)][Space]
		_ColorT ("Top Color", Color) = (1,1,1,1)
		_ColorM ("Middle Color", Color) = (1,1,1,1)
		_ColorB ("Bottom Color", Color) = (1,1,1,1)
		
		[Header(Skybox Settings)][Space]
		_ExponentT ("Upper Exponent", Float) = 1.0
		_ExponentB ("Lower Exponent", Float) = 1.0
		_Intensity ("Intensity", Float) = 1.0
		[Toggle(DITHER)] _Dither ("Add Screenspace Dither", Float) = 0
	}
	SubShader {
		Tags { "RenderType"="Background" "Queue"="Background" "DisableBatching"="True" "IgnoreProjector"="True" "PreviewType"="Skybox" }
		Fog { Mode Off }
		ZWrite Off
		Cull Back

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma shader_feature DITHER
			#pragma fragmentoption ARB_precision_hint_fastest
			#include "UnityCG.cginc"
			#include "GradientSkybox.cginc"

			half3 _ColorT;
			half3 _ColorM;
			half3 _ColorB;
			half _ExponentT;
			half _ExponentB;
			half _Intensity;

			struct appdata {
				float4 position : POSITION;
				float3 texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct varyings {
				float4 position : SV_POSITION;
				float3 texcoord : TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			varyings vert(appdata v) {
				varyings o;

				UNITY_SETUP_INSTANCE_ID(v)
				UNITY_INITIALIZE_OUTPUT(varyings, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				o.position = UnityObjectToClipPos(v.position);
				o.texcoord = v.texcoord;

				return o;
			}
			
			fixed4 frag(varyings i) : SV_Target {
				GradientSkyboxSettings settings;

				settings.ColorT = _ColorT;
				settings.ColorM = _ColorM;
				settings.ColorB = _ColorB;
				
				settings.ExponentT = _ExponentT;
				settings.ExponentB = _ExponentB;
				settings.Intensity = _Intensity;

				fixed3 color = GradientSkybox(settings, i.texcoord);
				
				#if DITHER
				color += ScreenSpaceDither(i.position.xy);
				#endif
				
				return fixed4(color, 1);
			}
			ENDCG
		}
	}
}
