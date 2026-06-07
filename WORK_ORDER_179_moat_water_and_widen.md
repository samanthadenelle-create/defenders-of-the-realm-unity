# WORK ORDER 179 — Moat: Stylized Water + Widen for Defense

**Status:** READY TO IMPLEMENT
**Lane:** A (Village Scene — `Fortify.cs` BuildMoat geometry + a new water material + navmesh). Freeze 2.
**Source:** owner playtest ("water looks really bad") + design (wider moat = intentional bridges + defender distance).
**Priority:** P1 (visible eyesore + a real defensive-gameplay improvement).

## Part 1 — Widen the moat (geometry + defense)
Owner: "make the water a little wider so the bridge seems intentional — adds distance for defenders."
- **Widen the moat channel** (BuildMoat in `Fortify.cs`) so it reads as a real moat, not a sliver. The four
  stone bridges (WO-188) then span a meaningful gap = obviously intentional crossings.
- **Defensive payoff:** a wider moat that enemies CANNOT cross funnels them onto the bridges = **defended
  chokepoints** (towers cover the bridge mouths), and the gap buys defenders reaction distance + range.
- **NAVMESH (the bit that makes the funnel real):** the water surface must be **non-walkable** (carve it out
  of the navmesh / mark it not-walkable), and the **bridges are the only walkable crossings**. So enemy AI
  paths to the gates via the bridges, never through the water. Verify on the freeze-2 navmesh bake.
- **Bridge length** must match the new moat width (bridges span bank-to-bank, flush both ends).

## Part 2 — Stylized water shader (replaces the blue crystal shards)
**Target:** Final Fantasy / Warcraft / Elden Ring — beautiful but mobile-performant. URP Shader Graph (Lit).
**Node setup:**
- **Depth color:** shallow bright cyan-teal `#4ECDC4` → deep dark teal `#1A5A6B`, two-color lerp.
  **Mobile note:** drive the lerp from **vertex color / baked gradient**, NOT the URP Depth Texture, if
  possible — enabling Depth Texture adds a full-frame prepass for one small water body. Try depth-texture-OFF first.
- **Flow:** two normal maps panning opposite directions via Time→Panner; speeds **~0.03 and ~0.06 UV/sec** (slow, gentle).
- **Gentle vertex wave:** sine on position Y, amplitude **~0.06** (0.05–0.12). **Needs a subdivided moat plane**
  to show — a flat quad won't deform; or lean on the normal-map flow and keep the wave a tiny accent.
- **Edge foam:** white, soft — from a **distance-to-edge UV mask or foam texture** (cheap) rather than scene
  depth on mobile. Foam width ~0.3–0.5m at the shore.
- **Subtle specular sparkle:** Smoothness **0.9**, Metallic **0** — ensure a directional light is present so it catches.
- **NO Scene Color / refraction** (too costly on mobile — skip).
**Material:** Surface Type **Transparent**, Blending **Alpha**, Alpha Clipping **Off**.

## Acceptance
- Moat reads as real water (stylized, flowing, foam at banks), sits **below grade in the channel**, no blue crystal shards, no floating chunks.
- Moat is **wider**; the 4 stone bridges span it intentionally, flush bank-to-bank.
- **Enemies cannot cross the water — only the bridges** (navmesh excludes water, includes bridges); waves funnel across the bridge chokepoints.
- Holds mobile/WebGL frame budget (depth-texture off if avoidable; no refraction).

## Gate
Brace check on any `.cs`; green; folds into the freeze-2 village bake; commit `feat: implement WO-179 — stylized moat water + widen for defense`. Screenshot for UI validation.
