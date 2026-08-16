# WORK ORDER 997 — Class resource system: mana costs, recharge, and identity per class

**Status:** SPEC — READY FOR OWNER REVIEW (all numbers are proposals; balance intent is the owner's)
**Minted:** 2026-08-15 (CLI seat, main-line block) — banner bumped 997 -> 999 in the same edit as this
mint + WO-998. *(First minted as "1024" — the UI seat's block — caught and renumbered before any
reference existed; the two-block discipline is the lesson, again.)*
**Lane:** Hero combat data + `HeroAbilities` plumbing. File-disjoint from WO-1021 (talent UI) and WO-991 (caravan).
**Provenance:** owner ask 2026-08-15 (relayed mid-session): *"Full mana system for classes, costs, recharge, designed for the game."*

## 1. The measured gap (read at source 2026-08-15)

The plumbing EXISTS and is healthy — the DATA makes it inert:

- Pool/regen: `HeroAbilities.cs` — `_maxMana = 10f` (:52), regen ~0.9/s (:54), `EffectiveMaxMana`
  (:158-163), one shared over-time drip (`RestoreManaOverTime`/`StepManaOverTime`, :409-470).
- Cost: ONE reader — `ManaCostOf` (:170-171) = `def.ManaCost * HeroTalentModifiers.MageManaCostMultiplier`;
  charge/gate/refund all route through it (:245, :501, :557, :601-609). This is the right shape — keep it.
- Authored costs (`abilities.json`) are flat and class-blind: every basic attack and every `universal.*`
  skill is 0 mana; utility 1-3; mid 3-5; ultimates 6-8 — against a 10 pool at 0.9/s.
  **So only ultimates are pool-gated; everything else is cooldown-gated. Mana currently does not matter.**
- Structural recharge exists ONLY on the Cathedral and ONLY for the mage
  (`building-tiers.json`: +1 max / x1.15 regen per tier, 0.85 cost mult at tier 3).
  Knight and Ranger have NO resource economy at all.

## 2. Design proposal — one plumbing, three identities

Keep the single pool + single `ManaCostOf` reader. Differentiate by DATA: per-class base pool, regen
shape, and cost curve. Classic RPG-legible (ten-year-old test): the mage counts mana, the knight
paces vigor, the ranger earns focus by shooting.

| | **Mage — Mana** | **Knight — Vigor** | **Ranger — Focus** |
|---|---|---|---|
| Display name | Mana | Vigor | Focus |
| Base pool | 20 | 10 | 12 |
| Passive regen | 1.0/s | 1.5/s | 0.6/s |
| On-hit restore | — | — | **+1 per basic-attack hit** |
| Cost curve (Q/W/E/R) | 3 / 5 / 7 / 10 | 2 / 3 / 4 / 6 | 2 / 4 / 5 / 8 |
| Basic attack | 0 (always) | 0 | 0 |
| Identity | The pool IS the limiter; short cooldowns | Sustains long fights, gated in bursts | Weave basics between skills to refuel |
| Structural source | Cathedral (existing keys, unchanged) | **Barracks tiers** (new keys) | **Lumbermill? / none in V1** (owner pick) |

Recharge sources, in one list: per-class passive regen · ranger on-hit · Mana Draught + Manaweave
(existing drip path, unchanged) · **full restore on entering town** (rest = recovery, teaches town as home) ·
Cathedral for the mage (existing) · optional structural sources for knight/ranger (owner pick, can defer).

## 3. Implementation shape (for CLI, after the owner rules on §2)

1. **Data:** add a per-class `resource` block to `abilities.json` `classes[*]`:
   `{ "displayName": "Vigor", "max": 10, "regenPerSecond": 1.5, "onHitRestore": 0 }`.
   Re-author per-class skill costs to the ruled curve. Dual-copy + version bump.
2. **Plumbing:** `HeroAbilities` reads the class resource block on class resolve (base values only —
   modifiers still fold on top exactly as today). Ranger on-hit = one call from the basic-attack hit
   confirm into the EXISTING `RestoreMana` path (no second pool, no new component).
3. **Modifiers:** keep the `MageMana*` keys as-is for the mage. New structural keys (e.g. knight
   Barracks vigor) require `GameModifiers` strict-key additions + `ModifierKeyCoverageRegression` rows
   **in the same commit** — the key list is coverage-gated; keys, plumbing, and regression land together.
4. **UI:** ability faces show a cost pip; unaffordable = darkened face + cost badge pulse — **never
   hue-alone** (owner red/green colourblind). Resource bar label reads the class `displayName`.
5. **Oracle:** new `[class-resource]` suite: every class has a resource block; every authored cost
   <= that class's max; **at least one non-ultimate skill per class has cost > 0** (pins the
   "everything is cooldown-gated" regression this WO exists to kill); costs consumed via `ManaCostOf`
   only (no second reader).

## 3b. The mana BAR already exists — this WO also owns making it LEGIBLE (do not build a second bar)

Verified at source 2026-08-15: the chain is live end-to-end — `HeroAbilities.Mana/MaxMana` →
`HudModelProducers.HeroVitalsProducer` (`HudModelProducers.cs:195-245`) → `HeroVitalsModel` →
`HudKitController.cs:1456-1457` (`_vitals.ManaFill.fillAmount`, "MP LIVE (§0 fix)"); the bar object is
built in `ElarionUiKitNameplate.cs:130-173` (`StatBars/ManaBackground/ManaFill`). **Why it reads as
missing today:**
- `HudModelProducers.cs:226-227` does `Mathf.RoundToInt` on mana + max — against a 10-point pool the
  bar quantizes to 10% steps and the 0.9/s regen is invisible until a whole point flips.
- The producer polls at 0.20s (`:204`) — a 3-mana cast is one instant jump, no drain motion.
- `HudKitController.cs:997-1000` deliberately hides the mana row on the HEART plate (correct; confirm
  the hero plate never lands on that path).

**Fix shape (lands WITH or BEFORE the §2 cost rebalance, or the rebalance is invisible):** carry
floats through `HeroVitalsModel.Set` (or a float overload), tighten/event-drive the poll, lerped fill
+ a spend flash so burn-down reads. ⚠ Nameplate restructure must preserve the child names
`StatBars`/`HealthBackground`/`ManaBackground` — `HudUiRegression.cs:687` + the MVVM/Obsidian
conformance ratchets assert them.

## 4. What NOT to touch

- The ATB module (dormant) — no mana wiring there.
- `RestoreManaOverTime` drip semantics (Mana Draught/Manaweave behaviour is shipped).
- `MageManaCostMultiplier` clamp band [0.25..2.0] (just restored — review finding #7).
- No new MonoBehaviour, no second pool field — the single pool + single reader IS the architecture.

## 5. Open owner picks

**IMPLEMENTED 2026-08-15 late (SME agent, gated + committed by the CLI seat).** What shipped: the §2
pools/regens/W-E-R curves as data (abilities.json v4, both copies byte-identical), per-class resource
blocks read by `HeroAbilities.ApplyClassResource` (base values only, modifiers fold unchanged),
ranger on-hit Focus via the melee hit-confirm, bar legibility (floats through `HeroVitalsModel`,
epsilon-gated 5 Hz producer, lerped fill + brightness spend-flash), and the `[class-resource]` oracle.

**Deviations recorded by the implementer — the first two are OPEN OWNER RULINGS:**
1. **The §2 Q-column was NOT applied** — Q is the locked free basic attack in code, and this spec also
   says "basic attack 0 (always)". The table contradicted itself; the free-basic ruling won. If a
   costed 4th skill tier was intended, rule it.
2. **Ranger Quick Shot (`ranger.q`, the archer fantasy) earns NO Focus** — the on-hit restore is wired
   at the melee-swing path only (`PlayerAttackController`), not the cast path. One ruling needed:
   should ranged basics refuel Focus too?
3. On-hit restore is once per CONNECTED SWING, not per enemy caught in the 360-degree sweep
   (anti-farming judgement call — a crowd cannot multi-refund one attack).
4. Skill-pool costs (knight-skills/mage-skills/ranger-skills) untouched; all fit the new pools
   (oracle Case 2 pins it). Retuning them is owner-tuning.
5. "Full restore on entering town" already existed (`RestoreManaToFull` via SafeZoneRecovery) — no change.

**Still open from the original spec:** knight/ranger STRUCTURAL sources (Barracks vigor per tier
needs new GameModifiers keys + coverage rows, deliberately deferred) · display names shipped as
Vigor/Focus/Mana — veto if unwanted.
