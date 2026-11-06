using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Utv.Examples;

/// <summary>One entry of <c>examples/traditional.json</c>.</summary>
/// <param name="Id">Stable identifier.</param>
/// <param name="Section">Section heading, for example <c>Dates</c>.</param>
/// <param name="Title">Short title.</param>
/// <param name="Usual">The call you would usually write in a conventional library.</param>
/// <param name="Point">Why the UTV form is different.</param>
/// <param name="Later">The operation and milestone this entry is waiting for, if any.</param>
/// <param name="Text">The JSON text to validate: <c>text</c> if present, otherwise the raw JSON of <c>value</c>.</param>
/// <param name="ExpectStructural">Expected status of structural validation.</param>
/// <param name="ExpectGregorian">Expected status of the Gregorian civil-date check, if the entry states one.</param>
internal sealed record CatalogEntry(
    string Id,
    string Section,
    string Title,
    string Usual,
    string Point,
    string? Later,
    string Text,
    string ExpectStructural,
    string? ExpectGregorian);

/// <summary>The outcome of one check of one entry.</summary>
/// <param name="Check"><c>structural</c> or <c>gregorian</c>.</param>
/// <param name="Expected">The status the catalog expects.</param>
/// <param name="Actual">The status this library reports.</param>
internal sealed record CheckOutcome(string Check, string Expected, string Actual)
{
    public bool Matches => string.Equals(Expected, Actual, StringComparison.Ordinal);
}

/// <summary>
/// Loads the shared catalog of traditional date/time tasks, checks each entry against this library
/// and prints the tour. Used by the example program and compiled into the test project.
/// </summary>
internal static class TraditionalCatalog
{
    /// <summary>Reads every entry of the catalog at <paramref name="path"/>, in file order.</summary>
    public static IReadOnlyList<CatalogEntry> Load(string path)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("entries").EnumerateArray().Select(ReadEntry).ToArray();
    }

    /// <summary>Runs the checks the entry states expectations for, structural first.</summary>
    public static IReadOnlyList<CheckOutcome> Check(CatalogEntry entry)
    {
        var outcomes = new List<CheckOutcome>
        {
            new("structural", entry.ExpectStructural, UtvValidator.ValidateStructural(entry.Text).Status.ToWireString()),
        };
        if (entry.ExpectGregorian is not null)
        {
            outcomes.Add(new("gregorian", entry.ExpectGregorian, GregorianCivil.Validate(entry.Text).Status.ToWireString()));
        }
        return outcomes;
    }

    /// <summary>Prints the tour of <paramref name="entries"/> and its summary line.</summary>
    /// <returns>The number of mismatches.</returns>
    public static int Tour(IReadOnlyList<CatalogEntry> entries, TextWriter output)
    {
        int mismatches = 0;
        string? section = null;
        foreach (CatalogEntry entry in entries)
        {
            if (!string.Equals(section, entry.Section, StringComparison.Ordinal))
            {
                if (section is not null)
                {
                    output.WriteLine();
                }
                section = entry.Section;
                output.WriteLine($"== {section} ==");
            }

            IReadOnlyList<CheckOutcome> outcomes = Check(entry);
            output.WriteLine();
            output.WriteLine(entry.Title);
            output.WriteLine($"  usual:      {entry.Usual}");
            foreach (CheckOutcome outcome in outcomes)
            {
                output.WriteLine($"  {outcome.Check + ":",-11} {outcome.Actual}");
            }
            output.WriteLine($"  why:        {entry.Point}");
            if (entry.Later is not null)
            {
                output.WriteLine($"  later:      {entry.Later}");
            }
            foreach (CheckOutcome outcome in outcomes.Where(o => !o.Matches))
            {
                mismatches++;
                output.WriteLine($"  MISMATCH: {outcome.Check} expected {outcome.Expected}, got {outcome.Actual}");
            }
        }

        output.WriteLine();
        output.WriteLine(mismatches == 0
            ? $"{entries.Count} examples, every outcome as expected."
            : $"{mismatches} mismatches.");
        return mismatches;
    }

    private static CatalogEntry ReadEntry(JsonElement e)
    {
        JsonElement expect = e.GetProperty("expect");
        string text = e.TryGetProperty("text", out JsonElement raw)
            ? raw.GetString()!
            : e.GetProperty("value").GetRawText();
        return new CatalogEntry(
            Id: Required(e, "id"),
            Section: Required(e, "section"),
            Title: Required(e, "title"),
            Usual: Required(e, "usual"),
            Point: Required(e, "point"),
            Later: Optional(e, "later"),
            Text: text,
            ExpectStructural: Required(expect, "structural"),
            ExpectGregorian: Optional(expect, "gregorian"));
    }

    private static string Required(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private static string? Optional(JsonElement e, string name) =>
        e.TryGetProperty(name, out JsonElement v) ? v.GetString() : null;
}
