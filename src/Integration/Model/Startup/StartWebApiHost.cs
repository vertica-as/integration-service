using System;
using Vertica.Integration.Infrastructure.Logging;
using Vertica.Integration.Model.Web;

namespace Vertica.Integration.Model.Startup
{
    internal class StartWebApiHost : StartupAction
    {
        private readonly ILogger _logger;

        public StartWebApiHost(ILogger logger)
        {
            _logger = logger;
        }

        public override bool IsSatisfiedBy(ExecutionContext context)
        {
            if (context == null) throw new ArgumentNullException("context");

            return context.Task is WebApiTask && base.IsSatisfiedBy(context);
        }

        protected override string ActionName
        {
            get { return "url"; }
        }

        protected override ArgumentValidator Validator
        {
            get { return new ArgumentValidator("[absolute-url]", Argument.AbsoluteUrl); }
        }

        protected override void DoExecute(ExecutionContext context)
        {
            using (new WebApiHost(context.ActionArguments[0], Console.Out, _logger, context.TaskName, context.Task, context.TaskArguments))
            {
                do
                {
                    Console.WriteLine();
                    Console.WriteLine("Press ESCAPE to stop web-service.");
                    Console.WriteLine();

                } while (WaitingForEscape());
            }
        }

        private static bool WaitingForEscape()
        {
            return Console.ReadKey(true /* will not display */).Key != ConsoleKey.Escape;
        }
    }
}