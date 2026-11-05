# Conformance material (UTV 1.0)

Language-neutral fixtures shared by every implementation in `implementations/`.

| File | Contents | Status |
|---|---|---|
| `examples/valid.json` | 31 named values covering 24 of the 26 core kinds (`constraint` and `calendar-rule` have no example yet; OI-16), all structurally valid against `schema/utv-1.0.schema.json` | Checked in CI |
| `examples/invalid-structural.json` | 8 named values that the schema must reject | Checked in CI |
| `scenarios/falsehoods.json` | 62 scenarios, one per row of `spec/FALSEHOODS-MATRIX.md` | Design requirements |
| `scenarios/standards-convergence.json` | 32 scenarios from the standards review (`STD-001`…`STD-032`) | Design requirements |

## Structural vs. semantic

The examples test **structure** only. A value in `valid.json` is well-formed. It may still be semantically invalid or unresolvable under particular rules, and that is deliberate: for example, `new-york-gap-intention` is a valid value that resolves to no instant.

The scenarios describe **semantic** behaviour: the expected outcome of validation, resolution, comparison or transformation. They aren't executable yet. Turning each one into concrete inputs, an evaluation context and an expected `utv-result/1.0` envelope is tracked as OI-12 in `spec/OPEN-ISSUES.md`. Scenario `status` values describe the fixture itself:

- `design-test-required` / `design-requirement-not-implemented`: specified, with no executable fixture yet.

## Running the structural checks

```bash
pip install -r tools/requirements.txt
```

```bash
python3 tools/validate.py
```
