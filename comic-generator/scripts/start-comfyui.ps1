# start-comfyui.ps1 — Launch ComfyUI with AMD DirectML optimizations
# Usage: powershell -ExecutionPolicy Bypass -File .\start-comfyui.ps1

$ErrorActionPreference = "Stop"

$COMFYUI_ROOT = "D:\ComfyUI_windows_portable_amd\ComfyUI_windows_portable"
$COMFYUI_DIR  = "$COMFYUI_ROOT\ComfyUI"
$PYTHON_EXE   = "$COMFYUI_ROOT\python_embeded\python.exe"

if (-not (Test-Path $PYTHON_EXE)) {
    Write-Host "ERROR: ComfyUI portable not found at $COMFYUI_ROOT" -ForegroundColor Red
    exit 1
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " Starting ComfyUI — AMD DirectML Mode" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Install:     $COMFYUI_ROOT" -ForegroundColor White
Write-Host "  Backend:     DirectML (AMD RDNA 3)" -ForegroundColor White
Write-Host "  VRAM Mode:   Low VRAM" -ForegroundColor White
Write-Host "  Preview:     Auto" -ForegroundColor White
Write-Host "  Address:     http://127.0.0.1:8188" -ForegroundColor White
Write-Host ""

# Set environment variables for optimal DirectML performance
$env:PYTORCH_DIRECTML_ENABLE_GRAPH_CAPTURE = "1"
$env:ONNXRUNTIME_DML_GRAPH_CAPTURE = "1"

# Launch ComfyUI with optimized flags
Push-Location $COMFYUI_DIR
& $PYTHON_EXE -s main.py `
    --windows-standalone-build `
    --directml `
    --lowvram `
    --preview-method auto `
    --dont-print-server

Pop-Location
