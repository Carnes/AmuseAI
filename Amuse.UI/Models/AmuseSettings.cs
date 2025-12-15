using Amuse.UI.Enums;
using Amuse.UI.Helpers;
using Amuse.UI.Models.FeatureExtractor;
using Amuse.UI.Models.StableDiffusion;
using Amuse.UI.Models.Upscale;
using OnnxStack.Common.Config;
using OnnxStack.Device.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Amuse.UI.Models
{
    public class AmuseSettings : IConfigSection, IHardwareSettings, INotifyPropertyChanged
    {
        private string _defualtModelDirectory;
        private Device _defaultExecutionDevice;
        private StretchDirection _defaultZoomDirection = StretchDirection.Both;
        private bool _isUpdateEnabled = true;
        private bool _isModelEvaluationModeEnabled;
        private bool _isPreviewEnabled;
        private int? _defaultDeviceId;
        private ExecutionProvider? _defaultExecutionProvider;

        [JsonIgnore]
        public int ProcessId { get; set; } = Environment.ProcessId;

        [AppDefault]
        public string FileVersion { get; init; }

        [AppDefault]
        public bool HasExited { get; set; }
        public bool IsUpdateEnabled
        {
            get { return _isUpdateEnabled; }
            set { _isUpdateEnabled = value; NotifyPropertyChanged(); }
        }

        public ModelCacheMode ModelCacheMode { get; set; }
        public RenderMode RenderMode { get; set; } = RenderMode.SoftwareOnly;
        public bool UseLegacyDeviceDetection { get; set; }
        public bool AutoSaveImage { get; set; }
        public bool AutoSaveVideo { get; set; }
        public bool IsPreviewEnabled
        {
            get { return _isPreviewEnabled; }
            set { _isPreviewEnabled = value; NotifyPropertyChanged(); }
        }
        public string DirectoryModel { get; set; }
        public string DirectoryImage { get; set; }
        public string DirectoryImageSave { get; set; }
        public string DirectoryImageAutoSave { get; set; }
        public string DirectoryVideo { get; set; }
        public string DirectoryVideoSave { get; set; }
        public string DirectoryVideoAutoSave { get; set; }
        public int BatchDelay { get; set; } = 500;
        public int RealtimeRefreshRate { get; set; } = 100;
        public bool RealtimeHistoryEnabled { get; set; } = true;
        public int HistoryMaxItems { get; set; } = 5000;

        public StretchDirection DefaultZoomDirection
        {
            get { return _defaultZoomDirection; }
            set { _defaultZoomDirection = value; NotifyPropertyChanged(); }
        }

        public List<ExecutionProvider> SupportedProviders { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int? DefaultDeviceId
        {
            get { return _defaultDeviceId; }
            set { _defaultDeviceId = value; NotifyPropertyChanged(); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public ExecutionProvider? DefaultProvider
        {
            get { return _defaultExecutionProvider; }
            set { _defaultExecutionProvider = value; NotifyPropertyChanged(); }
        }

        public bool IsModelEvaluationModeEnabled
        {
            get { return _isModelEvaluationModeEnabled; }
            set
            {
                _isModelEvaluationModeEnabled = value;
                if (!_isModelEvaluationModeEnabled)
                    IsPreviewEnabled = false;

                NotifyPropertyChanged();
            }
        }

        public ObservableCollection<PromptInputModel> Prompts { get; set; } = new ObservableCollection<PromptInputModel>();

        [AppDefault]
        public ObservableCollection<ModelTemplateViewModel> Templates { get; set; } = new ObservableCollection<ModelTemplateViewModel>();
        public ObservableCollection<UpscaleModelSetViewModel> UpscaleModelSets { get; set; } = new ObservableCollection<UpscaleModelSetViewModel>();
        public ObservableCollection<StableDiffusionModelSetViewModel> StableDiffusionModelSets { get; set; } = new ObservableCollection<StableDiffusionModelSetViewModel>();
        public ObservableCollection<ControlNetModelSetViewModel> ControlNetModelSets { get; set; } = new ObservableCollection<ControlNetModelSetViewModel>();
        public ObservableCollection<FeatureExtractorModelSetViewModel> FeatureExtractorModelSets { get; set; } = new ObservableCollection<FeatureExtractorModelSetViewModel>();

        [JsonIgnore]
        [AppDefault]
        public Device DefaultExecutionDevice
        {
            get { return _defaultExecutionDevice; }
            set { _defaultExecutionDevice = value; NotifyPropertyChanged(); }
        }


        public void Initialize()
        {
            _defualtModelDirectory = Path.Combine(App.DataDirectory, "Models");
            if (string.IsNullOrEmpty(DirectoryModel))
                DirectoryModel = _defualtModelDirectory;

            // Internal FeatureExtractors
            Utils.AddInternalFeatureExtractors(this);

            foreach (var template in Templates.Where(x => x.Group != ModelTemplateGroup.Fixed))
            {
                template.IsInstalled = UpscaleModelSets.Any(x => x.Id == template.Id && template.Category == ModelTemplateCategory.Upscaler)
                                  || ControlNetModelSets.Any(x => x.Id == template.Id && template.Category == ModelTemplateCategory.ControlNet)
                                  || StableDiffusionModelSets.Any(x => x.Id == template.Id && template.Category == ModelTemplateCategory.StableDiffusion)
                                  || FeatureExtractorModelSets.Any(x => x.Id == template.Id && template.Category == ModelTemplateCategory.FeatureExtractor);

                if (!template.IsInstalled && template.IsUpdateAvailable)
                    template.IsUpdateAvailable = false;
            }

            foreach (var modelSet in UpscaleModelSets)
                modelSet.Template = Templates.FirstOrDefault(x => x.Category == ModelTemplateCategory.Upscaler && x.Id == modelSet.Id);
            foreach (var modelSet in ControlNetModelSets)
                modelSet.Template = Templates.FirstOrDefault(x => x.Category == ModelTemplateCategory.ControlNet && x.Id == modelSet.Id);
            foreach (var modelSet in StableDiffusionModelSets)
                modelSet.Template = Templates.FirstOrDefault(x => x.Category == ModelTemplateCategory.StableDiffusion && x.Id == modelSet.Id);
            foreach (var modelSet in FeatureExtractorModelSets)
                modelSet.Template = Templates.FirstOrDefault(x => x.Category == ModelTemplateCategory.FeatureExtractor && x.Id == modelSet.Id);
        }


        public List<string> GetModelNames(ModelTemplateCategory category)
        {
            return Templates
                .Where(x => x.Group != ModelTemplateGroup.Fixed && x.Category == category)
                .Select(x => x.Name)
                .Distinct()
                .ToList();
        }


        public Task SaveAsync()
        {
            if (DirectoryModel == _defualtModelDirectory)
                DirectoryModel = string.Empty;

            SettingsManager.SaveSettings(this);
            if (string.IsNullOrEmpty(DirectoryModel))
                DirectoryModel = _defualtModelDirectory;

            return Task.CompletedTask;
        }


        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        #endregion
    }
}
