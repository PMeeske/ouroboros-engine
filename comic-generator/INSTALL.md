# Installation Guide — Comic Strip Generator on ROG Ally

## Prerequisites

- Windows 11 (ARM or x64 — ROG Ally ships x64)
- Python 3.10.x (3.11+ has ONNX compatibility issues)
- Git for Windows
- At least 20 GB free disk space
- BIOS: Set VRAM allocation to **8 GB** (ASUS Armoury Crate → GPU Settings)

## Step 0 — Set VRAM to 8 GB

1. Reboot into BIOS (hold Volume Down + Power).
2. Navigate to **Advanced → UMA Frame Buffer Size**.
3. Set to **8G**.
4. Save and exit.

This gives the RDNA 3 iGPU the maximum shared memory it can use.

## Step 1 — Install Python 3.10

```powershell
# Download from python.org (3.10.x — NOT 3.11+)
# Or use winget:
winget install Python.Python.3.10

# Verify
python --version
# Expected: Python 3.10.x
```

Add Python to PATH during installation.

## Step 2 — Install Git

```powershell
winget install Git.Git
```

## Step 3 — Clone ComfyUI

```powershell
cd C:\
git clone https://github.com/comfyanonymous/ComfyUI.git
cd ComfyUI
```

## Step 4 — Create Virtual Environment

```powershell
python -m venv venv
.\venv\Scripts\Activate.ps1
```

## Step 5 — Install PyTorch with DirectML

**Do NOT install the default CUDA PyTorch.** Use the DirectML variant:

```powershell
pip install torch-directml

# Install torch CPU as the base (DirectML wraps it)
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cpu
```

Verify DirectML is working:

```powershell
python -c "import torch_directml; print(torch_directml.device())"
# Expected: <torch_directml device>
```

## Step 6 — Install ONNX Runtime with DirectML

```powershell
pip install onnxruntime-directml
```

Verify:

```powershell
python -c "import onnxruntime as ort; print(ort.get_available_providers())"
# Expected: ['DmlExecutionProvider', 'CPUExecutionProvider']
```

## Step 7 — Install ComfyUI Dependencies

```powershell
cd C:\ComfyUI
pip install -r requirements.txt
```

## Step 8 — Install Additional Dependencies

```powershell
pip install opencv-python-headless Pillow pyyaml numpy scipy
pip install diffusers transformers accelerate safetensors
pip install optimum[onnxruntime-gpu]
```

## Step 9 — Download Models

Use the provided download script or manually download:

### Base Model — Anything V5 Ink (ONNX)

```powershell
# Option A: Use the download script
powershell -ExecutionPolicy Bypass -File .\scripts\download-models.ps1

# Option B: Manual download via huggingface-cli
pip install huggingface-hub
huggingface-cli download Linaqruf/anything-v3.0 --local-dir C:\ComfyUI\models\checkpoints\anything-v5-ink
```

### Convert to ONNX (if checkpoint is .safetensors)

```powershell
python scripts\export-onnx.py ^
  --model_path C:\ComfyUI\models\checkpoints\anything-v5-ink ^
  --output_path C:\ComfyUI\models\onnx\anything-v5-ink ^
  --dtype float16
```

### ControlNet Lineart (Optional)

```powershell
huggingface-cli download lllyasviel/control_v11p_sd15_lineart ^
  --local-dir C:\ComfyUI\models\controlnet\lineart
```

### Upscaler — Real-ESRGAN Anime x2

```powershell
huggingface-cli download ai-forever/Real-ESRGAN ^
  --local-dir C:\ComfyUI\models\upscale_models\realesrgan-anime
```

## Step 10 — Install ComfyUI ONNX Node Pack

```powershell
cd C:\ComfyUI\custom_nodes
git clone https://github.com/city96/ComfyUI_ExtraModels.git
cd ComfyUI_ExtraModels
pip install -r requirements.txt
```

## Step 11 — Configure ComfyUI for Low VRAM

Edit or create `C:\ComfyUI\extra_model_paths.yaml`:

```yaml
a]111:
    base_path: C:\ComfyUI\models

comfyui:
    checkpoints: models/checkpoints/
    vae: models/vae/
    loras: models/loras/
    controlnet: models/controlnet/
    upscale_models: models/upscale_models/
    onnx: models/onnx/
```

## Step 12 — Launch ComfyUI with AMD Optimizations

```powershell
cd C:\ComfyUI
python main.py --directml --lowvram --preview-method auto
```

Or use the provided start script:

```powershell
.\scripts\start-comfyui.ps1
```

## Step 13 — Load the Comic Workflow

1. Open http://127.0.0.1:8188 in your browser.
2. Click **Load** in the ComfyUI interface.
3. Navigate to `workflows/comic_strip_workflow.json`.
4. Click **Queue Prompt** to generate.

## Verification Checklist

- [ ] Python 3.10.x installed
- [ ] `torch_directml.device()` returns a device
- [ ] ONNX Runtime lists `DmlExecutionProvider`
- [ ] ComfyUI starts without errors
- [ ] Model loads into VRAM under 5 GB
- [ ] Single 512×512 generation completes
