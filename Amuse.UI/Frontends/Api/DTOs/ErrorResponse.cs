namespace Amuse.UI.Frontends.Api.DTOs
{
    /// <summary>
    /// Standard error response DTO.
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// Error message.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Error code for programmatic handling.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Additional details about the error.
        /// </summary>
        public string Details { get; set; }

        public static ErrorResponse NotFound(string message = "Resource not found")
            => new() { Error = message, Code = "NOT_FOUND" };

        public static ErrorResponse BadRequest(string message, string details = null)
            => new() { Error = message, Code = "BAD_REQUEST", Details = details };

        public static ErrorResponse InternalError(string message = "An internal error occurred")
            => new() { Error = message, Code = "INTERNAL_ERROR" };

        public static ErrorResponse JobNotComplete(string status)
            => new() { Error = $"Job is not complete. Current status: {status}", Code = "JOB_NOT_COMPLETE" };
    }
}
