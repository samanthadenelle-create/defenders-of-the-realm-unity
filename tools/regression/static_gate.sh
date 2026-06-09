#!/usr/bin/env bash
# =============================================================================
# static_gate.sh - thin wrapper around static_gate.py (WO-329).
# Lets UI/Cowork run the static check-in gate from the Linux mount with one
# command. Forwards any args (e.g. an explicit .cs file list) to the Python.
#
#   bash tools/regression/static_gate.sh
#   bash tools/regression/static_gate.sh Assets/_Modules/Foo/Bar.cs
# =============================================================================
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec python3 "${HERE}/static_gate.py" "$@"
