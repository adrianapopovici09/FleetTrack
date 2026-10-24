# M01-T6 · Modern C# for team codebases

Task: [M01-T6](../modules/M01-csharp-runtime.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [What's new in C# 12](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-12) · [What's new in C# 13](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-13) · [What's new in C# 14](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14) · [Records](https://learn.microsoft.com/dotnet/csharp/fundamentals/types/records) · [Pattern matching](https://learn.microsoft.com/dotnet/csharp/fundamentals/functional/pattern-matching)

**Reading time:** about 50 minutes.

## Contents

1. [Language features are a team decision](#1-language-features-are-a-team-decision)
2. [Records: types defined by their values](#2-records-types-defined-by-their-values)
3. [`init` and `required`: object initializers you can trust](#3-init-and-required-object-initializers-you-can-trust)
4. [Primary constructors](#4-primary-constructors)
5. [Collection expressions](#5-collection-expressions)
6. [Pattern matching and switch expressions](#6-pattern-matching-and-switch-expressions)
7. [C# 13 in one page](#7-c-13-in-one-page)
8. [C# 14: the `field` keyword, extension members and the rest](#8-c-14-the-field-keyword-extension-members-and-the-rest)
9. [How a team decides what to adopt](#9-how-a-team-decides-what-to-adopt)
10. [Enforcing the decision: `.editorconfig` and the build](#10-enforcing-the-decision-editorconfig-and-the-build)
11. [A worked before/after](#11-a-worked-beforeafter)
12. [Glossary](#12-glossary)
13. [Summary, check yourself, interview questions, for FleetTrack](#13-summary)

---

## 1. Language features are a team decision

Every C# release adds shorter ways to write things you could already write. Each one is a trade-off between **brevity** and **how many people can read the code without looking something up**. On your own project, you can use whatever you like. In a team codebase, a feature is only an improvement if:

- **Everyone reads it the same way.** A switch expression with nested property patterns is clearer than ten `if`s to someone who knows patterns, and a puzzle to someone who doesn't.
- **Its semantics hold no surprises.** Some features look like something they're not. For example, a primary constructor parameter looks like a `readonly` field but isn't (§4).
- **It's used consistently.** Two styles for the same thing in one codebase (half the services with primary constructors, half without) cost more than either style alone, because every reader and every reviewer has to switch between them.
- **The build can enforce it.** A rule that lives only in a wiki is forgotten in a month. A rule in `.editorconfig` with the build failing on violations (§10) survives team changes.

**Which C# version do you get?** The compiler picks the language version from the target framework: .NET 8 → C# 12, .NET 9 → C# 13, **.NET 10 → C# 14** (FleetTrack). You *can* set `<LangVersion>latest</LangVersion>` or `preview`, but that lets code use features the runtime may not fully support, so senior teams leave the default alone. That's one more reason to pin the SDK in `global.json` (M00-T1): the SDK decides the compiler.

---

## 2. Records: types defined by their values

### 2.1 What a record gives you

A **record** is a class (or struct) whose identity is its **values**, not its location in memory. One line:

```csharp
public record Position(double Latitude, double Longitude);
```

…makes the compiler generate:

| Generated | What it does |
| --- | --- |
| A constructor and `init`-only properties `Latitude`, `Longitude` | The "positional" parameters become public properties that can't change after creation |
| `Equals`, `GetHashCode`, `==`, `!=` | **Value equality**: two `Position`s with the same coordinates are equal |
| `ToString()` | `Position { Latitude = 45.75, Longitude = 21.22 }`, handy in logs and test failures |
| `Deconstruct` | `var (lat, lon) = position;` |
| A copy constructor and `with` support | `var moved = position with { Latitude = 46.0 };` |

Compare it with a normal class:

```csharp
var a = new Position(45.75, 21.22);
var b = new Position(45.75, 21.22);
Console.WriteLine(a == b);              // record: True   · class: False (two different objects)
Console.WriteLine(ReferenceEquals(a, b)); // False in both cases: they are still two objects
```

`with` creates a **new** object with some properties changed. The original is untouched. This is called **non-destructive mutation**, and it's what makes immutable types practical:

```csharp
var corrected = reading with { Speed = 0 };   // reading itself is unchanged
```

### 2.2 `record class` vs `record struct` vs `readonly record struct`

| | `record` / `record class` | `record struct` | `readonly record struct` |
| --- | --- | --- | --- |
| Kind | Reference type (heap) | Value type (copied) | Value type (copied) |
| Positional properties | `init` (immutable) | **`get; set;` (mutable!)** | `init` (immutable) |
| Equality | Value, generated | Value, generated (and faster than the default struct equality, which uses reflection) | Same |
| Good for | DTOs (data transfer objects), messages, events | Rarely the right choice | Small values: coordinates, money, the `TelemetryReading` from M01-T4 |

The middle column is a common surprise: a plain `record struct`'s positional properties are **mutable**, unlike a `record class`'s. If you want an immutable struct record, write `readonly record struct`. For a value type that's what you nearly always want, because mutating copies of structs is a classic bug source (you change a copy, not the original).

### 2.3 Pitfalls

**Collections inside records compare by reference.** The generated `Equals` compares each property with its *own* `Equals`. For a `List<T>` or an array, that's reference equality:

```csharp
public record Route(string VehicleId, List<string> Stops);

var r1 = new Route("DEV-42", ["A", "B"]);
var r2 = new Route("DEV-42", ["A", "B"]);
Console.WriteLine(r1 == r2);   // False: two different List objects
```

So a record with a collection isn't really a "value" anymore. The fixes: write your own `Equals` (comparing with `SequenceEqual`), or keep collections out of types you compare by value. `ToString()` also prints `Stops = System.Collections.Generic.List`1[System.String]`, not the items.

**`with` is a shallow copy.** `route with { VehicleId = "DEV-7" }` creates a new `Route`, but both share the **same** `Stops` list. Adding a stop to one adds it to the other.

**Records and inheritance.** Records can inherit from records, and equality checks the **runtime type** too (through a hidden `EqualityContract` property): a `Base` and a `Derived` with identical values are not equal. That's correct, but it surprises people. Deep record hierarchies are usually a sign the type should be a class.

**Records are a poor fit for EF Core entities.** An entity (a `Shipment`, a `Vehicle`) has an **identity** that outlives its values. A shipment is the same shipment after its status changes. Value equality says two objects with the same values are "the same", and EF Core's change tracker is built on reference identity. Records with `with` also encourage creating *new* objects, which the change tracker doesn't see as updates. Use **classes** for entities. Records fit **value objects** (an `Address`, a `Money`) mapped as EF Core complex or owned types, and everything at the edges: request/response DTOs, integration events, messages.

**When not to use a record:**
- The object has an identity or a lifecycle (entities, aggregates).
- The object is mutable by design (a builder, a cache entry you update in place).
- It's a service. A `ShipmentService` with value equality makes no sense.

---

## 3. `init` and `required`: object initializers you can trust

### 3.1 `init` accessors

An `init` accessor lets a property be set **only during object creation**: in the constructor or in an object initializer. After that, it's read-only:

```csharp
public class VehicleDto
{
    public string Plate { get; init; } = "";
}

var v = new VehicleDto { Plate = "TM-01-ABC" };   // OK: object initializer
v.Plate = "B-99-XYZ";                              // compile error: init-only
```

That's how records make their positional properties immutable, and it's useful on any DTO or options class.

### 3.2 The gap `required` closes

`init` says "you may only set this at creation". It doesn't say "you **must** set it". Without a constructor, nothing stops `new VehicleDto()`, which leaves `Plate` empty. That's why you see the `= ""` default above: it silences the nullable warning, and an empty plate silently flows into the system.

`required` (C# 11) makes the compiler enforce it:

```csharp
public class VehicleDto
{
    public required string Plate { get; init; }
    public string? Notes { get; init; }
}

var ok  = new VehicleDto { Plate = "TM-01-ABC" };   // fine
var bad = new VehicleDto();                          // compile error CS9035: required member 'Plate' must be set
```

`required` combines well with nullable reference types: a `required string` doesn't need a dummy default, because the compiler knows every creation sets it.

**`System.Text.Json` respects it** (since .NET 7). Deserializing JSON that's missing a `required` property throws a `JsonException`, instead of leaving the property empty.

### 3.3 Pitfalls

- **`required` checks that you assigned a value, not that the value is valid.** `new VehicleDto { Plate = "" }` and `Plate = null!` both compile. Validation (not empty, correct format) still belongs in a constructor, a domain factory method or a validator.
- **`[SetsRequiredMembers]`.** If a type has a constructor that sets the required members, you mark that constructor with `[SetsRequiredMembers]` so callers don't have to repeat them in an initializer. The compiler **trusts** the attribute and doesn't check it. Put it on a constructor that forgets a member and you're back to an unset property, with no warning.
- **Constructors vs `required` + `init`.** A constructor can enforce **invariants** ("end date after start date") in one place. An object initializer can't: each property is set separately and nothing runs at the end. So:
  - **DTOs, options and messages** (data with no rules): `required` + `init` is clean and serializer-friendly.
  - **Domain types with rules:** a constructor or factory method that validates, so an invalid object can't exist.

---

## 4. Primary constructors

### 4.1 What they are

C# 12 lets **any** class or struct declare constructor parameters right after its name, as records always could:

```csharp
// Before
public sealed class DispatchService
{
    private readonly IShipmentRepository _repository;
    private readonly TimeProvider _clock;

    public DispatchService(IShipmentRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public Task<Shipment?> GetAsync(Guid id, CancellationToken ct) => _repository.FindAsync(id, ct);
}

// After
public sealed class DispatchService(IShipmentRepository repository, TimeProvider clock)
{
    public Task<Shipment?> GetAsync(Guid id, CancellationToken ct) => repository.FindAsync(id, ct);
}
```

For DI-heavy services, this deletes most of the boilerplate. That's why it's the feature teams adopt first.

### 4.2 The key difference from records

| | `record Position(double Lat, double Lon)` | `class DispatchService(IShipmentRepository repository)` |
| --- | --- | --- |
| Parameters become | **Public `init` properties** | **Nothing public.** Just parameters in scope in the whole class body |
| Can you write `x.repository` from outside? | `p.Lat`: yes | No |

The same syntax means different things on a record and on a class. That alone causes confusion in reviews.

### 4.3 Captured parameters are *not* readonly fields

If a member uses a primary-constructor parameter (as `GetAsync` uses `repository` above), the compiler stores it in a **hidden private field**. That field is **mutable**, so this compiles without a warning:

```csharp
public sealed class DispatchService(IShipmentRepository repository)
{
    public void Reset() => repository = null!;   // compiles; the "dependency" is now gone
}
```

With the old explicit style, `private readonly` made that impossible. With primary constructors, there's no `readonly` option for the parameter itself (as of C# 14). The usual team answers:

1. **Accept it, with a rule:** "never assign to a primary-constructor parameter". Code review enforces it.
2. **Assign to an explicit readonly field:** `private readonly IShipmentRepository _repository = repository;`. You get `readonly` back, but most of the brevity is gone.

**Double capture.** If you do option 2 *and* also use the parameter directly somewhere, you have two copies of the same value, a field and a hidden capture, which can drift apart if one is reassigned. The compiler warns: **CS9124** ("parameter is captured into the state of the enclosing type and its value is also used to initialize a field").

### 4.4 No constructor body

A primary constructor has no body, so there's nowhere to put validation like `ArgumentNullException.ThrowIfNull(repository)`. You can validate in a field initializer (`private readonly Money _limit = limit > 0 ? limit : throw new ArgumentOutOfRangeException(nameof(limit));`), but that's clumsy for more than one rule.

**When primary constructors fit, and when not:**
- ✅ **DI services, handlers, controllers:** dependencies are just stored and used, and the container never passes `null`.
- ❌ **Domain types with invariants:** use an explicit constructor that validates.
- ❌ **Types with several constructors with different logic:** every other constructor must chain to the primary one with `: this(...)`, which gets awkward.

---

## 5. Collection expressions

### 5.1 One syntax for every collection

C# 12 adds a single literal syntax, `[...]`, that works for arrays, lists, spans, immutable collections and interfaces. The **target type** decides what gets created:

```csharp
int[] ids = [1, 2, 3];
List<string> stops = ["A", "B"];
ReadOnlySpan<byte> separator = [(byte)';'];
ImmutableArray<int> frozen = [1, 2, 3];
IReadOnlyList<string> tags = ["urgent", "fragile"];   // the compiler picks a suitable read-only type
List<int> empty = [];                               // instead of new List<int>()
```

The **spread** element `..` copies all items of another collection in:

```csharp
int[] all = [..ids, 4, 5, ..moreIds];
```

### 5.2 Why it's more than brevity

- **Consistency:** one way to write "a collection of these items", whatever the type. Changing a parameter from `List<T>` to `T[]` no longer forces you to rewrite every call site.
- **The compiler picks an efficient implementation.** For a span target it can use stack memory or static data. It also sizes lists exactly when it knows the count. `[]` for an empty array reuses a cached empty instance, like `Array.Empty<T>()`.

### 5.3 Limits and pitfalls

- **`var x = [1, 2];` doesn't compile.** A collection expression has no "natural type": the compiler needs a target type.
- **No dictionaries yet.** C# 14 has no `[key: value]` syntax for dictionaries. Use a collection initializer: `new Dictionary<string, int> { ["a"] = 1 }`.
- **Interface targets hide the real type.** For `IEnumerable<T>` or `IReadOnlyList<T>`, the compiler chooses the concrete type. Code that casts it back to `List<T>` may break. (It shouldn't cast anyway.)

---

## 6. Pattern matching and switch expressions

### 6.1 The patterns you'll actually use

A **pattern** tests a value's shape: its type, its properties or its range. Patterns appear after `is`, in `switch` statements and in **switch expressions**.

```csharp
// Type pattern + declaration
if (message is PositionReported reported) { … reported.Position … }

// Null checks: is null / is not null (can't be fooled by an overloaded == operator)
if (vehicle is not null) { … }

// Property pattern: test properties without a cast
if (reading is { Speed: 0, DeviceId: "DEV-42" }) { … }

// Relational and logical patterns: <, >, and, or, not
bool plausible = reading.Latitude is >= -90 and <= 90;

// List patterns (C# 11): match on the shape of a sequence
var fields = line.Split(';');
if (fields is [var id, var timestamp, _, _, _]) { … }   // exactly 5 items, the first two named
if (fields is [_, .., var last]) { … }                  // at least 2 items, take the last
```

### 6.2 Switch expressions

A **switch expression** turns a value into a result, with one arm per case:

```csharp
public static string SpeedBand(double kmh) => kmh switch
{
    < 0       => throw new ArgumentOutOfRangeException(nameof(kmh)),
    0         => "stationary",
    < 50      => "urban",
    < 90      => "rural",
    _         => "highway",
};
```

Compared with an `if`/`else` chain, it's an **expression** (it produces a value), every arm must produce the same type, and the compiler checks two things:

- **Exhaustiveness:** if some input isn't matched, you get warning **CS8509**, and at run time an unmatched input throws `SwitchExpressionException`. For enums, the compiler also warns when you don't cover every named value. That's a useful prompt when someone adds a new `ShipmentStatus`.
- **Unreachable arms:** an arm that can never match because an earlier arm already covers it is an error (**CS8510**). Arms are tested **top to bottom**, so order matters: `< 50` before `< 90`.

### 6.3 When not to

- **Switching on type instead of polymorphism.** `shape switch { Circle c => …, Square s => … }` scattered across ten files means adding a shape requires changing ten files. If the behaviour belongs to the type, a virtual method (or an interface) is better. Type switches fit at the **edges**: mapping messages, DTOs or errors you don't own.
- **Deeply nested patterns.** `x is { A: { B: { C: > 5 } } }` is correct and hard to read. C# 10's **extended property patterns**, `x is { A.B.C: > 5 }`, help, but past two or three conditions a named method (`IsOverdue(shipment)`) reads better in review.
- **Side effects in `when` clauses.** `when` guards should be pure checks. A `when` that calls the database is a hidden query.

---

## 7. C# 13 in one page

C# 13 (.NET 9) is a smaller release. The parts worth knowing:

- **`params` collections.** `params` now works with `Span<T>`, `ReadOnlySpan<T>`, `List<T>`, `IEnumerable<T>` and others, not just arrays. `void Log(params ReadOnlySpan<string> parts)` can take arguments **without allocating an array** (the M01-T4 theme). Many .NET 9+ library methods added such overloads, so existing code gets faster after recompiling.
- **The new `System.Threading.Lock` type.** `private readonly Lock _gate = new(); … lock (_gate) { … }` uses a dedicated lock type, which is faster and clearer than locking on a `private readonly object`. The compiler recognises it and calls `Lock.EnterScope()`. Pitfall: if you cast a `Lock` to `object` and lock on that, you get the old `Monitor` behaviour and a compiler warning.
- **`ref struct`s in more places.** `ref` locals and `Span<T>` can now appear in `async` methods and iterators, as long as they don't cross an `await` or `yield`. Generic code can accept `ref struct` type arguments with the `allows ref struct` anti-constraint. This is mostly library-author territory.
- **Partial properties.** A property can be declared in one `partial` part and implemented in another, which source generators use (for example `[GeneratedRegex]` on a property).
- **`^` (from-the-end index) in object initializers,** and the `\e` escape for the ESC character. Rarely needed.

---

## 8. C# 14: the `field` keyword, extension members and the rest

C# 14 ships with .NET 10, so this is what FleetTrack compiles with.

### 8.1 The `field` keyword

Before C# 14, adding **any** logic to a property's accessor meant giving up the auto-property and writing the backing field yourself:

```csharp
// Before: a backing field just to trim the value
private string _name = "";
public string Name
{
    get => _name;
    set => _name = value.Trim();
}

// C# 14: `field` refers to the compiler-generated backing field
public string Name
{
    get;
    set => field = value.Trim();
}
```

You write only the accessor that needs logic. The other stays automatic. Typical uses: trimming or normalising input, validating (`set => field = value >= 0 ? value : throw …`), lazy initialisation (`get => field ??= Load();`) and raising change notifications.

**Pitfall:** inside an accessor, `field` is now a keyword. A class that **already** has a member named `field` changes meaning when it's compiled with C# 14: the accessor now refers to the backing field, not your member. The compiler warns, and you write `this.field` or `@field` to mean your own member. Watch for this when upgrading old code.

### 8.2 Extension members

Extension methods (since C# 3) only let you add **methods**. C# 14 adds **extension blocks**, which can also declare **properties** and **static** members:

```csharp
public static class TelemetryReadingExtensions
{
    // Instance extension members: called as reading.IsStationary
    extension(TelemetryReading reading)
    {
        public bool IsStationary => reading.Speed < 1;
        public double SpeedMetresPerSecond => reading.Speed / 3.6;
    }

    // Static extension members: called as TelemetryReading.Origin
    extension(TelemetryReading)
    {
        public static TelemetryReading Origin(string deviceId) =>
            new(deviceId, DateTimeOffset.UnixEpoch, 0, 0, 0);
    }
}
```

Classic extension methods (`public static bool IsStationary(this TelemetryReading r)`) still work, and the two styles can live side by side.

**Trade-offs:** extension properties make code read naturally (`reading.IsStationary`), but they also make it **harder to tell where a member comes from**. A reader sees `reading.IsStationary` and assumes it's defined on `TelemetryReading`. "Go to definition" lands somewhere else, and the member only exists if the right `using` is in scope. That's fine for small, obvious helpers. It's a problem when extension members contain real business logic, which then lives outside the type that owns the rule.

### 8.3 The smaller C# 14 features

- **Null-conditional assignment:** `vehicle?.LastSeen = now;` assigns only if `vehicle` isn't null, and the right-hand side is evaluated only in that case.
- **`nameof` with unbound generics:** `nameof(List<>)` gives `"List"`.
- **Implicit span conversions:** arrays convert to `Span<T>` / `ReadOnlySpan<T>` automatically in more places, including extension method calls. Span-based APIs feel like first-class citizens.
- **Lambda parameter modifiers without types:** `(text, out result) => int.TryParse(text, out result)`.
- **Partial constructors and events,** and **user-defined compound assignment operators** (`operator +=`, for types that can update in place). Mostly for source generators and numeric libraries.

---

## 9. How a team decides what to adopt

For each feature, a senior engineer asks these questions before writing it into the team's guidelines:

| Question | Example |
| --- | --- |
| **Does it remove noise, or hide meaning?** | Collection expressions remove noise. A five-level nested pattern hides meaning. |
| **Are its semantics surprising?** | A mutable `record struct`; primary-constructor parameters that look readonly but aren't; `with` sharing a list. |
| **Can a new team member read it without a lookup?** | A switch expression on a number: yes. List patterns with slices: often not. |
| **Can the build enforce one style?** | Most of these features have analyzer rules (§10). Some team rules ("never reassign a primary-constructor parameter") are only reviewable. |
| **What does it cost to migrate the existing code?** | A codebase-wide switch to primary constructors touches every service: a large diff, merge conflicts, and history (`git blame`) pointing at a style change. Teams usually say "new code only" or do one mechanical PR. |
| **Does it fit where it's used?** | Records for DTOs, not for entities. Primary constructors for services, not for domain types with invariants. |

Most features aren't "good" or "bad" in general. They're good **in some places**. The best team guideline names both: where to use it and where not to.

---

## 10. Enforcing the decision: `.editorconfig` and the build

FleetTrack already has `EnforceCodeStyleInBuild` and `TreatWarningsAsErrors` on (M00-T3). So any **IDE rule** set to `warning` in `.editorconfig` breaks the build, locally and in CI. The rules for this lesson's features:

| Rule | What it suggests |
| --- | --- |
| `IDE0290` | Use a primary constructor (setting: `csharp_style_prefer_primary_constructors`) |
| `IDE0028`, `IDE0300`–`IDE0305` | Use a collection expression (for initializers, arrays, empty collections, fluent calls…) |
| `IDE0066` | Convert a `switch` statement to a switch expression |
| `IDE0078`, `IDE0083` | Use pattern matching (`is >= 0 and <= 9`, `is not null`) |
| `IDE0090` | Simplify `new` expressions (`List<int> x = new();`) |
| `IDE0032` | Use an auto-property |

Example of turning a decision into a rule:

```ini
[*.cs]
# Prefer primary constructors (and fail the build if not used where possible)
csharp_style_prefer_primary_constructors = true:warning
dotnet_diagnostic.IDE0290.severity = warning
```

And the opposite, if the team decides *against* something: set the preference to `false` and the severity to `warning`, and the build flags uses of the feature.

**Pitfall:** turning a rule to `warning` on an existing codebase breaks the build everywhere at once. The M00 lesson applies: visibility first (`suggestion`), fix the code (often with `dotnet format --diagnostics IDE0290`, which applies the fix automatically), then raise it to `warning`.

---

## 11. A worked before/after

A notification DTO and a sender from a typical .NET 6-era codebase:

```csharp
// BEFORE
public class NotificationRequest
{
    public string Recipient { get; set; } = "";
    public string Channel { get; set; } = "email";
    public List<string> Tags { get; set; } = new List<string>();
}

public class NotificationSender
{
    private readonly IEmailClient _email;
    private readonly ISmsClient _sms;

    public NotificationSender(IEmailClient email, ISmsClient sms)
    {
        _email = email;
        _sms = sms;
    }

    public Task SendAsync(NotificationRequest request, CancellationToken ct)
    {
        if (request.Channel == "email")
            return _email.SendAsync(request.Recipient, ct);
        else if (request.Channel == "sms")
            return _sms.SendAsync(request.Recipient, ct);
        else
            throw new NotSupportedException(request.Channel);
    }
}
```

```csharp
// AFTER
public sealed record NotificationRequest
{
    public required string Recipient { get; init; }
    public NotificationChannel Channel { get; init; } = NotificationChannel.Email;
    public IReadOnlyList<string> Tags { get; init; } = [];
}

public enum NotificationChannel { Email, Sms }

public sealed class NotificationSender(IEmailClient email, ISmsClient sms)
{
    public Task SendAsync(NotificationRequest request, CancellationToken ct) => request.Channel switch
    {
        NotificationChannel.Email => email.SendAsync(request.Recipient, ct),
        NotificationChannel.Sms   => sms.SendAsync(request.Recipient, ct),
        _ => throw new NotSupportedException(request.Channel.ToString()),
    };
}
```

What changed, and **why** each change is (or isn't) an improvement:

1. **`record` + `init`:** the request can't be changed after creation, and it gets value equality and a readable `ToString()` for logs. The `Tags` list is an `IReadOnlyList<string>`, so callers can't add to it, but remember §2.3: equality still compares `Tags` by reference.
2. **`required Recipient`:** forgetting the recipient is now a compile error instead of an empty string sent to the email client. It still doesn't check that the address is valid.
3. **`string` channel → enum:** this isn't a new C# feature, but the switch expression made it worth doing. The compiler now warns if a new channel is added and not handled. A switch expression over strings can't give you that.
4. **Primary constructor:** 8 lines of boilerplate gone. The trade-off: `email` and `sms` are now mutable captured parameters, not `readonly` fields.
5. **Switch expression:** one expression, every case visible at once. The `_` arm is still needed, because an enum variable can hold any integer value (`(NotificationChannel)42`).
6. **`[]` for the empty list:** shorter, and consistent with every other collection in the codebase.

**How to write it up:** for each class, show the before and after, then give the reasons per change as above. The "why" and the trade-off are what interviewers ask about, not the syntax.

---

## 12. Glossary

| Term | Meaning |
| --- | --- |
| **Record** | A class or struct with compiler-generated value equality, `ToString`, deconstruction and `with` support |
| **Value equality / reference equality** | Equal if all values are equal / equal only if it's the same object in memory |
| **Positional record** | A record declared with parameters (`record P(int X)`) that become properties |
| **Non-destructive mutation (`with`)** | Creating a modified copy instead of changing the original; the copy is shallow |
| **`init` accessor** | A property setter usable only during object creation |
| **`required` member** | A property or field the caller must set in the object initializer (compile-time check) |
| **`[SetsRequiredMembers]`** | Marks a constructor as setting all required members; trusted, not verified |
| **Invariant** | A rule that must always hold for an object ("end after start"); enforced in constructors or methods |
| **Primary constructor** | Constructor parameters declared after the type name; on classes they're captured parameters, not properties |
| **Captured parameter** | A primary-constructor parameter used in a member; stored in a hidden, mutable field |
| **Collection expression** | The `[a, b, ..c]` literal; the target type decides which collection is created |
| **Spread element (`..`)** | Copies all items of another collection into a collection expression |
| **Pattern** | A test of a value's type, properties, range or sequence shape (`is`, `switch`) |
| **Switch expression** | `value switch { pattern => result, … }`, an expression with exhaustiveness checks |
| **Exhaustiveness** | Whether every possible input is matched by some arm (warning CS8509 if not) |
| **List pattern** | A pattern on a sequence's elements, e.g. `[var first, .., var last]` |
| **`field` keyword** | In C# 14, the compiler-generated backing field of the property being defined |
| **Extension block / extension members** | C# 14 `extension(T x) { … }` blocks declaring extension methods, properties and static members |
| **DTO (data transfer object)** | A type that carries data across a boundary (HTTP, messages), with no behaviour |
| **Value object** | A domain type defined by its values (Money, Address), with no identity |

---

## 13. Summary

1. A language feature is a **team** decision: it must be readable by everyone, unsurprising, used consistently and enforceable by the build.
2. **Records** give value equality, `with` and `ToString`. They fit DTOs, messages and value objects, not entities. `record struct` is mutable, `readonly record struct` isn't; collections inside records compare by reference.
3. **`required` + `init`** guarantee a value is *assigned*, not that it's *valid*. Types with invariants still need a validating constructor.
4. **Primary constructors** delete DI boilerplate, but their parameters are mutable captures, not `readonly` fields, and there's no constructor body for validation.
5. **Collection expressions** are one syntax for every collection type, often more efficient. There's no natural type (no `var`) and no dictionary syntax.
6. **Pattern matching and switch expressions** make case analysis compact and checked for exhaustiveness. Prefer polymorphism when the behaviour belongs to the type, and named methods over deep nesting.
7. **C# 13** adds `params` spans and the `Lock` type. **C# 14** adds `field`, extension properties and static extension members, null-conditional assignment and implicit span conversions.
8. Turn the team's choices into `.editorconfig` rules at `warning`, and roll them out gradually on existing code.

### Check yourself

1. Two `record Route(string Id, List<string> Stops)` instances have the same id and stops. Are they equal? Why, and how would you fix it? (§2.3)
2. Why is `record struct Point(int X, int Y)` a worse default than `readonly record struct Point(int X, int Y)`? (§2.2)
3. Give two reasons why an EF Core entity like `Shipment` shouldn't be a record. (§2.3)
4. `required string Plate { get; init; }`. Which of these compile: `new V()`, `new V { Plate = "" }`, `new V { Plate = null! }`? What does that tell you about `required`? (§3.3)
5. What's the difference between the parameters of `record P(int X)` and `class S(int x)`? What can go wrong with the second one that couldn't with an explicit `private readonly` field? (§4.2–§4.3)
6. What does CS9124 warn you about? (§4.3)
7. Why doesn't `var x = [1, 2, 3];` compile? (§5.3)
8. A switch expression over an enum has an arm for every named value. Why might you still need a `_` arm? (§6.2, §11)
9. When is a type switch the wrong tool, and what would you use instead? (§6.3)
10. You upgrade an old project to C# 14 and a property accessor changes behaviour. What's the likely cause? (§8.1)

**Before you build: choose.** For each of the 3 classes you'll rewrite, write down which features you expect to apply and one risk you expect from each. Then compare with what actually happened.

### Interview questions this prepares you for

- "When would you use a record, and when a class? Would you make an EF Core entity a record?"
- "What does `required` guarantee, and what doesn't it?"
- "Primary constructors: would you allow them in your team's codebase? Where, and with what rules?"
- "How do you introduce a new language feature or style rule into a large existing codebase?"
- "Pattern matching or polymorphism: how do you choose?"
- "What's new in recent C# versions that you've actually used, and why?"

### For FleetTrack

- **Candidates for the 3 rewrites** (any mix of FleetTrack, the labs and your past work):
  - `DatabaseConfiguration` (`src/FleetTrack.Infrastructure/Configurations`): an options class with `init` and an `= string.Empty` default. What would `required` change, and does it work with options binding plus `ValidateOnStart()`?
  - `TenantReportService` (`labs/M01-T5-di-lifetimes`): explicit constructor + `readonly` field, a classic primary-constructor candidate. Decide what you do about mutability.
  - The parsers in `labs/M01-T4-parsing`: the repeated "find `;`, slice, parse" steps, or a validation step for bad lines (the open `SpanParser` issue), are a natural place for patterns.
  - A class from a past project with `if`/`else` chains, mutable DTOs or `new List<T>()` everywhere is often the best example, because the "before" is real.
- **Write-up:** `docs/journal/M01-T6-<slug>.md`, using the journal template. "What I did" holds the before/after per class, "Why" holds the reasons per change, and "The fix / the rule I'll follow" holds the feature you'd avoid in a team codebase, and why.
- **Later:** whatever you decide here becomes `.editorconfig` rules, and the team guideline you write in M01-T7.
