using System.Diagnostics.CodeAnalysis;
using System.Text;

using BenchmarkDotNet.Attributes;

namespace Parsing;

[MemoryDiagnoser]                     // adds the Gen0 and Allocated (bytes per operation) columns
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "BenchmarkDotNet runs instance methods only.")]
public class ParsingBenchmarks
{
    public const string Line = "DEV-42;2026-10-03T10:00:00Z;45.75;21.22;63.5";

    // Inputs are prepared once, so the benchmarks measure parsing only (lesson §12.4).
    private readonly byte[] _utf8 = Encoding.UTF8.GetBytes(Line);

    // Each benchmark returns its result, so the JIT can't optimise the work away.
    [Benchmark(Baseline = true)]
    public TelemetryReading Split() => SplitParser.Parse(Line);

    [Benchmark]
    public TelemetryReading Span() => SpanParser.Parse(Line);

    [Benchmark]
    public TelemetryReading Utf8Bytes() => Utf8BytesParser.Parse(_utf8);

    // The "harder" variant: version 2 doing the SAME job as version 3 (starting from network bytes),
    // so the cost of decoding UTF-8 into a string becomes visible.
    [Benchmark]
    public TelemetryReading SpanFromBytes() => SpanParser.Parse(Encoding.UTF8.GetString(_utf8));
}