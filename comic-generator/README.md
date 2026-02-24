# Lightweight Local Comic-Strip Generator for ASUS ROG Ally

Fully local, offline-capable comic strip generation pipeline optimized for
AMD Z1 / Z1 Extreme APUs with no NVIDIA CUDA dependency.

## Target Hardware

| Spec              | Value                              |
|-------------------|------------------------------------|
| Device            | ASUS ROG Ally                      |
| APU               | AMD Z1 / Z1 Extreme (RDNA 3 iGPU) |
| OS                | Windows 11                         |
| Shared VRAM       | Up to 8 GB (configurable in BIOS)  |
| GPU Compute       | DirectML / ONNX Runtime            |
| CUDA              | **Not available**                  |

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    COMIC STRIP PIPELINE                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────┐   ┌───────────┐   ┌───────────┐   ┌───────────┐ │
│  │  Prompt   │──▶│ Scheduler │──▶│ SD Model  │──▶│  VAE      │ │
│  │  Engine   │   │ (DPM++2M) │   │ (Q8 ONNX) │   │  Decode   │ │
│  └──────────┘   └───────────┘   └───────────┘   └───────────┘ │
│       │                              │                  │       │
│       │         ┌───────────┐        │                  │       │
│       └────────▶│ControlNet │────────┘                  │       │
│                 │ (Optional) │                           │       │
│                 └───────────┘                            │       │
│                                                         ▼       │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                   Post-Processing                         │  │
│  │  ┌──────────┐  ┌───────────┐  ┌────────────────────────┐ │  │
│  │  │ Upscale  │  │ Halftone  │  │ Speech Bubble Overlay  │ │  │
│  │  │ (ESRGAN) │  │ Filter    │  │ (Pillow compositing)   │ │  │
│  │  └──────────┘  └───────────┘  └────────────────────────┘ │  │
│  └───────────────────────────────────────────────────────────┘  │
│                          │                                      │
│                          ▼                                      │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                  Panel Assembly                            │  │
│  │  ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐       │  │
│  │  │ P1  │ │ P2  │ │ P3  │ │ P4  │ │ P5  │ │ P6  │       │  │
│  │  └─────┘ └─────┘ └─────┘ └─────┘ └─────┘ └─────┘       │  │
│  │  Arranged into strip layout (1×3, 2×3, etc.)             │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│  Runtime Stack                                                  │
│  ┌────────────┐ ┌──────────────┐ ┌───────────────────────────┐ │
│  │  ComfyUI   │ │ ONNX Runtime │ │ DirectML (AMD RDNA 3 iGPU)│ │
│  │  (Backend) │ │ (Inference)  │ │ (Hardware Acceleration)   │ │
│  └────────────┘ └──────────────┘ └───────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

## Quick Start

```powershell
# 1. Run the automated installer (PowerShell as Administrator)
.\scripts\install.ps1

# 2. Start ComfyUI
.\scripts\start-comfyui.ps1

# 3. Open browser to http://127.0.0.1:8188
# 4. Load workflow from workflows/comic_strip_workflow.json
```

## Folder Structure

```
comic-generator/
├── README.md                  # This file
├── INSTALL.md                 # Detailed installation guide
├── TROUBLESHOOTING.md         # AMD DirectML troubleshooting
├── configs/
│   ├── comfyui-settings.yaml  # ComfyUI performance settings
│   └── model-config.yaml      # Model paths and parameters
├── examples/
│   ├── prompts.yaml           # Example comic-style prompts
│   └── sample-output/         # Example generated strips
├── models/
│   └── .gitkeep               # Model files (downloaded by installer)
├── scripts/
│   ├── install.ps1            # Automated setup script
│   ├── start-comfyui.ps1      # Launch script with optimized flags
│   ├── download-models.ps1    # Model downloader
│   ├── export-onnx.py         # Convert SD checkpoint to ONNX
│   └── assemble-strip.py      # Post-processing: panel assembly
└── workflows/
    ├── comic_strip_workflow.json        # Main ComfyUI workflow
    └── comic_strip_controlnet.json      # Workflow with ControlNet
```

## Recommended Models

| Model                        | Size    | Format | Purpose              |
|------------------------------|---------|--------|----------------------|
| Anything V5 Ink              | ~2.1 GB | ONNX   | Anime/manga base     |
| Counterfeit V3.0             | ~2.1 GB | ONNX   | Stylized anime       |
| SD 1.5 Lineart ControlNet   | ~700 MB | ONNX   | Line art guidance    |
| Real-ESRGAN x2 Anime        | ~17 MB  | ONNX   | Upscaling            |

All models are SD 1.5 based to stay within VRAM limits.

## Performance Expectations

| Metric                | Z1 Extreme     | Z1             |
|-----------------------|----------------|----------------|
| Single 512×512 image  | ~12–18 sec     | ~18–28 sec     |
| 4-panel strip         | ~55–80 sec     | ~80–120 sec    |
| 6-panel strip         | ~80–120 sec    | ~120–180 sec   |
| VRAM usage            | ~4–5 GB        | ~4–5 GB        |
| RAM usage             | ~6–8 GB        | ~6–8 GB        |

*Benchmarks with DPM++ 2M Karras, 20 steps, 512×512, batch size 1.*

## License

See the root repository [LICENSE](../LICENSE) file.
