using System;
using System.IO;
using Vertica.Integration.Infrastructure;
using Vertica.Integration.Infrastructure.Extensions;
using Vertica.Integration.Model.Hosting.Handlers;

namespace Vertica.Integration.Model.Hosting
{
    public class TaskHost : IHost
    {
        private readonly ITaskFactory _factory;
        private readonly ITaskRunner _runner;
        private readonly IWindowsServiceHandler _windowsService;

        public TaskHost(ITaskFactory factory, ITaskRunner runner, IWindowsServiceHandler windowsService)
        {
            _factory = factory;
            _runner = runner;
	        _windowsService = windowsService;
        }

        public bool CanHandle(HostArguments args)
        {
            if (args == null) throw new ArgumentNullException("args");

            return _factory.Exists(args.Command);
        }

        public void Handle(HostArguments args)
        {
	        if (args == null) throw new ArgumentNullException("args");

	        ITask task = _factory.Get(args.Command);

			var windowsService = new WindowsService(task.Name(), task.Description).OnStart(() =>
			{
				string repeat;
				args.CommandArgs.TryGetValue("repeat", out repeat);

				// TODO: Implement CRON functionality.

				uint seconds;
				if (!UInt32.TryParse(repeat, out seconds))
					seconds = 1;

				Action run = () => _runner.Execute(task, args.Args);

				return run.Repeat(Delay.Custom(seconds), TextWriter.Null);
			});

	        if (!_windowsService.Handle(args, windowsService))
				_runner.Execute(task, args.Args);
        }

	    public string Description
        {
            get
            {
                return "Handles execution of Tasks.";
            }
        }
    }
}