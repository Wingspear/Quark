using System;

namespace Jusvibes.Core
{
    public class PipelineException : Exception
    {
        public string Stage { get; }
        public string CorrelationId { get; }

        public PipelineException(string stage, string message, string correlationId = null, Exception innerException = null)
            : base($"[{stage}] {message}", innerException)
        {
            Stage = stage;
            CorrelationId = correlationId;
        }
    }

    public class CaptureException : PipelineException
    {
        public CaptureException(string message, string correlationId = null, Exception innerException = null)
            : base("Capture", message, correlationId, innerException)
        {
        }
    }

    public class OpenAIException : PipelineException
    {
        public int? StatusCode { get; }

        public OpenAIException(string message, int? statusCode = null, string correlationId = null, Exception innerException = null)
            : base("OpenAI", message, correlationId, innerException)
        {
            StatusCode = statusCode;
        }
    }

    public class SunoApiException : PipelineException
    {
        public int? StatusCode { get; }
        public string TaskId { get; }

        public SunoApiException(string message, int? statusCode = null, string taskId = null, string correlationId = null, Exception innerException = null)
            : base("SunoAPI", message, correlationId, innerException)
        {
            StatusCode = statusCode;
            TaskId = taskId;
        }
    }

    public class PipelineTimeoutException : PipelineException
    {
        public long ElapsedMs { get; }

        public PipelineTimeoutException(string stage, long elapsedMs, string correlationId = null)
            : base(stage, $"Operation timed out after {elapsedMs}ms", correlationId)
        {
            ElapsedMs = elapsedMs;
        }
    }

    public class AudioStreamException : PipelineException
    {
        public string Url { get; }

        public AudioStreamException(string message, string url = null, string correlationId = null, Exception innerException = null)
            : base("AudioStream", message, correlationId, innerException)
        {
            Url = url;
        }
    }
}
