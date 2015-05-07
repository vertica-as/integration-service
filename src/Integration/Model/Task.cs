using System;
using System.Collections.Generic;
using System.Linq;

namespace Vertica.Integration.Model
{
	public abstract class Task : Task<EmptyWorkItem>
	{
		protected Task()
			: base(Enumerable.Empty<IStep<EmptyWorkItem>>())
		{
		}

		public override EmptyWorkItem Start(ILog log, params string[] arguments)
		{
			if (log == null) throw new ArgumentNullException("log");

			StartTask(log, arguments);

			return new EmptyWorkItem();
		}

		public abstract void StartTask(ILog log, params string[] arguments);
	}

	public abstract class Task<TWorkItem> : ITask<TWorkItem>
	{
		private readonly IEnumerable<IStep<TWorkItem>> _steps;

		protected Task(IEnumerable<IStep<TWorkItem>> steps)
		{
			_steps = steps ?? Enumerable.Empty<IStep<TWorkItem>>();
		}

		IEnumerable<IStep> ITask.Steps
		{
			get { return Steps; }
		}

		public IEnumerable<IStep<TWorkItem>> Steps
		{
			get { return _steps; }
		}

		public abstract string Description { get; }

		public abstract TWorkItem Start(ILog log, params string[] arguments);

        public virtual void End(TWorkItem workItem, ILog log, params string[] arguments)
		{
		}
	}
}