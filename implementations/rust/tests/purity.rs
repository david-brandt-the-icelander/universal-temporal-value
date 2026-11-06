//! No hidden environment: library code never reads the clock, the host's time zone or locale,
//! or environment variables. The clock is an input (`Instant::from_system_time(reading)`), and
//! zone rules are a file the caller names (`ZoneRules::load(path)`).

use std::path::{Path, PathBuf};

const FORBIDDEN: [&str; 9] = ["::now(", "Local::", "Local.", ".elapsed(", "std::env", "env::var", "localtime", "\"TZ\"", "locale"];

/// The code of a source file: comments removed, and nothing from `#[cfg(test)]` on (unit tests
/// may build host-local values to check that they are refused).
fn code(path: &Path) -> String {
    let text = std::fs::read_to_string(path).unwrap_or_else(|e| panic!("{}: {e}", path.display()));
    let text = text.split("#[cfg(test)]").next().unwrap_or_default();
    text.lines().map(|l| l.split("//").next().unwrap_or_default()).collect::<Vec<_>>().join("\n")
}

fn sources(dir: &Path, out: &mut Vec<PathBuf>) {
    for entry in std::fs::read_dir(dir).expect("src/ is readable") {
        let path = entry.expect("a directory entry").path();
        if path.is_dir() {
            sources(&path, out);
        } else if path.extension().is_some_and(|e| e == "rs") {
            out.push(path);
        }
    }
}

#[test]
fn library_code_never_reads_the_clock_zone_locale_or_environment() {
    let mut files = vec![];
    sources(&Path::new(env!("CARGO_MANIFEST_DIR")).join("src"), &mut files);
    assert!(files.iter().any(|f| f.ends_with("interop/chrono.rs")) && files.len() > 10, "library sources not found");
    let mut found = vec![];
    for f in &files {
        let text = code(f);
        for pattern in FORBIDDEN {
            if text.contains(pattern) {
                found.push(format!("{}: `{pattern}`", f.display()));
            }
        }
    }
    assert!(found.is_empty(), "hidden environment in library code:\n{}", found.join("\n"));
}
