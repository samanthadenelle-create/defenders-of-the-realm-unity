# WORK ORDER 739 — Generic Obsidian Building-Upgrade Tier Panel (Enhancement Path)

**Status:** READY TO IMPLEMENT
**Minted:** 2026-07-17 from the `CLI_LANES_WO_NUMBERS.md` banner (this mint bumps next-free to 740)
**Seat:** UI/design (Cowork session, owner-directed) — spec only, no code touched
**Owner (PO):** Sam — felt-verify + close
**Mockup (canonical reference):** `docs/UI_Mockups/building_upgrade_obsidian_template.html`
(also Claude project "Defenders" `designs/building-upgrade-obsidian-template.html`, desktop artifact `building-upgrade-obsidian-template`)
**Related:** WO-714 (Obsidian conformance program) · WO-675 (`ff.buildingupgradepanel` redesign flag) · WO-680/UPG-1 (tier-gate legibility, edit-complete) · WO-737 (Barracks Train panel — sibling surface, do not duplicate) · WO-738+ phase-2 (model swaps, synergies)
**Canon:** `docs/UI_BLINK_TEMPLATE_CANON.md` (BINDING) · `docs/UI/Grok-02-Obsidian-UI-guidance.md` · `docs/design/BUILDING_UPGRADE_TREES.md` rev 2 (AUTHORITATIVE, owner 2026-07-16) · `docs/design/BUILDING_PERKS_DESIGN.md` (effect mapping + schema gap) · `docs/SME/BLINK_SME.md` §1.4/§2.2

---

## 1. Goal

Restyle `BuildingUpgradePanelMvvm` into the owner-approved **Enhancement Path** layout in TRUE
Obsidian chrome, as ONE generic template: **every upgradeable building opens the same panel and
passes only its data** (building-tiers row + VM). No per-building screens, no per-screen chrome.

Buildings served by this one panel (canon 1:1 ids, no rename):
`lumbermill` · `windmill` · `forge` · `armorer` · `arcane-tower` (· `barracks` upgrade tab —
the Train flow itself is WO-737, do not duplicate it here).

**Mobile-first:** NO hotkeys anywhere on this surface (owner directive 2026-07-17). Touch targets
only; bottom Close stays as the big tap target.

## 2. Layout (from the mockup — drop-zones per template canon)

- Chrome via `ElarionUiKit.BuildObsidianPanel(parent, title, ..., frameName)` — real frame sprite,
  content parented ONLY into `chrome.layout` zones. No second frame inside the body (transparent
  hosts only).
- **header:** building title (`font_title` Merriweather, gold text = content accent).
- **medallion:** building emblem.
- **close:** the ONE kit close (top-right notch) + footer Close button as the mobile tap target —
  pick per kit law; if only one is kept, keep the footer button (phone-first).
- **body:** wallet row (kit `BuildWalletRow`/`CurrencyChip`, gold primary, CompactNumber, no
  ellipsis) → `BuildTabRow` [Upgrade | Skills] (selected = plate + gold underline, never hue
  alone) → Enhancement Path stage: 3 tier cards side-by-side (T1..T3; T0 is the basic building,
  not a card) with per-tier building art, progression arrows (gold to the next unlockable step,
  steel beyond) → benefits/detail side panel (portrait phones: detail stacks below the selected
  card; the 3-card path row scrolls horizontally if needed).

## 3. Data contract (generic — this IS the scope)

Source of truth: `Assets/StreamingAssets/Data/Canonical/building-tiers.json` (via
`BuildingTierCatalog`) + `BUILDING_UPGRADE_TREES.md` rev 2 content. The panel is a pure READER of
the Upgradable capability — zero per-building code paths. Adding a building = adding its JSON row.

Per building: `buildingId, title, tabs, wallet, tiers[]`; per tier: `id, name, bonus, art/model
key, benefits[] (earned flag), cost[] {resource, amount}, requires {tier, name}`. State
(`owned | unlockable | locked`) is DERIVED by the VM from save data, never authored.

**Schema gap to close in this WO:** `BuildingTierDef` has only `costWood/costFood/costCrystal` —
**add `costIron`** so Armorer (Metal pool) tiers can charge Iron (flagged in
`BUILDING_PERKS_DESIGN.md`). Migration note: additive field, no save bump needed unless serialized
into GameState.

## 4. VM binding map (owner-supplied — bind exactly this, View reads VM only)

| Mockup element | VM source |
|---|---|
| Tier name ("Tier 1 — Sawmill") | `ItemVM.Name` |
| Effect line ("Wood gather rate +40%") | `EffectFor(id)` |
| Cost | `CostFor(id)` — returns "Unlocked" or a combined string like "900 Wood · 300 Crystals" |
| Owned / lit | `ItemVM.Equipped` |
| Gold affordance (buyable now) | `ItemVM.Affordable` |
| Locked + dim | `ItemVM.Locked` |
| Lock text ("Unlock 'Sawmill' to open Tier 2") | `ItemVM.LockReason` |
| "UPGRADES LUMBERMILL TO TIER 2" sub-line | `KeyLineFor(id)` |
| Which gate blocks it | `GateFor(id)` → village / building-tier / cost / "" |
| Tile icon | `ItemVM.IconRole` + `IconKey` (role key only — see gap 1) |

Gap 1 (icon): tile art resolves through `RpgUiCatalog` role+key with null-safe procedural
fallback — never a hard path, never `Assets/Blink/**`.

MVVM law: View never reads game state (no `EconomyService.Instance` in the View); VM never
references `GameObject/Image/Sprite/RectTransform`.

## 5. Obsidian styling rules (bind to kit, not literals)

- Forged-steel chrome from the real frame sprite; **gold = accents & content only** (currency,
  bonuses, unlockable rim, CTA, tab underline) — never chrome outline. Flat black+gold = the
  unstyled-fallback failure mode; if the panel reads that way, the frame sprite is missing/masked
  (alpha-0 any decorative SolidFill).
- Fonts: `font_title` (Merriweather) titles · `font_body` (Alata) labels/numbers · `font_stamp`
  (Acme) TIER labels / section stamps. **ASCII-only TMP strings.**
- States: unlockable = gold plate ~a0.2 + gold rim glow + gold `Upgrade` CTA (`ButtonGold`);
  owned = green (Affordable) plate + "Built" chip; locked = desaturate art / dim ~a0.5 + lock
  chip carrying the SPECIFIC requirement from `LockReason` — never a bare "LOCKED"; locked stays
  mostly in color to sell progression.
- Affordability: `Affordable` green + explicit text; meaning by icon + text + position, never
  red-vs-green alone (owner colorblind). No red/green number flash — count-tween only.
- Buttons via `BuildObsidianButton` family; tabs via `BuildTabRow`; wallet via
  `BuildWalletRow`/`CurrencyChip`; currency icons stay OURS (no `Icons_Obsidian` bulk swap).

## 6. Files to edit

- `Assets/_Modules/Village/**/BuildingUpgradePanelMvvm.cs` — layout rebuild into zones (tier
  path + detail), bind the §4 map. Keep `[Flow:Upgrade]` traces (open, select, unlock, gate-deny).
- `Assets/_Modules/Village/**/BuildingUpgradeVM.cs` — ensure `EffectFor/CostFor/KeyLineFor/
  GateFor/LockReason/Affordable/Equipped/Locked` cover the tier cards (extend, don't fork).
- `Assets/_Modules/**/BuildingTierCatalog.cs` — add `costIron`.
- `Assets/StreamingAssets/Data/Canonical/building-tiers.json` (+ the Resources twin — keep the
  dual copies in sync; DATAWEB drift is a known regression red) — tier rows for all buildings
  from `BUILDING_UPGRADE_TREES.md` rev 2 (T1..T3 names/effects; costs = placeholder balance, PO
  tunes).
- `ElarionUiKit.ZonesFor(frameName)` ONLY if a dedicated frame is adopted for this screen (else
  keep `FrameCore`) — zones tuned once there, never per screen.
- Remove hotkey hints/handlers from this panel (mobile-first).

## 7. Do NOT touch

- No UXML/UI Toolkit; no new chrome/widget systems; no per-screen frame restyle.
- No direct references to `Assets/Blink/**` (gitignored — mirrored `Resources/RpgUi` only).
- No `.unity` scene edits; no `Village.unity` ever.
- WO-737 Barracks Train panel scope; WO-738 echo spec; phase-2 systems (synergy engine, model
  swaps, offline accrual) — the JSON may CARRY their text (benefits lines) but this WO wires no
  new gameplay systems.
- `ff.buildingupgradepanel` stays the kill-switch; sprite-first with procedural fallback — panel
  never blank when pack art is absent.

## 8. Acceptance criteria

1. One panel class serves all six building ids purely from data; adding a test building row
   renders a correct third-party tree with zero code change.
2. All six open correctly from their `BuildingInteractable`; Upgrade/Skills tabs both live.
3. States render per §5 for every tier permutation (owned/unlockable/locked; affordable/short);
   lock chips always show the specific requirement; ASCII-only strings; no color-only meaning.
4. `costIron` charges correctly for Armorer tiers; ledger spend routed exactly as today
   (no EconomyService/GameState dual-pool drift introduced).
5. No hotkey UI anywhere on the panel; all interactive elements >= 44px touch targets; portrait
   phone layout stacks detail under the path row (Pi Browser target).
6. CompileGate `COMPILE_GATE_OK` + DataRegression baseline; `UiObsidianConformanceRegression`
   passes (no hand-rolled tabs/wallet/slots).
7. **Image pair** (owner verify law): graphics-enabled capture of each building's panel vs the
   mockup + Blink panel PNG, logged to `UI_REVIEW/INDEX.html`; owner felt-verify on phone closes.

## 9. Result protocol

On completion write `WorkOrders/WORK_ORDER_739.RESULT.md` (headless evidence + capture paths),
leave push HELD for owner; canon updates (KEY_FACTS, banner) ride in the same commit.
