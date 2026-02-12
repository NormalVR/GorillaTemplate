#ifndef NORMAL_GRADIENT_SKYBOX
#define NORMAL_GRADIENT_SKYBOX

struct GradientSkyboxSettings {
	half3 ColorT;
	half3 ColorM;
	half3 ColorB;
	
	half ExponentT;
	half ExponentB;
	half Intensity;
};

half3 ScreenSpaceDither(float2 screenpos)
{
	float3 dither = dot(float2(171.0, 231.0), screenpos + _Time.yy).xxx;
	dither.rgb = frac(dither / float3(103.0, 71.0, 97.0)) - float3(0.5, 0.5, 0.5);
	return (dither / 255.0);
}

half3 GradientSkybox(GradientSkyboxSettings settings, float3 dir) {
	float3 n = normalize(dir);
	
	float factorT = 1.0 - pow(abs(min(1.0, 1.0 - n.y)), settings.ExponentT);
	float factorB = 1.0 - pow(abs(min(1.0, 1.0 + n.y)), settings.ExponentB);
	float factorM = 1.0 - factorT - factorB;
	
	return (settings.ColorT * factorT + settings.ColorM * factorM + settings.ColorB * factorB) * settings.Intensity;
}

// Can be used in a Custom Function node in Shader Graph
void GradientSkybox_SRP_Wrapper_float(in half3 colorT, in half3 colorM, in half3 colorB, in half exponentT, in half exponentB, in half intensity, in float3 dir, out half3 color) {
	GradientSkyboxSettings settings;
	
	settings.ColorT = colorT;
	settings.ColorM = colorM;
	settings.ColorB = colorB;
	
	settings.ExponentT = exponentT;
	settings.ExponentB = exponentB;
	settings.Intensity = intensity;
	
	color = GradientSkybox(settings, dir);
}

#endif
