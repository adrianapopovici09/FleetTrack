namespace FleetTrack.API.Errors;

// The stable "type" URIs of the error contract, documented in docs/api/errors.md.
// Clients branch on these values: never change or reuse a published one, only add new ones.
public static class ProblemTypes
{
    private const string BaseUri = "https://fleettrack.dev/errors/";

    public const string Validation = BaseUri + "validation";
    public const string NotFound = BaseUri + "not-found";
    public const string InvalidStatusTransition = BaseUri + "invalid-status-transition";
    public const string InternalError = BaseUri + "internal-error";
    public const string ServiceUnavailable = BaseUri + "service-unavailable";
}