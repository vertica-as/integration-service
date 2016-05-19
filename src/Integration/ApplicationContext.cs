﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using Castle.Windsor;
using Vertica.Integration.Infrastructure.Logging;
using Vertica.Integration.Infrastructure.Logging.Loggers;
using Vertica.Integration.Model.Exceptions;
using Vertica.Integration.Model.Hosting;

namespace Vertica.Integration
{
	[SuppressMessage("ReSharper", "UseNullPropagation")]
	public sealed class ApplicationContext : IApplicationContext
    {
        private static readonly Lazy<Action> EnsureSingleton = new Lazy<Action>(() => () => { });

	    private readonly ApplicationConfiguration _configuration;

	    private readonly IWindsorContainer _container;
	    private readonly IArgumentsParser _parser;
	    private readonly IHost[] _hosts;

		private readonly Lazy<Action> _disposed = new Lazy<Action>(() => () => { });

	    internal ApplicationContext(Action<ApplicationConfiguration> application)
        {
            _configuration = new ApplicationConfiguration();

            if (application != null)
                application(_configuration);

		    _configuration.RegisterDependency<IApplicationContext>(this);

            if (_configuration.IgnoreSslErrors)
			    ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

            _container = CastleWindsor.Initialize(_configuration);
            _parser = Resolve<IArgumentsParser>();
            _hosts = Resolve<IHostFactory>().GetAll();

            AppDomain.CurrentDomain.UnhandledException += LogException;
		}

	    public static IApplicationContext Create(Action<ApplicationConfiguration> application = null)
	    {
            if (EnsureSingleton.IsValueCreated)
			    throw new InvalidOperationException("An instance of ApplicationContext has already been created. It might have been disposed, but you should make sure to reuse the same instance for the entire lifecycle of this application.");

	        EnsureSingleton.Value();

		    return new ApplicationContext(application);
	    }

		public object Resolve(Type service)
		{
			if (service == null) throw new ArgumentNullException(nameof(service));

			return _container.Resolve(service);
		}

		public Array ResolveAll(Type service)
		{
			if (service == null) throw new ArgumentNullException(nameof(service));

			return _container.ResolveAll(service);
		}

		public T Resolve<T>()
	    {
		    return _container.Resolve<T>();
	    }

	    public T[] ResolveAll<T>()
	    {
		    return _container.ResolveAll<T>();
	    }

	    public void Execute(params string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            Execute(_parser.Parse(args));
        }

        public void Execute(HostArguments args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            IHost[] hosts = _hosts.Where(x => x.CanHandle(args)).ToArray();

	        if (hosts.Length == 0)
		        throw new NoHostFoundException(args);

	        if (hosts.Length > 1)
		        throw new MultipleHostsFoundException(args, hosts);

            hosts[0].Handle(args);
        }

        public void Dispose()
		{
			if (_disposed.IsValueCreated)
				throw new InvalidOperationException("ApplicationContext has already been disposed.");

			_disposed.Value();

			_configuration.Extensibility(extensibility => 
			{
				foreach (var disposable in extensibility.OfType<IDisposable>())
				{
					try
					{
						disposable.Dispose();
					}
					catch (Exception ex)
					{
						LogException(ex);
					}
				}
			});

			AppDomain.CurrentDomain.UnhandledException -= LogException;
			_container.Dispose();
		}

        private void LogException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;

            if (exception == null)
                return;

            LogException(exception);
        }

        private void LogException(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            if (exception is TaskExecutionFailedException)
                return;

            var logger = Resolve<ILogger>();

            try
            {
                logger.LogError(exception);
            }
            catch
            {
                if (logger is EventLogger)
                    throw;

	            var eventLogger = new EventLogger(
					Resolve<EventLoggerConfiguration>(), 
					Resolve<IRuntimeSettings>());

	            eventLogger.LogError(exception);
            }
        }
    }
}