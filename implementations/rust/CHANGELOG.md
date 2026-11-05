# Changelog

## 0.3.0

The typed layer of `docs/library-api.md`, on top of the 0.2 JSON layer, which keeps its
behaviour and its fixtures.

- **Typed values**, at the crate root: `CivilDate`, `CivilYearMonth`, `CivilTime`,
  `CivilDateTime`, `FixedOffsetDateTime`, `ZonedIntent`, `Instant`, `Period`, `PosixSeconds`,
  `ElapsedSeconds`, `Interval<T>` with `Bound<T>` (`T` is `CivilDate` or `Instant`), and the
  `enum Value` with `utv::parse(text)`. Constructors return `Result<T, Diagnostics>`; values
  implement `Display` (the text form), `Debug` (`CivilDate(2026-11-05)`), `Eq`, `Hash`,
  `FromStr`, `to_json` / `from_json` / `parse`, and `Ord` where the order can't depend on zone
  rules. Every operation calls the JSON layer underneath.
- **Total vs partial.** Operations that can't fail return plain values (`add_days`,
  `add_months_constrained`, `days_until`, `Instant::add`, `contains` → `Truth`); the ones that
  can return `Outcome<T>`: `Unique(T, Diagnostics)`, `Multiple(Vec<T>, Diagnostics)`,
  `None`, `Unsupported`, `Invalid`, with `.diagnostics()`, `.unique()`, `.unique_or()`,
  `.single()` (→ `Result<T, OutcomeError>`, for `?`), `.status()`, `.map()` and `.to_result()`.
- **Typed policies and a context.** `MonthEnd`, `Gap`, `Overlap`, `ResolutionPolicy` (`STRICT`,
  `ALL_CANDIDATES`); `TemporalContext::with(ZoneRules::load(path)?)` with `resolve`,
  `resolve_with`, `in_zone` and `compare` (→ `Outcome<Ordering>`). New `Rules::load(path)`,
  returning `Result<Rules, Diagnostics>` (`malformed-value`). The total month-end forms are
  separate methods (`add_months_constrained`), so `Reject` can't reach them.
- **`from_json`:** another kind or form is `invalid` (`kind-mismatch`); a valid value no typed
  type holds (zoned date, `resolutionPolicy`, interval of date-times, recurrence) is
  `unsupported` (`unsupported-operand`). Intervals without any point are constructor errors
  (`unsupported-form`), as is `(.., ..)` in the text form. `Instant::nanoseconds` is the floor
  of the sub-second part and `Instant::sub_second` the exact fraction.
- **Text form** (`docs/text-form.md`): `parse_text` / `format_text` in the new module `utv::text`,
  with periods (`P1Y2M10DT2H30M`, `-P1M`), bracket intervals (`[2026-11-01, ..)`,
  `[2026-11-01, ?)`) and EDTF slash intervals on input. `format_text` refuses values it can't
  write losslessly (`no-text-form`).
- **Shared diagnostic codes** (library-api §5) everywhere in the JSON layer. JSON of the wrong
  shape is `malformed-value` (before the validator's `envelope.*` / `structural.schema`
  details); `Z[zone]` is `unsupported-annotation`. Further renames, for example
  `iso.field` → `day-out-of-range` / `month-out-of-range` / `field-out-of-range` /
  `offset-out-of-range`, `iso.syntax` → `syntax`, `range` → `implementation-limit`,
  `zone.unknown` → `unknown-zone`, `rules.window` → `outside-rule-window`, `floating` → `no-zone`,
  `compare.zoned` → `needs-zone-rules`, `leap-seconds` → `needs-leap-seconds`, `zone.mixed` →
  `different-zones`, `interop.*` → `local-zone`, `out-of-native-range`, `precision-loss`,
  `zone-dropped`. Messages are shorter (`2026-02 has no day 31`). Invalid JSON text is `syntax`.
- **Native types:** `From`/`TryFrom` between the typed values and chrono's `NaiveDate`,
  `NaiveTime`, `NaiveDateTime`, `DateTime<FixedOffset>` (the target type, `Instant` or
  `FixedOffsetDateTime`, names the intent) and `DateTime<Utc>`; `Instant::from_system_time` and
  `SystemTime::try_from(instant)`. Nothing converts from `DateTime<Local>`.
- **Builders:** `build::elapsed_seconds(n)`. `build::Period` is now the typed `Period`
  (same fields; its constructors are `const fn`).
- **Example:** `examples/everyday.rs` rewritten as plain code: chrono then UTV for each task, a
  real `match` on the outcome for month end, DST gap and DST overlap, outputs as trailing
  comments, aligned rows, `// BEGIN`/`// END` task markers. Its output is the snapshot
  `examples/everyday.expected.txt`; host-dependent lines end in `‡`.
- **Tests:** the fixture runner checks `expect.diagnostics`; new `tests/text_form.rs` (all of
  `conformance/operations/text-form.json`), `tests/typed.rs`, `tests/purity.rs` (no clock, host
  zone, locale or environment in library code); `tests/everyday_example.rs` is now the snapshot
  test.

## 0.2.0

- The everyday API of `docs/everyday-api.md`, as modules re-exported at the crate root:
  `build` (builders), `iso` (`parse_iso`, `format_iso`), `ops` (`fields`, `add`, `compare`,
  `days_between`, `posix_seconds_between`, `contains`, `overlaps`) and `zones` (`Rules`,
  `resolve`, `to_zone`, `to_offset`). Exact `i128` arithmetic; values beyond it are `unsupported`.
- `UtvResult` gained `candidates` and `operation`, and `to_json()` emits both. Helpers
  `UtvResult::new`, `unique`, `value`, `as_str` and a `Display` impl.
- Native interop in `utv::interop`: `SystemTime` ↔ instant always; behind the new optional
  feature `chrono`, adapters for `NaiveDate`, `NaiveTime`, `NaiveDateTime`,
  `DateTime<FixedOffset>` (caller picks instant or offset date-time) and `DateTime<Utc>`.
  `DateTime<Local>` is refused, and lossy conversions back to chrono return `unsupported`.
- `examples/everyday.rs`: 14 everyday tasks side by side, chrono/chrono-tz then UTV
  (`cargo run --example everyday --features chrono`). chrono and chrono-tz are dev-dependencies.
- Tests: `tests/everyday.rs` runs all of `conformance/operations/everyday.json`;
  `tests/everyday_example.rs` runs the example under `cargo test --all-features`;
  `tests/operations.rs` covers rules the fixtures don't.
- Schema patterns used by structural validation are compiled once and cached.

### Also in 0.2.0 (changes made after 0.1.0)

- Added `examples/traditional.rs`, a runnable tour of the shared
  `examples/traditional.json` catalog (`cargo run --example traditional`), and
  `tests/traditional.rs`, which checks every catalog outcome as part of `cargo test`.
- `validate_structural` no longer runs the Gregorian date check. That check is now
  the separate `validate_gregorian_civil`, which returns `unsupported` for other kinds
  and calendars. Structural and semantic validation are separate questions.
- Any well-formed `utv/M.N` other than `utv/1.0` (including other major versions)
  now returns `unsupported`, as `VERSIONING.md` requires.

## 0.1.0

- First increment: envelope and structural validation for `utv/1.0` against the
  embedded schema, the `utv-result/1.0` result type, and proleptic Gregorian date
  checks.
