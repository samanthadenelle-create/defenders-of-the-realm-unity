# WORK ORDER 905 — "Manage": one screen for every upgrade, sorted by what you can afford

**Status:** SPEC — READY for design review. Depends on WO-864's rail component.
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

**Entry point:** a **Manage** section reachable from the **Bag** button (the bottom bar already carries
Build / Talk / Bag / Map / Quests). Bag is inventory; Manage is progression. Confirm with the owner
whether Manage is a tab INSIDE Bag or a sibling button — she said "under Bag", which reads as inside.

**Top of screen — the three rails.** Reuse WO-864's rail component verbatim; do NOT re-implement it.
WO-864 has been told to expose it as a reusable builder taking `(ChannelId, RectTransform mount)`. Three
rails: Builders, Training, Research — each showing its own `SlotCount`, active jobs, queued jobs and free
slots.

**Below — the browse list, tabbed by content type. V1 SHIPS THREE TABS** (Defensive structures / Building upgrades / Troop upgrades). **Weapons and Armor are explicitly FUTURE** (owner: *"those are future ideas... things like that"*) and are documented in §2 only so the tab model is designed to take them later without a rewrite. Do not build them now. Each row: what it is, its next
level, its cost, and — the point of the whole screen — **whether the player can afford it right now**,
and if not, what is short.

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

1. **Manage inside Bag, or a sibling button?** "Under Bag" reads as a tab inside it, but Bag is inventory
   and Manage is progression — they are different mental models sharing a button.
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
