# WORK ORDER 1006 — Manage becomes a LAUNCHER; the upgrade catalog moves into per-category browser panels

**Status:** FIXED 2026-08-29 - progressive disclosure and the Defense build route are present in Seeker tester APK 2026.08.29.346849; awaiting owner device test. *(Prior: DONE — owner-confirmed 2026-08-21.)*

**2026-08-29 correction:** Manage categories now derive from authoritative current-town placements;
empty/locked categories are absent until the first relevant structure is placed. Only actionable
placed rows render, with the selected actions before queue history, explicit result counts and
Previous/Next paging when more than four structures qualify. A secondary **Build new** route remains
available for absent categories. Static gate and `DeNelle.Village.csproj` compile are green; status
is now device-present in APK 2026.08.29.346849 and awaits the owner's phone-viewport test.
**Minted:** 2026-08-08 (UI seat, owner directive) — number from `CLI_LANES_WO_NUMBERS.md` banner (UI block, bumped 1006 → 1007 in the same edit)
**Lane:** HUD/UI. Presentation + a read-only browse model. **No economy, timer, or catalog-data changes.**
**Provenance:** owner ask, verbatim below. Refines the owner's words into a spec (memory `grok-authors-work-orders` flow: owner → UI refine → CLI implement).
**Supersedes-in-part:** WO-905 §2.6/§3 (the inline scroll-list browse). The **affordability model, the affordable-first sort, and the tab→channel crossing** all survive and are REUSED — this WO relocates the presentation, it does not rewrite the logic.
**Depends on:** WO-911 (the `ManageScreenPanel` + `ManageScreenVM` this edits), WO-895 (`BuildingUpgradePanelMvvm`, the single-item drill-in target — reused, not rebuilt).
**Adjacent / reuse:** WO-807 (troop upgrade power readability), WO-821 (timed perk research), WO-432/476 (perk research tree).

---

## 1. The idea, in the owner's words (2026-08-08)

> *"i was wrong the scroll window is very long, can we use a separate panel for those with a button
> from manage panel for the different options so they can see more details about whats available to
> them costs benefits time to build?"*

Two things happened in one sentence:

1. **The earlier worry was wrong.** The Manage browse list is NOT empty — it populates and, on a
   built-up town, it is **very long**. (The `[Buildings]` tab reading "Nothing to upgrade on this tab
   yet" in the felt-test screenshot was the correct empty-state for a town whose built buildings are
   all at max tier / have no next tier — not a data failure.)
2. **The real problem is the shape.** One long combined scroll (IN QUEUE + every upgradeable item)
   inside the Manage modal is cramped, and each browse row today shows only **name + cost +
   affordability** — it cannot answer *"what does this actually get me, and how long will it take?"*

**The fix:** Manage stops being a giant list. It becomes a **launcher**. The long catalog moves OUT
into a **dedicated browser panel per category**, opened by a button on Manage, where each item has room
to show **cost + benefit + time-to-build + affordability** — and still drills into the existing
single-item detail panel for the deep view.

---

## 2. What Manage shows today (the starting point — verified from source 2026-08-08)

`ManageScreenPanel.RenderList` (`Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs:590`) builds ONE
scroll list holding, in order:

- **IN QUEUE - <line summary>** — the active + pending jobs on the active tab's channel (the "…and
  queues" heart of WO-911). *This stays in Manage.*
- **UPGRADES - what you can afford first** — the browse rows from `ManageScreenVM.BrowseRows`. *This is
  what moves out.*

`ManageScreenVM.BuildBrowseRows` (`ManageScreenVM.cs:521`) already knows the four category filters, and
every one is an **"upgrade what you already own"** filter (confirmed at source — this is by design, not
a bug):

| Tab | Source | Shows |
|---|---|---|
| Defense | `state.BaseLayout` | placed structures, `maxLevel > 1`, not maxed |
| Buildings | `BuildingTierCatalog.All` | built buildings (`tier ≥ 1`) with a next tier |
| Troops | `TroopCatalog.All` | unlocked troops with a next level |
| Research | building perks | perks on built buildings, not already owned |

**Keep that filter logic verbatim.** This WO changes WHERE the rows render and HOW MUCH each row shows —
not WHICH rows qualify.

---

## 3. What to build

### 3.0 THE LAUNCHER — OWNER RULED: option A (2026-08-08)

> **The four category tabs BECOME the launch buttons.** Tapping **Defense / Buildings / Troops /
> Research** on Manage no longer repaints an inline list — it **opens the dedicated browser panel**
> scoped to that category. Manage itself then shows only the QUEUE view (channel strip + rail + IN
> QUEUE list + slot row + Close), so it is short and never scrolls past one screen.

Owner ruling 2026-08-08: **A**. (Rejected: (B) one contextual "Browse ->" button, and (C) keep the
inline list + a per-row "details" button — (C) does not shorten Manage, which was the whole complaint.)

**Consequence for the build:** the tabs on Manage change role from *filter-the-inline-list* to
*open-the-browser*. The IN QUEUE section in Manage becomes tab-independent for its channel choice — decide
with the owner whether the queue view then shows the ACTIVE line only (last category opened) or all three
lines stacked; recommend **the three-line strip already covers all lines, so show the last-opened line's
rail + list**, and the `ManageScreenVM.BrowseRows` inline render path is retired.

### 3.1 The new panel — `UpgradeBrowserPanel` (one panel, category-scoped)

A single new modal that takes a **category** on open and lists that category's items with full detail.
ONE panel, four scopes — not four panels — so the chrome, the scroll band math, and the row factory are
written once.

- **PanelId:** add `UpgradeBrowser = 17` to `PanelId` (`Assets/_Modules/Core/UI/PanelRouter.cs` — next
  free after `Manage = 16`). Register scene-independently, same pattern as `ManageScreenPanel`
  (a `UpgradeBrowserBootstrap`).
- **Open contract:** `PanelRouter.Open(PanelId.UpgradeBrowser, <ManageTab as int or a category key>)`.
  Mirror how `BuildingUpgrade` takes a string subject (`PanelRouter` already supports a subject arg —
  see `ManageScreenVM.OpenUpgradePanel`).
- **Back to Manage:** closing the browser returns to Manage (Manage stays open underneath, or is
  re-opened). The browser's Close closes only the browser. **Do not stack two exclusive modals
  silently** — route through `PanelManager` exactly as `ManageScreenPanel` does (`NotifyOpened` /
  `NotifyClosed`), or the second modal reads as an invisible scrim (the WO-465 trap).

### 3.2 The row — cost + benefit + time-to-build + affordability

Each browse row grows from today's three columns to a detail card. Every field already has a data
source (verified 2026-08-08 — no new catalog authoring, no economy change):

| Field | Source (reuse — do NOT hand-roll) |
|---|---|
| **Name / target** | today's `BrowseRowVM.Label` ("Arrow Tower -> L3") |
| **Cost** | today's `DescribeCost` / `UpgradeCostFor` / tier & troop costs / perk `goldCost` |
| **Benefit** | `BuildingUpgradeVM.EffectFor(id)` / `NextBonuses` / `AppendEffectClauses` for tiers & perks; `BarracksProgression` power readability (WO-807) for troops; structure effect clauses for Defense |
| **Time-to-build** | `BuildTimerConfig.DurationSecondsForTier(tier, BuildJobKind)` for build/upgrade rows; `BuildingPerkService.ResearchSeconds` / `BuildTimerConfig.ResearchSecondsForGold` for Research rows — render via `ManageScreenVM.FormatTime` |
| **Affordability** | today's `CanAfford` / `ShortfallOf` — the SAME resolver that charges (WO-905 §4), so the browser cannot lie |
| **Drill-in / act** | today's `Activate` — `BuildingUpgrade` panel for Defense/Buildings; `UpgradeTroop`/`Research` CTAs for Troops/Research |

Extend `BrowseRowVM` (`ManageScreenVM.cs:165`) with `BenefitText` and `BuildTimeText` (both ASCII), fill
them in the four `Build*Browse` methods, and let the new panel render them. **The Manage inline path can
stop rendering the browse rows entirely under default (A).**

### 3.2a ⚠ THE DEFENSE LENS — owner ruling 2026-08-08 (fixes the filter gap flagged in WO-905 §7)

> Owner: *"defense should have a lens as it would show all towers that can be upgraded ... since those
> would enter into the build pipeline."*

Today `BuildDefenseBrowse` (`ManageScreenVM.cs:540`) walks the **entire** `state.BaseLayout` with **no
category filter**, so any non-defensive placeable sitting in the layout would surface on the Defense
tab. **Add the lens.** A placed item qualifies for the Defense browser ONLY when it is a defensive
structure — i.e. its catalog `type` is one of:

```
CatalogEntry.type ∈ { CatalogType.Tower, CatalogType.Gate, CatalogType.Wall }
```

(Verified at source 2026-08-08: `CatalogType` = `{ Wall, Stairs, Floor, Room, Tower, Gate, Resource,
Decoration, Troop, Collector, Support }`. Per the WO-673 taxonomy the **Defense** build category =
Tower/Gate and **Walls** = Wall — all three fight/block and all three are placed + upgraded through the
**Builder** build pipeline, which is why they belong on this one lens and share the Builder rail, §2a.)

Concretely, in `BuildDefenseBrowse`, after resolving `entry`, add:

```csharp
if (entry.type != CatalogType.Tower &&
    entry.type != CatalogType.Gate  &&
    entry.type != CatalogType.Wall) continue;   // Defense lens: defensive structures only
```

Result: the Defense browser lists **all towers / gates / walls the player has placed that can still be
upgraded** — and nothing else. Resource/Collector/Decoration placeables never leak onto the Defense
lens; they belong to the Buildings tab (which already reads `BuildingTierCatalog`, a disjoint source).

### 3.3 What STAYS in Manage

- The always-visible three-line channel strip (every line glanceable on every screen).
- The active line's rail + IN QUEUE list (active/pending jobs, Finish/Cancel/Move-up, the Q12 stack).
- The extra-slot / Buy-slot row and the repair fold.
- Close.

Manage keeps its whole queue-management job; it just hands the *catalog* to the browser.

---

## 4. Constraints (binding — inherited from WO-905 §5 / WO-911)

- **Landscape 2340x1080.** `MinTouchPx = 112` on every row, tab, and button.
- ⚠ **Fixed-pixel bands, NEVER fractions of parent** (the WO-841/852/905-build-1 overprint root cause).
  The new browser reuses the same measured-well → summed-bands → remainder-to-the-list arithmetic
  `ManageScreenPanel.BuildChrome` already proves in a `FlowTrace.Step` line.
- **Text-encoded state, never colour alone** — owner is red/green colourblind. Benefit, cost, time, and
  affordability all READ as sentences; tints are decoration on top.
- **ASCII only in TMP strings** (`ManageScreenVM.Ascii`) — "->" not an arrow, "x5" not a glyph.
- **UXML does NOT work in builds** — code-built uGUI via `ElarionUiKit`.
- **Strict MVVM** — the `[ui-mvvm]` ratchet is armed (`HardFailOnNew = true`). NO new reflection bridge,
  NO new `static_gate.py` allowlist entry. The new panel is a View over a VM (extend `ManageScreenVM` or
  add a sibling `UpgradeBrowserVM` that reuses its `Build*Browse` logic — do not duplicate the filters).
- **Read-only.** The browser DISPLAYS and ROUTES. It charges / grants / enqueues NOTHING itself — every
  action goes through the existing service or the existing drill-in panel.
- **No economy, timer, or catalog-JSON changes.** Every number shown is read from an existing resolver.

---

## 5. Acceptance criteria

- [ ] Manage no longer shows a long inline upgrade scroll; under default (A) the four category buttons
      open the browser and Manage fits one screen without scrolling past the queue.
- [ ] `UpgradeBrowserPanel` opens scoped to a category and lists exactly the items that tab's filter
      qualifies today (parity with `Build<Category>Browse` — pin with a regression).
- [ ] **Defense lens (§3.2a):** the Defense browser shows ONLY placed Tower/Gate/Wall structures that
      can be upgraded — no Resource/Collector/Decoration leakage. **Pin with a regression** that plants a
      non-defensive placeable in the layout and asserts it does NOT appear on the Defense lens.
- [ ] Every browse row shows **name, cost, benefit, time-to-build, and affordability**; when short, it
      names what is short.
- [ ] Affordable-first sort preserved (`Affordable`, then `CostWeight` ascending).
- [ ] Drill-in still opens the EXISTING `BuildingUpgradePanelMvvm` (Defense/Buildings) and the existing
      troop/research CTAs — **no second single-item detail panel is created**.
- [ ] Time-to-build agrees with `BuildTimerConfig`; affordability agrees with the resolver that charges.
      **A row that reads affordable can be bought; one that reads short cannot.** Regression-pinned.
- [ ] Closing the browser returns to Manage cleanly (PanelManager arbitration, no invisible-scrim
      stack).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `UI_CAPTURE_OK` (open the PNGs — headless
      screenshot-verify per memory `headless-screenshot-verify-ui-before-build`; compile-green never
      proves a panel looks right).

---

## 6. What NOT to touch

- **Do NOT change the browse FILTERS — with ONE ruled exception: the Defense lens (§3.2a).** Buildings /
  Troops / Research filters stay verbatim (WO-905/911). Defense GAINS the Tower/Gate/Wall type lens —
  that is the only filter change in this WO. Everything else is relocate + enrich.
- **Do NOT build a second single-item detail panel** — route to `BuildingUpgradePanelMvvm`.
- **Do NOT change timer, cost, catalog-JSON, or economy logic.** The browser is a reader.
- **Do NOT re-implement the queue rail** — it stays in Manage (consume WO-864's component as today).
- **Do NOT convert gear (Weapons/Armor) into queued work** — still FUTURE (WO-905 §7.3); not a category
  here.

---

## 7. Open question for the owner

1. RESOLVED (owner 2026-08-08): **§3.0 = option A** — tabs become the launch buttons; Manage shows only
   the queue. See §3.0.
2. **Row density vs. drill-in.** With cost + benefit + time all on the browser row, is the single-item
   `BuildingUpgradePanelMvvm` drill-in still wanted for Defense/Buildings, or does the browser row now
   carry enough that the CTA should upgrade directly? (Recommend KEEP the drill-in — it also hosts the
   perks/Skills tab and the tier art; the browser is the catalog, the panel is the deep view.)

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `ManageScreenPanel.cs:607 one RenderList` — launcher unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal):** "1006 implemented".
