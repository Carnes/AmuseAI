using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Amuse.UI.Core.Models
{
    /// <summary>
    /// Represents a generation job in the queue.
    /// Used by all frontends (UI, API, Discord, etc.) to submit and track generation requests.
    /// </summary>
    public class GenerationJob : INotifyPropertyChanged
    {
        private JobStatus _status;
        private DateTime? _startedAt;
        private DateTime? _completedAt;
        private int _progress;
        private string _progressMessage;
        private string _errorMessage;

        /// <summary>
        /// Unique identifier for this job.
        /// </summary>
        public Guid Id { get; init; }

        /// <summary>
        /// Type of generation (TextToImage, ImageToImage, Upscale, etc.).
        /// </summary>
        public JobType Type { get; init; }

        /// <summary>
        /// Current status of the job.
        /// </summary>
        public JobStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Source frontend that created this job (e.g., "UI", "API", "Discord").
        /// Used for filtering and display purposes.
        /// </summary>
        public string Source { get; init; }

        /// <summary>
        /// When the job was created/queued.
        /// </summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>
        /// When the job started processing (null if still pending).
        /// </summary>
        public DateTime? StartedAt
        {
            get => _startedAt;
            set { _startedAt = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// When the job completed or failed (null if still pending/processing).
        /// </summary>
        public DateTime? CompletedAt
        {
            get => _completedAt;
            set { _completedAt = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Current progress percentage (0-100).
        /// </summary>
        public int Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Current progress message (e.g., "Loading model...", "Step 15/30").
        /// </summary>
        public string ProgressMessage
        {
            get => _progressMessage;
            set { _progressMessage = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Error message if the job failed.
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// The request data for this job (type depends on JobType).
        /// Cast to appropriate request type based on JobType.
        /// </summary>
        public object RequestData { get; init; }

        /// <summary>
        /// TaskCompletionSource for async result retrieval.
        /// Completes when job finishes (success or failure).
        /// </summary>
        public TaskCompletionSource<GenerationJobResult> CompletionSource { get; init; }

        /// <summary>
        /// Cancellation token source for this job.
        /// Used to cancel in-progress generation.
        /// </summary>
        public System.Threading.CancellationTokenSource CancellationTokenSource { get; init; }

        /// <summary>
        /// Creates a new pending job with default values.
        /// </summary>
        public static GenerationJob Create(JobType type, string source, object requestData)
        {
            return new GenerationJob
            {
                Id = Guid.NewGuid(),
                Type = type,
                Status = JobStatus.Pending,
                Source = source,
                CreatedAt = DateTime.Now,
                RequestData = requestData,
                CompletionSource = new TaskCompletionSource<GenerationJobResult>(),
                CancellationTokenSource = new System.Threading.CancellationTokenSource()
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
