//! [`Outcome`], [`Diagnostics`], [`OutcomeError`] and [`Truth`].

use std::cmp::Ordering;
use std::fmt;

use serde_json::Value as Json;

use crate::{Diagnostic, Status, UtvResult};

/// The diagnostics of an answer: why there is none, why it is unsupported, or a note on a
/// successful one. Codes are shared by every UTV library (`docs/library-api.md` §5); messages
/// are free text.
///
/// `Display` prints each message followed by its code, `2026-02 has no day 31 (month-end)`,
/// and the type implements [`std::error::Error`], so a failed constructor works with `?`.
#[derive(Clone, PartialEq, Eq, Hash, Default)]
pub struct Diagnostics {
    list: Vec<Diagnostic>,
    operation: String,
}

impl Diagnostics {
    pub(crate) fn of(r: &UtvResult) -> Diagnostics {
        Diagnostics { list: r.diagnostics.clone(), operation: r.operation.clone() }
    }

    /// The codes, in order: `["month-end"]`.
    pub fn codes(&self) -> Vec<&str> {
        self.list.iter().map(|d| d.code.as_str()).collect()
    }

    /// Whether a diagnostic with this code is present.
    pub fn contains(&self, code: &str) -> bool {
        self.list.iter().any(|d| d.code == code)
    }

    pub fn iter(&self) -> std::slice::Iter<'_, Diagnostic> {
        self.list.iter()
    }

    pub fn is_empty(&self) -> bool {
        self.list.is_empty()
    }

    pub fn len(&self) -> usize {
        self.list.len()
    }

    /// The JSON-layer operation that produced these diagnostics (`add`, `resolve`, ...).
    pub fn operation(&self) -> &str {
        &self.operation
    }
}

impl<'a> IntoIterator for &'a Diagnostics {
    type Item = &'a Diagnostic;
    type IntoIter = std::slice::Iter<'a, Diagnostic>;
    fn into_iter(self) -> Self::IntoIter {
        self.list.iter()
    }
}

impl fmt::Display for Diagnostics {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let text: Vec<String> = self.list.iter().map(|d| format!("{} ({})", d.message, d.code)).collect();
        f.pad(&text.join("; "))
    }
}

impl fmt::Debug for Diagnostics {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_list().entries(self.list.iter().map(|d| format!("{}: {}", d.code, d.message))).finish()
    }
}

impl std::error::Error for Diagnostics {}

/// The answer to a question time can refuse: one value, several, none, or a refusal.
///
/// Only partial operations return an `Outcome` (`date.add_months(1)`, `zoned.resolve(&ctx)`,
/// parsing, JSON import). Every case carries [`Diagnostics`], possibly empty, so `Unique` and
/// `Multiple` keep their notes too (`ambiguous` on a DST overlap, `offset-assertion-dropped`).
///
/// ```
/// use utv::{CivilDate, Outcome};
/// let jan31 = CivilDate::of(2026, 1, 31)?;
/// let text = match jan31.add_months(1) {
///     Outcome::Unique(date, _) => date.to_string(),
///     Outcome::None(why) => format!("no such date: {why}"),
///     other => other.to_string(),
/// };
/// assert_eq!(text, "no such date: 2026-02 has no day 31 (month-end)");
/// # Ok::<(), utv::Diagnostics>(())
/// ```
///
/// Never `use Outcome::*`: its `None` would shadow `Option::None`.
#[derive(Clone, PartialEq, Eq, Hash)]
pub enum Outcome<T> {
    /// Exactly one answer.
    Unique(T, Diagnostics),
    /// Several answers, ascending (both instants of a DST overlap).
    Multiple(Vec<T>, Diagnostics),
    /// Proven: no answer exists (month end, DST gap).
    None(Diagnostics),
    /// This library lacks the rules or the capability.
    Unsupported(Diagnostics),
    /// The input contradicts itself or the rules.
    Invalid(Diagnostics),
}

impl<T> Outcome<T> {
    /// The wire status: `Status::Unique`, `Status::None`, ...; `.as_str()` gives `unique`, `none`, ...
    pub fn status(&self) -> Status {
        match self {
            Outcome::Unique(..) => Status::Unique,
            Outcome::Multiple(..) => Status::Multiple,
            Outcome::None(_) => Status::None,
            Outcome::Unsupported(_) => Status::Unsupported,
            Outcome::Invalid(_) => Status::Invalid,
        }
    }

    /// The diagnostics of any case.
    pub fn diagnostics(&self) -> &Diagnostics {
        match self {
            Outcome::Unique(_, d) | Outcome::Multiple(_, d) | Outcome::None(d) | Outcome::Unsupported(d) | Outcome::Invalid(d) => d,
        }
    }

    pub fn is_unique(&self) -> bool {
        matches!(self, Outcome::Unique(..))
    }

    /// The value of `Unique`; `None` for every other case (the contract's `TryGetUnique`).
    pub fn unique(self) -> Option<T> {
        match self {
            Outcome::Unique(v, _) => Some(v),
            _ => None,
        }
    }

    /// A reference to the value of `Unique`.
    pub fn as_unique(&self) -> Option<&T> {
        match self {
            Outcome::Unique(v, _) => Some(v),
            _ => None,
        }
    }

    /// The value of `Unique`, or `fallback` for every other case (`Multiple` included: no
    /// answer is ever picked silently).
    pub fn unique_or(self, fallback: T) -> T {
        self.unique().unwrap_or(fallback)
    }

    /// The value of `Unique`, or an [`OutcomeError`] carrying the status and diagnostics.
    /// Use it with `?` where anything but one answer is an error for you.
    pub fn single(self) -> Result<T, OutcomeError> {
        match self {
            Outcome::Unique(v, _) => Ok(v),
            other => Err(OutcomeError { status: other.status(), diagnostics: other.diagnostics().clone() }),
        }
    }

    /// Apply `f` to the answer(s), keeping the case and the diagnostics.
    pub fn map<U>(self, mut f: impl FnMut(T) -> U) -> Outcome<U> {
        match self {
            Outcome::Unique(v, d) => Outcome::Unique(f(v), d),
            Outcome::Multiple(vs, d) => Outcome::Multiple(vs.into_iter().map(f).collect(), d),
            Outcome::None(d) => Outcome::None(d),
            Outcome::Unsupported(d) => Outcome::Unsupported(d),
            Outcome::Invalid(d) => Outcome::Invalid(d),
        }
    }

    /// The `utv-result/1.0` result of 0.2: the bridge back to the JSON layer.
    pub fn to_result(&self) -> UtvResult
    where
        T: ToJson,
    {
        let candidates = match self {
            Outcome::Unique(v, _) => vec![v.to_json()],
            Outcome::Multiple(vs, _) => vs.iter().map(ToJson::to_json).collect(),
            _ => vec![],
        };
        let d = self.diagnostics();
        let mut r = UtvResult::new(self.status(), candidates);
        r.diagnostics = d.list.clone();
        r.operation = if d.operation.is_empty() { "validate".into() } else { d.operation.clone() };
        r
    }
}

impl<T: fmt::Display> fmt::Display for Outcome<T> {
    /// The value's text when `Unique`; otherwise the status and the codes, `none (month-end)`.
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Outcome::Unique(v, _) => f.pad(&v.to_string()),
            other if other.diagnostics().is_empty() => f.pad(other.status().as_str()),
            other => f.pad(&format!("{} ({})", other.status().as_str(), other.diagnostics().codes().join(", "))),
        }
    }
}

impl<T: fmt::Debug> fmt::Debug for Outcome<T> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Outcome::Unique(v, d) if d.is_empty() => f.debug_tuple("Unique").field(v).finish(),
            Outcome::Unique(v, d) => f.debug_tuple("Unique").field(v).field(d).finish(),
            Outcome::Multiple(vs, d) => f.debug_tuple("Multiple").field(vs).field(d).finish(),
            Outcome::None(d) => f.debug_tuple("None").field(d).finish(),
            Outcome::Unsupported(d) => f.debug_tuple("Unsupported").field(d).finish(),
            Outcome::Invalid(d) => f.debug_tuple("Invalid").field(d).finish(),
        }
    }
}

/// What [`Outcome::single`] returns for anything but `Unique`: the status and its diagnostics.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct OutcomeError {
    pub status: Status,
    pub diagnostics: Diagnostics,
}

impl fmt::Display for OutcomeError {
    /// `none: 2026-02 has no day 31 (month-end)`
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        if self.diagnostics.is_empty() {
            write!(f, "{}", self.status.as_str())
        } else {
            write!(f, "{}: {}", self.status.as_str(), self.diagnostics)
        }
    }
}

impl std::error::Error for OutcomeError {}

/// Three-valued truth, for questions an unknown endpoint can leave open.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum Truth {
    Definitely,
    Possibly,
    Impossible,
}

impl Truth {
    pub fn as_str(self) -> &'static str {
        match self {
            Truth::Definitely => "definitely",
            Truth::Possibly => "possibly",
            Truth::Impossible => "impossible",
        }
    }

    pub(crate) fn parse(s: &str) -> Option<Truth> {
        Some(match s {
            "definitely" => Truth::Definitely,
            "possibly" => Truth::Possibly,
            "impossible" => Truth::Impossible,
            _ => return None,
        })
    }
}

impl fmt::Display for Truth {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.pad(self.as_str())
    }
}

/// Values that have a UTV JSON form (every typed value, plus the answers of `compare`).
pub trait ToJson {
    fn to_json(&self) -> Json;
}

impl ToJson for Ordering {
    /// `"before"`, `"equal"` or `"after"`, as the JSON-level `compare` answers.
    fn to_json(&self) -> Json {
        Json::String(
            match self {
                Ordering::Less => "before",
                Ordering::Equal => "equal",
                Ordering::Greater => "after",
            }
            .into(),
        )
    }
}

impl ToJson for Truth {
    fn to_json(&self) -> Json {
        Json::String(self.as_str().into())
    }
}

/// Turn a JSON-layer result into an outcome, reading each candidate with `read`. A candidate
/// that doesn't read (it shouldn't happen) turns the whole outcome into that failure.
pub(crate) fn outcome<T>(r: UtvResult, read: impl Fn(&Json) -> Result<T, UtvResult>) -> Outcome<T> {
    let d = Diagnostics::of(&r);
    match r.status {
        Status::Unique | Status::Multiple => {
            let values: Result<Vec<T>, UtvResult> = r.candidates.iter().map(read).collect();
            match values {
                Ok(mut vs) if r.status == Status::Unique && vs.len() == 1 => Outcome::Unique(vs.remove(0), d),
                Ok(vs) => Outcome::Multiple(vs, d),
                Err(e) => failure(e),
            }
        }
        _ => failure(r),
    }
}

/// A result without an answer, as an outcome.
pub(crate) fn failure<T>(r: UtvResult) -> Outcome<T> {
    let d = Diagnostics::of(&r);
    match r.status {
        Status::None => Outcome::None(d),
        Status::Invalid => Outcome::Invalid(d),
        _ => Outcome::Unsupported(d),
    }
}

/// The answer of an operation that can't fail for valid typed values.
///
/// # Panics
///
/// Only if the JSON layer refuses anyway, which for valid typed values means arithmetic beyond
/// this library's `i128` range, the way integer overflow panics in Rust.
pub(crate) fn total<T>(r: UtvResult, read: impl Fn(&Json) -> Result<T, UtvResult>) -> T {
    let operation = r.operation.clone();
    match outcome(r, read) {
        Outcome::Unique(v, _) => v,
        other => panic!("utv: `{operation}` can't fail for valid values, but answered {:?}: {}", other.status(), other.diagnostics()),
    }
}
