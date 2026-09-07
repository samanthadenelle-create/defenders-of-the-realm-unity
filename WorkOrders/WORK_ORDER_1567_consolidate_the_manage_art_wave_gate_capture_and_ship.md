# WO-1567: consolidate the Manage art wave — clear two compile blockers, gate, capture, build, push

**Status:** READY TO IMPLEMENT — **handover to the CLI lane.** Owner ask 2026-09-06: *"hand over the
details in a WO to CLI for them to consolidate and push with the build."*
**Priority:** P1 — it is the gate/ship step for work that is already committed and cannot be proven until
the tree compiles.
**Silo:** the gate + capture + build chain. ⛔ **This WO changes no gameplay code.** The only edits it
authorises are the two compile fixes in §2, and those belong to the lanes named there.
**Minted** from the banner (`CLI_LANES_WO_NUMBERS.md`, main line 1567 -> 1568 in the SAME edit).

---

## 1. WHAT IS ALREADY DONE AND COMMITTED — do not redo any of it

| Commit | What |
|---|---|
| `ad808ecf3` | **the Manage art conformance pack** — 57 PNGs + 58 metas |
| `eb3698daf` | the art ask narrowed to an export job |
| `3cb621863` | **WO-1566** the conformance spec + the art ask |
| `8b2481895` | WO-1534, the parent review |
| `(earlier)` | WO-1541/1542/1543 + WO-1560..1565, the nine implementable lanes |

**Verified at import, before the files were written — not after:**
- **26 of 26** structures the BUILD grid offers now resolve a portrait. **Zero missing**, up from 5.
- The 21 filenames are an **exact match** for the 21 missing catalog ids — underscores intact
  (`tower_ground_archer`), `pet-house` hyphenated, and the three id/name traps honoured
  (`collector_farm` = Quarry, `collector_forge` = Iron Mine, `silo` = Stoneyard).
- 21 portraits **1024×1024 RGBA**, every corner alpha 0. 36 UI files under
  `Assets/Resources/UI/ElarionMedieval/Manage/` (25 × 256², 9 × 512², 2 × 512×64).
- **84 metas, 84 unique GUIDs, 0 duplicates, 0 orphan PNGs.** Metas generated from `barracks.png.meta`
  verbatim (Sprite / Single / `alphaIsTransparency: 1`).
- No file overwrote an existing one; the six tier ladders are untouched and complete.

⚠ **ALL OF THAT IS FILESYSTEM EVIDENCE, NOT UNITY EVIDENCE.** It is arithmetic over ids and files. The
Unity-side proof is `ManagePortraitCoverageRegression`, which has **not run**, because the tree does not
compile (§2). **Do not report the art as verified until that oracle is green in a marker.**

---

## 2. ⛔ THE TWO COMPILE BLOCKERS — the gate is RED and NEITHER IS THE ART

Measured twice, `Builds/cg-artpack.log` (22:36) and `Builds/cg-artpack2.log` (22:38). Both runs
**exited 0 and both FAILED** — `VERDICT=FAIL reason=MARKER_ABSENT`, no `COMPILE_GATE_OK`. That is the
`gates-report-success-without-proving-it` class; **judge the marker, never the exit code.**

⛔ **PNGs cannot produce CS errors. The art did not cause either of these.**

### 2a. 66 errors — `Assets/Editor/Regression/RaidSelectionSpoilsRegression.cs`
The file is **committed and unmodified**. It calls `RaidSelectionVM.ArmyLockWordFor` and siblings, which
the in-flight **WO-1542** lane removed from the VM (`CS0117`, `CS1061` on `RaidSelectionVM`).
**Owner:** the WO-1542 lane. That ticket ruled *"Warning, not a lock"*, so the word changes — **the oracle
must move WITH the ruling, not be deleted.** Re-point it at whatever replaces `ArmyLockWordFor`, and keep
it asserting that a card whose face claims a lock cannot silently open (WO-1542 acceptance 4).

### 2b. 6 errors — `Assets/Editor/Regression/SkillsPanelLayoutRegression.cs`
Uncommitted. `:1460` uses **`lGround`**, which is undefined (`CS0103`), while its siblings `lWell` and
`lRaised` on the same statement resolve. **A declaration was dropped mid-edit.** One line to restore.
**Owner:** whichever lane is editing that file.

⚠ **The first gate run raced a live edit** — `RaidSelectionVM.cs` and `RaidSelectionScreen.cs` were
written at 22:37:20 and 22:37:32, *after* the run started at 22:36:09. **Confirm the tree is quiescent
before believing any gate result.** A gate over a half-written tree is not a verdict.

---

## 3. THE SEQUENCE TO RUN — in this order, each judged by its MARKER on a FRESH log

⛔ **Gate scripts live at the REPO ROOT, not `tools/`** (memory `gate-scripts-live-at-repo-root`).
⛔ **Judge by MARKER + log freshness + size. NEVER the exit code** (CLAUDE.md §8).

1. **Compile** — `run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -ExpectMarker COMPILE_GATE_OK`
2. **Regression** — `DeNelle.Editor.Regression.DataRegression.RunAll` -> `REGRESSION_OK <n>/<n> suites`.
   ⭐ **`ManagePortraitCoverageRegression` is THE proof for this wave.** It enumerates the catalog against
   the filesystem and fails **by name** on any id with no portrait. Green = the grid is dressed.
3. **Capture** — `DeNelle.Editor.UICaptureLaunch.RunManageFlowMapCaptureHeadless` ->
   `MANAGE_FLOW_MAP_OK`. Output is **`Builds/ui-capture/`**, never `docs/manage-flow-map/` (that is a
   frozen 09:17 baseline). Require **no** `CAPTURE_LEDGER_MISSING` and **no** `CAPTURE_LEDGER_DUPLICATE`.
4. ⛔ **OPEN THE PNGs AND LOOK.** A marker proves frames were written, never that they look right
   (memory `headless-screenshot-verify-ui-before-build`).
5. **Compare** against the mockup panel by panel — `WorkOrders/ManageRedesign/CAPTURE_LOOP_GOAL.md` §3 and
   **WO-1566** §2. Any visible difference is another pass; the acceptance is exact, not similar.
6. **Build + install** through the sanctioned chain only.

---

## 4. ⛔ §16 — READ THIS BEFORE PUSHING. THE ANSWER IS "NO R2 PUSH IS OWED", AND HERE IS THE PROOF

**Owner ruling 2026-09-06, asked and answered in-session:** the Manage portraits **stay in `Resources/`
and are NOT addressable.** *(She briefly said "should be addressable", then reversed it the same minute —
"ok not addressable, thats better". The reversal is the ruling. Recorded because a future seat will
otherwise re-open it.)*

**Measured this session, not assumed:**
- No Addressable group references `Resources/Portraits` — the grep returns nothing.
- `git status ServerData/` is **empty**; the wave touched none of it.

**Therefore:** this art ships **inside the APK** via `Resources`. **No `r2-ship.ps1` run is owed for it**,
and the `.githooks/pre-push` invariant (proof must postdate the bytes) is satisfied untouched, because
`ServerData/` did not change.

⚠ **That is NOT permission to hand-build and `adb install`.** CLAUDE.md §16 is explicit: installing or
distributing goes **through the scripts** (`overnight-apk-build.ps1` / `install-apk-to-seeker.ps1`),
never raw `adb`. Those chains call `r2-ship.ps1` themselves; let them. The 2026-08-20 capsule incident
happened precisely because a build was made and installed outside them.

⚠ **Had the addressable answer gone the other way, every content build would need its own push** —
bundle names are content-hashed, so a previous push can never cover a new build. It did not go that way.
Do not migrate these to Addressables without a fresh ruling.

---

## 5. TWO ART CAVEATS — neither blocks, both are owed a look

1. **The 21 portraits were reconstructed from the contact sheets and UPSCALED to 1024.** This is the
   delivery's own note: *"suitable for grid/mobile review, but should be visually checked at large
   detail-card scale after Unity import."* **Mockup panel 3 draws art large — judge them there.** Grid
   scale will flatter them.
2. **The four tile frames are NOT drop-in interchangeable at one rect.** Measured alpha bounding boxes at
   512²: `frame-max` reaches **23 px** from the top, `frame-selected` starts at **84 px**, and none is
   symmetric in its own canvas (`frame-tile` = L72 T64 R53 B83). Swapping state at a fixed rect will make
   the border jump or change weight. Either re-centre them to a common inset, or drive each state's rect
   from its own bbox. ⭐ The earlier defect — two frames opaque-centred, two hollow — **is fixed**; all
   four now read centre alpha 0.

---

## 6. ACCEPTANCE

1. `COMPILE_GATE_OK` on a fresh log, over a **quiescent** tree.
2. `REGRESSION_OK <n>/<n>` with **`ManagePortraitCoverageRegression` green** — the Unity proof that
   §1's filesystem arithmetic is real.
3. `MANAGE_FLOW_MAP_OK`, no missing or duplicate frames, and **the PNGs opened and looked at**.
4. The panel-by-panel comparison recorded against WO-1566 §2 / `CAPTURE_LOOP_GOAL.md` §3 — each row ticked
   from a frame, or marked BLOCKED with the reason named.
5. APK built and installed **through the sanctioned scripts**.
6. **Push only on the owner's word** (CLAUDE.md §11). Commit local, by explicit path, sole committer.

## 7. WHAT NOT TO TOUCH

- **The imported art.** It is committed and verified at the filesystem level; do not rename, re-import or
  "tidy" it. The filenames are load-bearing — `ManageArt.BuildingPortraitKey` uses the catalog id verbatim.
- **`RaidSelectionSpoilsRegression`'s intent.** Re-point it with the ruling; **do not delete the suite** to
  make the gate green. Deleting an oracle to pass a gate is the failure this repo has an entire §12 about.
- Any other lane's uncommitted work. Consolidate by **explicit path**, never `git add -A` (§11).
