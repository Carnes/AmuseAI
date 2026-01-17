# Issue #1: Add headless API project for programmatic image generation

## Current Status: Phases 1-4 Mostly Complete

Last updated: 2026-01-16

---

## What's Been Implemented

### Phase 1: API Foundation ✅
- **ApiHostService** (`Frontends/Api/ApiHostService.cs`) - ASP.NET Core host running alongside WPF
- **JobQueueService** (`Core/Services/JobQueueService.cs`) - Shared job queue for UI and API
- **GenerationService** (`Core/Services/GenerationService.cs`) - Shared generation engine
- **Command line flags**: `--headless` / `--no-ui` for headless mode
- **Settings**: `ApiIsEnabled`, `ApiPort` in AmuseSettings

### Phase 2: UI Integration ✅
- **ApiView** (`Views/ApiView.xaml`) - New main tab showing:
  - API status indicator (running/stopped with port)
  - Job queue with status, progress, and clickable job IDs (copy to clipboard)
  - Real-time scrolling logs with color-coded log levels
- **Settings > Api tab** - Enable/disable checkbox, port configuration, Swagger link
- **LogSinkService** (`Services/LogSinkService.cs`) - In-memory Serilog sink for UI display
- **API tab visibility** - Hidden when `ApiIsEnabled` is false (`MainWindow.xaml`)

### Phase 3: Feature Parity ✅
All endpoints implemented in `Frontends/Api/Controllers/`:

| Endpoint | Controller | Description |
|----------|------------|-------------|
| `POST /api/generate/text-to-image` | GenerateController | Queue text-to-image job |
| `POST /api/generate/upscale` | GenerateController | Queue upscale job (supports sourceJobId) |
| `GET /api/jobs` | JobsController | List all jobs |
| `GET /api/jobs/{id}` | JobsController | Get job status |
| `GET /api/jobs/{id}/result` | JobsController | Get result image (PNG) |
| `GET /api/jobs/{id}/result/metadata` | JobsController | Get result metadata |
| `GET /api/jobs/{id}/wait` | JobsController | Long-poll for completion |
| `POST /api/jobs/{id}/cancel` | JobsController | Cancel a job |
| `GET /api/health` | HealthController | Health check |
| `GET /api/models` | HealthController | List available models |

### Phase 4: Production Ready ✅ (Core Features)
- [x] OpenAPI/Swagger documentation (available at `/swagger`)
- [x] Health check endpoint
- [x] **History View Integration** - API-generated images appear in UI history
- [x] **Hide API Module When Disabled** - API tab hidden when `ApiIsEnabled` is false
- [x] **Generation Defaults** - App-wide defaults for model, negative prompt, dimensions, steps, guidance scale, scheduler
- [x] **Dimension Validation** - API validates width/height against model constraints (returns 400 with valid ranges)
- [x] **Upscale Job Chaining** - Upscale endpoint accepts `sourceJobId` to use output from a previous job
- [x] **Default Upscale Settings** - Default upscale model and scale factor configurable in Settings
- [x] **Click-to-Copy** - Job IDs and log entries can be clicked to copy to clipboard
- [x] **Auto-Tiling for Upscale** - Automatically enables tiling for images with incompatible dimensions

### Phase 4: Optional Features (Future)
- [ ] API Key Authentication
- [ ] Rate Limiting
- [ ] Image-to-image endpoint (removed for now, may be added later)

---

## History View Integration Details

API-generated images now automatically appear in the appropriate view's history:

**How it works:**
1. `GenerationJobResult` now includes `OnnxImage`, `GenerateOptions`, `DiffuserType`, and `PipelineType`
2. `JobQueueService` raises a `JobCompleted` event when jobs complete successfully
3. `StableDiffusionImageViewBase` subscribes to this event
4. Views filter by `DiffuserType` matching their `SupportedDiffusers` list
5. Matching results are converted to `ImageResult` and added to the view's `ImageResults` collection
6. Auto-save is triggered for API results (with "API_" prefix)

**Key files modified:**
- `Core/Models/GenerationJobResult.cs` - Added OnnxImage, GenerateOptions, DiffuserType, PipelineType
- `Core/Services/GenerationService.cs` - Populates new result fields
- `Core/Services/JobEventArgs.cs` - Added JobCompletedEventArgs
- `Core/Services/IJobQueueService.cs` - Added JobCompleted event
- `Core/Services/JobQueueService.cs` - Raises JobCompleted on success
- `Views/BaseViews/StableDiffusionImageViewBase.cs` - Subscribes and handles API results

---

## Key Files Reference

### API Infrastructure
- `Frontends/Api/ApiHostService.cs` - API host lifecycle management
- `Frontends/Api/Controllers/` - All API endpoints
- `Frontends/Api/DTOs/` - Request/response models

### Core Services
- `Core/Services/JobQueueService.cs` - Job queue implementation
- `Core/Services/GenerationService.cs` - Generation execution
- `Core/Services/IJobQueueService.cs` - Queue interface
- `Core/Models/GenerationJob.cs` - Job model

### UI Components
- `Views/ApiView.xaml` + `.cs` - API monitoring tab
- `Views/SettingsView.xaml` - Settings UI (Api section at line ~658)
- `Windows/MainWindow.xaml` - Main window with tab navigation
- `Services/LogSinkService.cs` - Log collection for UI

### Configuration
- `Models/AmuseSettings.cs` - Settings model (`ApiIsEnabled`, `ApiPort`, `ApiSwaggerUrl`)
- `App.xaml.cs` - App startup, API initialization (lines ~250-270)

---

## Generation Lock (Preventing Concurrent GPU Access)

A semaphore-based lock prevents crashes when both UI and API try to generate simultaneously:

**How it works:**
1. `JobQueueService` exposes `AcquireGenerationLockAsync()` which returns an `IDisposable`
2. API jobs acquire the lock in `ProcessJobAsync()` before generating
3. UI's `Generate()` method in `StableDiffusionImageViewBase` acquires the lock before generating
4. Both release the lock when done (via `Dispose()`)
5. If UI clicks Generate while API is processing, it shows "Waiting for generation lock..." and waits

**Key files:**
- `Core/Services/IJobQueueService.cs` - Added `AcquireGenerationLockAsync()` and `IsGenerationLockHeld`
- `Core/Services/JobQueueService.cs` - Implements semaphore-based locking
- `Views/BaseViews/StableDiffusionImageViewBase.cs` - UI acquires lock in `Generate()`

---

## Known Issues / Bugs Fixed This Session

1. **API status indicator showed "Stopped" when running** - Fixed by adding `Loaded` event handler to refresh status
2. **Clear completed jobs button not working** - Fixed by simplifying `ClearCompletedJobs` logic and adding `NotifyPropertyChanged`
3. **Settings not saving** - Added explicit `Mode=TwoWay` and `UpdateSourceTrigger=PropertyChanged` to bindings
4. **Crash when UI and API generate simultaneously** - Fixed by adding semaphore-based generation lock that both UI and API must acquire

---

## Git Branch

Current branch: `add_api`
Base branch: `master`

Files modified (staged):
- `Amuse.UI/Amuse.UI.csproj`
- `Amuse.UI/App.xaml.cs`
- `Amuse.UI/Models/AmuseSettings.cs`
- `Amuse.UI/Views/SettingsView.xaml`
- `Amuse.UI/Windows/MainWindow.xaml`
- Plus all new files in `Core/`, `Frontends/Api/`, `Services/`, `Views/`
