﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using Vertica.Integration.Infrastructure;
using Vertica.Integration.Infrastructure.Windows;
using Vertica.Utilities_v4.Extensions.EnumerableExt;

namespace Vertica.Integration.Model.Hosting.Handlers
{
	public class WindowsServiceHandler : IWindowsServiceHandler
	{
		private const string Command = "service";

		private const string ServiceStartMode = "startmode";
		private const string ServiceAccountCommand = "account";
		private const string ServiceAccountUsernameCommand = "username";
		private const string ServiceAccountPasswordCommand = "password";

	    private readonly IRuntimeSettings _runtimeSettings;
		private readonly IWindowsServices _windowsServices;
	    private readonly IShutdown _shutdown;
	    
	    public WindowsServiceHandler(IRuntimeSettings runtimeSettings, IWindowsFactory windows, IShutdown shutdown)
	    {
		    _runtimeSettings = runtimeSettings;
	        _shutdown = shutdown;
	        _windowsServices = windows.WindowsServices();
	    }

        public bool Handle(HostArguments args, HandleAsWindowsService service)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
	        if (service == null) throw new ArgumentNullException(nameof(service));

	        string action;
			if (!args.CommandArgs.TryGetValue(Command, out action))
                return false;

	        Func<KeyValuePair<string, string>, bool> actionIs = command =>
				string.Equals(command.Value, action, StringComparison.OrdinalIgnoreCase);

			if (actionIs(InstallCommand))
			{
				var configuration = new WindowsServiceConfiguration(GetServiceName(service), ExePath, ExeArgs(args))
					.DisplayName(Prefix(service.DisplayName))
					.Description(service.Description);

				string startMode;
				args.CommandArgs.TryGetValue(ServiceStartMode, out startMode);

				ServiceStartMode serviceStartMode;
				if (Enum.TryParse(startMode, true, out serviceStartMode))
					configuration.StartMode(serviceStartMode);

				string account;
				if (args.CommandArgs.TryGetValue(ServiceAccountCommand, out account))
				{
					ServiceAccount serviceAccount;
					if (Enum.TryParse(account, true, out serviceAccount))
						configuration.RunAs(serviceAccount);
				}
				else
				{
					string username;
					args.CommandArgs.TryGetValue(ServiceAccountUsernameCommand, out username);

					string password;
					args.CommandArgs.TryGetValue(ServiceAccountPasswordCommand, out password);

					if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
						configuration.RunAsUser(username, password);
				}

				_windowsServices.Install(configuration);
			}
			else if (actionIs(UninstallCommand))
			{
				_windowsServices.Uninstall(GetServiceName(service));
			}
			else
			{
                using (service.OnStartFactory())
                using (var serviceBase = new ServiceBase())
                {
                    serviceBase.ServiceName = GetServiceName(service);

                    ServiceBase.Run(serviceBase);

                    _shutdown.WaitForShutdown();
                }
			}

            return true;
        }

		private string GetServiceName(HandleAsWindowsService service)
		{
			if (service == null) throw new ArgumentNullException(nameof(service));

			return Regex.Replace(Prefix(service.Name), @"\W", string.Empty);
		}

		private string Prefix(string value)
		{
			bool dontPrefix;
			bool.TryParse(_runtimeSettings["WindowsService.DontPrefix"], out dontPrefix);

			if (dontPrefix)
				return value;

			ApplicationEnvironment environment = _runtimeSettings.Environment;

			return $"Integration Service{(environment != null ? $" [{environment}]" : string.Empty)}: {value}";
		}

		private static string ExePath => Assembly.GetEntryAssembly().Location;

		private static string ExeArgs(HostArguments args)
		{
			Arguments arguments = new Arguments(args.CommandArgs
				.Where(x => !ReservedCommandArgs.Contains(x.Key, StringComparer.OrdinalIgnoreCase))
				.Append(new KeyValuePair<string, string>("-service", string.Empty))
				.Append(args.Args.ToArray())
				.ToArray());

			return $"{args.Command} {arguments}";
		}

		private static IEnumerable<string> ReservedCommandArgs
		{
			get
			{
				yield return Command;
				yield return ServiceStartMode;
				yield return ServiceAccountCommand;
				yield return ServiceAccountUsernameCommand;
				yield return ServiceAccountPasswordCommand;
			}
		}

		public static KeyValuePair<string, string> InstallCommand => new KeyValuePair<string, string>(Command, "install");
		public static KeyValuePair<string, string> UninstallCommand => new KeyValuePair<string, string>(Command, "uninstall");
	}
}