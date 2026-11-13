# C# engine interface (sketch)

This is the C# shape of the question catalog in [`docs/operations.md`](../../../docs/operations.md): a design sketch
written after library 0.1.0, not the library's API. Its twelve design decisions, each with what library 0.3 realizes,
are in [`.dev/decisions.json`](../.dev/decisions.json) (`engine-1-values-are-data` … `engine-12-bcl-interop`); the
milestones M2–M10 are in [`.dev/roadmap.json`](../.dev/roadmap.json); the planned evolution of the 0.1.0 types, the
analyzer `UTV001` and `Format` (I3) are in [`.dev/backlog.json`](../.dev/backlog.json) and, where they wait on the owner,
in the open records of `.dev/decisions.json`. The API library 0.3 does have is in [`../README.md`](../README.md).

## Using it

```csharp
var registry = await RuleRegistry.LoadAsync(rulesDirectory, ct);   // TZif, calendars, leap table: pinned by SHA-256
ITemporalEngine engine = new TemporalEngine(registry);

var context = new EvaluationContext { Rules = registry.Manifest };
var meeting = (CivilValue)engine.Parse(json).Candidates[0];

var resolved = engine.Resolve(meeting, ResolutionPolicy.Strict, context);
switch (resolved.Status)
{
    case UtvStatus.Unique:
        Schedule(resolved.Candidates[0]);
        break;
    case UtvStatus.None:          // the local time doesn't exist (DST gap)
    case UtvStatus.Multiple:      // it exists twice (DST overlap)
        AskTheUser(resolved);     // resolved.Diagnostics says which, resolved.Candidates lists the overlap
        break;
    case UtvStatus.Indeterminate: // rules can't decide (e.g. historical zone data unknown)
    case UtvStatus.Unsupported:   // this engine lacks the rules
        Escalate(resolved);
        break;
}

// "Is the meeting over?" needs an explicit 'now'. Take it from a named clock and pass it in.
var now = clock.Observe();        // an ObservationValue with clock identity and quality
var over = engine.Holds(meeting, RelationSet.Before | RelationSet.Meets, now.Reading,
                        context with { ReferenceObservation = now });
if (over.TryGetUnique(out var truth) && truth == Truth.Definitely) Archive(meeting);
```

## Interface

```csharp
// UTV engine interface sketch for C#. Compiles against .NET 8; bodies are intentionally absent.
#nullable enable
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Utv;

// ───────────────────────────── Results ─────────────────────────────

public enum UtvStatus { Invalid, Unsupported, Indeterminate, None, Unique, Multiple, Range, Symbolic, ResourceLimit, Error, Valid }

/// <summary>Answer to a yes/no temporal question. "Unknown" is expressed by the result status, not here.</summary>
public enum Truth { Definitely, Possibly, Impossible }

public enum LossClass { Lossless, InformationLosing, AssumptionDependent, Unrepresentable }

public enum DiagnosticSeverity { Info, Warning, Error }

public sealed record Diagnostic(string Code, DiagnosticSeverity Severity, string Path, string Message);

public sealed record Assumption(string Code, string Description, bool CallerApproved);

public sealed record LossReport(LossClass Classification, ImmutableArray<string> Fields)
{
    public static LossReport None { get; } = new(LossClass.Lossless, ImmutableArray<string>.Empty);
}

/// <summary>How many candidates exist, and whether <see cref="UtvResult{T}.Candidates"/> lists all of them.</summary>
public sealed record Cardinality(CardinalityKind Kind, System.Numerics.BigInteger? Count, bool Exhaustive);
public enum CardinalityKind { Finite, Infinite, Unknown }

/// <summary>Exact, content-addressed rule data an answer depended on.</summary>
public sealed record RuleManifest(ImmutableArray<RuleRef> Dependencies)
{
    public static RuleManifest Empty { get; } = new(ImmutableArray<RuleRef>.Empty);
}

/// <summary>Non-generic view, shared by all operations (and by 0.1.0's validation result).</summary>
public abstract class UtvResult
{
    public required string Operation { get; init; }
    public required UtvStatus Status { get; init; }
    public RuleManifest Manifest { get; init; } = RuleManifest.Empty;
    public ImmutableArray<Assumption> Assumptions { get; init; } = ImmutableArray<Assumption>.Empty;
    public LossReport Loss { get; init; } = LossReport.None;
    public ImmutableArray<Diagnostic> Diagnostics { get; init; } = ImmutableArray<Diagnostic>.Empty;
    /// <summary>Digest of the explicit context the answer was computed under (RESULT-CONTRACT contextDigest).</summary>
    public string? ContextDigest { get; init; }
    public abstract string ToJson();
}

public sealed class UtvResult<T> : UtvResult
{
    public ImmutableArray<T> Candidates { get; init; } = ImmutableArray<T>.Empty;
    public Cardinality? Cardinality { get; init; }

    /// <summary>True only when Status is Unique. There is deliberately no <c>.Value</c> property that throws.</summary>
    public bool TryGetUnique([MaybeNullWhen(false)] out T value)
    {
        if (Status == UtvStatus.Unique && Candidates.Length == 1) { value = Candidates[0]; return true; }
        value = default;
        return false;
    }

    public override string ToJson() => throw new NotImplementedException();
}

// ───────────────────────────── Values (abbreviated) ─────────────────────────────
// One sealed record per kind (26). Immutable; no IComparable, no operators, no implicit conversions.
// Record equality is *representation* equality over canonical form; semantic equality goes through the engine.

public enum UtvKind
{
    Instant, Civil, Amount, Interval, Recurrence, Uncertain, Set, Clock, Observation, LogicalClock, Relative,
    Relation, Assertion, EventHistory, LocationIntent, Constraint, CalendarRule, TemporalReference,
    OrdinalPosition, EventRelative, Selection, Series, Sample, TemporalGraph, Sequence, Pattern,
}

public sealed record RuleRef(string System, string Id, string Version, string? Sha256 = null);

public abstract record UtvValue
{
    public const string Schema = "utv/1.0";
    public abstract UtvKind Kind { get; }
}
public sealed record InstantValue(RuleRef System, System.Numerics.BigInteger Ticks) : UtvValue { public override UtvKind Kind => UtvKind.Instant; }
public sealed record CivilValue(string Form /* …fields, calendar, zone, zoneIntent, resolutionPolicy… */) : UtvValue { public override UtvKind Kind => UtvKind.Civil; }
public sealed record AmountValue(string Basis /* … */) : UtvValue { public override UtvKind Kind => UtvKind.Amount; }
public sealed record IntervalValue(string Domain /* … */) : UtvValue { public override UtvKind Kind => UtvKind.Interval; }
public sealed record RecurrenceValue(string Algorithm /* … */) : UtvValue { public override UtvKind Kind => UtvKind.Recurrence; }
public sealed record UncertainValue(string Model /* … */) : UtvValue { public override UtvKind Kind => UtvKind.Uncertain; }
public sealed record ObservationValue(string Role, UtvValue Reading /* … source, latency, quality */) : UtvValue { public override UtvKind Kind => UtvKind.Observation; }
public sealed record AssertionValue(string AssertionId /* … */) : UtvValue { public override UtvKind Kind => UtvKind.Assertion; }
public sealed record EventHistoryValue(string SubjectId /* … */) : UtvValue { public override UtvKind Kind => UtvKind.EventHistory; }
public sealed record LocationIntentValue(string Authority /* … */) : UtvValue { public override UtvKind Kind => UtvKind.LocationIntent; }
public sealed record SelectionValue(string Take /* … */) : UtvValue { public override UtvKind Kind => UtvKind.Selection; }
public sealed record SeriesValue(string Id /* … */) : UtvValue { public override UtvKind Kind => UtvKind.Series; }
public sealed record SampleValue(string Subject /* … */) : UtvValue { public override UtvKind Kind => UtvKind.Sample; }
public sealed record TemporalGraphValue(string Domain /* … */) : UtvValue { public override UtvKind Kind => UtvKind.TemporalGraph; }
public sealed record OrdinalPositionValue(RuleRef System, string Label) : UtvValue { public override UtvKind Kind => UtvKind.OrdinalPosition; }
// …and the remaining kinds.

// ───────────────────────────── Context, policies, budgets ─────────────────────────────

/// <summary>Deterministic limits. Counted in steps, never wall time, so the same input gives the same answer.</summary>
public sealed record Budget(int MaxCandidates = 10_000, long MaxSteps = 10_000_000, int MaxDepth = 64)
{
    public static Budget Default { get; } = new();
}

/// <summary>Everything an answer may depend on. There is no ambient "now", zone, locale or rule data.</summary>
public sealed record EvaluationContext
{
    /// <summary>Pinned rule data the caller allows this evaluation to use.</summary>
    public required RuleManifest Rules { get; init; }
    /// <summary>An explicit observation of "now" from a named clock, if the question needs one.</summary>
    public ObservationValue? ReferenceObservation { get; init; }
    public LocationIntentValue? Location { get; init; }
    public Budget Budget { get; init; } = Budget.Default;
    /// <summary>Assumptions the caller approves. Unapproved generated assumptions never yield a definitive answer.</summary>
    public ImmutableArray<Assumption> ApprovedAssumptions { get; init; } = ImmutableArray<Assumption>.Empty;
}

public enum GapPolicy { Reject, PreserveUnresolved, ShiftForwardByGap, ShiftBackwardByGap, NextValidBoundary, PreviousValidBoundary }
public enum OverlapPolicy { Reject, All, Earliest, Latest, OffsetMatch, OccurrenceIndex }

public sealed record ResolutionPolicy(GapPolicy Gap, OverlapPolicy Overlap, int? OccurrenceIndex = null)
{
    /// <summary>Recommended for anything that needs one exact instant: ambiguity is reported, not chosen.</summary>
    public static ResolutionPolicy Strict { get; } = new(GapPolicy.Reject, OverlapPolicy.Reject);
    /// <summary>Report every candidate; never transform the civil intent.</summary>
    public static ResolutionPolicy AllCandidates { get; } = new(GapPolicy.PreserveUnresolved, OverlapPolicy.All);
}

public enum MonthEndPolicy { Reject, Constrain, Carry, PreserveEnd }
public enum HolidayRoll { None, Following, Preceding, ModifiedFollowing, Reject }

public sealed record ArithmeticPolicy(MonthEndPolicy MonthEnd, HolidayRoll HolidayRoll, ResolutionPolicy Resolution, RuleRef? BusinessCalendar = null)
{
    public static ArithmeticPolicy Strict { get; } = new(MonthEndPolicy.Reject, HolidayRoll.None, ResolutionPolicy.Strict);
}

public enum AmountBasis { Elapsed, Calendar, Business, ClockTicks, ProperTime }
public enum EquivalenceMode { Representation, Semantic, Timeline, ScheduleIntent, EventIdentity }
public enum CalendarUnit { Year, Quarter, Month, Week, Day, Hour, Minute, Second }

/// <summary>Allen's 13 interval relations plus point relations, as a set of possibilities.</summary>
[Flags]
public enum RelationSet
{
    None = 0,
    Before = 1 << 0, Meets = 1 << 1, Overlaps = 1 << 2, Starts = 1 << 3, During = 1 << 4, Finishes = 1 << 5,
    Equal = 1 << 6,
    FinishedBy = 1 << 7, Contains = 1 << 8, StartedBy = 1 << 9, OverlappedBy = 1 << 10, MetBy = 1 << 11, After = 1 << 12,
}

// ───────────────────────────── Supporting result shapes ─────────────────────────────

public sealed record OccurrenceId(string Value);
public sealed record Occurrence(OccurrenceId Id, UtvValue Value, bool Overridden, bool Cancelled);
public sealed record ContinuationToken(string Value);
public sealed record PageRequest(int Size, ContinuationToken? After = null);
public sealed record Page<T>(ImmutableArray<T> Items, ContinuationToken? Next);
public sealed record CalendarFields(ImmutableDictionary<string, string> Values);
public sealed record FieldQuery(ImmutableArray<string> Fields, RuleRef? WeekModel = null);
public sealed record BitemporalQuery(UtvValue? ValidAt, UtvValue KnownAt);
public sealed record CacheKey(string InputDigest, string Operation, string PolicyDigest, RuleManifest Manifest, string ContextDigest);
public sealed record StalenessReport(bool Stale, ImmutableArray<RuleRef> ChangedDependencies);
public enum GraphVerdict { Satisfiable, Unsatisfiable }
public sealed record GraphQuery(string From, string To);
public enum WorldAssumption { Open, Closed }
public sealed record MatchReport(ImmutableArray<(UtvValue Obligation, ObservationValue? Match)> Pairs, ImmutableArray<UtvValue> Missed);
public sealed record UncertaintyMethod(string Id);
public sealed record SeriesQuery(string Axis, IntervalValue Window, bool BySupportInterval);
public sealed record InterpolationModel(string Id);
public sealed record AggregationSpec(string Function, AmountValue BinSize, RuleRef? Calendar, RuleRef? Zone);
public sealed record StandardFormat(string Standard, string Edition);
public sealed record MappedText(string Text, string Fidelity, ImmutableArray<string> UnmappedFields);
public sealed record DisplayFormat(RuleRef Locale, RuleRef? Calendar, RuleRef? Zone, string Pattern);
public sealed record CanonicalJson(ImmutableArray<byte> Utf8, string Sha256);

public abstract record ConversionTarget
{
    public sealed record ToInstant(RuleRef Timescale, ResolutionPolicy Resolution) : ConversionTarget;
    public sealed record ToCivil(RuleRef Calendar, UtvValue Zone) : ConversionTarget;
    public sealed record ToCalendar(RuleRef Calendar) : ConversionTarget;
    public sealed record ToTimescale(RuleRef Timescale) : ConversionTarget;
}

public sealed record EngineCapabilities(
    ImmutableArray<string> SpecVersions,
    ImmutableArray<string> Profiles,
    ImmutableDictionary<UtvKind, ImmutableArray<string>> Operations,
    ImmutableArray<RuleRef> InstalledRules);

// ───────────────────────────── Rule registry ─────────────────────────────

/// <summary>A versioned, content-addressed rule set (tzdb zone, calendar, leap table, business calendar, …).</summary>
public interface IRuleSet { RuleRef Reference { get; } }

/// <summary>Immutable rule data. Loaded before evaluation (that may be async); never fetched during evaluation.</summary>
public interface IRuleRegistry
{
    bool TryGet<TRules>(RuleRef reference, [MaybeNullWhen(false)] out TRules rules) where TRules : class, IRuleSet;
    IEnumerable<RuleRef> Installed { get; }
    /// <summary>Manifest pinning everything installed; callers may narrow it per evaluation.</summary>
    RuleManifest Manifest { get; }
}

// ───────────────────────────── The engine, by concern ─────────────────────────────
// Every method is pure with respect to (arguments, context, registry). Semantic outcomes are statuses, never
// exceptions. Exceptions are reserved for programmer errors (ArgumentNullException) and cancellation
// (OperationCanceledException). Budget exhaustion is UtvStatus.ResourceLimit.

/// <summary>A. Is this value meaningful?</summary>
public interface IValidation
{
    UtvResult<UtvValue> Parse(string json);
    UtvResult<UtvValue> Parse(ReadOnlySpan<byte> utf8Json);
    UtvResult<UtvValue> Validate(UtvValue value, EvaluationContext context, CancellationToken ct = default);
}

/// <summary>B. When is it?</summary>
public interface IResolution
{
    UtvResult<InstantValue> Resolve(CivilValue value, ResolutionPolicy policy, EvaluationContext context, CancellationToken ct = default);
    UtvResult<UtvValue> Resolve(UtvValue value, ResolutionPolicy policy, EvaluationContext context, CancellationToken ct = default);
    UtvResult<CalendarFields> Fields(UtvValue value, FieldQuery query, EvaluationContext context, CancellationToken ct = default);
    UtvResult<IntervalValue> DayBounds(CivilValue date, EvaluationContext context, CancellationToken ct = default);
}

/// <summary>C. Ordering, containment, identity.</summary>
public interface IComparison
{
    UtvResult<RelationSet> Compare(UtvValue a, UtvValue b, EvaluationContext context, CancellationToken ct = default);
    UtvResult<Truth> Holds(UtvValue a, RelationSet relation, UtvValue b, EvaluationContext context, CancellationToken ct = default);
    UtvResult<Truth> Contains(UtvValue container, UtvValue candidate, EvaluationContext context, CancellationToken ct = default);
    UtvResult<Truth> Equivalent(UtvValue a, UtvValue b, EquivalenceMode mode, EvaluationContext context, CancellationToken ct = default);
}

/// <summary>D–E. How long, and calendar arithmetic.</summary>
public interface IArithmetic
{
    UtvResult<UtvValue> Add(UtvValue value, AmountValue amount, ArithmeticPolicy policy, EvaluationContext context, CancellationToken ct = default);
    UtvResult<AmountValue> Difference(UtvValue from, UtvValue to, AmountBasis basis, ArithmeticPolicy policy, EvaluationContext context, CancellationToken ct = default);
    UtvResult<UtvValue> Truncate(UtvValue value, CalendarUnit unit, ResolutionPolicy policy, EvaluationContext context, CancellationToken ct = default);
}

/// <summary>B2/B3. Between calendars, zones and timescales; every result carries a loss report.</summary>
public interface IConversion
{
    UtvResult<UtvValue> Convert(UtvValue value, ConversionTarget target, EvaluationContext context, CancellationToken ct = default);
}

/// <summary>F. Repetition, selection and sets.</summary>
public interface IGeneration
{
    UtvResult<Page<Occurrence>> Expand(UtvValue generator, IntervalValue window, PageRequest page, EvaluationContext context, CancellationToken ct = default);
    UtvResult<OccurrenceId> OccurrenceOf(RecurrenceValue recurrence, UtvValue candidate, EvaluationContext context, CancellationToken ct = default);
    UtvResult<UtvValue> Select(SelectionValue selection, EvaluationContext context, CancellationToken ct = default);
    UtvResult<UtvValue> Union(ImmutableArray<UtvValue> operands, EvaluationContext context, CancellationToken ct = default);
    UtvResult<UtvValue> Intersect(ImmutableArray<UtvValue> operands, EvaluationContext context, CancellationToken ct = default);
    UtvResult<UtvValue> Subtract(UtvValue from, UtvValue remove, EvaluationContext context, CancellationToken ct = default);
    UtvResult<UtvValue> Complement(UtvValue value, UtvValue universe, EvaluationContext context, CancellationToken ct = default);
}

/// <summary>G. What did we know, and when?</summary>
public interface IHistory
{
    UtvResult<AssertionValue> AsOf(EventHistoryValue history, BitemporalQuery query, EvaluationContext context, CancellationToken ct = default);
    UtvResult<EventHistoryValue> Append(EventHistoryValue history, AssertionValue revision, EvaluationContext context);
    UtvResult<StalenessReport> IsStale(CacheKey key, EvaluationContext current);
}

/// <summary>C7, F6, H, J4. Reasoning over graphs, obligations, uncertainty and ordinal systems.</summary>
public interface IReasoning
{
    UtvResult<GraphVerdict> CheckConsistency(TemporalGraphValue graph, EvaluationContext context, CancellationToken ct = default);
    UtvResult<RelationSet> QueryGraph(TemporalGraphValue graph, GraphQuery query, EvaluationContext context, CancellationToken ct = default);
    UtvResult<MatchReport> MatchEvents(UtvValue obligation, ImmutableArray<ObservationValue> observations, WorldAssumption world, EvaluationContext context, CancellationToken ct = default);
    UtvResult<UncertainValue> PropagateUncertainty(UtvValue expression, UncertaintyMethod method, EvaluationContext context, CancellationToken ct = default);
    UtvResult<UtvValue> ExpandOrdinal(OrdinalPositionValue position, EvaluationContext context, CancellationToken ct = default);
}

/// <summary>J. Series and samples.</summary>
public interface ISeriesOperations
{
    UtvResult<Page<SampleValue>> Sample(SeriesValue series, SeriesQuery query, PageRequest page, EvaluationContext context, CancellationToken ct = default);
    UtvResult<SampleValue> Interpolate(SeriesValue series, UtvValue at, InterpolationModel model, EvaluationContext context, CancellationToken ct = default);
    UtvResult<SeriesValue> Aggregate(SeriesValue series, AggregationSpec spec, EvaluationContext context, CancellationToken ct = default);
    UtvResult<SeriesValue> Resample(SeriesValue series, AggregationSpec spec, InterpolationModel model, EvaluationContext context, CancellationToken ct = default);
}

/// <summary>I. Getting values in and out.</summary>
public interface IInterchange
{
    UtvResult<UtvValue> Import(string text, StandardFormat format, EvaluationContext context);
    UtvResult<MappedText> Export(UtvValue value, StandardFormat format, EvaluationContext context);
    UtvResult<string> Format(UtvValue value, DisplayFormat display, EvaluationContext context, CancellationToken ct = default);
    UtvResult<CanonicalJson> Canonicalize(UtvValue value);
}

/// <summary>The facade most callers use. Immutable and thread-safe once constructed.</summary>
public interface ITemporalEngine :
    IValidation, IResolution, IComparison, IArithmetic, IConversion,
    IGeneration, IHistory, IReasoning, ISeriesOperations, IInterchange
{
    EngineCapabilities Capabilities { get; }
}

/// <summary>I5. Explicit adapters to BCL types. Each one states its intent and reports loss.</summary>
public static class Bcl
{
    /// <summary>DateTimeOffset as an exact instant: the offset is display metadata and is reported as lost.</summary>
    public static UtvResult<InstantValue> InstantFrom(DateTimeOffset value) => throw new NotImplementedException();
    /// <summary>DateTimeOffset as a civil date-time with a fixed offset (zoneIntent fixed-offset). Lossless.</summary>
    public static UtvResult<CivilValue> CivilFrom(DateTimeOffset value) => throw new NotImplementedException();
    /// <summary>DateOnly is proleptic Gregorian, so this is lossless.</summary>
    public static UtvResult<CivilValue> CivilFrom(DateOnly value) => throw new NotImplementedException();
    /// <summary>Unrepresentable for leap-second labels, years outside 1–9999 and precision finer than 100 ns.</summary>
    public static UtvResult<DateTimeOffset> ToDateTimeOffset(InstantValue value, TimeSpan offset, ITemporalEngine engine, EvaluationContext context) => throw new NotImplementedException();
}
```
