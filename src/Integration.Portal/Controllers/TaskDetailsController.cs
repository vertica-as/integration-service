﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vertica.Integration.Infrastructure.Database.Dapper;
using Vertica.Integration.Infrastructure.Extensions;
using Vertica.Integration.Model;

namespace Vertica.Integration.Portal.Controllers
{
    public class TaskDetailsController : ApiController
    {
        private readonly IDapperProvider _dapper;
        private readonly ITaskService _taskService;

        public TaskDetailsController(ITaskService taskService, IDapperProvider dapper)
        {
            _taskService = taskService;
            _dapper = dapper;
        }

        public HttpResponseMessage Get()
        {
            return 
                Request.CreateResponse(HttpStatusCode.OK,
                    _taskService.GetAll()
                        .Select(x => new { Name = x.Name(), x.Description })
                        .OrderBy(x => x.Name));
        }

        public HttpResponseMessage Get(string name)
        {
            ITask task = _taskService.GetByName(name);

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                Name = task.Name(),
                task.Description,
                Steps = task.Steps.Select(step => new
                {
                    Name = step.Name(),
                    step.Description
                }).ToArray()
            });
        }

        public HttpResponseMessage Get(string name, int count)
        {
            string sql = string.Format(@"
SELECT TOP {0}
	[TimeStamp]
FROM [TaskLog]
WHERE [TaskName] = '{1}' AND [Type] = 'T'
ORDER BY [TimeStamp] DESC
", count, name);

            IEnumerable<DateTimeOffset> lastRun;

            using (IDapperSession session = _dapper.OpenSession())
            {
                lastRun = session.Query<DateTimeOffset>(sql).ToList();
            }

            return Request.CreateResponse(HttpStatusCode.OK, lastRun);
        }
    }
}
