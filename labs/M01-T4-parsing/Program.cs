// M01-T4 lab: three ways to parse a telemetry line, benchmarked with BenchmarkDotNet.
//   dotnet run -c Release --project labs/M01-T4-parsing             → correctness check, then the benchmarks
//   dotnet run -c Release --project labs/M01-T4-parsing -- --check  → correctness check only
// Always Release, never under a debugger (lesson §12.2).

using System.Text;

using BenchmarkDotNet.Running;

using Parsing;

// Correctness first: a fast parser that returns the wrong values is worthless (lesson §12.4).
var line = ParsingBenchmarks.Line;
var expected = new TelemetryReading("DEV-42", new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero), 45.75, 21.22, 63.5);

var results = new (string Name, Func<TelemetryReading> Parse)[]
{
    ("Split", () => SplitParser.Parse(line)),
    ("Span", () => SpanParser.Parse(line)),
    ("Utf8Bytes", () => Utf8BytesParser.Parse(Encoding.UTF8.GetBytes(line))),
};

var allCorrect = true;
foreach (var (name, parse) in results)
{
    try
    {
        var actual = parse();
        var ok = actual == expected;
        allCorrect &= ok;
        Console.WriteLine($"{name,-10} {(ok ? "OK " : "BAD")} {actual}");
    }
    catch (Exception ex)
    {
        allCorrect = false;
        Console.WriteLine($"{name,-10} ERR {ex.GetType().Name}: {ex.Message}");
    }
}

if (!allCorrect)
{
    Console.WriteLine("Fix the parsers before benchmarking.");
    return 1;
}

if (args.Contains("--check"))
{
    return 0;
}

BenchmarkRunner.Run<ParsingBenchmarks>();
return 0;