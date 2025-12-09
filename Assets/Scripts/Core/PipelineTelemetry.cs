using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jusvibes.Core
{
    /// <summary>
    /// Tracks and aggregates telemetry data across pipeline executions
    /// </summary>
    public class PipelineTelemetry : MonoBehaviour
    {
        private static PipelineTelemetry _instance;
        public static PipelineTelemetry Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("PipelineTelemetry");
                    _instance = go.AddComponent<PipelineTelemetry>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Serializable]
        public class TelemetryStats
        {
            public int totalExecutions;
            public int successfulExecutions;
            public int failedExecutions;
            public long totalDurationMs;
            public long avgCaptureDurationMs;
            public long avgOpenAiDurationMs;
            public long avgSunoDurationMs;
            public Dictionary<string, int> errorCounts = new Dictionary<string, int>();
        }

        private TelemetryStats _stats = new TelemetryStats();
        private List<PipelineMetrics> _recentExecutions = new List<PipelineMetrics>();
        private const int MaxRecentExecutions = 50;

        public void RecordExecution(PipelineMetrics metrics)
        {
            _stats.totalExecutions++;

            if (metrics.success)
            {
                _stats.successfulExecutions++;
            }
            else
            {
                _stats.failedExecutions++;

                // Track error types
                if (!string.IsNullOrEmpty(metrics.errorMessage))
                {
                    var errorType = metrics.errorMessage.Split(':')[0];
                    if (_stats.errorCounts.ContainsKey(errorType))
                        _stats.errorCounts[errorType]++;
                    else
                        _stats.errorCounts[errorType] = 1;
                }
            }

            // Update averages
            _stats.totalDurationMs += metrics.totalDurationMs;
            _stats.avgCaptureDurationMs = (_stats.avgCaptureDurationMs * (_stats.totalExecutions - 1) + metrics.captureDurationMs) / _stats.totalExecutions;
            _stats.avgOpenAiDurationMs = (_stats.avgOpenAiDurationMs * (_stats.totalExecutions - 1) + metrics.openAiDurationMs) / _stats.totalExecutions;
            _stats.avgSunoDurationMs = (_stats.avgSunoDurationMs * (_stats.totalExecutions - 1) + metrics.sunoDurationMs) / _stats.totalExecutions;

            // Keep recent executions
            _recentExecutions.Add(metrics);
            if (_recentExecutions.Count > MaxRecentExecutions)
                _recentExecutions.RemoveAt(0);

            LogStats();
        }

        public TelemetryStats GetStats() => _stats;

        public List<PipelineMetrics> GetRecentExecutions() => new List<PipelineMetrics>(_recentExecutions);

        private void LogStats()
        {
            var successRate = _stats.totalExecutions > 0
                ? (_stats.successfulExecutions * 100f / _stats.totalExecutions)
                : 0f;

            Debug.Log($"📊 Pipeline Telemetry | Executions: {_stats.totalExecutions} | Success Rate: {successRate:F1}% | Avg Duration: {_stats.totalDurationMs / Math.Max(1, _stats.totalExecutions)}ms");
        }

        public void Reset()
        {
            _stats = new TelemetryStats();
            _recentExecutions.Clear();
            Debug.Log("📊 Telemetry reset");
        }
    }
}
