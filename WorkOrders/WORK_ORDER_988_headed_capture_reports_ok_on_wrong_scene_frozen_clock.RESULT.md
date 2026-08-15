# RESULT — WO-988 Headed dungeon capture gate

**Status:** DONE — 2026-08-15 (verified; implementation already on disk)

## Proof (ran)

```
powershell -File tools\capture\headed-dungeon-capture.ps1 -SelfTest All
```

| Case | Result |
|------|--------|
| SceneMismatch | Ok=False Code=**5** |
| FrozenClock | Ok=False Code=**6** |
| FocusStolen | Ok=False Code=**7** |
| NoMovement | Ok=False (moved 0.00m) |
| Healthy | Ok=True moved 3.90m |
| **SELFTEST_OK** | **5/5** |

Script already staged shots, refused wrong-scene/frozen/focus/no-move, and only emits `HEADED_CAPTURE_OK` on healthy path. No further code change required this pass.
