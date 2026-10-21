// M01-T3 lab: cancellation end to end (endpoint → service → repository → Postgres).
// Setup (once):
//   dotnet user-secrets set "ConnectionStrings:Postgres" "Host=127.0.0.1;Port=<POSTGRES_PORT>;Database=<POSTGRES_DB>;Username=<POSTGRES_USER>;Password=<POSTGRES_PASSWORD>" --project labs/M01-T3-cancellation
// Run:
//   dotnet run -c Release --project labs/M01-T3-cancellation      (Development environment, so user secrets load)
// Try:
//   curl.exe -i http://localhost:5001/report      → Ctrl+C after ~1 s, then check pg_stat_activity (see check.sql)

using Cancellation;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Postgres is missing. Set it with: dotnet user-secrets set \"ConnectionStrings:Postgres\" \"Host=...\" --project labs/M01-T3-cancellation");
}

// One NpgsqlDataSource per app: it owns the connection pool.
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<ReportRepository>();
builder.Services.AddSingleton<ReportService>();

var app = builder.Build();

app.MapGet("/report", async ([FromServices] ReportService service, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    try
    {
        await service.RunReportAsync(cancellationToken: cancellationToken);
    }
    catch (OperationCanceledException oce)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            logger.CancellationSurfaced("Program", oce.GetType().Name, oce.InnerException?.GetType().Name, "yes");
            throw;
        }
        else
        {
            logger.LogError(oce, "Operation timed out: {Message}", oce.Message);
            return Results.Problem("Operation timed out.", statusCode: 504);
        }
    }

    return Results.Ok("Report completed successfully.");
});

app.Run("http://localhost:5001");