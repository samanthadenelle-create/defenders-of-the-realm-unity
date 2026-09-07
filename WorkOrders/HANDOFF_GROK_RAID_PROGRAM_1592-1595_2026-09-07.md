# HANDOFF - Grok seat implements the raid program (WO-1592 spine, WO-1593, WO-1594, WO-1595)

**Dated:** 2026-09-07 (CLI lead). **Owner ruling:** "i was going to have grok take the raid ones and do
the work and hand back to you to review and commit after you approve." This file is the contract. It is
frozen by its date; the tickets are the spec.

## 0. The numbers

The pack was minted as 1588-1591 and COLLIDED with three CLI tickets minted the same minute. It is now
**1592** (program spine, SPEC), **1593** (KayKit raid bases: landscape, walls, towers with tops),
**1594** (countdown always on; three stars lit at engage, snuffed on milestones), **1595** (attacker
jobs Front / Skirmisher / Breaker + defender Hold / Hunter). The files are
`WorkOrders/WORK_ORDER_1592..1595_*.md` and their bodies are re-pointed. Use these numbers everywhere.
Never mint on the main line again: read `CLI_LANES_WO_NUMBERS.md` and mint from the UI/Grok seat's own
block (the board prints `BANNER_OK next mint - CLI: n, UI seat: m`).

## 1. What Grok is allowed to do

- **Work in his OWN git worktree / branch** off the current head of `feat/synty-art-retheme`
  (`git worktree add ../eoa-grok-raid feat/synty-art-retheme -b grok/raid-1593-1595`). Never in the
  shared tree at the repo root: five lanes and the Unity gate run there. Never commit or push to
  `feat/synty-art-retheme`; never touch `master`.
- **Edit code only inside the lanes the tickets name:**
  - 1593: the raid base scene INJECTORS / builders and KayKit prefab wiring under `Assets/_Modules/Raid*`
    and `Assets/Editor/*Raid*Builder*` (whatever builds `Village2` / the raid targets at bake time),
    plus Addressable entries for the KayKit pieces. **NEVER hand-edit a `.unity` file** (CLAUDE.md s3);
    scenes are rebuilt by the builder method, and a bake runs ONLY in his worktree, never the shared
    tree (memory `dungeon-scene-shared-tree-corruption` - the same corruption applies to any scene).
  - 1594: `RaidHudController` and the scoring PRESENTATION only. The scoring RULE (what a star means)
    stays in its Core model; the HUD reads it. Presentation never mutates the objects.
  - 1595: `TroopController` roles and the garrison brain. Enemy AI finds targets by COMPONENT, never by
    tag (the `SpawnPoint` and `HeroTarget` tags do not exist; CLAUDE.md s7).
  - Do NOT touch: `Assets/_Modules/Village/Troops/ArmyMuster*` (WO-1586 lane, landed today),
    `Assets/_Modules/Village/Enemies/Enemy.cs` reward region (WO-1590), the wallet/commerce files
    (WO-1579), `GameStateService.cs` (WO-1587), anything under `Assets/_Modules/HUD/Kit` except the raid
    HUD, `FeatureFlags.cs`, any `.asmdef`, any `.json` under `Assets/Resources/Data/Canonical` unless the
    ticket names the file (then: binary-safe edits, LF preserved, memory
    `canonical-json-edits-binary-only-verify-newlines`).
- **Instrument before fixing** (CLAUDE.md s12): `FlowTrace.Step/Warn/Fail` at every branch of the new
  flow; on any per-frame site the 4-arg `FlowTrace.Measure("Perf", "X.Update", 4f, 1f)`. Never delete an
  existing FlowTrace call.
- **Write the regression WITH the feature**, registered in `Assets/Editor/Regression/DataRegression.cs`
  inside the START/END fences with the full namespace, and prove it RED first against the old behaviour
  (state which mutation makes it fail).
- **Ask the owner, not the CLI, for the two rulings the tickets flag:** 1593 Q1 the kit pick, 1594 Q1
  the milestone time table. Record her words verbatim at the top of the ticket before building on them.
- He may run the compile gate and the regression suite IN HIS WORKTREE with his own Unity instance
  (`run-unity-method.ps1` from his worktree root). He may NOT fire Unity against `D:\eoa`.

## 2. What must come back (the handback)

One file per ticket, `WorkOrders/WORK_ORDER_159N_<slug>.RESULT.md` in his worktree, plus the branch name
and its head sha. Each RESULT carries, in this order:

1. **Owner rulings used**, verbatim, dated.
2. **Evidence of the cause / the before-state**: the FlowTrace lines or capture frames that show the old
   behaviour (a screenshot for anything visual; `Builds/ui-capture/*.png` from his worktree).
3. **Files changed with line ranges**, and the list of files deliberately NOT touched from section 1.
4. **Suite**: the regression file, the case names, and the RED proof (which mutation it catches).
5. **Markers on FRESH logs from his worktree**: `COMPILE_GATE_OK` (log path + time) and the
   `REGRESSION_OK n/n` summary line (judge by the `^REGRESSION_(OK|FAIL)` line, not the runner's PASS;
   logs are UTF-16 - read them with PowerShell `Select-String`).
6. **After-frames**: for 1593 a headless capture of a rebuilt raid base from two angles; for 1594 the HUD
   at engage (three lit), after the first milestone (two), and at the end; for 1595 a trace excerpt
   showing each role acting (Front holds, Skirmisher flanks, Breaker on a wall, defender Hunter chasing).
7. **Unproven**, stated as such. A claim without a captured line is a guess (CLAUDE.md s11B); "should
   work" is not a handback.
8. The ticket's `**Status:**` line set to `IMPLEMENTED - awaiting CLI review (branch grok/..., sha ...)`;
   never DONE, never FIXED - those are the CLI's and the owner's words.

## 3. What the CLI does with it

- Fetches the branch, diffs it by explicit path against the tickets' lanes (anything outside the lane
  is rejected, not merged), re-runs `COMPILE_GATE_OK` + `REGRESSION_OK n/n` at the merged head in the
  shared tree, opens the after-frames, then commits by explicit path with a message naming the RESULT.
  Nothing is merged on the strength of his markers alone (memory `other-seats-commit-ungated`).
- A rejected handback comes back as a comment block at the top of the RESULT, dated, with the exact
  line that failed. Fix and hand back again; do not argue it in chat.
- Board: `python tools/board_build.py` after the commit; the owner felt-tests on the Seeker and her
  Pass closes it.

## 4. Things Grok must not assume

- The raid hero is the REAL hero carried across (`RaidHeroSpawner` never existed; CLAUDE.md s8).
- Heartfire gates raids (WO-1379); training is time-only, gold only skips (WO-1387); the loadout bank
  has no raid-deploy reader today (found by the WO-1586 lane this morning) - if 1595 needs one, that is
  a ticket, not a side effect.
- Art is remote (R2) and content-hashed (CLAUDE.md s16): a new KayKit bundle that is not pushed renders
  as a capsule with no error. He does not push to R2; he lists the new addresses in the RESULT and the
  CLI runs `tools\r2-ship.ps1` after the merge.
