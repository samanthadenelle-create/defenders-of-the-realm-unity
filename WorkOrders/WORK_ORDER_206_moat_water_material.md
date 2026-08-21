**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 179 — Moat water doesn't look like water

**Status: READY TO IMPLEMENT**
**Priority:** Medium — visual; the moat reads as solid blue blobs, not water.
**Date:** 2026-05-31
**Lane:** Architect / Art — moat water material/mesh (`BuildMoat` in `VillageSceneBuilder` + materials).
CLI; no gameplay change.
**Source:** owner playtest — *"water does not look like water."* Screenshot: the moat ring is flat,
chunky **opaque blue low-poly blobs** sitting on the grass — no water surface read (no transparency,
no reflection/ripple, no flow), looks like blue rocks.

---

## The problem
`BuildMoat` (WO-104/136) currently uses `Terrain_Plane_Water` polyperfect tiles, but they render as
**solid, opaque, faceted blue chunks** — they don't read as water: no transparency, no specular/sheen,
no ripple/flow, and they sit *on top of* the grass rather than in a channel. (The earlier note even
flagged `Terrain_Plane_Lake` was a grass-with-pond tile; the current `Terrain_Plane_Water` swap still
isn't reading as a water *surface*.)

## ⚠ GEOMETRY FIX (owner 2026-05-31): the water must go UNDER/BELOW grade — in the moat channel, not on top
Owner: *"water should go under the moat."* Current bug (verified in 2nd screenshot): the moat water is
**sitting on top of / above the ground**, piled up like a blue wave/heap on the surface — NOT recessed
into a ditch. **Fix the water's Y + the channel geometry:**
- The moat should be a **dug channel BELOW ground level** (the ground/bank dips down into the ditch), and
  the **water surface sits below grade** inside it (e.g. the moat floor/water at a negative Y, banks
  above). `BuildMoat` currently sets `waterY = -0.4` but the water reads as *raised* — so either the water
  Y isn't actually below the surrounding ground, the tiles are scaled/stacked into a heap, or there's no
  recessed channel for it to sit in.
- **Carve/lower the moat band** so the terrain forms a trench (banks higher than the water), and place the
  **flat water surface at the trench floor (below the village/exterior ground Y)** — so you look *down
  into* water in a moat, not at a blue mound on the grass.
- This is a **geometry/placement fix** (water Y + channel), separate from (and in addition to) the material
  style fix below. Do both: water below grade in a channel + the styled water material.

## ⚠ SEQUENCING (owner 2026-05-31): apply the FINISHED water style from the VFX/style pass — don't invent one
There is a **water styling coming from the VFX/style pass** (the dedicated water shader/material being
authored in the style work — e.g. Mirza Beig water VFX or a stylized water shader). **CLI should APPLY
that finished water style to the moat once it's ready — not author a separate ad-hoc one here.** So this
WO is **gated on the style/VFX water deliverable:**
- **When the styled water material/shader is done → apply it to the moat** (`BuildMoat` water tiles use
  the new material; seat it as a surface per below).
- Until then, the moat can keep a simple placeholder; the *real* fix is applying the style-pass water.
- Coordinate with whoever owns the VFX/style water so the moat consumes the **same** material the rest of
  the game's water uses (consistency — all water shares one style).

## The goal
Make the moat **read as water** by **applying the finished VFX/style-pass water material** — a stylized
water surface consistent with the game's water look (it doesn't need photoreal; it needs to *say "water"*
and match the rest of the game's water).

## What to do (CLI/designer's call on approach)
- **Use a stylized water material/shader** instead of the opaque tile mat — semi-transparent blue with a
  subtle **specular/fresnel sheen**, ideally a gentle **animated ripple/flow** (a simple URP water shader
  or a scrolling normal map; mobile-light). Even a flat semi-transparent tinted plane with a sheen reads
  as water far better than opaque blue chunks.
- **Seat it as a surface, not blobs** — a flatter water plane sitting slightly **below grade in the moat
  channel** (the ditch), with the grass/bank edge above it, so it reads as water *in* a moat, not lumps
  *on* grass. (Ties the WO-136 moat — the moat should be a dug channel the water fills.)
- **Match the palette** — the world's stylized blue; keep it readable from the top-down camera.
- If a custom water shader is too heavy for mobile, a **simpler stylized approach** (transparent tinted
  plane + a faint animated normal/uv-scroll + a light edge-foam line at the bank) is fine — prioritize the
  *read* over fidelity.

## Constraints
- Visual/material/mesh only — moat gameplay (drawbridge crossings, enemies-cross-at-gates) unchanged.
- Mobile-light (this is a phone game) — avoid expensive reflection/refraction; a stylized shader or
  transparent plane + scroll is plenty.
- If it touches `VillageSceneBuilder.BuildMoat`, single-writer architect lane (Agent 1) + editor-closed
  rebake. Brace-gate.

## Acceptance criteria
1. The moat **reads as water** at a glance — semi-transparent/sheened (and ideally gently animated), not opaque blue blobs.
2. **Water sits BELOW grade in a dug channel** — the moat is a trench (banks higher than the water surface); you look *down into* the water, NOT a blue heap stacked on top of the ground. (Owner: "water should go under the moat.")
3. Matches the low-poly palette; readable from the gameplay camera; mobile-light.
4. Moat gameplay unchanged; brace balance; editor-closed rebake if builder touched.

## Open question for owner
- **Water style** — animated stylized water shader (nicer, slightly more cost), or simple transparent
  tinted plane + scroll + edge foam (cheapest, still reads)? (Recommend the simple stylized version first;
  upgrade later.)

## Done checklist (CLAUDE.md §10)
- [ ] Moat uses a stylized water material (transparent/sheen, ideally animated) — reads as water
- [ ] Seated below grade in the moat channel, not blobs on grass; palette-matched; mobile-light
- [ ] Moat gameplay intact; brace balance; rebake (editor closed) if builder touched
- [ ] `WORK_ORDER_179_moat_water_material.RESULT.md` when complete

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
