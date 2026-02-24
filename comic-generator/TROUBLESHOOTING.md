# Troubleshooting — AMD DirectML on ROG Ally

## Common Issues

### 1. "No DirectML device found"

**Symptoms:** ComfyUI fails to start or falls back to CPU.

**Fixes:**
- Update AMD Adrenalin drivers to the latest version:
  ```powershell
  # Check current driver
  wmic path win32_videocontroller get name, driverversion
  ```
- Ensure Windows 11 is updated (DirectML ships via Windows Update).
- Verify `torch-directml` is installed:
  ```powershell
  pip show torch-directml
  ```
- Reinstall if needed:
  ```powershell
  pip uninstall torch-directml
  pip install torch-directml
  ```

### 2. "Out of Memory" during generation

**Symptoms:** Generation crashes mid-diffusion with a memory error.

**Fixes:**
- Confirm VRAM is set to 8 GB in BIOS (see INSTALL.md Step 0).
- Launch ComfyUI with `--lowvram` flag (already in start script).
- Reduce resolution to 384×384 during testing.
- Close all other applications (games, browsers with hardware acceleration).
- Reduce batch size to 1.
- Use fewer diffusion steps (15 instead of 20).
- Ensure no other DirectML processes are running:
  ```powershell
  tasklist | findstr "python onnxruntime"
  ```

### 3. Black or garbage images output

**Symptoms:** Generation completes but output is solid black, noise, or corrupted.

**Fixes:**
- Switch from float16 to float32 ONNX model (some DirectML ops lack fp16 support):
  ```powershell
  python scripts\export-onnx.py --model_path <path> --output_path <path> --dtype float32
  ```
- Update `onnxruntime-directml`:
  ```powershell
  pip install --upgrade onnxruntime-directml
  ```
- Try a different scheduler/sampler (Euler A is the most compatible).
- Check that the VAE is loading correctly. A missing VAE produces black images.

### 4. Extremely slow generation (>60 seconds for 512×512)

**Symptoms:** Generation takes far longer than expected benchmarks.

**Fixes:**
- Verify DirectML is actually being used (not falling back to CPU):
  ```python
  import torch_directml
  print(torch_directml.device())       # Should print a device
  print(torch_directml.device_count())  # Should be >= 1
  ```
- Check Windows power plan is set to **Best Performance**:
  - Settings → System → Power → Power mode → Best performance
  - Also in ASUS Armoury Crate: set **Turbo** mode.
- Ensure the device is plugged in (battery mode throttles the APU).
- Disable Windows Game Mode during generation (it can steal GPU time):
  - Settings → Gaming → Game Mode → Off
- Close background apps using GPU (Task Manager → GPU column).

### 5. ComfyUI custom node errors

**Symptoms:** `ModuleNotFoundError` or node not recognized.

**Fixes:**
- Ensure you're in the correct venv:
  ```powershell
  .\venv\Scripts\Activate.ps1
  ```
- Reinstall node dependencies:
  ```powershell
  cd C:\ComfyUI\custom_nodes\ComfyUI_ExtraModels
  pip install -r requirements.txt
  ```
- Restart ComfyUI after installing nodes.

### 6. ControlNet crashes or produces no effect

**Symptoms:** Adding ControlNet node causes OOM or output ignores the control image.

**Fixes:**
- ControlNet adds ~700 MB to VRAM usage. With the base model at ~4 GB, total approaches the 8 GB limit.
- Reduce base model resolution to 384×384 when using ControlNet.
- Use the lightweight ControlNet-small variants if available.
- Apply ControlNet at lower strength (0.4–0.6 instead of 1.0).
- Set ControlNet start step to 0.2 (skip early steps).

### 7. ONNX export fails

**Symptoms:** `export-onnx.py` crashes with shape mismatch or unsupported op.

**Fixes:**
- Use `opset_version=17` (highest stable for DirectML):
  ```powershell
  python scripts\export-onnx.py --model_path <path> --output_path <path> --opset 17
  ```
- Ensure `optimum` is installed:
  ```powershell
  pip install optimum[onnxruntime]
  ```
- For stubborn models, try fp32 export first, then quantize to fp16 separately.

### 8. Python version conflicts

**Symptoms:** Various import errors or wheels not found.

**Fixes:**
- This project requires **Python 3.10.x**. Python 3.11+ has known issues
  with `onnxruntime-directml` binary wheels.
- Check version:
  ```powershell
  python --version
  ```
- If you have multiple Python versions, create the venv explicitly:
  ```powershell
  py -3.10 -m venv venv
  ```

## DirectML-Specific Notes

### Supported Operations

DirectML supports most standard diffusion model operations, but has gaps:

| Operation         | Status      | Workaround              |
|-------------------|-------------|-------------------------|
| Conv2d            | Supported   | —                       |
| GroupNorm         | Supported   | —                       |
| Attention (SDPA)  | Partial     | Use attention slicing   |
| FlashAttention    | Not supported | Falls back to standard |
| xformers          | Not supported | Use attention slicing  |
| FP16 matmul       | Partial     | Use FP32 if artifacts   |

### Recommended Driver Versions

- AMD Adrenalin: 23.12.1 or newer
- Windows 11: 23H2 or newer
- DirectML: 1.13.0+ (ships with Windows Update)

### Power Profile for Best Performance

```powershell
# Set Windows to High Performance
powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c

# Verify
powercfg /getactivescheme
```

## Getting Help

- ComfyUI GitHub Issues: https://github.com/comfyanonymous/ComfyUI/issues
- ONNX Runtime DirectML: https://github.com/microsoft/onnxruntime/issues
- AMD ROG Ally community: r/ROGAlly
