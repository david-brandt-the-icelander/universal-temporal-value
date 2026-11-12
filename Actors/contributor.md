# Contributor

**Kind:** person in a role
**Evidence:** `AGENTS.md` (opening line: the directives apply "to every agent (and every human) that writes or changes code in this repository"; section 3, Running the tests), `README.md` (`Contributing`), `implementations/README.md` (the conventions and obligations every library must follow), `tools/validate.py` (`main`), `tools/side_by_side.py` (`main`), `.github/workflows/validate.yml` (`on`: every push and pull request)

## What they want from the system

- To know a change to the specification material is consistent before pushing it: [Check the specification material](../tools/.dev/use-case.json), `check-the-specification-material`.
- To keep the side-by-side page showing the code the example tests run: [Update the side-by-side page](../tools/.dev/use-case.json), `update-the-side-by-side-page`.
- To see on the commit or pull request whether a pushed change passes CI: [Have a pushed change checked](../.github/workflows/.dev/use-case.json), `check-a-change`.
- To change a language library without breaking its obligations: the [obligations every library meets](../implementations/.dev/requirements.json) and each library's setup runbook, `implementations/<language>/.dev/runbooks.json`.

## How they reach it

A checkout of the repository: the scripts in `tools/` (with `jsonschema` from `tools/requirements.txt`), each library's own test command (`AGENTS.md` section 3), and pushes and pull requests to GitHub, where the Validate specification workflow runs.

## Not to be confused with

The [Application developer](application-developer.md), who calls a UTV library from their own code and needs nothing from this repository but the library. `AGENTS.md` makes no distinction between a person and a coding agent: both follow the same directives, run the same checks and open the same pull requests, so the repository has one actor for both.
