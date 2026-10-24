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

| Method | Mean | StdDev | Median | Ratio | Gen0 | Allocated | Predicted |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Split (baseline) | 407.1 ns | 35.82 ns | 383.7 ns | 1.01 | 0.0629 | 264 B | ≈ 256 B |
| Span | 320.9 ns | 4.96 ns | 317.8 ns | 0.79 | 0.0095 | 40 B | ≈ 32 B |
| Utf8Bytes | 409.3 ns | 10.40 ns | 407.2 ns | 1.01 | 0.0095 | 40 B | ≈ 32 B |
| SpanFromBytes | 391.4 ns | 36.72 ns | 416.7 ns | 0.97 | 0.0362 | 152 B | ≈ 144 B |

Gen0 = Gen 0 collections per 1,000 operations. Version 3 parses the timestamp with `DateTimeOffset.TryParseExact` over a `stackalloc` char buffer, because `Utf8Parser`'s `'O'` format requires 7 fraction digits and rejects `2026-10-03T10:00:00Z`.

## Why each version allocates what it does

> Drafted by Claude on request, from my answers in the session. Reread it.

**Why every prediction was 8 B short.** A .NET string is an 8-byte object header + an 8-byte method-table pointer + a 4-byte length + 2 bytes per character + a 2-byte **null terminator** (`'\0'`), rounded up to a multiple of 8. So the rule is **22 + 2n**, not 20 + 2n. The terminator is in every string, but it only matters when it pushes a string over an 8-byte boundary, and only `"DEV-42"` crosses one (6 chars: 34 B → 40 B instead of 32 B). The timestamp (62 → 64), the numbers (30–32 → 32) and the 44-char line (110 → 112) stay in the same bucket. Every version allocates the device id, so every row is exactly +8 B. `TelemetryReading` itself is a `readonly record struct` returned by value, so it adds **0 B** in all four versions.

**Version 1, `Split` (264 B).** `Split` allocates a `string[]` of 5 references (64 B) and then copies each field into its own new string: 40 + 64 + 32 + 32 + 32 = 200 B. That makes 6 heap objects per line, and 5 of them become garbage as soon as the method returns (only the device id survives, inside the result). `double.Parse` and `DateTimeOffset.Parse` read the existing strings and allocate nothing more.

**Version 2, spans (40 B).** Slicing a `ReadOnlySpan<char>` creates a window (pointer + length) onto the original line, on the stack, with no copy. The span overloads of `double.Parse` / `DateTimeOffset.TryParse` read the characters in place. The only allocation is `deviceId.ToString()`. It's needed because the result has to store the id, and a span can't be stored in a field of a type that might live on the heap (spans are stack-only, `ref struct`).

**Version 3, `Utf8Parser` (40 B).** Same reasoning, over bytes: `IndexOf((byte)';')` and slices are free, `Utf8Parser.TryParse` reads the numbers straight from the bytes, and `Encoding.UTF8.GetString` for the device id is the only heap allocation. The timestamp workaround uses a `stackalloc` char buffer, which lives on the stack, so it adds nothing to the Allocated column.

**Version 4, spans from bytes (152 B).** Same as version 2, plus `Encoding.UTF8.GetString` on the whole 44-byte line first: a 112-byte string that exists only to be sliced and thrown away. 112 + 40 = 152 B. This is the honest comparison for the real pipeline, where telemetry arrives as UTF-8 bytes: versions 1 and 2 hide this decoding cost because the benchmark gives them a ready-made string.

**Speed: fewer allocations ≠ much faster.** Allocation dropped 6.6× (264 → 40 B), but time only dropped ~21 % (407 → 321 ns), and only Span is clearly faster. Allocating in Gen0 is cheap (the allocator just moves a pointer), so most of the time goes into the parsing work that all versions share: one date and three doubles from text. Utf8Bytes is no faster than Split. Part of that is my `TryParseExact` workaround (format-string parsing isn't cheap), and `Utf8Parser.TryParse(double)` isn't faster than the modern `double.Parse` either. The real win is **GC pressure**: Gen0 collections per 1,000 calls fall from 0.0629 to 0.0095 (~6.6× fewer). At M08's ingest rate (thousands of lines per second, forever), that's the difference between frequent Gen0 GCs and rare ones, and fewer short-lived objects that could get promoted when a GC catches them mid-flight.

**How much to trust these numbers.** Span is stable (StdDev 5 ns, mean ≈ median). Split and SpanFromBytes are noisy (StdDev ~36 ns, and SpanFromBytes' median of 417 ns is *above* its 391 ns mean: most runs were slow, a few fast ones pulled the average down). So the 0.97 vs 1.01 gap between SpanFromBytes and Split is within the noise: Split, Utf8Bytes and SpanFromBytes are "about the same". A cleaner run would need the laptop plugged in, the High-performance power plan and no other apps open. The Allocated column is deterministic and doesn't have this problem: it's the trustworthy result of this lab.

**Open question for M08:** don't parse the timestamp with a general-purpose parser at all. The format is fixed, so parse the digits by position (`Utf8Parser` on `yyyy`, `MM`, … slices, or by hand) and build the `DateTimeOffset` from the parts. Benchmark it against the current version 3.
