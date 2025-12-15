using Amuse.UI.Commands;
using Amuse.UI.Helpers;
using Amuse.UI.Models;
using Amuse.UI.Models.FeatureExtractor;
using Amuse.UI.Models.StableDiffusion;
using Amuse.UI.Models.Upscale;
using OnnxStack.Core;
using OnnxStack.StableDiffusion.Enums;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Amuse.UI.UserControls
{
    /// <summary>
    /// Interaction logic for StableDiffusionPickerControl.xaml
    /// </summary>
    public partial class StableDiffusionPickerControl : UserControl, INotifyPropertyChanged
    {
        private ICollectionView _modelCollectionView;
        private ICollectionView _controlNetModelCollectionView;
        private ICollectionView _upscaleModelCollectionView;
        private ICollectionView _featureExtractorModelCollectionView;

        /// <summary>Initializes a new instance of the <see cref="StableDiffusionPickerControl" /> class.</summary>
        public StableDiffusionPickerControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty SettingsProperty =
            DependencyProperty.Register(nameof(Settings), typeof(AmuseSettings), typeof(StableDiffusionPickerControl), new PropertyMetadata<StableDiffusionPickerControl>(x => x.OnSettingsChanged()));

        public static readonly DependencyProperty SupportedDiffusersProperty =
            DependencyProperty.Register(nameof(SupportedDiffusers), typeof(List<DiffuserType>), typeof(StableDiffusionPickerControl), new PropertyMetadata<StableDiffusionPickerControl>(x => x.OnSupportedDiffusersChanged()));

        public static readonly DependencyProperty SelectedBaseModelProperty =
            DependencyProperty.Register(nameof(SelectedBaseModel), typeof(StableDiffusionModelSetViewModel), typeof(StableDiffusionPickerControl), new PropertyMetadata<StableDiffusionPickerControl>(x => x.OnSelectedModelChanged()));

        public static readonly DependencyProperty SelectedControlNetModelProperty =
            DependencyProperty.Register(nameof(SelectedControlNetModel), typeof(ControlNetModelSetViewModel), typeof(StableDiffusionPickerControl));

        public static readonly DependencyProperty SelectedUpscaleModelProperty =
            DependencyProperty.Register(nameof(SelectedUpscaleModel), typeof(UpscaleModelSetViewModel), typeof(StableDiffusionPickerControl));

        public static readonly DependencyProperty SelectedFeatureExtractorModelProperty =
            DependencyProperty.Register(nameof(SelectedFeatureExtractorModel), typeof(FeatureExtractorModelSetViewModel), typeof(StableDiffusionPickerControl));

        public static readonly DependencyProperty SelectedVariantProperty =
            DependencyProperty.Register(nameof(SelectedVariant), typeof(string), typeof(StableDiffusionPickerControl));

        public static readonly DependencyProperty IsUpscalerEnabledProperty =
            DependencyProperty.Register(nameof(IsUpscalerEnabled), typeof(bool), typeof(StableDiffusionPickerControl), new PropertyMetadata<StableDiffusionPickerControl>(x => x.OnIsUpscaleEnabledChanged()));

        public static readonly DependencyProperty IsControlNetEnabledProperty =
            DependencyProperty.Register(nameof(IsControlNetEnabled), typeof(bool), typeof(StableDiffusionPickerControl), new PropertyMetadata<StableDiffusionPickerControl>(x => x.OnIsControlNetEnabledChanged()));

        public static readonly DependencyProperty IsFeatureExtractorEnabledProperty =
            DependencyProperty.Register(nameof(IsFeatureExtractorEnabled), typeof(bool), typeof(StableDiffusionPickerControl), new PropertyMetadata<StableDiffusionPickerControl>(x => x.OnIsFeatureExtractorEnabledChanged()));

        public static readonly DependencyProperty IsPipelineLoadingProperty =
             DependencyProperty.Register(nameof(IsPipelineLoading), typeof(bool), typeof(StableDiffusionPickerControl));

        public static readonly DependencyProperty LoadPipelineCommandProperty =
            DependencyProperty.Register(nameof(LoadPipelineCommand), typeof(AsyncRelayCommand), typeof(StableDiffusionPickerControl));

        public static readonly DependencyProperty UnloadPipelineCommandProperty =
            DependencyProperty.Register(nameof(UnloadPipelineCommand), typeof(AsyncRelayCommand), typeof(StableDiffusionPickerControl));

        public static readonly DependencyProperty IsControlNetSupportedProperty =
            DependencyProperty.Register(nameof(IsControlNetSupported), typeof(bool), typeof(StableDiffusionPickerControl));

        public static readonly DependencyProperty IsFeatureExtractorSupportedProperty =
             DependencyProperty.Register(nameof(IsFeatureExtractorSupported), typeof(bool), typeof(StableDiffusionPickerControl));

        public static readonly DependencyProperty IsUpscalerSupportedProperty =
            DependencyProperty.Register(nameof(IsUpscalerSupported), typeof(bool), typeof(StableDiffusionPickerControl));

        public static readonly DependencyProperty CurrentPipelineProperty =
            DependencyProperty.Register(nameof(CurrentPipeline), typeof(StableDiffusionPipelineModel), typeof(StableDiffusionPickerControl));

        public static readonly DependencyProperty IsVideoModelPickerProperty =
            DependencyProperty.Register(nameof(IsVideoModelPicker), typeof(bool), typeof(StableDiffusionPickerControl));

        /// <summary>
        /// Gets or sets the settings.
        /// </summary>
        public AmuseSettings Settings
        {
            get { return (AmuseSettings)GetValue(SettingsProperty); }
            set { SetValue(SettingsProperty, value); }
        }

        /// <summary>
        /// Gets or sets the supported diffusers.
        /// </summary>
        public List<DiffuserType> SupportedDiffusers
        {
            get { return (List<DiffuserType>)GetValue(SupportedDiffusersProperty); }
            set { SetValue(SupportedDiffusersProperty, value); }
        }

        /// <summary>
        /// Gets or sets the selected model.
        /// </summary>
        public StableDiffusionModelSetViewModel SelectedBaseModel
        {
            get { return (StableDiffusionModelSetViewModel)GetValue(SelectedBaseModelProperty); }
            set { SetValue(SelectedBaseModelProperty, value); }
        }

        /// <summary>
        /// Gets or sets the selected ControlNet model.
        /// </summary>
        public ControlNetModelSetViewModel SelectedControlNetModel
        {
            get { return (ControlNetModelSetViewModel)GetValue(SelectedControlNetModelProperty); }
            set { SetValue(SelectedControlNetModelProperty, value); }
        }

        /// <summary>
        /// Gets or sets the selected upscale model.
        /// </summary>
        public UpscaleModelSetViewModel SelectedUpscaleModel
        {
            get { return (UpscaleModelSetViewModel)GetValue(SelectedUpscaleModelProperty); }
            set { SetValue(SelectedUpscaleModelProperty, value); }
        }

        /// <summary>
        /// Gets or sets the selected feature extractor model.
        /// </summary>
        public FeatureExtractorModelSetViewModel SelectedFeatureExtractorModel
        {
            get { return (FeatureExtractorModelSetViewModel)GetValue(SelectedFeatureExtractorModelProperty); }
            set { SetValue(SelectedFeatureExtractorModelProperty, value); }
        }

        /// <summary>
        /// Gets or sets the selected variant.
        /// </summary>
        public string SelectedVariant
        {
            get { return (string)GetValue(SelectedVariantProperty); }
            set { SetValue(SelectedVariantProperty, value); }
        }

        /// <summary>
        /// Gets or sets the current pipeline.
        /// </summary>
        public StableDiffusionPipelineModel CurrentPipeline
        {
            get { return (StableDiffusionPipelineModel)GetValue(CurrentPipelineProperty); }
            set { SetValue(CurrentPipelineProperty, value); }
        }

        /// <summary>
        /// Gets or sets the model collection view.
        /// </summary>
        public ICollectionView ModelCollectionView
        {
            get { return _modelCollectionView; }
            set { _modelCollectionView = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the ControlNet model collection view.
        /// </summary>
        public ICollectionView ControlNetModelCollectionView
        {
            get { return _controlNetModelCollectionView; }
            set { _controlNetModelCollectionView = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the upscale model collection view.
        /// </summary>
        public ICollectionView UpscaleModelCollectionView
        {
            get { return _upscaleModelCollectionView; }
            set { _upscaleModelCollectionView = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the feature extractor model collection view.
        /// </summary>
        public ICollectionView FeatureExtractorModelCollectionView
        {
            get { return _featureExtractorModelCollectionView; }
            set { _featureExtractorModelCollectionView = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether upscale is enabled.
        /// </summary>
        public bool IsUpscalerEnabled
        {
            get { return (bool)GetValue(IsUpscalerEnabledProperty); }
            set { SetValue(IsUpscalerEnabledProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether control net is enabled.
        /// </summary>
        public bool IsControlNetEnabled
        {
            get { return (bool)GetValue(IsControlNetEnabledProperty); }
            set { SetValue(IsControlNetEnabledProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether feature extractor is enabled.
        /// </summary>
        public bool IsFeatureExtractorEnabled
        {
            get { return (bool)GetValue(IsFeatureExtractorEnabledProperty); }
            set { SetValue(IsFeatureExtractorEnabledProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the pipeline is loading.
        /// </summary>
        public bool IsPipelineLoading
        {
            get { return (bool)GetValue(IsPipelineLoadingProperty); }
            set { SetValue(IsPipelineLoadingProperty, value); }
        }

        /// <summary>
        /// Gets or sets the load pipeline command.
        /// </summary>
        public AsyncRelayCommand LoadPipelineCommand
        {
            get { return (AsyncRelayCommand)GetValue(LoadPipelineCommandProperty); }
            set { SetValue(LoadPipelineCommandProperty, value); }
        }

        /// <summary>
        /// Gets or sets the unload pipeline command.
        /// </summary>
        public AsyncRelayCommand UnloadPipelineCommand
        {
            get { return (AsyncRelayCommand)GetValue(UnloadPipelineCommandProperty); }
            set { SetValue(UnloadPipelineCommandProperty, value); }
        }

        /// <summary>
        /// Gets a value indicating whether this view supports ControlNet.
        /// </summary>
        public bool IsControlNetSupported
        {
            get { return (bool)GetValue(IsControlNetSupportedProperty); }
            set { SetValue(IsControlNetSupportedProperty, value); }
        }

        /// <summary>
        /// Gets a value indicating whether this view supports FeatureExtractors.
        /// </summary>
        public bool IsFeatureExtractorSupported
        {
            get { return (bool)GetValue(IsFeatureExtractorSupportedProperty); }
            set { SetValue(IsFeatureExtractorSupportedProperty, value); }
        }

        /// <summary>
        /// Gets a value indicating whether this view supports upscalers.
        /// </summary>
        public bool IsUpscalerSupported
        {
            get { return (bool)GetValue(IsUpscalerSupportedProperty); }
            set { SetValue(IsUpscalerSupportedProperty, value); }
        }

        public bool IsVideoModelPicker
        {
            get { return (bool)GetValue(IsVideoModelPickerProperty); }
            set { SetValue(IsVideoModelPickerProperty, value); }
        }


        /// <summary>
        /// Called when Settings has changed.
        /// </summary>
        private Task OnSettingsChanged()
        {
            // Base Models
            ModelCollectionView = new ListCollectionView(Settings.StableDiffusionModelSets);
            ModelCollectionView.Filter = (obj) =>
            {
                if (obj is not StableDiffusionModelSetViewModel viewModel)
                    return false;

                if (SupportedDiffusers.IsNullOrEmpty())
                    return false;

                if (viewModel.IsVideo != IsVideoModelPicker)
                    return false;

                return viewModel.ModelSet.Diffusers.Intersect(SupportedDiffusers).Any();
            };

            //ControlNet models
            ControlNetModelCollectionView = new ListCollectionView(Settings.ControlNetModelSets);
            ControlNetModelCollectionView.Filter = (obj) =>
            {
                if (obj is not ControlNetModelSetViewModel viewModel)
                    return false;

                if (SelectedBaseModel is null)
                    return false;

                if (!SelectedBaseModel.IsControlNet)
                    return false;

                return viewModel.PipelineTypes.Contains(SelectedBaseModel.ModelSet.PipelineType);
            };

            //FeatureExtractor models
            FeatureExtractorModelCollectionView = new ListCollectionView(Settings.FeatureExtractorModelSets);
            FeatureExtractorModelCollectionView.Filter = (obj) =>
            {
                if (obj is not FeatureExtractorModelSetViewModel viewModel)
                    return false;

                return viewModel.IsControlNetSupported;
            };

            //Upscale models
            UpscaleModelCollectionView = new ListCollectionView(Settings.UpscaleModelSets);
            UpscaleModelCollectionView.Filter = (obj) =>
            {
                if (obj is not UpscaleModelSetViewModel viewModel)
                    return false;

                return true;
            };

            return Task.CompletedTask;
        }


        /// <summary>
        /// Called when SelectedModel has changed.
        /// </summary>
        private Task OnSupportedDiffusersChanged()
        {
            ModelCollectionView?.Refresh();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Called when SupportedDiffusers has changed.
        /// </summary>
        private Task OnSelectedModelChanged()
        {
            ControlNetModelCollectionView?.Refresh();
            FeatureExtractorModelCollectionView?.Refresh();
            UpscaleModelCollectionView?.Refresh();
            if (SelectedBaseModel is null)
                return Task.CompletedTask;

            SelectedVariant = SelectedBaseModel.Variant;
            SelectedControlNetModel = !SelectedBaseModel.IsControlNet || !IsControlNetSupported
                 ? default
                 : Settings.ControlNetModelSets.FirstOrDefault(x => x.IsLoaded && x.PipelineTypes.Contains(SelectedBaseModel.ModelSet.PipelineType));
            IsControlNetEnabled = SelectedControlNetModel is not null;
            return Task.CompletedTask;
        }


        /// <summary>
        /// Called when IsUpscaleEnabled changed.
        /// </summary>
        private Task OnIsUpscaleEnabledChanged()
        {
            if (!IsUpscalerEnabled)
            {
                SelectedUpscaleModel = null;
                return Task.CompletedTask;
            }

            UpscaleModelCollectionView.MoveCurrentToFirst();
            SelectedUpscaleModel = (UpscaleModelSetViewModel)UpscaleModelCollectionView.CurrentItem;
            return Task.CompletedTask;
        }


        /// <summary>
        /// Called when IsControlNetEnabled changed.
        /// </summary>
        private Task OnIsControlNetEnabledChanged()
        {
            if (!IsControlNetEnabled)
            {
                SelectedControlNetModel = null;
                return Task.CompletedTask;
            }

            ControlNetModelCollectionView.MoveCurrentToFirst();
            SelectedControlNetModel = (ControlNetModelSetViewModel)ControlNetModelCollectionView.CurrentItem;
            return Task.CompletedTask;
        }


        /// <summary>
        /// Called when IsFeatureExtractorEnabled changed.
        /// </summary>
        private Task OnIsFeatureExtractorEnabledChanged()
        {
            if (!IsFeatureExtractorEnabled)
            {
                SelectedFeatureExtractorModel = null;
                return Task.CompletedTask;
            }

            FeatureExtractorModelCollectionView.MoveCurrentToFirst();
            SelectedFeatureExtractorModel = (FeatureExtractorModelSetViewModel)FeatureExtractorModelCollectionView.CurrentItem;
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
