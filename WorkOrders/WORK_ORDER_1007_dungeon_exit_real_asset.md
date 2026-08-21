# WORK ORDER 1007 — A real dungeon EXIT asset (retire the primitive emerald archway)

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Minted:** 2026-08-08 (UI seat, owner directive) — number from `CLI_LANES_WO_NUMBERS.md` banner (UI block, bumped 1007 → 1008 in the same edit)
**Lane:** Dungeons / Art-integration. Presentation only — swap the exit's built visual for a real prop.
**Provenance:** owner felt-test 2026-08-08, verbatim: *"in dungeons this is the exit. It needs a real asset not something stupid like this. look into full assets in docs we have a collection and design something as work order."*
**Adjacent:** WO-1005 (dungeon UI cohesion — owns the **mirrored "TIX3" EXIT label** fix; do NOT duplicate it here), WO-1000/1001 (dungeon visual overhaul + deep-dungeon program), WO-869 (dungeon portal rebuild — the ENTRY portal, the sibling this must stay visually DISTINCT from).

---

## 1. The problem (from the felt-test screenshot)

The dungeon return exit is a **placeholder built from Unity primitives**: two green cubes (pillars), a
green cube lintel, a translucent green "sheet", a tall glow beam, a point light, and a billboarded ASCII
"EXIT" TextMesh. It reads as programmer-art — the owner's word was "stupid." It needs to be a **real 3D
prop** from the asset collection we already own.

*(Two things visible in the shot are NOT this WO: the mirrored **"TIX3"** text is WO-1005's job, and the
editor gizmo/handles are just the scene view. This WO is the exit's MESH.)*

---

## 2. The single swap point (verified at source 2026-08-08)

`DungeonExitInteractable.BuildVisual()`
(`Assets/_Modules/Dungeons/DungeonExitInteractable.cs:191`) builds the whole placeholder:

```csharp
AddDecor("Pillar_L", ... frame ...);   // emerald cube
AddDecor("Pillar_R", ... frame ...);   // emerald cube
AddDecor("Lintel",  ... frame ...);    // emerald cube
AddDecor("Sheet",   ... glow  ...);    // translucent sheet
BuildBeacon(glow);                     // point light + tall beam + billboard "EXIT" label
```

**This is the ONE place to change**, and changing it covers EVERY exit in the game, because all of them
route through `DungeonExitInteractable.Spawn` → `BuildVisual`:
- the composed-scene **return exit** (`DungeonExitSpawner`, line ~103),
- the rich-dungeon **normal exit** and **secret/boss back-door** (`DungeonController.cs:575/606`),
- the per-floor **extract pads** (WO-1001 slice 8).

So one prop swap fixes them all. The `_label` still varies per spawn ("Leave Dungeon" / "Secret Exit" /
"Extract (deep)") and must keep working.

---

## 3. The asset collection (verified present — KayKit Dungeon Remastered)

Kit root: `Assets/Models/KayKit/dungeon/` — a complete modular dungeon kit (docs:
`docs/kaykit-asset-catalog.md`, `docs/dungeons-3d-unity-layout-spec.md`). It shares ONE URP material so
everything themes together:

- **Shared material:** `Assets/Models/KayKit/dungeon/dungeon_texture_URP.mat`
  (guid `5b22ff1ad3f06a741bd104c130866db6`). Apply this to every swapped mesh.
- **Doorway / arch frames:** `wall_doorway.fbx`, `wall_doorway_sides.fbx`
  (guid `1beae163b317a6b48bb958eff5b4b7e3`), `wall_arched.fbx`, `wall_archedwindow_open.fbx`.
- **Stairs (ascend = leave):** `stairs_wide.fbx` (guid `e88b08e4ab8f1a24a945790ef4e47a2c`),
  `stairs_wood_decorated.fbx`, `stairs.fbx`.
- **Dressing:** `pillar_decorated.fbx`, `column.fbx`, `torch_lit.fbx` / `torch_mounted.fbx` (warm flank
  lights), `banner_*_green.fbx` (theme accents).

The pieces are authored on the kit's grid (walls ~4m tall) — CLI verifies scale/orientation at bake so
the doorway clears the ~2.6m hero arch the current primitives span.

---

## 4. THE EXIT ARCHETYPE — OWNER PICKED: Option C, freestanding decorated arch (2026-08-08)

> **BUILD OPTION C.** `wall_arched.fbx` + two `pillar_decorated.fbx` as a freestanding monument arch, with a
> green-gold glow plane filling the opening and the WO-1008 beacon light above it. Closest to the current
> silhouette (so the walk-in trigger + `ResolveExitPosition` do not move), most "portal-like," reads as a
> way out on its own with minimal surrounding architecture. Keep it emerald/green-gold — DISTINCT from the
> purple ENTRY portal (WO-869). (Rejected: A lit doorway, B stairs-up.)

All options below kept the same walk-in trigger + beacon behaviour (§5); only the mesh differs.

**Option A — Lit stone DOORWAY / portal (recommended).**
`wall_doorway_sides.fbx` as the frame, flanked by two `torch_lit.fbx`, a **green-gold glow plane** filling
the opening (keep the emerald "you may pass home" read), topped by the existing beacon light + beam. This
maps 1:1 onto today's footprint (pillars→door frame, sheet stays), so the trigger geometry and
`ResolveExitPosition` do not move. Clearest "walk INTO it" affordance — matches the walk-in trigger.

**Option B — Stairs UP to a lit landing.**
`stairs_wide.fbx` (or `stairs_wood_decorated.fbx`) climbing to a glowing threshold — "ascend to leave the
dungeon," which is thematically the strongest "way out." Slightly more layout care (the stairs need a
back wall / landing so they don't read as leading into rock), and the walk-in trigger seats at the stair
foot.

**Option C — Freestanding decorated ARCH.**
`wall_arched.fbx` + `pillar_decorated.fbx` as a monument arch with the glow plane. Closest to the current
silhouette, most "portal-like," least architectural context needed.

---

## 5. What to KEEP (behaviour is correct — only the mesh is wrong)

- **Walk-in trigger:** the root `SphereCollider` (`TriggerRadius = 2.0`) and the `ActivateRadius = 4.5`
  shared-button prompt — unchanged.
- **The beacon:** point light + tall glow beam so the exit reads over a camped mob and from the corridor
  mouth (WO-797 / F8 seq 622 discoverability fix). Retune intensity/position to sit on the new prop, but
  keep the "follow the light" cue.
- **The floating label:** keep a readable prompt cue, but **the mirrored-text fix is WO-1005's** — this WO
  must not regress it and should land after / with it. With a real, self-evident exit asset the owner may
  want the text smaller or gone; see §7 Q2.
- **Green-gold theme:** the exit stays emerald/green-gold so it is instantly distinct from the **purple
  ENTRY portal** (WO-869). Do not make the exit look like the entrance.
- **No colliders on decor:** every swapped mesh gets its colliders stripped (as `AddDecor(..., false)`
  does today) so nothing physically traps the hero in the doorway.
- **Per-spawn `_label`** ("Leave Dungeon" / "Secret Exit" / "Extract (deep)") keeps flowing through.

---

## 6. Constraints (binding)

- **Material / no magenta in build:** apply `dungeon_texture_URP.mat`; the shader must survive the build
  shader-pin (`Assets/Editor/PinShadersOnBuild.cs` / `EnsureShadersIncluded.cs`) — the same guard that
  keeps the current primitives from rendering magenta. Verify in a real player build capture, not just the
  editor.
- **Code-built, no UXML / no prefab-scene edits to curated dungeons.** `BuildVisual` instantiates the FBX
  at runtime (or from a small authored prefab under `Assets/Dungeon/`), consistent with the current
  code-built approach. Do NOT hand-edit baked dungeon `.unity` scenes (§3 scene rules).
- **Missing-asset safety:** if the mesh/material fails to resolve, `FlowTrace.Warn` and fall back to the
  current primitive arch — never a null/invisible exit (a lost exit is a softlock).
- **Instrument:** keep the `[Flow:DungeonExit]` step/warn lines; add one naming which prop variant built.
- **`UI_CAPTURE_OK` / headless screenshot:** capture the exit in a real dungeon and **open the PNG**
  (memory `headless-screenshot-verify-ui-before-build`) — compile-green never proves art reads right.

---

## 7. Acceptance criteria

- [ ] The dungeon exit renders as a real KayKit dungeon prop (owner-picked §4), not primitive cubes.
- [ ] All exit spawners inherit it (return exit, normal exit, secret back-door, extract pads) from the
      single `BuildVisual` change.
- [ ] Walk-in trigger + Interact-button prompt still fire; hero is never physically trapped by the mesh.
- [ ] The beacon (light + beam) still makes the exit findable over a camped mob and from the corridor.
- [ ] The exit remains visually DISTINCT from the purple entry portal (green-gold theme kept).
- [ ] Prop uses `dungeon_texture_URP.mat` and does NOT render magenta in a real player build.
- [ ] Graceful fallback to the primitive arch if the asset fails to resolve (Warn, never invisible).
- [ ] The label is readable and NOT mirrored (coordinate with WO-1005; do not regress it).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `UI_CAPTURE_OK` (PNG opened).

---

## 8. What NOT to touch

- **Do NOT touch the ENTRY portal** (WO-869) or the leave/routing logic (`ExitToVillage`,
  `SceneRouter.Castle`), `ResolveExitPosition`, or the trigger radii — this WO is the exit's MESH only.
- **Do NOT re-fix the mirrored label here** — that is WO-1005. Cross-reference, don't duplicate.
- **Do NOT hand-edit baked dungeon scenes** or introduce new reflection bridges.
- **Do NOT change exit placement or count** — same exits, same seats, better mesh.

---

## 9. Open questions for the owner

1. RESOLVED (owner 2026-08-08): **§4 = Option C, freestanding decorated arch.** See §4.
2. **Keep the floating "EXIT" text?** With a real, self-evident exit prop + beacon, the text may be
   redundant. Options: keep it (corrected, smaller), keep only the Interact-button prompt, or drop the
   world label entirely. Recommend: keep a small corrected label for the from-across-the-room read.

---

## 2026-08-10 - PARTIAL LANDING of the combined 957 + 1007 + 1008 lane (CLI seat, gated)

**The CODE half landed in full and is gate-green. The BAKE has not been re-run, so none of it is on
screen yet.**

Landed in `Assets/_Modules/Dungeons/DungeonExitInteractable.cs` (+349/-88):
- **WO-1008** - `Beacon_Beam` is now TRANSLUCENT and capped to world y 2.9-4.0 (`:437`), under
  `RoomForgeCanon.WallHeight = 4`, so it can no longer punch through the floor above and read as
  "a green bar rising out of the descent hole". The point light + slow pulse are unchanged and still
  carry the cue from range. Colour is deliberately UNTOUCHED (owner's call, colourblind law): the
  distinction is SHAPE and POSITION.
- **WO-1007** - the true exit builds a KayKit Option-C monument arch (`wall_arched` +
  2x `pillar_decorated`, colliders stripped), resolving Resources first then the editor kit, and on
  failure Warns and falls back to the primitive arch (`:264`, `:293-324`). A lost exit is a softlock,
  so this path never returns nothing.
- **WO-957** - TWO presentations selected by `_isTrueExit` (`:216`, `:255`). TRUE = arch + beam +
  "EXIT". FALSE = a flat translucent `Pad_Marker` disc + a small `Pad_Label`, no light, no beam, no
  "EXIT" (`BuildLeavePad`, `:371`). `DungeonBaker` passes `false` for every per-floor pad and
  passes all four args explicitly, because reflection does NOT apply C# default args. **Owner pin 1
  honoured: the pads STAY.**
- Schema v2, additive: `DungeonComposeLayout.exitRoomId` designates the ONE true exit; unset falls back
  to the entry room (the pre-multi-floor behaviour), so v1 layouts parse and behave identically.

**Completion by the committer:** the lane's session expired having written `BuildLeavePad` against two
helper methods it never extracted - the tree did not compile (`error CS0103` on `ApplyDecorMaterial`
and `BuildWorldLabel`). Both were extracted from the existing inline blocks so the pad and the true
exit now share ONE material path and ONE world-label path - a second copy is how one of them ends up
opaque again. The pinned child names `Beacon_Beam` and `Beacon_Label` are preserved
(`DungeonRoomOwnershipRegression.cs:366` finds the beam by name). One behavioural delta, an
improvement: an unresolved shader now Warns instead of silently keeping the primitive material.

**Owner pin 2 ("the word is Leave") - DATA NOW LANDED:** every shipped content layout authored
`"label": "Extract"`, which overrides the code default, so the pin had not reached the screen. All 13
extract labels across `dg_bonecrypt` / `dg_ember_deep` / `dg_sunken_vault` are now `"Leave"`, in
BOTH dual copies, verified byte-identical and parsing. The two control fixtures (`dg_descent_probe`,
`dg_stair_rig`) were deliberately left alone - they are the quarantined WO-930 control group.

**REMAINING SCOPE - why this WO stays READY:**
1. **The dungeons have NOT been re-baked.** `_isTrueExit` is a `SerializeField` defaulting TRUE and
   the pads are BAKED objects, so every already-baked `Extract_*` still deserialises as a full
   arch+beacon and still says "Extract". **Nothing above is visible until a re-bake** - and per memory
   `dungeon-scene-shared-tree-corruption` that bake belongs in an ISOLATED WORKTREE, not this shared
   tree mid-wave. That is the next mechanical step and it is the owner's call to schedule.
2. **No layout authors `exitRoomId`.** The designation mechanism exists; every layout takes the
   `entry` fallback. Behaviour is correct-by-fallback, but WHERE the one true exit sits is a design
   pick, not something to invent.
3. **The WO-957 per-layout regression was not written** ("for each converted layout, exactly ONE exit
   beacon"). Nothing asserts `_isTrueExit`, `Pad_Marker`, or a beacon count per layout.
4. `Assets/Resources/Dungeon/Exit/` does not exist, so a PLAYER build always takes the primitive-arch
   fallback (with its Warn); the editor/bake path resolves from the gitignored kit. Registered as tracked
   debt in `HudUiRegression.MissingResourceBaseline` rather than hidden.

**Gate:** `Builds/gate-settle4.log` -> `COMPILE_GATE_OK` (zero `error CS`) ·
`Builds/regression-settle3.log` -> `REGRESSION_OK 143/143 suites`.

**Owner felt-verify (after the re-bake):** dark `dg_ember_deep` - the mid-floor pads read as quiet
discs saying "Leave", exactly one arch+beam, and the beam does not stand proud of the floor above.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `DungeonExitInteractable.cs:445-469` — real kit arch shipped. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
