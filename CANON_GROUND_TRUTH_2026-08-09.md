# CANON GROUND TRUTH — 2026-08-09

**Supersedes `CANON_GROUND_TRUTH_2026-08-08.md`.** Per CLAUDE.md §15 this is the single live anchor:
every other doc loses to it on conflict. Written after the 2026-08-08 day-long ship wave.

**Branch:** `wip/village2-and-f8-tickets` · **HEAD `19a50616`** (re-anchored 2026-08-09 evening) ·
**NOT PUSHED — 63 commits ahead of `origin/wip/...`.** master is stale.

> ### ⚠ THIS HEADER WAS 57 COMMITS STALE AND THE WHOLE DAY WAS MISSING FROM CANON
> It read `HEAD c8320434` / "PUSHED, 0 ahead" while 63 commits sat unpushed. Grepping this file,
> `SESSION_CANON_LOADER.md` and `docs/HANDOVER.md` for `WO-1010` / `D14` / `D19` returned **zero hits** —
> a twenty-item build-screen redesign, seven ruled canon conflicts, four false-green gate classes and
> two new generated reference maps, none of it recorded. The CLI seat enforced §15 on every other doc
> that day and skipped its own anchor. **A session booting from this file would have started blind.**
>
> **LANDED 2026-08-09 (read the git log from `c8320434` for detail):** WO-1010 P1+P2 + §7 D1–D20 (build
> screen rebuilt around the ghost: lean right-edge rail, compact corner Done, thin bottom resource strip,
> auto-showing analog nudge stick, `^ Buildings` restore tab with Structures/Defenses quick-tabs);
> seven AccuRig enemies imported Humanoid + wired + the troll family; `RULES.md`, `docs/BOARD.md`,
> `docs/UI_PLAYBOOK.md`, `docs/TICKET_LIFECYCLE.md`, `docs/reference/DATA_CLASS_MAP.md`,
> `docs/reference/VFX_AUDIO_WIRING_MAP.md` + `tools/vfx_audio_map.py`; board Unlabeled 91 → 0;
> `COMPILE_GATE_OK` made provable; Linear/Notion/Task-list retired; repo root ruled machine-dependent;
> FlowTrace never-stripped and ON in every build.
>
> **⚠ RECENCY DOES NOT CERTIFY A DOC.** This file was committed 2026-08-09 06:29 and was ALREADY FALSE
> when written (see the webbot correction below, broken by its own commit). Treat "newest wins" as a
> tiebreaker between sources, never as evidence that any one of them is right.

> ⚠ **The 08-08 anchor is not merely stale — it is INVERTED on BOTH of its headline sections.** Its §0
> ("the machine is blocked, reboot is the fix") is RESOLVED, and its §2 ("the dungeon stairs — where the
> hunt actually stands", four hypotheses dead, nothing cheap left) is CLOSED: the stairs were solved the
> same morning. Its last edit was `07d2c6f8` (08-08 08:25) and **30 commits landed on 08-08**
> (`git log` by both author and committer date), leaving it **exactly 21 commits** and ~11.5 hours behind
> HEAD. Do not act on it.

> ### ⚡ EVENING-2 DELTA (2026-08-09 ~23:00, appended in the same breath as the work — §15)
> **The WO-1010 build-screen defect pass is closed to capture-proof** (D17 sprites live, D19 seating,
> touch D-pad retired, ONE skip, P3 hint, PICK band-tightening; gates `COMPILE_GATE_OK` +
> `REGRESSION_OK 133/133` + `UI_CAPTURE_OK 62`, PNGs opened vs the owner-re-pinned
> `UI_REVIEW/build_ui_target_wireframe.html`). **The Sylas F8 is fixed at the root**:
> `HeroBodySwapper.Start()` probes `Resources/Heroes/<slug>` first — the tracked Ranger/Mage bodies
> were dead code behind a terminal-on-success Blink load. **The F8 daemon was blind to the renamed
> product folder** (`Echoes of Elarion`) — restarted on the corrected script. Minted: **WO-941**
> (pre-existing RumorBoard/RealmMap geometry overlaps, 16 assertions) · **WO-942** (capture-case
> gaps). UI seat shipped **WO-1012** (tutorial/FTUE redesign + wireframes; owner-amended §2a:
> "person A never guides person A"). **Open for the owner:** D8 Walls-tab ruling · tester re-test ·
> felt-verify of tonight's screen · WO-931/910/939/940 unchanged. EditMode XML remains stale (08-04).

---

## 0. ✅ THE MACHINE IS NO LONGER BLOCKED — the 08-08 §0 is RESOLVED

The machine **rebooted 2026-08-08 08:07:21**. Measured after the reboot:

| | State |
|---|---|
| **Commit charge** | **45.7 GB of a 127.8 GB limit** (was 119.5 / 127.8), 11.9 GB physical free, no Unity process running |
| **Windows EXE** | ✅ `Builds/Windows/DefendersOfTheRealm.exe`, **2026-08-08 14:33** |
| **Android APK** | ✅ `Builds/Android/DefendersOfTheRealm.apk`, **2026-08-08 20:00**, 572,202,338 bytes |
| **Firebase release** | ⚠ **UNPROVEN — do not record as done.** No App Distribution upload line exists in `Builds/build-apk-ship3.log` or `Builds/build-apk-resubmit.log`; `Builds/firebase-notes.txt` is dated **2026-08-07**, and `Builds/apk-release-notes.txt` is **08-08 11:59** — i.e. authored BEFORE the 14:33 / 19:44 / 20:00 APKs. The final 08-08 APK may never have reached testers. Verify before assuming testers have it. |
| **WebGL / web deploy** | ❌ **NOT DONE** — `Builds/WebGL` is still dated **2026-08-05** and there is **no `Builds/webgl-chain-status.txt`** |

**The 08-08 morning order (reboot → EXE → APK → Firebase → WebGL) ran through the APK. Its last TWO
steps are unconfirmed or undone** — Firebase is unproven (above), WebGL was never attempted. The web
rail is the thing the reboot unblocked and nobody took. Both are carried-forward actions.

> **Why this row is flagged rather than ticked:** it was originally written ✅ by inference from the
> morning order, not from a measurement. Per §12 an inferred state is a guess. Every other row in this
> table was measured off a file timestamp or a byte count; this one could not be, so it says so.

*(Standing lesson, already in memory `commit-charge-leak-blocks-builds`: long batchmode nights leak
commit charge that no process owns, player builds OOM with RAM free and nothing to kill, and a reboot
is the only fix. It worked exactly as recorded.)*

---

## 1. Gate state — read off the markers, never off this doc

| Marker file | Stamped | Emitted |
|---|---|---|
| `Builds/gate-ship3.log` | 2026-08-08 19:36 | `COMPILE_GATE_OK` |
| `Builds/regression-ship3.log` | 2026-08-08 19:38 | `REGRESSION_OK 130/130 suites` |
| `Builds/ui-capture-ship.log` | 2026-08-08 14:30 | `UI_CAPTURE_OK 44` |

⚠ **`Builds/test-results-EditMode.xml` reads `total=930 passed=930 failed=0` but is stamped 2026-08-04
— five days stale. Do NOT present it as current evidence.** Re-run it before quoting it.

⚠ **Never restate a suite count from a doc.** The three entry points emit DISTINCT markers
(`DataRegression.RunAll` → `REGRESSION_OK <n>/<n> suites`, `RegressionSuite.RunAll` → `CHECKIN_SUITE_OK`,
`SessionRegression.RunAll` → `SESSION_GUARDS_OK`) precisely so a small suite's pass can never again read
as the full suite's. The numbers above are transcribed from the marker lines named in the table and are
true only of those runs.

---

## 2. ★ THE DUNGEON STAIRS ARE SOLVED — the 08-08 §2 hunt is CLOSED

This is the lead story of 08-08, not a footnote. The 08-08 anchor's guidance — *"four hypotheses tested
and killed, nothing cheap remains, the next move is to dump navmesh triangles"* — is **OBSOLETE**. The
answer arrived a few hours after it was written, and it was not any property of the stair.

### The root cause: stair YAW

`GraphDungeonComposer.SolveMate` **hardcodes `yaw = 0f` on vertical sockets** (the planar solve
degenerates when both outwards point straight up/down). Only a Delta yaw of **180** put the flight's top
nose inside the mating floor hole; at any other Delta the flight climbed into a **solid slab**, so the
voxelizer carved no walkable span at the top, so there was no navmesh to path *from* → `PathPartial`.

**That is why four rounds of bucketing the stair's own scalars all came back negative.** Every one of
them measured a property of the stair. The defect was in *where the stair was pointed*. Keep that as the
transferable lesson: when a population bucketed against scalar after scalar keeps returning nothing, the
variable is not on the axis being measured.

### The commits, in order

| Sha | Time | What landed |
|---|---|---|
| `3ab1bfb6` | 11:24 | **WO-930 one-room stairwell shipped** — the **first floor-to-floor `PathComplete` in project history.** The old pair-model probe was deliberately kept as a control. |
| `e7163c9c` | 11:27 | Stairwell skinned via the shared `RoomForgeMaterials` — **0 bad surfaces.** |
| `5f0e23aa` | 11:53 | Stairwell candle lights (3, under the URP 4-light cap) **plus a caught RED gate**: `dg_sunken_vault.json` dual-copy drift — **Resources held the OLD 17-room layout, and Resources WINS at runtime.** The game would have loaded the old dungeon. |
| `cb092b7f` | 12:03 | bonecrypt + ember_deep converted: **all 4 content dungeons PathComplete, 12 descents, 0 mate failures, 14/14 dual-copy parity.** `dg_descent_probe` / `dg_stair_rig` deliberately left on the old model as controls. |
| `51a89364` | 14:34 | `RoomPrefabMeta` stamped on `StairwellRoom` — the overlap gate had been measuring a **20x10 m room as one 10 m cell.** Regression oracle rewritten: 8 new cases, 3 legacy quarantined. |

### ⚠ What WO-930 promised to DELETE was NOT deleted — and that is BY DESIGN

WO-930's own spec said it **DELETES** `StairUp`/`StairDown`, the vertical mate branch, `IsVertical`,
`SEALED_VERTICAL`, the floor holes and the ceiling shafts. **None of that happened, deliberately.**
All of it is **retained as a quarantined, gated CONTROL GROUP** — documented in
`DungeonMultiLevelRegression.cs:41-63` under an explicit **"⚠ DO NOT DELETE"** banner, because the code
is still live and still loaded by three graphs, and deleting it would leave live code with no oracle
*and* let the A/B control group rot, destroying the ability to re-run the comparison that proved the new
model.

- **`dg_stair_rig` and `dg_descent_probe` are TEST FIXTURES, not stale content.** `[graphs-converted]`
  asserts they **still name the retired prefabs** and that those prefabs still exist on disk, precisely so
  a tidy-up cannot delete the control group by accident. **Do not describe them as regressions or as
  unconverted debt.** (`dg_starter_loop` also still loads the retired path; it is single-floor.)
- The old model is **re-enterable only by editing a graph**, which `[graphs-converted]` fails.
- The deletion is a **future, single-commit job** (WO-930 §5): when the pair model genuinely goes, the
  three `[legacy-*]` cases, the `ControlGroupGraphs`/`ControlGroupPrefabs` arrays and the control-group
  half of `[graphs-converted]` go **in the same commit** — not before.

**Verified conversion, layout-level:** exactly four layouts are pure `"prefab": "StairwellRoom"` in both
copies — `dg_bonecrypt`, `dg_ember_deep`, `dg_sunken_vault`, `dg_stairwell_probe`. (The "all 4 content
dungeons PathComplete / 12 descents / 0 mate failures / 14/14 dual-copy parity" figure is quoted from
`cb092b7f`'s commit body, not re-measured here.)

### ⚠ AND THE BAKE'S REACHABILITY PROBE IS WEAKER THAN THE WIN SUGGESTS — OPEN, UNCOVERED

`DungeonBaker` probes **ONE** path, `placedOrder[0] → placedOrder[last]`
(`DungeonBaker.cs:432-445`), and the failure branch is **log-only**: a non-`PathComplete` result prints
the `PATH DIES` diagnostic via `FlowTrace.Fail` (`:457-479`) and **`EditorSceneManager.SaveScene` runs
unconditionally right after** (`:490-494`). **There is no per-descent probe and no abort.**

> **Why this matters specifically here:** canon's own finding is that **reachability is gated by the
> FIRST failure on the path, not by the average.** With a single end-to-end probe and no abort, a dungeon
> whose **first** descent fails is **indistinguishable** from one whose last does — and either way the
> scene still ships. The stairwell win is real; the *guard* that would catch its regression is not.

### What to preserve from the 08-08 anchor

**Keep its killed-hypotheses table as history** — landing width, slope, ramp length, navmesh tiling, plus
the 08-08 `RAMP CONTEXT` negatives (yaw buckets, overlapping colliders, voxel phase). It still does real
work: it stops a future session re-running any of them. **But the hunt it framed is over.** Anything that
reads as "the next move is to dump navmesh triangles" is dead guidance.

The permanent bake diagnostics (`PATH DIES`, `RAMP CARVE`, `RAMP SEAMS`, `RAMP SHAPE vs WHOLE`,
`RAMP CONTEXT vs WHOLE`, `RAMP TILE`) stay — start from them, not from source. And the 08-08 warning
still holds: **the instruments have been wrong twice and both times looked confident** — probe radii are
deliberately opposite (tight 0.35 m on the ramp, generous 6 m when finding a room's floor). Do not
unify them.

---

## 3. The 08-08 commit day — what else landed (30 commits)

### ⚠ The orientation revert — the transferable lesson of the day

`70a86c17` (12:41) is a **REVERT of `bb6dc010`.** Applying `SkinOptions.PreservePrefabRotation` to ALL
structures **laid the whole town on its side**: 13 catalog rows carry a manual -90 that composes to 180.
It only reproduces on the **dungeon → town return path** via `BaseLayoutLoader`.

> **★ HEADLESS GATES CANNOT SEE ORIENTATION.** Compile-green, regression-green, marker-green — and every
> building on its side. This defect class needs **eyes**, not markers: the UI capture pass, a device
> screencap, or the owner's felt-test. Say so out loud whenever a change touches transforms.

`439e03ee` (14:35) is the correct narrow fix: a **per-catalog-row `RepoProps.preservePrefabRotation`**
(default false; **exactly one row opts in — `tower_ground_archer`**), with `StructureFactory.OptsFor`
made the single reader unifying `Create` / `MeasureUprightFootprintMetres` / `GhostPreview`.
⚠ **Still-live root cause noted in that commit:** `Resources/Structures` holds both a `.fbx` and a
same-stem `.prefab`, which makes `Resources.Load` **ambiguous**. Not fixed.

### Dev tooling out of the shipped player

- `eeb2d389` (12:13) — `ff.devresourcetool` default flipped **OFF**; DevPanel moved under Settings
  (`PanelId.DevPanel` = 17, gated on `PanelRouter.IsRegistered`).
- `374ccd26` (12:55) — **RELEASE desktop player** (verified: `DeNelle.DevTools.dll` absent — 206 DLLs,
  was 207). ✅ **This closes the long-standing KEY_FACTS item "desktop release still ships Development
  builds."**
  ⚠ **KEY TRAP:** the flag flip **did nothing on this machine**, because `FeatureFlags.Get` reads
  **PlayerPrefs FIRST** and this box has `ff.devresourcetool=1` persisted from 08-07. A default change is
  not a state change on any machine that already answered the question.

### Felt fixes

- `2f10f6ac` (14:34) — auto-upgrade was handing **every level-2 knight a paid Forge `knight_flameblade`
  for free.** Candidate set narrowed to owned gear; tri-state ownership so it survives a
  `VillageInventory.EnsureLoaded` pre-load race.
- `763d1a60` (14:35) — building nameplates were rendering literal **`[[missing:market]]` /
  `[[missing:jeweler]]`** to the player; forge/armorer duplicate resolved; "Lumber Mill" renamed across
  catalog / quests / prefab.

---

## 4. Store, legal and publishing — the evening arc, and its two OPEN flags

### ⚠ The security re-gate (`576601e3`, 19:15) — read this before anyone asks to flip the flag

`FeatureFlags.RealmStorePurchase` is back to **`defaultOn: false`, and locked.** The reason is
security-grade, not hygiene:

`StubWalletProvider` has **NO `#if UNITY_EDITOR` / `DEVELOPMENT_BUILD` guard**, so it compiles into
**every** shipped player. It fabricates a wallet, a **2000 SKR mock balance** and a base58 signature;
`ApplyPackContents` then **grants the pack for ZERO payment** while firing `purchase_completed` with the
fake txSig. **The submitted store build had a tappable Buy button.**

This is **WO-931, READY TO IMPLEMENT**, and it is **precondition 3 of 3** in that flag's
DO-NOT-TURN-ON block. The flag does not move until 931 lands.

### Shipped

- `640bfc1c` (19:48) — `productName` → **"Echoes of Elarion"**, so the app installs under the store
  listing name.
- `c8320434` (19:48, HEAD) — `docs/TERMS_OF_USE.md` authored and **hosted verbatim** at `site/terms.html`,
  live at `https://echoes-of-elarion.vercel.app/terms` (verified HTTP 200), linked from the landing nav
  and footer. Governing law **Texas**. ⚠ **No arbitration clause, no class-action waiver, no jury-trial
  waiver was added — deliberately left for the owner's attorney.**
  Publishing scaffold added under `publishing/` (`config.yaml`, `SUBMIT_CHECKLIST.md`, `media/README.md`)
  plus `tools/store_previews_resize.py`.

### ⚠ Two flags raised in that commit body, both STILL OPEN

1. **`PRIVACY_POLICY.md:87-89` contains ONE FALSE SENTENCE on a LIVE PUBLISHED PAGE.** It says the Ad
   button "grants that time saving immediately without presenting any advertisement" — but **that button
   is now ABSENT from the UI entirely.** The core no-ads claim is **verified TRUE**; only the explanatory
   sentence is stale. ⛔ **Not edited by canon work — a live legal page's wording is the owner's (and her
   attorney's) call.** Recorded here as an open item, nothing more.
2. **`docs/PUBLISHING_STEPS.md` Rail 1 is OBSOLETE** and now carries a STALE banner.
   `dapp-store-cli@1.0.0` has **NO `init` / `create` / `validate` / `publish`** subcommands — its whole
   surface is `dapp-store --apk-file ... --whats-new ...` — and **the app must ALREADY exist in the portal
   with an App NFT.** Publisher and app are now created **in the web portal with a browser wallet**;
   `publishing/config.yaml` is kept as the **verified paste-source** for that form, not as CLI input.

---

## 5. Working-tree anomalies — both OPEN

**The tree is NOT clean.**

### `tools/webbot/` was DELETED OUTSIDE GIT

> **⚠ CORRECTED 2026-08-09: THE PARAGRAPH BELOW WAS FALSE THE MOMENT IT WAS WRITTEN.** Commit
> `e1380870` — **the same commit that added this file** — `git rm`'d all four, on a confirmed owner
> decision. They are **NOT present at HEAD**, and recovery is `git checkout c8320434 -- tools/webbot/`,
> **not** `git checkout -- tools/webbot/` (which restores nothing). The same inversion is repeated in
> `SESSION_CANON_LOADER.md` and `KEY_FACTS.md`.

All four files (`canvas-probe.js`, `introtest.js`, `package.json`, `webbot.js`) were **present at
`c8320434`**, were **deleted in `e1380870`**, are **not gitignored**, and **the directory does not
exist on disk.** This is the **Playwright web-build self-test
rig** — the eyes on the deployed web build (memory `owner-office-autonomy-web-loop`).

Restorable with `git checkout -- tools/webbot/`. **That has NOT been run.** It is an **open decision for
the owner** — deliberate removal or accidental deletion is not established. Do not treat it as decided.

### `ProjectSettings/ProjectSettings.asset` — not a hand edit

The diff is **exactly two keys**: `bundleVersion 2026.08.09.316839 → 2026.08.09.316856` and
`AndroidBundleVersionCode 316839 → 316856`. Both are auto-stamped by `AndroidBuild.BuildSeekerApk`, so
**an Android build ran AFTER HEAD.** Reconcile it as a build stamp, not as content.

---

## 6. F8 inbox — ONE UNACKNOWLEDGED capture

**seq 2248**, 2026-08-08 13:17:10 local, scene `Main_Castle_Overworld`:

```
Cannot set the parent of the GameObject '[VFX_Harvest_Wood]' while activating or deactivating
the parent GameObject 'Lumbermill'.
```

This is the **WO-929** defect class, and WO-929 already names `HarvestAura.cs` among its four candidate
sites. **But every proving line in WO-929 is `OutpostEnemy (...)` — a POOLED ENEMY.** This capture proves
the same illegal `SetParent` fires from a **BUILDING**.

> ⚠ **A fix scoped to the pooled-enemy path would be INCOMPLETE.** WO-929 must cover the building path
> too, and this capture is the proving line for it.

---

## 7. WO board — corrections landed with this anchor

- **`CLI_LANES_WO_NUMBERS.md` contradicted itself.** The top reconciled-2026-08-08 header (main line next
  free, with the 931→932 bump recorded in the same edit as the 931 mint) is **correct**; the block table
  further down still carried the pre-bump value. **Only the stale table row was corrected.** The UI-seat
  row was left untouched. ⚠ **Read the number off the banner — it is not restated here, and copying it
  into a doc is exactly what caused five collisions in one day on 2026-08-02.**
- **WO-930 SHIPPED but its file still said `READY TO IMPLEMENT` / `SHIP-BLOCKING`.** Corrected, citing
  `3ab1bfb6` and `cb092b7f`.
- **WO-927** is superseded by its own §0 (root cause = stair yaw). Its status line already said so; a
  pointer to the shipping shas was added.
- **RESULT-file debt on the live arc:** none of **921 / 923 / 924 / 925 / 926 / 927 / 928 / 929 / 930 /
  931 / 1006 / 1007 / 1008 / 1009** has a `.RESULT.md`. Recorded as debt. **No RESULT file was
  fabricated** — a RESULT is written by the seat that verified the work.
- **`0d75bc06` (08:45) — a WO audit found 52 of ~91 WO statuses WRONG.** Output:
  `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. It also surfaced that **WO-884's VFX facade never
  existed**, **WO-898's `crystalsPerBracket` has 0 hits**, and **WO-875 / WO-877 were never attempted.**

---

## 8. ★ CARRIED FORWARD — still open, and the 08-08 anchor silently dropped them

Everything in this section predates 08-08 and is **not** closed. It is repeated here because a fresh
session that reads only the newest anchor loses it otherwise.

### VFX
- **The ONESHOT pool saturates 40/40** in three captures. **Different pool, different reclaim path — it
  is explicitly NOT closed by the 08-06 loop-cap fix.** Do not assume otherwise.
- **The ABSENCE of `SKIPPED - active loops 20/20` across a full wave has never been proven.** The loop-cap
  fix is owed a fleet run before it can be called verified.
- **`VFXType` serialises by ORDINAL, not name — APPENDS ONLY.** Reordering the enum silently repoints
  every authored row.
- **`Build()` does `entries.arraySize = rows.Count`**, so **a row written only by a builder is silently
  dropped by the next regenerate** — and the effect falls back to something that still looks like it works.

### Design calls waiting on the owner
- **WO-910 is READY FOR OWNER RULING** — **31 dead talent nodes across 40 player-reachable Ranger/Mage
  talents** (Ranger: **1 usable of 20**; Mage: **5**; **both tier-4 capstone rows dead**). A design call,
  not an implementation ticket.
- **Promoting `api/` to prod remains an owner call.** `api/` is deployed to **PREVIEW only** while the
  game hardcodes the prod domain, and **prod's nonce endpoint has no CORS** — so a browser blocks the
  WebGL wallet rail regardless of the client.

### Traps that bite testing
- **Hero select SELF-SKIPS when the save already records a class.** Testing a class change needs **New
  Game / Play Intro** — **never Continue.**

### Structures
- **Height cadence (owner ruling), recorded in the data as `_heightCadence`:** **1.25** landmark ·
  **1.2** towers · **1.0** building base · **0.75** siege · **0.35** decoration.
  **WALLS ARE DELIBERATELY EXCLUDED** — a uniform fit **narrows** a wall, which **opens PATHABLE GAPS in
  saved wall runs** and shrinks the navmesh obstacle with them.
  **`collector_farm` at 1.4 is a COMPENSATION** (windmill blades inflate the Y bounds), **not an
  outlier — do not "fix" it.**

### Accessibility (the owner is red/green colourblind)
- Still **colour-only and OPEN**: **the build placement ghost** (valid/invalid on the red/green axis, in
  the one mode where the player commits resources) and **the hero health bar**.

### HUD
- The bottom action bar is **SIX visible faces**, with `Upgrade` **re-pointed** to Manage/Queues.
  `HudActionBarModel.ButtonCount` stays **7** (enum identity / array bound); the number that went 7 → 6
  is **`MaxVisibleFaces`**. **`Map` stays dormant at ordinal 4 and must NEVER be renumbered** — the face
  arrays are indexed by ordinal.

---

## 9. ★ CANON CLAIMS REFUTED AT SOURCE — these are CLOSED, stop carrying them

Four long-standing canon claims were re-checked against HEAD by parallel read-only audits and **verified
false at source** by this seat before being written here. They must **not** be carried forward as open
items. Each is corrected in `KEY_FACTS.md` in place as well.

### 9.1 ✅ "THE SEAM" IS CLOSED — it no longer exists at HEAD

The 08-03 claim — *"nothing can damage a wall, gate or enemy tower; `WallSegment` + `Gate` implement
`IDamageableStructure`, `TroopController` sweeps for `IDamageable`, disjoint"* — **is now FALSE.**
WO-853 closed it from both ends:

- **Dual implementation** (verified line-by-line): `Village/Walls/WallSegment.cs:53` ·
  `Village/Gates/Gate.cs:67` · `Village/Buildings/DefenseTower.cs:57` ·
  `Village/World/Camps/RaidSpire.cs:61` — every one is
  `sealed class ... : MonoBehaviour, IDamageable, IDamageableStructure`.
- **Mask widening on BOTH troop entry points**, so a factory-supplied Enemy-only mask cannot strip it:
  `TroopController.cs:189` (`SetEnemyMask`), `:201-202` (`WithStructureLayer`), `:394` (`Awake`). Walls
  stay on the **Structure** layer *on purpose* — that layer is the tower line-of-sight blocker mask, so
  relayering them onto Enemy would make towers shoot through walls again.
- **Collider buffer raised 48 → 128** (`:104`) so wall panels in a 14 m sweep cannot crowd the enemy
  colliders out of `OverlapSphereNonAlloc`'s arbitrary-order truncation.
- Covered by `TowerWallLosRegression`, `StructureTargetableRegression:440`,
  `DefenseTargetableRegression:136`, `RaidArenaShapeRegression:363`.

> **Consequence for the roadmap:** the 08-03 line calling this *"~2-3 days, and the prerequisite under
> BOTH raid roadmaps"* is spent — **the prerequisite is SATISFIED.** That changes the shape of the
> deferred **WO-774.0 drop-and-watch vs. led** posture ruling: it was deferrable *because* the seam
> blocked both roadmaps. It no longer is. The ruling is now a live design question, not a parked one.

### 9.2 ✅ The "orphan third copy" of the gear catalogs is GONE

Canon (`CANON_GROUND_TRUTH_2026-07-22.md:193` §5.8, echoed in `CLAUDE_BEST_SUGGESTIONS_*.md:90` and
`DESIGN_FORGEMASTERS_SAGA_LEGENDARY.md:176`) describes `Assets/Data/Canonical/{weapons,armor}.json` as a
live orphan third copy still being written to. **`Assets/Data/Canonical` DOES NOT EXIST** — deleted in
`c55a5561` ("the check-in gate was auditing a directory nothing loads"). All gear tooling writes
`Assets/Resources/Data/Canonical/` and `Assets/StreamingAssets/Data/Canonical/`.

It could not have shadowed the pair anyway: `LocalJsonCatalogSource.Read` probes **only**
`Resources.Load<TextAsset>` and then `Application.streamingAssetsPath`
(`LocalJsonCatalogSource.cs:33-52`). *(The two design docs above are stale on this point and are recorded
here as such — they were deliberately not edited.)*

### 9.3 ✅ `CatalogBootstrap.RegisterFallback` drift is FIXED and now GUARDED

Canon says all three hardcoded fallback rows had drifted from the catalog (deleted `PatriciaLight/tower2`
prefab path, wrong `displayName`, missing `visualTexturePath` → pure white). **All three are now
field-equal**, including `tower_arcane_spire.visualTexturePath = "Structures/ArcaneSpire_Albedo"`
(`CatalogBootstrap.cs:307`) — **the pure-white defect is CLOSED.** It is now enforced by
`BuildEconomyRegression.cs:1191-1290`, gate 12 `[fallback-parity]`, which reflects over `RegisterFallback`
and fails on any field divergence, a missing counterpart, a zero-row registration, or the method being
renamed away.

### 9.4 ✅ Dual-copy is HEALTHY — the defect is the MISSING GATE, not current drift

Full sweep run by this seat: **80 JSON files per side, 77 paired.** **Exactly 2 drift — `weapons.json`
and `armor.json` — and both are the DELIBERATE owner gear ruling** (Resources is the curated truth and
wins at runtime; the large StreamingAssets copy is the stale side, and `DataWebRegression` exempts them
by name). The 2026-08-08 `dg_sunken_vault.json` drift is **FIXED** — both copies are version 1 / 14
rooms. All dungeon layouts and graphs are byte-identical.

### 9.5 ✅ Adaptive difficulty is NOT inert — but the real defect is narrower and WORSE-SHAPED

Canon says *"adaptive difficulty is INERT — `WaveManager` records none of the six fields, so every read
returns 1.0."* **Half false.** All six `EncounterSample` fields **are** measured and recorded:
the six-arg constructor and `DynamicDifficulty.RecordEncounter` at
`Village/Waves/WaveManager.cs:2471-2484`, armed by `BeginEncounterTelemetry` at `:2341`, and the result
consumed at `:1761-1762` and `:1876-1877` via `e.ApplyDifficulty(...)`.

**The real defect — a NEW finding, HIGH, and UNCOVERED — is in §10.1 below.**

⚠ **Namespace vs. path, both true, do not "correct" either:** the folder on disk is
**`Assets/_Modules/Core/Difficulty/`**, but every file in it declares
**`namespace DeNelle.Core.Adaptive`** (verified on all six files). The 08-03 rename moved the
**namespace** — because it shadowed the persisted enum — and left the **folder** name alone. A doc
citing `Core/Difficulty/DynamicDifficulty.cs` and a doc citing `DeNelle.Core.Adaptive` are both right.

---

## 10. NEW findings from the same audits — OPEN and UNCOVERED

### 10.1 ⚠ HIGH — three of the five difficulty multipliers have ZERO gameplay consumers

`DynamicDifficulty` produces five levers. **Only the Hp/Damage pair is consumed.**
`EnemyCountMultiplier`, `BossHpMultiplier` and `BossDamageMultiplier`
(`Core/Difficulty/DynamicDifficulty.cs:119,122,125`) have **no reader anywhere outside
`Core/Difficulty`** — verified by repo-wide sweep: the only external hits are
`DynamicDifficultyRegression.cs:276-292` and `Assets/Tests/EditMode/DynamicDifficultyTests.cs`, and
**both call `DifficultyMath.*` (the pure math), never the live `DynamicDifficulty.*` properties.**

> **So every boss wave ignores the softer boss curve the whole math file exists to produce, and the
> enemy-count signal is dead.** The system measures honestly, computes correctly, and then throws three
> fifths of its output away.

**UNCOVERED:** `DynamicDifficultyRegression` proves the **math and the oracle only** — it contains no
reference to `WaveManager` and no assertion of *consumption*. A lever can be correct and unwired and the
suite stays green. This is the §12 shape: the gate proves the part that was never broken.

### 10.2 ⚠ The data gates cannot see the copy that WINS at runtime

Both `DataWebRegression` checks iterate the **StreamingAssets root only** —
`CanonicalJsonFiles(streamingRoot)` at `DataWebRegression.cs:208` (drift) and `:356` (version). **A
Resources-only file is therefore never drift-checked and never version-checked — and Resources is the
copy that WINS at runtime.**

Verified Resources-only: **`ad-creatives.json`, `ad-placements.json`, `widget-params.json`**.
`widget-params.json` **has no `version` field at all** and is completely invisible to the gate.
*(StreamingAssets-only, for symmetry: `battle_monthly_packs.sample.json`, `skr_staking.json`,
`skr_store.json`.)*

### 10.3 ⚠ The version check cannot detect a change that skips a bump

`DataWebRegression.cs:352-398` checks **presence and cross-copy agreement only** — never *"a content
change bumps the version."* **24 catalogs had content changed with no version bump on their most recent
commit**; worst offenders `enemies.json` (+95), `en.json` (+265), `themes.json` (+369), `waves.json`,
`abilities.json`. A stale `version` therefore lies to every consumer that trusts it.

### 10.4 ⚠ The RoomForge dual-copy gate is a hardcoded 3-file list

`RoomForgeRegression.cs:162` iterates a literal
`{ "d4_sunken_crypt_spine.json", "demo_branching_kit.json", "rooms-catalog.json" }` — **no `dg_*` layout
is in it, including `dg_sunken_vault.json`, the exact file that drifted on 2026-08-08.** That drift was
caught by a human running a bake, not by this gate. **The next one ships the same way.**

---

## 11. Open, needing the owner

| # | Item | Why it needs her |
|---|---|---|
| 1 | **The WebGL / web-deploy step never ran** (`Builds/WebGL` still 08-05, no `webgl-chain-status.txt`) | Preview build is mechanical; **promotion to prod is always her call** |
| 2 | **`tools/webbot/` deleted outside git** — restorable, not restored | Deliberate or accidental is unestablished; restoring is her decision |
| 3 | **`PRIVACY_POLICY.md:87-89` — one false sentence on a LIVE page** (the Ad button it describes is gone; the no-ads claim itself is TRUE) | Live legal copy. Her call, and her attorney's |
| 4 | **Terms of Use has no arbitration / class-action / jury-trial waiver** — deliberately omitted | Attorney decision |
| 5 | **WO-931 (StubWalletProvider free-grant hole)** — READY TO IMPLEMENT; precondition 3 of 3 before `RealmStorePurchase` can ever flip on | Architecture call: build-guard / runtime refusal at the `WalletService` seam / both, left UNPICKED |
| 6 | **WO-910** — 31 dead Ranger/Mage talent nodes | Design ruling |
| 7 | **Promote `api/` to prod** (preview-only + no prod CORS blocks the WebGL wallet rail) | Deploy-to-prod is hers |
| 8 | **WO-929 must widen to the BUILDING path** (F8 seq 2248 proves it fires from `Lumbermill`, not just pooled enemies) | Scope change on a READY ticket |
| 9 | **RESULT-file debt: 921/923/924/925/926/927/928/929/930/931/1006/1007/1008/1009** | The verifying seat writes them; none fabricated here |
| 10 | **`Resources/Structures` holds a `.fbx` and a same-stem `.prefab`** — `Resources.Load` is ambiguous (noted live in `439e03ee`) | Unfixed; needs a decision on which side wins |
| 11 | **WO-774.0 drop-and-watch vs. led posture** is no longer deferrable — §9.1 closed the seam that was blocking both raid roadmaps | It was parked *because* of the seam; that reason is gone |
| 12 | **Three difficulty levers computed and thrown away** (§10.1: enemy count, boss HP, boss damage — every boss wave ignores the softer boss curve) | Wire them, or delete them and stop paying for math nothing reads — a design call |
| 13 | **Three gate holes: Resources-only files unchecked, no change-bumps-version rule, RoomForge's hardcoded 3-file list** (§10.2-10.4) | Which to close first; all three are guard work, not felt work |

---

### Data-fact correction recorded once, so it stops being re-copied

**`structures-catalog.json` is `version: 15`** — verified identical in both copies, 29 entries, top-level
keys `version` / `_heightCadence` / `notes` / `entries`, with `_heightCadence` present as canon states.
Any doc saying **v6 / v7 / v8** is a stale point-in-time reading. Per the standing rule this number is
stated **here once and nowhere else** — read it off the file, not off a doc.
