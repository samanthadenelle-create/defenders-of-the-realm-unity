# WORK ORDER 737 — Barracks Train Panel: Proper Obsidian Layout Spec

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at TroopDef.cs:124 + TroopUnlock.cs:34-80 + TroopTrainingPanel.cs:103-445 + TroopRosterRegression wired at DataRegression.cs:313.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Priority:** P0 (UI law — unlock ladder is unreadable without this layout)  
**Silo:** UI / Obsidian conformance  
**Depends on:** WO-732 (roster data; may stub 2 troops if 732 not landed), WO-733 unlock helper preferred  
**Parallel-safe with:** WO-734, WO-735  
**Blocks:** WO-724 felt-pass quality bar; WO-736 UI pair-pass  
**Program:** Troop roster `WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md` + CoC `723–731`  
**Effort:** M–L  
**Audience:** Claude (UI seat) + CLI  

---

## Goal

Specify and implement a **canonical Obsidian master-detail layout** for the Barracks **Train** screen so:

1. The **full troop ladder** (default + locked upgrades) is legible.  
2. Zones, buttons, wallet, lock states, and fonts follow **Blink/Obsidian factory law** — no bespoke chrome.  
3. Mobile-first (narrow left list, thumb-reachable CTAs).  
4. A future implementer can match **screenshot vs Blink Crafting template** without inventing layout.

This WO is the **layout contract**. Unlock math lives in WO-733; data roster in WO-732; this WO owns **where every pixel of content sits and which kit primitive builds it**.

---

## SME docs (READ FIRST — BINDING)

| Doc | Use for |
|-----|---------|
| `docs/UI_BLINK_TEMPLATE_CANON.md` | One master frame, drop-zones only, no per-screen chrome, code-built uGUI only |
| `docs/UI/Grok-02-Obsidian-UI-guidance.md` | Factory map, FrameCrafting recipe, wallet/button contracts, failure modes |
| `docs/UI/OBSIDIAN_UI_DESIGN_skilltree_inventory.md` §3.3 | **Locked / selected / owned** cell state styling (gold lock veil pattern) |
| `docs/BLINK_UI.md` | Pack roles if needed |
| `docs/HUD_OBSIDIAN_ARCHITECTURE_2026-07-03.md` | Only if wallet/HUD cross-talk; do not put Train on HUD frames |

**One-line law (canon §0):** *The Blink frame IS the chrome. Screens NEVER restyle — they DROP chrome-less content into pre-styled drop-zones and bind the model.*

---

## Current state (code-verified)

`Assets/_Modules/Village/Hero/TroopTrainingPanel.cs` already intends FrameCrafting master-detail:

| Zone | Intent today | Gap vs proper Obsidian ladder UI |
|------|--------------|----------------------------------|
| Frame | `BuildObsidianPanel(..., FrameCrafting)` | Keep |
| `bodyLeft` | Flat list of Obsidian buttons (all troops equal) | No **locked / selected / affordable** row states; no tier badge; no portrait/icon chip |
| `bodyRight` | Title, owned, wounded, army cap, cost, hint, Train×1/×5 | No **lock reason block**; no role/stats block; stacked fractions risk overlap; no portrait well |
| `footer` | `BuildWalletRow` wood/iron/food/crystal | Good — keep kit wallet law |
| Close | Kit shared close | Good |
| Toast | `ShowToast` on train | Good; extend refuse reasons for **locked** |

**Does not yet implement** inventory-style locked veil or BuildingUpgrade-style “Locked — needs Tier N” plate.

---

## Frame + surface choice (LOCKED)

| Decision | Value | Why |
|----------|-------|-----|
| Frame | `RpgUiCatalog.FrameCrafting` | Canon map: crafting/jeweler = list + detail (`UI_BLINK_TEMPLATE_CANON` §6; Grok-02 §5) |
| Entry | `BuildObsidianPanel` (or Modal wrapper already used) | ONE factory |
| Panel title | `"Barracks — Train"` (or `"Train"` if title zone clips) | Header zone only |
| Medallion | Optional: Barracks / sword concept icon | Only if `layout.medallion` non-null; do not invent a second frame |
| No second outer frame | BINDING | Detail uses parchment `bodyRight` only — never nest `BuildObsidianPanel` |

---

## Zone map (authoritative layout)

Use kit-measured zones only (`ElarionUiKit.ZonesFor` / `FrameLayout`). **Never** parent content to full-screen 0..1 of the modal canvas outside chrome.

```
┌──────────────── FrameCrafting ────────────────┐
│ [medallion?]     BARRACKS — TRAIN        [X]  │  header
├──────────────────┬────────────────────────────┤
│ bodyLeft         │ bodyRight                  │
│ DARK LIST WELL   │ PARCHMENT DETAIL WELL      │
│                  │                            │
│  [row] Footman   │  Name + role chip          │
│  [row] Archer ●  │  Portrait / icon socket    │
│  [row] Spearman 🔒│  Owned / Recovering / Cap  │
│  [row] …         │  Stats (HP · DMG · RNG)    │
│                  │  Cost chips / cost line    │
│                  │  LOCK banner OR Train CTAs │
│                  │  Hint (1 line)             │
├──────────────────┴────────────────────────────┤
│ footer:  [Wood] [Iron] [Food] [Crystal]       │  wallet
└───────────────────────────────────────────────┘
```

### Zone → content (drop only chrome-less widgets)

| Zone | Content | Kit primitive |
|------|---------|----------------|
| `header` | Title only (factory-owned) | Do not rebuild title chrome |
| `bodyLeft` | Scrollable troop **rows** (all 7, sorted by unlock tier) | `BuildObsidianButton` **or** slot-row composite (see §Row anatomy) |
| `bodyRight` | Selected troop **detail card** | TMP via `EnsureFont`; CTAs via `BuildObsidianButton` |
| `footer` | Wallet | `BuildWalletRow` + `CurrencyChip` only — **no hand-formatted wallet string** (WO-697) |
| `medallion` | Optional structure icon | `ConceptIconResolver` / kit medallion icon if already used |

**Panel rect (mobile-safe defaults — keep unless clipping):**  
`min ≈ (0.10, 0.08)`, `max ≈ (0.90, 0.92)` as today — landscape-friendly; on very narrow phones ensure left list row height ≥ 44px touch.

---

## bodyLeft — row anatomy (LOCKED states)

Mirror **inventory locked/selected** semantics (`OBSIDIAN_UI_DESIGN_skilltree_inventory.md` §3.3) and upgrade locked plates.

Each row is one selectable list item. **Show locked troops** (ladder education) — never hide them.

### Row layout (inside each list cell, L→R)

```
[ icon 36–44px ]  DisplayName          [ tier chip | lock ]
                  short role (melee…)     owned "×N" optional
```

| Element | Spec |
|---------|------|
| Icon | `iconId` / role fallback (`sword`, bow concept); dim α **0.5** when locked |
| Name | `DisplayName` only — **never raw id** (WO-714 P10) |
| Role line | Optional 11–12sp dim: `melee` / `ranged` |
| Owned badge | `×N` if owned > 0 (ink dim) |
| Tier chip | If locked: `T{n}` or lock glyph; if unlocked selected: none or check |
| Selected | **Yellow** Obsidian button color (or gold rim if using slot plate) |
| Unselected unlocked | **Gray** Style1 |
| Locked unselected | Gray + **LockedTint** plate (see BuildingUpgradePanelMvvm `LockedTint` ≈ grey 0.52, a 0.80) + lock chip |
| Locked selected | Still selectable; detail explains unlock — do **not** use Green |

**Sort order:** `UnlockBarracksTier` ASC, then catalog order.  
**Cap list overflow:** vertical scroll if > ~6 rows (ScrollRect on bodyLeft only; do not scroll whole frame).

### Row interaction

| Input | Behavior |
|-------|----------|
| Tap unlocked | Select → rebuild detail; Train CTAs enabled if affordable |
| Tap locked | Select → rebuild detail with lock banner; Train disabled |
| Double-tap | Not required |

---

## bodyRight — detail anatomy (vertical stack)

Use **fraction Y bands** that do not overlap (fix if fonts change). Suggested bands for parchment ink (`Ink` / `InkDim` / `InkGood` / `InkBad` family already in panel):

| Band (Y max→min, approx) | Content | Notes |
|--------------------------|---------|--------|
| 0.92–0.99 | **DisplayName** | Bold title; `EnsureFont` title role if available |
| 0.86–0.91 | **Role · Slots · Unlock** | e.g. `Melee · 2 slots · Barracks T3` |
| 0.72–0.85 | **Portrait / icon socket** | Optional large icon; empty slot art if no portrait (`slot_item` / character slot) |
| 0.64–0.71 | **Owned · Recovering** | Recovering in InkBad italic if > 0 |
| 0.58–0.63 | **Army cap** | `Army: used / max slots` |
| 0.48–0.57 | **Combat stats** | One line: `HP {n}  ·  DMG {n}  ·  Range {n}` (or two lines) |
| 0.38–0.47 | **Cost** | Cost line tinted Good/Bad by afford; prefer chip row if free |
| 0.28–0.37 | **STATE BLOCK** | See below — lock **or** train affordance |
| 0.16–0.26 | **Hint** | Single static line (ladder education or DetailHint) |
| 0.03–0.14 | **CTA row** | Train / Train ×5 |

### STATE BLOCK (mutual exclusive)

**A — Locked (not yet unlocked by Barracks tier)**

```
┌─────────────────────────────────────┐
│  LOCKED                             │  dim plate / parchment veil α~0.45
│  Unlocks at Barracks Tier {n}       │
│  "{TierName}"                       │  from BuildingTierCatalog if available
│  Upgrade the Barracks to recruit.   │
└─────────────────────────────────────┘
```

- Train buttons: **Gray, non-interactable** (or hidden — prefer visible disabled for mobile discovery).  
- Toast if somehow invoked: `"{Name} unlocks at Barracks Tier {n}."` Danger tone.

**B — Unlocked but cannot train (cap / resources)**

- Cost line InkBad and/or cap line InkBad.  
- Train Gray disabled.  
- Toast on tap attempt: existing cap/resources message (keep).

**C — Unlocked and can train**

- Train **Green** Style1; Train ×5 Green or Yellow secondary.  
- Success toast Confirm (existing).

**D — Unlocked, affordable, but wounded-only army noise**

- Still allow train new units; recovering line stays informative only.

### CTA rules (colorblind-safe)

| Button | Color when ready | Color when blocked | Label |
|--------|------------------|--------------------|-------|
| Primary | Green | Gray + non-interactable | `Train` (×1) |
| Secondary | Green or Yellow | Gray | `Train x5` |
| Meaning | **Text + enabled state**, not color alone | Grok-02 §4.2 |

Do **not** use red buttons for lock (red = destructive in kit grammar).

---

## Footer wallet (LOCKED contract)

- `ElarionUiKit.BuildWalletRow` with Wood, Iron, Food, Crystal (food matters for roster costs).  
- Chips own CompactNumber — **currency-ellipsis forbidden**.  
- Subscribe `EconomyService.OnChanged` → chip `SetAmount` only.  
- Footer must not be covered by Train CTAs (bodyRight CTA band stays above footer; kit zones already reserve footer — respect `bodyRight` bottom ≥ footer top).

---

## Typography & ink (parchment convention)

| Role | Color | Use |
|------|-------|-----|
| Ink | `(0.16, 0.12, 0.08)` | Titles, primary text on parchment |
| InkDim | `(0.34, 0.28, 0.20)` | Meta, hints |
| InkGood | green-brown | Affordable cost |
| InkBad | red-brown | Unaffordable / recovering / refuse |
| On dark list | Parchment / kit button text | Let `BuildObsidianButton` own label color |

Fonts: `ElarionUiKit.EnsureFont` title/body roles when available; never random TMP defaults that break WebGL.

---

## MVVM / logic split (recommended, not mandatory rewrite)

**Prefer:** thin view + small presenter or static queries:

| Concern | Owner |
|---------|--------|
| Roster list + sort | `TroopCatalog` + unlock helper (WO-733) |
| `IsTrainable` / `LockedReason` | `TroopUnlock` (WO-733) |
| Train / spend | `TroopDialogueCommands.Train` |
| View | `TroopTrainingPanel` — **only** projects state into zones |

If time-boxed: keep panel as dumb skin but **zero** unlock math inlined twice.

---

## Mobile / touch

- Row min height **≥ 0.10–0.12** of bodyLeft (or 48px).  
- CTA height ≥ 0.10 of detail.  
- Scrim + tap-outside close already via kit — keep.  
- sortingOrder 31000 + overrideSorting (already) so panel sits above HUD.  
- No drag-required-only controls; scroll OK on list.

---

## Accessibility / copy rules

- ASCII-safe labels (device tofu risk).  
- Never show `troop-footman` raw.  
- Lock copy always includes **tier number + unit fantasy name**.  
- Toast covers: trained OK · cap/resources · **locked tier** · barracks feature locked (`ff.barracks`).

---

## Pair-walk acceptance (owner method — BINDING)

After implement, capture Train panel (graphics build or Editor Game view) and compare to:

- Blink **Crafting_Panel** / Obsidian crafting template (`Assets/Blink/...` local; or mirrored `Resources/RpgUi/frame/...`)  
- Inventory **locked cell** feel (veil + still readable name)  
- Building upgrade **locked perk** reason plate  

Checklist:

- [ ] Real FrameCrafting art visible (not flat procedural-only as primary).  
- [ ] Two-tone: dark list | parchment detail.  
- [ ] ONE close control.  
- [ ] Wallet chips in footer only.  
- [ ] Locked rows visible with lock treatment.  
- [ ] Selected locked troop shows STATE BLOCK A, no green Train.  
- [ ] Selected unlocked affordable shows green Train.  
- [ ] No double frame / no UXML / no hand-drawn gold boxes as chrome.

---

## Tasks (implementation order)

1. **Read SME docs** listed above + current `TroopTrainingPanel.Open/Rebuild/BuildDetail`.  
2. **Wire unlock projection** (call WO-733 helper if present; else temporary stub: all unlockTier≤1 open).  
3. **Rebuild bodyLeft** with row states (selected / locked / unlocked).  
4. **Rebuild bodyRight** with non-overlapping bands + STATE BLOCK A/B/C.  
5. **CTA + toast** paths for locked.  
6. **Scroll** if ≥7 rows.  
7. **Screenshot pair-walk** notes in RESULT.  
8. Brace/NUL + CompileGate.

---

## Acceptance

- [ ] Layout matches zone map; content only in kit zones.  
- [ ] All roster troops appear in left list (defaults + locked).  
- [ ] Lock / select / train affordance states match tables above.  
- [ ] Footer wallet kit-only; no ellipsis currency.  
- [ ] No UXML; no second `BuildObsidianPanel` nested.  
- [ ] Mobile: primary CTAs thumb-reachable; rows tappable.  
- [ ] RESULT includes before/after notes + which SME rules were applied.  
- [ ] CompileGate green.

---

## Not in scope

- Authoring 7 troop JSON rows (WO-732) — layout must work with 2 until then.  
- Barracks building-tiers economy rebalance (WO-734 copy only).  
- Final unique troop meshes (WO-735).  
- Raid deploy tray layout (separate surface; may later reuse row state language).  
- Changing `ZonesFor(FrameCrafting)` globally unless a measured bleed bug is proven (then fix in kit, not one-off hacks).

---

## Key files

| Action | Path |
|--------|------|
| EDIT | `Assets/_Modules/Village/Hero/TroopTrainingPanel.cs` |
| READ | `Assets/_Modules/Core/UI/ElarionUiKit.cs` (`FrameLayout`, `ZonesFor`, wallet, buttons) |
| READ | `Assets/_Modules/Core/UI/RpgUiCatalog.cs` |
| READ | WO-733 unlock helper when present |
| READ | `BuildingUpgradePanelMvvm.cs` locked plate reference |
| READ | SME docs table above |
| MAY EDIT | Tiny kit helper only if row composite is reused — prefer no kit fork |

---

## Claude seat — non-negotiables

1. **Be SME** on template canon before drawing rectangles.  
2. **Factory-first:** if a widget exists (`BuildObsidianButton`, `BuildWalletRow`, `ShowToast`, lock veil pattern), use it.  
3. **Presentation never invents game rules** — only displays unlock/afford/cap from services.  
4. **Instrument** open/rebuild failures with FlowTrace `"Barracks"` / Guard if list build can throw.  
5. **Pair-walk** against Crafting template, not against “what looks fine in isolation.”  
6. RESULT path: `WorkOrders/WORK_ORDER_737_barracks_train_obsidian_layout.RESULT.md`

---

## Wireframe (reference only — implement with kit, not raw Images for chrome)

```
LEFT (dark)                    RIGHT (parchment)
+------------------+           +---------------------------+
| [*] Footman   x3 |           | Shieldguard               |
| [*] Archer    x1 | selected  | Melee · 2 slots · T3      |
| [ ] Spearman  T2 |           | [==== portrait/icon ====] |
| [ ] Shield..  T3 | 🔒        | Owned: 0  Recovering: 0   |
| [ ] Outrider  T4 |           | Army: 4 / 10 slots        |
| [ ] BattlemageT5 |           | HP 180 · DMG 10 · R 2.2   |
| [ ] Legion    T6 |           | Cost: 60w 40i 15f   (red) |
+------------------+           | +-----------------------+ |
                               | | LOCKED  Barracks T3   | |
                               | | War College           | |
                               | +-----------------------+ |
                               | Upgrade Barracks to     |
                               | recruit this unit.      |
                               | [ Train ] [ Train x5 ]  |  (disabled gray)
                               +---------------------------+
FOOTER:  🪵 1200   ⚙️ 340   🌾 80   💎 12
```

---

## RESULT

`WorkOrders/WORK_ORDER_737_barracks_train_obsidian_layout.RESULT.md`
