**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_455 — Drop YarnSpinner, build our own dialogue system

**Status: READY TO IMPLEMENT** · Owner executive decision (2026-06-20)
*(WO number provisional — slot into MASTER_PIPELINES_BACKLOG.)*

## Decision
Decouple and **drop YarnSpinner entirely**; write our **own data-driven dialogue
system**, styled consistently through our **presentation layer (MVVM)**. Rationale: Yarn
has been the recurring fragility (No-node race, Stop()-teardown NRE, transactional misuse →
wrong-logic-on-click). Docs research confirmed Yarn is for *narrative*, and commands should
*trigger* UI, not *be* it — and we already own a presentation layer to style our own.

## Design — data + runtime + one styled MVVM view
- **DATA** (`Assets/Resources/Data/Canonical/dialogue/*.json`, via `CanonicalJson`): a dialogue
  is a graph of **nodes**. `node = { id, lines:[{speaker,text}], options:[{text, requires?,
  goto}], commands:[{verb,args}], condition?, next? }`. Variables/flags read from
  `GameState` / `QuestService`. WebGL-safe (Resources dual-copy).
- **RUNTIME** (`DeNelle.Core.Dialogue.DialogueRunner`, our own): walks nodes — present line →
  choose option → fire command → branch on condition. **Lifecycle WE control**:
  `Start/Advance/Choose/Stop`, cancellation-safe (walk-away stops cleanly mid-line, no race,
  no orphaned Continue). One owner; no source generator; no `<<stop>>` workarounds.
- **PRESENTATION (MVVM, canon-correct uGUI)**: `DialogueViewModel` holds all state (current
  line, speaker, options, isOpen); `DialogueView` is a dumb code-built uGUI skin using
  `ElarionUiKit` chrome — matches every other panel. View never reads game state.
- **COMMANDS**: keep the verb concept — reuse the **existing `DialogueCommandBridge` handler
  logic** (~40 verbs: camera/audio/structure/movement/HUD/combat/pets/quests) as **direct C#
  calls** from the runner (no Yarn registration, no register-once-globally constraint).
- **TRANSACTIONS LEAVE DIALOGUE**: shop / upgrade / structure menu become **direct code-built
  panels** (upgrade ✓ TKT-7; shop + structure menu next). Never authored as dialogue again.

## Migration phases (flag-gated `FeatureFlags.CustomDialogue`)
1. **Core**: data model + `DialogueRunner` + `DialogueViewModel`/`DialogueView`. Author 1–2
   nodes to prove the loop (line → option → command → stop). Unit-tested.
2. **Decouple transactions**: every Buy/Sell/Upgrade path → direct panel; remove from dialogue.
3. **Convert narrative**: `.yarn` nodes → `dialogue/*.json` (story beats, companion banter,
   intro), one area at a time, content-parity checked. Convert — never delete content.
4. **Rip Yarn**: remove `dev.yarnspinner.unity` + addons from `Packages/manifest.json`, delete
   the `.yarn` files + the Yarn-hosted `DialogueService` path + `CompanionDialoguePresenter`,
   fold remaining verbs into the new runner.

## Reconcile / reuse (don't greenfield)
- `DialogueCommandBridge` handler bodies → call directly (de-Yarn'd). `ElarionUiKit` →
  `DialogueView` styling. `CanonicalJson` → dialogue loader. `GameState`/`QuestService` →
  variables/flags/conditions. `PanelManager` → modal arbiter (dialogue registers like any panel).

## Do NOT
- Don't rip Yarn before the core + narrative conversion are proven (phased, flag-gated).
- Don't lose narrative content (convert, don't delete). Transactions never return to dialogue.

## Tests (permission gate, §2c)
- `DialogueRunner` unit tests: node walk, options, conditions, command fire, **clean Stop
  mid-line (no race)**. A converted-node golden test for content parity.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
