# WORK ORDER 42 — Hero Select Screen: Two-Panel Redesign (rev 2)

**Status:** CLOSED — SUPERSEDED (owner-approved sweep 2026-08-09: two-panel multi-hero select vs the single-hero pivot, COMBAT_PIVOT_NORTHSTAR)
**Date:** 2026-05-26
**Author:** Owner creative direction — playtest feedback + reference screenshot
**Priority:** High — current layout lacks visual hierarchy; dragon not prominent;
              no brand identity in top zone

---

## Owner Direction

> "Top half: keep the dragon, make it more prominent. 'Defenders of the Realm /
> powered by SKR' at the top. Wallet Connect button centred in the dragon stage.
> Bottom half: completely different section — player roster / hero cards.
> Split: top = big dragon display, bottom = clean card-based hero selection
> with nav arrows either side of the cards."

---

## Current vs. Target Layout

**Current** — single vertical stack, homogeneous dark bg:
```
[ title + subtitle                  ]
[ card  |  card  |  card            ]
[ confirm button                    ]
```

**Target** — two distinct visual zones:
```
╔══════════════════════════════════╗
║  DEFENDERS OF THE REALM          ║  ← title at top of zone
║  powered by SKR                  ║  ← tagline
║                                  ║
║      DRAGON  (3D background)     ║  ~55% height — transparent UI layer
║                                  ║
║  [ ◈  Connect Wallet ]           ║  ← amber wallet-connect CTA, centred
╠══════════════════════════════════╣  ← amber separator line
║  ─── HERO SELECT ───             ║
║  ‹  [Knight] [Mage] [Ranger]  ›  ║  ~45% height — opaque dark panel
║        [ ENTER THE REALM → ]     ║
╚══════════════════════════════════╝
```

---

## Design Principles

**Dragon stage (top)**
- Fully transparent background — the Unity 3D dragon fills this space.
- "DEFENDERS OF THE REALM" sits at the very top of the zone (large, bold,
  centred). "powered by SKR" appears directly below it in amber, smaller.
- Wallet Connect button sits centred in the body of the stage — diamond glyph
  + "Connect Wallet" text, amber outline style (not a filled block), so it
  reads over the dragon without visually competing.
- No card content here — nothing else competes with the dragon.

**Hero roster panel (bottom)**
- A solid, noticeably different background: deep indigo `rgba(18, 12, 30, 0.97)`.
- Separated from the stage above by a thin amber separator element.
- Section eyebrow: "— HERO SELECT —" in amber uppercase.
- Card row flanked by `‹` / `›` navigation buttons — visible at all times;
  at launch with three heroes they are purely decorative / future-proofed for
  additional heroes added later.
- Three compact hero cards in a horizontal row.
- Cards show name, class role tag, and lore blurb. No ability lists.
- Confirm button anchored at the bottom of the panel.

**Card design**
- Taller glyph portrait block with element-colour ambient tint.
- Name in large bold type.
- Role tag in amber uppercase, 11px.
- Lore blurb in soft lavender, 2–3 lines max.
- Selected state: amber border + subtle amber glow tint on the portrait block.
- Unselected hover: soft violet border lift.

**Wallet Connect**
- UI element only in this WO. The button has `name="hero-wallet-connect"`.
- Actual wallet connection logic (Solana / SKR SDK) is out of scope here —
  the controller just needs to register a `clicked` callback for a future WO.

---

## 1. Revised `HeroSelectScreen.uxml`

Full replacement. Element `name` attributes that `HeroSelectController` queries
are unchanged — the controller needs no logic edits beyond the
`AddHeroSelectHint` removal in §3.

```xml
<?xml version="1.0" encoding="utf-8"?>
<!--
  HeroSelectScreen.uxml — hero-select screen, two-panel redesign (WO-42 rev 2).
  ============================================================================
  Top:    hero-dragon-stage  — transparent, lets the 3D dragon show through.
                               Branding block at the very top of the zone.
                               Wallet-connect CTA centred in the stage body.
  Bottom: hero-roster-panel  — opaque dark indigo, amber separator,
                               nav arrows + hero card row + confirm CTA.

  HeroSelectController binding contract (names must not change):
    hero-select-root       VisualElement — full-screen container
    hero-select-title      Label         — "Defenders of the Realm"
    hero-select-subtitle   Label         — "powered by SKR"
    hero-card-row          VisualElement — card row built at runtime
    hero-select-confirm    Button        — disabled until a hero is picked
    hero-wallet-connect    Button        — wallet CTA (logic in future WO)
    hero-nav-prev          Button        — ‹ previous card (future carousel)
    hero-nav-next          Button        — › next card (future carousel)
-->
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="SelectScreen.uss" />

    <ui:VisualElement name="hero-select-root" class="select-root">

        <!-- ── TOP: Dragon stage — transparent, 3D scene shows through ── -->
        <ui:VisualElement name="hero-dragon-stage" class="hero-dragon-stage">

            <!-- Branding block — anchored at the top of the stage. -->
            <ui:VisualElement name="hero-brand-block" class="hero-brand-block">
                <ui:Label name="hero-select-title"
                          text=""
                          class="select-title hero-brand-title" />
                <ui:Label name="hero-select-subtitle"
                          text=""
                          class="select-subtitle hero-brand-subtitle" />
            </ui:VisualElement>

            <!-- Wallet connect — centred in the stage body. -->
            <ui:Button name="hero-wallet-connect"
                       text="◈  Connect Wallet"
                       class="hero-wallet-btn" />

        </ui:VisualElement>

        <!-- ── SEPARATOR — amber hairline between the two zones ── -->
        <ui:VisualElement name="hero-section-divider" class="hero-section-divider" />

        <!-- ── BOTTOM: Hero roster panel — opaque dark indigo ── -->
        <ui:VisualElement name="hero-roster-panel" class="hero-roster-panel">

            <!-- Section eyebrow label. Static text. -->
            <ui:Label name="hero-roster-eyebrow"
                      text="— HERO SELECT —"
                      class="hero-roster-eyebrow" />

            <!-- Card area: prev arrow | cards | next arrow -->
            <ui:VisualElement name="hero-card-area" class="hero-card-area">

                <ui:Button name="hero-nav-prev"
                           text="‹"
                           class="hero-nav-btn hero-nav-btn--prev" />

                <!-- The three hero cards — built at runtime by HeroSelectController. -->
                <ui:VisualElement name="hero-card-row" class="select-card-row" />

                <ui:Button name="hero-nav-next"
                           text="›"
                           class="hero-nav-btn hero-nav-btn--next" />

            </ui:VisualElement>

            <!-- Confirm CTA. -->
            <ui:VisualElement name="select-footer" class="select-footer">
                <ui:Button name="hero-select-confirm"
                           text=""
                           class="select-confirm" />
            </ui:VisualElement>

        </ui:VisualElement>

    </ui:VisualElement>
</ui:UXML>
```

---

## 2. Revised `SelectScreen.uss`

Full replacement. New classes added at the bottom; existing shared classes
(used by `PetSelectScreen.uxml` too) are preserved but adjusted.

```uss
/*
 * SelectScreen.uss — hero-select + pet-select screens.  WO-42 rev 2.
 * -------------------------------------------------------------------------
 * Hero-select: dragon-stage (transparent top, brand + wallet) +
 *              hero-roster-panel (opaque bottom, nav arrows + cards).
 * Pet-select:  unchanged single-stack layout (reuses select-card / select-confirm).
 */

/* ── Root — full-screen, no padding, pure black base ────────────────── */
.select-root {
    flex-grow: 1;
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background-color: rgb(6, 4, 12);
    flex-direction: column;
    align-items: stretch;
    justify-content: flex-start;
}

/* ════════════════════════════════════════════════════════════════════════
   HERO SELECT — two-panel layout
   ════════════════════════════════════════════════════════════════════════ */

/* ── Dragon stage — top 55%, transparent so the 3D scene shows through ── */
.hero-dragon-stage {
    flex: 0 0 55%;
    background-color: rgba(0, 0, 0, 0);
    flex-direction: column;
    justify-content: space-between;   /* brand block top, wallet btn bottom */
    align-items: center;
    padding-bottom: 20px;
}

/* ── Brand block — top of the dragon stage ──────────────────────────── */
.hero-brand-block {
    align-items: center;
    padding: 20px 32px 12px 32px;
    /* Soft gradient wash so the title is legible against the dragon */
    background-color: rgba(6, 4, 12, 0.55);
    align-self: stretch;
}

.hero-brand-title {
    font-size: 38px;
    -unity-font-style: bold;
    color: rgb(237, 233, 250);
    -unity-text-align: middle-center;
}

.hero-brand-subtitle {
    margin-top: 4px;
    font-size: 13px;
    -unity-font-style: bold;
    color: rgb(245, 166, 35);
    -unity-text-align: middle-center;
    letter-spacing: 2px;
}

/* ── Wallet connect button — centred in the stage body ───────────────── */
.hero-wallet-btn {
    min-width: 200px;
    height: 44px;
    font-size: 14px;
    -unity-font-style: bold;
    border-radius: 22px;
    border-width: 2px;
    border-color: rgb(245, 166, 35);
    background-color: rgba(245, 166, 35, 0.08);
    color: rgb(245, 166, 35);
    -unity-text-align: middle-center;
}

.hero-wallet-btn:hover {
    background-color: rgba(245, 166, 35, 0.20);
}

/* ── Amber separator hairline ────────────────────────────────────────── */
.hero-section-divider {
    flex: none;
    height: 2px;
    background-color: rgb(245, 166, 35);
    align-self: stretch;
}

/* ── Hero roster panel — bottom 45%, opaque deep indigo ─────────────── */
.hero-roster-panel {
    flex: 1 0 0;                    /* takes all remaining height */
    background-color: rgba(18, 12, 30, 0.97);
    flex-direction: column;
    align-items: center;
    justify-content: space-between;
    padding: 16px 12px 20px 12px;
}

/* "— HERO SELECT —" eyebrow */
.hero-roster-eyebrow {
    font-size: 12px;
    -unity-font-style: bold;
    color: rgb(245, 166, 35);
    -unity-text-align: middle-center;
    letter-spacing: 3px;
    margin-bottom: 10px;
    align-self: stretch;
}

/* ── Card area: arrow | cards | arrow ───────────────────────────────── */
.hero-card-area {
    flex-direction: row;
    align-items: center;
    justify-content: center;
    flex-grow: 1;
    align-self: stretch;
}

/* Nav arrow buttons */
.hero-nav-btn {
    width: 40px;
    height: 56px;
    font-size: 28px;
    -unity-font-style: bold;
    border-radius: 8px;
    border-width: 1px;
    border-color: rgba(196, 181, 253, 0.25);
    background-color: rgba(255, 255, 255, 0.04);
    color: rgba(196, 181, 253, 0.60);
    -unity-text-align: middle-center;
    flex-shrink: 0;
}

.hero-nav-btn:hover {
    border-color: rgba(245, 166, 35, 0.55);
    color: rgb(245, 166, 35);
    background-color: rgba(245, 166, 35, 0.06);
}

.hero-nav-btn--prev {
    margin-right: 8px;
}

.hero-nav-btn--next {
    margin-left: 8px;
}

/* ════════════════════════════════════════════════════════════════════════
   CARD ROW  (shared hero-select + pet-select)
   ════════════════════════════════════════════════════════════════════════ */

.select-card-row {
    flex-direction: row;
    justify-content: center;
    align-items: stretch;
    flex-grow: 1;
}

/* ── One hero card ───────────────────────────────────────────────────── */
.select-card {
    width: 240px;
    margin-left: 10px;
    margin-right: 10px;
    padding: 0;
    border-radius: 14px;
    border-width: 2px;
    border-color: rgba(124, 58, 237, 0.30);
    background-color: rgba(26, 18, 42, 0.98);
    overflow: hidden;
    flex-direction: column;
}

.select-card:hover {
    border-color: rgba(196, 181, 253, 0.65);
}

/* Selected card — amber ring, portrait tint lifted */
.select-card--active {
    border-color: rgb(245, 166, 35);
    background-color: rgba(38, 26, 52, 0.98);
}

/* Portrait block — large glyph / texture, element-tinted bg */
.select-card__portrait {
    height: 130px;
    align-items: center;
    justify-content: center;
    background-color: rgba(124, 58, 237, 0.10);
}

/* Amber-tinted portrait when card is active */
.select-card--active .select-card__portrait {
    background-color: rgba(245, 166, 35, 0.12);
}

/* Large glyph inside portrait block */
.select-card__glyph {
    font-size: 72px;
    -unity-font-style: bold;
    color: rgb(196, 181, 253);
}

/* Thin element-coloured strip between portrait and text */
.select-card__accent {
    height: 4px;
}

/* Text body */
.select-card__body {
    padding: 12px 14px 16px 14px;
    flex-grow: 1;
}

.select-card__name {
    font-size: 20px;
    -unity-font-style: bold;
    color: rgb(237, 233, 250);
}

/* Role tag — amber, uppercase, tight spacing */
.select-card__role {
    margin-top: 3px;
    font-size: 11px;
    -unity-font-style: bold;
    color: rgb(245, 166, 35);
}

/* Lore blurb — soft lavender, small, wrapping */
.select-card__blurb {
    margin-top: 8px;
    font-size: 12px;
    color: rgba(186, 178, 210, 0.85);
    white-space: normal;
}

/* ════════════════════════════════════════════════════════════════════════
   FOOTER + CONFIRM BUTTON  (shared)
   ════════════════════════════════════════════════════════════════════════ */

.select-footer {
    margin-top: 14px;
    align-items: center;
    align-self: stretch;
}

.select-confirm {
    min-width: 260px;
    height: 52px;
    font-size: 16px;
    -unity-font-style: bold;
    border-radius: 12px;
    border-width: 0;
    color: rgb(22, 14, 6);
    background-color: rgb(245, 166, 35);
}

.select-confirm:hover {
    background-color: rgb(255, 186, 70);
}

/* Before a card is chosen — visually muted */
.select-confirm--disabled {
    color: rgba(237, 233, 250, 0.35);
    background-color: rgba(255, 255, 255, 0.06);
}

/* ════════════════════════════════════════════════════════════════════════
   PET SELECT — inherits single-stack from pre-WO-42 layout
   (select-root, select-header, select-card-row, select-footer unchanged)
   ════════════════════════════════════════════════════════════════════════ */

/* Header — used by PetSelectScreen only (HeroSelect uses hero-brand-block) */
.select-header {
    align-items: center;
    margin-bottom: 28px;
    padding: 24px 24px 0 24px;
}

.select-title {
    font-size: 34px;
    -unity-font-style: bold;
    color: rgb(237, 233, 250);
    -unity-text-align: middle-center;
}

.select-subtitle {
    margin-top: 8px;
    font-size: 15px;
    -unity-font-style: italic;
    color: rgb(168, 160, 188);
    -unity-text-align: middle-center;
    white-space: normal;
    max-width: 720px;
}
```

---

## 3. `HeroSelectController.cs` — small cleanup + wallet stub

### Remove `AddHeroSelectHint`:
```csharp
// In BindElements() — DELETE this call:
AddHeroSelectHint(_root);

// DELETE the entire method:
private static void AddHeroSelectHint(VisualElement root) { … }
```

### Add wallet-connect stub + nav button queries (no-op for now):
```csharp
// Add to the name constants block:
private const string WalletConnectName = "hero-wallet-connect";
private const string NavPrevName       = "hero-nav-prev";
private const string NavNextName       = "hero-nav-next";

// In BindElements(), after _confirmButton is resolved:
var walletBtn = _root.Q<Button>(WalletConnectName);
if (walletBtn != null)
    walletBtn.clicked += OnWalletConnectClicked;

var navPrev = _root.Q<Button>(NavPrevName);
var navNext = _root.Q<Button>(NavNextName);
// Nav buttons are wired to cycle hero selection (future carousel expansion).
// For now, at 3 heroes they wrap: prev/next just move the selection ring.
if (navPrev != null) navPrev.clicked += () => CycleHero(-1);
if (navNext != null) navNext.clicked += () => CycleHero(+1);

// Add these methods:
private void OnWalletConnectClicked()
{
    // TODO WO-??: wire SKR / Solana wallet SDK here.
    Debug.Log("[HeroSelectController] Wallet connect tapped — not yet wired.");
}

private void CycleHero(int direction)
{
    if (HeroCatalog.Heroes.Length == 0) return;
    int current = _hasSelection
        ? System.Array.FindIndex(HeroCatalog.Heroes, h => h.Hero == _selectedHero)
        : -1;
    int next = ((current + direction) % HeroCatalog.Heroes.Length
               + HeroCatalog.Heroes.Length) % HeroCatalog.Heroes.Length;
    OnCardClicked(HeroCatalog.Heroes[next].Hero);
}
```

No other logic changes. All existing `_root.Q<>()` calls use the same element
names as before — the binding contract is unchanged.

---

## 4. `en.json` string keys (no new keys needed)

The controller already fills `hero-select-title` from `heroSelect.title` and
`hero-select-subtitle` from `heroSelect.subtitle`. Update those values:

```json
"heroSelect.title":    "Defenders of the Realm",
"heroSelect.subtitle": "powered by SKR"
```

---

## Files to Edit

| File | Change |
|---|---|
| `Assets/_Modules/Onboarding/UI/HeroSelectScreen.uxml` | Full replacement — two-panel layout with brand block + wallet btn + nav arrows |
| `Assets/_Modules/Onboarding/UI/SelectScreen.uss` | Full replacement — dragon-stage brand/wallet classes; nav arrow classes; pet-select classes preserved |
| `Assets/_Modules/Onboarding/HeroSelectController.cs` | Remove `AddHeroSelectHint()`; add wallet stub + `CycleHero()` nav logic |
| `Assets/_Modules/Core/Localisation/en.json` | Update `heroSelect.title` + `heroSelect.subtitle` strings |

---

## Acceptance Criteria

- [ ] Top ~55% of the screen is fully transparent — the Unity 3D dragon is
      clearly visible and fills the upper portion
- [ ] "DEFENDERS OF THE REALM" appears in large bold type at the very top
      of the dragon zone, centred, with a soft dark wash behind it for legibility
- [ ] "powered by SKR" appears directly below the title in amber uppercase
- [ ] A "◈ Connect Wallet" amber-outline button sits centred in the body of
      the dragon stage — does not compete with the dragon visually
- [ ] A 2px amber horizontal line separates the two sections
- [ ] Bottom panel has a noticeably different (darker indigo) background —
      the two zones read as distinctly different areas at a glance
- [ ] "— HERO SELECT —" eyebrow label appears above the cards in amber
- [ ] `‹` and `›` nav buttons flank the card row; clicking them cycles the
      active-hero selection ring through all three heroes (wraps around)
- [ ] Three hero cards sit side-by-side: name, role tag, blurb visible
- [ ] Selecting a card (or using nav) shows an amber border + lighter portrait tint
- [ ] Confirm button is amber, full-width-ish, anchored at bottom of panel
- [ ] Pet-select screen layout is unchanged (reuses shared card/footer classes)
- [ ] No scene re-bake required — purely USS/UXML + one C# cleanup + en.json update
