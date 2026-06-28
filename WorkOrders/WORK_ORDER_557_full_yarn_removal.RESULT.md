# WORK ORDER 557 — Full YarnSpinner Removal — RESULT (COMPLETE)

**Status:** ✅ DONE — Yarn FULLY removed. Project compiles with NO YarnSpinner package, NO Yarn types, NO `.yarn` assets.
**Date:** 2026-06-28
**Branch:** wip/village2-and-f8-tickets (agent worktree ff-merged to tip 05d2f032 before work)
**Builds on:** WO-557 Phase 1 (pet-house node + pet-select conditions, already committed).

---

## What changed the plan: transactions are NOT dialogue (this unblocked the full rip)

The Phase-2/3 deferral in the spec assumed every live Yarn conversation had to be *migrated* — which
required heavy enablers (interpolation, expression conditions, dynamic `StructureMenu` status). Applying the
owner's two standing rules collapsed that scope:

1. **"Transactions (shop/upgrade/training menus) are NOT dialogue → direct panels"** (memory
   `drop-yarnspinner-custom-dialogue`). So the entire `StructureMenu` / `*_Upgrade` / vendor-shop Yarn graph is
   **RETIRED, not migrated** — buildings open their MVVM panels directly (the path already existed in
   `BuildingInteractable.TryPanelFor` + the upgradable short-circuit). No `CmdStructureStatus` port, no
   per-call variable seam, no interpolation needed.
2. **"Delete the dead questlines with Yarn"** (owner). The deep vendor questlines (`NPC_*.yarn`,
   `ForgemastersSaga.yarn`, `NPC_StableBonds.yarn`, lore/guidance barks) had **no live `Play()` caller** under
   the castle-hub / single-Knight pivot → **DELETED with the package**, not migrated.

That left only **three real conversations** to keep, all already model-faithful in `dialogues.json`
(`pet-house`, `SylasFirstMeeting`, `brom_intro`) plus one small new one (`CompanionMeeting`). No new
`DialogueModel` features were required.

---

## Migrated / rewired (the 3 live non-seam paths)

| Path | Before (Yarn) | After (Yarn-free) | File:change |
|---|---|---|---|
| **Intro** | `IntroSequencePlayer` hosted the Yarn prefab + `StartDialogue("Intro_Screen1")` (9 Yarn screens) | **Bespoke code-built image-slate sequence** (5 beats, ~30s, skippable) on uGUI/ElarionUiKit — see WO-561 | `Assets/_Modules/DialogueUI/IntroSequencePlayer.cs` (rewritten) |
| **Companion meeting** | prefab autostart + `Play("CompanionMeeting")` Yarn node | `DialogueService.Play("CompanionMeeting")` → custom runner; node authored in `dialogues.json` | `dialogues.json` (+ `CompanionMeetingTrigger.cs` `Current`→`IsRunning`) |
| **NPC upgrade station** | `GetComponent<Yarn.Unity.DialogueRunner>().StartDialogue(talkNode)` | direct code-built upgrade UI (`ShowUpgradeUI()`, always was the fallback) — a transaction, not dialogue | `Assets/_Modules/Village/Buildings/NPCUpgradeStation.cs` |

## Routing seam — rewritten Yarn-free

`Assets/_Modules/Village/Tutorial/DialogueService.cs` is now a thin **Yarn-free shim** forwarding every legacy
call site to `DeNelle.Core.Dialogue.DialogueService` (+ `dialogues.json`):
- `Play(id)` / `NodeExists(id)` → custom catalog lookup + custom runner.
- `PlayStructure(id,label)` → conversational structure (e.g. `pet-house`) runs as dialogue; **shoppable
  vendors open the gear-store panel directly**; everything else returns false so the caller's own panel
  mapping handles it (transactions = direct panels).
- `IsRunning` / `Stop()` → custom service. `CurrentStructureId/Name` preserved. `RegisterResetHook` now stops
  the active custom conversation (no Yarn variable storage to clear).

## Hero input-suppression — re-pointed off Yarn

`HeroLocomotion` and `HeroBodySwapper` no longer find/hook a `Yarn.Unity.DialogueRunner` (with the
fork-bomb-prone retry coroutines). They subscribe to two new **engine-wide static signals**
`DeNelle.Core.Dialogue.DialogueService.Started` / `.Ended` (added this pass) and reconcile to `IsRunning` on
hook. Simpler, race-free, no retry loop.

## Flag flipped ON

`FeatureFlags.CustomDialogue` default **OFF → ON** (`ff.customdialogue`). With Yarn gone there is no fallback
path; the custom sink + View must register.

---

## DELETED (explicit paths — reconcile as deletions)

**Yarn-typed code (.cs + .meta):**
- `Assets/_Modules/DialogueUI/IntroCommandBridge.cs`
- `Assets/_Modules/DialogueUI/CompanionDialoguePresenter.cs`
- `Assets/_Modules/DialogueUI/NPCCommandBridge.cs`
- `Assets/_Modules/Village/Tutorial/DialogueCommandBridge.cs`
- `Assets/Editor/DialogueSystemBuilder.cs`
- `Assets/Editor/DialogueAdvanceSetup.cs`
- `Assets/Editor/YarnDialogueSetup.cs`
- `Assets/Tests/EditMode/YarnCommandPrefixLintTests.cs`
- `Assets/Tests/EditMode/DialogueOptionReentrancyGuardTests.cs`

**Yarn content:**
- `Assets/Dialogue/` (entire tree: all `.yarn` + `DefendersDialogue.yarnproject` + `.meta`s)
- `Assets/Resources/Dialogue/DialogueSystem.prefab` (+ `.meta`; folder removed)

**Yarn packages:**
- `Packages/dev.yarnspinner.unity.addons.classicrpg/`
- `Packages/dev.yarnspinner.unity.addons.snaaake/`
- `Packages/dev.yarnspinner.unity.addons.textanimator/`
- `Packages/manifest.json` — removed `dev.yarnspinner.unity` git dependency line
- `Packages/packages-lock.json` — removed all 4 yarnspinner entries

## MODIFIED (explicit paths)
- `Assets/_Modules/Core/Dialogue/DialogueService.cs` (added `Started`/`Ended` events)
- `Assets/_Modules/Village/Tutorial/DialogueService.cs` (Yarn-free shim rewrite)
- `Assets/_Modules/Village/CompanionMeetingTrigger.cs` (`Current`→`IsRunning`)
- `Assets/_Modules/Village/Buildings/NPCUpgradeStation.cs` (dropped Yarn branch + `ResolveTalkNode`)
- `Assets/_Modules/Village/Hero/HeroLocomotion.cs` (Yarn hook → custom Started/Ended)
- `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` (Yarn hook → custom Started/Ended)
- `Assets/_Modules/Core/FeatureFlags.cs` (`CustomDialogue` default → true)
- `Assets/_Modules/DialogueUI/IntroSequencePlayer.cs` (rewritten — see WO-561)
- `Assets/_Modules/DialogueUI/DeNelle.DialogueUI.asmdef` (removed YarnSpinner refs, added Unity.TextMeshPro)
- `Assets/_Modules/Village/DeNelle.Village.asmdef` (removed YarnSpinner.Unity ref)
- `Assets/Resources/Data/Canonical/dialogue/dialogues.json` + StreamingAssets copy (added `CompanionMeeting`)

## VERIFICATION
- **Residual Yarn type sweep = ZERO** (`Yarn.Unity` / `using Yarn` / `YarnProject` / `RPGDialoguePresenter` /
  `IActionRegistration` / `.StartDialogue(` — only stale *comments* remain, no code).
- No `.asmdef` references YarnSpinner anymore.
- Both `dialogues.json` copies valid JSON + byte-identical; `manifest.json` + `packages-lock.json` valid JSON.
- Brace-balanced + NUL-free on every edited `.cs` (8/8 OK).
- `RegressionSuite.Case_YarnCommandPrefix` left in place (scans `.yarn` files; now always PASSES — no `.yarn`
  exists). `DialogueRunnerTests.cs` tests the CUSTOM runner — kept.

## OWNER-DECISION FLAGS (for PO)
- **CompanionMeeting UX guessed:** the old 8-node Yarn FTUE (village tour / tower / ambush beats) was
  obsolete under single-Knight; I authored a compact 2-node welcome that recruits Sylas (Ranger) and states
  the reclaim loop. Confirm this is the desired fallback when the walk-up introducer NPC is absent.
- **Vendor questlines deleted, not migrated** (NPC_*, ForgemastersSaga, StableBonds, WorldLore, guidance) —
  confirmed dead (no live caller). If any was wanted, it must be re-authored in `dialogues.json`.
- **AutoPilot dev note:** `AutoPilotDriver.PlayStructure("market"/"farm")` then checks `IsRunning` (expecting a
  Yarn conversation). Shoppable structures now open a *panel* (IsRunning stays false), so that dev-tool
  surface-detection branch may need a tweak. Not player-facing.
