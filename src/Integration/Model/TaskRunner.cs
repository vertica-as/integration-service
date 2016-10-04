using System;
using System.Collections.Generic;
using System.IO;
using Vertica.Integration.Infrastructure.Extensions;
using Vertica.Integration.Infrastructure.Logging;
using Vertica.Integration.Model.Exceptions;
using Vertica.Integration.Model.Tasks;
using Vertica.Utilities_v4;

namespace Vertica.Integration.Model
{
	public class TaskRunner : ITaskRunner
	{
		private readonly ILogger _logger;
	    private readonly IConcurrentTaskExecution _concurrentTaskExecution;
		private readonly TextWriter _outputter;

		public TaskRunner(ILogger logger, IConcurrentTaskExecution concurrentTaskExecution, TextWriter outputter)
		{
			_logger = logger;
			_outputter = outputter;
		    _concurrentTaskExecution = concurrentTaskExecution;
		}

		public TaskExecutionResult Execute(ITask task, Arguments arguments = null)
		{
			if (task == null) throw new ArgumentNullException(nameof(task));

			// latebound because we don't know the exact generic type at compile time
			return ExecuteInternal((dynamic) task, arguments ?? Arguments.Empty);
		}

		private TaskExecutionResult ExecuteInternal<TWorkItem>(ITask<TWorkItem> task, Arguments arguments)
		{
			if (task == null) throw new ArgumentNullException(nameof(task));
			if (arguments == null) throw new ArgumentNullException(nameof(arguments));

			var output = new List<string>();

			Action<string> outputter = message =>
			{
				message = $"[{Time.Now:HH:mm:ss}] {message}";

				_outputter.WriteLine(message);
				output.Add(message);
			};

            using (var taskLog = new TaskLog(task, _logger.LogEntry, new Output(outputter)))
            using (_concurrentTaskExecution.Handle(task, taskLog))
            {
                TWorkItem workItem;

                try
                {
                    workItem = task.Start(new TaskExecutionContext(new Log(taskLog.LogMessage, _logger), arguments));
                }
                catch (Exception ex)
                {
                    ErrorLog errorLog = _logger.LogError(ex);
                    taskLog.ErrorLog = errorLog;

                    throw new TaskExecutionFailedException("Starting task failed.", ex);
                }

                try
                {
                    foreach (IStep<TWorkItem> step in task.Steps)
                    {
                        Execution continueWith = step.ContinueWith(workItem);

                        if (continueWith == Execution.StepOut)
                            break;

                        if (continueWith == Execution.StepOver)
                            continue;

                        using (var stepLog = taskLog.LogStep(step))
                        {
                            try
                            {
                                step.Execute(workItem, new TaskExecutionContext(new Log(stepLog.LogMessage, _logger), arguments));
                            }
                            catch (Exception ex)
                            {
                                ErrorLog errorLog = _logger.LogError(ex);

                                taskLog.ErrorLog = errorLog;
                                stepLog.ErrorLog = errorLog;

                                throw new TaskExecutionFailedException($"Step '{stepLog.Name}' failed.", ex);
                            }
                        }
                    }

                    try
                    {
                        task.End(workItem, new TaskExecutionContext(new Log(taskLog.LogMessage, _logger), arguments));
                    }
                    catch (Exception ex)
                    {
                        ErrorLog errorLog = _logger.LogError(ex);
                        taskLog.ErrorLog = errorLog;

                        throw new TaskExecutionFailedException("Ending task failed.", ex);
                    }
                }
                finally
                {
                    workItem.DisposeIfDisposable();
                }
            }

			return new TaskExecutionResult(output.ToArray());
		}
	}
}