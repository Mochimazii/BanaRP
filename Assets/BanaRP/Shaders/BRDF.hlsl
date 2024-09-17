#ifndef BANA_BRDF_INCLUDED
#define BANA_BRDF_INCLUDED

// D 法线分布函数
float Trowbridge_Reitz_GGX(float NdotH, float a)
{
    float a2 = a * a;
    float NdotH2 = NdotH * NdotH;

    float num = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return num / denom;
}

// F 菲涅尔项
float3 Schlick_Fresnel(float HdotV, float3 F0)
{
    float m = saturate(1.0 - HdotV);
    return F0 + (1.0 - F0) * pow(m, 5.0);
}

// F_roughness 菲涅尔项近似,用作 irradiance IBL 中积分中提出系数 K_d = (1 - K_s) * (1 - metallic)
float3 Schlick_Fresnel_Roughness(float NdotV, float3 F0, float roughness)
{
    float r1 = 1.0f - roughness;
    return F0 + (max(float3(r1, r1, r1), F0) - F0) * pow(1 - NdotV, 5.0f);
}

// G 几何遮蔽函数
float Schlick_GGX(float NdotV, float k)
{
    float num = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return num / denom;
}

float Smith_Method(float3 N, float3 V, float3 L, float k)
{
    float NdotV = max(dot(N, V), 0);
    float NdotL = max(dot(N, L), 0);

    float GGX1 = Schlick_GGX(NdotV, k);         // 观察方向的几何遮蔽(Geometry Obstruction)
    float GGX2 = Schlick_GGX(NdotL, k);   // 光照方向的几何阴影(Geometry Shadowing)

    return GGX1 * GGX2;
}

float3 PBR(float3 N, float3 V, float3 L, float3 albedo, float3 radiance, float roughness, float metallic)
{
    roughness = max(roughness, 0.05); // 保证光滑物体也有高光

    float3 H = normalize(V + L);
    float NdotL = max(dot(N, L), 0);
    float NdotV = max(dot(N, V), 0);
    float NdotH = max(dot(N, H), 0);
    float HdotV = max(dot(H, V), 0);
    float alpha = roughness * roughness;
    float k = ((alpha+1) * (alpha+1)) / 8.0;
    // cook torrance brdf 中金属度越高, F0 越接近 albedo, 金属部分没有漫反射, 此时 albedo 表示金属的反射颜色
    // 反射率退化为 RGB 相同的 float 了
    float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);

    float D = Trowbridge_Reitz_GGX(NdotH, alpha);
    float3 F = Schlick_Fresnel(HdotV, F0);
    float G = Smith_Method(N, V, L, k);

    float3 k_s = F;
    float3 k_d = (1.0 - k_s) * (1.0 - metallic);
    float3 f_diffuse = albedo / PI;
    float3 f_specular = D * F * G / (4 * NdotL * NdotV + 0.001); // 避免出现除零错误

    // f_diffuse *= PI;
    // f_specular *= PI;
    //float3 ambient = float3(0.03, 0.03, 0.03) * albedo * ao;
    float3 Lo = (k_d * f_diffuse + f_specular) * radiance * NdotL;
    
    float3 color = Lo;

    return color;
}

float3 IBL(
    float3 N, float3 V,
    float3 albedo, float roughness, float metallic,
    samplerCUBE _irradianceIBL, samplerCUBE _prefilteredSpecIBL, sampler2D _brdfLUT)
{
    roughness = min(roughness, 0.99);

    //float3 H = normalize(V);
    float NdotV = max(dot(N, V), 0);
    //float HdotV = max(dot(H, V), 0);
    float3 R = normalize(reflect(-V, N));

    float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
    float3 F = Schlick_Fresnel_Roughness(NdotV, F0, roughness);
    float3 k_s = F;
    float3 k_d = (1.0 - k_s) * (1.0 - metallic);

    // diffuse
    float3 irradiance = texCUBE(_irradianceIBL, N).rgb;
    float3 diffuse = k_d * irradiance * albedo;

    // specular
    float mipmap_roughness = roughness * (1.7 - 0.7 * roughness);
    float lod = 6.0 * mipmap_roughness;
    float3 IBLspec = texCUBElod(_prefilteredSpecIBL, float4(R, lod)).rgb;
    float2 brdf = tex2D(_brdfLUT, float2(NdotV, roughness)).rg;
    float3 speclur = IBLspec * (F0 * brdf.x + brdf.y);

    float3 ambient = diffuse + speclur;

    return ambient;
}

#endif
