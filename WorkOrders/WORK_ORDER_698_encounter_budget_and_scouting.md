<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-13
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-13) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 698 — Seeded encounter budget (level-aware caps) + scout-the-force option *(renumbered from a colliding fresh 685 mint, 2026-07-13 — 685 is the closed webtrace TTL cron)*

**Status: READY TO IMPLEMENT** (owner ask 2026-07-12: "logic that dictates a seeded max of
enemies — at level 3 I would not expect 8 enemies with 3 casters. Or an option to scout the
enemy if not in range and determine force". **All four pins RULED — owner "agree" 2026-07-12:**
(1) band sets the ceiling, hero level tunes the LOW bands only — the danger gradient keeps its
teeth; (2) DEFEND waves EXEMPT — player-triggered escalation is the chosen risk; (3) first-pass
budget numbers as tabled, tune-by-data; (4) scout = free at the compass in V1, priced/earned
echo-scout parked to V2.)
**Lane:** Combat/AI + World. **Type:** NEW system (verified gap: the 2026-07-06 honest finding —
NO hero-vs-enemy level rule exists; danger bands are DISTANCE-keyed; composition is unbounded).
**Extends:** WO-650 (Overworld Spawn Budget Governor, Grok audit — the reserved home) ·
WO-626 (EnemySpawnKit) · the F8-8 scatter family system (18 seeded records, 3 danger bands).

## Part 1 — the encounter budget (data-driven, "data only always")

A seeded pack rolls against a BUDGET, not a free count:

- **`encounter-budgets.json`** (dual-copy): per danger band × hero-level bracket →
  `{ maxUnits, maxCasters, maxElites, threatPoints }`. Each enemy family role carries a
  threat-point cost in enemies.json (warrior 1 · tank 2 · caster 2 · elite 3 — first pass);
  a pack's roll must fit the budget. Example row: band-1 (home ring), hero L1-4 →
  `{ maxUnits: 4, maxCasters: 1, threatPoints: 6 }` — the owner's 8-with-3-casters becomes
  impossible near home at level 3.
- **Seeded + deterministic:** the existing seeded scatter records keep their seeds; the budget
  clamps composition at spawn-roll time (governor reads the budget; spawner stays dumb).
  One reader (the WO-650 governor), no per-spawner rules — One Model.
- **Canon tension, named (owner pin #1):** the world's soft gate is the DISTANCE danger
  gradient ("get stronger before venturing further" — WO-453 canon). A hero-level cap must not
  flatten that: proposal = **band sets the ceiling, hero level tunes only the LOW bands**
  (home ring stays gentle for low levels; far bands stay dangerous regardless of level — the
  gradient keeps its teeth). Alternative = full level-relative scaling everywhere (safer, but
  kills the "I walked too far and paid for it" canon). Owner picks.
- **Waves exempt by design (pin #2):** town DEFEND waves are player-triggered press-your-luck —
  their escalation is the chosen risk. Proposal: budgets govern OVERWORLD packs + ambient
  spawns only; waves keep their 20-wave schedule. Confirm.

## Part 2 — scout the force (the read-before-engage)

**Why (owner, 2026-07-12): "helps engagement strategy."** The scout read exists to make
ENGAGING a decision, not an accident — see the force, then choose: fight now / come back
stronger / route around / pick which pack first. Same design spine as the strategic skill tree
(WO-676) and the placement game (WO-673): information → strategic choice. The threat word is
the decision surface; the fuzz keeps it a read, not a spoiler.

Reuse the built tells; no new sim:

- **At sight range (packs already instantiate at 85m):** the pack's compass pip / skull plate
  gains a THREAT READ — unit count pips + composition glyphs (sword/staff icons for
  melee/caster count) + a relative-threat word keyed to the budget math ("Even match" /
  "Dangerous" / "Deadly") — text + glyph, never color-only.
- **Beyond sight range (the scout verb, owner's option):** long-press/tap a compass pip →
  a small intel card: "Scouts report: ~5 orcs, 2 casters — Dangerous." Fuzzy by design
  (counts rounded, composition approximate) so scouting informs without spoiling. V1 = free
  information from the compass; a future echo/scout-unit fiction can price it later (V2 pin).
- Threat word derives from the SAME threat-point math as the budget (one formula, two readers:
  the governor caps with it, the scout report describes with it).

## Gates
- [ ] EditMode: budget math (points/caps) unit-tested; every family role has a threat cost;
      budgets json parses + dual-copy sync.
- [ ] Fleet probe: seed 100 packs per band × level bracket headless → assert zero rolls exceed
      budget; band-1/L3 never exceeds maxCasters 1.
- [ ] Scout read matches actual composition within the fuzz tolerance (probe assert).
- [ ] `[Flow:Spawn]` traces name budget clamps (§12); COMPILE_GATE_OK + REGRESSION_OK +
      owner felt-pass at a low-level fresh save walking the home ring (PO closes).

## Owner pins — ALL RULED 2026-07-12 ("agree")
1. ✅ Band-ceiling + low-band level tuning (the gradient stays canon).
2. ✅ Waves exempt.
3. ✅ First-pass numbers accepted — tune table, not code.
4. ✅ Scout free-at-compass V1; echo-scout pricing = V2 pin.

## What NOT to touch
Wave schedule/endless rules (04481c59) · family seeds/records (F8-8) · enemy stat blocks
(divergence consolidation is WO-627/WO-641's job, not this) · BattleArena interior composition.

*Cross-refs:* WO-650 (governor home) · WO-626 (spawn kit) · `docs/MONSTER_FAMILY_ARCHITECTURE.md` ·
WO-453 canon (danger gradient) · COMBAT_PIVOT_NORTHSTAR ("fewer, meatier, telegraphed") ·
2026-07-06 honest finding (no level-delta rule exists).
