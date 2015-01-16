﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Vertica.Integration.Infrastructure.Windows;
using Vertica.Integration.Model;

namespace Vertica.Integration.Infrastructure.Logging
{
	public class TaskLog : LogEntry
	{
		private readonly Action<LogEntry> _persist;
        private readonly Output _output;

		private readonly IList<StepLog> _steps;
		private readonly IList<MessageLog> _messages;

		protected TaskLog()
		{
		}

		public TaskLog(string taskName, Action<LogEntry> persist, Output output)
			: base(taskName)
		{
		    if (persist == null) throw new ArgumentNullException("persist");
		    if (output == null) throw new ArgumentNullException("output");

		    _persist = persist;
            _output = output;

			_steps = new List<StepLog>();
			_messages = new List<MessageLog>();

			Initialize();
		}

		private void Initialize()
		{
			MachineName = Environment.MachineName;
			IdentityName = WindowsUtils.GetIdentityName();

			Persist(this);
            _output.Message(TaskName);
		}

		public virtual string MachineName { get; protected set; }
		public virtual string IdentityName { get; protected set; }

		public virtual ReadOnlyCollection<StepLog> Steps
		{
			get { return new ReadOnlyCollection<StepLog>(_steps); }
		}

		public virtual ReadOnlyCollection<MessageLog> Messages
		{
			get { return new ReadOnlyCollection<MessageLog>(_messages); }
		}

		public virtual ErrorLog ErrorLog { get; protected internal set; }

		public virtual StepLog LogStep(string stepName)
		{
			var log = new StepLog(this, stepName, _output);

			_steps.Add(log);

			return log;
		}

		public virtual void LogMessage(string message)
		{
			using (var log = new MessageLog(this, message, _output))
			{
				_messages.Add(log);
			}
		}

		internal protected virtual void Persist(LogEntry logEntry)
		{
			if (logEntry == null) throw new ArgumentNullException("logEntry");

			_persist(logEntry);
		}

		public override void Dispose()
		{
			base.Dispose();

			Persist(this);
		}
	}
}