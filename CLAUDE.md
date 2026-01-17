# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AmuseAI is a Windows desktop application for AI-powered image and video generation, optimized for AMD graphics cards using DirectML and ONNX Runtime. Built on .NET 9.0 with a WPF UI and an optional REST API for programmatic access.

Fork of TensorStack-AI/AmuseAI with DirectML optimizations and custom OnnxStack packages.

## Build Commands

```bash
# Build the project
dotnet build

# Create a release build
dotnet publish Amuse.UI/Amuse.UI.csproj -c Release -o ./publish
```

**Build Configurations:**
- `Debug` - Standard debug build
- `Release` - Optimized release build
- `Debug_Direct` - Debug with direct OnnxStack references (for development)
- `Release-Installer` - Release for installer creation

## Architecture

### Multi-Frontend Architecture

The application supports multiple frontends (WPF UI and REST API) sharing a common generation engine:

```
┌─────────────────────┐     ┌─────────────────────┐
│     WPF UI          │     │     REST API        │
│  (Views/*.xaml)     │     │  (Frontends/Api/)   │
└──────────┬──────────┘     └──────────┬──────────┘
           │                           │
           └───────────┬───────────────┘
                       ▼
           ┌───────────────────────┐
           │    JobQueueService    │
           │  (Core/Services/)     │
           └───────────┬───────────┘
                       ▼
           ┌───────────────────────┐
           │   GenerationService   │
           │  (Core/Services/)     │
           └───────────┬───────────┘
                       ▼
           ┌───────────────────────┐
           │   OnnxStack (local)   │
           │   DirectML backend    │
           └───────────────────────┘
```

### Key Directories

- `Amuse.UI/Core/` - Shared services: JobQueueService, GenerationService, job models
- `Amuse.UI/Frontends/Api/` - REST API: controllers, DTOs, ApiHostService
- `Amuse.UI/Views/` - WPF views, including `StableDiffusion/` for generation views
- `Amuse.UI/Services/` - Application services: ModelCacheService, FileService, LogSinkService
- `Amuse.UI/Models/` - Data models including `AmuseSettings.cs`
- `Amuse.UI/Packages/` - Local OnnxStack NuGet packages (DirectML builds)

### Entry Points

- **App.xaml.cs** - Application startup, DI container setup, headless mode handling (`--headless` or `--no-ui`)
- **MainWindow.xaml** - Main WPF window with tab navigation
- **ApiHostService** - ASP.NET Core server running alongside WPF (Frontends/Api/)

### API Endpoints

When API is enabled (port 5000 by default):
- `POST /api/generate/text-to-image` - Queue text-to-image job
- `POST /api/generate/upscale` - Queue upscale job (supports sourceJobId for chaining)
- `GET /api/jobs/{id}` - Get job status
- `GET /api/jobs/{id}/result` - Get result image (PNG)
- `GET /api/jobs/{id}/wait` - Long-poll for completion
- `GET /api/health` - Health check
- `GET /api/models` - List available models
- `GET /swagger` - OpenAPI documentation

### Settings

`AmuseSettings.cs` contains application settings including:
- `ApiIsEnabled`, `ApiPort` - API configuration
- Model directories and paths
- Content moderation toggle

## Dependencies

Custom OnnxStack packages (v0.60.0) in `Packages/` directory - configured via `NuGet.config`:
- OnnxStack.Core, OnnxStack.Device, OnnxStack.StableDiffusion, OnnxStack.ImageUpscaler, OnnxStack.FeatureExtractor

## Release Process

To create a new release:

```bash
# 1. Update version in Amuse.UI/Amuse.UI.csproj
#    <Version>X.Y.Z</Version>

# 2. Build the release
dotnet publish Amuse.UI/Amuse.UI.csproj -c Release -r win-x64 --self-contained false -o ./publish

# 3. Create the zip file (PowerShell)
powershell -Command "Compress-Archive -Path './publish/*' -DestinationPath './AmuseAI-vX.Y.Z-win-x64.zip' -Force"

# 4. Create GitHub release with the zip file
gh release create vX.Y.Z ./AmuseAI-vX.Y.Z-win-x64.zip --title "vX.Y.Z" --notes "Release notes here..."
```

**Release naming convention:**
- Tag: `vX.Y.Z` (e.g., `v3.3.0`)
- Zip file: `AmuseAI-vX.Y.Z-win-x64.zip`

**Version bumping guidelines:**
- Major (X): Breaking changes or major new features
- Minor (Y): New features, API additions
- Patch (Z): Bug fixes, minor improvements

## Current Development

### Issue #1: REST API (Branch: `add_api`)
See `CONTEXT.md` for detailed status:
- Phase 1-4 Complete: API foundation, UI integration, endpoints, Swagger, API key auth
- Optional: Rate limiting (not implemented)

### Issue #2: MCP Server for Claude Desktop
See [GitHub Issue #2](https://github.com/Carnes/AmuseAI/issues/2) for details:
- Separate `Amuse.MCP` project in the solution
- Allows Claude Desktop to generate images via natural language
- "Register with Claude Desktop" button in Settings
