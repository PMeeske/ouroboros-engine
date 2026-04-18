// Phase 188.1.1 AVA-02-3 — per-pixel per-tile alpha-blend accumulation + normalize.
// 1:1 port of CpuGaussianRasterizer.cs:172-189 (accumulate) + 103-115 (normalize).
// One thread per pixel per tile; thread group = tile (16x16). Output is RGBA8
// unorm (same byte layout as CpuGaussianRasterizer.rgba — row-major, width*4
// per row, alpha=255 when weightSum>1e-6 else 0).

#define TILE_SIZE    16
#define MAX_PER_TILE 512u

cbuffer FrameConstants : register(b0)
{
    uint g_tilesX;
    uint g_tilesY;
    uint g_frameWidth;
    uint g_frameHeight;
    uint _pad0;
    uint _pad1;
    uint _pad2;
    uint _pad3;
};

StructuredBuffer<float4> g_projected  : register(t0); // xy,sigma,opacity
StructuredBuffer<float4> g_projColors : register(t1); // rgb + pad
StructuredBuffer<uint>   g_tileCounts : register(t2);
StructuredBuffer<uint>   g_tileLists  : register(t3);

RWTexture2D<unorm float4> g_output : register(u0);

[numthreads(TILE_SIZE, TILE_SIZE, 1)]
void CSMain(uint3 gid : SV_GroupID, uint3 tid : SV_GroupThreadID, uint3 dtid : SV_DispatchThreadID)
{
    uint px = dtid.x;
    uint py = dtid.y;
    if (px >= g_frameWidth || py >= g_frameHeight) return;

    uint tileIdx  = gid.y * g_tilesX + gid.x;
    uint count    = min(g_tileCounts[tileIdx], MAX_PER_TILE);
    uint listBase = tileIdx * MAX_PER_TILE;

    float3 accum = float3(0.0f, 0.0f, 0.0f);
    float  weightSum = 0.0f;

    for (uint i = 0u; i < count; ++i)
    {
        uint   g = g_tileLists[listBase + i];
        float4 proj = g_projected[g];
        float3 col  = g_projColors[g].rgb;

        float gx = proj.x;
        float gy = proj.y;
        float sigma = proj.z;
        float opacity = proj.w;

        float dx = (float)px - gx;
        float dy = (float)py - gy;
        float dist2 = dx * dx + dy * dy;
        float invSigma2 = 1.0f / (2.0f * sigma * sigma);
        float gauss = exp(-dist2 * invSigma2) * opacity;
        if (gauss < 1e-6f) continue;

        weightSum += gauss;
        accum += gauss * col;
    }

    float w = max(weightSum, 1e-6f);
    float r = saturate(accum.r / w);
    float gChan = saturate(accum.g / w);
    float b = saturate(accum.b / w);
    float a = weightSum > 1e-6f ? 1.0f : 0.0f;
    g_output[int2(px, py)] = float4(r, gChan, b, a);
}
