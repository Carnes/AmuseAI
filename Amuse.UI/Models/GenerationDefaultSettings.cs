using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Amuse.UI.Models
{
    /// <summary>
    /// Default generation settings used by API (when parameters omitted) and UI (initial values).
    /// </summary>
    public class GenerationDefaultSettings : INotifyPropertyChanged
    {
        private string _defaultModelName;
        private string _defaultNegativePrompt;
        private int _defaultWidth = 512;
        private int _defaultHeight = 512;
        private int _defaultSteps = 30;
        private float _defaultGuidanceScale = 7.5f;
        private string _defaultSchedulerType;
        private string _defaultUpscaleModelName;
        private int _defaultScaleFactor = 2;

        /// <summary>
        /// Default model to use when not specified.
        /// If empty, the first available model will be used.
        /// </summary>
        public string DefaultModelName
        {
            get => _defaultModelName;
            set { _defaultModelName = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Default negative prompt to use when not specified.
        /// </summary>
        public string DefaultNegativePrompt
        {
            get => _defaultNegativePrompt;
            set { _defaultNegativePrompt = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Default image width in pixels.
        /// </summary>
        public int DefaultWidth
        {
            get => _defaultWidth;
            set { _defaultWidth = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Default image height in pixels.
        /// </summary>
        public int DefaultHeight
        {
            get => _defaultHeight;
            set { _defaultHeight = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Default number of inference steps.
        /// </summary>
        public int DefaultSteps
        {
            get => _defaultSteps;
            set { _defaultSteps = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Default guidance scale for classifier-free guidance.
        /// </summary>
        public float DefaultGuidanceScale
        {
            get => _defaultGuidanceScale;
            set { _defaultGuidanceScale = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Default scheduler type (e.g., "EulerAncestral", "DDPM").
        /// If empty, the model's default scheduler will be used.
        /// </summary>
        public string DefaultSchedulerType
        {
            get => _defaultSchedulerType;
            set { _defaultSchedulerType = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Default upscale model to use when not specified.
        /// If empty, the first available upscale model will be used.
        /// </summary>
        public string DefaultUpscaleModelName
        {
            get => _defaultUpscaleModelName;
            set { _defaultUpscaleModelName = value; NotifyPropertyChanged(); }
        }

        /// <summary>
        /// Default scale factor for upscaling (e.g., 2 for 2x, 4 for 4x).
        /// </summary>
        public int DefaultScaleFactor
        {
            get => _defaultScaleFactor;
            set { _defaultScaleFactor = value; NotifyPropertyChanged(); }
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
