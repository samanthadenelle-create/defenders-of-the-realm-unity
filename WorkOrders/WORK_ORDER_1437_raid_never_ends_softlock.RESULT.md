# WO-1437 RESULT - a won raid terminates; the exit is no longer owned by a destroyable view

**Status:** FIXED - ON THE SEEKER `2026.09.07.358574` (installed 2026-09-06 19:20). Awaiting the owner's
felt-verify: win a raid, leave by the victory door, respawn does not re-enter the raid.
**Commit:** `5bc5025f5` (2026-09-06 13:45, "fix(raid): the raid can be left, declares combat, and stops wearing
the town HUD"). The commit is this ticket's fix and the Status line was never flipped; recorded here from the
read-only board sweep of 2026-09-06 against HEAD `a67241754`.
**Evidence at source:** `Assets/_Modules/Village/Troops/RaidDeployController.cs:317,392` (the exit no longer
belongs to the destroyable victory view), `Assets/Editor/Regression/RaidTerminalStateRegression.cs:127` (600 lines,
the three exits pinned separately as §4 demanded).
**Gates on fresh logs postdating the commit:** `COMPILE_GATE_OK` (18:48), `REGRESSION_OK 414/414` (18:50).

Related tickets the same commit closed: WO-1436 (raid declares combat), WO-1435, WO-1434, WO-1432. Its own
RESULT notes that the WO-1438 behaviour fix was deliberately left unwritten; that ticket is in a lane tonight.
