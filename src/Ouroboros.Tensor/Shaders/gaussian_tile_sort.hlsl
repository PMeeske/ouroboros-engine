// Phase 188.1.1 AVA-02-3 — per-tile depth sort (STUB / passthrough).
// Orthographic + degree-0 direct-RGB → depth sorting is a NO-OP for this
// phase. The CPU reference (CpuGaussianRasterizer.RasterizeTiled) does not
// sort either; the tile-raster pass is associative under the weighted-
// average accumulation rule (not a front-to-back alpha-over).
//
// Shipped as a stub so the Plan 03 dispatch pipeline has all four stages.
// A future perspective-projection extension will replace this body with
// a real bitonic or merge sort. The shader is dispatched once per tile
// with 1 thread — effectively a syncpoint between tile-assign and
// tile-raster. Empty body is intentional.

[numthreads(1, 1, 1)]
void CSMain(uint3 dtid : SV_DispatchThreadID)
{
    // No-op — see header comment.
}
