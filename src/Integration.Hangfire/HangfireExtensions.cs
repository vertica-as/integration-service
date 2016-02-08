﻿using System;
using Vertica.Integration;

namespace Integration.Hangfire
{
    public static class HangfireExtensions
    {
        public static ApplicationConfiguration UseHangfire(this ApplicationConfiguration application, Action<HangfireConfiguration> hangfire)
        {
            if (application == null) throw new ArgumentNullException("application");
	        if (hangfire == null) throw new ArgumentNullException("hangfire");

	        return application.Extensibility(extensibility =>
			{
				var configuration = extensibility.Register(() => new HangfireConfiguration(application));

				hangfire(configuration);
			});
        }
    }
}