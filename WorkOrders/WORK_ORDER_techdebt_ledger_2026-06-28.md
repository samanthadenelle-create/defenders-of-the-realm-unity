# Tech-Debt Ledger — 2026-06-28

> STALE: 2026-08-09 — this frozen ledger points at `C:\eoa\docs\audits\AUDIT_techdebt_2026-06-28.md`
> (twice). The repo root is machine-dependent (`C:\eoa` / `D:\eoa`); read it as the repo-root-relative
> `docs/audits/AUDIT_techdebt_2026-06-28.md`. Body left frozen per CLAUDE.md §15.

**Status:** SPEC — PLANNING ONLY — no code changed. Mini-specs for owner routing.
**Source audit:** `C:\eoa\docs\audits\AUDIT_techdebt_2026-06-28.md`
**Numbering:** these are ledger entries (TD-NN), not minted WOs. Promote to a real WO
(next free = 430, per `CLI_LANES_WO_NUMBERS.md`) only when the owner queues the item.

Grouped by §9 lane, sorted by risk (High → Low) within each lane. Effort = S/M/L.

---

## How to read an entry
`TD-NN | file:line | RISK | effort | lane` then **Risk**, **Proposed fix**, and any
**Owner-call** gate (canon decision needed before code moves).

---

## Cross-cutting themes (read before picking items)
- **(a) Reflection-as-asmdef-workaround is systemic** (TD-02, TD-03 + PackStore /
  CryptoPaymentManager / SceneRouter / PersistenceBridge / AudioBootstrap /
  BattlePassManager). The correct fix is **Core interfaces via `CoreServices`**, not more
  reflection. Reflection no-ops *silently on rename* — it dodges the compile gate and §12.
- **(b) Two-of-everything in combat** (ATB vs BattleArena, ArenaMode vs BattleArena,
  Tower vs DefenseTower, DungeonStub* vs DungeonController). Each needs an **owner canon
  call** to name the survivor BEFORE any deletion — do not guess which is dead.
- **(c) Magenta is well-defended at runtime** (MagentaGuard / EnvironmentTreeMaterialFixer /
  AutoPilot probe). TD-04 is the one place new pink can still be minted.
- **(d) Most bare `catch {}` are acceptable** best-effort hardware/optional-service calls
  (rumble, sprite-load fallback, BreakCaptureHarness log-pump). Only the **gameplay-path
  swallows (TD-05, TD-06)** violate §12 "no silent failures."

---

## Lane: Combat / AI
*(EnemyBrain, ATB, Tower, Hero, Arena, Dungeons — code only, no scene files. §9.)*

### TD-01 | `BattleATB/` (assembly) vs `Village/Arena/BattleArena.cs:76` | RISK: HIGH | L
- **Risk:** Two live combat stacks. The legacy flat ATB assembly is still compiled and
  reflected-into alongside the canon real-time BattleArena (combat-pivot north star = one
  real-time arena). Drift and double-maintenance; reflection bridges keep ATB artificially
  alive.
- **Proposed fix:** Owner decides — retire ATB outright, or scope it **dungeons-only behind
  a feature flag** and document the boundary in `docs/COMBAT_PIVOT_NORTHSTAR.md`.
- **Owner-call:** REQUIRED (canon: is ATB dead or dungeon-scoped?). Theme (b).

### TD-02 | `HUD/BattleHudVisibilityManager.cs:35,214,429,451` | RISK: HIGH | M
- **Risk:** Cross-assembly reflection into WaveManager / BattleController to dodge the
  HUD→Core asmdef rule (§5). Silently no-ops on any rename — breaks the battle HUD
  visibility with zero compile error and no trace.
- **Proposed fix:** Add Core `ICombatState` / `IWaveState` exposed via `CoreServices`;
  delete the reflection. Lane spans Combat/AI + HUD (Village implements, HUD consumes).
- **Owner-call:** none. Theme (a).

### TD-05 | `Village/Hero/HeroProgression.cs:177,181,192,195` | RISK: HIGH | S
- **Risk:** Level-up grants and `OnLevelUp` invokes sit in bare `catch {}`. A broken
  reward or UI subscriber is swallowed with **no log** — direct §12 violation; player
  silently loses level-up payoff.
- **Proposed fix:** Wrap each in `Guard.Try(...)` so a bad subscriber logs via
  `FlowTrace.Fail` and is skipped, never blanks the grant.
- **Owner-call:** none.

### TD-06 | `Village/Waves/WaveFeedbackDirector.cs:111,112,115,240` | RISK: HIGH | S
- **Risk:** Wave-clear currency grants in bare `catch {}`. Players **silently lose
  currency** on any throw, no trace — §12 violation on a gameplay/economy path.
- **Proposed fix:** Wrap grants in `Guard.Try(...)` with `FlowTrace` on failure.
- **Owner-call:** none.

### TD-07 | `Village/Buildings/Tower.cs:853-865` | RISK: MED | M
- **Risk:** Tier perks (Slow / Heal / Fire aura, FrostNova, MagicalAffinity) are
  `// TODO wire` no-ops. Building-upgrade tech-tree (WC3 model) **charges for perks that do
  nothing** — pay-for-nothing UX bug.
- **Proposed fix:** Either file DEF tickets to wire each perk, or **hide unwired perks** in
  the upgrade UI until implemented. Ties to WORK_ORDER_432 (building upgrade ladder).
- **Owner-call:** light (which perks are V1 vs deferred).

### TD-08 | `Village/Buildings/DefenseTower.cs:35` vs `Tower.cs:41` | RISK: MED | S
- **Risk:** Two competing tower classes. Risk of wiring/upgrading the **dead** one and
  shipping a no-op.
- **Proposed fix:** Confirm canon survivor; mark the other `STALE:` at top and schedule
  removal. Pairs with TD-07.
- **Owner-call:** REQUIRED (which Tower is canon). Theme (b).

### TD-11 | `Village/Hero/EquipmentController.cs:68,181` | RISK: MED | M
- **Risk:** Weapon `visualMesh` / grip hardcoded in C#. Drifts from `offsets.json` /
  Offset Forge intent — the exact data-vs-code split the Offset Forge tool was built to
  kill (owner thinks in data structures).
- **Proposed fix:** Move grip + mesh references to `weapons.json`; delete the hardcoded
  block; resolve via the data loader + AttachmentOffsetRegistry (WO-490 slice 2).
- **Owner-call:** none (aligns with existing data direction).

### TD-14 | `Dungeons/DungeonStubReturn.cs` + `DungeonStubEncounter.cs` | RISK: MED | M
- **Risk:** Parallel "stub dungeon" path beside the real DungeonController. Fragile
  "any non-static body = hero" heuristic; second code path to keep green.
- **Proposed fix:** Fold the stub behavior into DungeonController, or explicitly scope +
  document it as a throwaway harness behind a flag.
- **Owner-call:** light (keep stub as harness or fold in). Theme (b).

---

## Lane: VFX / Audio
*(VFXManager, AudioService — no gameplay dependencies. §9.)*

### TD-04 | `BattleATB/AtbCombatantSwapper.cs:647-649` | RISK: HIGH | S
- **Risk:** `Shader.Find("Standard")` fallback then `new Material(...)` renders **MAGENTA
  under URP** — the one place new pink can still be minted past the runtime guards.
- **Proposed fix:** Drop the Standard fallback entirely; on missing shader/material,
  `FlowTrace.Fail` and **skip the tint** rather than spawning a Standard material.
- **Owner-call:** none. Theme (c). (Note: file lives in the ATB assembly — coordinate with
  TD-01's retire/scope decision; the fix is valid regardless.)

---

## Lane: Monetization / Backend
*(Web3, store, ads, persistence, data-catalog ops — fully isolated. §9.)*

### TD-09 | `Web3/WalletBridgeStub.cs:39-47` + `Web3/JupiterSwapService.cs:281-291` | RISK: HIGH | M
- **Risk:** Swap signing is a stub that `LogError`s "reached in release" yet **ships a fake
  signature**. A release build could surface a broken/fake swap path to users.
- **Proposed fix:** Gate the stub behind a build flag; **block the swap UI in release**
  until a real signer is wired. Fail loud (not a fake sig) in dev.
- **Owner-call:** light (release gating policy).

### TD-03 | `HUD/AdminOverlay.cs:347,408,627,754` (~25 reflective hits) | RISK: MED | M
- **Risk:** Largest reflection web in the codebase (economy / progression / menus). Breaks
  silently on rename; hardest single file to keep green. Same anti-pattern as TD-02 at
  scale.
- **Proposed fix:** Expose the specific ops it needs as Core interfaces via `CoreServices`;
  replace reflection call-sites. Can be done incrementally (one subsystem at a time).
- **Owner-call:** none. Theme (a).

### TD-10 | `Village/Crafting/VillageInventory.cs:111-118` | RISK: MED | M
- **Risk:** Crafting is a stub — no recipe lookup, no ingredient check. "Craft" succeeds
  **without consuming** ingredients — economy exploit.
- **Proposed fix:** Wire real recipe consume (recipes.json lookup + ingredient debit), or
  gate the craft button until implemented.
- **Owner-call:** light (V1 crafting scope).

### TD-12 | `Village/Monetization/RewardedAdManager.cs:96` | RISK: MED | M
- **Risk:** Rewarded-ad `TODO` — **grants the reward with no real ad shown**. Free reward;
  no revenue; broken when monetization goes live.
- **Proposed fix:** Gate the reward behind a real ad-completed callback before enabling
  monetization. Dev/test path can keep an instant-grant behind a flag.
- **Owner-call:** light (ad SDK choice / timing).

### TD-15 | `Village/Arena/ArenaDefenseCatalog.cs:80-187` + `DefensePatternLibrary.cs:1` | RISK: MED | M
- **Risk:** ~30× `// TODO data-driven: arena-defense.json` — hardcoded stats. The exact
  anti-pattern the owner rejects (systems as data, not control flow).
- **Proposed fix:** Extract to `arena-defense.json` + a loader; thin runtime interpreter
  over the table. Pair with FlowTrace per the data-modeling memory.
- **Owner-call:** none (aligns with data direction). (Data sub-lane.)

### TD-13 | `HUD/AdminOverlay.cs:32` | RISK: LOW | S
- **Risk:** `OwnerWalletAddress = ""` — owner revenue routes nowhere; an empty-address
  transfer could silently drop funds.
- **Proposed fix:** Supply via config/secret; guard against empty before any transfer
  (fail loud, block the action).
- **Owner-call:** REQUIRED (provide the address/config source).

---

## Lower-priority / watch list (from full audit)
Reflection-bridge cluster beyond TD-02/03: **PackStore, CryptoPaymentManager, SceneRouter,
PersistenceBridge, AudioBootstrap, BattlePassManager** — same Core-interface remedy as
theme (a); batch them once the interface seam exists.
Acceptable bare `catch {}` (best-effort, do NOT "fix"): rumble, sprite-load fallback,
BreakCaptureHarness log-pump — per theme (d).

See `C:\eoa\docs\audits\AUDIT_techdebt_2026-06-28.md` for method notes + the full ledger.

---

## Routing summary (counts)
- **Combat / AI:** TD-01, 02, 05, 06, 07, 08, 11, 14 (8) — 3 High, 4 Med, owner-calls on 01/08.
- **VFX / Audio:** TD-04 (1) — 1 High, S.
- **Monetization / Backend:** TD-03, 09, 10, 12, 13, 15 (6) — 1 High, 4 Med, 1 Low; owner-call on 13.

**Quick wins (S, no owner-call, ship first):** TD-04, TD-05, TD-06.
**Owner-gated (canon call before code):** TD-01, TD-08 (theme b), TD-13 (config).
