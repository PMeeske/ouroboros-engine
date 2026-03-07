# download-models.ps1 — Download recommended models for comic strip generation
# Usage: powershell -ExecutionPolicy Bypass -File .\download-models.ps1

$ErrorActionPreference = "Stop"

$COMFYUI_ROOT = "D:\ComfyUI_windows_portable_amd\ComfyUI_windows_portable"
$COMFYUI_DIR  = "$COMFYUI_ROOT\ComfyUI"
$PYTHON_EXE   = "$COMFYUI_ROOT\python_embeded\python.exe"

if (-not (Test-Path $PYTHON_EXE)) {
    Write-Host "ERROR: ComfyUI portable not found at $COMFYUI_ROOT" -ForegroundColor Red
    exit 1
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " Model Downloader — Comic Strip Generator" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Ensure huggingface-cli is available via embedded Python
& $PYTHON_EXE -s -m pip install --quiet huggingface-hub

# --- Base Model: Anything V5 ---
Write-Host "[1/4] Downloading base model: Anything V5..." -ForegroundColor Yellow
$checkpointDir = "$COMFYUI_DIR\models\checkpoints"
if (-not (Test-Path "$checkpointDir\anything-v5-PrtRE.safetensors")) {
    Write-Host "  Downloading from HuggingFace (this may take a while)..." -ForegroundColor White
    & $PYTHON_EXE -s -m huggingface_hub.commands.huggingface_cli download stablediffusionapi/anything-v5 `
        anything-v5-PrtRE.safetensors `
        --local-dir $checkpointDir `
        --local-dir-use-symlinks False
    Write-Host "  Base model downloaded." -ForegroundColor Green
} else {
    Write-Host "  Base model already exists, skipping." -ForegroundColor Green
}

# --- Alternative: Counterfeit V3.0 ---
Write-Host "[2/4] Downloading alternative model: Counterfeit V3.0..." -ForegroundColor Yellow
if (-not (Test-Path "$checkpointDir\CounterfeitV30_v30.safetensors")) {
    Write-Host "  Downloading from HuggingFace..." -ForegroundColor White
    & $PYTHON_EXE -s -m huggingface_hub.commands.huggingface_cli download gsdf/Counterfeit-V3.0 `
        CounterfeitV30_v30.safetensors `
        --local-dir $checkpointDir `
        --local-dir-use-symlinks False
    Write-Host "  Counterfeit model downloaded." -ForegroundColor Green
} else {
    Write-Host "  Counterfeit model already exists, skipping." -ForegroundColor Green
}

# --- ControlNet: Lineart ---
Write-Host "[3/4] Downloading ControlNet: Lineart SD 1.5..." -ForegroundColor Yellow
$controlnetDir = "$COMFYUI_DIR\models\controlnet"
if (-not (Test-Path "$controlnetDir\control_v11p_sd15_lineart.pth")) {
    & $PYTHON_EXE -s -m huggingface_hub.commands.huggingface_cli download lllyasviel/ControlNet-v1-1 `
        control_v11p_sd15_lineart.pth `
        --local-dir $controlnetDir `
        --local-dir-use-symlinks False
    Write-Host "  ControlNet Lineart downloaded." -ForegroundColor Green
} else {
    Write-Host "  ControlNet Lineart already exists, skipping." -ForegroundColor Green
}

# --- Upscaler: Real-ESRGAN Anime ---
Write-Host "[4/4] Downloading upscaler: Real-ESRGAN x2 Anime..." -ForegroundColor Yellow
$upscaleDir = "$COMFYUI_DIR\models\upscale_models"
if (-not (Test-Path "$upscaleDir\RealESRGAN_x2plus_anime.pth")) {
    & $PYTHON_EXE -s -m huggingface_hub.commands.huggingface_cli download ai-forever/Real-ESRGAN `
        RealESRGAN_x2plus_anime.pth `
        --local-dir $upscaleDir `
        --local-dir-use-symlinks False
    Write-Host "  Upscaler downloaded." -ForegroundColor Green
} else {
    Write-Host "  Upscaler already exists, skipping." -ForegroundColor Green
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host " All models downloaded!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Models location: $COMFYUI_DIR\models" -ForegroundColor White
Write-Host ""
Write-Host "Disk usage:" -ForegroundColor Yellow

# Show sizes
$totalSize = 0
Get-ChildItem "$COMFYUI_DIR\models" -Recurse -File |
    Where-Object { $_.Extension -in '.safetensors', '.pth', '.onnx', '.bin' } |
    ForEach-Object {
        $sizeMB = [math]::Round($_.Length / 1MB, 1)
        $totalSize += $_.Length
        Write-Host "  $($_.Name): $sizeMB MB" -ForegroundColor White
    }
$totalGB = [math]::Round($totalSize / 1GB, 2)
Write-Host "  Total: $totalGB GB" -ForegroundColor Cyan
