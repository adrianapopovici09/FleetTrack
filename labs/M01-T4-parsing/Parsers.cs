namespace Parsing;

// The three parsers are the point of the task: write them yourself (lesson §7–§11).
// Line format: "DEV-42;2026-10-03T10:00:00Z;45.75;21.22;63.5"
//              deviceId ; timestamp (UTC, ISO 8601) ; latitude ; longitude ; speed
// Always parse with CultureInfo.InvariantCulture (lesson §11: the ro-RO decimal comma trap).

// Version 1: string.Split + double.Parse / DateTimeOffset.Parse.
public static class SplitParser
{
    public static TelemetryReading Parse(string line) =>
        throw new NotImplementedException("TODO (you): Split(';') and parse each part");
}

// Version 2: ReadOnlySpan<char> slicing with IndexOf(';'), parsing each slice in place.
public static class SpanParser
{
    public static TelemetryReading Parse(ReadOnlySpan<char> line) =>
        throw new NotImplementedException("TODO (you): slice with IndexOf(';'), parse the spans");
}

// Version 3: Utf8Parser over UTF-8 bytes (System.Buffers.Text), no string for the line at all.
// Utf8Parser's DateTimeOffset formats are strict: check what it accepts for the timestamp.
public static class Utf8BytesParser
{
    public static TelemetryReading Parse(ReadOnlySpan<byte> line) =>
        throw new NotImplementedException("TODO (you): slice with IndexOf((byte)';'), Utf8Parser.TryParse each part");
}