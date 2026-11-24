#!/usr/bin/env bash
# The validate.yml structural job: the schemas, the conformance examples and scenarios, the spec's embedded examples
# (tools/validate.py), and that docs/side-by-side.md matches the UTV-first tests (tools/side_by_side.py --check).
# Both only read the repository, so they run in place.
set -euo pipefail
export HOME=/tmp PYTHONDONTWRITEBYTECODE=1
srcs=(/zazzle/input/repos/*/universal-temporal-value); src=${srcs[0]}
python3 "$src/tools/validate.py"
python3 "$src/tools/side_by_side.py" --check
echo "validate: passed"
