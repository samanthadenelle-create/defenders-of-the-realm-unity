# Technical-Debt Audit — Assets/_Modules (+ Assets/Editor)

**Date:** 2026-06-28 · **Scope:** READ-ONLY static audit · **Author:** CLI audit agent
**Method:** ripgrep sweeps (swallowed catches, `System.Reflection`, TODO/HACK/FIXME,
stubs/legacy markers, magenta/shader risk) + targeted header reads. No files edited.

> This is a triage ledger, not a work order. Each item below is a candidate; route the
> ones the owner accepts into a WO and instrument-before-fix per CLAUDE.md §12.

---

## Headline findings

1. **Two combat stacks coexist** — `DeNelle.BattleATB` (ATBCombatManager + BattleController,
   the flat/static legacy ATB) and `DeNelle.Village.Arena.BattleArena` (the real-time
   north-star). Memory `atb-flat-vs-overworld-animated-combat` says ATB is the abandoned
   feeling; BattleArena (WO-482) is canon. The ATB assembly is still compiled, referenced,
   and reflected-into by the HUD. Decide: retire ATB or formally scope it (dungeons only).
2. **Two arena controllers** — `ArenaMode` (async-PvP raid) vs `BattleArena` (PvE encounter).
   Intentional per owner (generalize-by-extraction pending) but both are live; the planned
   merge onto the BattleArena spine has not happened, so combat logic is duplicated.
3. **Two tower implementations** — `Tower.cs` (IDamageableStructure, full) vs `DefenseTower.cs`.
   Confirm which is canon for V1 base-defense; the other is dead weight.
4. **Reflection-as-asmdef-workaround is systemic** — HUD/bridge scripts reflect across
   assembly boundaries instead of resolving a Core interface. Violates the CLAUDE.md §10
   checklist ("no new System.Reflection in bridge scripts"). Worst offender: `AdminOverlay`
   (~25 reflective calls), then `BattleHudVisibilityManager`, `BattlePassManager`,
   `CryptoPaymentManager`, `PackStore`, `SceneRouter`, `PersistenceBridge`, `AudioBootstrap`.
5. **Magenta risk is mostly guarded** (`MagentaGuard`, `EnvironmentTreeMaterialFixer`,
   AutoPilot magenta probe) — but `AtbCombatantSwapper` still *creates* a material from a
   `Shader.Find("Standard")` fallback (renders pink under URP) at runtime.
6. **Silent `catch {}` swallows in gameplay paths** — most `catch {}` are best-effort
   optional-hardware/currency calls, but several swallow real gameplay exceptions
   (level-up listener invokes, wave reward grants) with no log, violating §12 "no silent
   failures."
7. **Monetization is stubbed end-to-end** — `WalletBridgeStub`, `JupiterSwapService` signing,
   `RewardedAdManager` ad SDK, and `AdminOverlay.OwnerWalletAddress = ""` are all
   placeholders that LogError/no-op if reached in a release build.

---

## Prioritized Top 15

| # | Location | Risk | Suggested fix | Effort | §9 Lane |
|---|----------|------|---------------|--------|---------|
| 1 | `BattleATB/` (whole assembly) vs `Village/Arena/BattleArena.cs:76` | Two live combat stacks; ATB is the abandoned flat-feeling one, still compiled + reflected-into → confusion, double-maintenance, bug surface | Owner decision: retire ATB or scope it to dungeons-only behind a flag; document the boundary in COMBAT_PIVOT_NORTHSTAR | L | Combat/AI |
| 2 | `HUD/BattleHudVisibilityManager.cs:35,214,258,429,451` | Cross-assembly reflection into WaveManager + BattleController to dodge HUD→Core asmdef; brittle string-keyed binding silently no-ops if a member renames | Add a Core `ICombatState`/`IWaveState` resolved via `CoreServices`; delete the reflection | M | Combat/AI + HUD |
| 3 | `HUD/AdminOverlay.cs:347,408,427,485,627,754` (~25 reflective hits) | Massive reflection web into EconomyService/HeroProgression/menus; any rename breaks the dev overlay silently; hardest file to keep green | Expose proper Core interfaces for the few ops it needs; keep reflection only for genuinely editor-only types | M | Monetization/Backend |
| 4 | `BattleATB/AtbCombatantSwapper.cs:647-649` | `Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")` then `new Material(sh)` — the Standard fallback renders MAGENTA in a URP build | Drop the Standard fallback; if URP/Lit is absent log a `FlowTrace.Fail` and skip the tint (let MagentaGuard not even see it) | S | VFX/Audio |
| 5 | `Village/Hero/HeroProgression.cs:177,181,192,195` | Level-up grants + `OnLevelUp`/`OnAnyLevelUp` invokes wrapped in bare `catch {}` — a throwing listener is swallowed with NO log; a broken reward/UI update vanishes silently (§12 violation) | Replace with `Guard.Try("HeroProgression", ...)` so failures hit the break-log | S | Combat/AI |
| 6 | `Village/Waves/WaveFeedbackDirector.cs:111,112,115,240` | Wave-clear currency grants (Wisdom/Glimmer) + repair surfacing in bare `catch {}` — a failed reward is invisible; players silently lose currency with no trace | Wrap in `Guard.Try`; assert-log on grant failure | S | Combat/AI |
| 7 | `Village/Buildings/Tower.cs:853-865` | Tier-perk auras/abilities (SlowAura/HealAura/FireAura/FrostNova/MagicalAffinity) are `// TODO: DEF-?? wire` no-ops — upgrade buttons charge resources for nothing | File the DEF tickets or hide the unimplemented perks until wired | M | Combat/AI |
| 8 | `Village/Buildings/DefenseTower.cs:35` vs `Tower.cs:41` | Two competing tower classes; unclear which is canon → risk of wiring the dead one | Confirm canon, mark the other `STALE:`/remove; update MASTER_CATALOG | S | Combat/AI |
| 9 | `Web3/WalletBridgeStub.cs:39-47` + `Web3/JupiterSwapService.cs:281-291` | Swap signing is a stub that `LogError`s "reached in a release build"; ships a fake signature — real payments cannot complete | Gate behind a build-time flag; block the swap UI in release until the real signer lands (tracked TODO) | M | Monetization/Backend |
| 10 | `Village/Crafting/VillageInventory.cs:111-118` | Crafting is a stub — `// TODO proper recipe lookup + ingredient check`; UI lets players "craft" without consuming/validating ingredients | Wire real recipe lookup + ingredient consume, or gate the craft button until done | M | Monetization/Backend (data) |
| 11 | `Village/Hero/EquipmentController.cs:68,181` | Weapon visualMesh/grip is hardcoded in C# with `// TODO data-driven: delete once weapons.json carries visualMesh/grip` — duplicates the offsets.json/Offset Forge intent; drifts from data | Move grip/mesh into weapons.json (Offset Forge already produces offsets.json); delete the hardcoded block | M | Combat/AI (data) |
| 12 | `Village/Monetization/RewardedAdManager.cs:96` | `// TODO integrate Unity Ads/AdMob` — rewarded-ad flow grants rewards with no real ad shown | Platform override stub is fine for dev; gate reward grant behind real ad callback before monetizing | M | Monetization/Backend |
| 13 | `HUD/AdminOverlay.cs:32` | `public const string OwnerWalletAddress = ""; // TODO(owner)` — empty owner wallet; any owner-revenue routing silently sends nowhere | Owner supplies the address (or move to config/secret); guard against empty before any transfer | S | Monetization/Backend |
| 14 | `Dungeons/DungeonStubReturn.cs` + `DungeonStubEncounter.cs` | Parallel "stub dungeon" path next to the real `DungeonController`; hero detected as "any non-static body"/Capsule placeholder — fragile, easy to mis-fire | Fold stubs into DungeonController or clearly scope+document them; they are scaffolding | M | Combat/AI |
| 15 | `Village/Arena/ArenaDefenseCatalog.cs:80-187` + `DefensePatternLibrary.cs:1` + `ArenaMode.cs:84,261` | Arena defense stats + patterns hardcoded with ~30× `// TODO data-driven: arena-defense.json`; owner thinks in data structures (memory) — this is exactly the anti-pattern | Extract to `arena-defense.json` + a catalog loader (mirror existing canonical-json pattern) | M | Monetization/Backend (data) |

---

## Lower-priority / noted (not in top 15)

- **Best-effort `catch {}` that are acceptable** (optional hardware / re-entrancy guards, keep
  but ideally narrow the exception type): `HeroImpactFeedback.cs:104,116` (gamepad rumble),
  `WaveFeedbackDirector.cs:219,246,252` (Handheld.Vibrate / rumble), `InventoryPaperDoll.cs:190-205`
  (Resources.Load sprite fallbacks), `AutoPilotInstaller.cs:67,92-136` (dev tool),
  `BreakCaptureHarness.cs` (must never throw into the log pump — intentional, documented).
- **Reflection in tests** (`Core/Tests/TestSupport.cs`, `Wallet/Tests/*`) — acceptable, test-only.
- **`ConsumableUseService.cs:14-18`** — Mana/Buff/DoT effects "recognised + logged TODO" — partial
  implementation; consumables silently do less than their tooltip implies. (S, data lane.)
- **`TutorialDirector.cs:526,572`** — WO-277 follow-up TODOs (objective text / panel pulse) left
  unwired; tutorial steps are quieter than intended. (S.)
- **`CastleVendorNpcInjector.cs:288` / many NPC injectors** — placeholder NPC art `// TODO real NPC art`;
  cosmetic, deferred to art pass per `asset-purge-deferred-to-polish-end`.
- **`OverworldEncounterSpawner.cs:232`** — large `V2 TODO` block (seam navmesh traversal) — already
  tracked in memory `v2-enemy-seam-navmesh-traversal`; V2, leave.
- **`HeroChargeVFX.cs` / `HeroImpactFeedback.cs`** — whole components stubbed waiting on a future
  `HeroCombat` to wire input-down/cast/impact; currently inert. Confirm they're still on the path.

---

## Method notes / coverage caveats

- Swallowed-catch detection used a multiline regex for short/empty catch bodies; a catch that logs
  via a *non-standard* helper could be a false positive — each top-15 catch was eyeballed.
- The reflection sweep excludes legitimate Unity `GetComponent` calls; only `System.Reflection`,
  `BindingFlags`, `GetMethod/GetField/GetProperty`, `InvokeMember` were counted.
- Magenta risk is largely *defended at runtime* (MagentaGuard + EnvironmentTreeMaterialFixer +
  AutoPilot probe) — item #4 is the one place new pink can still be *minted*.
- Did not run the project; all findings are static. Confirm canon-vs-dead (#1, #8, #14) with the
  owner before deletion.
