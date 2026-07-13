# WORK ORDER 709 — Echo workforce: count-based global harvest multiplier + workforce HUD panel

**Status: SPEC — needs owner pins 1–4, then READY** (owner design paste + "design a HUD that
matches", 2026-07-13; HUD mockup approved-in-chat pending her reaction).
**Lane:** Economy/Harvest + HUD. **Type:** design evolution of BUILT systems (EchoService,
EchoWorkforceHud, WO-587 slot growth). **Minted from banner: 709; banner bumped to 710 in the
same edit.**

## The mechanic (owner design, canon-reconciled)

**Each new Echo amplifies the ENTIRE harvesting operation** — unlocking one is a global power
event, not an incremental lane:

- 1 Echo → global ×1 · 2 → ×2 · 3 → ×3 · 4 (cap) → ×4, applied to ALL harvest income
  (echo lanes + collector accrual + offline) through ONE modifier read (the
  `HeroTalentModifiers.StatSum` pattern — a single `echoGlobalMult` every harvest tick
  consumes; no per-system math).
- Unlock driver: wave milestones (5/10/15/20) — matches the v25 save-era rule
  (`wavesCompleted` unlocks next echo ≤4); reconcile with WO-587's population-milestone slots
  (pin #2: one driver must win; recommend waves as the owner's stated design, population XP
  retiring into flavor).
- Lane assignment (WO-658/WO-681 picker) STAYS — it chooses WHAT each echo gathers; the
  multiplier is the main power driver. Per-echo specialization bonuses stay minor.
- Cap-4 capstone (owner "make the final unlock feel epic"): +10% on top OR reduced node
  depletion (ties WO-657 finite reserves) — pin #3.
- Unlock moment: panel pulse + multiplier fly-up + sparkle on nodes + one voice line
  (owner draft says "Silas" — canon spelling is SYLAS, and the natural speaker is the WO-699
  STEWARD or the Tree itself — pin #4). All effects pooled via VFXManager keys (never
  Instantiate — §2b.2).

## CANON RECONCILIATION (BINDING — the paste used retired framing)

1. **These are ECHOES, not pets.** The harvesters are the Echo spirits (COMBAT_PIVOT canon +
   the approved Fall-and-Founding lore: the kept townsfolk). "Pet Companions / wolf / drake"
   framing is the retired pets-as-harvesters direction. All UI copy says Echo. **Pin #1: if
   the owner truly wants visible PET companions harvesting, that is a canon reversal she must
   rule explicitly — this WO assumes Echoes.**
2. Cap: the paste says 4; the northstar refinement said 5 (3 organic + 2 flex). v25 save data
   says ≤4. **This WO ships cap 4 per the owner's table** — the 5th/flex-echo idea retires
   unless she overrules (pin #2 covers driver + cap together).
3. **Multiplier math defined exactly** (the paste is ambiguous): `totalIncome =
   Σ(perEchoBaseRate) × echoCount`. With identical echoes that is quadratic in count
   (1→1, 2→4, 3→9, 4→16 × one echo's base) — steep; tune base rates down accordingly, OR the
   softer read `totalIncome = baseOperation × echoCount` (linear total, still "everyone works
   N× hard" in fiction). **CLI implements behind one constant-shape function; owner picks the
   curve at pin #3 with the table above in front of her.** EditMode tests lock whichever curve
   is ruled.

## The HUD panel (per the approved mockup — replaces/extends EchoWorkforceHud)

- **Global multiplier = the hero stat**: gold medallion chip top-right, "×N" (Cambria class)
  + "ALL HARVEST" label — never color-only (text carries it; green-at-max ALSO says "MAX").
- Per-echo rows: wisp icon plate · name · activity pips (●●●○○ = effectiveness) · live
  contribution "+45/min" with the mirrored currency icon · state word ("Harvesting"/"Idle").
- Locked rows: dashed plate + lock glyph + "Wave 10" + "Wakes: ×3 all harvest" (the next
  reward is always visible — the carrot).
- Footer: thin wave-progress bar "Waves to next echo 7/10".
- Collapsible (chevron) + compact for mobile; suppressed during DEFEND troop moments (panel
  registers with PanelManager conventions; MVVM — a WorkforceVM owns EchoService reads, the
  View is a dumb skin via the kit; Collect All keeps living here).
- Tap an echo row → the WO-681 echo card (one surface family).
- Tooltip/long-press on the medallion: "All echoes harvest at ×N. Total output +NNN%."

## Monetization hook (owner, 2026-07-13 — future slice, NOT this WO's scope)
**Echo special food = timed productivity boost**, fed by the free model's rewarded ads or by
packs ("perfect hook for ad revenue of free model or packs — feeding them special food
increases productivity for x duration"). Shape: a timed multiplier-on-the-multiplier
(e.g. +50% all-harvest for 30–60 min), earned via rewarded ad (WO-612 law: ad = skip/boost,
NEVER a wall) or bought in PackStore bundles. The GlobalHarvestMultiplier seam this WO ships
is the one read the buff plugs into. Mint its own WO when claimed; pairs with the WelcomeBack
popup + offline accrual for the "feed before you log off" ritual.

## Gates
- [ ] EditMode: multiplier curve math (ruled curve) + unlock thresholds; no dead effect types
      (G3 pattern); dual-copy on any new json rows.
- [ ] Fleet: simulate wave milestones headless → assert multiplier steps + income deltas match
      the ruled table; offline accrual honors the multiplier; save round-trip (schema bump if
      a new field — additive, default-on-read).
- [ ] `[Flow:Echo]` traces on multiplier changes; COMPILE_GATE_OK + REGRESSION_OK + owner
      felt-pass on the unlock MOMENT (it must feel like a power spike — that is the point).

## What NOT to touch
EchoService accrual plumbing beyond the one multiplier read · WO-658 lane picker semantics ·
Collect All spine (WO-663) · pet-slot legacy persistence (separate pre-exister).

## Owner pins
1. **RESOLVED (owner to CLI, 2026-07-13 evening, verbatim): "echoes (rebranded originally as
   pets)"** — the harvesters are ECHOES; the pet-warm presentation (names/creature icons) is
   flavor. No canon reversal.
2. Unlock driver (waves 5/10/15/20 as pasted, recommended) + cap: **OPEN — 4 (owner's table)
   vs 5 (shipped code MaxEchoes=5, HUD shows "1/5")** — one must win.
3. Multiplier curve: **RESOLVED — QUADRATIC-TOTAL** (owner answered "exactly" to CLI's
   explicit "2 companions = 4x a single" framing): totalIncome = Σ(perEchoBase) × echoCount;
   tune base rates down to compensate. Capstone flavor (+10% vs reduced depletion) still OPEN.
4. Unlock voice line speaker — **CREATIVE RECOMMENDATION (UI seat, 2026-07-13, at owner's
   "spirit of the tree? ask creative"): the WAKING ECHO speaks; the Tree answers with its
   CHORD + light pulse, never words.** Rationale: per the founding lore the Echoes ARE the
   tree's kept townsfolk — they are its voice; keeping the Heart wordless preserves its
   mystery (canon: the Heartwood's sustained note). Scales per unlock (each echo = a
   different remembered trade). Draft lines (owner voice pass):
   w5: "I kept the mill, once. Show me the fields — we'll work them together." ·
   w10: "Three of us now. It's almost like a morning in the old town." ·
   w15: "The others woke me. They said Elarion was warm again." ·
   w20 (cap): "All of us, together — watch what this city could do." (+ the Heart's light
   steps visibly brighter — the permanent max-workforce tell, shape/brightness not color-only).
   Ships as TEXT via the dialogue/toast rail (no VO dependency for V1). Steward stays the
   tutorial voice only. **Awaiting owner yes on speaker + lines.**

*Cross-refs:* mockup in-chat 2026-07-13 · ECHO_WORKFORCE_SPEC · WO-587 (slots) · WO-658/681
(assignment + card) · WO-657 (depletion) · WO-699 (Steward voice) · COMBAT_PIVOT_NORTHSTAR ·
docs/LORE_FALL_AND_FOUNDING_OF_ELARION.md (who the Echoes are).
