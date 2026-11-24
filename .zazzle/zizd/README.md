# `.zazzle/zizd/`: universal-temporal-value's zizd pipeline

The steps zazzle's builders run for this repository, written against zazzle's step-pipeline spec
(`src/macos/zazzle-pipeline/pipeline-spec.md`, sections 1–4). One pipeline, `default`, with one step in
`.zazzle/zizd/validate/` (a `Dockerfile` and a `run.sh`). It only checks, so there is no deploy phase. The pipeline file
itself lives in the orchestrator (`continuous_integration_and_delivery`,
`.zazzle/zizd/pipelines/david-brandt-the-icelander/universal-temporal-value/default.json`); the copy below is what goes
there.

| Step | Image | What it does (job in `.github/workflows/validate.yml`) | Leaves in `/zazzle/output` |
|---|---|---|---|
| `validate` | `python:3.12.16-slim-bookworm` (the workflow's Python 3.12), jsonschema 4.26.0 and its dependencies pinned in `validate/requirements.txt` | `python3 tools/validate.py` (the schemas, the conformance examples and scenarios, the spec's embedded examples) and `python3 tools/side_by_side.py --check` (`docs/side-by-side.md` matches the UTV-first tests) (`structural`) | nothing |

Both tools only read the repository, so the step runs in place.

Every `FROM` names its image by tag and by digest (the multi-arch index), so a rebuilt image starts from the same
bytes on any builder.

## The pipeline file

```json
{
  "id": "david-brandt-the-icelander/universal-temporal-value:default",
  "repos": ["https://github.com/david-brandt-the-icelander/universal-temporal-value.git"],
  "steps": [
    { "name": "validate", "path": ".zazzle/zizd/validate" }
  ],
  "supersede": true
}
```

No `paths`: the checks read the spec's Markdown and the implementations' tests, so every commit can change their
result. `validate.yml` also runs on every push.

## Config keys

None. The repository is public and nothing is published. The builder's own `GITHUB_TOKEN` and z3 keys (`shared.json`)
are zazzle's and reach no step. Nothing goes in `deploy_keys`.

## What is not here

The five implementations' own test suites (`dotnet test`, `mvn -q test`, the Python `unittest`, `npm test`,
`cargo test`; `AGENTS.md`, "Running the tests") and the `.dev` validators (`Meta/Agents/...`) are not in CI today, so
they are not steps. Each would be a step of its own when wanted.

## Trying it by hand

The step was run with the spec's "Trying a step by hand" recipe (`--cpus 2 --memory 4g --pids-limit 4096`, the host's
uid, the repository read-only).

## What goes at cutover

Not yet removed: `.github/workflows/validate.yml` (its `structural` job is the `validate` step). There is no legacy
runner for this repository in `continuous_integration_and_delivery`.
