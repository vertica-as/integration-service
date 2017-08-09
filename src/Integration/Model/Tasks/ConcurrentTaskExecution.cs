﻿using System;
using System.Reflection;
using Castle.MicroKernel;
using Vertica.Integration.Infrastructure.Extensions;
using Vertica.Integration.Infrastructure.Logging;
using Vertica.Integration.Infrastructure.Threading.DistributedMutex;
using Vertica.Integration.Model.Exceptions;

namespace Vertica.Integration.Model.Tasks
{
    public class ConcurrentTaskExecution : IConcurrentTaskExecution
    {
        private readonly IDistributedMutex _distributedMutex;
        private readonly IKernel _kernel;
        private readonly ILogger _logger;

        private readonly DistributedMutexConfiguration _defaultConfiguration;
        private readonly bool _preventConcurrentTaskExecutionOnAllTasks;

        public ConcurrentTaskExecution(IDistributedMutex distributedMutex, IKernel kernel, ILogger logger, IRuntimeSettings settings)
        {
            _distributedMutex = distributedMutex;
            _kernel = kernel;
            _logger = logger;

            _defaultConfiguration = DefaultConfiguration(settings, out _preventConcurrentTaskExecutionOnAllTasks);
        }

        public IDisposable Handle(ITask task, Arguments arguments, TaskLog log)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            if (log == null) throw new ArgumentNullException(nameof(log));

            Type taskType = task.GetType();

            var preventConcurrent = taskType.GetCustomAttribute<PreventConcurrentTaskExecutionAttribute>();

            if (preventConcurrent == null)
            {
                var allowConcurrentTaskExecutionAttribute = taskType.GetCustomAttribute<AllowConcurrentTaskExecutionAttribute>();

                if (allowConcurrentTaskExecutionAttribute != null)
                    return null;

                if (!_preventConcurrentTaskExecutionOnAllTasks)
                    return null;
            }
            else
            {
                Type runtimeEvaluatorType = preventConcurrent.RuntimeEvaluator;

                if (runtimeEvaluatorType != null)
                {
                    var runtimeEvaluator = _kernel.Resolve(runtimeEvaluatorType) as IPreventConcurrentTaskExecutionRuntimeEvaluator;

                    if (runtimeEvaluator == null)
                        throw new InvalidOperationException($@"Unable to resolve evaluator type '{runtimeEvaluatorType.FullName}'. 


Either the type does not implement '{nameof(IPreventConcurrentTaskExecutionRuntimeEvaluator)}'-interface or you forgot to register the type as a custom evaluator. 

You can register custom evaluators when setting up Integration Service in the ApplicationContext.Create(...)-method:

using (IApplicationContext context = ApplicationContext.Create(application => application
    .Tasks(tasks => tasks
        .ConcurrentTaskExecution(concurrentTaskExecution => concurrentTaskExecution
            .AddFromAssemblyOfThis<Program>()
            .AddEvaluator<MyCustomEvaluator>()))");

                    if (runtimeEvaluator.Disabled(task, arguments))
                        return null;
                }
            }

            string lockName = GetLockName(task, arguments, preventConcurrent);

            try
            {
                var context = new DistributedMutexContext(
                    lockName,
                    preventConcurrent?.Configuration ?? _defaultConfiguration,
                    log.LogMessage,
                    GetLockDescription(task, log, arguments, preventConcurrent));

                return _distributedMutex.Enter(context);
            }
            catch (Exception ex)
            {
                ErrorLog errorLog = _logger.LogError(ex);
                log.ErrorLog = errorLog;

                throw new TaskExecutionFailedException($"Unable to acquire lock '{lockName}'.", ex);
            }
        }

        private string GetLockName(ITask task, Arguments arguments, PreventConcurrentTaskExecutionAttribute preventConcurrent)
        {
            Type customLockNameType = preventConcurrent?.CustomLockName;

            if (customLockNameType != null)
            {
                var customLockName =
                    _kernel.Resolve(customLockNameType) as IPreventConcurrentTaskExecutionCustomLockName;

                if (customLockName == null)
                    throw new InvalidOperationException(
                        $@"Unable to resolve custom lock name type '{customLockNameType.FullName}'. 


Either the type does not implement '{nameof(IPreventConcurrentTaskExecutionCustomLockName)}'-interface or you forgot to register the type as a custom lock name. 

You can register custom evaluators when setting up Integration Service in the ApplicationContext.Create(...)-method:

using (IApplicationContext context = ApplicationContext.Create(application => application
    .Tasks(tasks => tasks
        .ConcurrentTaskExecution(concurrentTaskExecution => concurrentTaskExecution
            .AddFromAssemblyOfThis<Program>()
            .AddCustomLockName<MyCustomLockName>()))");

                string lockName = customLockName.GetLockName(task, arguments);

                if (!string.IsNullOrWhiteSpace(lockName))
                    return lockName;
            }

            return task.Name();
        }

        private string GetLockDescription(ITask task, TaskLog log, Arguments arguments, PreventConcurrentTaskExecutionAttribute preventConcurrent)
        {
            string description = $"Acquired by '{task.Name()}'. TaskLogId: {log.Id ?? "<n/a>"}.";

            Type customLockDescriptionType = preventConcurrent?.CustomLockDescription;

            if (customLockDescriptionType != null)
            {
                var customLockDescription =
                    _kernel.Resolve(customLockDescriptionType) as IPreventConcurrentTaskExecutionCustomLockDescription;

                if (customLockDescription == null)
                    throw new InvalidOperationException(
                        $@"Unable to resolve custom lock description type '{customLockDescriptionType.FullName}'. 


Either the type does not implement '{nameof(IPreventConcurrentTaskExecutionCustomLockDescription)}'-interface or you forgot to register the type as a custom lock description. 

You can register custom evaluators when setting up Integration Service in the ApplicationContext.Create(...)-method:

using (IApplicationContext context = ApplicationContext.Create(application => application
    .Tasks(tasks => tasks
        .ConcurrentTaskExecution(concurrentTaskExecution => concurrentTaskExecution
            .AddFromAssemblyOfThis<Program>()
            .AddCustomLockDescription<MyCustomLockDescription>()))");

                description = customLockDescription.GetLockDescription(task, arguments, description);
            }

            return description;
        }

        private static DistributedMutexConfiguration DefaultConfiguration(IRuntimeSettings settings, out bool preventConcurrentTaskExecutionOnAllTasks)
        {
            TimeSpan defaultWaitTime;
            if (!TimeSpan.TryParse(settings[$"{nameof(ConcurrentTaskExecution)}.DefaultWaitTime"], out defaultWaitTime))
                defaultWaitTime = TimeSpan.FromSeconds(30);

            bool.TryParse(settings[$"{nameof(ConcurrentTaskExecution)}.PreventConcurrentTaskExecutionOnAllTasks"], out preventConcurrentTaskExecutionOnAllTasks);

            return new DistributedMutexConfiguration(defaultWaitTime);
        }
    }
}