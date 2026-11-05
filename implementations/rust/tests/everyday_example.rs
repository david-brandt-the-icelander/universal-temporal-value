//! Snapshot test of `examples/everyday.rs` (`docs/library-api.md` §7).
//!
//! It runs the example and compares every output line with `examples/everyday.expected.txt`,
//! except host-dependent lines (the clock, the host's zone), which end in ` ‡`. Each output
//! line must also fit in 100 columns.
//!
//! There is no libtest harness (`harness = false` in Cargo.toml): this binary includes the
//! example as a module and runs itself in a child process to capture the example's stdout.
//! To record a new snapshot: `cargo run --example everyday --features chrono > examples/everyday.expected.txt`.

use std::path::PathBuf;
use std::process::{exit, Command};

#[path = "../examples/everyday.rs"]
mod everyday;

const CHILD: &str = "UTV_EVERYDAY_EXAMPLE_CHILD";
const HOST_DEPENDENT: &str = " ‡";

fn main() {
    if std::env::var_os(CHILD).is_some() {
        if let Err(e) = everyday::main() {
            eprintln!("the example failed: {e}");
            exit(1);
        }
        return;
    }

    let out = Command::new(std::env::current_exe().expect("this test's path")).env(CHILD, "1").output().expect("the example runs");
    if !out.status.success() {
        eprintln!("the example failed:\n{}", String::from_utf8_lossy(&out.stderr));
        exit(1);
    }
    let actual = String::from_utf8(out.stdout).expect("UTF-8 output");
    let path = PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("examples/everyday.expected.txt");
    let expected = std::fs::read_to_string(&path).unwrap_or_else(|e| panic!("{}: {e}", path.display()));

    let (actual, expected): (Vec<&str>, Vec<&str>) = (actual.lines().collect(), expected.lines().collect());
    let mut problems = vec![];
    if actual.len() != expected.len() {
        problems.push(format!("{} lines, the snapshot has {}", actual.len(), expected.len()));
    }
    let (mut compared, mut skipped) = (0, 0);
    for (i, (got, want)) in actual.iter().zip(&expected).enumerate() {
        if got.chars().count() > 100 {
            problems.push(format!("line {}: {} columns, more than 100: {got}", i + 1, got.chars().count()));
        }
        if want.ends_with(HOST_DEPENDENT) && got.ends_with(HOST_DEPENDENT) {
            skipped += 1;
        } else if got == want {
            compared += 1;
        } else {
            problems.push(format!("line {}:\n  expected: {want}\n  actual:   {got}", i + 1));
        }
    }
    if !problems.is_empty() {
        eprintln!("examples/everyday.rs differs from {}:\n{}", path.display(), problems.join("\n"));
        exit(1);
    }
    println!("everyday example snapshot: {compared} lines match, {skipped} host-dependent lines (‡) skipped");
}
