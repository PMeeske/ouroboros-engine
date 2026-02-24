# install.ps1 — Automated setup for Comic Strip Generator on ASUS ROG Ally
# Run as Administrator in PowerShell:
#   powershell -ExecutionPolicy Bypass -File .\install.ps1

$ErrorActionPreference = "Stop"

$COMFYUI_DIR = "C:\ComfyUI"
$PYTHON_VERSION = "3.10"
$VENV_DIR = "$COMFYUI_DIR\venv"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " Comic Strip Generator — ROG Ally Installer" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# --- Check Python version ---
Write-Host "[1/8] Checking Python version..." -ForegroundColor Yellow
$pythonVer = python --version 2>&1
if ($pythonVer -notmatch "Python 3\.10") {
    Write-Host "ERROR: Python 3.10.x required. Found: $pythonVer" -ForegroundColor Red
    Write-Host "Install Python 3.10 from https://www.python.org/downloads/" -ForegroundColor Red
    Write-Host "Or run: winget install Python.Python.3.10" -ForegroundColor Red
    exit 1
}
Write-Host "  Found: $pythonVer" -ForegroundColor Green

# --- Check Git ---
Write-Host "[2/8] Checking Git..." -ForegroundColor Yellow
$gitVer = git --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Git not found. Install with: winget install Git.Git" -ForegroundColor Red
    exit 1
}
Write-Host "  Found: $gitVer" -ForegroundColor Green

# --- Clone ComfyUI ---
Write-Host "[3/8] Cloning ComfyUI..." -ForegroundColor Yellow
if (Test-Path $COMFYUI_DIR) {
    Write-Host "  ComfyUI directory already exists at $COMFYUI_DIR, pulling latest..." -ForegroundColor Yellow
    Push-Location $COMFYUI_DIR
    git pull
    Pop-Location
} else {
    git clone https://github.com/comfyanonymous/ComfyUI.git $COMFYUI_DIR
}
Write-Host "  ComfyUI ready at $COMFYUI_DIR" -ForegroundColor Green

# --- Create virtual environment ---
Write-Host "[4/8] Creating Python virtual environment..." -ForegroundColor Yellow
if (-not (Test-Path $VENV_DIR)) {
    python -m venv $VENV_DIR
}
& "$VENV_DIR\Scripts\Activate.ps1"
Write-Host "  Virtual environment activated" -ForegroundColor Green

# --- Install PyTorch + DirectML ---
Write-Host "[5/8] Installing PyTorch with DirectML..." -ForegroundColor Yellow
pip install --quiet torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cpu
pip install --quiet torch-directml
pip install --quiet onnxruntime-directml
Write-Host "  PyTorch + DirectML + ONNX Runtime installed" -ForegroundColor Green

# --- Install ComfyUI requirements ---
Write-Host "[6/8] Installing ComfyUI dependencies..." -ForegroundColor Yellow
Push-Location $COMFYUI_DIR
pip install --quiet -r requirements.txt
Pop-Location

# Additional dependencies
pip install --quiet opencv-python-headless Pillow pyyaml numpy scipy
pip install --quiet diffusers transformers accelerate safetensors
pip install --quiet huggingface-hub
Write-Host "  All dependencies installed" -ForegroundColor Green

# --- Install custom nodes ---
Write-Host "[7/8] Installing ComfyUI custom nodes..." -ForegroundColor Yellow
$customNodesDir = "$COMFYUI_DIR\custom_nodes"
if (-not (Test-Path "$customNodesDir\ComfyUI_ExtraModels")) {
    Push-Location $customNodesDir
    git clone https://github.com/city96/ComfyUI_ExtraModels.git
    Pop-Location
}
if (Test-Path "$customNodesDir\ComfyUI_ExtraModels\requirements.txt") {
    pip install --quiet -r "$customNodesDir\ComfyUI_ExtraModels\requirements.txt"
}
Write-Host "  Custom nodes installed" -ForegroundColor Green

# --- Create model directories ---
Write-Host "[8/8] Setting up model directories..." -ForegroundColor Yellow
$modelDirs = @(
    "$COMFYUI_DIR\models\checkpoints",
    "$COMFYUI_DIR\models\vae",
    "$COMFYUI_DIR\models\loras",
    "$COMFYUI_DIR\models\controlnet",
    "$COMFYUI_DIR\models\upscale_models",
    "$COMFYUI_DIR\models\onnx"
)
foreach ($dir in $modelDirs) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}
Write-Host "  Model directories created" -ForegroundColor Green

# --- Verify installation ---
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " Verifying installation..." -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "DirectML device check:" -ForegroundColor Yellow
python -c "import torch_directml; print('  DirectML device:', torch_directml.device())"

Write-Host "ONNX Runtime providers:" -ForegroundColor Yellow
python -c "import onnxruntime as ort; print('  Providers:', ort.get_available_providers())"

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host " Installation complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Download models:  .\scripts\download-models.ps1" -ForegroundColor White
Write-Host "  2. Start ComfyUI:    .\scripts\start-comfyui.ps1" -ForegroundColor White
Write-Host "  3. Open browser:     http://127.0.0.1:8188" -ForegroundColor White
Write-Host "  4. Load workflow:    workflows\comic_strip_workflow.json" -ForegroundColor White
