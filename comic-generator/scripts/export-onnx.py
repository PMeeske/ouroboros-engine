#!/usr/bin/env python3
"""export-onnx.py — Convert a Stable Diffusion checkpoint to ONNX format.

Optimized for DirectML inference on AMD GPUs (ASUS ROG Ally).

Usage:
    python export-onnx.py \
        --model_path D:\ComfyUI_windows_portable_amd\ComfyUI_windows_portable\ComfyUI\models\checkpoints\anything-v5 \
        --output_path D:\ComfyUI_windows_portable_amd\ComfyUI_windows_portable\ComfyUI\models\onnx\anything-v5 \
        --dtype float16

    python export-onnx.py \
        --model_path D:\ComfyUI_windows_portable_amd\ComfyUI_windows_portable\ComfyUI\models\checkpoints\anything-v5 \
        --output_path D:\ComfyUI_windows_portable_amd\ComfyUI_windows_portable\ComfyUI\models\onnx\anything-v5 \
        --dtype float32 \
        --opset 17
"""

import argparse
import os
import sys
from pathlib import Path


def parse_args():
    parser = argparse.ArgumentParser(
        description="Export Stable Diffusion model to ONNX for DirectML inference"
    )
    parser.add_argument(
        "--model_path",
        type=str,
        required=True,
        help="Path to the SD model directory or .safetensors file",
    )
    parser.add_argument(
        "--output_path",
        type=str,
        required=True,
        help="Output directory for ONNX model files",
    )
    parser.add_argument(
        "--dtype",
        type=str,
        choices=["float16", "float32"],
        default="float16",
        help="Data type for ONNX export (default: float16)",
    )
    parser.add_argument(
        "--opset",
        type=int,
        default=17,
        help="ONNX opset version (default: 17, max stable for DirectML)",
    )
    parser.add_argument(
        "--width",
        type=int,
        default=512,
        help="Target image width (default: 512)",
    )
    parser.add_argument(
        "--height",
        type=int,
        default=512,
        help="Target image height (default: 512)",
    )
    return parser.parse_args()


def check_dependencies():
    """Verify required packages are installed."""
    missing = []
    for pkg in ["torch", "diffusers", "optimum", "onnxruntime"]:
        try:
            __import__(pkg)
        except ImportError:
            missing.append(pkg)

    if missing:
        print(f"ERROR: Missing packages: {', '.join(missing)}")
        print("Install with:")
        print("  pip install torch diffusers optimum[onnxruntime] onnxruntime-directml")
        sys.exit(1)


def export_model(args):
    """Export the SD pipeline components to ONNX."""
    import torch
    from optimum.onnxruntime import ORTStableDiffusionPipeline

    output_path = Path(args.output_path)
    output_path.mkdir(parents=True, exist_ok=True)

    print(f"Loading model from: {args.model_path}")
    print(f"Output directory:   {output_path}")
    print(f"Data type:          {args.dtype}")
    print(f"ONNX opset:         {args.opset}")
    print(f"Target resolution:  {args.width}x{args.height}")
    print()

    # Determine torch dtype
    torch_dtype = torch.float16 if args.dtype == "float16" else torch.float32

    model_path = Path(args.model_path)

    # Check if input is a single .safetensors file or a directory
    if model_path.suffix == ".safetensors":
        # Load from single file using diffusers
        from diffusers import StableDiffusionPipeline

        print("Loading from .safetensors file...")
        pipeline = StableDiffusionPipeline.from_single_file(
            str(model_path),
            torch_dtype=torch_dtype,
        )
        # Save as diffusers format first, then convert
        diffusers_path = output_path / "_diffusers_tmp"
        print("Converting to diffusers format...")
        pipeline.save_pretrained(str(diffusers_path))
        source_path = str(diffusers_path)
    else:
        source_path = str(model_path)

    # Export to ONNX using optimum
    print("Exporting to ONNX (this may take several minutes)...")
    ort_pipeline = ORTStableDiffusionPipeline.from_pretrained(
        source_path,
        export=True,
    )
    ort_pipeline.save_pretrained(str(output_path))

    # Clean up temporary diffusers conversion if used
    if model_path.suffix == ".safetensors":
        import shutil

        diffusers_tmp = output_path / "_diffusers_tmp"
        if diffusers_tmp.exists():
            shutil.rmtree(str(diffusers_tmp))

    # Verify output
    onnx_files = list(output_path.rglob("*.onnx"))
    print()
    print(f"Export complete. Generated {len(onnx_files)} ONNX file(s):")
    total_size = 0
    for f in sorted(onnx_files):
        size_mb = f.stat().st_size / (1024 * 1024)
        total_size += size_mb
        print(f"  {f.relative_to(output_path)}: {size_mb:.1f} MB")
    print(f"  Total: {total_size:.1f} MB")

    # Verify with ONNX Runtime DirectML
    print()
    print("Verifying ONNX Runtime DirectML compatibility...")
    try:
        import onnxruntime as ort

        providers = ort.get_available_providers()
        if "DmlExecutionProvider" in providers:
            print("  DirectML provider available.")
        else:
            print("  WARNING: DmlExecutionProvider not found in providers.")
            print(f"  Available: {providers}")
            print("  Install: pip install onnxruntime-directml")
    except Exception as e:
        print(f"  WARNING: Could not verify ONNX Runtime: {e}")

    print()
    print("Done. Use the ONNX model in ComfyUI with the ExtraModels node pack.")


def main():
    args = parse_args()
    check_dependencies()
    export_model(args)


if __name__ == "__main__":
    main()
