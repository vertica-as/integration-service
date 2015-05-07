﻿using System;
using System.Diagnostics;
using Vertica.Integration.Infrastructure.Extensions;
using Vertica.Integration.Infrastructure.Windows;
using Vertica.Utilities_v4;

namespace Vertica.Integration.Infrastructure.Logging
{
    public abstract class LogEntry : IDisposable
    {
        private readonly Stopwatch _watch;

        protected LogEntry(string taskName, bool startStopwatch = true)
        {
            _watch = new Stopwatch();

            if (startStopwatch)
                _watch.Start();

            TaskName = taskName;
            TimeStamp = Time.UtcNow;
        }

        public int Id { get; internal set; }
        public string TaskName { get; private set; }
        public double ExecutionTimeSeconds { get; protected set; }
        public DateTimeOffset TimeStamp { get; private set; }

        public virtual void Dispose()
        {
            if (_watch.IsRunning)
                _watch.Stop();

            ExecutionTimeSeconds = _watch.Elapsed.TotalSeconds;
        }
    }
}