using System;
using UnityEngine;

namespace Jusvibes.Core
{
    [Serializable]
    public class PipelineMetrics
    {
        public long totalDurationMs;
        public long captureDurationMs;
        public long openAiDurationMs;
        public long sunoDurationMs;
        public long audioDurationMs;
        public int retryCount;
        public bool success;
        public string errorMessage;
        public VisualInsights visualInsights;
    }
}
