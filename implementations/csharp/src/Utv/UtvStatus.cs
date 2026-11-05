using System;

namespace Utv;

/// <summary>
/// The status of a <c>utv-result/1.0</c> result (spec/RESULT-CONTRACT.md, "Status invariants").
/// Non-answers such as <see cref="None"/>, <see cref="Multiple"/>, <see cref="Indeterminate"/>,
/// <see cref="Unsupported"/> and <see cref="ResourceLimit"/> are successful outcomes.
/// </summary>
public enum UtvStatus
{
    /// <summary><c>invalid</c>: contradiction, malformed input or failed semantic invariant.</summary>
    Invalid,
    /// <summary><c>unsupported</c>: no installed algorithm or necessary immutable rules (including an unimplemented specification version).</summary>
    Unsupported,
    /// <summary><c>indeterminate</c>: semantics defined but evidence insufficient or contested.</summary>
    Indeterminate,
    /// <summary><c>none</c>: a complete valid query proven to have an empty result.</summary>
    None,
    /// <summary><c>unique</c>: exactly one proven result.</summary>
    Unique,
    /// <summary><c>multiple</c>: more than one proven result.</summary>
    Multiple,
    /// <summary><c>range</c>: a continuum or interval of possible answers.</summary>
    Range,
    /// <summary><c>symbolic</c>: expression unevaluated or context not bound.</summary>
    Symbolic,
    /// <summary><c>resource-limit</c>: incomplete evaluation because a budget ran out.</summary>
    ResourceLimit,
    /// <summary><c>error</c>: operational fault, no asserted semantic conclusion.</summary>
    Error,
    /// <summary><c>valid</c>: validation succeeded (not necessarily resolvable).</summary>
    Valid,
}

/// <summary>Wire-string conversions for <see cref="UtvStatus"/>.</summary>
public static class UtvStatusExtensions
{
    /// <summary>The status as it appears on the wire, for example <c>resource-limit</c>.</summary>
    public static string ToWireString(this UtvStatus status) => status switch
    {
        UtvStatus.Invalid => "invalid",
        UtvStatus.Unsupported => "unsupported",
        UtvStatus.Indeterminate => "indeterminate",
        UtvStatus.None => "none",
        UtvStatus.Unique => "unique",
        UtvStatus.Multiple => "multiple",
        UtvStatus.Range => "range",
        UtvStatus.Symbolic => "symbolic",
        UtvStatus.ResourceLimit => "resource-limit",
        UtvStatus.Error => "error",
        UtvStatus.Valid => "valid",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "not a UTV result status"),
    };

    /// <summary>Parses a wire status string (exact, case-sensitive).</summary>
    public static bool TryParseWireString(string? text, out UtvStatus status)
    {
        foreach (UtvStatus candidate in Enum.GetValues<UtvStatus>())
        {
            if (string.Equals(candidate.ToWireString(), text, StringComparison.Ordinal))
            {
                status = candidate;
                return true;
            }
        }
        status = default;
        return false;
    }
}
