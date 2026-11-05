"""utv-result/1.0 result envelope (spec/RESULT-CONTRACT.md)."""

from __future__ import annotations

import enum
from dataclasses import dataclass, field
from typing import Any


class Status(str, enum.Enum):
    INVALID = "invalid"
    UNSUPPORTED = "unsupported"
    INDETERMINATE = "indeterminate"
    NONE = "none"
    UNIQUE = "unique"
    MULTIPLE = "multiple"
    RANGE = "range"
    SYMBOLIC = "symbolic"
    RESOURCE_LIMIT = "resource-limit"
    ERROR = "error"
    VALID = "valid"


@dataclass(frozen=True)
class Diagnostic:
    code: str
    severity: str
    path: str
    message: str

    def to_dict(self) -> dict[str, Any]:
        return {"code": self.code, "severity": self.severity, "path": self.path, "message": self.message}


@dataclass
class Result:
    operation: str
    status: Status
    diagnostics: list[Diagnostic] = field(default_factory=list)
    manifest: dict[str, Any] = field(default_factory=lambda: {"dependencies": []})
    assumptions: list[Any] = field(default_factory=list)
    loss: dict[str, Any] = field(default_factory=lambda: {"classification": "lossless", "fields": []})
    schema: str = "utv-result/1.0"

    def to_dict(self) -> dict[str, Any]:
        return {
            "schema": self.schema,
            "operation": self.operation,
            "status": self.status.value,
            "manifest": self.manifest,
            "assumptions": list(self.assumptions),
            "loss": self.loss,
            "diagnostics": [d.to_dict() for d in self.diagnostics],
        }
