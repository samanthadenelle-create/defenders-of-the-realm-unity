# Dungeon Read-Only Regression — 2026-07-26

**Author:** dungeon-subsystem SME pass (read-only regression).
**Scope:** every Village→Dungeon→Battle→Village path, both dungeon scenes, all
`DeNelle.Dungeons` runtime code, the two editor builders, and the canonical data.
**Method:** ground-truth verification against the working tree on branch
`claude/dungeon-readonly-regression-eql01g` (base `master` @ `30ff18b`). Nothing
was modified. Every finding below cites the file/line it was verified from.

> **Note on the earlier "SME audit" of corrupt scenes.** A prior summary reported
> two NUL-corrupted scenes (`dg_starter_loop.unity`, `Dungeon.unity`) and a
> `Main_Castle_Overworld` portal route. **None of those files exist anywhere in
> this branch** — not in the working tree, not tracked, not in any git history,
> and not in `EditorBuildSettings`. This branch's build list is 7 scenes, both
> dungeons are clean `%YAML 1.1` with **zero NUL bytes**. Either that audit ran
> against a different repo/branch or a hypothetical state. The regression below is
> against what is actually here.

---

## Build-settings ground truth

`ProjectSettings/EditorBuildSettings.asset` — 7 scenes, all clean YAML, no NULs:
Title, HeroSelect, PetSelect, Village, **Dungeon_HealersCottage**, ATBBattle,
**Dungeon_FolksGranary**. Two dungeon scenes ship; `SceneRouter` names 5 more
(`SunkenBellTower`, `WolfwardensVigil`, `FrostStair`, `GlassCathedral`,
`ApothecarysVault`) whose scenes do not exist — safe scaffold (guarded by
`DungeonDef.SceneExists` + `Application.CanStreamedLevelBeLoaded`).

---

## Findings (severity-ranked)

| # | Sev | Finding | Verified at |
|---|-----|---------|-------------|
| D1 | 🔴 P0 | **No way out of the full dungeon.** `DungeonController.ExitToVillage()` has zero callers. Healer's Cottage has no exit pad, no HUD leave button; `PauseController` (Quit→Title only) isn't in the scene. Player is trapped after entering. | `DungeonController.cs:298`; grep: 0 callers; scene has no `DungeonStubReturn`; `PauseController` count in scene = 0 |
| D2 | 🔴 P0 | **Folk's Granary is a bare stub, not a dungeon.** 134 objects, `DungeonStubReturn` exit pad, **no `DungeonController`**, no lore/checkpoints/Bryn/audio, and **no `folks-granary.json`** layout. Its lone `EncounterTrigger` is never hydrated (no controller) → dead. | scene component scan; `StreamingAssets/.../dungeons/` has only `healers-cottage.json` |
| D3 | 🔴 P0 | **Encounter battle returns to the WRONG dungeon.** `EncounterTrigger.LaunchBattle` hardcodes `ReturnScene = SceneRouter.DungeonHealersCottage`. Any non-Cottage dungeon fight dumps the player into the Cottage. | `EncounterTrigger.cs:312` |
| D4 | 🟠 P1 | **Battle victory is always assumed `true` on return.** `DungeonController.ResolvePendingEncounter()` passes `victory = true` unconditionally; a lost dungeon fight still "wins," and a lost boss is still marked `BossDefeated`. No Core-level battle-result carrier exists. | `DungeonController.cs:627`; `DungeonRuntimeState.cs:380` |
| D5 | 🟠 P1 | **Two redundant Village→Dungeon entry systems, both live.** 2 baked `DungeonPortal`s (in `Village.unity`, via `VillageSceneBuilder.SpawnDungeonPortal`) **plus** 2 runtime `DungeonEntrance` ring doorways (via `VillageController.Start → DungeonEntranceBootstrap`). ~4 doors for 2 scenes. `DungeonPortal` also auto-routes on trigger-touch with no confirm. | `Village.unity:51577,72494`; `VillageSceneBuilder.cs:1765`; `DungeonEntranceBootstrap.cs`; `DungeonPortal.cs:84` |
| D6 | 🟠 P1 | **Lore stones are completely non-functional.** Worse than "no modal": `LoreStone.Read()` is **never called by anything** (no input layer invokes it — grep: 0 callers), `ReadRequested` has **no subscriber**, and `LoreStoneModal.uxml` **does not exist**. A stone shows its proximity prompt but can never be read; the questline beat can never complete via gameplay. Triple gap: no input → no event listener → no view. | `LoreStone.cs:176` (0 callers); grep: 0 subscribers; no `*Modal*.uxml` |
| D7 | 🟡 P2 | **`DungeonPortal` latent double-prefix footgun.** Serialized default `_dungeonId = "Dungeon_HealersCottage"` + `EnterDungeon()` building `"Dungeon_" + _dungeonId` → `Dungeon_Dungeon_HealersCottage`. Current baked portals dodge it with short ids, but any new/un-Configured portal breaks. | `DungeonPortal.cs:27,117` |
| D8 | 🟡 P2 | **KayKit Dungeon Remastered pack absent** (gitignored `/Assets/Models/`) → both scenes fall back to `[PLACEHOLDER]` primitives (6 in Cottage, 7 in Granary). By-design fresh-clone trap (WO-23), but neither renders real dungeon geometry as-is. | `a82256` inventory; `.gitignore:86` |
| D9 | 🟡 P2 | **Ambient BGM silent.** `echoes-beneath-elarion.mp3` not in project; `StartAmbientAudio()` guards null and warns. | `DungeonController.cs:660`; `Assets/Audio/` empty of dungeon clips |
| D10 | 🟡 P2 | **Hero vitals are placeholder (120/60).** No dungeon hero-stat component; checkpoint heal + ATB round-trip run off `_heroBaselineHp/_Mana` constants. | `DungeonController.cs:118` |
| D11 | 🟢 P3 | **Builder overlap on Folk's Granary.** Both `FolksGranaryBuilder.cs` and `DungeonStubBuilder.cs` target `Dungeon_FolksGranary.unity`; only the former matches on-disk output. `DungeonStubBuilder` has no `[MenuItem]`. Confusing dual source-of-truth. | `a82256` inventory |
| D12 | 🟢 P3 | **No hero walk animation in dungeon.** `DungeonHero` exposes `IsMoving`/`CurrentSpeed` but no Animator blend; hero slides. Blocked on Mixamo clips (see SESSION_HANDOFF §4). | `week5-dungeon-foundation.md` |

| D13 | 🟡 P2 | **Dead feedback events — no toast layer.** `Checkpoint.ToastRequested`, `Checkpoint.Activated`, and `CraftingPedestal.ToastRequested` are invoked but have **zero subscribers** project-wide. Checkpoints heal silently; crafting completes with no confirmation. | `Checkpoint.cs:79,82,168`; `CraftingPedestal.cs:100,268` — 0 subscribers |
| D14 | 🟢 P3 | **Dead dialogue paths.** `WandererDialogue.FirstMeet[]`/`Idle[]` + their pickers are never invoked by `Bryn` (`_firstMeetFired` set but nothing surfaces the intro lines). Canon copy authored but unreachable. | `Bryn.cs:223`; `WandererDialogue.cs:49` |
| D15 | 🟢 P3 | **`DungeonRuntimeState.OnEnable` doesn't clear id/room/lists** — a stale value can momentarily read between enable and `StartRun`. Low-impact but sloppy lifecycle. | `DungeonRuntimeState.cs:518` |
| D16 | 🟢 P3 | **`Newtonsoft.Json` not in `DeNelle.Dungeons.asmdef` references** despite 3 loaders using it; relies on auto-reference. Low compile risk, shared with other modules. | `DeNelle.Dungeons.asmdef:4`; `DungeonLayout.cs`, `LoreFragments.cs`, `CraftingData.cs` |

### What actually works (so fixes don't regress it)

- **Crafting is fully wired for Healer's Cottage** — `DungeonSceneBuilder` builds pickups + pedestal + `CraftingPanel.uxml` (exists) + HUD and passes them into `DungeonController.ConfigureCrafting`. Pedestal opens on `E`, pickups auto-collect on proximity, `crafting-recipes.json` exists. (Only the completion toast is dead — D13.) `DungeonController.cs:470`, `DungeonSceneBuilder.cs:283`.
- **The ATB round-trip mechanics work** — `BattleController.ReturnAfterResult` correctly honors `BattleParams.ReturnScene` (`:417,432`); the SO handoff survives the scene reload; `OnDestroy` correctly skips `EndRun` while an encounter is pending. The only defects are *which* scene it returns to (D3) and the *assumed-victory* result (D4).
- **Lantern oil mechanic** (drain/refill/reach/flicker/tincture debuff), **Bryn** proximity bubble, **Checkpoint** crystal state-change, **DungeonHero** locomotion + camera-relative WASD + tap-to-move, and the **DungeonCameraRig** isometric follow are all functional.
- **DevPanel jump** — `DevPanelController` has a "Dungeon" button → `Dungeon_HealersCottage` (`:305,649`). A debug-only entry that bypasses the village entirely (Cottage only; no Granary button).

### Reachability map (the "is it playable end-to-end?" answer)

```
Village  ──DungeonPortal×2 (baked) ─┐
         └─DungeonEntrance×2 (ring)─┴─► Dungeon_HealersCottage ──► [NO EXIT] ✗   (D1)
                                    └─► Dungeon_FolksGranary   ──► ExitPad ✓ but it's an empty stub (D2)
Dungeon encounter ──► ATBBattle ──► returns to Dungeon_HealersCottage ALWAYS (D3), result always "win" (D4)
```

**Bottom line:** the Cottage is a rich, mostly-working dungeon you can enter but **cannot leave**, whose lore is **unreadable**, whose fights **always report victory** and **always return to the Cottage even from other dungeons**; the Granary is an **empty stub**; and the player reaches both through **two redundant door systems**. None of it is corrupt — it's wiring, content, and one missing UI panel.
