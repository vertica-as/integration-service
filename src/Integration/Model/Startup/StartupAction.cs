using System;

namespace Vertica.Integration.Model.Startup
{
    internal abstract class StartupAction
    {
        protected abstract string ActionName { get; }

        public virtual bool IsSatisfiedBy(ExecutionContext context)
        {
            if (context == null) throw new ArgumentNullException("context");

            return String.Equals(ActionName, context.ActionName, StringComparison.OrdinalIgnoreCase);
        }

        public void Execute(ExecutionContext context)
        {
            if (context == null) throw new ArgumentNullException("context");

            if (Validator != null)
            {
                if (Validator.Length != context.ActionArguments.Length)
                    throw new ArgumentException(String.Format("Invalid number of arguments passed to this startup action. Usage: -{0} {1}", ActionName, Validator.Usage));

                for (int i = 0; i < context.ActionArguments.Length; i++)
                {
                    string error;
                    if (!Validator[i].IsValid(context.ActionArguments[i], out error))
                        throw new ArgumentException(String.Format("{0} Usage: -{1} {2}", error, ActionName, Validator.Usage));
                }
            }

            DoExecute(context);
        }

        protected abstract ArgumentValidator Validator { get; }

        protected abstract void DoExecute(ExecutionContext context);
    }
}