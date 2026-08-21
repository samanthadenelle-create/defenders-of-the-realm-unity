# WORK ORDER 673 — Strategic Building Placement (player-placed functional buildings)

> **⚠ FLAG LANGUAGE SUPERSEDED 2026-07-13 (WO-695, ex-682):** `ff.strategicplacement` has been
> **REMOVED** — strategic placement is ALWAYS ON in every build. Every "default OFF until felt-pass" /
> "flag-off = today byte-identical" line below is historical. Also superseded: the §Reconciliation
> "new game inherits the pre-laid town" ruling — owner ruled 2026-07-12 "I want to see the blank
> template and add buildings"; New Game is now the blank template (+ one FTUE grace-default Forge record).

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Owner rulings (the creative+architecture recommendations, accepted wholesale):** free position +
walkability validity, collector yield scales with node distance (fast-follow lane), NO adjacency
bonuses · Pillager enemy archetype for economy-targeting (fast-follow WO-673B) · guided first-Forge
FTUE + Founder's Plan ghost (flag-ON arc) · free moves (locked during DEFEND), full demolish refund ·
**rotation 45° steps** (owner final ruling 2026-07-11, overriding the 90° caution — WITH the
engineering condition that validity/footprint-claiming is rotation-aware or conservatively over-claims
at diagonals; placement must never lie about claimed space) · taxonomy = **Build → Town / Defenses /
Walls** (owner adopted the reviewer naming: "a tower is a structure, the old term feels vague") · budget = core kit + exactly one leftover choice · flip-a-base V1 =
claim ceremony + banner re-skin + captured walls repairable + one collector slot (per-site layouts V2).
**Reviews:** `docs/WO673_ARCHITECTURE_REVIEW.md` (GO-WITH-CHANGES, the 7-lane V1) ·
`docs/WO673_CREATIVE_REVIEW.md`. Flag `ff.strategicplacement`, default OFF until owner felt-pass;
flag-off = today byte-identical.
**Minted:** 2026-07-11 (owner WO, verbatim in §Owner-Spec below).
**Why now:** WO-672 made structures attackable + damage visible + repair costed — so WHERE a
Forge/collector/storefront sits is now a real strategic exposure decision. Auto-placement denies it.
**Owner thesis (2026-07-11, canon):** "we have gone from a static city to a player defined map which
is now the organic correct solution." The layout system now agrees with the damage system: authored
shell (castle/walls/floor/decor), player-defined town inside.
**Owner extension:** this "will allow the FLIP-A-BASE to work better" — the WO-584 ownership flip /
camp claim (clear → claim an enemy base) becomes real ownership: a flipped site is laid out with the
SAME placement tools. Architectural implication for the review: BaseLayout grows from one home layout
toward PER-SITE layouts (the headless-replay / Arena-server seam already designed into BaseLayout
makes this the natural direction — verify the shape, don't build it in V1).

## Owner spec (verbatim intent)
1. The castle/base builder only auto-places: walls, floor/ground, purely structural/decorative pieces.
2. Remove auto-placement of all functional/targetable buildings (Forge, production, etc.).
3. Build menu lets the player select + freely place those buildings anywhere valid.
4. Starting resource budget so core buildings are affordable at match start.
5. All player-placed buildings register as targetable (enemy AI + waves) correctly.

## Owner additions (2026-07-11, same session)
- **Unlock-gating (owner insight):** "the best part is it allows us to hide the Colosseum and the
  Barracks till they 'unlock' them." Palette entries become PROGRESSION-LOCKED: a building appears in
  Build → Town only when unlocked (village-tier / WO-432 tech-tree node), and exists in the world only
  when placed. The palette's existing `_lockedIds` seam is the mechanism; wiring locks to tech-tree
  unlocks (+ authoring Colosseum/Barracks catalog rows) = **WO-673B fast-follow**. Unlock = earn the
  RIGHT to build it, then choose where it stands.
- **Rotation:** during placement, rotate LEFT/RIGHT in stepped increments — 45° (owner open to 90°).
  Reuse-first: `BaseLayout.PlacedStructureData` already persists `yaw`, and `TowerPlacementRotateMenu`
  already exists in the placement flow — extend it with stepped rotate buttons (and keyboard [Q]/[E]
  or equivalent), don't greenfield. Creative review to recommend 45 vs 90 (45 = expressive layouts;
  90 = cleaner snapping/nav carving — navmesh-carve obstacles must rotate with the model).
- **Palette naming (owner taxonomy):** the build menu has THREE categories — **Build → Structures**
  (functional buildings: Forge, production, storefronts, collectors), **Build → Defense**
  (towers/gates), and **Build → Walls** (wall pieces — split out because walls extend one further:
  CLAIMED OUTPOSTS get wall-building too, the CoC walls-sink canon; owner 2026-07-11).

## Reconciliation (BINDING — how this lands safely HERE)
- **⛔ MainCastle_Hall is owner-hand-dialed** — `CastleHubBuilder` regen REVERTS her offsets (standing
  canon). The live scene is the MERGED `Main_Castle_Overworld`. Expect most functional structures to be
  placed by RUNTIME INJECTORS (`HubStructureVisualInjector` — the Apothecary was
  'ApothecaryStation (runtime)'; `CastleVendorNpcInjector`), NOT by a scene bake — the census confirms
  per-structure. Prefer disabling/redirecting injector entries over any scene rebake.
- **Palette reversal (owner-authorized here):** the 06-27 ruling restricted the build palette to
  Tower/Wall/Gate ("Defensive only"). THIS WO supersedes it — functional buildings join the palette
  (`BuildPaletteUI._types` + catalog rows). Record the reversal in the palette code comment.
- **Reuse, never greenfield:** placement/persistence/charging = the existing BuildMode spine
  (palette → ghost → IsValidPlacement → charge → BaseLayout → BaseLayoutLoader replay). Placed
  functional buildings ride `StructureFactory.Create` + `PlacedStructureData` exactly like towers.
  Build/upgrade timers (F8-51) apply automatically via the same seam.
- **Targetable-by-construction (One Model):** a placed Building/collector already implements
  `IDamageableStructure`; enemy structure-aware targeting + WaveDamageReport + StructureDamageVisuals
  + repair pricing (in-kind, WO-672 amendment) all read the same capability surface — registration is
  the SAME path, no bespoke wiring. The census verifies each type's prefab/catalog row exists.
- **Starting budget:** data-driven (new-game GameState defaults / DifficultyTuning seam — census names
  the right home). Sized so the core kit (Forge + 1 collector chain + a couple defenses) is affordable.
- **Migration:** existing saves with injector-placed structures must not lose them — first-load
  migration converts auto-placed functional structures into BaseLayout records at their current
  positions (owner keeps what she has; new games start with the free-placement flow).
- **FTUE guard:** the tutorial's first-tower beat and vendor talk-routes depend on structures existing —
  census lists which flows assume an auto-placed building; those get either a tutorial step ("place your
  Forge") or a grace default. Flag-gate the whole behavior (`ff.strategicplacement`) until felt-passed.
- **New-game semantics (owner ruling 2026-07-11):** a new game INHERITS the pre-laid town as movable
  player-owned records (the L3 migration behavior as built — correct). The EMPTY-FIELD
  build-from-scratch experience is deliberately **V2 flip-a-outpost territory**: you earn the blank
  canvas by conquering it. The guided-Forge/Founder's-Plan FTUE re-scopes accordingly in WO-673B
  (onboarding teaches MOVE/re-layout on the inherited town, not first placement).

## Acceptance
- [ ] New game: walls/floor/decor auto-build; zero functional buildings pre-placed; palette offers them;
      budget affords the core kit; placement persists + reloads; enemies target them; damage report,
      repair/rebuild, and timers all work on them.
- [ ] Existing save: nothing lost (migration proves via save round-trip regression).
- [ ] MainCastle_Hall scene file untouched (no regen).
- [ ] COMPILE_GATE_OK + DataRegression + fleet (palette/placement probes green) + owner felt-pass.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
