using System;
using System.IO;

namespace Utv.Examples.Tests;

/// <summary>Shared test data for the example tests. Each factory returns a fresh, valid value.</summary>
internal static class Fixtures
{
    /// <summary>
    /// The pinned zone rules of <c>conformance/rules/demo-zones.json</c>: America/New_York, Europe/Paris and
    /// Atlantic/Reykjavik, for 2026 and 2027 only. UTV never reads the host's time zone database.
    /// </summary>
    public static ZoneRules DemoZoneRules() =>
        ZoneRules.Load(Path.Combine(AppContext.BaseDirectory, "demo-zones.json"));
}
