using Amuse.UI.Commands;
using Amuse.UI.Dialogs;
using Amuse.UI.Helpers;
using Amuse.UI.Models;
using Amuse.UI.Models.FeatureExtractor;
using Amuse.UI.Models.StableDiffusion;
using Amuse.UI.Models.Upscale;
using Amuse.UI.Services;
using Microsoft.Extensions.Logging;
using OnnxStack.Core.Config;
using OnnxStack.StableDiffusion.Enums;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Amuse.UI.Views
{
    public class SettingsViewBase : UserControl, INotifyPropertyChanged
    {
        private readonly ILogger<SettingsViewBase> _logger;
        private readonly IDialogService _dialogService;
        private readonly IModelCacheService _modelCacheService;

        private ControlNetModelSetViewModel _selectedControlNetModel;
        private FeatureExtractorModelSetViewModel _selectedFeatureExtractorModel;
        private StableDiffusionModelSetViewModel _selectedStableDiffusionModel;
        private UpscaleModelSetViewModel _selectedUpscaleModel;
        private PromptInputModel _selectedPromptInputModel;
        private OnnxStackConfig _onnxStackConfig;
        private bool _isSaving;
        private string _saveStatusMessage;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsViewBase"/> class.
        /// </summary>
        public SettingsViewBase()
        {
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                _logger = App.GetService<ILogger<SettingsViewBase>>();
                _dialogService = App.GetService<IDialogService>();
                _modelCacheService = App.GetService<IModelCacheService>();
                _onnxStackConfig = App.GetService<OnnxStackConfig>();
            }

            SaveCommand = new AsyncRelayCommand(SaveAsync);

            AddStableDiffusionModelCommand = new AsyncRelayCommand(AddStableDiffusionModel);
            UpdateStableDiffusionModelCommand = new AsyncRelayCommand(UpdateStableDiffusionModel, () => SelectedStableDiffusionModel is not null);
            RemoveStableDiffusionModelCommand = new AsyncRelayCommand(() => RemoveStableDiffusionModel(true), () => SelectedStableDiffusionModel is not null);

            AddControlNetModelCommand = new AsyncRelayCommand(AddControlNetModel);
            UpdateControlNetModelCommand = new AsyncRelayCommand(UpdateControlNetModel, () => SelectedControlNetModel is not null);
            RemoveControlNetModelCommand = new AsyncRelayCommand(() => RemoveControlNetModel(true), () => SelectedControlNetModel is not null);

            AddFeatureExtractorModelCommand = new AsyncRelayCommand(AddFeatureExtractorModel);
            UpdateFeatureExtractorModelCommand = new AsyncRelayCommand(UpdateFeatureExtractorModel, () => SelectedFeatureExtractorModel is not null && !SelectedFeatureExtractorModel.Template.IsFixedInstall);
            RemoveFeatureExtractorModelCommand = new AsyncRelayCommand(() => RemoveFeatureExtractorModel(true), () => SelectedFeatureExtractorModel is not null && !SelectedFeatureExtractorModel.Template.IsFixedInstall);

            AddUpscaleModelCommand = new AsyncRelayCommand(AddUpscaleModel);
            UpdateUpscaleModelCommand = new AsyncRelayCommand(UpdateUpscaleModel, () => SelectedUpscaleModel is not null);
            RemoveUpscaleModelCommand = new AsyncRelayCommand(() => RemoveUpscaleModel(true), () => SelectedUpscaleModel is not null);


            AddPromptInputModelCommand = new AsyncRelayCommand(AddPromptInputModel);
            UpdatePromptInputModelCommand = new AsyncRelayCommand(UpdatePromptInputModel, () => SelectedPromptInputModel is not null);
            RemovePromptInputModelCommand = new AsyncRelayCommand(RemovePromptInputModel, () => SelectedPromptInputModel is not null);
            MovePromptInputModelCommand = new AsyncRelayCommand<bool>(MovePromptInputModel, (x) => SelectedPromptInputModel is not null);
        }

        public static readonly DependencyProperty SettingsProperty =
            DependencyProperty.Register(nameof(Settings), typeof(AmuseSettings), typeof(SettingsViewBase), new PropertyMetadata<SettingsViewBase>(c => c.OnSettingsChanged()));

        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand AddStableDiffusionModelCommand { get; }
        public AsyncRelayCommand UpdateStableDiffusionModelCommand { get; }
        public AsyncRelayCommand RemoveStableDiffusionModelCommand { get; }

        public AsyncRelayCommand AddControlNetModelCommand { get; }
        public AsyncRelayCommand UpdateControlNetModelCommand { get; }
        public AsyncRelayCommand RemoveControlNetModelCommand { get; }

        public AsyncRelayCommand AddFeatureExtractorModelCommand { get; }
        public AsyncRelayCommand UpdateFeatureExtractorModelCommand { get; }
        public AsyncRelayCommand RemoveFeatureExtractorModelCommand { get; }

        public AsyncRelayCommand AddUpscaleModelCommand { get; }
        public AsyncRelayCommand UpdateUpscaleModelCommand { get; }
        public AsyncRelayCommand RemoveUpscaleModelCommand { get; }

        public AsyncRelayCommand AddPromptInputModelCommand { get; }
        public AsyncRelayCommand UpdatePromptInputModelCommand { get; }
        public AsyncRelayCommand RemovePromptInputModelCommand { get; }
        public AsyncRelayCommand<bool> MovePromptInputModelCommand { get; }

        protected ILogger<SettingsViewBase> Logger => _logger;
        protected IDialogService DialogService => _dialogService;
        protected IModelCacheService ModelCacheService => _modelCacheService;

        public AmuseSettings Settings
        {
            get { return (AmuseSettings)GetValue(SettingsProperty); }
            set { SetValue(SettingsProperty, value); }
        }

        public StableDiffusionModelSetViewModel SelectedStableDiffusionModel
        {
            get { return _selectedStableDiffusionModel; }
            set { _selectedStableDiffusionModel = value; NotifyPropertyChanged(); }
        }

        public ControlNetModelSetViewModel SelectedControlNetModel
        {
            get { return _selectedControlNetModel; }
            set { _selectedControlNetModel = value; NotifyPropertyChanged(); }
        }

        public FeatureExtractorModelSetViewModel SelectedFeatureExtractorModel
        {
            get { return _selectedFeatureExtractorModel; }
            set { _selectedFeatureExtractorModel = value; NotifyPropertyChanged(); }
        }

        public UpscaleModelSetViewModel SelectedUpscaleModel
        {
            get { return _selectedUpscaleModel; }
            set { _selectedUpscaleModel = value; NotifyPropertyChanged(); }
        }

        public PromptInputModel SelectedPromptInputModel
        {
            get { return _selectedPromptInputModel; }
            set { _selectedPromptInputModel = value; NotifyPropertyChanged(); }
        }

        public bool IsSaving
        {
            get { return _isSaving; }
            set { _isSaving = value; NotifyPropertyChanged(); }
        }

        public string SaveStatusMessage
        {
            get { return _saveStatusMessage; }
            set { _saveStatusMessage = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the selected default scheduler type for generation defaults.
        /// Converts between the enum and string stored in settings.
        /// </summary>
        public SchedulerType? SelectedDefaultSchedulerType
        {
            get
            {
                var schedulerName = Settings?.GenerationDefaults?.DefaultSchedulerType;
                if (string.IsNullOrEmpty(schedulerName))
                    return null;

                if (Enum.TryParse<SchedulerType>(schedulerName, out var result))
                    return result;

                return null;
            }
            set
            {
                if (Settings?.GenerationDefaults != null)
                {
                    Settings.GenerationDefaults.DefaultSchedulerType = value?.ToString();
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Shoulds unload model.
        /// </summary>
        /// <returns></returns>
        protected Task<bool> ShouldUnloadModelAsync()
        {
            return _dialogService.ShowMessageDialogAsync("Model In Use", "Would you like to unload model before updating", MessageDialog.MessageDialogType.YesNo, MessageDialog.MessageBoxIconType.Question, MessageDialog.MessageBoxStyleType.Info);
        }


        /// <summary>
        /// Called when Settings changed.
        /// </summary>
        /// <returns></returns>
        protected virtual Task OnSettingsChanged()
        {
            return Task.CompletedTask;
        }


        /// <summary>
        /// Adds the template.
        /// </summary>
        /// <param name="templateName">Name of the template.</param>
        /// <param name="modelTemplate">The model template.</param>
        /// <returns></returns>
        protected ModelTemplateViewModel AddTemplate(string templateName, ModelTemplateViewModel modelTemplate)
        {
            var newModelTemplate = new ModelTemplateViewModel
            {
                Id = Guid.NewGuid(),
                Name = templateName,
                Category = modelTemplate.Category,
                IsInstalled = true,
                Group = ModelTemplateGroup.Custom,
                ImageIcon = string.Empty,
                Author = "Amuse",
                Template = modelTemplate.Template,
                MemoryMax = modelTemplate.MemoryMax,
                MemoryMin = modelTemplate.MemoryMin,
                DownloadSize = modelTemplate.DownloadSize,
            };
            Settings.Templates.Add(newModelTemplate);
            return newModelTemplate;
        }


        /// <summary>
        /// Removes the template.
        /// </summary>
        /// <param name="templateId">The template identifier.</param>
        protected void RemoveTemplate(Guid templateId)
        {
            var template = Settings.Templates.FirstOrDefault(x => x.Id == templateId);
            if (template is null)
                return;

            template.IsInstalled = false;
            if (template.Group == ModelTemplateGroup.Custom)
            {
                Settings.Templates.Remove(template);
            }
        }


        /// <summary>
        /// Saves the Settings file.
        /// </summary>
        /// <returns></returns>
        protected virtual async Task SaveAsync()
        {
            try
            {
                IsSaving = true;
                SaveStatusMessage = "Saving...";
                _logger.LogInformation($"Saving settings to {App.DataDirectory}");

                await Settings.SaveAsync();

                SaveStatusMessage = $"Saved to {App.DataDirectory}";
                _logger.LogInformation("Settings saved successfully");

                // Clear the message after 3 seconds
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    await App.UIInvokeAsync(() =>
                    {
                        if (SaveStatusMessage?.StartsWith("Saved") == true)
                            SaveStatusMessage = string.Empty;
                        return Task.CompletedTask;
                    });
                });
            }
            catch (Exception ex)
            {
                SaveStatusMessage = $"Error: {ex.Message}";
                _logger.LogError($"Error saving configuration file, {ex.Message}");
            }
            finally
            {
                IsSaving = false;
            }
        }

        #region StableDiffusion


        /// <summary>
        /// Adds the stable diffusion model.
        /// </summary>
        protected async Task AddStableDiffusionModel()
        {
            var addModelDialog = _dialogService.GetDialog<AddModelDialog>();
            if (await addModelDialog.ShowDialogAsync())
            {
                var newModelTemplate = AddTemplate(addModelDialog.ModelName, addModelDialog.ModelTemplate);
                newModelTemplate.StableDiffusionTemplate = addModelDialog.ModelTemplate.StableDiffusionTemplate with { };
                newModelTemplate.Repository = addModelDialog.ModelFolder;

                var model = new StableDiffusionModelSetViewModel
                {
                    Id = newModelTemplate.Id,
                    ModelSet = addModelDialog.ModelSetResult,
                    Template = newModelTemplate,
                };
                Settings.StableDiffusionModelSets.Add(model);
                SelectedStableDiffusionModel = model;
                await SaveAsync();
            }
        }


        /// <summary>
        /// Updates the stable diffusion model.
        /// </summary>
        protected async Task UpdateStableDiffusionModel()
        {
            if (SelectedStableDiffusionModel.IsLoaded)
            {
                if (!await ShouldUnloadModelAsync())
                    return;

                await _modelCacheService.UnloadModelAsync(SelectedStableDiffusionModel);
            }

            var updateModelDialog = _dialogService.GetDialog<UpdateModelDialog>();
            if (await updateModelDialog.ShowDialogAsync(SelectedStableDiffusionModel.ModelSet))
            {
                var modelSet = updateModelDialog.ModelSetResult;
                var template = Settings.Templates.FirstOrDefault(x => x.Id == SelectedStableDiffusionModel.Id);
                template.Name = modelSet.Name;
                template.StableDiffusionTemplate.SampleSize = modelSet.SampleSize;
                template.StableDiffusionTemplate.TokenizerLength = modelSet.TokenizerConfig.TokenizerLength;
                template.StableDiffusionTemplate.Tokenizer2Limit = modelSet.Tokenizer2Config?.TokenizerLimit ?? 77;
                template.StableDiffusionTemplate.DiffuserTypes = modelSet.Diffusers.ToArray();
                template.StableDiffusionTemplate.SampleSize = modelSet.SampleSize;
                template.StableDiffusionTemplate.ModelType = modelSet.UnetConfig.ModelType;

                SelectedStableDiffusionModel.ModelSet = modelSet;
                SelectedStableDiffusionModel.NotifyPropertyChanged("Name");
                Settings.StableDiffusionModelSets.ForceNotifyCollectionChanged();
                await SaveAsync();
            }
        }


        /// <summary>
        /// Removes the stable diffusion model.
        /// </summary>
        /// <param name="removeTemplate">if set to <c>true</c> [remove template].</param>
        protected async Task RemoveStableDiffusionModel(bool removeTemplate = true)
        {
            if (SelectedStableDiffusionModel.IsLoaded)
            {

                if (!await ShouldUnloadModelAsync())
                    return;

                await _modelCacheService.UnloadModelAsync(SelectedStableDiffusionModel);
            }

            if (removeTemplate)
                RemoveTemplate(SelectedStableDiffusionModel.Id);

            Settings.StableDiffusionModelSets.Remove(SelectedStableDiffusionModel);
            SelectedStableDiffusionModel = Settings.StableDiffusionModelSets.FirstOrDefault();
            await SaveAsync();
        }

        #endregion

        #region ControlNet


        /// <summary>
        /// Adds the control net model.
        /// </summary>
        protected async Task AddControlNetModel()
        {
            var addModelDialog = _dialogService.GetDialog<AddControlNetModelDialog>();
            if (await addModelDialog.ShowDialogAsync())
            {
                var newModelTemplate = AddTemplate(addModelDialog.ModelName, addModelDialog.ModelTemplate);
                newModelTemplate.ControlNetTemplate = addModelDialog.ControlNetTemplate with { };

                var model = new ControlNetModelSetViewModel
                {
                    Id = newModelTemplate.Id,
                    ModelSet = addModelDialog.ModelSetResult,
                    PipelineTypes = newModelTemplate.ControlNetTemplate.PipelineTypes,
                    Template = newModelTemplate
                };
                Settings.ControlNetModelSets.Add(model);
                SelectedControlNetModel = model;
                await SaveAsync();
            }
        }


        /// <summary>
        /// Updates the control net model.
        /// </summary>
        protected async Task UpdateControlNetModel()
        {
            if (SelectedControlNetModel.IsLoaded)
            {
                if (!await ShouldUnloadModelAsync())
                    return;

                await _modelCacheService.UnloadModelAsync(SelectedControlNetModel);
            }

            var updateModelDialog = _dialogService.GetDialog<UpdateControlNetModelDialog>();
            if (await updateModelDialog.ShowDialogAsync(SelectedControlNetModel.ModelSet, SelectedControlNetModel.PipelineTypes))
            {
                var modelSet = updateModelDialog.ModelSetResult;
                SelectedControlNetModel.ModelSet = modelSet;
                var template = Settings.Templates.FirstOrDefault(x => x.Id == SelectedControlNetModel.Id);
                template.Name = modelSet.Name;
                template.ControlNetTemplate.PipelineTypes = updateModelDialog.SelectedPipelineTypes.ToSelectedList();
                template.ControlNetTemplate.InvertInput = modelSet.InvertInput;
                template.ControlNetTemplate.LayerCount = modelSet.LayerCount;
                template.ControlNetTemplate.DisablePooledProjection = modelSet.DisablePooledProjection;
                SelectedControlNetModel.PipelineTypes = template.ControlNetTemplate.PipelineTypes;
                SelectedControlNetModel.NotifyPropertyChanged("Name");
                await SaveAsync();
            }
        }


        /// <summary>
        /// Removes the control net model.
        /// </summary>
        /// <param name="removeTemplate">if set to <c>true</c> [remove template].</param>
        protected async Task RemoveControlNetModel(bool removeTemplate = true)
        {
            if (SelectedControlNetModel.IsLoaded)
            {
                if (!await ShouldUnloadModelAsync())
                    return;

                await _modelCacheService.UnloadModelAsync(SelectedControlNetModel);
            }

            if (removeTemplate)
                RemoveTemplate(SelectedControlNetModel.Id);

            Settings.ControlNetModelSets.Remove(SelectedControlNetModel);
            SelectedControlNetModel = Settings.ControlNetModelSets.FirstOrDefault();
            await SaveAsync();
        }

        #endregion

        #region Feature Extractor


        /// <summary>
        /// Adds the feature extractor model.
        /// </summary>
        protected async Task AddFeatureExtractorModel()
        {
            var addModelDialog = _dialogService.GetDialog<AddFeatureExtractorModelDialog>();
            if (await addModelDialog.ShowDialogAsync())
            {
                var newModelTemplate = AddTemplate(addModelDialog.ModelName, addModelDialog.ModelTemplate);
                newModelTemplate.FeatureExtractorTemplate = addModelDialog.FeatureExtractorTemplate with { };

                var model = new FeatureExtractorModelSetViewModel
                {
                    Id = newModelTemplate.Id,
                    ModelSet = addModelDialog.ModelSetResult,
                    IsControlNetSupported = newModelTemplate.FeatureExtractorTemplate.IsControlNetSupported,
                    Template = newModelTemplate
                };
                Settings.FeatureExtractorModelSets.Add(model);
                SelectedFeatureExtractorModel = model;
                await SaveAsync();
            }
        }


        /// <summary>
        /// Updates the feature extractor model.
        /// </summary>
        protected async Task UpdateFeatureExtractorModel()
        {
            if (SelectedFeatureExtractorModel.IsLoaded)
            {
                if (!await ShouldUnloadModelAsync())
                    return;

                await _modelCacheService.UnloadModelAsync(SelectedFeatureExtractorModel);
            }

            var updateModelDialog = _dialogService.GetDialog<UpdateFeatureExtractorModelDialog>();
            if (await updateModelDialog.ShowDialogAsync(SelectedFeatureExtractorModel.ModelSet, SelectedFeatureExtractorModel.IsControlNetSupported))
            {
                var modelSet = updateModelDialog.ModelSetResult;
                SelectedFeatureExtractorModel.ModelSet = modelSet;

                var template = Settings.Templates.FirstOrDefault(x => x.Id == SelectedFeatureExtractorModel.Id);
                template.Name = modelSet.Name;
                template.FeatureExtractorTemplate.IsControlNetSupported = updateModelDialog.IsControlNetSupported;
                template.FeatureExtractorTemplate.SampleSize = modelSet.SampleSize;
                template.FeatureExtractorTemplate.InputResizeMode = modelSet.InputResizeMode;
                template.FeatureExtractorTemplate.NormalizeType = modelSet.NormalizeType;
                template.FeatureExtractorTemplate.NormalizeOutputType = modelSet.NormalizeOutputType;
                template.FeatureExtractorTemplate.OutputChannels = modelSet.OutputChannels;
                template.FeatureExtractorTemplate.SetOutputToInputAlpha = modelSet.SetOutputToInputAlpha;
                template.FeatureExtractorTemplate.InvertOutput = modelSet.InvertOutput;
                SelectedFeatureExtractorModel.IsControlNetSupported = updateModelDialog.IsControlNetSupported;
                SelectedFeatureExtractorModel.NotifyPropertyChanged("Name");
                await SaveAsync();
            }
        }


        /// <summary>
        /// Removes the feature extractor model.
        /// </summary>
        /// <param name="removeTemplate">if set to <c>true</c> [remove template].</param>
        protected async Task RemoveFeatureExtractorModel(bool removeTemplate = true)
        {
            if (SelectedFeatureExtractorModel.IsLoaded)
            {
                if (!await ShouldUnloadModelAsync())
                    return;

                await _modelCacheService.UnloadModelAsync(SelectedFeatureExtractorModel);
            }

            if (removeTemplate)
                RemoveTemplate(SelectedFeatureExtractorModel.Id);

            Settings.FeatureExtractorModelSets.Remove(SelectedFeatureExtractorModel);
            SelectedFeatureExtractorModel = Settings.FeatureExtractorModelSets.FirstOrDefault();
            await SaveAsync();
        }

        #endregion

        #region Upscale


        /// <summary>
        /// Adds the upscale model.
        /// </summary>
        protected async Task AddUpscaleModel()
        {
            var addModelDialog = _dialogService.GetDialog<AddUpscaleModelDialog>();
            if (await addModelDialog.ShowDialogAsync())
            {
                var newModelTemplate = AddTemplate(addModelDialog.ModelName, addModelDialog.ModelTemplate);
                newModelTemplate.UpscaleTemplate = addModelDialog.UpscaleTemplate with { };

                var model = new UpscaleModelSetViewModel
                {
                    Id = newModelTemplate.Id,
                    ModelSet = addModelDialog.ModelSetResult,
                    Template = newModelTemplate
                };
                Settings.UpscaleModelSets.Add(model);
                SelectedUpscaleModel = model;
                await SaveAsync();
            }
        }


        /// <summary>
        /// Updates the upscale model.
        /// </summary>
        protected async Task UpdateUpscaleModel()
        {
            if (SelectedUpscaleModel.IsLoaded)
            {
                if (!await ShouldUnloadModelAsync())
                    return;

                await _modelCacheService.UnloadModelAsync(SelectedUpscaleModel);
            }

            var updateModelDialog = _dialogService.GetDialog<UpdateUpscaleModelDialog>();
            if (await updateModelDialog.ShowDialogAsync(SelectedUpscaleModel.ModelSet))
            {
                var modelSet = updateModelDialog.ModelSetResult;
                SelectedUpscaleModel.ModelSet = modelSet;
                var template = Settings.Templates.FirstOrDefault(x => x.Id == SelectedUpscaleModel.Id);
                template.Name = modelSet.Name;
                template.UpscaleTemplate.SampleSize = modelSet.SampleSize;
                template.UpscaleTemplate.ScaleFactor = modelSet.ScaleFactor;
                template.UpscaleTemplate.Channels = modelSet.Channels;
                template.UpscaleTemplate.NormalizeType = modelSet.NormalizeType;
                template.UpscaleTemplate.TileMode = modelSet.TileMode;
                template.UpscaleTemplate.TileSize = modelSet.TileSize;
                template.UpscaleTemplate.TileOverlap = modelSet.TileOverlap;
                SelectedUpscaleModel.NotifyPropertyChanged("Name");
                await SaveAsync();
            }
        }


        /// <summary>
        /// Removes the upscale model.
        /// </summary>
        /// <param name="removeTemplate">if set to <c>true</c> [remove template].</param>
        protected async Task RemoveUpscaleModel(bool removeTemplate = true)
        {
            if (SelectedUpscaleModel.IsLoaded)
            {
                if (!await ShouldUnloadModelAsync())
                    return;

                await _modelCacheService.UnloadModelAsync(SelectedUpscaleModel);
            }

            if (removeTemplate)
                RemoveTemplate(SelectedUpscaleModel.Id);

            Settings.UpscaleModelSets.Remove(SelectedUpscaleModel);
            SelectedUpscaleModel = Settings.UpscaleModelSets.FirstOrDefault();
            await SaveAsync();
        }

        #endregion

        #region Prompts

        private async Task AddPromptInputModel()
        {
            var promptDialog = DialogService.GetDialog<AddPromptInputDialog>();
            if (await promptDialog.ShowDialogAsync())
            {
                SelectedPromptInputModel = Settings.Prompts.FirstOrDefault();
                await SaveAsync();
            }
        }

        private async Task UpdatePromptInputModel()
        {
            var promptDialog = DialogService.GetDialog<AddPromptInputDialog>();
            if (await promptDialog.ShowDialogAsync(SelectedPromptInputModel))
                await SaveAsync();
        }


        private async Task RemovePromptInputModel()
        {
            if (await DialogService.ShowMessageDialogAsync("Remove Saved Prompt", "Are you sure you want to remove this saved prompt?", MessageDialog.MessageDialogType.YesNo, MessageDialog.MessageBoxIconType.Question, MessageDialog.MessageBoxStyleType.Info))
            {
                Settings.Prompts.Remove(SelectedPromptInputModel);
                SelectedPromptInputModel = Settings.Prompts.FirstOrDefault();
                await SaveAsync();
            }
        }

        private async Task MovePromptInputModel(bool moveUp)
        {
            var index = Settings.Prompts.IndexOf(SelectedPromptInputModel);
            var newIndex = moveUp
                ? Math.Max(0, index - 1)
                : Math.Min(Settings.Prompts.Count - 1, index + 1);

            if (index == newIndex)
                return;

            Settings.Prompts.Move(index, newIndex);
            await SaveAsync();
        }


        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
        #endregion
    }
}
