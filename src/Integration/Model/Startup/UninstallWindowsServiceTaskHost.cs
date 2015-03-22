using Vertica.Integration.Infrastructure.Extensions;
using Vertica.Integration.Infrastructure.Windows;

namespace Vertica.Integration.Model.Startup
{
    internal class UninstallWindowsServiceTaskHost : StartupAction
    {
        protected override string ActionName
        {
            get { return "uninstall"; }
        }

        protected override ArgumentValidator Validator
        {
            get { return null; }
        }

        protected override void DoExecute(ExecutionContext context)
        {
            string taskName = context.Task.Name();

            using (var installer = new WindowsServiceInstaller(taskName, taskName))
            {
                installer.Uninstall();
            }
        }
    }
}