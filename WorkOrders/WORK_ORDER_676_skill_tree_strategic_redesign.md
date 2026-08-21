**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-11
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-11) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 676 — Skill Tree: strategic redesign (content + Obsidian view)

**Status: READY TO IMPLEMENT** (owner rulings 2026-07-11, in-chat: (1) approved the icon-only
Obsidian redesign mockup — "yes so much clearer" / "I love it, exactly what this needed"; (2) design
pivot — **the tree is a STRATEGIC investment surface, not an execution-skill surface**).
**Lane:** Talents (data + View) + one-reader hooks in Economy/Defense systems. **Companion to:**
WO-614 (signature W/E/R rail — SEPARATE lane, unchanged) · WO-675 (upgrade panel redesign — shared
visual grammar). **Flag:** none needed for data (tree already ships); View changes ride the panel.
**Numbering:** confirm 676 vs `CLI_LANES_WO_NUMBERS.md`; mint the Notion row on claim.

## The owner's design law (BINDING for this tree, recorded verbatim intent)

> "Focus less on when things land perfectly — that's challenging in the game — and more on how a
> node benefits you and makes strategic choices. Passives like gathering resources, taking less
> damage, making your defensive structures more advanced."

Operationalized:
1. **No execution-timing procs.** Nothing keyed to perfect blocks/parry windows/reaction tests.
   Every node's value is always-on or clearly-triggered by a strategic action (start a wave, place
   a structure, collect).
2. **Every node answers "what kind of lord am I building?"** The tree spans the WHOLE game loop —
   hero combat, economy, defenses — so a Wisdom point is a real allocation choice between them.
3. **Solo-legible:** every description states its payoff in play terms ("your collectors hold more
   before raiders can steal it"), never party flavor.

## Verified state (read from code/data 2026-07-11 — do not relearn)

- `hero-talents.json` (dual-copy byte-equal ✓) is ALREADY solo: zero `ally:` nodes; Knight tree has
  ranged (Thunderbolt/Throwing Spear), burn DoT (Emberbrand), heals (Mending Salve/Oathmend/Second
  Wind), control (Shield Slam/Pinning Spear/Warden's Roar). WO-614's KEEP/CONVERT/REPLACE landed.
- **`HeroTalentModifiers` is the existing Σ-registry** (StatSum per effect type; HeroHealth.TakeDamage
  consumes damageReduction/blockChance/defense; laststand/reflect wired WO-566). New strategic
  passives = new StatSum types + ONE reader each. `HeroTalentCatalog.cs:34` lists the effect-type
  vocabulary — extend it there.
- Stub reality: several effects carry `(V2)`/`(V-later)` notes (e.g. Holy Retribution's burn;
  most Ranger/Mage effects). `ff.knightonly` hides Ranger/Mage — Knight + shared are the live scope.
- View: `HeroSkillTreePanelMvvm` (graph, 132px text-heavy nodes) — redesign approved per mockup.

## A. Tree content — three strategic branches (Knight + shared, data-only rows)

Branch identity (naming = owner pin #1; working names):
**WAR** (hero combat) · **STEWARD** (economy/harvest) · **BULWARK** (defensive structures).
Tier gating unchanged (Wisdom costs + VillageTier gate ties strategy to the WO-432 town arc).

### Keep (already right)
All current Knight actives + flat combat passives (Iron Resolve, Guardian Stance, Bulwark,
Legendary Vanguard) — always-on, strategic, no timing tests. Shared stat nodes keep.

### Remove / rework (violate the new law or dead)
- Any effect that is a dead stub in V1 gets **wired or hidden** (no dead nodes — see gate test G3).
  Holy Retribution: either wire the taunt-burn now (small) or replace with a Bulwark node.
- Retaliation Surge (reflect) stays — always-on, not timing-based — but re-describe in strategic
  terms ("attackers hurt themselves on your armor").

### New STEWARD nodes (effect type → consumer; all [NEW READER] unless noted)
| Node (working name) | Effect | Type → consumer |
|---|---|---|
| Provider's Bond | +15% echo/collector harvest rate | `harvestRate` → EchoService tick + ResourceBuildingHarvester accrual |
| Deep Reserves | Collectors hold +50% pending before full | `collectorCap` → ResourceCollector capacity |
| Master Mason | Repairs cost 25% less | `repairCost` → the WO-672 repair pricing path (one choke point) |
| Foreman's Pace | Build/upgrade timers 20% faster | `buildTime` → BuildTimerService duration calc |
| Salvager | Selling/losing a structure refunds +15% | `salvage` → BuildModeController sell refund + WO-672 destroyed-loss calc |
| Bountiful Banners (capstone) | Wave rewards +20% | `waveReward` → wave reward grant path |

### New BULWARK nodes
| Node | Effect | Type → consumer |
|---|---|---|
| Keen Ballistics | Towers +15% damage | `towerDamage` → DefenseTower/ArcaneTower damage calc (shared base read) |
| Farsight Emplacements | Towers +2m range | `towerRange` → same seam |
| Hardened Ramparts | Walls/gates take 20% less damage | `structureToughness` → WallSegment/Gate damage intake (WO-672 lifecycle) |
| Standing Orders | Towers attack 12% faster | `towerAttackSpeed` → tower fire-rate |
| Warden of Elarion (capstone) | All defenses take 25% less damage during DEFEND | `structureToughness` scoped to wave-active → same intake read + WaveManager phase check |

### New WAR node
| Node | Effect | Type |
|---|---|---|
| Venombrand | Thunderbolt/Throwing Spear apply poison: 5 dps for 6s (stacks to 2) | `modifyAbility` rider — mirrors the Emberbrand burn shape; distinct tell (green drip trail vs fire), never color-only (add the drip icon) |

**Architecture rule (One Model, §2b):** each new effect type is a CAPABILITY summed by
`HeroTalentModifiers.StatSum`; each consumer system asks the registry ONCE at its existing choke
point (`?.` null-safe, default 0). No per-node code, no system references the tree — they read the
modifier surface. Cross-asmdef: economy/defense readers call the Core-side sum via the existing
seam pattern (mirror how HeroHealth consumes it); if an asmdef edge blocks a direct call, mirror
the value onto GameState-adjacent Core statics — never Village↔HUD.

## B. View redesign (the approved mockup, applied to this panel)

> **CLI design intent (owner, 2026-07-11, verbatim spirit):** the current panel WORKS — nothing is
> broken. The problem is BUSYNESS: too much competing text/chrome. The goal is visually correct
> **and aesthetically pleasing** — calm, readable, gothic-handsome. When a change trades polish
> for information density, choose polish: remove/relocate the text rather than shrink it. The
> "less on screen" test: every persistent element must justify itself; if the detail column can
> say it on select, the graph must not say it always. Judge each pass against the approved mockup
> + the Blink template PNG (canon §7), not just against "does it function."

1. **Icon-only nodes** (~96px): plate (talent border art, sprite-first ungated) + icon + ONE
   affordance: cost pip (unlockable), −n pip (planned, ring), check stamp (owned), dim (locked).
   ALL name/desc/state text moves to the detail column (it already exists). Branch/section
   dividers use the crown-glyph band grammar from WO-675.
2. **Consolidate the readouts:** Wisdom becomes a `CurrencyChip` (top-right); the plan summary
   folds into the Confirm label ("CONFIRM n · −cost"); quick-swap caption + instruction lines and
   respec status become transient toasts / detail-column state lines. Target: ≤2 persistent text
   strips outside the graph.
3. **Edges:** live path 4px gilt; inactive 1.5px at ~0.12 alpha (quiet the string-web).
4. **Detail column** stays; its state line doubles as the quick-swap hint when an owned skill is
   selected. Quick-swap slots keep numerals on slot art, lose the caption.
5. Verbiage/legibility pass over every kept node: strategic payoff wording, no party flavor.

## C. Gates (§2c/§12)

- **G1 EditMode:** hero-talents.json parses; dual copies byte-equal; every node's effect `type` ∈
  the `HeroTalentCatalog` vocabulary.
- **G2 modifier math:** StatSum unit tests per new type (stacking, clamps — e.g. structureToughness
  cap 0.5, harvestRate cap sane).
- **G3 NO DEAD NODES (new, from the owner's stub complaint):** an EditMode gate asserting every
  SHIPPED node's effect type has a REGISTERED consumer (a static registry of implemented types);
  a node whose type is unimplemented must carry `"hidden": true` or fail the gate. This
  mechanizes wire-or-hide, forever.
- **G4 regression:** DataRegression rows for the new types; fleet probe — unlock Provider's Bond
  headless → assert the harvest tick rate changed (one end-to-end proof per branch).
- **G5:** COMPILE_GATE_OK + screenshot-vs-template (canon §7) + owner felt-pass on the redesigned
  panel.

## What NOT to touch
- WO-614's signature rail (W/E/R) — separate lane, do not fold in.
- `HeroSkillTreeVM` plan/commit logic (View + data + modifier reads only).
- Ranger/Mage trees (gated off; re-audit when classes unlock).
- Tower/wall CATALOG stats (the modifier multiplies at the consumer; catalog rows stay authored).
- §0 Windows path; sole committer; push held.

## Owner pins
1. Branch names: War / Steward / Bulwark — or her picks.
2. Numbers (all first-pass; tune vs economy after felt-pass).
3. Holy Retribution: wire the burn now, or swap for a Bulwark node?
4. Does Warden of Elarion (defense capstone) feel better as always-on -15% instead of
   DEFEND-scoped -25%? (Scoped is stronger + more thematic; always-on is simpler to read.)

*Cross-refs:* mockups approved in-chat 2026-07-11 · WO-614 · WO-675 · WO-672 (repair/damage seams) ·
`docs/COMBAT_PIVOT_NORTHSTAR.md` (skill tree = where dead skill points pay off) ·
`HeroTalentModifiers.cs` (the Σ-registry) · `docs/UI_BLINK_TEMPLATE_CANON.md`.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
