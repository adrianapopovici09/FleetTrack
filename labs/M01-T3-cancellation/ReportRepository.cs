using Npgsql;

namespace Cancellation;

// The "infrastructure" layer of the lab: the only place that talks to Postgres.
internal sealed class ReportRepository(NpgsqlDataSource dataSource, ILogger<ReportRepository> logger)
{
    // TODO (you):
    //  - open a connection from the data source
    //  - run "SELECT pg_sleep(10)" with the token passed all the way to Npgsql
    //  - log when the OCE surfaces here (look at its InnerException), then rethrow
    public async Task SleepAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_sleep(10)";
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
        }
        catch (OperationCanceledException oce)
        {
            logger.CancellationSurfaced("Repository", oce.GetType().Name, oce.InnerException?.GetType().Name, "unknown");
            throw;
        }
    }
}