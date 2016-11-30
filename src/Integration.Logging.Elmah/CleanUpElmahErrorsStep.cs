using System;
using System.Data.SqlClient;
using Vertica.Integration.Domain.Core;
using Vertica.Integration.Infrastructure.Configuration;
using Vertica.Integration.Model;

namespace Vertica.Integration.Logging.Elmah
{
    public class CleanUpElmahErrorsStep : Step<MaintenanceWorkItem>
    {
        private const string ConfigurationName = "C3B63A03-FE1B-4D75-9406-E330967A82F0";

        private readonly IConfigurationService _configuration;

        public CleanUpElmahErrorsStep(IConfigurationService configuration)
        {
            _configuration = configuration;
        }

        public override Execution ContinueWith(MaintenanceWorkItem workItem)
        {
            ElmahConfiguration configuration = _configuration.GetElmahConfiguration();

            if (configuration.GetConnectionString() == null)
                return Execution.StepOver;

            if (configuration.Disabled)
                return Execution.StepOver;

            workItem.Context(ConfigurationName, configuration);

            return Execution.Execute;
        }

        public override void Execute(MaintenanceWorkItem workItem, ITaskExecutionContext context)
        {
            ElmahConfiguration configuration = workItem.Context<ElmahConfiguration>(ConfigurationName);

            DateTime lowerBound = DateTime.UtcNow.Date.Subtract(configuration.CleanUpEntriesOlderThan);

            using (var connection = new SqlConnection(configuration.GetConnectionString()))
            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();

                command.CommandTimeout = (int)configuration.CommandTimeout.TotalSeconds;
                command.CommandText = "DELETE FROM [ELMAH_Error] WHERE [TimeUtc] <= @t";
                command.Parameters.AddWithValue("t", lowerBound);

                int count = command.ExecuteNonQuery();

                if (count > 0)
                    context.Log.Message("Deleted {0} entries older than '{1}'.", count, lowerBound);
            }
        }

        public override string Description =>
	        $"Deletes Elmah entries older than {_configuration.GetElmahConfiguration().CleanUpEntriesOlderThan.TotalDays} days";
    }
}