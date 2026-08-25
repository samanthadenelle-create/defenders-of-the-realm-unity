# WORK_ORDER_514 — tower cap (perf) + Population → "Saved Echoes" → skill points

**Status:** SPEC — DESIGN CAPTURED (owner felt-test brain-dump 2026-06-25, F8) · Economy/Defense lane · **NOT yet implemented** (needs owner confirm + a deliberate build).

*(Board note 2026-08-24: bucket corrected. The verdict word was non-canonical, so the parser fell through to its substring pass and bucketed this row as **Done** on a word that appeared later in the sentence — the WO-1180 laundering path, and it only ever errs toward "finished". No claim about the work changed; only the leading token, so the row buckets where its own text says it belongs.)*
**Origin:** owner F8 note: "we never touched POPULATION and we never enforced or counted number of TOWERS. Capping towers is smart for performance. We can kill population unless we rebrand it to Saved Echoes X/10, and every 3 echoes saved releases one SP."

## Item A — Tower cap (counted + enforced) — PERF *and* anti-turtle balance
- Today: towers are NOT counted and NOT capped. Two problems: (1) perf (each tower = AI/targeting/VFX cost), and (2) **BALANCE — owner 2026-06-25: an uncapped player can "box their base in"** (wall off completely) and trivialize the whole defense. The cap forces interesting choices: a limited tower budget spent to cover the 4 drawbridge lanes IS the defense puzzle.
- **Build:** count active towers + enforce a MAX (tunable cap, e.g. start ~8-12; final number owner-tuned). Block/deny placement past the cap (clear UI feedback: "Tower limit reached"). Expose the cap as a tunable.
- **Synergy:** ties to the moat/drawbridge CHOKEPOINTS (WO-513/moat) — a capped tower budget spent to cover the 4 drawbridge lanes IS the defense puzzle. Consider per-lane / near-chokepoint placement value later.
- SME first: find the tower placement system (TowerPlacementSystem) + where towers register/spawn; add the count + cap gate there. Instrument the count.

## Item B — Population → "Saved Echoes" (X/10) → skill points
> ⚠ **SUPERSEDED 2026-06-29 by `WORK_ORDER_587` (Population & Echo Growth System V1).** The owner's matured design
> replaces the flat "X/10, 3-per-SP" model with milestone-driven population XP + cap unlocking echo workforce slots
> (1→5) from quests / outposts / wave victories / village upgrades, data-driven via `population-milestones.json`.
> Build Item B per **WO-587**. (Items A tower-cap + C siege below remain OPEN under this WO.) The SP-linkage idea
> here is an OPEN question in WO-587.
- Today: a "Population" stat exists but is **never touched / unused / not wired to anything**. Dead UI.
- **Owner decision:** EITHER kill it, OR (preferred) **rebrand it to "Saved Echoes"** and make it MEAN something:
  - Display as **Saved Echoes X/10** (the echoes you've rescued/saved — ties to the echo/life-force economy, memory `echo-workforce-drag-drop`).
  - **Every 3 Echoes saved releases 1 Skill Point (SP).** So saving echoes = progression fuel (feeds the Wisdom/skill-tree spend path).
  - This turns a dead counter into a reward loop: save echoes → earn SP → unlock skills.
- SME first: find the Population stat (where it's defined/displayed — VillageHudController? a GameState field?), confirm it's truly unwired, then either remove it OR repurpose: rename to Saved Echoes, wire the X/10 counter to the echo-save event, and grant 1 SP per 3 saved (route SP into the existing Wisdom/skill-point grant path — see HeroProgression / WisdomCurrencyService).
- Ties to: the echo workforce (cap 5, life-force thresholds), the skill tree (Wisdom -> SP -> nodes), and the "drive enemies back -> tree grows -> spirits/echoes harvest" loop.

## Item C — Defense enemies should SIEGE structures, not only the hero (owner insight 2026-06-25)
- Observed in town: mobs only attack the HERO, never the towers/walls/heart. In a base-defense that's wrong — enemies should siege the structures (towers/walls/heart), with the hero as one target among many.
- IMPORTANT context distinction: the ARENA was *just* set hero-only (`SetHeroOnlyTarget`, WO-512 — correct, no base in a duel). Town/defense enemies need the OPPOSITE — the full `EnemyBrain.ScoreAndPickTarget` (hero / tower / heart). So this is NOT undoing the arena fix; it's making sure TOWN enemies use the multi-target siege path.
- SME: `EnemyBrain` target priority already has `FindNearbyHero() ?? FindNearestTower() ?? FindClosestTarget()` (heart). Check WHY towers aren't being picked in town: are towers registered as targets / IDamageable? Does the hero score always win? Is the defense even active in V1 (base-defense gated V2, ff.basebuilding) — i.e. is this expected-off until V2? Confirm before building. Likely lands with the base-defense V2 work.

## Acceptance (when built)
- A: tower count enforced against a tunable cap; placement blocked past it with feedback; counted/instrumented.
- B: owner picks kill-vs-rebrand; if rebrand, "Saved Echoes X/10" displays + every 3 saved grants 1 SP into the skill-point pool, felt-verified.

## Do NOT
Build blind — both need an owner confirm (esp. B: kill vs rebrand, the X/10 cap, the 3:1 ratio) + careful wiring into the existing economy/SP systems. Capture now, build deliberately.
