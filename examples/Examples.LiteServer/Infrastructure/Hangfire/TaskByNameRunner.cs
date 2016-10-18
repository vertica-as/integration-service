﻿using System.Collections.Generic;
using Vertica.Integration.Model;

namespace Examples.LiteServer.Infrastructure.Hangfire
{
    public class TaskByNameRunner : ITaskByNameRunner
    {
        private readonly ITaskFactory _factory;
        private readonly ITaskRunner _runner;

        public TaskByNameRunner(ITaskFactory factory, ITaskRunner runner)
        {
            _factory = factory;
            _runner = runner;
        }

        public void Run(string taskName, params KeyValuePair<string, string>[] arguments)
        {
            _runner.Execute(_factory.Get(taskName), new Arguments(arguments));
        }
    }
}