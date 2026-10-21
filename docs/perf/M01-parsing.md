# M01-T4 · Parsing a telemetry line: allocations and speed

* **Task:** [M01-T4](../plans/modules/M01-csharp-runtime.md) · **Lesson:** [reading notes](../plans/reading/M01-T4-memory-gc-spans.md)
* **Lab:** [labs/M01-T4-parsing](../../labs/M01-T4-parsing/) (`Parsers.cs`, `ParsingBenchmarks.cs`)
* **Date:** 2026-10-21

## Setup

| | |
| --- | --- |
| Input | `"DEV-42;2026-10-03T10:00:00Z;45.75;21.22;63.5"` (44 characters / 44 UTF-8 bytes) |
| Result | `readonly record struct TelemetryReading(string DeviceId, DateTimeOffset Timestamp, double Latitude, double Longitude, double Speed)`: returned by value, so it allocates nothing itself; the device id is kept as a `string` |
| Versions | 1 `Split` + `double.Parse` · 2 `ReadOnlySpan<char>` slicing · 3 `Utf8Parser` over UTF-8 bytes · 4 (extra) version 2 starting from bytes (`Encoding.UTF8.GetString` + spans) |
| Tool | BenchmarkDotNet 0.15.8, `[MemoryDiagnoser]`, version 1 as baseline, Release build, no debugger |
| Machine | 4 physical cores / 8 logical processors, .NET 10 |

### How to run

```powershell
dotnet run -c Release --project labs/M01-T4-parsing -- --check   # correctness only
dotnet run -c Release --project labs/M01-T4-parsing              # correctness, then benchmarks
```

## Prediction (written before running)

Rule of thumb from the lesson: a string of *n* characters takes about `20 + 2n` bytes, rounded up to a multiple of 8; an array of 5 references takes 5 × 8 plus a ~24-byte header.

**Version 1, `Split`:** the array holds only **references**; each element points to a **separate new string** with a copy of its characters.

| Allocation | Characters | Bytes |
| --- | --- | --- |
| `string[]` (5 references) | — | ~64 |
| `"DEV-42"` | 6 | 32 |
| `"2026-10-03T10:00:00Z"` | 20 | 64 |
| `"45.75"` | 5 | 32 |
| `"21.22"` | 5 | 32 |
| `"63.5"` | 4 | 32 |
| **Total** | | **≈ 256 B** |

`double.Parse` on an existing string adds nothing; the result struct adds nothing; the device id string from `Split` is reused in the result.

**Version 2, spans:** slicing (`span[..sep]`) is a window into the original line, and `double.Parse` / `DateTimeOffset.Parse` have span overloads that read the characters in place. The only allocation left is `deviceSpan.ToString()`, because a span can't be stored in the result (spans are stack-only) → **≈ 32 B** (0 B if the result didn't need the device id as a string).

**Version 3, `Utf8Parser`:** numbers are parsed straight from the bytes; turning the device id bytes into a `string` (`Encoding.UTF8.GetString`) is the only allocation → **≈ 32 B**.

| | Allocated | Speed |
| --- | --- | --- |
| 1 `Split` | ≈ 256 B | Slowest: 6 objects allocated and every character copied |
| 2 Spans | ≈ 32 B | Much faster than 1 |
| 3 `Utf8Parser` | ≈ 32 B | **About the same as 2** in this benchmark: parsing digits from bytes or from chars is similar work |
| 4 Spans from bytes | ≈ 32 B + the decoded line (44 chars → ~112 B) ≈ 144 B | Slower than 3: it pays for decoding the whole line |

Why 3 can win in the real pipeline but not in the benchmark: telemetry arrives as UTF-8 bytes. Versions 1 and 2 need a `string` first, so the real job includes decoding the line (time) and allocating a ~112-byte string (memory). The benchmark hands versions 1–3 their input ready-made, so that cost only shows up in version 4, which does the same job as version 3.

Open question to answer in the lab: does `Utf8Parser` accept `2026-10-03T10:00:00Z` as a `DateTimeOffset`? Its formats are strict.

## Results

| Method | Mean | Ratio | Gen0 | Allocated |
| --- | --- | --- | --- | --- |
| Split (baseline) | | | | |
| Span | | | | |
| Utf8Bytes | | | | |
| SpanFromBytes | | | | |

## Why each version allocates what it does

{One paragraph per version, in your own words: compare the Allocated column with the prediction, and explain any difference.}
