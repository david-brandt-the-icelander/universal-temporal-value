using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Utv.Examples;

// Everyday date/time tasks with .NET's own types and with UTV, side by side.
// The demo zone rules are read from the repository root, located relative to this project at build time.
string root = typeof(EverydayTour).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
    .Single(a => a.Key == "UtvRepositoryRoot").Value
    ?? throw new InvalidOperationException("UtvRepositoryRoot metadata is missing");
return EverydayTour.Run(Console.Out, Path.Combine(root, "conformance", "rules", "demo-zones.json"));
