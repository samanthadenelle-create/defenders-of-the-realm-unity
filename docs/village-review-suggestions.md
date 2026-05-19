# Avalon Village — Creative Review & Tweak Suggestions

**Reviewer:** Art Direction pass
**Date:** 2026-05-19
**Scope:** Review-only. Source studied — `screenshot-village-week3.png`, `screenshot-village-elarion.png`, `screenshot-village-week3-exterior.png`, `avalon-village-layout-spec.md`, `VillageSceneBuilder.cs`, `unity-decisions.md` (Week 3/4 + Flags).
**How to use this doc:** Each item is an approvable checklist entry. Tick the ones you want; leave the rest. Effort tags — `[quick]` ~minutes of builder tweaking, `[medium]` ~a focused builder/asset session, `[larger]` a real chunk of work or new asset wiring.

---

## Already-known issues — NOT re-reported here

Per `unity-decisions.md` these are tracked and being handled; I am not spending suggestions on them:

- The white-render of the Elarion tree + Keeper's Keep centerpiece (atlas/shader-variant quirk — needs the interactive Editor pass).
- The black/unlit exterior Terrain and the orange-void sky (Task #14, exterior polish, owner-flagged "finetune later").
- The over-bright `ForceFieldShimmer` gate plane (real shimmer shader lands Week 4).

Everything below is a *genuinely new* creative observation. The headline finding: the layout is structurally correct and faithful to the spec — four gates, two centerpieces, the plaza, the five buildings, the quadrant dressing are all there — but the interior currently reads as **scattered objects on a wide empty lawn**. The spec's promise is "lived-in fairy-tale… the Folk live here." The biggest wins are about *density, clustering, and contrast* — making the empty grass feel intentional and making the town feel inhabited.

---

## A. Composition & interior layout — highest impact

- [ ] **Tighten the building spacing — kill the "game-board" gaps** *(interior layout)* — In the render, the five gameplay buildings and the dressing buildings sit isolated with large grass voids between them. Pull the SW residential cluster ~20% closer together, and pull the market/tavern/townhall in toward the plaza edge so they visibly *front* the plaza rather than float near it. The spec explicitly warns against "a game-board with five oddly-isolated structures" (§6). Closer grouping reads as a real town. `[medium]`

- [ ] **Activate the NE quadrant — it's the emptiest corner** *(interior layout)* — The render shows a wide blank lawn between the Crystal Mine, the Workshop and the north wall. Add a tight knot of 2-3 extra `building_home_A/B` here as a small "artisan's row" behind the blacksmith, plus a woodpile/cart prop cluster. The spec already frames NE as the artisan district (§6.3); right now it's the most under-dressed quadrant. `[medium]`

- [ ] **Break up the empty NW lawn with a quarry-yard motif** *(props & dressing)* — The Crystal Mine sits alone in a sea of grass. Cluster 4-6 `resource_stone`/`rock_single` props and a couple of `barrel`/cart props into a worked "ore yard" beside the mine, with a short `hex_dirt_path` worn from mine to plaza. This makes the mine feel operational and gives the NW corner a reason to exist. `[quick]`

- [ ] **Cluster a market scene into the plaza, not just buildings around it** *(props & dressing)* — The plaza currently reads as a flat empty paved patch with the Heart and Keep on it. Add a small market scene on the plaza's SE quadrant — 3-4 stall/cart props (`barrel`, `resource_*`, crates), a couple of `haybale`s, the `weaponrack` — clustered like a market day. This is the single fastest way to make the centre feel populated. `[quick]`

- [ ] **Make the crop field look farmed, not regimented** *(props & dressing)* — The east-side crop block reads as one solid green rectangle of identical rows — too uniform, almost like a texture error. Vary it: stagger row lengths, leave 1-2 fallow dirt strips, vary scale slightly per plant, and curve the field edge to follow the orchard rather than forming a hard rectangle. Farmland in fairy-tale art is irregular and hand-tended. `[medium]`

- [ ] **Add foot-worn dirt paths between buildings** *(interior layout)* — Right now only the formal stone `+` cross exists; every building is reached across open grass. The spec calls for `hex_dirt_path` foot-traffic-worn routes (§6.1, §7.2) — branch narrow dirt paths from the main cross to each of the five building plots and through the residential cluster. Paths tell the eye where the Folk walk and instantly read as "lived-in." `[medium]`

---

## B. Color & atmosphere

- [ ] **Lift the plaza paving so it reads distinct from grass** *(color & atmosphere)* — In the render the plaza stone (`hex_road_B`) barely separates from the surrounding lawn — the ceremonial heart of the town visually disappears. The spec wants the plaza "slightly lighter than the road stone, marking it as ceremonial space" (§7.3). Warm the plaza tiles to a lighter sandy-stone and keep the radiating roads a cooler grey, so the plaza visibly glows as the centre. `[quick]`

- [ ] **Warm up the grass — it currently reads olive/drab** *(color & atmosphere)* — The interior ground is a flat yellow-green olive across the whole render, which fights the "warm fairy-tale" register the spec asks for (§1, §9.5 "soft dawn… fairy-tale just-awake"). Shift the grass toward a warmer, slightly more saturated spring-green and introduce 2-3 subtle tonal variants (mossy darker patches near walls, lighter worn patches near paths) so the ground isn't one uniform sheet. `[quick]`

- [ ] **Recolor the curtain wall toward warm mossy stone** *(color & atmosphere)* — The wall in the render reads as a flat dull pink-grey. The spec's intended Stone tier is "mossy stone" and the Warded tier is "warm aged-stone" (§4.4). Tint the wall toward a warmer aged grey with a hint of moss-green at the base — it will read as old defended stone rather than plastic, and ties the wall to the lived-in mood. `[quick]`

- [ ] **Give Elarion's mound a clear silhouette** *(color & atmosphere)* — The raised mound under the world-tree barely reads in the close-up; it blends into the plaza. Make the mound a touch taller and tint it a distinctly mossier green than the plaza/grass, so the Heart visibly sits *enthroned* on its own sacred ground. `[quick]`

---

## C. Storytelling detail — making Avalon a defended home

- [ ] **Dress the gates as guarded thresholds** *(storytelling detail)* — The four gates currently read as plain wall openings. Each gate is canon — the only way in, and the four points the Hollow Ones attack (§4.3). Flank each gate's *interior* side with a pair of props that say "watched": a `weaponrack`, a `barrel` or two, a banner, maybe a brazier-stand. Small touch, big narrative payoff — the town reads as actively defended, not decorative. `[quick]`

- [ ] **Plant violet Avalon banners along the approach to the Heart** *(storytelling detail)* — Only the Keep has a banner. Line the N-S spine (plaza → north gate) with 2-3 recolored violet `flag` props. It draws the eye down the ceremonial axis toward Elarion and reinforces the "By lantern. By oath. By Heart." identity across the whole town, not just one corner. `[quick]`

- [ ] **Add a wayside lantern motif along the main cross** *(storytelling detail / lighting)* — The tagline is literally "By lantern." Scatter a handful of small lantern/torch-post props down the N-S and E-W roads, ideally with a faint warm emissive. Even unlit they reinforce canon; lit, they pre-stage the dawn/dusk mood and give the night-defence fantasy a foothold. `[medium]`

- [ ] **Tell a small story in the residential cluster** *(storytelling detail)* — Right now the SW homes are just placed buildings + a well. Add the lived-in beats the spec implies (§6.1): a garden plot or two with `fence_wood_short`, a `haybale` or cart by a house, a couple of barrels at the well. A village quarter should look *used* — washing-line-level detail without needing new assets. `[quick]`

- [ ] **Strengthen the standing-stone ring around Elarion** *(props & dressing)* — In the close-up the six stones read as small pale lumps, easily missed. Scale them up noticeably and stand them more upright/menhir-like so the ring reads as a deliberate sacred circle, not scattered rocks. Consider a faint violet emissive rune-tint at the base of each to echo Elarion's veins and the gate force-fields — one consistent "Heart-magic" color language. `[medium]`

---

## D. Lighting & mood

- [ ] **Commit to the dawn key-light direction for hero framing** *(lighting & mood)* — The sun is at ~16° (correct per §9.5) but the render reads flat and shadowless across the interior. With the white-centerpiece fix in the interactive pass, also angle the key light so the Keep and Arcane Tower throw long readable dawn shadows across the plaza — long raking shadows are what sells "soft dawn, fairy-tale just-awake" and give the flat ground depth. `[quick]`

- [ ] **Add a gentle warm fill / ambient tint** *(lighting & mood)* — The interior currently looks evenly lit and slightly cold. A subtle warm ambient (or a low-intensity warm fill light) will round out the shadow side of buildings and pull the whole town into the dawn palette the spec wants. Pairs naturally with the warmer grass and wall tints above. `[quick]`

- [ ] **Stage the title-screen camera angle deliberately** *(composition)* — The current review camera is a high wide survey shot — good for QA, weak as a hero shot. The spec notes the Arcane Tower is placed "visible from the title-screen camera" (§5). Define one lower, more cinematic camera that frames Elarion + the Keep + a gate behind them, with the tower silhouette in frame — so there's a beauty angle ready when the centerpiece render is fixed. `[medium]`

---

## E. Exterior (beyond the known black-terrain / sky items)

- [ ] **Tighten the approach lanes so they read as deliberate roads** *(exterior)* — Once the terrain is lit, make sure each of the four approach lanes reads as a clear paved corridor against the wilderness — they're the wave-attack routes (§8) and should be visually unmistakable. Give the lane edges a defined foliage border (denser than the current sparse tree/rock pair) so the lane *channels* the eye from spawn zone to gate. `[medium]`

- [ ] **Differentiate the four approaches by biome flavour** *(exterior)* — The spec wants each gate's approach to express its realm-map direction — north forest, east farmland, south barren, west river (§8.2). In the current build the four lanes look identical. Even before full Terrain biomes land, vary the per-lane dressing: pines north, orchard trees + crop strips east, bare/dead trees south, a few `hex_water` tiles + standing stones west. Cheap, and it makes the world feel directional and narratively loaded. `[medium]`

- [ ] **Seat scattered exterior strays onto the ground** *(exterior)* — The exterior shot shows a couple of props/tiles floating off the Terrain (a cylinder, stray tiles). When Task #14's terrain pass happens, sweep for any object whose Y doesn't sit on the heightmap — floating geometry breaks the "one continuous world" goal of §9.8 instantly. `[quick]`

---

## Summary

The Avalon village is **structurally sound and faithful to the spec** — the shaped rectangle wall, four cardinal gates, the Heart-and-Keep twin centerpiece, the five named buildings in their correct quadrants, and the residential/market/workshop/orchard dressing are all present and correctly placed. The work that remains is **art direction, not layout**: the interior currently reads as scattered objects on a wide empty olive lawn. The highest-impact moves are *clustering* (tighten spacing, fill the dead NE/NW corners, build a real market scene on the plaza), *contrast* (make the plaza paving and dirt paths read distinctly from grass), and *lived-in storytelling detail* (guarded gates, banners on the ceremonial axis, lanterns, garden beats). All suggestions are buildable from the already-imported KayKit packs — no new assets required.

**Suggestion count: 21**, grouped by effort:
- `[quick]` — 12
- `[medium]` — 9
- `[larger]` — 0
