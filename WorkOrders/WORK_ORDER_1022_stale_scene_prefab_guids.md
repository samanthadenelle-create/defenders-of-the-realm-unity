# WORK ORDER 1022 — `Main_Castle_Overworld` carries 56 refs to three DELETED prefab GUIDs

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-15 (UI seat) — provenance stack bumped 1022 → 1023 in the same edit
**Lane:** Scene integrity / world. ⚠ Serialization-sensitive — see §4.
**Provenance:** F8 inbox, ~48 of the 60 captures queued 2026-08-15 21:01–21:15
(`logs/f8-inbox/capture-20260815-2101*-seq2329..2388`, continuing at seq 2389–2392). Fires on **every**
scene open.
**Priority argument:** this defect generates ~4 captures per scene load. It is what buried two genuine
tutorial `STEP-STUCK` signals under 48 duplicates. Fixing it restores the F8 channel's signal-to-noise
during the owner's playtests — which §14 depends on.

---

## 1. The evidence (captured, not inferred)

Trigger line, `capture-20260815-210117-seq2330.md`:

```
Prefab instance problem: 'StorefrontCrate (Missing Prefab with guid: 15abeecb0524ef34c8f3354cd736ca8c)'.
4 instances are missing the same Prefab Asset with guid 15abeecb0524ef34c8f3354cd736ca8c.
```

Accompanied every time by:

```
Problem detected while opening the Scene file: 'Assets/Scenes/Main_Castle_Overworld.unity'.
```

**Three dead GUIDs**, measured at source 2026-08-15:

| GameObject | GUID | resolves in `Assets/**.meta`? |
|---|---|---|
| `StorefrontCrate` | `15abeecb0524ef34c8f3354cd736ca8c` | **NO** |
| `CourtyardFloor_*` | `7f2e531b6304de6458f68…` | **NO** |
| `StorefrontVine` | `fffbfdf9636e88848873b537c…` | **NO** |

```
grep -c "15abeecb0524ef34c8f3354cd736ca8c" Assets/Scenes/Main_Castle_Overworld.unity
→ 56
```

No `*Storefront*` or `*CourtyardFloor*` prefab exists anywhere under `Assets/`.

## 2. Root cause — a cleanup commit left the scene pointing at what it removed

```
git log --all -S"15abeecb0524ef34c8f3354cd736ca8c"
→ cc122e844  refactor(seam): remove dead seam infrastructure now that merged-world is live
→ b3b5cef80  feat(castle): start in MainCastle_Hall hub + OuterWorld gate wiring
```

`cc122e844` (2026-07-04, WO-608 follow-up) removed seam infrastructure it had verified was dead code.
Its own commit message documents a careful safety pass — fleet test 10 runs, compile gate, data
regression. **None of those gates read scene GUID references**, so the scene's 56 pointers to the
removed assets survived unnoticed for six weeks.

⚠ **Method note (this is why it went unseen, and it generalises):** Unity serialises asset references by
**GUID**, so a grep for the class or prefab *name* across `.unity` files finds nothing and reads as
"no problem". You must resolve the `.meta` GUID and search **that**. This is the same trap recorded on
WO-992 in `CLI_LANES_WO_NUMBERS.md`.

## 3. What to decide FIRST (do not skip — this is a design question, not a mechanical one)

Were `StorefrontCrate` / `StorefrontVine` / `CourtyardFloor` **meant** to die with the seam refactor?

- The **storefront** props are plausibly still wanted — canon §8 states the live monetization model is a
  **player-built town with movable functional storefronts + vendor NPCs**. Deleting their dressing may
  have been collateral, not intent.
- `CourtyardFloor_*` reads as castle-courtyard tiling, unrelated to the seam.

**Two valid outcomes, and they are opposite:**

| outcome | action |
|---|---|
| **(a) They were meant to die** | Strip the 56 dead references so the scene opens clean |
| **(b) They were collateral** | RESTORE the prefabs from `cc122e844^` and re-point the GUIDs |

**Read `cc122e844` in full and check whether these three prefabs are even named in its removal list**
(the message enumerates `RuntimeRegionGate.cs` and `region-gates.json` — it does **not** obviously
mention storefront or courtyard art). If they are not named, that points hard at **(b)**: they were
swept out as unreferenced assets rather than removed by intent.

**Surface the answer to the owner before executing (b).** Restoring art she deliberately cut is worse
than the errors.

## 4. ⚠ HOW to fix it — scene rules are HARD (CLAUDE.md §3)

- **NEVER hand-edit a `.unity` file.** `Village.unity`'s corruption-on-resave history is why this rule
  exists, and `Main_Castle_Overworld` is the live hub — a corrupted hub is an unbootable game.
- **Never run a bake while the Unity Editor is open** — project lock. Owner is actively playtesting;
  confirm the Editor is closed first.
- Route the repair through the **scene builder / an editor script**, not a text edit. If no builder path
  covers these objects, write a small editor fixup that resolves the missing instances through the
  Unity API and logs every object it touches.
- Consider doing the work in an **isolated worktree** — memory `dungeon-scene-shared-tree-corruption`
  records a `.unity` going NUL-corrupt when baked in the shared tree.
- After the fix, run the **NUL-byte guard** (WO-434, folded into `CompileGate`) before committing.

## 5. Two real signals this defect was burying — separate tickets, do not fix here

Surfaced while draining the queue (all 60 acked 2026-08-15, `NO_CAPTURE ack=2388`):

| capture | line |
|---|---|
| seq=2343 | `[Flow:Tutorial] STEP-STUCK :: founding_walk — no 'hero.reached:guide_gate' after 241s in-step` |
| seq=2352 | `[Flow:Tutorial] STEP-STUCK :: founding_defense — no 'build.tower_placed' after 600s in-step` |
| seq=2342 | `[Flow:RepairProbe] SURFACES scene='Main_Castle_Overworld' WallRepairController=ABSENT` |

⚠ The `founding_walk` stall may interact with **WO-993** (`PetHeroLeash` is the tutorial guide lead and
is slated for retirement). Check that ticket before diagnosing the FTUE stall independently.

## 6. Noise fix worth folding in (small, same lane)

`[Flow:MagentaGuard] hid stray MAGENTA placeholder 'Common/Levels/Basic/Cubes/Cube'` fired **16 times**
in this batch at **error** severity. The guard is *succeeding* — it hid the placeholder as designed. A
successful guard action must not log at error, because F8 captures on error and this floods the owner's
triage queue.

**Fix:** demote to `FlowTrace.Warn`. ⚠ **Do not delete the trace** — CLAUDE.md §12 (owner ruling
2026-08-09): instrumentation is permanent; flag it down, never strip it.

## 7. Acceptance criteria

- [ ] `Main_Castle_Overworld.unity` opens with **zero** "Missing Prefab with guid" errors
- [ ] `grep -c` for all three GUIDs in the scene returns **0** (outcome a) **or** all three resolve to
      real `.meta` files (outcome b)
- [ ] The chosen outcome (a or b) is **stated in the RESULT with its justification**, and (b) has
      explicit owner sign-off recorded
- [ ] Scene loads and the hub is navigable — hero spawns, navmesh intact, no missing floor
- [ ] MagentaGuard no longer logs at error severity; the trace still exists in code
- [ ] NUL-byte guard clean on the scene file

## 8. Verify

1. `COMPILE_GATE_OK` (includes the WO-434 NUL scan) — **Editor closed**
2. `REGRESSION_OK <n>/<n> suites`
3. Load `Main_Castle_Overworld` and confirm a **clean Editor.log** across the scene-open window — this
   is the actual proof; the gate markers do not read scene GUIDs (that is exactly how `cc122e844`
   passed every gate while leaving this behind)
4. `UI_CAPTURE_OK` / a hub screenshot — confirm no visual holes where the courtyard floor was
5. Owner felt-verifies + closes (§13)
