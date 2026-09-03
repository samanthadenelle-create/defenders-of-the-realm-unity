# WORK ORDER 1283 — the overnight APK build is judged UNASSERTED

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED — marker callers, failure guard, and bidirectional static oracle implemented; awaiting real final build proof (2026-08-30)
**Minted:** 2026-08-30 (CLI seat, main line; banner bumped 1283 -> 1284 in the same edit)
**Lane:** Build/ship tooling (isolated — no gameplay code)
**Size:** small. One argument, one guard, one regression.

---

## The defect, observed live

During tonight's sanctioned prod ship (`overnight-apk-build.ps1`, 2026-08-30 02:02–02:08) the
runner printed, verbatim:

```
[run] NOTICE: no -ExpectMarker was supplied - success is being judged by LOG TEXT ONLY.
      Nothing proves this log came from this run (WO-984).
[run] VERDICT=PASS-UNASSERTED (log text only, NO marker was checked)
      log=D:\eoa\Builds\apk-build.log mtime=2026-08-30T02:08:13 sizeBytes=3430682
```

`overnight-apk-build.ps1:72` invokes the Android build **without** `-ExpectMarker`:

```powershell
& '.\run-unity-method.ps1' -Method DeNelle.Editor.AndroidBuild.BuildSeekerApk `
    -LogName apk-build.log -TimeoutMin 120 -BuildTarget Android -ExtraScriptingDefines $Defines
```

`install-apk-to-seeker.ps1` calls the **same method** and *does* assert it:

```powershell
-ExpectMarker '[AndroidBuild] SUCCEEDED'
```

**Same build, two callers, only one proves the log came from this run.** The chain intended for an
unattended overnight prod ship is the weaker of the two — exactly backwards.

---

## Why this matters (it is the §16 failure shape, again)

This is the same duplicated-state drift that §16 was written about after the R2 push+verify pair was
copy-pasted into two chains and silently diverged — *"overnight pushed then verified; morning ONLY
VERIFIED."* Here the divergence is in the marker assertion rather than the push, and it has the same
property: **the weaker path exits 0 and looks identical to the stronger one.**

The repo's standing rule is `gates-report-success-without-proving-it` — runners exit 0 on refusals
and FAILs, so a marker on a fresh log is the only proof. `PASS-UNASSERTED` is, by the runner's own
wording, *not that proof*.

**What saved tonight's ship was a separate check, not this one:** `overnight-apk-build.ps1` records
`APK_OK` only after a real `Test-Path` + size read on the artifact. That is a genuine signal and it
is why tonight's APK is trustworthy. But it proves *a file exists*, not *this run produced it* — a
stale APK from a previous run sitting at the same path would satisfy it. The marker is what closes
that gap, and it is the one thing not being asked for.

---

## Scope

1. Add `-ExpectMarker '[AndroidBuild] SUCCEEDED'` to the `run-unity-method.ps1` invocation at
   `overnight-apk-build.ps1:72`.
2. Fail the chain when it is absent — `Die`/exit non-zero with the log path named, in the same style
   as the existing `SCHEMA_PARITY_OK` guard at `:60-66`. Do not merely warn.
3. **Audit every other caller of `run-unity-method.ps1` in the repo** for the same omission and fix
   each. At minimum check: `morning-ship-chain.ps1`, `distribute-android.ps1`, `build-windows.ps1`,
   `build-webgl.ps1`, `ship-webgl.ps1`, `overnight-webgl-deploy.ps1`, `run-autopilot-fleet.ps1`.
   Report the full list with its verdict per caller — a caller that legitimately has no marker must
   say so in a comment, not be silently left bare.
4. Add a regression that **greps the shipping scripts** and fails if any `run-unity-method.ps1`
   invocation on a player-artifact path lacks `-ExpectMarker`. Without this the fix rots the moment
   someone adds a fifth chain.

**Do NOT** "fix" this by making `-ExpectMarker` default to something. A wrong default marker that
silently matches is worse than a loud `PASS-UNASSERTED` — the current notice is at least honest.
The caller must name the marker it expects.

---

## Acceptance criteria

- [ ] `overnight-apk-build.ps1` passes `-ExpectMarker '[AndroidBuild] SUCCEEDED'` and the runner
      prints `VERDICT=PASS` (not `PASS-UNASSERTED`) on a real APK build.
- [ ] Deleting/renaming the APK mid-run, or feeding a stale log, makes the chain **exit non-zero**
      with the log path named. **Prove the failure path, not only the success path** — memory
      `prove-the-success-path-not-just-the-refusal` records a pin guard that aborted every good run
      while exiting 0, because only one side was tested. Test BOTH directions here.
- [ ] Every other `run-unity-method.ps1` caller audited; list reported with a per-caller verdict.
- [ ] New regression fails when a player-artifact invocation is missing `-ExpectMarker`.
- [ ] `COMPILE_GATE_OK` (if any `.cs` changes) and `REGRESSION_OK <n>/<n> suites`, judged by marker
      on a fresh log.

## What NOT to touch

- Do not change `run-unity-method.ps1`'s verdict semantics or its `PASS-UNASSERTED` wording — the
  notice is working correctly; the callers are wrong.
- Do not alter `tools/r2-ship.ps1`, the `.githooks/pre-push` hook, or the R2 push/verify argument
  forms (§16 — both are hardcoded exactly once, on purpose).
- Do not remove the existing `APK_OK` `Test-Path` check. It is complementary, not redundant: the
  marker proves provenance, the file check proves the artifact exists.

## Canon (§15)

- `CLI_LANES_WO_NUMBERS.md` — bumped at mint (1283 -> 1284).
- Note the marker-assertion rule wherever the ship chain is documented (`docs/HANDOVER.md` build
  cycle section).
