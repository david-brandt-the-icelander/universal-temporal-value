# Application developer

**Kind:** person in a role
**Evidence:** the public entrypoints of the five libraries, which are all an application developer meets:
- C#: `implementations/csharp/src/Utv/Utv.csproj` (`PackageId` Utv), `src/Utv/Typed/CivilDate.cs` (`CivilDate.Of`, `TryOf`, `Parse`, `AddMonths`), `src/Utv/Typed/ZonedValues.cs` (`ZonedIntent.Resolve`, `TemporalContext.With`, `TemporalContext.Compare`), `src/Utv/Typed/Temporal.cs` (`Temporal.Parse`, `Temporal.FromJson`), `src/Utv/UtvValidator.cs` (`ValidateStructural`)
- Java: `implementations/java/pom.xml` (`artifactId` utv-core), `src/main/java/net/icelander/utv/CivilDate.java` (`of`, `tryOf`, `parse`, `addMonths`), `ZonedIntent.java` (`resolve`), `TemporalContext.java` (`with`, `compare`), `TemporalValue.java` (`parse`, `fromJson`), `Utv.java` (`validateStructural`)
- Python: `implementations/python/pyproject.toml` (`[project]`, name utv), `src/utv/__init__.py` (`__all__`), `src/utv/values.py` (`CivilDate.of`, `try_of`, `add_months`, `ZonedIntent.resolve`), `src/utv/context.py` (`TemporalContext.compare`), `src/utv/interop.py` (`from_native`, `to_native`), `src/utv/validate.py` (`validate_structural`)
- Rust: `implementations/rust/Cargo.toml` (`[package]`, name utv), `src/lib.rs` (the `pub use` re-exports, `validate_structural`), `src/typed/context.rs` (`TemporalContext::with`, `resolve`, `compare`), `src/interop/chrono.rs`
- TypeScript: `implementations/typescript/package.json` (`name` @utv/core), `src/index.ts`, `src/values.ts` (`CivilDate.of`, `tryOf`, `addMonths`, `ZonedIntent.resolve`), `src/context.ts` (`TemporalContext.with`, `compare`), `src/native.ts` (`fromDate`, `toDate`), `src/structural.ts` (`validateStructural`)

## What they want from the system

The same seven goals in every language, under the same IDs in each library's use cases:

- To make a date from fields or text, and learn why it cannot exist (`create-a-date`).
- To add months to a date without a missing day being filled in silently (`add-months`).
- To turn a wall time in a named zone into an instant, and be told when it falls in a gap or an overlap (`resolve-a-zoned-time`).
- To order two values, and be told when the order depends on zone rules or is ambiguous (`compare-values`).
- To bring their language's own date/time values in and out, without the host's zone being used (`convert-native`).
- To read and write values as text (`read-and-write-text`).
- To check UTV JSON they receive and read it into typed values (`validate-utv-json`).

Where each library serves them:

| Library | Use cases |
|---|---|
| C# | [`implementations/csharp/src/Utv/.dev/use-case.json`](../implementations/csharp/src/Utv/.dev/use-case.json) |
| Java | [`implementations/java/src/main/java/net/icelander/utv/.dev/use-case.json`](../implementations/java/src/main/java/net/icelander/utv/.dev/use-case.json) |
| Python | [`implementations/python/src/utv/.dev/use-case.json`](../implementations/python/src/utv/.dev/use-case.json) |
| Rust | [`src/typed`](../implementations/rust/src/typed/.dev/use-case.json) (`create-a-date`, `add-months`, `resolve-a-zoned-time`, `compare-values`), [`src`](../implementations/rust/src/.dev/use-case.json) (`read-and-write-text`, `validate-utv-json`), [`src/interop`](../implementations/rust/src/interop/.dev/use-case.json) (`convert-native`) |
| TypeScript | [`implementations/typescript/src/.dev/use-case.json`](../implementations/typescript/src/.dev/use-case.json) |

[`docs/side-by-side.md`](../docs/side-by-side.md) shows the everyday tasks behind these goals in all five languages.

## How they reach it

In-process, as a library in their own application: the .NET package `Utv` (namespace `Utv`, .NET 8), the Maven artifact `net.icelander.utv:utv-core` (package `net.icelander.utv`, Java 21), the Python package `utv` (Python 3.10 or later), the Cargo crate `utv` (optional feature `chrono`) or the npm package `@utv/core` (an ES module for Node.js). Within this repository each is built from its folder; nothing in the repository shows any of them published to a package registry. Zone rules are a file they load and pass in explicitly, and "now" is a clock reading they take themselves and hand to the library.

## Not to be confused with

The [Contributor](contributor.md), who changes the libraries and the specification in this repository. An application developer only calls a library; the libraries do not know who calls them and check nothing about the caller.
