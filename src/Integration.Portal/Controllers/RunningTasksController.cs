﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vertica.Integration.Infrastructure.Database.Dapper;
using Vertica.Integration.Portal.Models;

namespace Vertica.Integration.Portal.Controllers
{
    public class RunningTasksController : ApiController
    {
        private readonly IDapperFactory _dappper;

        public RunningTasksController(IDapperFactory dappper)
        {
            _dappper = dappper;
        }

        public HttpResponseMessage Get()
        {
            // TODO: Fix denne.

            string sql = string.Format(@"
SELECT
	[Id],
	[TaskName],
	[StepName],
	[Message],
	[TimeStamp]
FROM [TaskLog]
WHERE (
    [Type] = N'T' AND
    [ExecutionTimeSeconds] IS NULL AND
    [ErrorLog_Id] IS NULL
)
ORDER BY [Id] DESC");

            IEnumerable<TaskLogModel> tasks;

            using (IDapperSession session = _dappper.OpenSession())
            {
                tasks = session.Query<TaskLogModel>(sql).ToList();
            }

            return Request.CreateResponse(HttpStatusCode.OK, tasks);
        }
    }
}