# M01-T4 · Memory, the GC and allocation-free code

Task: [M01-T4](../modules/M01-csharp-runtime.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [Fundamentals of garbage collection](https://learn.microsoft.com/dotnet/standard/garbage-collection/fundamentals) · [Memory- and span-related types](https://learn.microsoft.com/dotnet/standard/memory-and-spans/) · [Memory\<T\> and Span\<T\> usage guidelines](https://learn.microsoft.com/dotnet/standard/memory-and-spans/memory-t-usage-guidelines) · [BenchmarkDotNet: getting started](https://benchmarkdotnet.org/articles/guides/getting-started.html)

**Reading time:** about 60 minutes. **Builds on:** [M01-T1](M01-T1-async-await.md) §8 (why allocations matter) and the GC counters you saw in your T2 CSVs.

## Contents

1. [Stack and heap: where data lives](#1-stack-and-heap-where-data-lives)
2. [How the garbage collector works](#2-how-the-garbage-collector-works)
3. [Generations](#3-generations)
4. [The large object heap](#4-the-large-object-heap)
5. [What triggers a GC, and what it costs](#5-what-triggers-a-gc-and-what-it-costs)
6. [GC modes: workstation, server, concurrent](#6-gc-modes-workstation-server-concurrent)
7. [Where allocations hide in everyday code](#7-where-allocations-hide-in-everyday-code)
8. [`Span<T>`: a view over memory, without copying](#8-spant-a-view-over-memory-without-copying)
9. [Why a span can't live on the heap](#9-why-a-span-cant-live-on-the-heap)
10. [`Memory<T>` and the usage guidelines](#10-memoryt-and-the-usage-guidelines)
11. [Parsing without allocating: chars vs UTF-8 bytes](#11-parsing-without-allocating-chars-vs-utf-8-bytes)
12. [Benchmarking correctly with BenchmarkDotNet](#12-benchmarking-correctly-with-benchmarkdotnet)
13. [When not to optimise allocations](#13-when-not-to-optimise-allocations)
14. [Glossary](#14-glossary)
15. [Summary, check yourself, interview questions, for FleetTrack](#15-summary)

---

## 1. Stack and heap: where data lives

A .NET program uses two kinds of memory.

**The stack.** Each thread has its own stack, a small region (about 1 MB by default) used like a pile of plates. When a method is called, a **stack frame** for its local variables is pushed on top; when it returns, the frame is popped. Allocating on the stack is just moving a pointer, and freeing is automatic and instant when the method returns. But stack data **only lives as long as the method call**.

**The managed heap.** A large shared region for objects that must outlive the method that created them. Objects on the heap stay alive as long as something **refers** to them, and the **garbage collector** (GC) frees them once nothing does.

Where a value ends up depends on its **type**:

| | Value types | Reference types |
| --- | --- | --- |
| Examples | `int`, `double`, `bool`, `DateTime`, `struct`, `record struct`, enums | `string`, arrays, `class`, `record`, delegates, `List<T>` |
| A variable holds… | the value itself | a **reference** (an address) to an object on the heap |
| `new` allocates on the heap? | No | **Yes, always** |

The precise rule is: **value types live wherever their container lives.** A `double` local variable lives in the stack frame. A `double` field of a class lives inside that object, on the heap. An array of `double` stores all its values inside the array object, on the heap.

**Boxing** is the sneaky case. When a value type is converted to `object` or to an interface, .NET wraps a **copy** of it in a new heap object:

```csharp
int speed = 63;
object boxed = speed;                         // allocates a box on the heap
Console.WriteLine("Speed: {0}", speed);       // older overloads take object → boxing
```

(Modern C# string interpolation, `$"Speed: {speed}"`, avoids boxing for most types.)

---

## 2. How the garbage collector works

**Allocating is very cheap.** The heap keeps an **allocation pointer**. `new` hands out the memory at the pointer and moves the pointer forward by the object's size. That's about as fast as allocating on the stack. The cost of heap memory comes **later**, when the GC has to clean up.

**Collecting has three phases:**

```
1. MARK      Start from the ROOTS (local variables in every thread's stack frames,
             static fields, CPU registers, GC handles) and follow every reference.
             Every object reached is LIVE. Everything else is garbage.

2. SWEEP     The space of dead objects is now free.

3. COMPACT   Slide the live objects together, to remove the holes,
             and update every reference to point to the new addresses.
             The allocation pointer goes back to the end of the live data.
```

Two consequences matter in practice:
- **The GC's cost depends on how many objects are *alive*, not on how many died.** Dead objects are never touched; they're just overwritten later. So allocating a million short-lived objects that die immediately is cheaper than you might expect, but not free (§5).
- **Compacting moves objects.** That's why a raw pointer to a managed object is only safe while the object is **pinned** (marked "don't move me"), and why too much pinning hurts the GC.

---

## 3. Generations

The GC relies on an observation called the **generational hypothesis**: **most objects die young.** A request's temporary strings, a LINQ iterator, the `string[]` from a `Split` all become garbage within microseconds. A few objects (caches, singletons, configuration) live for the whole process.

So the heap is split into **generations**, and young objects are collected far more often than old ones:

```
 new objects ─►  Gen 0  ──survives──►  Gen 1  ──survives──►  Gen 2
                (young,               (buffer between        (old, long-lived;
                 small budget,         young and old)          collected rarely,
                 collected very often)                         most expensive)
```

- **Gen 0** is where almost every new object starts. When its **budget** (a size the runtime tunes dynamically, typically a few MB) is used up, a **gen 0 GC** runs. It only has to examine gen 0, so it's fast. Usually well under a millisecond.
- Objects that survive a gen 0 GC are **promoted** to gen 1. Gen 1 acts as a buffer, so objects that live "just a bit longer" don't go straight to gen 2.
- Survivors of a gen 1 GC are promoted to **gen 2**. A **gen 2 GC** is a **full** collection of the whole heap, so it's the expensive one.
- Collecting a generation **also collects all younger generations**: a gen 1 GC collects gen 0 and 1; a gen 2 GC collects everything.

How does a gen 0 GC avoid scanning gen 2 for references *into* gen 0? The runtime records which old-generation memory has been written with references to young objects (a structure called the **card table**), and only scans those areas. You don't need to manage it; it's why the generational scheme stays cheap.

**The pattern to avoid: mid-life crisis.** Objects that live *just* long enough to be promoted to gen 2, then die. They make gen 2 grow, which triggers expensive full GCs. A typical cause is a cache with a short expiry, or objects held across a slow `await`. Short-lived garbage is cheap, long-lived objects are fine; objects that live "medium" long are the expensive ones.

---

## 4. The large object heap

Objects of **85,000 bytes or more** go to the **large object heap** (LOH), which is treated as part of gen 2. For example, a `byte[]` of 100 KB, or a `List<T>` whose internal array has grown past that size.

- Copying big objects during compaction is expensive, so the LOH is **not compacted by default**. Freed space is reused, but it can **fragment**: lots of free holes, none big enough for the next large allocation, so the heap grows.
- LOH objects are only collected in **gen 2** GCs. So allocating many temporary large arrays triggers full collections.
- The fix for hot paths is usually **reusing buffers** (with `ArrayPool<T>.Shared.Rent(...)` and `Return(...)`) instead of allocating a new large array each time.

There's also a **pinned object heap** (POH), for objects that are allocated already pinned, such as I/O buffers handed to the operating system. Keeping them apart stops them from fragmenting the normal heap. (You saw `loh` and `poh` rows in the GC counters of your T2 CSVs.)

---

## 5. What triggers a GC, and what it costs

**A GC runs when:**
1. **Gen 0's allocation budget is used up.** By far the most common trigger: *every allocation brings the next GC a little closer.*
2. **The system is low on memory**, or the process is near a memory limit (for example a container limit).
3. **Your code calls `GC.Collect()`.** Almost always a mistake in production code. It forces an expensive full collection and disturbs the GC's own tuning.

**What a GC costs:**
- **Pauses.** During parts of a collection, the GC must **suspend all managed threads** (a "stop the world" pause) so that objects don't change while it marks and moves them. A gen 0 pause is short; a full blocking gen 2 pause on a large heap can take tens or hundreds of milliseconds. That shows up as **latency spikes**: your p99 latency, not the median.
- **CPU time** spent marking and compacting instead of doing work.

So "fewer allocations" means "fewer GCs", which means fewer pauses and less CPU. That's the whole case for allocation-free code on a **hot path**: code that runs thousands or millions of times per second, like FleetTrack's telemetry ingest in M08.

You can see allocations live with the tools from T2: `dotnet-counters` shows `dotnet.gc.heap.total_allocated` (bytes allocated per second), `dotnet.gc.collections` per generation, and `dotnet.gc.pause.time`.

---

## 6. GC modes: workstation, server, concurrent

| Mode | How it works | Default for |
| --- | --- | --- |
| **Workstation GC** | One heap; the GC runs on the thread that triggered it | Console and desktop apps |
| **Server GC** | One heap **per logical processor**, each collected by its own dedicated thread, in parallel | ASP.NET Core apps |
| **Background (concurrent) GC** | Gen 2 collections mostly run **alongside** your threads, with only short pauses | On by default in both modes |

**The trade-off:** Server GC gives higher throughput on multi-core machines, but uses much more memory (each heap has its own budget), which matters in a small container. Since .NET 8, a feature called **DATAS** (dynamic adaptation to application sizes) lets Server GC start small and adjust the number of heaps to the load, and it's on by default for Server GC in recent .NET versions. Containers also matter: .NET respects the container's memory and CPU limits when sizing the GC.

You rarely change these settings. Knowing they exist matters for interviews and for diagnosing "why does my small container use so much memory?".

---

## 7. Where allocations hide in everyday code

| Code | What it allocates |
| --- | --- |
| `line.Split(';')` | A `string[]`, **plus a new string for every part** (each part's characters are copied) |
| `text.Substring(3, 5)` | A new string with a copy of those characters |
| `a + b + c` or `$"…"` creating a string | A new string (the result) |
| `double.Parse(someString)` | Nothing, if the string already exists. The cost is in creating that string |
| LINQ (`.Where(...).Select(...)`) | Iterator objects and delegates (closures if lambdas capture variables) |
| A lambda that captures a local variable | A closure object, plus a delegate |
| `params object[]`, string formatting with `object` | An array, and boxing of value types |
| `async` methods that really wait | The state machine box (M01-T1 §4) |
| `new List<T>()` that grows | Each time it grows, a new, bigger internal array (the old one becomes garbage) |

Strings deserve special mention: **a .NET string is immutable**, so every operation that "changes" a string creates a **new** one. A string of *n* characters takes about `20 + 2n` bytes on a 64-bit machine (an object header, a length, and 2 bytes per UTF-16 character), rounded up to a multiple of 8.

---

## 8. `Span<T>`: a view over memory, without copying

`Span<T>` (and its read-only twin, `ReadOnlySpan<T>`) is a **window** onto a contiguous block of memory that already exists: part of an array, part of a string, memory on the stack, or even native memory. It's just a **reference to the start plus a length**. **Creating or slicing a span copies nothing and allocates nothing.**

```csharp
string line = "DEV-42;2026-10-03T10:00:00Z;45.75;21.22;63.5";

ReadOnlySpan<char> span = line.AsSpan();   // a view over the string's characters
int sep = span.IndexOf(';');               // 6
ReadOnlySpan<char> deviceId = span[..sep]; // "DEV-42": still the SAME memory, no new string
ReadOnlySpan<char> rest = span[(sep + 1)..];
```

```
line:   D E V - 4 2 ; 2 0 2 6 - 1 0 - 0 3 T … ; 4 5 . 7 5 ; …
        └─deviceId─┘  └───────── rest (same memory) ─────────…
```

Most parsing APIs accept spans directly, so you can parse without creating substrings:

```csharp
double lat = double.Parse(span.Slice(start, length), CultureInfo.InvariantCulture);
```

When you finally need a real string (to store the device id in a long-lived object, for example), you call `deviceId.ToString()`, which allocates **once**, exactly where you decided.

**`stackalloc`** gives you a temporary buffer on the stack, as a span, with no heap allocation at all:

```csharp
Span<byte> buffer = stackalloc byte[64];   // freed automatically when the method returns
```

Keep `stackalloc` small (a few hundred bytes to a couple of KB): the stack is only about 1 MB, and overflowing it crashes the process.

---

## 9. Why a span can't live on the heap

`Span<T>` is a **`ref struct`**: a special kind of struct that the compiler guarantees **only ever lives on the stack**. That's why these don't compile:

```csharp
class Cache { Span<byte> _buffer; }       // ❌ a field of a class lives on the heap
object o = span;                          // ❌ boxing would copy it to the heap
List<Span<byte>> list;                    // ❌ generic type arguments can be stored on the heap
Func<int> f = () => span.Length;          // ❌ a lambda capture becomes a heap closure field
```

And in an `async` method, a span can't be used **across an `await`**, because the method's locals are hoisted into a state machine that may be boxed to the heap (M01-T1 §4). Since C# 13 you may declare a span local in an async method, as long as it isn't used after an `await`.

**Why the restriction?** A span can point into memory that's only valid for a short time: a `stackalloc` buffer in some method's stack frame, or the inside of an array that might be returned to a pool. If a span could be stored on the heap, it could **outlive the memory it points to**, and later reads would see garbage or someone else's data, a classic memory-safety bug from C/C++. Keeping spans on the stack lets the compiler prove they never outlive their memory. The cost is that you can't keep them around, which is what `Memory<T>` is for.

---

## 10. `Memory<T>` and the usage guidelines

`Memory<T>` (and `ReadOnlyMemory<T>`) is the **heap-friendly** counterpart of `Span<T>`. It's a normal struct, so it can be a field, be captured, be stored in a list, and **survive across `await`**. When you want to actually read or write the data, you ask for `.Span` and work with the span **synchronously**.

```csharp
async Task ProcessAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
{
    await Task.Delay(10, ct);                 // Memory<T> can cross awaits…
    ReadOnlySpan<byte> bytes = data.Span;     // …then get a span for the synchronous work
    Parse(bytes);
}
```

The main guidelines from Microsoft's usage rules, with the reason for each:
- **Synchronous methods take `Span<T>` / `ReadOnlySpan<T>` parameters.** Spans are the cheapest and work with every kind of memory, including `stackalloc`.
- **Asynchronous methods take `Memory<T>` / `ReadOnlyMemory<T>`**, because spans can't cross `await`.
- **Use the read-only variants when you only read**, so callers can pass strings and read-only data.
- **Ownership must be clear.** Someone allocated the underlying buffer and someone frees or returns it. With pooled buffers (`ArrayPool<T>`, or `MemoryPool<T>` and its `IMemoryOwner<T>`), the **owner** returns the buffer, and nobody may keep a `Memory<T>` pointing into it afterwards. A consumer that only *borrows* a buffer must not store it beyond the call.

---

## 11. Parsing without allocating: chars vs UTF-8 bytes

FleetTrack's telemetry arrives from devices over the network as **bytes**, encoded in **UTF-8** (one byte per ASCII character). A .NET `string` is **UTF-16** (two bytes per character). So there are two very different ways to parse:

```
 network bytes (UTF-8)                                              your parsed values
 ─────────────────────                                              ──────────────────
  Version 1:  bytes ──decode──► string ──Split──► 5 strings ──Parse──► values
  Version 2:  bytes ──decode──► string ──AsSpan, slice──────Parse──► values
  Version 3:  bytes ────────────────────── slice ──Utf8Parser──────► values
```

- **Version 1, `string.Split` + `double.Parse`:** simple and readable. Allocates the array and one new string per field.
- **Version 2, `ReadOnlySpan<char>` slicing:** finds each `;` with `IndexOf`, slices, and parses the slice directly (`double.Parse(ReadOnlySpan<char>, …)`). No substrings. But it still needs the line as a `string` (or chars) first.
- **Version 3, `Utf8Parser` over bytes:** `System.Buffers.Text.Utf8Parser.TryParse(ReadOnlySpan<byte>, out double value, out int bytesConsumed)` parses numbers **straight from the UTF-8 bytes**, so you can skip decoding into a string entirely. Find the separators with `IndexOf((byte)';')`.

**A culture trap that bites in Romania.** `double.Parse("45.75")` uses the **current culture** by default. In the Romanian culture (`ro-RO`), the decimal separator is a **comma**, so `"45.75"` parses wrongly or throws. **Machine-to-machine data must always be parsed with `CultureInfo.InvariantCulture`.** `Utf8Parser` is always invariant, which is one less thing to get wrong.

**Timestamps.** `DateTimeOffset.Parse` also accepts a span. `Utf8Parser` can parse `DateTimeOffset` too, but only in a few fixed formats, and the `'O'` (round-trip) format is strict about its exact shape. Check what it accepts for `2026-10-03T10:00:00Z`, and decide what to do if it doesn't match. That's part of the lab.

**Design for reuse (M08):** return a small value type rather than a class, for example a `readonly record struct TelemetryReading(...)`, so that parsing a line can allocate **nothing at all**, apart from the device id if you choose to keep it as a string.

---

## 12. Benchmarking correctly with BenchmarkDotNet

### 12.1 Why not `Stopwatch` in a loop?

Measuring nanosecond-scale code is full of traps: the JIT compiles code on the first call (so early iterations are slow), it may optimise away work whose result is never used, timer resolution is coarse, background activity adds noise, and a single measurement says nothing about variance. **BenchmarkDotNet** (free, MIT licence) handles all of this: it runs each benchmark in a **separate process**, does warm-up runs, picks the number of iterations automatically, and reports statistics.

### 12.2 A minimal benchmark

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<ParsingBenchmarks>();

[MemoryDiagnoser]                         // adds the Gen0 and Allocated columns
public class ParsingBenchmarks
{
    private const string Line = "DEV-42;2026-10-03T10:00:00Z;45.75;21.22;63.5";

    [Benchmark(Baseline = true)]          // the others are compared against this one (Ratio column)
    public TelemetryReading Split() => SplitParser.Parse(Line);

    [Benchmark]
    public TelemetryReading Span() => SpanParser.Parse(Line);
}
```

Run it **in Release, without a debugger**: `dotnet run -c Release`. BenchmarkDotNet refuses to give trustworthy numbers for Debug builds.

### 12.3 Reading the results

| Column | Meaning |
| --- | --- |
| **Mean** | The average time per operation (ns = nanoseconds, µs = microseconds) |
| **Error / StdDev** | How much the measurements varied. If these are large compared with Mean, the result is noisy |
| **Ratio** | Mean compared with the baseline (0.25 = four times faster) |
| **Gen0** | Gen 0 collections **per 1,000 operations** |
| **Allocated** | **Bytes allocated per operation**. `-` means zero. The most useful column for this task |

### 12.4 Pitfalls

- **Return the result.** If a benchmark method computes a value and throws it away, the JIT may remove the work, and you'll benchmark nothing. Returning the value (as above) prevents that.
- **Same input for all versions**, and set up inputs once (fields, or a `[GlobalSetup]` method), so that you don't measure the setup. For the byte version, convert the line to UTF-8 bytes **once**, in setup.
- **Check correctness first.** A fast parser that returns wrong values is worthless. Assert that all three return the same reading before you benchmark them.
- **Don't compare across machines or runs casually.** Compare versions within the **same** run, which is what the Ratio column is for.
- **A benchmark is a microscope, not the whole picture.** 50 ns saved per line only matters if you parse millions of lines. Section 13 is about when it matters.

---

## 13. When not to optimise allocations

Span-based parsing is harder to read, easier to get wrong (index arithmetic, edge cases) and harder to change. Senior engineers apply it **where it pays**:

- **Do it** on proven hot paths: the parser behind M08's telemetry ingest (thousands of messages per second), serializers, protocol handlers, logging libraries.
- **Don't do it** in code that runs a few times per request, such as an endpoint that loads 20 shipments from the database. A database call takes milliseconds; saving 200 bytes and 50 ns there is noise.
- **Measure first.** Use `dotnet-counters` (allocation rate, GC count and pause time) or a profiler to find where allocations actually come from. Then benchmark the alternatives.
- **The simple version is the right default.** `string.Split` is clear, and clarity has real value for the next developer. Write the optimised version when the numbers justify it, and say so in a comment.

---

## 14. Glossary

| Term | Meaning |
| --- | --- |
| **Stack / stack frame** | Per-thread memory for method calls; a frame holds one call's locals and is freed on return |
| **Managed heap** | Shared memory for objects, freed by the garbage collector |
| **Value type / reference type** | A type whose variables hold the value itself / a reference to a heap object |
| **Boxing** | Wrapping a copy of a value type in a heap object (when converted to `object` or an interface) |
| **GC (garbage collector)** | The runtime component that finds unreachable objects and reclaims their memory |
| **Roots** | Starting points for marking: locals on thread stacks, static fields, registers, GC handles |
| **Mark / sweep / compact** | Find live objects / free the rest / slide live objects together and update references |
| **Generation (0, 1, 2)** | Age groups of objects; young ones are collected often and cheaply, old ones rarely |
| **Promotion** | Moving an object that survived a GC to the next older generation |
| **LOH (large object heap)** | Heap area for objects ≥ 85,000 bytes; collected with gen 2, not compacted by default |
| **POH (pinned object heap)** | Heap area for objects allocated pinned, so they don't fragment the normal heap |
| **Pinning** | Marking an object so the GC won't move it during compaction |
| **GC pause** | Time the GC suspends application threads |
| **Workstation / Server GC** | One heap / one heap per logical processor, collected in parallel |
| **`Span<T>` / `ReadOnlySpan<T>`** | A stack-only view (reference + length) over contiguous memory; slicing copies nothing |
| **`ref struct`** | A struct the compiler keeps on the stack: no boxing, no heap fields, no captures |
| **`Memory<T>`** | A heap-storable counterpart of a span; use `.Span` to access the data |
| **`stackalloc`** | Allocate a small temporary buffer on the stack |
| **`ArrayPool<T>`** | A shared pool of reusable arrays (`Rent`/`Return`) to avoid repeated allocations |
| **UTF-8 / UTF-16** | Text encodings: 1 byte per ASCII char (network, files) / 2 bytes per char (.NET `string`) |
| **`Utf8Parser`** | `System.Buffers.Text` API that parses numbers and dates directly from UTF-8 bytes |
| **Invariant culture** | Culture-neutral formatting rules (`.` as decimal separator); use for machine data |
| **BenchmarkDotNet** | The standard .NET micro-benchmarking library |
| **Hot path** | Code that runs so often that its cost dominates the system's performance |

---

## 15. Summary

1. Value types live where their container lives; `new` on a class, arrays and strings always allocate on the heap; boxing allocates in disguise.
2. Allocating is cheap. The cost comes later: every allocation brings the next GC closer, and GCs cost CPU and pauses.
3. The GC is generational: gen 0 is collected often and cheaply, gen 2 rarely and expensively. Objects of 85,000 bytes or more go to the LOH, which is collected with gen 2 and can fragment.
4. `Span<T>` is a zero-copy view over existing memory. It's a `ref struct`, so it can't live on the heap or cross an `await`; that rule is what keeps it memory-safe. `Memory<T>` is the storable, async-friendly counterpart.
5. Parse machine data with the invariant culture. `Utf8Parser` parses straight from UTF-8 bytes and skips creating a string at all.
6. Benchmark with BenchmarkDotNet in Release, with `[MemoryDiagnoser]`, returning results, after checking correctness.
7. Optimise allocations only on proven hot paths; the simple version is the right default elsewhere.

### Check yourself

1. Where does each of these live: a `double` local, a `double` field of a class, a `double[]`? (§1)
2. Why is allocating on the heap cheap, and where does the real cost show up? (§2, §5)
3. What is the generational hypothesis, and how does the GC exploit it? What's a "mid-life crisis"? (§3)
4. Your service allocates a new 200 KB buffer for every request. What happens in the GC, and how would you fix it? (§4)
5. Why can't a `Span<char>` be a field of a class? What would go wrong if it could? (§9)
6. A method is `async`. Should its buffer parameter be `Span<byte>` or `Memory<byte>`, and why? (§10)
7. `double.Parse("45.75")` works on your colleague's machine and fails on yours. What's going on, and what's the fix? (§11)
8. Name two ways a benchmark can measure the wrong thing. (§12.4)

**Before you build: predict.** For the line `"DEV-42;2026-10-03T10:00:00Z;45.75;21.22;63.5"`, estimate for each of the three versions:
- **Allocated** bytes per parse: count the arrays and strings each version creates, using the string size rule from §7 (about `20 + 2 × length` bytes, rounded up to a multiple of 8), plus the `string[]` for `Split`.
- **The order of speed:** which version is fastest, and roughly by how much?

Post your predictions here before running anything.

### Interview questions this prepares you for

- "Explain how the .NET garbage collector works. What are generations, and why do they exist?"
- "What is the large object heap, and what problems can it cause?"
- "What's the difference between `Span<T>` and `Memory<T>`? Why can't a span be stored in a field?"
- "Our API has latency spikes every few seconds. How would you check whether it's the GC?"
- "Workstation vs Server GC: which does ASP.NET Core use, and what's the trade-off?"
- "How would you benchmark two implementations correctly?"
- "When is allocation-free code *not* worth it?"

### For FleetTrack

- **The lab** goes in `labs/M01-T4-parsing`: a console project with the three parsers, a quick correctness check (all three return the same reading), and a BenchmarkDotNet class with `[MemoryDiagnoser]` and the `Split` version as baseline.
- **BenchmarkDotNet** is a new package: it's MIT-licensed (fine for FleetTrack rule 7), and its version goes in `Directory.Packages.props` like every other package.
- **The ✅ write-up** goes in `docs/perf/M01-parsing.md`: the results table, plus one paragraph explaining *why* each version allocates what it does.
- **Reuse in M08:** the winning parser becomes the core of the telemetry ingest path, returning a `readonly record struct TelemetryReading`. That's the hot path where this work actually pays for itself.
