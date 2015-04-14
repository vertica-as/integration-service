﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vertica.Integration.Infrastructure.Database.Dapper;
using Vertica.Integration.Portal.Models;

namespace Vertica.Integration.Portal.Controllers
{
    public class TaskExecutionDetailsController : ApiController
    {
        private readonly IDapperFactory _dapper;

        public TaskExecutionDetailsController(IDapperFactory dapper)
        {
            _dapper = dapper;
        }

        public HttpResponseMessage Get(int id)
        {
            string sql = @"
SELECT [Id]
      ,[TaskName]
      ,[StepName]
      ,[Message]
      ,[ExecutionTimeSeconds]
      ,[TimeStamp]
  FROM [dbo].[TaskLog]
  WHERE [Id] = @id OR TaskLog_Id = @taskLogId";

            IEnumerable<TaskExecutionDetailModel> taskLogs;

            using (IDapperSession session = _dapper.OpenSession())
            {
                taskLogs = session.Query<TaskExecutionDetailModel>(sql, new { id, taskLogId = id }).ToList();
            }

            return Request.CreateResponse(HttpStatusCode.OK, taskLogs);
        }
    }
}
