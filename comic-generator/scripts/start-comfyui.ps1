# start-comfyui.ps1 — Launch ComfyUI with AMD DirectML optimizations
# Usage: powershell -ExecutionPolicy Bypass -File .\start-comfyui.ps1

$ErrorActionPreference = "Stop"

$COMFYUI_DIR = "C:\ComfyUI"
$VENV_DIR = "$COMFYUI_DIR\venv"

# Activate virtual environment
& "$VENV_DIR\Scripts\Activate.ps1"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " Starting ComfyUI — AMD DirectML Mode" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Backend:     DirectML (AMD RDNA 3)" -ForegroundColor White
Write-Host "  VRAM Mode:   Low VRAM" -ForegroundColor White
Write-Host "  Preview:     Auto" -ForegroundColor White
Write-Host "  Address:     http://127.0.0.1:8188" -ForegroundColor White
Write-Host ""

# Set environment variables for optimal DirectML performance
$env:PYTORCH_DIRECTML_ENABLE_GRAPH_CAPTURE = "1"
$env:ONNXRUNTIME_DML_GRAPH_CAPTURE = "1"

# Launch ComfyUI with optimized flags:
#   --directml          : Use DirectML backend instead of CUDA
#   --lowvram           : Aggressive VRAM management (offload to RAM when needed)
#   --preview-method auto : Enable live preview during generation
#   --dont-print-server : Reduce console noise
Push-Location $COMFYUI_DIR
python main.py `
    --directml `
    --lowvram `
    --preview-method auto `
    --dont-print-server

Pop-Location
