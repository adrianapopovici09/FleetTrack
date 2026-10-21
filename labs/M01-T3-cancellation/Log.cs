namespace Cancellation;

// Source-generated logging: the compiler writes these methods from the attributes.
// FleetTrack's guardrails (CA1848 / CA1873 as errors) reject plain logger.LogInformation(...) calls,
// because those box arguments and format strings even when the level is disabled.
internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "{Layer}: {Step}")]
    public static partial void Step(this ILogger logger, string layer, string step);

    // Use in each layer's catch block, then rethrow with `throw;`.
    // exceptionType: ex.GetType().Name   innerType: ex.InnerException?.GetType().Name
    // clientAborted: "yes" / "no", or "unknown" in a layer that only sees the linked token (it can't tell an abort from a timeout)
    [LoggerMessage(Level = LogLevel.Information, Message = "{Layer}: cancellation surfaced as {ExceptionType} (inner: {InnerType}); client aborted: {ClientAborted}")]
    public static partial void CancellationSurfaced(this ILogger logger, string layer, string exceptionType, string? innerType, string clientAborted);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Layer}: timed out after {Timeout}")]
    public static partial void TimedOut(this ILogger logger, string layer, TimeSpan timeout);
}