> # ⚠ SUPERSEDED 2026-08-06 — ABSORBED BY **WO-911**
>
> **Status: SUPERSEDED. Do NOT implement this work order. One screen exists, not two.**
>
> Owner ruling **Q13** (`WORK_ORDER_911_unified_queue_screen.md` §8, 2026-08-06) merged this WO into
> **WO-911 — Unified Manage/Queues Screen**. The "Manage" screen specified below **IS** WO-911's
> screen: same three channel rails, same tabbed browser, same entry point (the bar face already named
> "Upgrade", which WO-911 re-points). Two screens doing the same job was the single biggest scope risk
> identified in the WO-911 audit; the ruling closed it by building **once**.
>
> **Implemented 2026-08-06** as `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs` +
> `ManageScreenVM.cs` + `ManageScreenBootstrap.cs`.
>
> **What of this WO survives, and where it went:**
> - §2's *"five content tabs over three queues"* structural catch — **honoured**. `ManageScreenVM.ChannelOf`
>   is the single home of the tab→channel crossing, and Defense + Buildings share ONE Builder rail.
> - §2(b) *"gear has NO wall-clock cost"* — **honoured**. Weapons/Armor are NOT built (they were already
>   resolved FUTURE in §7.3, and WO-911's ruled tab set excludes them).
> - §3 *affordable-first sorting*, per-row cost + shortfall text, and drill-in to the EXISTING
>   `BuildingUpgradePanelMvvm` — **all carried over verbatim**.
> - §5 constraints (MinTouchPx, fixed-pixel bands, text-encoded state, ASCII-only, no UXML) — **binding
>   on WO-911 too and applied**.
>
> **What CHANGED under WO-911's rulings (this WO's text is stale on these points):**
> - §3.0 entry point: it is **not** a new dedicated button. The existing **Upgrade** bar face is
>   RE-POINTED (ruling Q10+Q13) — no 8th face, no `ButtonCount` bump.
> - §6 AC "reachable from **Bag**": superseded — it is reachable from the **bar face**. (Map is what
>   moved into Bag.)
> - §6 AC "the always-on queue panel is REMOVED from the play HUD": superseded by ruling Q10 — the
>   right-column Builders **chip SURVIVES as a status glance**; what was retired is its double-tap
>   *door*, so the bar face is the single entry.
> - §7 open question 2 ("should troop TRAINING appear?"): **YES** — Troops is a ruled tab (Q3).
> - This WO's §1/§6 wireframe shows all three rails stacked at once; WO-911's screen shows the ACTIVE
>   tab's rail full-width plus an always-visible three-line TEXT strip, so every channel stays
>   glanceable without starving the list of vertical space on a handheld.
>
> Read this file for the browser/affordability detail and the tab model. Take the **rulings and the
> acceptance criteria from WO-911**, which is the authority.

---

# WORK ORDER 905 — "Manage": one screen for every upgrade, sorted by what you can afford

**Status:** SUPERSEDED by WO-911 (2026-08-06) — see the banner above. *(Was: SPEC — READY for design review.)*
**Minted:** 2026-08-04 (CLI), owner directive
**Lane:** HUD/UI. Presentation + a read-only affordability model. **No economy or timer logic changes.**
**Depends on:** WO-864 (the queue card rail — must expose a REUSABLE component)
**Adjacent:** WO-857/901 Phase F (bank caps), WO-903 (pallet fill), `BuildingUpgradePanelMvvm`

---

## 1. The idea, in the owner's words

> *"Could we somehow make a manage section under Bag where they can see all three rails and dive into
> one if they want to upgrade — but not sure what they can afford."*
>
> *"All defensive structures versus all building upgrades versus troop upgrades."*
>
> *"Then eventually versus weapons upgrades and armor upgrades."*

**The problem it solves is the last clause.** The player's real question is not *"what does this building
do"* — it is **"what should I spend on right now, and what can I actually afford."** Today there is no
screen that answers it. The existing `BuildingUpgradePanelMvvm` answers it for **one building at a time,
and only if you walk to that building.**

---

## 2. ⚠ THE STRUCTURAL CATCH — five content tabs over three queues, and two tabs have no queue

**The owner's categories and the queue channels CROSS. They are not the same three things.** Build this
knowing that, or the tabs will promise a separation the rails do not have.

| Owner's content tab | Queue channel it uses | Has a rail? |
|---|---|---|
| **Defensive structures** (towers, walls, gates) | **Builder** | shares the Builder rail |
| **Building upgrades** (economy/production) | **Builder** | **shares the SAME Builder rail** |
| **Troop upgrades** | **Research** | yes |
| *(troop TRAINING — not a tab the owner named, but it owns a rail)* | **Train** | yes |
| **Weapons upgrades** | **NONE** | ⚠ **no queue exists** |
| **Armor upgrades** | **NONE** | ⚠ **no queue exists** |

Two consequences to design around, not discover:

**(a) Defensive and Buildings share one rail and one set of slots.** A player queuing a tower is spending
the same builder as a player queuing a farm. The tabs can filter the BROWSE list by content, but the
Builders rail underneath is one shared capacity. Do not render two Builder rails.

**(b) Gear has NO wall-clock cost at all.** `GearProgression.Improve`
(`Assets/_Modules/Village/Hero/GearProgression.cs:250-281`) spends the ledger and applies inline — its own
comment says *"instant V1 — no job/channel"*. It is the only progression sink in the game that costs
resources but no time. So the Weapons and Armor tabs have **nothing to put on a rail**; they are pure
affordability browsers. **Either they render without a rail, or gear gets queued first — which is a
separate WO and an economy change, explicitly out of scope here.** Recommend the former; flag the latter.

---

## 2.6 WIREFRAME (UI refinement 2026-08-05 — the visual target)

```
┌ Manage ─────────────────────────────────── "sorted by what you can afford" ┐
│  ┌ Builders 2/3 ─────┐  ┌ Training 1/2 ────┐  ┌ Research 0/1 ───┐          │  ← 3 LIVE QUEUE RAILS
│  │ Barracks → T2 2:45│  │ 5× Spearman 1:10 │  │ Idle            │          │    (active + queued
│  │ Farm queued·1 free│  │ 1 free           │  │ 1 free          │          │     per channel)
│  └───────────────────┘  └──────────────────┘  └─────────────────┘          │
│  [ DEFENSIVE ]   [ Buildings ]   [ Troops ]                                 │  ← content TABS
│  ◆ Arrow Tower → L3     wood 400 · food 200        [Ready]            >     │  ← AFFORDABLE-FIRST rows
│  ◆ Stone Wall → L2      stone 300                  [Ready]            >     │    state is TEXT
│  ◆ Cannon Tower → L2    wood 900 · crystal 150     [Short 150 wood]   >     │    (not colour-only)
│  ◆ Arcane Spire → L4    crystal 800 · food 500     [Short 320 crystal] >    │    > = drill-in to
│                              [ Close ]                                      │        BuildingUpgradePanelMvvm
└────────────────────────────────────────────────────────────────────────────┘
```
Rails on top = the "…and queues" view. Rows sorted affordable-first; each names its shortfall when short.
Defensive + Buildings tabs both filter the browse list but share ONE Builders rail (§2a). Drill-in reuses the
existing upgrade panel (WO-895's redesigned "next-only" card). Built with `ElarionUiKit` / `docs/UI_BLINK_TEMPLATE_CANON.md`.

## 2.7 ⚠ BUILD-1 FELT-TEST FIXES (owner 2026-08-07) — the first build collides; match §2.6

**Owner (2026-08-07): "text is close, just overlapping."** The CONTENT is right — this is a **SPACING/OVERLAP bug, not a rebuild.** THE PRIMARY FIX is #5: give each top element its own vertical band with real spacing so the text stops overprinting (the rail line is overlapping the extra-slot/Buy-slot row above the tabs). #1–4 are polish toward §2.6 once the overlap is gone.

The first build has the right structure (4 tabs, rails, affordability header) but the **top region overlaps and the body is a cavernous void.** Punch-list to reach §2.6:

1. **Rails are a run-on overlapping line — make them the 3 CARDS (§2.6).** Today "Builders 0/2 busy - 0/5 queued | Training … | Research …" is ONE wrapping text line that collides with the row below it. Render **three separate bordered cards** (Builders / Training / Research), evenly spaced across the top, each showing its own `N/M busy · Q/5 queued` — no wrapping, no overlap.
2. **"Buy slot" + "Extra slot: locked – awaken a 3rd Echo" + the two "FREE" chips overlap the rails.** Move the extra-slot control OUT of the rail band: the **FREE** indicators belong INSIDE their rail card (free-slot count), and **Buy slot** is a single clean button placed on its own row **below** the rails (right-aligned), with "locked – awaken a 3rd Echo" as its disabled reason line. Nothing floats over the rail text.
3. **Cavernous black void below the list.** The panel is far taller than its content. Either (a) size the panel to content (no empty half-screen), or (b) let the upgrade list fill the body as the scrollable section (§2.6). An empty tab shows a **centered empty-state** ("Nothing to upgrade here yet"), never a black void.
4. **The upgrade list renders NO rows.** "UPGRADES – what you can afford first" has an empty body. Verify the affordability rows populate for the active tab; if genuinely empty in this state, show the empty-state (#3), don't leave blank.
5. **Strict vertical bands, no overlap** — the top three elements (rails / extra-slot / tabs) each own their own band (the §5 fixed-pixel-band rule was violated here). Header → rails(3 cards) → extra-slot row → tabs → scrollable list → Close, each in its own band.

Target = the §2.6 wireframe exactly. Re-capture headless after the fix and open the PNG.

## 3. What to build

### ⚠ 3.0 THE POINT OF THE SCREEN — upgrades get a HOME, and the HUD gets its space back

> **Owner, 2026-08-04:** *"Since they now belong somewhere, not randomly by walking to them."*
> *"By doing that way we can remove from showing on the screen."*

Two goals, and the second is a deliverable, not a side effect:

**(a) Discoverability by walking is not discoverability.** Today the ONLY way to find an upgrade is to
physically walk to that building (`BuildingInteractable.cs:289`). A player never sees what they COULD be
doing — only what they happen to be standing next to. Manage makes the full set browsable in one place.

**(b) RETIRE the always-on queue panel from the HUD.** Once the rails live in Manage, the persistent
right-column queue panel comes OFF the play screen. That is the owner's explicit intent and it is worth
real estate on a 2340x1080 handheld. **Sequencing: Manage must land FIRST and be reachable, then the HUD
panel is removed — never the reverse**, or the player loses all queue visibility with nothing replacing
it. Removing it is a checklist item on THIS WO, not a follow-up to forget.

**Entry point (RESOLVED, owner 2026-08-05):** a **dedicated HUD button** that opens the Manage screen — owner:
*"a button in the HUD that allows all upgrades from a screen."* It is its OWN button (Manage / Upgrades), not
buried inside Bag (Bag = inventory, Manage = progression — different mental models). The screen opens straight to
the tabbed browser + the three rails. (Supersedes the earlier "under Bag" phrasing.)

**"…and queues" (owner 2026-08-05):** the queue visibility the owner is asking for IS the three rails shown at the
TOP of this screen (Builders / Training / Research — active + queued jobs per channel). That is the queues view;
there is no separate Queues content-tab needed (the rails already surface every channel's live jobs).

**Top of screen — the three rails.** Reuse WO-864's rail component verbatim; do NOT re-implement it.
WO-864 has been told to expose it as a reusable builder taking `(ChannelId, RectTransform mount)`. Three
rails: Builders, Training, Research — each showing its own `SlotCount`, active jobs, queued jobs and free
slots.

**Below — the browse list, tabbed by content type. V1 SHIPS FOUR TABS, one per queue channel** (owner 2026-08-05,
resolving old open-Q2): **Defensive** (Builder rail) · **Buildings** (Builder rail — shares it) · **Training**
(Train rail — troop training + the WO-897 army muster) · **Research** (Research rail — troop/tech upgrades). Every
rail now has a home tab. **Weapons and Armor are explicitly FUTURE** and documented in §2 only so the tab model
takes them later without a rewrite. Do not build them now. Each row: what it is, its next level, its cost, and —
the point of the whole screen — **whether the player can afford it right now**, and if not, what is short.

**⚠ The per-tab item list is a SCROLLABLE section (owner 2026-08-05):** each tab's list can hold many rows
(every tower / every building / every troop line), so it is a `ScrollRect` that scrolls vertically inside its
band — the rails, tabs, and Close stay pinned; only the list scrolls. Same for a rail's queued-jobs list if it
overflows. Fixed-pixel rows (MinTouchPx 112), never fraction-of-parent (§5).

**Sorting is the feature.** Default the list to *affordable first*, then by cost ascending. A player
opening Manage should see what they can act on immediately, without arithmetic. Everything else is a
catalog.

**Drill-in** opens the existing `BuildingUpgradePanelMvvm` (`PanelRouter.Open(PanelId.BuildingUpgrade, id)`
— see `HudKitController.cs:1553`, `BuildingInteractable.cs:289`). **Do not build a second upgrade panel.**

---

## 4. Existing seams — find and reuse, do not reinvent

Today has repeatedly turned up systems that were already built and never wired. Check each of these
before writing anything:

- **`BuildingUpgradePanelMvvm`** (83 KB, registered `PanelId.BuildingUpgrade`) — the drill-in target.
- **Affordability:** `BuildModeController.EffectiveCostFor` / `SoftcappedCostFor` / `ShortfallMessage`,
  and `BuildMenuVM.TrySpendBuild`. An affordable-flag may already exist on a VM — WO-864 was asked to
  report any it finds. **Reuse the resolver; do not hand-roll a cost comparison**, or the screen will
  eventually disagree with the thing that actually charges.
- **Upgrade costs by content type:** structures → `repo.upgradeCost` in `structures-catalog.json`;
  city buildings → `building-tiers.json`; troops → `BarracksProgression` (cost = train x targetLevel);
  gear → `gear-levels.json`.
- **Queue state:** `ObsidianQueueGate.WorkQueueStatus` already publishes per-channel busy/slots/queued
  plus per-job `Label` / `RemainingSec` / `Queued`.
- **Bank headroom (WO-857):** `TownBankCapacity.HasHeadroom(BankResource, amount)`. ⚠ **An upgrade the
  player can afford but whose cost exceeds nothing — fine; but a REWARD they cannot bank is a different
  case.** More importantly: if a resource is at cap, the affordability display should not imply the
  player should keep collecting.

---

## 5. Constraints (binding)

- **Landscape 2340x1080 only.** `MinTouchPx = 112` on every row and tab.
- ⚠ **Fixed-pixel text bands, NEVER fractions of parent** — the documented root cause of repeated UI
  culling bugs here (WO-841 / WO-852). A five-tab list is exactly where someone reaches for percentages.
- **Text-encoded state, never colour alone** — the owner is red/green colourblind. "Affordable" and
  "short 200 wood" must READ, not just tint.
- **ASCII only in TMP strings.** No glyph-font icons (tofu risk); sprites or text.
- **Strict MVVM** — the `[ui-mvvm]` ratchet is armed (`HardFailOnNew = true`). **No new reflection
  bridge**, no new `static_gate.py` allowlist entry.
- **Read-only.** This screen displays and routes. It must not charge, grant, enqueue or mutate anything
  itself — every action goes through the existing panel or the existing queue API.
- **`UI_CAPTURE_OK` required** — open the PNGs. Compile-green never proves a panel looks right.

---

## 6. Acceptance criteria

- [ ] Manage is reachable from Bag and shows all three rails without opening anything else.
- [ ] THREE content tabs in v1; the Builder-backed tabs (Defensive, Buildings) share ONE Builders rail.
- [ ] Every row shows cost and an affordability state; when short, it names what is short.
- [ ] Default sort puts affordable items first.
- [ ] The always-on queue panel is REMOVED from the play HUD, and only AFTER Manage is reachable.
- [ ] Drill-in opens the EXISTING `BuildingUpgradePanelMvvm`; no second upgrade panel is created.
- [ ] Affordability agrees with the resolver that actually charges — a row that reads affordable can be
      bought, and one that reads short cannot. **Pin this with a regression**; a screen that lies about
      affordability is worse than no screen.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` + `UI_CAPTURE_OK`.

---

## 7. Open owner questions

1. RESOLVED (owner 2026-08-05): **a dedicated HUD button** (Manage / Upgrades), not inside Bag. See §3.0.
2. **Should troop TRAINING appear?** The owner named five upgrade categories; training is not an upgrade,
   but it owns one of the three rails she wants shown.
3. RESOLVED — Weapons/Armor are FUTURE, not v1 (owner 2026-08-04). Retained in section 2 so the tab model anticipates them. Original note: they have no queue and are instant, so they may be better held
   until gear is queued — otherwise two of five tabs behave unlike the rest.

---

## 8. What NOT to touch

- **Do NOT queue gear improve** — that is an economy change and its own WO (logged from WO-856 §9 and the
  2026-08-04 reward measurement).
- **Do NOT build a second upgrade panel** — route to `BuildingUpgradePanelMvvm`.
- **Do NOT re-implement WO-864's rail** — consume its component.
- **Do NOT change timer, cost or economy logic.** This screen is a reader.
