using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Utv.Tests;

/// <summary>
/// The snapshot test of docs/library-api.md §7: runs the Everyday example and compares every output line with
/// <c>examples/Everyday/everyday.expected.txt</c>, except lines ending in <c> ‡</c> (they depend on this machine's
/// clock or time zone database). To record a new snapshot: <c>dotnet run --project examples/Everyday &gt; examples/Everyday/everyday.expected.txt</c>.
/// </summary>
public class EverydayExampleTests
{
    private const string HostMark = " ‡";

    private static readonly string ExampleDirectory = Fixtures.PathOf("implementations", "csharp", "examples", "Everyday");

    private static readonly Lazy<string[]> Output = new(RunExample);

    [Fact]
    public void OutputMatchesTheSnapshot()
    {
        string[] expected = Lines(File.ReadAllText(Path.Combine(ExampleDirectory, "everyday.expected.txt")));
        string[] actual = Output.Value;
        for (int i = 0; i < Math.Max(expected.Length, actual.Length); i++)
        {
            string? want = i < expected.Length ? expected[i] : null;
            string? got = i < actual.Length ? actual[i] : null;
            if (want is not null && got is not null && want.EndsWith(HostMark, StringComparison.Ordinal) && got.EndsWith(HostMark, StringComparison.Ordinal))
            {
                continue; // host-dependent
            }
            Assert.True(want == got, $"line {i + 1} differs from everyday.expected.txt:\n  expected: {want ?? "(end of snapshot)"}\n  actual:   {got ?? "(end of output)"}");
        }
    }

    [Fact]
    public void LinesStayWithin100Columns()
    {
        Assert.All(Output.Value, line => Assert.True(line.Length <= 100, $"{line.Length} columns: {line}"));
    }

    [Fact]
    public void EveryTaskIsMarked()
    {
        string source = File.ReadAllText(Path.Combine(ExampleDirectory, "Program.cs"));
        string[] ids =
        {
            "now", "a-date", "weekday-week", "feb-30", "add-days", "month-end", "days-between", "compare-dates",
            "time-of-day", "datetime-plus-hours", "parse-offsets", "meeting-in-paris", "dst-gap", "dst-overlap",
            "timestamps", "day-vs-24h", "ranges", "round-trip",
        };
        foreach (string id in ids)
        {
            Assert.Single(Regex.Matches(source, @"^\s*// BEGIN " + id + @"\s*$", RegexOptions.Multiline));
            Assert.Single(Regex.Matches(source, @"^\s*// END " + id + @"\s*$", RegexOptions.Multiline));
        }
        // The headline partial operations are handled with a real switch on the outcome.
        foreach (string id in new[] { "month-end", "dst-gap", "dst-overlap" })
        {
            Match task = Regex.Match(source, "// BEGIN " + id + "(.*?)// END " + id, RegexOptions.Singleline);
            Assert.Contains("switch (", task.Groups[1].Value, StringComparison.Ordinal);
        }
    }

    private static string[] RunExample()
    {
        MethodInfo main = Assembly.Load("Everyday").EntryPoint ?? throw new InvalidOperationException("the example has no entry point");
        TextWriter saved = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            main.Invoke(null, new object[] { Array.Empty<string>() });
        }
        finally
        {
            Console.SetOut(saved);
        }
        return Lines(output.ToString());
    }

    private static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
}
