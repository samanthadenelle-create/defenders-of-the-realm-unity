# RESULT — WO-984 run-unity-method marker gate

**Status:** DONE — 2026-08-15 (verified; implementation already on disk)

## Proof

```
powershell -File .\run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run `
  -LogName wo984-judge-stale.log -ExpectMarker COMPILE_GATE_OK `
  -JudgeExistingLog "2099-01-01T00:00:00"
```

Output (abbrev):
```
VERDICT=FAIL reason=LOG_MISSING - this run is NOT PROVEN. marker='COMPILE_GATE_OK' ...
exit=8
```

Also: without `-ExpectMarker` the script prints NOTICE that log-text-only is unasserted. Error-text scan retained alongside marker gate.
