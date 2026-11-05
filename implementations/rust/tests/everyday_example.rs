//! Runs `examples/everyday.rs` as part of `cargo test --all-features` (the example needs the
//! `chrono` feature). Only the UTV outcomes are checked; the native side is printed, never
//! asserted, because it depends on the host clock and zone. Nothing here depends on the host.

#[allow(dead_code)]
#[path = "../examples/everyday.rs"]
mod everyday;

#[test]
fn everyday_example_runs_and_every_utv_outcome_is_as_expected() {
    assert_eq!(everyday::run(), 0, "some UTV outcomes in examples/everyday.rs differ; run `cargo run --example everyday --features chrono`");
}
