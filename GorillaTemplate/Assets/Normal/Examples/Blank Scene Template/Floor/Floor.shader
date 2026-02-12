Shader "Normal/Examples/Floor" {
    Properties {
        [Header(Floor Settings)][Space]
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _Color ("Color", Color) = (0.5, 0.5, 0.5)

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
        Tags { "IgnoreProjector" = "True" "RenderType" = "Opaque" }
        
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert finalcolor:color addshadow fullforwardshadows
		#pragma shader_feature DITHER
        #include "../Skybox/GradientSkybox.cginc"

        sampler2D _MainTex;
        half3 _Color;

        half3 _ColorT;
        half3 _ColorM;
        half3 _ColorB;
        half _ExponentT;
        half _ExponentB;
        half _Intensity;

        struct Input {
            float2 uv_MainTex;
            float3 viewDir;
            float3 objectPos;
            float4 screenPos;
        };

        void vert(inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objectPos = v.vertex.xyz;
        }
        
        void surf(Input i, inout SurfaceOutput o) {
            o.Albedo = _Color;
        }

        void color(Input i, SurfaceOutput o, inout fixed4 color) {
            GradientSkyboxSettings settings;

            settings.ColorT = _ColorT;
            settings.ColorM = _ColorM;
            settings.ColorB = _ColorB;

            settings.ExponentT = _ExponentT;
            settings.ExponentB = _ExponentB;
            settings.Intensity = _Intensity;

            fixed3 grid = tex2D(_MainTex, i.uv_MainTex).rgb;
            fixed3 skybox = GradientSkybox(settings, -i.viewDir);

            #if DITHER
			skybox += ScreenSpaceDither(i.screenPos.xy);
            #endif

            float coord = saturate(length(i.objectPos.xz * 2));

            color.rgb = lerp(saturate(color.rgb) * grid, skybox, coord);
        }
        ENDCG
    }
}