> ## RECONCILED 2026-08-08 - true status is PARTIAL
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: `DungeonDresser.cs:66-67` already encodes the cosmetic-vs-hazard product rule and names its own remaining Phase C (the hazard fire recipe) as future work. WHY IT WAS MISLABELLED: this WO file was FIRST ADDED in the very commit that implemented its shipped part - it was BORN STALE, never neglected.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 921 — Dungeon fire: stop “encased in fire that does nothing”

**Status: PARTIAL — Phase C (hazard fire recipe) outstanding** (reconciled 2026-08-08, see banner)  
**Minted:** 2026-08-07 (CLI / Grok — owner: fire is there but does nothing; start level encased in fire)  
**Silo:** Dungeons / VFX / hazards (composed Pipeline A)  
**Roles:** CLI implement + re-dress/re-bake as needed; PO felt-closes  
**Depends on:** none hard; pairs with WO-919 enclose (mood lighting)  
**Related:** WO-1001 slice 7 (`ComposedTrapHazard`); Ember Deep “fire/steam hazard” design in program doc; `DungeonDresser` torches  
**Owner proof:** combat corridor shot with large flames + Orc Berserker; felt “start encased in fire”

---

## 0. One-line truth

What looks like **dangerous fire** in composed dungeons is almost entirely **cosmetic torch props + hot point lights**. Real step-on traps **do damage** but are **invisible** (no particle telegraph) and only support `spike` | `grate` — **there is no fire trap kind**. Result: player walks through dramatic flames with zero effect, and spawn rooms feel like standing inside a bonfire.

---

## 1. Grounded cause (do not re-guess)

### A. Visible fire = dressing, not a hazard

| Piece | Where | Behavior |
|-------|--------|----------|
| Torches | `DungeonDresser` — tokens `torch_mounted`, `torch_lit`, `torch` | KayKit mesh (+ particles if on prefab). **Colliders stripped.** No damage. |
| Point light | Same — every torch gets `Light` intensity **2.0**, range **10 m** | Fills a 6 m cell; 4 corners = room looks “on fire” |
| Placement | 4 interior corners every room (unless near socket) | **Entry room included** → spawn “encased in fire” |

### B. Real hazards = invisible spikes/grates

| Piece | Where | Behavior |
|-------|--------|----------|
| `ComposedTrapHazard` | WO-1001 slice 7 | `SphereCollider` trigger → `HeroHealth.TakeDamage` + re-arm |
| Kinds | `_kind = spike \| grate` only | **No `fire` / `steam`** |
| Visual | Header promises “optional particle telegraph” | **Not implemented** — Editor gizmos only |
| Data | layout JSON `traps[]` | spike/grate damage 12–20 |

### C. Design debt

WO-1001 Ember Deep called for **steam-jet / fire** telegraphed hazards. Bake only wired spike/grate. Fire look and fire *gameplay* never met.

### D. Why “does nothing” feels broken

Player model: **if it looks like fire, it should burn or clearly be torch-light.**  
Shipped model: **looks like fire, is wall décor; real traps look like empty floor.**

---

## 2. Product rules (owner intent → binding)

1. **Cosmetic light** (wall torches): small flame, modest light — never reads as a room-filling inferno; **never damages**.  
2. **Hazard fire** (if present): clear telegraph (shape/motion, colourblind-safe), **does damage** on contact/tick, not on spawn frame spam.  
3. **Spawn room**: hero must **not** start inside hazard volume or inside a plume that looks lethal.  
4. Never ship “pretty fire you walk through for free” next to real combat — either dial it to accent or make it a real trap.

---

## 3. Scope

### Phase A — Dial cosmetic fire so spawn is not “encased” (required, fast)

`DungeonDresser.SeatProp` torch branch:

1. **Light:** intensity **2.0 → ~0.6–0.9**, range **10 → ~4–5 m** (still readable after WO-919 dark ambient).  
2. **Prefer** `torch_mounted` over `torch_lit` if `torch_lit` carries a huge particle plume (verify KayKit prefab; if plume huge, force mounted-only or scale particles down).  
3. **Entry / spawn room:** either  
   - **(A1)** fewer torches (e.g. 2 opposite corners), or  
   - **(A2)** no floor-level fire particles within **3 m** of hero seat (`PopulateForPlay` entry sample), or  
   - **(A3)** skip torch lights in the entry room only (mesh OK, no intensity bomb).  
4. Re-dress is bake-time — **re-bake composed scenes** after dresser change (or document runtime scaler if one exists — prefer bake).

### Phase B — Make real traps honest (required)

`ComposedTrapHazard`:

1. **Add runtime telegraph** (not Editor-only gizmo):  
   - `spike` → low spike/disc mesh or short particle (existing KayKit floor spike art if cheap),  
   - `grate` → grate plate / cube pad,  
   - both colourblind by **shape**.  
2. Prove damage: step on pad → HP drops; FlowTrace already logs `TRAP 'id' hit`.  
3. **Spawn safety:** if a trap offset lands within **2.5 m** of hero entry seat, **nudge trap** or **skip** with Warn (never arm under spawn feet).

### Phase C — Fire hazard kind (required if owner wants the corridor fire to mean something)

1. Extend trap `kind` with **`fire`** (and optionally **`steam`** later):  
   - Configure damage + radius from JSON (default dmg ~8–14 / tick, rearm ~0.8–1.25 s).  
   - Visual: **controlled** flame VFX (WO-890 subtlety — **not** a room plume; footprint ≈ trap radius).  
2. Author at least one fire trap in **Ember Deep** (or the dungeon owner was in) **away from entry**.  
3. **Do not** auto-convert every torch into a fire trap.

### Phase D — Clarity copy (optional, cheap)

- First time near a fire trap: short toast “Fire — stand clear” (once per run).  
- Do **not** toast for wall torches.

### Phase E — Out of scope

- WO-919 walls/ceilings (separate).  
- Hub structure burn (`StructureDamageVisuals`).  
- Rewriting lantern oil system.

---

## 4. Files (likely)

| File | Action |
|------|--------|
| `Assets/Editor/RoomForge/DungeonDresser.cs` | Torch light + entry rules + token preference |
| `Assets/_Modules/Dungeons/ComposedTrapHazard.cs` | Telegraph visuals; fire kind; spawn-safe notes |
| `Assets/Editor/RoomForge/DungeonBaker.cs` | Trap place: spawn-clearance check; pass fire kind |
| Layout JSON (e.g. `dg_ember_deep.json` dual-copy) | Optional fire trap rows |
| Re-bake composed scenes | After dresser/trap changes |

---

## 5. Acceptance

- [ ] Entry spawn does **not** look or feel “encased in lethal fire” (capture PNG of first 3 s).  
- [ ] Wall torches are **accent light** — small flame, modest pool; walk-through free.  
- [ ] Step-on traps have a **visible** pad/telegraph; walking on them **reduces HP**.  
- [ ] If fire traps exist: walking the flame volume damages; standing outside does not.  
- [ ] No trap collider under hero spawn at t=0.  
- [ ] FlowTrace: trap hits log; no silent miss when HeroHealth present.  
- [ ] `COMPILE_GATE_OK` + dungeon trap/compose regression green if present.  
- [ ] RESULT: before/after spawn shot + trap damage note.

---

## 6. RESULT

`WorkOrders/WORK_ORDER_921_dungeon_fire_cosmetic_vs_hazard.RESULT.md`
