# Agent directives

These directives apply to every agent (and every human) that writes or changes code in this repository.

## 1. Tests are written for the maintainer

Every test in this repository follows
[Tests are written for the maintainer](https://icelander.net/v1/processes/tests-are-written-for-the-maintainer).
The guide is written for C#; apply it in each language's own idiom. In short:

1. **Optimize for the reader, not the writer.** A test is written once and read many times. Verbosity that
   helps the reader is welcome.
2. **Same shape every time.** Arrange, act, assert, split into labelled sections that always appear in this
   order. Leave out a section that would be empty; never merge two.

   | Stage | Section | Holds |
   |---|---|---|
   | Arrange | `GIVEN` | inputs and conditions that drive the scenario |
   | | `CAPTURE` | placeholders a mock or fake fills in |
   | | `MOCKING` | mocks and fakes and their behavior |
   | | `SETUP` | the environment the code under test runs in (zone rules, culture, …) |
   | | `SYSTEM UNDER TEST` | construction of the thing being tested |
   | Act | `WHEN` | the single action under test |
   | Assert | `EXPECTATIONS` | expected values, all declared before any assertion |
   | | `THEN` | assertions on results |
   | | `LOGGING` | assertions on logged output |
   | | `BEHAVIOR` | verification of mock interactions |

   Each section starts with a comment (`// GIVEN: …`, `# GIVEN: …`). Where it helps, the header is a full sentence
   that states intent: `// GIVEN: 31 January 2026, one month before a February with 28 days`.
3. **One documentation comment per test**, of the form "Verifies that `<thing under test>` `<behavior>` when
   `<trigger>`." (`///` in C#, Javadoc in Java, a docstring in Python, `/** */` in TypeScript, `///` in Rust.)
4. **Variable names state their role.**

   | Prefix | Role |
   |---|---|
   | `given` | input to the scenario |
   | `capture` | placeholder a mock or fake fills in |
   | `mock` | mock object (used only in `MOCKING` and `BEHAVIOR`) |
   | `fake` | hand-written test double |
   | `env` | a dependency as the code under test sees it |
   | `sut` | the thing being tested |
   | `actual` | something the test observed |
   | `expected` | something the test predicted |
   | `tmp` | a staging value that never leaves its section |

   Use the language's casing: `givenDate` / `given_date`. `THEN` compares `expected` values with `actual` values
   only, never with a `given` value, even when they are equal: give the expected value its own variable.
5. **One test, one branch, and prove it was taken.** Test names describe a specific branch:
   `Test_AddMonths_ReturnsNone_WhenTheDayIsMissingFromTheTargetMonth`, never `Test_AddMonths` or `Test_Works`.
   Parameterize only to run the *same* behavior over several datasets. Where the code under test reports why it
   took a branch (a UTV diagnostic code, a log line), assert on it.
6. **Mock as little as possible.** Prefer real instances and shared fixture factories (`Fixtures.DemoZoneRules()`);
   prefer a hand-written fake to a mock that needs gymnastics. Fixtures and fakes get tests of their own.
7. **Duplicate boilerplate instead of writing clever helpers.** No `AssertX(input, expected)` helpers, no shared
   set-up methods. A test that looks exactly like its neighbors is boring, and boring is the point.
8. **Deterministic.** Never let a result depend on the clock, the host's time zone, locale or tz database unless
   the test is *about* that dependency, and then say so in the test. If a test reads something nondeterministic on
   purpose, the documentation comment says why the outcome cannot depend on it. A flake is a finding, not noise.
9. **Tests mirror production.** Organize tests by namespace (package, module), class and function of the code
   they exercise, and name files after the class they test.
10. **Break a rule only on purpose,** and write the reason next to the code.

Tests that predate these directives are brought in line when they are next changed.

## 2. Examples are unit tests

This repository has no example programs. Every example is a unit test, so it runs with the normal test command
and cannot drift from what the library does. [`docs/example-tests.md`](docs/example-tests.md) is the catalog of
example scenarios every language covers.

Each language library has two example suites:

| Suite | Organized by | What each test shows | Expected values come from |
|---|---|---|---|
| **UTV first** | UTV's namespace, class and function (`CivilDate` → `AddMonths`) | how to do a task with UTV, and how the result compares with the language's standard date/time API | the standard API wherever the two agree; otherwise a literal, with the doc comment saying what the standard API does instead |
| **Native first** | the standard API's namespace, class and function (`System.DateOnly` → `AddMonths`) | what the standard API does for the same task, and how that compares with UTV | UTV wherever the two agree; otherwise a literal, with the doc comment saying what UTV does instead |

Where they live:

| Language | UTV first | Native first |
|---|---|---|
| C# | `implementations/csharp/tests/Utv.Examples.Tests/UtvFirst/` | `implementations/csharp/tests/Utv.Examples.Tests/NativeFirst/` |
| Java | `implementations/java/src/test/java/net/icelander/utv/examples/utvfirst/` | `implementations/java/src/test/java/net/icelander/utv/examples/nativefirst/` |
| Python | `implementations/python/tests/examples/utv_first/` | `implementations/python/tests/examples/native_first/` |
| TypeScript | `implementations/typescript/test/examples/utv-first/` | `implementations/typescript/test/examples/native-first/` |
| Rust | `implementations/rust/tests/utv_first/` | `implementations/rust/tests/native_first/` |

When you add or change an example:

- **Cover both directions.** A new example scenario gets a UTV-first test and, where the standard API can do the
  task at all, a native-first test. If the standard API has no counterpart, say so in the UTV-first test's doc
  comment.
- **Name the counterpart.** Each doc comment has a second sentence, starting `Compared with:`, that names the
  other side's API and what it does.
- **Keep the other side out of `WHEN`.** The action under test is the one the suite is about. The other side's
  answer is computed in `EXPECTATIONS` (`tmp` values may stage it).
- **Pin the rules.** UTV zone operations use `conformance/rules/demo-zones.json` through the fixture, never the
  host's tz database. Native zone operations necessarily read the host's tz database; say so in the class comment.
- **Update every language** and the catalog in `docs/example-tests.md`.
- **Side-by-side page.** UTV-first tests that illustrate a task of [`docs/side-by-side.md`](docs/side-by-side.md)
  are wrapped in `// BEGIN <task-id>` … `// END <task-id>` comments (`#` in Python). After changing one, run
  `python3 tools/side_by_side.py`; CI fails if the page is out of date.

## 3. Running the tests

| Language | Command (from `implementations/<language>/`) |
|---|---|
| C# | `dotnet test` |
| Java | `mvn -q test` |
| Python | `PYTHONPATH=src python3 -m unittest discover -s tests` |
| TypeScript | `npm test` |
| Rust | `cargo test` |

Repository checks: `python3 tools/validate.py` and `python3 tools/side_by_side.py --check`.
