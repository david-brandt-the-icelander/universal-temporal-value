using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using Utv;
using Utv.Examples;

// Part 1: build dates from C# values and handle each outcome.
static string CivilDate(int year, int month, int day) => new JsonObject
{
    ["schema"] = "utv/1.0",
    ["kind"] = "civil",
    ["value"] = new JsonObject
    {
        ["form"] = "date",
        ["fields"] = new JsonObject { ["year"] = year.ToString(CultureInfo.InvariantCulture), ["month"] = month, ["day"] = day },
        ["calendar"] = new JsonObject { ["id"] = "gregory", ["chronology"] = "proleptic" },
    },
}.ToJsonString();

Console.WriteLine("== Building dates in C# ==");
foreach ((string label, string json) in new[] { ("5 November 2026", CivilDate(2026, 11, 5)), ("30 February 2026", CivilDate(2026, 2, 30)) })
{
    UtvResult structural = UtvValidator.ValidateStructural(json);
    UtvResult semantic = GregorianCivil.Validate(json); // runs the structural check first
    string verdict = semantic.Status switch
    {
        UtvStatus.Valid => "a real date; store it",
        UtvStatus.Invalid when structural.Status == UtvStatus.Valid => "well-formed, but no such day; ask again",
        UtvStatus.Invalid => "not well-formed UTV; reject the input",
        UtvStatus.Unsupported => "can't judge this calendar here; keep it as is",
        _ => $"unexpected status {semantic.Status.ToWireString()}",
    };
    Console.WriteLine();
    Console.WriteLine(label);
    Console.WriteLine($"  structural: {structural.Status.ToWireString()}");
    Console.WriteLine($"  gregorian:  {semantic.Status.ToWireString()} ({verdict})");
    foreach (Diagnostic d in semantic.Diagnostics)
    {
        Console.WriteLine($"  diagnostic: {d.Code} at {d.Path}: {d.Message}");
    }
}
Console.WriteLine();

// Part 2: the shared catalog, located relative to this project at build time.
string root = typeof(TraditionalCatalog).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
    .Single(a => a.Key == "UtvRepositoryRoot").Value
    ?? throw new InvalidOperationException("UtvRepositoryRoot metadata is missing");
var entries = TraditionalCatalog.Load(Path.Combine(root, "examples", "traditional.json"));
return TraditionalCatalog.Tour(entries, Console.Out) == 0 ? 0 : 1;
