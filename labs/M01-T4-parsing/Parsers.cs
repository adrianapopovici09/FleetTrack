using System.Buffers.Text;

namespace Parsing;

// The three parsers are the point of the task: write them yourself (lesson §7–§11).
// Line format: "DEV-42;2026-10-03T10:00:00Z;45.75;21.22;63.5"
//              deviceId ; timestamp (UTC, ISO 8601) ; latitude ; longitude ; speed
// Always parse with CultureInfo.InvariantCulture (lesson §11: the ro-RO decimal comma trap).

// Version 1: string.Split + double.Parse / DateTimeOffset.Parse.
public static class SplitParser
{
    public static TelemetryReading Parse(string line)
    {
        var info = line.Split(';');

        return new TelemetryReading
        {
            DeviceId = info[0],
            Timestamp = DateTimeOffset.Parse(info[1], System.Globalization.CultureInfo.InvariantCulture),
            Latitude = double.Parse(info[2], System.Globalization.CultureInfo.InvariantCulture),
            Longitude = double.Parse(info[3], System.Globalization.CultureInfo.InvariantCulture),
            Speed = double.Parse(info[4], System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}

// Version 2: ReadOnlySpan<char> slicing with IndexOf(';'), parsing each slice in place.
public static class SpanParser
{
    public static TelemetryReading Parse(ReadOnlySpan<char> line)
    {
        var deviceIdEnd = line.IndexOf(';');
        var deviceId = line.Slice(0, deviceIdEnd);
        var timestampStart = deviceIdEnd + 1;
        var timestampEnd = line.Slice(timestampStart).IndexOf(';') + timestampStart;
        var timestampSlice = line.Slice(timestampStart, timestampEnd - timestampStart);
        if (!DateTimeOffset.TryParse(timestampSlice, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var timestamp))
        {
            throw new FormatException("Invalid timestamp format");
        }
        var latitudeStart = timestampEnd + 1;
        var latitudeEnd = line.Slice(latitudeStart).IndexOf(';') + latitudeStart;
        var latitudeSlice = line.Slice(latitudeStart, latitudeEnd - latitudeStart);
        if (!double.TryParse(latitudeSlice, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var latitude))
        {
            throw new FormatException("Invalid latitude format");
        }
        var longitudeStart = latitudeEnd + 1;
        var longitudeEnd = line.Slice(longitudeStart).IndexOf(';') + longitudeStart;
        var longitudeSlice = line.Slice(longitudeStart, longitudeEnd - longitudeStart);
        if (!double.TryParse(longitudeSlice, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var longitude))
        {
            throw new FormatException("Invalid longitude format");
        }
        var speedSlice = line.Slice(longitudeEnd + 1);
        if (!double.TryParse(speedSlice, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var speed))
        {
            throw new FormatException("Invalid speed format");
        }
        return new TelemetryReading
        {
            DeviceId = deviceId.ToString(),
            Timestamp = timestamp,
            Latitude = latitude,
            Longitude = longitude,
            Speed = speed
        };
    }
}

// Version 3: Utf8Parser over UTF-8 bytes (System.Buffers.Text), no string for the line at all.
// Utf8Parser's DateTimeOffset formats are strict: check what it accepts for the timestamp.
public static class Utf8BytesParser
{
    public static TelemetryReading Parse(ReadOnlySpan<byte> line)
    {
        var deviceIdEnd = line.IndexOf((byte)';');
        var deviceId = System.Text.Encoding.UTF8.GetString(line.Slice(0, deviceIdEnd));
        var timestampStart = deviceIdEnd + 1;
        var timestampEnd = line.Slice(timestampStart).IndexOf((byte)';') + timestampStart;
        var timestampSlice = line.Slice(timestampStart, timestampEnd - timestampStart);
        // Utf8Parser's 'O' format needs 7 fraction digits ("…T10:00:00.0000000Z"), so it rejects our timestamp.
        // ASCII bytes → chars into a stack buffer (no heap allocation), then the char-based exact parse.
        Span<char> timestampChars = stackalloc char[timestampSlice.Length];
        System.Text.Encoding.UTF8.GetChars(timestampSlice, timestampChars);
        if (!DateTimeOffset.TryParseExact(timestampChars, "yyyy-MM-ddTHH:mm:ssK", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var timestamp))
        {
            throw new FormatException("Invalid timestamp format");
        }

        var latitudeStart = timestampEnd + 1;
        var latitudeEnd = line.Slice(latitudeStart).IndexOf((byte)';') + latitudeStart;
        var latitudeSlice = line.Slice(latitudeStart, latitudeEnd - latitudeStart);
        if (!Utf8Parser.TryParse(latitudeSlice, out double latitude, out _))
        {
            throw new FormatException("Invalid latitude format");
        }
        var longitudeStart = latitudeEnd + 1;
        var longitudeEnd = line.Slice(longitudeStart).IndexOf((byte)';') + longitudeStart;
        var longitudeSlice = line.Slice(longitudeStart, longitudeEnd - longitudeStart);
        if (!Utf8Parser.TryParse(longitudeSlice, out double longitude, out _))
        {
            throw new FormatException("Invalid longitude format");
        }
        var speedSlice = line.Slice(longitudeEnd + 1);
        if (!Utf8Parser.TryParse(speedSlice, out double speed, out _))
        {
            throw new FormatException("Invalid speed format");
        }
        return new TelemetryReading
        {
            DeviceId = deviceId,
            Timestamp = timestamp,
            Latitude = latitude,
            Longitude = longitude,
            Speed = speed
        };
    }
}