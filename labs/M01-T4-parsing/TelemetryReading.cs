namespace Parsing;

// The parsed result. A readonly record struct: returning it by value allocates nothing on the heap,
// so the only allocations left are the ones the parser itself makes. Reused by the M08 ingest path.
public readonly record struct TelemetryReading(
    string DeviceId,
    DateTimeOffset Timestamp,
    double Latitude,
    double Longitude,
    double Speed);