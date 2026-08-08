# WORK ORDER 1009 — Composed-dungeon interactables: real art + a "what / how" affordance pass

**Status:** SPEC — READY TO IMPLEMENT (assets verified present; one owner styling pick per slice noted inline)
**Minted:** 2026-08-08 (UI seat, owner directive) — number from `CLI_LANES_WO_NUMBERS.md` banner (UI block, bumped 1009 → 1010 in the same edit)
**Lane:** Dungeons / Art-integration. Presentation + a baker mesh-placement change. **No loot, key, lock, or economy LOGIC changes** — the triggers and grants already work; they are invisible or unreadable.
**Provenance:** owner felt-test 2026-08-08, three reports in one session (verbatim):
  1. *"in dungeons this is the exit. It needs a real asset not something stupid like this."* → **WO-1007** (exit mesh) + **WO-1008** (exit beacon reads as light).
  2. *"in sunken dungeon says locked but no door here."* → **slice 3** below.
  3. *"this tells me the square is something but i dont understand what the action is for it."* → **slice 1** below (the gold cube is a chest).
**Umbrella:** this WO is the coherent parent. **Slice 4 (exit) = WO-1007; the exit beacon = WO-1008.** This WO owns the other three interactables + the cross-cutting affordance rule.

---

## 1. The one root cause (RCA — all verified at source 2026-08-08)

Every interactable the composer places in a Pipeline-A dungeon is a **placeholder primitive or an invisible
trigger**, so the player cannot tell WHAT a thing is or HOW to act on it. Under the post-WO-919/1004 dark
ambient (#0a0a10) this went from "ugly" to "unreadable / looks broken."

| Interactable | What builds it | What the player sees | File:line |
|---|---|---|---|
| **Chest / breakable** | `BreakableContainer.Create` builds a `PrimitiveType.Cube` tinted gold `(0.78,0.62,0.22)` for "chest" | a **featureless gold cube** — "the square is something but I don't understand the action" | `Assets/_Modules/Village/World/BreakableContainer.cs:135-182` |
| **Key pickup** | `DungeonBaker.PlaceComposeKeys` adds ONLY a `SphereCollider` trigger + `ComposedKeyPickup` | **nothing at all** — an invisible walk-over | `Assets/Editor/RoomForge/DungeonBaker.cs:1625` ; `ComposedKeyPickup.cs` |
| **Locked door** | `DungeonBaker.PlaceComposeLocks` adds ONLY an empty GO + `ComposedLockedPort` (trigger + prompt) | **no door** — "Locked — need key" floats at an open gap | `Assets/Editor/RoomForge/DungeonBaker.cs:1653` ; `ComposedLockedPort.cs` |
| Exit *(slice 4)* | `DungeonExitInteractable.BuildVisual` — primitive emerald arch | **WO-1007** (mesh) + **WO-1008** (beacon light) | — |

**The logic is fine.** Keys grant, locks gate + warp, chests roll loot. This WO makes each one **look like
what it is** and **say how to act** — nothing about the grant/gate/loot behaviour changes.

---

## 2. The asset collection (KayKit Dungeon Remastered — all verified present 2026-08-08)

Kit root `Assets/Models/KayKit/dungeon/`. Shared URP material for all of them:
`dungeon_texture_URP.mat` (guid `5b22ff1ad3f06a741bd104c130866db6`).

- **Chests:** `chest.fbx`, `chest_gold.fbx` (guid `578843988168a2142b1ba3fc14e7defd`). Animated lid variants
  exist in the kit if an open/close is wanted later — V1 can be static + a break/open VFX.
- **Keys:** `key.fbx`, `key_gold.fbx`, `keyring.fbx`, `keyring_hanging.fbx`.
- **Locked door:** `wall_gated.fbx` (guid `335e7a9f77044ce4ebbfdba1076f9e54`, a portcullis/gate in a wall — reads
  "locked" instantly), or `wall_doorway.fbx` (guid `554c935c046d3c64891550c8c5638fac`) with a closed leaf.
- **Dressing / cues:** `torch_lit.fbx` (warm flank light), `banner_*_green.fbx`.

---

## 3. THE CROSS-CUTTING RULE (binding on every slice) — every interactable answers WHAT and HOW

The owner's confusion ("I don't understand the action") is the acceptance bar. Each placed interactable MUST:

1. **Read as WHAT it is** — a real mesh (chest looks like a chest, key like a key, locked door like a barred
   door), themed with `dungeon_texture_URP.mat` so it renders under the dark ambient and never magenta.
2. **Say HOW to act** — the existing `MobileInteractButton` prompt is the verb ("Open", "Take key",
   "Locked — need key" / "Unlock & pass"). Where there is no button proximity yet (walk-over key), add a
   small world-space verb hint. Keep it ASCII, keep it un-mirrored (billboarded toward the camera — the
   mirrored-text class is WO-1005's fix; do not regress it).
3. **Be findable** — a modest beacon (point light / soft mote) so it reads in the dark, consistent with the
   WO-1008 "beacon must be LIT, not Unlit" ruling. Do not over-light; these are room objects, not the exit.

---

## 4. Slices

### Slice 1 — Chests look like chests (the gold cube)
- **Where:** `BreakableContainer.Create` (`BreakableContainer.cs:135`). It is the SHARED breakable factory
  (village + dungeon), so upgrade it once: resolve a real mesh by `visualToken`
  ("chest" → `chest_gold.fbx`, "barrel" → a barrel mesh, "crate" → a crate mesh), parent it under the
  trigger GO, keep the existing collider + loot + break VFX. **Fall back to the current tinted cube (Warn)**
  if a mesh fails to resolve — never invisible.
- ⚠ **Cross-context caution (HP B2B):** this factory also renders village breakables. Upgrading it improves
  both, but **re-verify a village capture** so a dungeon art fix does not silently restyle town props.
- **Affordance:** the break/open prompt already fires on proximity; ensure it reads "Open" / "Break" and the
  chest carries a soft glint so it is findable in the dark.

### Slice 2 — Key pickups are visible
- **Where:** `DungeonBaker.PlaceComposeKeys` (`DungeonBaker.cs:1625`). Instantiate `key_gold.fbx` (themed,
  colliders stripped) under the key GO, gently bob/spin it (a Village `SpinBob`-style component if one
  exists; else a tiny runtime rotator), and give it a small warm mote so it reads as "take me."
- **Affordance:** it is a walk-over (OnTriggerEnter grants). Add a world-space "Take key" hint when the hero
  is near, so the player knows the glinting object is the thing the locked door wants.

### Slice 3 — Locked doors have a door (the "says locked but no door" bug)
- **Where:** `DungeonBaker.PlaceComposeLocks` (`DungeonBaker.cs:1653`). Today it seats an empty GO at `from`
  with no mesh. Instantiate a **barred door** (`wall_gated.fbx`, themed, colliders stripped so it never traps
  the hero) at the port, **oriented by `face` = `YawToward(from, to)`** so it sits IN the doorway between the
  two rooms, not floating. Parent it under the Lock GO so it travels with the component.
- **On unlock:** when `ComposedLockedPort.TryPort` succeeds (key held), **hide/slide the gate** before the
  warp so the door visibly opens — the player sees the key did something. (Keep it simple: SetActive(false)
  + a small open SFX is acceptable for V1; an animated raise is a nice-to-have.)
- **Affordance:** prompt already reads "Locked — need key" / "Unlock & pass" — now it points at a real gate.
- ⚠ **This is a BAKER change → composed dungeons must be RE-BAKED.** Per §3 scene rules + memory
  `dungeon-scene-shared-tree-corruption`, **re-bake only in an isolated worktree** (DungeonCompose scenes
  NUL-corrupt in the shared tree). List the affected layouts (`d4_sunken_crypt_spine.json` is the felt-test
  one) and re-bake all Pipeline-A scenes that carry locks.

### Slice 4 — Exit → **WO-1007** (mesh) + **WO-1008** (beacon reads as light)
Not implemented here. Cross-referenced so the pass is complete. Keep the exit green-gold and DISTINCT from
the purple entry portal (WO-869).

---

## 5. Constraints (binding)

- **Material / no magenta:** every instantiated mesh uses `dungeon_texture_URP.mat`; the shader must survive
  the build shader-pin (`Assets/Editor/PinShadersOnBuild.cs` / `EnsureShadersIncluded.cs`). Verify in a real
  player build capture (memory `headless-screenshot-verify-ui-before-build`).
- **No colliders on decorative meshes** — the interaction collider stays the existing trigger; the added art
  is visual only, so nothing physically traps the hero (especially the doorway gate).
- **Instrument (§12):** keep every `[Flow:ComposedKey]` / `[Flow:DungeonBake]` step; add one line per slice
  naming the prop instantiated (or the Warn fallback).
- **Missing-asset safety:** resolve-fail → `FlowTrace.Warn` + the current placeholder, never a null/invisible
  interactable (an invisible key or unmarked exit is a softlock/dead-end).
- **UXML does not work in builds** — any world-space hint is code-built TMP/TextMesh, billboarded, ASCII.
- **Baker changes bake in an isolated worktree** (slice 3) — never in the shared tree.

---

## 6. Acceptance criteria

- [ ] A dungeon chest renders as a real chest (not a gold cube), with a readable Open/Break prompt and a
      findable glint; loot behaviour unchanged.
- [ ] Village breakables re-verified — the shared-factory change did not restyle town props unexpectedly.
- [ ] A key pickup is a visible, findable key prop with a "Take key" cue; walk-over grant unchanged.
- [ ] A locked door renders a real barred gate IN the doorway (oriented by `face`), and it visibly
      opens/clears on unlock; the "Locked — need key" prompt now points at a real door.
- [ ] Nothing added traps the hero (no colliders on decor); no interactable renders magenta in a player build.
- [ ] Every interactable answers WHAT (real mesh) and HOW (verb prompt/hint) per §3.
- [ ] Composed dungeons carrying locks are re-baked (isolated worktree) and the scenes are clean (no NUL).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `UI_CAPTURE_OK` (PNGs opened — dungeon AND village).

---

## 7. What NOT to touch

- **Do NOT change key/lock/loot LOGIC** — grants, gating, warp, and loot tables are correct; this is art +
  affordance only.
- **Do NOT hand-edit baked dungeon `.unity` scenes** — regenerate via the baker in a worktree.
- **Do NOT touch the exit here** — WO-1007 (mesh) + WO-1008 (beacon) own it.
- **Do NOT re-fix the mirrored label** — WO-1005 owns it; just don't regress it.
- **Do NOT introduce new reflection bridges** beyond the baker's existing `FindType` pattern.

---

## 8. Open questions for the owner

1. **Chest open style (slice 1):** static chest + break/open VFX for V1, or hold for an animated-lid chest?
   Recommend static + VFX now; animate later.
2. **Locked door on unlock (slice 3):** simple SetActive(false) + SFX for V1, or an animated gate raise?
   Recommend simple now.
3. **Key styling:** `key_gold.fbx` (a single ornate key) vs `keyring.fbx` (a ring of keys). Recommend the
   single gold key — it reads 1:1 with "need key."
