using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Jusvibes.Core
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public class PipelineLogger
    {
        private readonly string _correlationId;
        private readonly string _context;
        private readonly Dictionary<string, Stopwatch> _timers;

        public string CorrelationId => _correlationId;

        public PipelineLogger(string context = "Pipeline")
        {
            _correlationId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _context = context;
            _timers = new Dictionary<string, Stopwatch>();
        }

        public void Debug(string message, object data = null)
        {
            Log(LogLevel.Debug, message, data);
        }

        public void Info(string message, object data = null)
        {
            Log(LogLevel.Info, message, data);
        }

        public void Warning(string message, object data = null)
        {
            Log(LogLevel.Warning, message, data);
        }

        public void Error(string message, Exception ex = null, object data = null)
        {
            var logData = new Dictionary<string, object>();
            if (data != null) logData["data"] = data;
            if (ex != null)
            {
                logData["exception"] = ex.Message;
                logData["stackTrace"] = ex.StackTrace;
            }

            Log(LogLevel.Error, message, logData.Count > 0 ? logData : null);
        }

        public void StartTimer(string timerName)
        {
            if (_timers.ContainsKey(timerName))
            {
                _timers[timerName].Restart();
            }
            else
            {
                _timers[timerName] = Stopwatch.StartNew();
            }

            Info($"⏱️ Started: {timerName}");
        }

        public long StopTimer(string timerName)
        {
            if (_timers.TryGetValue(timerName, out var timer))
            {
                timer.Stop();
                var elapsed = timer.ElapsedMilliseconds;
                Info($"⏱️ Completed: {timerName}", new { durationMs = elapsed });
                return elapsed;
            }

            Warning($"Timer '{timerName}' not found");
            return 0;
        }

        public long GetElapsedMs(string timerName)
        {
            return _timers.TryGetValue(timerName, out var timer) ? timer.ElapsedMilliseconds : 0;
        }

        private void Log(LogLevel level, string message, object data = null)
        {
            var prefix = $"[{_context}:{_correlationId}]";
            var formattedMessage = data != null
                ? $"{prefix} {message} | {JsonUtility.ToJson(data)}"
                : $"{prefix} {message}";

            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    UnityEngine.Debug.Log(formattedMessage);
                    break;
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(formattedMessage);
                    break;
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(formattedMessage);
                    break;
            }
        }
    }
}
