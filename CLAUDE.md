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

## Current Development

Branch `add_api` adds the REST API. See `CONTEXT.md` for detailed status and remaining tasks:
- Phase 1-3 Complete: API foundation, UI integration, endpoint parity
- Phase 4 Partial: Swagger done, API key auth and rate limiting pending
