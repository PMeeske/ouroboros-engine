# install.ps1 — Setup comic-generator extras on ComfyUI Portable (AMD DirectML)
# Usage: powershell -ExecutionPolicy Bypass -File .\install.ps1
#
# Prerequisites: ComfyUI portable installed at D:\ComfyUI_windows_portable_amd

$ErrorActionPreference = "Stop"

$COMFYUI_ROOT = "D:\ComfyUI_windows_portable_amd\ComfyUI_windows_portable"
$COMFYUI_DIR  = "$COMFYUI_ROOT\ComfyUI"
$PYTHON_EXE   = "$COMFYUI_ROOT\python_embeded\python.exe"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " Comic Strip Generator — Portable Setup" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# --- Verify portable installation ---
Write-Host "[1/4] Verifying ComfyUI portable installation..." -ForegroundColor Yellow
if (-not (Test-Path $PYTHON_EXE)) {
    Write-Host "ERROR: ComfyUI portable not found at $COMFYUI_ROOT" -ForegroundColor Red
    Write-Host "Download from: https://github.com/comfyanonymous/ComfyUI/releases" -ForegroundColor Red
    Write-Host "Extract to: D:\ComfyUI_windows_portable_amd\" -ForegroundColor Red
    exit 1
}
$pythonVer = & $PYTHON_EXE --version 2>&1
Write-Host "  Found: $pythonVer" -ForegroundColor Green
Write-Host "  Location: $COMFYUI_ROOT" -ForegroundColor Green

# --- Install additional pip packages ---
Write-Host "[2/4] Installing additional dependencies..." -ForegroundColor Yellow
& $PYTHON_EXE -s -m pip install --quiet opencv-python-headless Pillow pyyaml scipy
& $PYTHON_EXE -s -m pip install --quiet huggingface-hub
Write-Host "  Additional packages installed" -ForegroundColor Green

# --- Install custom nodes ---
Write-Host "[3/4] Installing ComfyUI custom nodes..." -ForegroundColor Yellow
$customNodesDir = "$COMFYUI_DIR\custom_nodes"
if (-not (Test-Path "$customNodesDir\ComfyUI_ExtraModels")) {
    Push-Location $customNodesDir
    git clone https://github.com/city96/ComfyUI_ExtraModels.git
    Pop-Location
}
if (Test-Path "$customNodesDir\ComfyUI_ExtraModels\requirements.txt") {
    & $PYTHON_EXE -s -m pip install --quiet -r "$customNodesDir\ComfyUI_ExtraModels\requirements.txt"
}
Write-Host "  Custom nodes installed" -ForegroundColor Green

# --- Verify DirectML ---
Write-Host "[4/4] Verifying DirectML..." -ForegroundColor Yellow
& $PYTHON_EXE -s -c "import torch_directml; print('  DirectML device:', torch_directml.device())"

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host " Setup complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Download models:  .\scripts\download-models.ps1" -ForegroundColor White
Write-Host "  2. Start ComfyUI:    .\scripts\start-comfyui.ps1" -ForegroundColor White
Write-Host "  3. Open browser:     http://127.0.0.1:8188" -ForegroundColor White
Write-Host "  4. Load workflow:    workflows\comic_strip_workflow.json" -ForegroundColor White
