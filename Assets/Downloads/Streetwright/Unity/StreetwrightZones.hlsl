// Streetwright kit zones for Unity Shader Graph (URP and HDRP): a Custom Function node, File mode, function name
// "StreetwrightZones" (precision Float). Pieces imported from the .glb files (glTFast) or FBX carry the zone in the
// vertex colour ALPHA: 1.0 = A, 0.667 = B, 0.333 = C, 0.0 = D. Each zone has a tint and one tiling surface from
// Textures/ (BaseColor sRGB, Normal as "Normal map" texture type, Roughness with sRGB off), sampled on UV0 where
// 1 UV = 1 m; Tile* = the size one image covers, in metres (textures.json tile_cm / 100).
//
// Graph: Vertex Color -> VertexColor, UV (UV0) -> UV, the twelve textures, four tints, four tiles, a Sampler State
// (Linear, Repeat) -> SS. Outputs: BaseColor -> Base Color, NormalTS -> Normal (Tangent Space), Smoothness ->
// Smoothness (URP Lit / HDRP Lit). Set Metallic 0. See README "Unity" for the full steps.

void StreetwrightZones_float(float4 VertexColor, float2 UV,
    UnityTexture2D BaseA, UnityTexture2D BaseB, UnityTexture2D BaseC, UnityTexture2D BaseD,
    UnityTexture2D NormalA, UnityTexture2D NormalB, UnityTexture2D NormalC, UnityTexture2D NormalD,
    UnityTexture2D RoughA, UnityTexture2D RoughB, UnityTexture2D RoughC, UnityTexture2D RoughD,
    float4 TintA, float4 TintB, float4 TintC, float4 TintD,
    float2 TileA, float2 TileB, float2 TileC, float2 TileD,
    float NormalStrength, UnitySamplerState SS,
    out float3 BaseColor, out float3 NormalTS, out float Smoothness)
{
    float zone = round((1.0 - VertexColor.a) * 3.0);
    float4 w = float4(zone < 0.5, abs(zone - 1.0) < 0.5, abs(zone - 2.0) < 0.5, zone > 2.5);
    float2 ua = UV / TileA, ub = UV / TileB, uc = UV / TileC, ud = UV / TileD;

    float3 tint = TintA.rgb * w.x + TintB.rgb * w.y + TintC.rgb * w.z + TintD.rgb * w.w;
    float3 baseCol = SAMPLE_TEXTURE2D(BaseA.tex, SS.samplerstate, ua).rgb * w.x
                   + SAMPLE_TEXTURE2D(BaseB.tex, SS.samplerstate, ub).rgb * w.y
                   + SAMPLE_TEXTURE2D(BaseC.tex, SS.samplerstate, uc).rgb * w.z
                   + SAMPLE_TEXTURE2D(BaseD.tex, SS.samplerstate, ud).rgb * w.w;
    BaseColor = baseCol * tint;

    float rough = SAMPLE_TEXTURE2D(RoughA.tex, SS.samplerstate, ua).r * w.x
                + SAMPLE_TEXTURE2D(RoughB.tex, SS.samplerstate, ub).r * w.y
                + SAMPLE_TEXTURE2D(RoughC.tex, SS.samplerstate, uc).r * w.z
                + SAMPLE_TEXTURE2D(RoughD.tex, SS.samplerstate, ud).r * w.w;
    Smoothness = 1.0 - rough;

    // Normal maps are OpenGL (green up), Unity's own convention: import them as "Normal map".
    float3 n = UnpackNormalScale(SAMPLE_TEXTURE2D(NormalA.tex, SS.samplerstate, ua), NormalStrength) * w.x
             + UnpackNormalScale(SAMPLE_TEXTURE2D(NormalB.tex, SS.samplerstate, ub), NormalStrength) * w.y
             + UnpackNormalScale(SAMPLE_TEXTURE2D(NormalC.tex, SS.samplerstate, uc), NormalStrength) * w.z
             + UnpackNormalScale(SAMPLE_TEXTURE2D(NormalD.tex, SS.samplerstate, ud), NormalStrength) * w.w;
    NormalTS = normalize(n);
}

void StreetwrightZones_half(float4 VertexColor, float2 UV,
    UnityTexture2D BaseA, UnityTexture2D BaseB, UnityTexture2D BaseC, UnityTexture2D BaseD,
    UnityTexture2D NormalA, UnityTexture2D NormalB, UnityTexture2D NormalC, UnityTexture2D NormalD,
    UnityTexture2D RoughA, UnityTexture2D RoughB, UnityTexture2D RoughC, UnityTexture2D RoughD,
    float4 TintA, float4 TintB, float4 TintC, float4 TintD,
    float2 TileA, float2 TileB, float2 TileC, float2 TileD,
    float NormalStrength, UnitySamplerState SS,
    out float3 BaseColor, out float3 NormalTS, out float Smoothness)
{
    StreetwrightZones_float(VertexColor, UV, BaseA, BaseB, BaseC, BaseD, NormalA, NormalB, NormalC, NormalD,
        RoughA, RoughB, RoughC, RoughD, TintA, TintB, TintC, TintD, TileA, TileB, TileC, TileD,
        NormalStrength, SS, BaseColor, NormalTS, Smoothness);
}
