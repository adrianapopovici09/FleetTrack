namespace Cancellation;

// The "application" layer of the lab: owns the 2-second budget.
internal sealed class ReportService(ReportRepository repository, ILogger<ReportService> logger)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    // TODO (you):
    //  - link the incoming token with your own 2 s timeout (lesson §5.2)
    //  - pass the LINKED token to the repository
    //  - log when the OCE passes through this layer, then rethrow
    public async Task RunReportAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(Timeout);
            logger.Step("Service", "starting report");
            await repository.SleepAsync(linkedCts.Token);
        }
        catch (OperationCanceledException oce)
        {
            logger.CancellationSurfaced("Service", oce.GetType().Name, oce.InnerException?.GetType().Name, cancellationToken.IsCancellationRequested ? "yes" : "no");
            throw;
        }
    }
}