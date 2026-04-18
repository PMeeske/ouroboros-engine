// Phase 188.1.1 AVA-02-3 — per-gaussian orthographic projection.
// 1:1 port of CpuGaussianRasterizer.cs:148-158 (tx/ty translation, sigma
// formula). Inputs are the raw GaussianSet arrays (scales/opacities are
// pre-activated by the caller — the training pipeline's exports store
// already-activated values on disk). Outputs a per-gaussian screen-space
// descriptor (gx, gy, sigma, opacity) + color for the tile-assign and
// tile-raster passes to consume.

cbuffer FrameConstants : register(b0)
{
    float g_translateX;    // view matrix [12]
    float g_translateY;    // view matrix [13]
    uint  g_gaussianCount;
    uint  g_frameWidth;
    uint  g_frameHeight;
    uint  _pad0;
    uint  _pad1;
    uint  _pad2;
};

StructuredBuffer<float3> g_positions : register(t0);
StructuredBuffer<float3> g_scales    : register(t1);
StructuredBuffer<float>  g_opacities : register(t2);
StructuredBuffer<float3> g_colors    : register(t3);

RWStructuredBuffer<float4> g_projected  : register(u0); // xy=pos, z=sigma, w=opacity
RWStructuredBuffer<float4> g_projColors : register(u1); // rgb + pad

[numthreads(64, 1, 1)]
void CSMain(uint3 dtid : SV_DispatchThreadID)
{
    uint g = dtid.x;
    if (g >= g_gaussianCount) return;

    float3 pos = g_positions[g];
    float3 scl = g_scales[g];
    float  op  = g_opacities[g];
    float3 col = g_colors[g];

    float gx = pos.x + g_translateX;
    float gy = pos.y + g_translateY;
    float s  = (scl.x + scl.y + scl.z) / 3.0f;
    float sigma = max(0.5f, min(s, 30.0f) / 2.0f);

    g_projected[g]  = float4(gx, gy, sigma, op);
    g_projColors[g] = float4(col, 0.0f);
}
