**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 675 — Building Enhancement Panel: Obsidian/Talent redesign

**Status: READY TO IMPLEMENT** (owner approved the mockup 2026-07-11: "yes so much clearer").
**Lane:** UI / HUD (View-only — VM untouched). **Flag-safe:** all changes are presentation inside
`BuildingUpgradePanelMvvm`; `FeatureFlags.BuildingUpgradePanel` stays the ship gate.
**Canon:** `docs/UI_BLINK_TEMPLATE_CANON.md` (BINDING) · `docs/MASTER_CATALOG/BLINK.md` ·
verbiage law ("Unlock perk"/"Enhancement") · one-action-one-button · colorblind law (never hue alone).
**Owner ruling recorded:** the Obsidian re-skin IS the desired panel look — closes BLINK.md open
question 6 for panel surfaces.
**Mockup:** approved in-chat 2026-07-11 (landscape talent frame, tier bands, chip footer).
**Numbering:** confirm 675 against `CLI_LANES_WO_NUMBERS.md` + mint the Notion row on claim.

## Why

The panel is canon-compliant (MVVM, master factory, one Close) but uses the wrong frame and leaves
already-mirrored Obsidian parts unused. Every change below is a re-skin through existing seams —
zero new architecture, likely zero new sprite imports.

> **CLI design intent (owner, 2026-07-11):** the current panel WORKS; the failure is aesthetic —
> too busy, too much going on. Target = visually correct **and pleasing**: fewer persistent text
> strips, information moved to where it's asked for (tiles/detail on tap, toasts for transients),
> the ornate frame doing the decorating instead of our labels. Prefer removing an element over
> shrinking it. Verify against the approved mockup + template PNG (canon §7), not function alone.

## Verified facts (read from code 2026-07-11)

- `frame_talent`, `panel_talent`, `slot_talent_1..6`, `talent_1..4`, `deco_talent_1/2`,
  `crown/tier1..3`, `currency_{wood,food,iron,crystal,gold}`, `rarity_1..5`, button families —
  ALL already in committed `Assets/Resources/RpgUi/` (no importer run needed unless a name 404s).
- `ZonesFor(RpgUiCatalog.FrameTalent)` is pixel-measured (ElarionUiKit.cs:~366; medallion + header
  + body; landscape 2779×1843). Used today by HeroSkillTreePanelMvvm / HeroLoadoutPanelMvvm.
- `ElarionUiKit.CurrencyChip(parent, CurrencyKind, …)` exists (ElarionUiKitObsidian.cs:719,
  count-tween). `CurrencyChipHandle` at :669.
- Current View: `BuildingUpgradePanelMvvm.cs` — FrameCore portrait column (:168-170), text wallet
  line (:134, :187), tier/village-tier rows as fake grid tiles (:358-366, VillageTierRowId),
  `DressTilePlate` blinkchrome-gated (:427-443), gold `Outline` affordance (:329-334), local
  `PanelOpenCloseFx` flagged for kit promotion (:493-549).
- VM (`BuildingUpgradeVM`) already exposes everything needed: `Perks` (ItemVM: Equipped/Locked/
  Affordable/LockReason/IconRole), `EffectFor`/`CostFor`, `VillageTierRowId`, tier ids, wallet
  values. **NO VM changes** — this is the dumb-skin half only. (If per-currency wallet visibility
  needs one accessor, add a read-only property — no logic moves.)

## Changes (all in `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs` unless noted)

1. **Frame → `FrameTalent`, landscape.** `BuildObsidianPanel(…, frameName: RpgUiCatalog.FrameTalent)`
   with a wide anchor (mirror HeroSkillTreePanelMvvm's sizing). Keep `medallionIcon: "hammer"`.
   Title unchanged ("<Building> Enhancements").
2. **Tier BANDS replace tier tiles.** Group `vm.Perks` by tier. Each band = slim divider strip
   (crown tier glyph art `RpgUi/crown/tier{n}` + thin gilt rule + "TIER n" label) above a row of
   perk tiles (3 columns in the talent body; ScrollRect only if content overflows).
   - The synthetic `VillageTierRowId` tile is REMOVED from the grid: the locked tier's band header
     carries the requirement text + the ONE gold **Unlock · <cost>** action (routes to the same
     `vm.Select(VillageTierRowId)`). One action = one button holds: it's the band's only control.
   - Locked tiers still render their slot plates (dimmed, lock glyph) — sparse grid law.
3. **Tile plates → `slot_talent_*` sprite-first, ungated.** `DressTilePlate` uses
   `RpgUiCatalog.Get(RoleSlot, "slot_talent_1")` (pick the plate variant that reads best vs the
   template PNG) with the existing procedural fallback. Remove the `FeatureFlags.BlinkChrome`
   condition — §5 canon is sprite-first ALWAYS; the flag only gates hiding our chrome.
4. **Affordance ring → rarity rim sprite.** Replace the uGUI `Outline` component with a sliced
   `rarity_4` (or `rarity_5`) rim overlay image on the unlockable+affordable tile. Keep states:
   owned = lit plate + "UNLOCKED" stamp; locked = dim + requirement; unaffordable = "Need <cost>"
   (text cue stays — colorblind law).
5. **Wallet → CurrencyChip row in `layout.footer`.** Replace `_walletText` with
   `ElarionUiKit.CurrencyChip` per currency, footer zone. Show only currencies the open building's
   perks can spend (derive from the VM's cost data; fall back to all five if ambiguous). If
   FrameTalent's footer band clips (verify vs template), seat the chip row at the body base strip
   instead — tune in `ZonesFor` ONLY (canon §3), never per-screen.
6. **Status line → transient toast.** Route `vm.Status` changes to `BuildFeedbackToast.Show`
   (transient events only; no persistent strip). Drop `_statusText`.
7. **Cost lines get currency icons.** Tile bottom line renders `RpgUi/currency/*` icon + value
   (+ short name), replacing text-only "120 Wood".
8. **Promote `PanelOpenCloseFx` → `ElarionUiKit`** (already flagged in-code). Move the class,
   keep timings (0.18s/0.14s, unscaled), re-point this panel; other panels adopt on-touch (no
   big-bang sweep — ARCH §3).

## Acceptance

- [ ] Panel opens in the landscape Talent frame; medallion, header title, ONE shared Close (bottom
      band) — no per-screen chrome, content strictly inside zones.
- [ ] Perks grouped under tier bands with crown glyphs; locked tier band shows requirement + the
      single gold Unlock action; village-tier fake tile gone from the grid.
- [ ] Tile states read without hue: UNLOCKED stamp / gold rim + cost / "Need <cost>" / dim +
      requirement. Tap unlocks via `vm.Select` (unchanged).
- [ ] Footer shows currency chips (count-tween) — no text wallet line; nothing clipped.
- [ ] Screenshot-vs-template verify (canon §7): built-player or F8 capture compared against
      `Talent_Tree_Panel.png`; zone tuning only in `ZonesFor`.
- [ ] `COMPILE_GATE_OK` + fleet HUD panel probes stay green (13/13) + popup-close oracle clean +
      owner felt-pass.
- [ ] Sprite-miss safety: with `Resources/RpgUi` art absent, every element falls back procedural —
      panel never blanks (Guard/null-fallback paths exercised).

## What NOT to touch

- `BuildingUpgradeVM` / `BuildingUpgradeService` / `BuildingTierCatalog` — no logic, no state moves.
- `ZonesFor` cases other than FrameTalent (and only if the footer verify demands it).
- Other panels' chrome (FX promotion is additive; adoption is on-touch later).
- No UXML/UIToolkit anywhere (§8 law). No `Assets/Blink` direct references (gitignored) — only
  `RpgUiCatalog` ids. §0: Windows path, Write/Edit only.

*Cross-refs:* `docs/UI_BLINK_TEMPLATE_CANON.md` · `docs/MASTER_CATALOG/BLINK.md` (mirror inventory +
open-question-6 ruling) · HeroSkillTreePanelMvvm (FrameTalent sizing + node-state grammar precedent) ·
WO-432/WO-476 (the perk system this View skins).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
