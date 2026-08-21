# WORK ORDER 1047 — A dungeon prop is registering as a HOSTILE target, and it renders as a bare orange cube

**Status:** READY - INSTRUMENTED - 2026-08-21 CLI, gate-green (COMPILE_GATE_OK + REGRESSION_OK 234/234).
**Minted:** 2026-08-17 (UI seat) — provenance stack bumped 1047 → 1048 in the same edit
**Lane:** Dungeon props + hero targeting. Disjoint from the town/UI lanes.
**Provenance:** owner 2026-08-17: *"target attaches to this item and there is a floating key next to it.
Something is broken it feels"*, with a dungeon screenshot: an untextured **orange cube** carrying a gold
shield glyph above it and a gold ground ring, with the target reticle locked to it.

---

## 1. ✅ FIRST — the floating key is NOT broken. Rule it out and move on.

`ComposedPropVisuals.BuildKey` (`:72-111`) builds it deliberately:

> *"A **floating** brass KEY: a ring… with a shaft and two bit teeth. **Spins and bobs**
> (`ComposedPropSpin`) so it reads as a pickup from across a dark room. The silhouette is the
> identifier, not the colour."*

…and it logs *"KEY body built… (**was invisible before WO-1112**)"*. So the float **is the design** —
it is a pickup tell, and it was added to fix keys being invisible.

⚠ **Do not "fix" the floating key.** Half the owner's report is working as intended, and grounding it
would re-break WO-1112's readability fix. **The real defect is the other object.**

## 2. ★ THE LIKELY DEFECT — a prop is in the HOSTILE target set

`HeroTargetIndicator` (`:9`) states its candidate rule:

> *"Each scan it gathers the **alive Hostile IDamageables**…"*

**So for the reticle to lock onto that object, something is registering it as an alive HOSTILE
IDamageable.** A chest / key / prop is not an enemy, and a reticle that locks to furniture makes the
hero's targeting untrustworthy everywhere — the player cannot tell what their attack will hit.

⚠ **Plausible mechanism, NOT confirmed:** WO-853 dual-implemented `IDamageable` +
`IDamageableStructure` across `WallSegment` / `Gate` / `DefenseTower` / `RaidSpire` and widened the
troop mask on both `TroopController` entry points. **If a dungeon prop derives from — or registers
like — a structure, it would land in that same damageable set** and inherit hostility it was never
meant to have. That is a hypothesis to test in §3, not a conclusion.

## 3. ⛔ STEP 1 — INSTRUMENT. Name the object before editing anything (§12)

The orange cube is **unidentified**. It matches **none** of the four procedural prop builders in
`ComposedPropVisuals` — `BuildKey` (cylinder+cubes), `BuildLock` (iron plate), `BuildTrapPad`
(cube/cylinder pad), `BuildOilStone` (**cylinder plinth + sphere bowl**). None produces a plain orange
cube, so it is something else: a missing-prefab placeholder, an objective marker's stand-in, or a prop
outside this file.

**Capture, in this order:**

1. **Dump the object** the reticle is locked to — full name, component list, the prefab/spawner that
   created it, and its world position. ⚠ `HeroTargetIndicator` already holds `CurrentTarget` /
   `_markerTarget`, so the object is in hand — log it.
2. **Log which interface/registration** puts it in the hostile set — `IDamageable` implementor, layer,
   tag, or registry entry.
3. **Identify the orange cube's visual owner** — is the cube a placeholder because a prefab failed to
   resolve, or is it authored? ⚠ The **gold shield glyph above it and the gold ground ring** are
   marker-shaped; check whether those belong to the same object or to a separate POI marker sitting on
   top of it.

**Record all three in the RESULT.** *"Something is broken it feels"* becomes an actionable ticket only
once the object has a name.

### ★ LEAD — a placeholder-cube sweeper ALREADY EXISTS and this one survived it

`DungeonController` calls **`SweepPlaceholderCubes()`** at `:367`, in the same hydration sequence as
`HydrateChests` / `HydrateExits`. So the project already knows placeholder cubes appear in dungeons and
already sweeps them — **and this one was not swept.**

**Read that sweeper FIRST.** It is the cheapest branch in this ticket, and it splits three ways:

1. the cube **is** a placeholder the sweep **missed** (wrong colour/name/layer filter) → widen the sweep
2. the cube is **authored** and correctly skipped → then the defect is purely §2's hostility
3. the sweep **ran too early**, before whatever spawned this object → an ordering bug in the sequence

⚠ Option 3 is worth checking closely: `SweepPlaceholderCubes` runs **after** `HydrateChests` and
`HydrateExits` but **before** `SubscribeRealtimeSettle` — anything spawned later in the run is
structurally unreachable by a one-shot sweep at hydration time.

## 4. Then fix the RIGHT one of two problems

| finding | fix |
|---|---|
| **The prop should never be hostile** | Remove it from the hostile candidate set — do **not** special-case it in `HeroTargetIndicator`. ⚠ Fix the registration at its source, or every future prop repeats it |
| **The prop is meant to be attackable** (a breakable chest?) | Then it should be **targetable but not HOSTILE** — the reticle's hostile scan is the wrong bucket, and that distinction needs to exist |

**And separately:** if the orange cube is a failed prefab resolve, that is a **missing-art** defect —
⚠ note `[Flow:MagentaProbe]` already exists for magenta placeholders (WO-1035 §4); an *orange* cube may
be a different placeholder path with **no probe covering it**. Worth asking whether the guard should
cover this case too.

## 5. Do NOT

- Do not ground or restyle the floating key (§1)
- Do not special-case this object inside `HeroTargetIndicator` (§4)
- Do not delete the `[Flow:Composed…]` prop traces — §12; the WO-1112 lines are what identified the key
- Do not re-open dungeon generation (WO-1028 §4 — closed; `dg_stair_rig` / `dg_descent_probe` are
  quarantined fixtures)

## 6. Acceptance criteria

- [ ] The object is **named in the RESULT**, with its spawner and its route into the hostile set
- [ ] The reticle no longer locks onto non-combat props anywhere in the dungeon
- [ ] The fix is at the **registration source**, not a filter patch in the indicator
- [ ] The orange cube either renders its intended art, or is confirmed authored-as-is with a reason
- [ ] ⛔ The floating key is **unchanged** — still spins and bobs (WO-1112)
- [ ] Real enemies still target normally — ⚠ a hostile-set change can silently un-target actual enemies,
      so prove combat still works rather than only proving the prop stopped being hit

## 7. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. Headless dungeon run: assert zero prop objects enter the hostile candidate set, **and** that enemies
   still do
3. Screenshot the same room — memory `screenshots-are-primary-evidence-for-visual-defects`
4. Owner felt-verifies + closes (§13)

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `HeroTargetIndicator.cs:752` — orange cube never instrumented. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **CLI 2026-08-21:** c436b858a - instrumentation ONLY, no fix, per s12: the prop is still unidentified. All three admission routes now name the object. ONE dungeon run settles it.

---

## REMAINING — named (INSTRUMENTED ≠ DONE)

| # | Hole | Evidence |
|---|---|---|
| **R1** | **Prop still unidentified** | No RESULT names the object. Instrumentation is ready; **no capture has been read yet**. |
| **R2** | **No fix at registration source** | Reticle can still lock a non-combat prop. Forbidden: filter-patch inside `HeroTargetIndicator`. |
| **R3** | **Orange cube art unresolved** | Sweep still hides only `[PLACEHOLDER]` + **near-white**. Tinted (orange) placeholders are **left alone by design** and now log why. Late spawns after hydration are unreachable by the one-shot sweep. |
| **R4** | **Combat still-works proof needed post-fix** | Non-enemy Warn lines + enemy Step lines exist; after the fix, re-prove enemies still admit. |

### What already landed (keep; do not strip — §12)

| Seam | What it logs |
|---|---|
| `HeroTargetIndicator` — 3 admit routes | `[hostile-admit]` once per object: path, implementor, owner GO, layer, components, children, mesh/shader/`_BaseColor` |
| `SweepPlaceholderCubes` census | `[cube-census]` for every primitive Cube (cap 16) + explicit SKIP for tinted placeholders |

Floating key (`ComposedPropVisuals.BuildKey`) is **not** the defect — do not ground it (WO-1112).

---

## SOLUTION — concrete close-out (research 2026-08-17)

### S0 — Earn the edit (§12): ONE dungeon run

1. Enter a dungeon; lock (or let auto-acquire) the orange cube.  
2. F8 / harvest Player.log for:
   - `[Flow:Reticle] [hostile-admit] …` (especially **NON-ENEMY** Warns)
   - `[Flow:Dungeon] [cube-census] …` / `TINTED placeholder …`
3. Write the object **name, path, implementor, admission route, rgb** into this WO’s RESULT.  
   **No code edit until that line exists.**

### S1 — Fix branch table (pick AFTER the capture)

| Finding in the log | Fix at source |
|---|---|
| `[PLACEHOLDER]` + orange tint, sweep skipped | Widen sweep to hide tinted missing-mesh boxes **or** stop tinting KayKit fallbacks orange; optional MagentaProbe-class guard for orange |
| Child collider admits ancestor `IDamageable` | Fix hierarchy / collider ownership on that prefab |
| `Faction=Hostile` on a prop/structure that should be Neutral/Friendly | Change faction on the implementor component |
| Structure-layer + Hostile from WO-853 mask | Correct layer / don’t put non-combat props on Structure+Hostile |
| Spawned after `SweepPlaceholderCubes` (`:367`) | Re-sweep on late spawn, or move sweep later in hydration |

### S2 — Prove

- Headless or traced: **zero** non-Enemy props in `_candidates` in dungeon; enemies still present.  
- Screenshot same room.  
- Floating key still spins/bobs.  
- Owner felt-close.

### Acceptance that flips INSTRUMENTED → DONE

- [ ] Object **named in RESULT** with spawner + hostile route  
- [ ] Reticle no longer locks non-combat props  
- [ ] Fix at **registration source**, not indicator filter  
- [ ] Orange cube: intended art **or** confirmed stand-in with reason  
- [ ] Floating key unchanged  
- [ ] Real enemies still target  

**Do not** mark DONE on instrumentation alone. A clean board that hides an unnamed prop is the expensive failure mode.

> ## ★ THE OBJECT IS IDENTIFIED (2026-08-21) - owner device observation + source
>
> Owner, on the Seeker: *"when i attacked an enemy it destroyed item and left a white
> pellet and i collected it"* / *"in dungeon but i had aggro and when killing eney i
> destroted it"* / *"the pellet came from that hostile marked item"*.
>
> **It is a `BreakableContainer`.** Confirmed at source, and it is not miswired - it is
> hostile ON PURPOSE:
> ```
> Assets/_Modules/Village/World/BreakableContainer.cs:65-66
> /// Containers read as Hostile so the hero's enemy-mask sweep hits them.
> public CombatFaction Faction => CombatFaction.Hostile;
> ```
> It sits on the **Enemy layer** and implements both `IDamageable` and
> `IDamageableStructure` so any existing damage path can break it. On death it rolls a
> drop - the white pellet is `IngredientPickup`'s primitive-sphere mote.
>
> **SO THE DEFECT IS NARROWER THAN THE TICKET ASSUMED.** Nothing here is a wrong faction
> or a stray collider. TWO CONCERNS SHARE ONE FLAG:
>   1. *may the hero DAMAGE this?*  - yes, and it must stay yes, or crates become unbreakable
>   2. *is this a valid TARGET to lock the reticle onto?* - no
> `HeroTargetIndicator` cannot tell them apart because `CombatFaction.Hostile` is the only
> signal it gets.
>
> **THE FIX BELONGS AT THE TARGETING SEAM, NOT ON THE CONTAINER.** Do not "fix" this by
> making containers non-hostile - that breaks the smash path the flag exists for, which is
> the same conflation in the other direction. Give the indicator a way to exclude
> destructible scenery (an interface/marker the container carries, or an explicit opt-out
> the indicator checks) and leave the damage seam untouched.
>
> ⚠ STILL OWED: the committed `[hostile-admit]` instrumentation has NOT yet run on device -
> the installed APK predates it. One dungeon run confirms this identification from the
> captured line rather than from a chain of reasoning, and would also settle whether a
> child collider or a late spawn is involved. Treat the above as a STRONG NAMED CANDIDATE
> until that line exists.

