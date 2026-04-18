// Phase 188.1.1 AVA-02-3 — per (tile, gaussian) overlap test.
// 1:1 port of CpuGaussianRasterizer.cs:148-164 tile/gaussian bbox early-out.
// Writes a packed (tileIdx, gaussianIdx) pair into the tile index buffer;
// an atomic per-tile counter keeps per-tile lists dense. We parallelize on
// the gaussian index and iterate tiles inside the shader (tile count is
// small: 512x512 / 16x16 = 1024 tiles).

#define TILE_SIZE    16
#define TILE_RADIUS  (TILE_SIZE * 0.707f)
#define MAX_PER_TILE 512u

cbuffer FrameConstants : register(b0)
{
    uint g_gaussianCount;
    uint g_tilesX;
    uint g_tilesY;
    uint g_frameWidth;
    uint g_frameHeight;
    uint _pad0;
    uint _pad1;
    uint _pad2;
};

StructuredBuffer<float4> g_projected : register(t0); // xy=pos, z=sigma, w=opacity

RWStructuredBuffer<uint> g_tileCounts : register(u0); // length = tilesX*tilesY
RWStructuredBuffer<uint> g_tileLists  : register(u1); // length = tilesX*tilesY*MAX_PER_TILE

[numthreads(64, 1, 1)]
void CSMain(uint3 dtid : SV_DispatchThreadID)
{
    uint g = dtid.x;
    if (g >= g_gaussianCount) return;

    float4 proj = g_projected[g];
    float gx = proj.x;
    float gy = proj.y;
    float sigma = proj.z;
    float cutoff = sigma * 3.0f;
    if (proj.w < 1e-6f) return; // opacity near-zero gaussians skip

    for (uint ty = 0u; ty < g_tilesY; ++ty)
    {
        float y0 = (float)(ty * TILE_SIZE);
        float y1 = min(y0 + (float)TILE_SIZE, (float)g_frameHeight);
        float tileCy = (y0 + y1) * 0.5f;
        if (abs(gy - tileCy) > cutoff + TILE_RADIUS) continue;

        for (uint tx = 0u; tx < g_tilesX; ++tx)
        {
            float x0 = (float)(tx * TILE_SIZE);
            float x1 = min(x0 + (float)TILE_SIZE, (float)g_frameWidth);
            float tileCx = (x0 + x1) * 0.5f;
            if (abs(gx - tileCx) > cutoff + TILE_RADIUS) continue;

            uint tileIdx = ty * g_tilesX + tx;
            uint slot;
            InterlockedAdd(g_tileCounts[tileIdx], 1u, slot);
            if (slot < MAX_PER_TILE)
            {
                g_tileLists[tileIdx * MAX_PER_TILE + slot] = g;
            }
        }
    }
}
