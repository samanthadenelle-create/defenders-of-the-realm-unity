# WO-673 Creative Review — Strategic Building Placement

**Role:** creative-director's sounding board. **Format:** options → recommendation, per the owner's direct-by-reacting style.
**Read against:** WO-673 spec, WO-672 (damage/repair shipped), COMBAT_PIVOT_NORTHSTAR, DESIGN_CORE_LOOP_AND_STRUCTURE (CoC seat/walls canon), the endless-wave ruling (manual DEFEND past wave 20), and the motivating moment: a wave enemy beelined the Apothecary and destroyed it.
**Code facts anchoring this review (not comments — catalog):** placement is FREE-FORM radial, not grid (`structures-catalog.json`: circular `footprint` radii + `noOverlap`, `IsValidPlacement`); costs are already multi-resource (wood/food/iron/crystals); repair is in-kind (WO-672 amendment).

---

## 0. The verdict up front

This is the right feature at the right moment. WO-672 built the *consequence* engine (damage, visible burn states, in-kind repair); WO-673 supplies the *decision* that gives those consequences an author — the player. The Apothecary moment is the proof: she felt loss about a building **she never chose to put there**. Once she placed it, that same loss becomes *her* story ("I knew it was exposed, I gambled"). That's the CoC emotional core, and this game already has every ingredient CoC lacks: a living town, a hero who personally defends it, and townsfolk who run into the buildings.

The risk is equally clear: free placement can dissolve a *town* into a min-max fortress-blob. The guardrails section (§4) exists to prevent that.

---

## 1. The strategic layer — what makes placement a GOOD decision space

A placement decision is only strategic if the map has a **gradient** — somewhere is better for yield, somewhere is better for safety, and they're not the same place. We already have that gradient for free:

- **Resource nodes sit where they sit** (world-authored, mostly outside the comfortable defense perimeter). Collector near the node = full yield, exposed. That's the CoC "collectors outside the walls" dilemma, inherited without inventing anything.
- **The Heart is the anchor at (0,0,0)** and the lose-condition. Close to the Heart = inside every defense you'll ever build. Far = fast access to nodes/gates. Distance-from-Heart IS the risk axis (this is already canon — DESIGN_CORE_LOOP §5's radial risk gradient, now expressed at town scale).
- **Walls are player/auto-shaped chokepoints** — a building behind a wall is a building an enemy must *earn*.

### The rules — three candidate rule-sets

**Option 1A — Pure position (CoC model).** The ONLY rule is *where you put it*. Yield, safety, walk-time all emerge from position vs nodes, walls, Heart. No adjacency bonuses, no road requirements.
- CoC ships a billion-dollar decision space on exactly this. A ten-year-old reads it instantly: "near the mine = more ore, but the monsters can reach it."

**Option 1B — Position + adjacency bonuses (Anno / SimCity model).** Forge near the mine = +10% smelting; shop near the plaza = +traffic; etc.
- Deeper on paper, but it converts the town into a solved puzzle (one optimal quilt), punishes the colorblind/legibility rule (bonuses need overlays to read), and it's exactly the "min-max grid" feel §4 must prevent. Also a balance tax forever.

**Option 1C — Position + reachability requirement (They Are Billions flavor).** Pure position, plus one *validity* rule: a building must be navmesh-reachable from the town (townsfolk have to be able to walk to it). Not a bonus — a placement check, red-ghost if you try to entomb a shop inside a wall donut.
- TAB/WC3 teach that path-shape is the real base-building skill; here the townsfolk paths are also the town's *life* — vendors walking to the Forge is the diegetic payoff.

**→ RECOMMENDATION: 1C.** Pure position carries all the strategy (proximity-to-node = yield, depth-behind-walls = safety, distance-from-Heart = risk); the single reachability check protects the living-town feel and costs the player zero cognitive load — it only speaks when violated. **Explicitly reject adjacency bonuses for V1.** Every rule must earn its place; adjacency doesn't.

One number to make proximity REAL: collectors should yield **as a function of node distance** (full within a radius, decaying or zero beyond). If a collector yields the same anywhere, the entire risk/reward axis is fake and everyone builds everything at the Heart. This is the one mechanical addition §1 needs. (ResourceCollector already scales accrual by HpFraction — same pattern, second input.)

---

## 2. The enemy-targeting ruling (shops attacked like defenses?)

The owner's open question, framed in the conventions she knows:

**Option 2A — Everything is a target, nearest-first (raw CoC).** Any structure in reach gets hit; enemies path to whatever's closest/cheapest.
- Honest and simple, and in CoC it IS the game. But CoC bases are disposable and unwatched; *this* town is a home the player lives in with townsfolk who flee INTO buildings. Every wave razing shops reads as cruel grind, not strategy — and repair (in-kind, WO-672) becomes a chore tax on every wave.

**Option 2B — Defenses-first taunt priority.** Enemies always prefer towers/walls/Heart; functional buildings only take splash or get hit if literally in the path.
- Safe and readable, but it deletes the Apothecary moment — placement of functional buildings stops mattering, which is the whole point of WO-673. The shield is too strong.

**Option 2C — Preference-weighted archetypes (CoC troop-AI, mirrored).** Most wave enemies prefer defenses + the Heart (2B behavior)… but a distinct minority archetype — call them **Pillagers** — beelines economy/functional buildings, exactly like CoC's goblins hunt collectors. Pillagers get a loud non-color read (distinct silhouette — sacks, torches, a hunched sprint — per the colorblind rule). Deeper waves = more Pillagers.
- The Apothecary moment stays in the game as a *designed, readable, counterable* threat: you SEE the torch-carriers split off, and you shaped your walls (or didn't) for exactly this. It creates the CoC meta (protect collectors vs sacrifice them as bait) without every wave leveling the town.

**→ RECOMMENDATION: 2C.** It's the only option that keeps placement stakes alive AND the town livable. Orcs already come in Mage/Tank/Warrior — Pillager is a behavior flag on an existing body, not new art.

**On the townsfolk-shelter tension ("delicious or cruel?"):** delicious — with two cruelty caps. (1) Sheltering must NEVER increase a building's target priority (shelter and targeting stay orthogonal; the player must never learn "townsfolk are bait"). (2) When a sheltered building breaks, townsfolk flee OUT, shaken but alive (V1). The player feels "I failed to protect them," never "I got them killed by building a shop." That's the ten-year-old line: tension yes, guilt-trauma no. (V2 can revisit stakes if the game earns it.)

---

## 3. First-session experience — magic or paralysis?

An empty field + a budget is magic for builders and paralysis for everyone else. The fix is the industry-standard pair:

**Option 3A — Suggested-layout ghost (recommended).** New game shows a faint ghost layout ("Founder's Plan"): one tap = "Build it for me" (spends the budget on the sensible default), or ignore it and place freely. Ghosts fade as you place your own.
- This is the Fallout Shelter / every-city-builder on-ramp: the default teaches what a good layout looks like *by example* (collector near node, shops behind walls), zero tutorial text. Directors-who-react (like the owner herself) get something to react against.

**Option 3B — Recommended-spot hint per building.** Pick "Forge" → a suggested spot glows.
- Weaker: teaches spots, not *layout logic*, and adds per-building authoring cost.

**Option 3C — Pure freedom + forgiving economy.** No hints; rely on free moves.
- Fine for builders, paralysis for the ten-year-old on turn one.

**→ RECOMMENDATION: 3A**, with the FTUE beat WO-673 already flags ("place your Forge") as the guided *first* placement before the ghost offers the rest. First placement by hand = ownership lands; ghost = the rest is optional homework.

### The move/refund rule

- **CoC: moving is FREE, always, instantly** — and that freedom is *why* base-layout meta exists at all. Charged moves kill experimentation dead.
- **→ RECOMMENDATION: free move, anytime EXCEPT during an active wave/DEFEND** (mid-combat teleporting buildings breaks the fiction and the threat). **Demolish = full refund in V1.** The economy's teeth are already elsewhere — WO-672 repair costs and lost yield are the real stakes; sunk-cost punishment on placement would only punish learning. Revisit partial refund (50%) only if data shows demolish-cycling abuse.

---

## 4. What placement freedom must NOT break — the guardrails

The nightmare: Elarion stops being a town and becomes a CoC grid-blob. Guardrails, each earning its place:

1. **The Heart is fixed at (0,0,0), never placeable, never movable** — plus a small clear **plaza radius** around it (no building inside ~8–10m). The Heart stays the readable anchor and the tree keeps its stage. (Also protects the echo/tree spectacle canon.)
2. **Walls, floors, gates stay auto-authored** (owner spec item 1 — affirmed). The town's *silhouette* is authored; the player arranges life *within and around* it. This is the single biggest defense against grid-blob.
3. **Build zone = the town's environs**, not the whole overworld: inside the walls + a bounded apron outside (where the nodes live). Collectors-outside-the-walls stays a *choice inside the sanctioned zone*, not buildings scattered to the horizon.
4. **Reachability validity (rule 1C)** — every functional building must be walkable by townsfolk; vendors keep their routes (WO-673's census already flags the talk-route dependency). This is what keeps placed buildings *inhabited* rather than furniture.
5. **No snap-grid visual language.** Free radial placement is already the code reality — keep it. Organic offsets are what make it read as a village.

---

## 5. The starting budget as a creative tool

Scarcity is the author of the first choice. Shape it so the player can afford the **core kit with one meaningful choice left over**:

- **Affordable at start (the core kit, per WO-673 spec):** Forge + 1 collector chain + ~1 tower's worth of defense. This is the "found a settlement" fantasy — production, income, one guard.
- **The designed leftover:** enough remaining for **one** of {second collector | extra tower | first shop} — not all three. The first session's real decision is *greed vs safety vs comfort*, and it's the player's, on turn one. That leftover IS the tutorial for the whole strategic layer.
- **Earned, not given:** shops/storefronts beyond the first, second collector chains, upgrades (the existing tier sink), and anything cosmetic. Shops as *earned* town-life makes the town visibly grow with success — the "living progress monument" pattern the Tree already uses.
- Budget lives in data (GameState defaults / DifficultyTuning, per the WO) — tune the leftover, not the kit.

---

## 6. Rotation stepping — 45° vs 90° (owner addition, 2026-07-11)

What the conventions do, and why:
- **CoC: no rotation at all** — buildings are visually rotation-agnostic on a grid; layout expression is 100% position. Not our case (our buildings have fronts: doors, counters, vendor spots).
- **They Are Billions / WC3 / RimWorld / most grid builders: 90° or none** — because **rectangular grid footprints** make 45° a collision/pathing headache. Their constraint is technical, not aesthetic.
- **The Sims (hold-key 45°), Animal Crossing, Valheim-likes: 45° or freer** — games whose fantasy is "this place is *mine*" choose expressiveness.

The deciding fact is in our own catalog: **placement here is free-form with CIRCULAR footprints** (`footprint: 2.5` radii, `noOverlap`), not a grid. The classic engineering argument for 90° — rectangular footprint math — **does not exist in this codebase**. A 45°-rotated building collides, paths, and validates identically to a 0° one.

So it's purely a feel call, and the owner already answered it: *"the beauty of this allows us to let the players layout as they like."*

**→ RECOMMENDATION: 45° steps** (Q/E or bumper taps, 8 orientations). 45° is what lets a shop *face* a curving lane, buildings ring the plaza toward the Heart, a Forge angle toward the gate — the difference between a village and a barracks. 90° would leave every facade parallel, which is exactly the board-game read we're guarding against in §4. Keep it **stepped** (not free-spin): 8 orientations stay readable, snap decisively, and feel deliberate on mobile. One caveat on record: if a future building ships a *rectangular* footprint or grid-snap, revisit — for circular footprints 45° is free.

---

## 7. Category naming — Build → "Structures" (owner addition, 2026-07-11)

Sanity check against the existing palette: the current category is **"Defensive"** (towers/walls/gates). But a tower IS a structure — "Defensive vs Structures" doesn't partition; a ten-year-old can't predict which tab holds the Forge, and "Structures" is the label a programmer gives it, not a mayor.

CoC's tabs are the convention the owner knows: **Defenses / Resources / Army / Traps / Decorations** — each names *what it does for you*.

**Options:** "Structures" (as ruled) · **"Town"** · "Economy" · "Production".

**→ RECOMMENDATION: rename the pair to "Defenses / Town".** "Town" is diegetic (these buildings ARE the town — Forge, shops, collectors, Apothecary), one syllable, and it partitions cleanly against "Defenses" by *purpose*: things that fight vs things that live. It also quietly reinforces §4's whole thesis — you're building a town, not placing assets. If the owner prefers keeping "Structures," it still works — but rename "Defensive" → "Defenses" so the tabs at least match grammatical register. (Cheap change now: `BuildPaletteUI._types` label + catalog category strings; expensive after players learn the tabs.)

---

## 8. Flip-a-base — the claim payoff (owner extension, 2026-07-11)

The conquest fantasy has one image: **your banner over their walls.** What makes claiming feel GREAT isn't rebuilding — it's the *moment of reversal*: the place that was shooting at you five minutes ago now works for you. CoC never delivers this (you raid and leave); WC3/Risk/territory games do — and our own canon is already built for it: **life force = territory reclaimed** (COMBAT_PIVOT_NORTHSTAR) means every claim is *supposed* to visibly push the darkness back.

Canon check, on record: WO-475 convert-on-clear was parked behind `ff.basebuilding` with the V1 outpost reward re-pointed to skill points/gear. This extension *softens* that gate — a claim-lite payoff enters V1 while full per-site building stays V2. Name that reversal in the WO so nobody "restores canon" by deleting the claim.

**The smallest claim that still feels like ownership — three ingredients, all reuse:**

1. **The ceremony (the feel — non-negotiable).** On clear: banner rises, the site's structures **recolor/re-skin to Elarion's identity** (shape+banner, never color alone — the banner silhouette IS the colorblind-safe tell), and a **life-force pulse travels visibly from the site to the Tree** — the world loop made literal in one camera moment. This is 90% of the fantasy at ~10% of the cost.
2. **Keep their walls (the trophy).** The cleared site's standing defenses stay, flipped friendly — battle-damaged, repairable with the WO-672 flow. Repairing *their* tower as *yours* is ownership expressed through a system we already shipped; "your banner over their walls" is literal.
3. **One collector slot (the income).** Each claimed site exposes ONE pre-placed collector slot; the player activates it and it pipes home. Claimed sites become **forward footholds** on the danger gradient (DESIGN_CORE_LOOP §5 radial risk): income sitting in dangerous territory, defended by captured walls — the outpost model's claim→harvest→pipe-home spine, delivered through WO-673's structures instead of a bespoke system.

**Explicitly parked to V2:** free per-site building/layout with the WO-673 tools (real per-site build zones, budgets, palettes). Right call — V1 claim-lite proves the loop feels good before we pay for N build sites; and per-site layout only matters once sites can be *counter-attacked* (the V2 triggered-wave layer), otherwise it's decorating.

**→ RECOMMENDATION:** V1 claim = **ceremony + flipped repairable defenses + one collector slot.** That's the minimum that reads as *ownership* (I hold it, it works for me, I maintain it) rather than *loot* (I took a thing and left).

| # | Decision | Options | Recommendation |
|---|---|---|---|
| 1 | Placement rules | pure position / +adjacency / +reachability | **Pure position + reachability check; collectors yield by node proximity; NO adjacency bonuses** |
| 2 | Enemy targeting of shops | everything (CoC) / defenses-first / weighted archetypes | **Pillager archetype: most enemies hit defenses, a readable minority hunts economy** |
| 3 | Townsfolk shelter tension | remove / keep raw / keep with caps | **Keep — shelter never raises target priority; townsfolk always survive a broken building (V1)** |
| 4 | FTUE | ghost layout / spot hints / pure freedom | **Guided first Forge placement, then a one-tap "Founder's Plan" ghost layout** |
| 5 | Move/refund | CoC free-move / charged | **Free move (locked during DEFEND); demolish = full refund V1** |
| 6 | Rotation step | 45° vs 90° | **45° (8 steps, Q/E)** — footprints are circular, so 45° costs nothing and buys the "my town" feel |
| 7 | Category name | Structures / Town / Economy | **"Defenses / Town"** — purpose-named pair, CoC-legible |
| 8 | Starting budget shape | — | **Core kit + ONE leftover choice (greed vs safety vs comfort); shops mostly earned** |
| 9 | V1 claim payoff (flip-a-base) | ceremony only / +flipped defenses / +collector slot / full per-site building | **Ceremony + flipped repairable defenses + one collector slot; per-site building = V2** (note: softens the WO-475 `ff.basebuilding` gate — record the reversal) |
