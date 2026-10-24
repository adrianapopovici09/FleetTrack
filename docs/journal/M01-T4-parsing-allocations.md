# M01-T4: allocation-free parsing of a telemetry line

* **Date:** 2026-10-24 (predictions written 2026-10-21)
* **Task:** M01-T4 (Memory, the GC and allocation-free code) · [lesson](../plans/reading/M01-T4-memory-gc-spans.md)
* **Lab:** [labs/M01-T4-parsing](../../labs/M01-T4-parsing/) (`Parsers.cs`, `ParsingBenchmarks.cs`)
* **Note:** the "Why" and production sections were drafted by Claude from my answers in the session. Reread them and rewrite anything I couldn't explain myself.

## What I did

I parsed the telemetry line `"DEV-42;2026-10-03T10:00:00Z;45.75;21.22;63.5"` (44 characters / 44 UTF-8 bytes) into a `readonly record struct TelemetryReading(string DeviceId, DateTimeOffset Timestamp, double Latitude, double Longitude, double Speed)`, in four ways:

1. **Split:** `string.Split(';')` + `double.Parse` / `DateTimeOffset.Parse`.
2. **Span:** `ReadOnlySpan<char>` slicing with `IndexOf(';')`, parsing each slice in place.
3. **Utf8Bytes:** `Utf8Parser.TryParse` over the UTF-8 bytes, with no string for the line at all.
4. **SpanFromBytes** (extra): version 2, but starting from bytes (`Encoding.UTF8.GetString` first), which is what the real pipeline would have to do.

I benchmarked them with BenchmarkDotNet 0.15.8 and `[MemoryDiagnoser]`, with Split as the baseline, a Release build and no debugger, on 4 physical cores / 8 logical processors and .NET 10. A correctness check runs first and blocks the benchmarks until all parsers return the same result.

```powershell
dotnet run -c Release --project labs/M01-T4-parsing -- --check   # correctness only
dotnet run -c Release --project labs/M01-T4-parsing              # correctness, then benchmarks
```

### My prediction (written before running)

Rule of thumb from the lesson: a string of *n* characters takes about `20 + 2n` bytes, rounded up to a multiple of 8; an array of 5 references takes 5 × 8 plus a ~24-byte header.

| Allocation in Split | Characters | Bytes |
| --- | --- | --- |
| `string[]` (5 references) | — | ~64 |
| `"DEV-42"` | 6 | 32 |
| `"2026-10-03T10:00:00Z"` | 20 | 64 |
| `"45.75"` | 5 | 32 |
| `"21.22"` | 5 | 32 |
| `"63.5"` | 4 | 32 |
| **Total** | | **≈ 256 B** |

| | Allocated | Speed |
| --- | --- | --- |
| 1 Split | ≈ 256 B | Slowest: 6 objects allocated and every character copied |
| 2 Span | ≈ 32 B (only `deviceId.ToString()`) | Much faster than 1 |
| 3 Utf8Bytes | ≈ 32 B (only `GetString` for the device id) | About the same as 2 |
| 4 SpanFromBytes | ≈ 32 B + the decoded line (~112 B) ≈ 144 B | Slower than 3: it pays for decoding the whole line |

Open question: does `Utf8Parser` accept `2026-10-03T10:00:00Z` as a `DateTimeOffset`?

## What I observed

| Method | Mean | StdDev | Median | Ratio | Gen0 | Allocated | Predicted |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Split (baseline) | 407.1 ns | 35.82 ns | 383.7 ns | 1.01 | 0.0629 | 264 B | ≈ 256 B |
| Span | 320.9 ns | 4.96 ns | 317.8 ns | 0.79 | 0.0095 | 40 B | ≈ 32 B |
| Utf8Bytes | 409.3 ns | 10.40 ns | 407.2 ns | 1.01 | 0.0095 | 40 B | ≈ 32 B |
| SpanFromBytes | 391.4 ns | 36.72 ns | 416.7 ns | 0.97 | 0.0362 | 152 B | ≈ 144 B |

Gen0 = Gen 0 collections per 1,000 operations.

* **Every allocation is exactly 8 B more than predicted.** The shape of the prediction was right: Split ≫ spans, and spans-from-bytes pays for the decoded line.
* **Speed didn't follow allocations.** Allocation dropped 6.6× (264 → 40 B), but time only dropped ~21 %, and Utf8Bytes is no faster than Split.
* **`Utf8Parser` rejects the timestamp.** `'o'` throws "Format specifier was invalid" (it only knows uppercase), and `'O'` needs exactly 7 fraction digits (`…T10:00:00.0000000Z`). So version 3 copies the 20 timestamp bytes into a `stackalloc` char buffer and uses `DateTimeOffset.TryParseExact(…, "yyyy-MM-ddTHH:mm:ssK", …)`.
* **Two rows are noisy.** Split and SpanFromBytes have a StdDev of ~36 ns, and SpanFromBytes' median (417 ns) is *above* its mean (391 ns): most runs were slow, and a few fast ones pulled the average down. Span is stable (StdDev 5 ns, mean ≈ median).

## What would happen in production

In M08 this parser sits on the ingest path: telemetry arrives as UTF-8 bytes, thousands of lines per second, all day.

* **The honest comparison is from bytes.** Split and Span need a string first, so in production they cost their own allocation *plus* the decoded line (~112 B): Split ≈ 376 B per line, Span ≈ 152 B (the SpanFromBytes row). Only Utf8Bytes stays at 40 B.
* **At 10,000 lines/s,** Split produces ~3.8 MB of garbage per second and Utf8Bytes ~0.4 MB. With this machine's Gen0 budget (Gen0 column × 10), that's about one Gen0 GC every 1.6 s for Split (more often once decoding is counted) and one every ~10 s for Utf8Bytes. Each GC pauses the ingest threads briefly, and any object still in flight when it runs (a line half-parsed, a batch waiting to be written) gets promoted to Gen1. That makes later collections more expensive.
* **Per-call speed barely matters:** 400 ns × 10,000 = 4 ms of CPU per second, under 1 % of one core. The thing that grows with load is GC work, not parsing time.
* **Bad input matters more than speed.** `SpanParser` ignores its `TryParse` results, so a corrupt line becomes a device at lat/lon (0, 0) with `Timestamp = default`, and nothing fails. In a fleet map, that's a truck in the Gulf of Guinea and a silent data-quality bug. `Utf8BytesParser` throws instead, which is what ingest needs: reject the line, count it and move on.

## Why

**Why every prediction was 8 B short.** A .NET string is an 8-byte object header + an 8-byte method-table pointer + a 4-byte length + 2 bytes per character + a 2-byte **null terminator** (`'\0'`), rounded up to a multiple of 8. So the rule is **22 + 2n**, not 20 + 2n. The terminator is in every string, but it only changes the size when it pushes a string over an 8-byte boundary. Only `"DEV-42"` crosses one (6 chars: 34 B → 40 B instead of 32 B). The timestamp (62 → 64), the numbers (30–32 → 32) and the 44-char line (110 → 112) stay in the same bucket. Every version allocates the device id, so every row is exactly +8 B. `TelemetryReading` itself is a `readonly record struct` returned by value, so it adds **0 B** in all four versions.

**Split (264 B).** `Split` allocates a `string[]` of 5 references (64 B), then copies each field into its own new string: 40 + 64 + 32 + 32 + 32 = 200 B. That's 6 heap objects per line, and 5 of them are garbage as soon as the method returns (only the device id survives, inside the result). `double.Parse` and `DateTimeOffset.Parse` read the existing strings and allocate nothing more.

**Span (40 B).** Slicing a `ReadOnlySpan<char>` creates a window (pointer + length) onto the original line, on the stack, with no copy. The span overloads of `double.TryParse` / `DateTimeOffset.TryParse` read the characters in place. The only allocation is `deviceId.ToString()`. It's needed because the result has to store the id, and a span can't be stored in a field of a type that might live on the heap (spans are stack-only, `ref struct`).

**Utf8Bytes (40 B).** Same reasoning, over bytes: `IndexOf((byte)';')` and slices are free, `Utf8Parser.TryParse` reads the numbers straight from the bytes, and `Encoding.UTF8.GetString` for the device id is the only heap allocation. The timestamp's `stackalloc` buffer lives on the stack, so it doesn't show in the Allocated column.

**SpanFromBytes (152 B).** Same as Span, plus `Encoding.UTF8.GetString` on the whole 44-byte line first: a 112-byte string that exists only to be sliced and thrown away. 112 + 40 = 152 B.

**Why fewer allocations didn't mean much faster.** Allocating in Gen0 is cheap: the allocator just moves a pointer forward. So most of the ~320–400 ns goes into the work all versions share, turning text into one date and three doubles. Utf8Bytes is no faster than Split, partly because of the `TryParseExact` workaround (format-string parsing isn't cheap), and partly because `Utf8Parser.TryParse(double)` isn't faster than the modern `double.Parse`. The real win of allocation-free code is **GC pressure**: Gen0 collections per 1,000 calls fall from 0.0629 to 0.0095 (~6.6× fewer).

**Why only some differences are real.** A benchmark difference is real only if it's clearly bigger than the spread (StdDev). Span's 321 ns ± 5 ns is clearly faster than Split's 407 ns ± 36 ns. The 0.97 vs 1.01 gap between SpanFromBytes and Split is inside the noise, so Split, Utf8Bytes and SpanFromBytes are "about the same". The noise comes from the machine (CPU clock changes, background apps, power mode), not the code. The Allocated column is deterministic and isn't affected, so it's the most trustworthy result of this lab.

## The fix / the rule I'll follow

On a hot path, I parse with spans over the bytes I actually receive and allocate only what I keep. I judge the result by the Allocated and Gen0 columns, because the win is GC pressure, not per-call speed, and I treat time differences within the StdDev as noise.

Open for M08: the timestamp format is fixed, so parse it by position (`Utf8Parser` on the `yyyy`, `MM`, … slices, or by hand) and build the `DateTimeOffset` from the parts. Benchmark that against the current version 3, and make `SpanParser` reject bad lines.
