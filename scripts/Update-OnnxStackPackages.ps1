# Downloads OnnxStack NuGet packages from GitHub Releases
# Usage: .\scripts\Update-OnnxStackPackages.ps1 [-Version "0.60.0"]

param(
    [string]$Version = "0.60.0"
)

$ErrorActionPreference = "Stop"

$packages = @(
    "OnnxStack.Core",
    "OnnxStack.Device",
    "OnnxStack.FeatureExtractor",
    "OnnxStack.ImageUpscaler",
    "OnnxStack.StableDiffusion"
)

$baseUrl = "https://github.com/Carnes/OnnxStack/releases/download/v$Version"
$outputDir = Join-Path $PSScriptRoot "..\Amuse.UI\Packages"

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

Write-Host "Downloading OnnxStack v$Version packages..." -ForegroundColor Cyan

foreach ($package in $packages) {
    $fileName = "$package.$Version.nupkg"
    $url = "$baseUrl/$fileName"
    $outputPath = Join-Path $outputDir $fileName

    Write-Host "  Downloading $fileName..." -NoNewline
    try {
        Invoke-WebRequest -Uri $url -OutFile $outputPath -UseBasicParsing
        Write-Host " OK" -ForegroundColor Green
    }
    catch {
        Write-Host " FAILED" -ForegroundColor Red
        Write-Host "    Error: $_" -ForegroundColor Red
    }
}

Write-Host "`nDone. Packages saved to: $outputDir" -ForegroundColor Cyan
Write-Host "Remember to update package versions in Amuse.UI.csproj if needed." -ForegroundColor Yellow
