# PROD-004 — Standing down a baked twin hides the building and leaves its footprint

**Status:** IN PROGRESS — **the §3 branch is CLOSED** (see §3b: it is a NAVMESH CARVE, not a decal
and not terrain paint; owner confirmed *"there is some invisible footprint there"*). No owner ruling
is outstanding on this ticket.
- **Cause 1 (bake ran before the rotation) — LANDED + GATED.** `NavMeshBakeFinal` (commit `15944d9f4`):
  the bake as a standalone always-last step, `COMPILE_GATE_OK` + `NAVMESH_BAKE_OK`, scene diff one line.
- **Cause 2 (twin active at bake, deactivated at runtime) — WRITTEN, NOT YET GATED.**
  `NavMeshObstacle` carving on all nine named twins with their colliders held out of the bake, plus a
  ground-only collection volume (owner ruling: roofs/wall tops must not be walkable). ⛔ No compile
  gate, no re-bake and no regression have been run on it — the owner has been in the editor, which
  locks batchmode. **Nothing here is proven; do not treat it as shipped.**
- ✅ **OWNER WALKED IT AND SIGNED OFF 2026-08-17.** Push unblocked.
  ⚠ Recorded honestly: the sign-off is the PO's felt verification, which is the only thing that
  can close a spatial defect — but the §6 boxes were not ticked individually, so if an invisible
  footprint resurfaces on an established town, re-open here rather than treating this as disproven.
**Superseded status line, kept so the change is visible:** *"READY TO IMPLEMENT — §3 has ONE branch
the owner is checking in-editor"*. That went stale the moment §3b resolved the branch and was caught
by a read-only board review, not by the seat that made it — exactly the drift CLAUDE.md §15 exists to
prevent: the body of the ticket moved and its header did not.
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

## 3b. RESOLVED — it is a NAVMESH CARVE, and TWO causes feed it

Owner, 2026-08-17, on the navmesh overlay: ***"there is some invisible footprint there"***. That is
the tell neither §3 branch predicted — the footprint is not seen, it is **walked into**. A hole in
the navmesh where nothing stands: an invisible wall the size of a building.

**Cause 1 — the bake runs before the rotation.** Owner: *"when you originally run the script you
bake it, then you rotate the buildings, so the bake moves with the rotation"*. `CastleHubBuilder`'s
BATCH-BAKE calls `BuildNavMesh()` partway through the build and keeps going. Confirmed in data by
the new pose-at-bake log: the twins sit at **non-grid** yaws — `CastleBarracks` 284.0°,
`Jeweler_Gems_Storefront` 338.7°, `Marketplace_Monetization` 268.2°, watermill 301.8°. A carve baked
at 0° under a building standing at 284° is skewed off the building in both directions.

**Cause 2 — the twin is ACTIVE at bake time and deactivated at runtime.** The baked twin's collider
is present when the scene bakes, so it carves. `StandDownBakedTwins` then deactivates it at runtime:
the building vanishes and **the carve cannot follow**, because baked navmesh is static data. This one
is not an ordering mistake at all — no bake order fixes it, since the twin is legitimately there when
the scene is built.

> ### ⛔ THE FIX MUST COVER BOTH, AND ONE MECHANISM DOES
> A **`NavMeshObstacle` with carving** on each baked twin, with the twin EXCLUDED from the baked
> carve. An obstacle carves from the object's CURRENT transform every frame, so:
> - it is immune to Cause 1 — rotate whenever you like, the carve follows;
> - it is immune to Cause 2 — deactivate the twin and the carve disappears with it.
>
> ⚠ **Do NOT "fix" this by excluding buildings from the bake alone.** They do TWO jobs: their bases
> carve the ground, and (before the ground-only volume) their roofs added surface. Remove them from
> collection without adding the obstacle and the navmesh runs STRAIGHT THROUGH every building —
> a far worse bug than an invisible footprint, and one that looks fine until an enemy walks through
> a wall.

**Already landed (separate commit), because it is a different defect found on the way here:**
`NavMeshBakeFinal` — the bake as a standalone ALWAYS-LAST step, plus a **ground-only collection
volume** after the owner ruled that roofs and wall tops must not be walkable (the overlay showed
walkable polygons floating at roof height, which also silently re-added the upper level that
`CastleHubBuilder`'s single-level pivot had deliberately stripped). That fixes Cause 1 for anything
rotated before the bake. **It does NOT fix Cause 2**, and it does not fix runtime-rotated structures.

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
