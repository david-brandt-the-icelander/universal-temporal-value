using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Utv.Internal.Everyday;

namespace Utv;

/// <summary>
/// The answer to a question time can refuse to answer: exactly one value (<see cref="Unique"/>), several
/// (<see cref="Multiple"/>), provably none (<see cref="None"/>), not answerable by this library
/// (<see cref="Unsupported"/>), or a self-contradicting question (<see cref="Invalid"/>). Every case carries
/// <see cref="Diagnostics"/>, possibly empty.
/// </summary>
/// <remarks>
/// <para>Only partial operations return an outcome; total ones return the value itself. Handle one with a
/// <c>switch</c> on the nested case types:</para>
/// <code>
/// switch (jan31.AddMonths(1))
/// {
///     case Outcome&lt;CivilDate&gt;.Unique(var date): …; break;
///     case Outcome&lt;CivilDate&gt;.None(var why): …; break;   // 2026-02 has no day 31 (month-end)
///     default: …; break;
/// }
/// </code>
/// <para>To keep case names short, alias the closed type (<c>using DateOutcome = Utv.Outcome&lt;Utv.CivilDate&gt;;</c>,
/// then <c>DateOutcome.Unique(var date)</c>), or, in a file that handles one result type, import its cases
/// (<c>using static Utv.Outcome&lt;Utv.CivilDate&gt;;</c>, then <c>Unique(var date)</c>).</para>
/// <para>C# does not know that these five cases are the only ones, so a <c>switch</c> expression needs a final
/// arm such as <c>_ =&gt;</c>. The helpers <see cref="TryGetUnique"/>, <see cref="UniqueOr"/> and
/// <see cref="Single"/> never pick an answer silently.</para>
/// </remarks>
/// <typeparam name="T">The type of an answer.</typeparam>
public abstract record Outcome<T>
{
    private protected Outcome(Diagnostics? diagnostics)
    {
        Diagnostics = diagnostics ?? Diagnostics.Empty;
    }

    /// <summary>Why, or what to know: the reason for a non-answer, or notes on an answer. Possibly empty.</summary>
    public Diagnostics Diagnostics { get; }

    /// <summary>The status: <see cref="UtvStatus.Unique"/>, <see cref="UtvStatus.Multiple"/>, <see cref="UtvStatus.None"/>,
    /// <see cref="UtvStatus.Unsupported"/> or <see cref="UtvStatus.Invalid"/>. <c>ToWireString()</c> gives <c>unique</c>, <c>none</c>, ….</summary>
    public abstract UtvStatus Status { get; }

    /// <summary>The JSON-layer operation that produced this outcome, used by <see cref="ToResult"/>.</summary>
    internal string Operation { get; init; } = "outcome";

    /// <summary>True, with the value, only for <see cref="Unique"/>.</summary>
    public bool TryGetUnique([MaybeNullWhen(false)] out T value)
    {
        if (this is Unique u)
        {
            value = u.Value;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>The value for <see cref="Unique"/>; <paramref name="fallback"/> for every other case.</summary>
    public T UniqueOr(T fallback) => this is Unique u ? u.Value : fallback;

    /// <summary>The value for <see cref="Unique"/>.</summary>
    /// <exception cref="OutcomeException">Any other case; it carries the status and diagnostics.</exception>
    public T Single() => this is Unique u ? u.Value : throw new OutcomeException(Status, Diagnostics);

    /// <summary>
    /// The <c>utv-result/1.0</c> envelope of the JSON layer: the bridge back to <see cref="UtvResult"/>.
    /// Answers are written as UTV JSON (or, for <see cref="Ordering"/>, <see cref="Truth"/> and integers, as the
    /// plain JSON the JSON layer uses).
    /// </summary>
    /// <exception cref="NotSupportedException">The answers are .NET values (from interop) that have no UTV JSON.</exception>
    public UtvResult ToResult() => this switch
    {
        Unique u => new UtvResult(Operation, UtvStatus.Unique, Diagnostics, new[] { Candidate(u.Value) }),
        Multiple m => new UtvResult(Operation, UtvStatus.Multiple, Diagnostics, m.Values.Select(Candidate).ToArray()),
        _ => new UtvResult(Operation, Status, Diagnostics),
    };

    /// <summary>The value's text for <see cref="Unique"/>; otherwise the status and the codes, e.g. <c>none (month-end)</c>.</summary>
    public sealed override string ToString()
    {
        if (this is Unique u)
        {
            return Text(u.Value);
        }
        string status = Status.ToWireString();
        return Diagnostics.Count == 0 ? status : status + " (" + string.Join(", ", Diagnostics.Codes) + ")";
    }

    /// <summary>Equal when the case, the answers and the diagnostics are equal.</summary>
    public virtual bool Equals(Outcome<T>? other) =>
        other is not null && EqualityContract == other.EqualityContract && Diagnostics.Equals(other.Diagnostics);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(EqualityContract, Diagnostics);

    internal static string Text(T? value) => value switch
    {
        null => "null",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    private static JsonElement Candidate(T value) => value switch
    {
        ITemporalValue v => v.ToJson(),
        Ordering o => Values.ToElement(o.ToWireString()),
        Truth t => Values.ToElement(t.ToWireString()),
        long n => Values.ToElement(n),
        int n => Values.ToElement(n),
        string s => Values.ToElement(s),
        _ => throw new NotSupportedException($"a {typeof(T).Name} has no UTV JSON; ToResult() is for UTV answers"),
    };

    /// <summary>Exactly one answer.</summary>
    [DebuggerDisplay("Unique({Value})")]
    public sealed record Unique : Outcome<T>
    {
        /// <summary>Exactly one answer, with optional notes.</summary>
        public Unique(T value, Diagnostics? diagnostics = null)
            : base(diagnostics)
        {
            Value = value;
        }

        /// <summary>The answer.</summary>
        public T Value { get; }

        /// <inheritdoc/>
        public override UtvStatus Status => UtvStatus.Unique;

        /// <summary>For <c>Unique(var value)</c> patterns.</summary>
        public void Deconstruct(out T value) => value = Value;
    }

    /// <summary>Several answers, ascending: for example both instants of a DST overlap.</summary>
    [DebuggerDisplay("Multiple({Values.Count})")]
    public sealed record Multiple : Outcome<T>
    {
        /// <summary>Several answers, in ascending order.</summary>
        public Multiple(IEnumerable<T> values, Diagnostics? diagnostics = null)
            : base(diagnostics)
        {
            ArgumentNullException.ThrowIfNull(values);
            Values = new ReadOnlyCollection<T>(values.ToArray());
        }

        /// <summary>The answers, ascending.</summary>
        public IReadOnlyList<T> Values { get; }

        /// <inheritdoc/>
        public override UtvStatus Status => UtvStatus.Multiple;

        /// <summary>For <c>Multiple(var values)</c> patterns.</summary>
        public void Deconstruct(out IReadOnlyList<T> values) => values = Values;

        /// <summary>Equal when the answers (in order) and the diagnostics are equal.</summary>
        public bool Equals(Multiple? other) => base.Equals(other) && Values.SequenceEqual(other!.Values);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Values.Count);
    }

    /// <summary>Proven: no answer exists (a day past the month end, a wall time in a DST gap).</summary>
    public sealed record None : Outcome<T>
    {
        /// <summary>No answer exists; <paramref name="diagnostics"/> says why.</summary>
        public None(Diagnostics diagnostics)
            : base(diagnostics)
        {
        }

        /// <inheritdoc/>
        public override UtvStatus Status => UtvStatus.None;

        /// <summary>For <c>None(var why)</c> patterns.</summary>
        public void Deconstruct(out Diagnostics why) => why = Diagnostics;
    }

    /// <summary>This library lacks the rules or the capability to answer.</summary>
    public sealed record Unsupported : Outcome<T>
    {
        /// <summary>Not answerable here; <paramref name="diagnostics"/> says why.</summary>
        public Unsupported(Diagnostics diagnostics)
            : base(diagnostics)
        {
        }

        /// <inheritdoc/>
        public override UtvStatus Status => UtvStatus.Unsupported;

        /// <summary>For <c>Unsupported(var why)</c> patterns.</summary>
        public void Deconstruct(out Diagnostics why) => why = Diagnostics;
    }

    /// <summary>The input contradicts itself or the rules.</summary>
    public sealed record Invalid : Outcome<T>
    {
        /// <summary>The question is invalid; <paramref name="diagnostics"/> says why.</summary>
        public Invalid(Diagnostics diagnostics)
            : base(diagnostics)
        {
        }

        /// <inheritdoc/>
        public override UtvStatus Status => UtvStatus.Invalid;

        /// <summary>For <c>Invalid(var why)</c> patterns.</summary>
        public void Deconstruct(out Diagnostics why) => why = Diagnostics;
    }
}

/// <summary>The diagnostics of an <see cref="Outcome{T}"/>: an immutable list with codes and readable text.</summary>
[DebuggerDisplay("{ToString(),nq}")]
public sealed class Diagnostics : IReadOnlyList<Diagnostic>, IEquatable<Diagnostics>
{
    private readonly Diagnostic[] _items;

    /// <summary>A list of diagnostics.</summary>
    /// <exception cref="ArgumentException">An item is null.</exception>
    public Diagnostics(IEnumerable<Diagnostic> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToArray();
        if (_items.Any(d => d is null))
        {
            throw new ArgumentException("diagnostics must not contain null", nameof(items));
        }
    }

    /// <summary>No diagnostics.</summary>
    public static Diagnostics Empty { get; } = new(Array.Empty<Diagnostic>());

    /// <inheritdoc/>
    public int Count => _items.Length;

    /// <inheritdoc/>
    public Diagnostic this[int index] => _items[index];

    /// <summary>The codes, in order, for example <c>month-end</c> (docs/library-api.md §5).</summary>
    public IReadOnlyList<string> Codes => Array.AsReadOnly(_items.Select(d => d.Code).ToArray());

    /// <summary>True if any diagnostic has this code.</summary>
    public bool Has(string code) => _items.Any(d => d.Code == code);

    /// <inheritdoc/>
    public IEnumerator<Diagnostic> GetEnumerator() => ((IEnumerable<Diagnostic>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public bool Equals(Diagnostics? other) => other is not null && _items.SequenceEqual(other._items);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Diagnostics);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (Diagnostic d in _items)
        {
            hash.Add(d);
        }
        return hash.ToHashCode();
    }

    /// <summary>Each message with its code, for example <c>2026-02 has no day 31 (month-end)</c>; joined by <c>; </c>.</summary>
    public override string ToString() => string.Join("; ", _items.Select(d => d.ToString()));

    internal static Diagnostics Of(IEnumerable<Diagnostic> items) =>
        items is Diagnostics d ? d : items.Any() ? new Diagnostics(items) : Empty;
}

/// <summary>Thrown by <see cref="Outcome{T}.Single"/> for anything but a unique answer.</summary>
public sealed class OutcomeException : InvalidOperationException
{
    /// <summary>An outcome that is not <see cref="UtvStatus.Unique"/>.</summary>
    public OutcomeException(UtvStatus status, Diagnostics diagnostics)
        : base("expected exactly one answer, got " + status.ToWireString()
               + (diagnostics is { Count: > 0 } ? ": " + diagnostics : ""))
    {
        Status = status;
        Diagnostics = diagnostics ?? Diagnostics.Empty;
    }

    /// <summary>The status that was not <c>unique</c>.</summary>
    public UtvStatus Status { get; }

    /// <summary>Why.</summary>
    public Diagnostics Diagnostics { get; }
}
