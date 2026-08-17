# PROD-004 — Standing down a baked twin hides the building and leaves its footprint

**Status:** READY TO IMPLEMENT — §3 has ONE branch the owner is checking in-editor; the fix differs a
lot between the two answers, so do not start until that is known.
**Minted:** 2026-08-17 (CLI seat) — banner bumped PROD-004 → PROD-005 in the same edit.
**Priority:** MEDIUM-HIGH — cosmetic, but it is on the home hub, it affects the LIVE build, and it
gets WORSE the more the player builds.
**Provenance:** owner, 2026-08-17, on a live Play session: the farm appeared *"on fire"* and the
barracks looked missing — then, decisively, ***"it was a bake imprint"*** / ***"wasnt a object but
the baked footprint"***.

---

## 1. What happens

Twelve structure rows are `singleton: true` with `bakedTwins` — a baked scene object that REPRESENTS
the row until the player builds their own. When the player builds one, `StructureSingleton
.StandDownBakedTwins` deactivates the twin so there are not two.

**The twin's mesh disappears. Its footprint does not.** The player is left looking at a building-shaped
imprint in the ground with nothing standing on it.

## 2. Why it is the footprint and not the building — mechanism, not inference

- ⛔ **`StructureSingleton` contains ZERO references to terrain, splat, imprint or flatten**
  (verified by grep over the whole file, 2026-08-17). It deactivates GameObjects. It has no code
  path that could remove a baked ground imprint, so it never has.
- **The save was healthy.** `state.buildingDamage` was `{}` — nothing damaged — so the "fire" was NOT
  a legitimate `StructureDamageVisuals` state (fire arms at HP ≤ 0.25). `baseLayout` listed
  `collector_farm` and `barracks` normally at level 1.
- **This explains the reset, which a cache theory cannot.** Owner: *"i reset character and let it
  rebuild and all is perfect."*
  - nothing built → twin RESURFACES → the building stands on its own footprint → looks correct
  - something built → twin STANDS DOWN → **footprint left bare** → the reported artifact
  The imprint was never removed by the reset; it got COVERED again.

> ### ⛔ THIS IS NOT A CACHE AND CLEARING ONE WILL NOT HELP
> Owner asked directly: *"does that tell us that we need to clear some cache or something on new
> build or its ok?"* — **No.** A bake imprint lives in the SCENE, not in the save and not in a cache.
> It ships inside the build, identically, to every player. And **"it went away on reset" is not
> "fixed"**: the owner could reset, but a live player with an established town cannot be told to.
> Every player who builds one of these twelve reaches this state and stays in it.

## 3. ⛔ THE ONE BRANCH — owner is checking this in-editor

Owner: *"i can load the mesh and see if thats it"*. Open `Main_Castle_Overworld` and inspect the twin
objects — **`CastleBarracks`** (barracks) and **`Windmill_Food_Storefront`** (collector_farm):

- **(a) The footprint is a SEPARATE GameObject** — a decal/plane/mesh that is a SIBLING of the twin
  rather than a child (which is why deactivating the twin misses it).
  → **CHEAP.** `StandDownBakedTwins` / `ResurfaceBakedTwins` deactivate the footprint alongside the
  twin. No re-bake, no scene edit, symmetric on both paths. Extend `bakedTwins` to name it, or match
  by a naming convention — prefer naming it in DATA so the fix stays a JSON retag.
- **(b) The footprint is TERRAIN data** — painted splat and/or flattened height baked into the
  terrain.
  → **EXPENSIVE.** A GameObject toggle cannot touch it. Either the bake stops imprinting under twin
  positions, or standdown repaints/raises that patch at runtime. ⚠ Re-baking is currently BLOCKED
  behind **WO-1049** (`BakeLayoutBatch` `populateForPlay = false` strips play content), so a
  re-bake-based fix inherits that blocker and must not be started until 1049 lands.

Everything else in this ticket is the same either way; only §4's approach changes.

> ### ⚠ FALSE LEAD, RECORDED SO IT IS NOT RE-WALKED (2026-08-17)
> During the first attempt at §3 the owner sent two screenshots of the hub with a large cyan mesh
> sheeting the ground. The CLI read it as (1) a flooded town — a water plane at the wrong height,
> "confirmed" by a green terrain island around the mill and half-submerged characters — and then, on
> the owner's *"the cyan is the mesh"*, as (2) proof the footprint was a separate GameObject, i.e.
> branch (a).
>
> **Both were wrong. The cyan was the Unity NAVMESH GIZMO**, turned on to inspect the scene: cyan is
> the navigation overlay's colour, the "island" was the carve-out around a building, and the
> "submerged" characters were simply standing under a translucent overlay. It is a debug display, not
> anything the player ever sees, and it says NOTHING about the footprint.
>
> **The lesson is the §12 one, in visual form:** a screenshot is primary evidence for what the player
> SEES, but it is not self-labelling — an editor overlay and a shipped defect can look identical, and
> a confident reading of an ambiguous image is still a guess. §3's branch stays UNRESOLVED; settle it
> by selecting the footprint object in the hierarchy with gizmos OFF, not by interpreting a picture.

## 4. Scope

1. Fix per the §3 branch, **symmetrically on standdown AND resurface** — a fix that hides the
   footprint but never restores it just trades one artifact for another the moment a player sells.
2. Cover **all twelve** rows, not the two observed. Same mechanism, same data shape:
   `pet-house`, `workshop`, `market`, `forge`, `jeweler`, `arcane-tower`, `collector_farm`,
   `collector_lumbermill`, `barracks` carry named twins; `healing_caravan`, `mill`, `armorer` are
   `singleton` with an EMPTY `bakedTwins` and must be confirmed as genuinely twin-less rather than
   assumed.
3. A `FlowTrace` line on standdown/resurface naming what was toggled AND whether a footprint was
   found — the absence of that line is why this shipped unnoticed.

## 5. Explicitly NOT in scope
- Do NOT change which rows are `singleton` or edit `bakedTwins` membership — that is the WO-834
  blank-town contract.
- Do NOT hand-edit `Main_Castle_Overworld` (CLAUDE.md §3). Scene changes go through the builder.
- Do NOT "fix" it by leaving the twin visible — two buildings for one row is the bug standdown exists
  to prevent.

## 6. VERIFICATION CHECKLIST — owner tests, CLI verifies each against evidence

| # | What to check | How it is verified | State |
|---|---|---|---|
| 1 | Build one of the twelve; **no bare footprint** where its twin stood | Owner observation / screenshot — this is a visual defect, the screenshot IS the data | ☐ |
| 2 | Sell/remove it; the twin **and its footprint** come back together | Owner observation — resurface is the half that gets forgotten | ☐ |
| 3 | Verified on an **EXISTING** save, not only a fresh one | Owner — a fresh save hides this by never standing anything down | ☐ |
| 4 | All twelve rows behave, not just barracks + farm | Trace: standdown line per row | ☐ |
| 5 | Compile gate green | `COMPILE_GATE_OK` by marker on a fresh log | ☐ |
| 6 | Regression no worse than baseline (206/210) | `DataRegression` run | ☐ |

⚠ **Box 3 is the one that matters and is easiest to skip.** A fresh save never stands a twin down, so
it reproduces nothing — testing only there would "pass" a completely unfixed build.
