<p align="center" width="100%">
    <img width="25%" src="Assets/Amuse-Logo-512.png">
</p>

# AmuseAI

![AmuseAI Screenshot](Assets/readme/image_gen_squirrel.png)

> This project is a fork of [TensorStack-AI/AmuseAI](https://github.com/TensorStack-AI/AmuseAI), with additional contributions from [saddam213/AmuseAI](https://github.com/saddam213/AmuseAI) including removal of extra dependencies, updated CLIP Tokenizer, and refactoring to be DirectML-only.

AmuseAI is a powerful AI image and video generation application optimized for **AMD graphics cards**. Using DirectML and ONNX Runtime, AmuseAI brings Stable Diffusion capabilities to AMD GPUs without requiring CUDA or complex setup procedures.

---

# Users

## Installation

1. Download the latest release from the [Releases page](https://github.com/Carnes/AmuseAI/releases)
2. Extract all files to a folder of your choice
3. Run `Amuse.exe`

## Quick Start
1. Go to "Model Manager" in top tabs
2. Go to "Stable Diffusion" smaller tab
3. Choose a model, like Stable Diffusion XL for example
4. Click Download
5. Go back to "Image Generation" in top tabs
6. Choose your model in the model selector dropdown and press Load button
7. Type a fun prompt
8. Press Generate button

## Privacy

**AmuseAI runs entirely on your local machine.** There is no telemetry, no analytics, and no data collection of any kind. Your prompts, images, and creative work are never sent to any company or server for review.

The application only accesses the network in these specific situations:

- **Update checks** - On startup, the app checks GitHub for new releases (can be disabled in Settings -> toggle "Automatically Check For Updates" off)
- **Model downloads** - When you explicitly click "Download" on a model, files are downloaded from HuggingFace or other model hosting sites
- **Model thumbnails** - Small preview images for models in the Model Manager are downloaded for display purposes

All AI processing happens locally on your GPU. Your prompts and generated content never leave your computer.


## Features

### Image Generation

#### Text To Image
Generate images from text prompts using Stable Diffusion models.

#### Image To Image
Transform existing images using AI with text-guided modifications.

#### Paint To Image
Create images by painting rough sketches and letting AI refine them.

#### Image Inpaint
Edit specific areas of an image by masking and regenerating with AI.

#### Upscaler
Enhance image resolution using AI upscaling models.

#### Feature Extractor
Extract features from images for use in other workflows.

### Video Generation

#### Text To Video
Generate video clips from text prompts.

#### Image To Video
Animate static images into video sequences.

#### Video To Video
Transform existing videos with AI-powered effects.

#### Frame To Frame
Process video frame-by-frame with AI transformations.

#### Video Upscaler
Enhance video resolution using AI upscaling.

#### Video Feature Extractor
Extract features from video for use in other workflows.

### REST API

AmuseAI includes an optional REST API for programmatic image generation, enabling integration with external applications and automation workflows.

#### Enabling the API
1. Go to **Settings > Api**
2. Check "Enable API"
3. Configure the port (default: 5000)
4. Restart the application

#### API Endpoints
- `POST /api/generate/text-to-image` - Queue a text-to-image generation job
- `POST /api/generate/upscale` - Queue an image upscale job
- `GET /api/jobs/{id}` - Get job status and progress
- `GET /api/jobs/{id}/result` - Download the generated image (PNG)
- `GET /api/jobs/{id}/wait` - Long-poll until job completes
- `GET /api/models` - List available models
- `GET /api/health` - Health check
- `GET /swagger` - Interactive API documentation

#### API Monitoring
When the API is enabled, an "Api" tab appears in the main navigation showing:
- Real-time job queue with status indicators
- Scrolling log viewer with color-coded messages
- API status indicator (running/stopped)

Images generated via the API automatically appear in the UI's history view.

## Settings

### Content Moderation
Content Moderation can be disabled by going to **Settings -> Stable Diffusion -> toggle "Content Moderation" off**

---

# Developers

## Building from Source

### Prerequisites
- .NET 9.0 SDK
- Windows 10/11 (x64)

### Build Instructions

```bash
# Clone the repository
git clone https://github.com/Carnes/AmuseAI.git
cd AmuseAI

# Build the project
dotnet build

# Or create a release build
dotnet publish Amuse.UI/Amuse.UI.csproj -c Release -o ./publish
```

## DirectML Version

DirectML `OnnxStack` packages can be found in the `Packages` folder. No other external dependencies are required.

## OnnxStack NuGet Packages

This project uses custom DirectML builds of [OnnxStack](https://github.com/TensorStack-AI/OnnxStack) packages. The pre-built `.nupkg` files are included in `Amuse.UI/Packages/`:

- `OnnxStack.Core`
- `OnnxStack.Device`
- `OnnxStack.FeatureExtractor`
- `OnnxStack.ImageUpscaler`
- `OnnxStack.StableDiffusion`

These packages are configured as local package sources via `NuGet.config`. If you need to update or rebuild these packages, you can use the `scripts/Update-OnnxStackPackages.ps1` script to download new versions from the OnnxStack GitHub releases.
