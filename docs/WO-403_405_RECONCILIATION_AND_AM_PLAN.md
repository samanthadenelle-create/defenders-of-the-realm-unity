# WO-403 ↔ WO-405 Reconciliation + AM Plan (built overnight, owner gates in AM)

**Author:** dev-lead/architect pass, 2026-06-10 (owner asleep; safe/read-only work only).
**Law:** `docs/ARCHITECTURE_PRINCIPLES.md` — right not easy; presentation = isolated
layer; bounded context. **Decision lens applied: I flagged my own WO-403 divergence
rather than paper over it.**

---

## 1. The honest finding (read this first)

The owner directed: **"the UI should match exactly what's in the work orders — I made
the mockups."** There are **THREE** mockups + a governing design-system WO I did not
fully build to:

- `Docs/UI_Mockups/hud_mobile_town.png` (#42) — town
- `Docs/UI_Mockups/hud_mobile_combat.png` (#40) — combat: RIGHT side = **SPELLS** (2
  circular ability buttons) + **WEAPON SKILLS** (3 buttons), "WAVE 3/5" centre.
- `Docs/UI_Mockups/hud_mobile_town_quest_modal.png` (#41-adjacent) — quest modal
- **`WORK_ORDER_405_ugui_design_system.md`** — the governing law for ALL HUDs.

**WO-405 §1 + §6 are non-negotiable:** ONE source of truth = **`ElarionUiKit`**;
**"no parallel SO/prefab/reflection theming system — extend `ElarionUiKit`."** §69:
**"Skin only — do NOT change data bindings."**

**My WO-403 divergence (owned):** I rebuilt `VillageHudController` with a **local
palette** (`LParch`/`LGilt`/…) and **bespoke builder helpers** (`DressPanel`,
`BuildFramedBar`, `BuildCircleIconButton`, `BuildPortrait`, `MakeText`). That is
exactly the **"per-screen local palette / parallel theming"** WO-405 §7.1 says to
consolidate. I built to two frames, not to the kit. Functionally correct + API-
preserving (good), but it **adds the very debt WO-405 exists to remove.**

## 2. What ElarionUiKit already gives us (use these, delete my bespoke twins)

`Assets/_Modules/Core/UI/ElarionUiKit.cs` (DeNelle.Core — HUD may use it) provides:
`BuildModalCanvas`, `Scrim` (dim backdrop + tap-close), `Panel` (runic frame),
`Well`, `Niche`, `Header`, `Rule`, `Button(kind)`, `StyleButtonColors`, `Slot`,
`Card`, `Label`, `AddImage`, `ApplyRounded`, `AddRimUnderline`, `AddInnerRim`.
Tokens in `ElarionUi`: `Parchment/ParchmentDim/Ink/Gold/Gilt/GoldButton`, `HpRed/
ManaBlue/Aether/Affordable/Danger`, `Font*`, `Pad*`, `Radius*`, `TapTarget`.

**Note (WO-405 §7.1 REMAINING #1):** the kit's surface tokens are still the *dark*
`Glass` set; the **light** parchment tokens currently live as per-screen locals. The
kit foundation task is to promote ONE shared light token set into `ElarionUi`/
`ElarionUiKit`. My WO-403 locals should fold INTO that, not exist beside it.

## 3. AM plan — build to the kit + mockups (gated by owner)

**Order (parallel-safe per WO-405 §9):**
1. **Kit foundation** — promote light parchment tokens into `ElarionUi`/`ElarionUiKit`
   (one place); add any missing builder: `OrnateBar` (HP/mana/XP framed), `CircularIconButton`,
   `AbilityFrame` (rune ring + cooldown + level badge), `PartyPortrait`, runic border frame.
2. **Refactor `VillageHudController` (WO-403) onto the kit** — replace my bespoke
   `DressPanel/BuildFramedBar/BuildCircleIconButton/BuildPortrait/MakeText` + local
   palette with kit calls. **Behavior-preserving** (every public setter / event /
   `BattleHudGroup`/`TownHudGroup`/`InVillage` stays — §69 skin-only). Net: same HUD,
   now kit-driven, one palette change re-themes it.
3. **Build the COMBAT group to `hud_mobile_combat.png`** — the CombatGroup I stubbed:
   RIGHT-side **SPELLS** (2 circular ability buttons) + **WEAPON SKILLS** (3 buttons)
   with cooldown rings via the kit's `AbilityFrame`; "WAVE 3/5" centre. Wire to the
   EXISTING `AbilityRequested` event + `SetAbilitySlot`/`SetAbilityCooldown` setters
   (already preserved — HeroAbilitiesHudBridge feeds them). Match the mockup exactly.
4. **Match town pixel-to-mockup** — verify banner/compass/INTEL/heart+pet/party/
   actions positions against `hud_mobile_town.png`; adjust to match.
5. **Interaction presentation cleanup** (the "now" tier) — but built through the kit,
   styled to match, NOT bespoke. (See §4.)

**Why not done overnight:** doing 2–5 as more bespoke code would deepen the WO-405
divergence. The right version needs the kit foundation (step 1) first. Improvising it
at night = easy-but-wrong. Held for an awake, gated pass.

## 4. Interaction cleanup — folded into the kit + the buildings-as-collection model

The owner's interaction direction tonight (capture, do in AM):
- Kill the "press F" / ugly world bubbles; context action shows in range, hides out.
- **Built through `ElarionUiKit`** (CircularIconButton / styled affordance), not bespoke.
- Reuse the existing nearest-in-range detection (don't rebuild — WO-391 does the full
  service later).

## 5. Buildings as a COLLECTION — `store.upgradable` / `store.interaction` (owner directive)

The owner (HP B2B lens) wants **buildings to live as a collection** — a catalog of
entries — where **capability is a PROPERTY on the entry**, not bespoke per-building code:

- A building is a **data entry** in a buildings collection (like a SKU in a catalog).
- Whether it is **upgradable** (`store.upgradable`) or **interactable**
  (`store.interaction`) is a **flag/capability on that entry**, surfaced + handled
  uniformly — "buildings that have upgrades are **noted and handled as such**."
- The HUD/interaction layer reads the capability from the collection; it does not
  hard-code which buildings upgrade.

**Architecture fit:** this is the SAME bounded-context law (§1) applied to buildings-
as-data, and it **dovetails with WO-391**: an interactable exposes its capability
(`Interaction`, `Upgradable`) and the collection is the single source of truth for
which buildings have which. The interaction service + the building collection are two
views of one model: *objects expose capability; presentation + logic read it.*

**AM task (new, to spec into a WO):** define the **buildings collection** (catalog of
entries + `Upgradable`/`Interaction`/action-id capabilities), migrate the per-building
upgrade/interact logic to read from it, and have the HUD interaction affordance + the
upgrade entry points consume the capability flags. This is the right home for
"buildings that have upgrades are noted and handled as such." Reconcile with the
existing `Building`/`BuildingInteractable`/`CrystalMine`/upgrade-panel code — additive,
not blind-replace (per memory: WO batch = reconcile, not replace).

## 6. What I changed overnight (all SAFE — docs + canon only, NO gameplay/UI code)

- `docs/ARCHITECTURE_PRINCIPLES.md` — canonized the HP-B2B law (presentation isolated;
  bounded context; right-not-easy; player-felt-vs-holistic).
- `CLAUDE.md` — added the binding pointer to the law at the top.
- `docs/WO-391_INTERACTION_SEPARATION_STRATEGY.md` — full read-only strategy for the
  interaction/presentation separation (the "do it correctly when it's time" plan).
- This file — WO-403↔405 reconciliation + AM plan + buildings-as-collection directive.
- Notion: WO-391 created (Spec, Lane 4 UI/HUD) + linked to the law.
- My memory: `hp-b2b-architecture-law.md`.

**I did NOT touch any `.cs` gameplay/UI file overnight.** No bespoke combat panel, no
interaction reskin — because the right version is kit-first + mockup-exact + gated by
you, not improvised at night. `VillageHudController.cs` (WO-403) stands as delivered
(API-preserving, functional) pending the kit refactor in step 2.

## 6b. OVERNIGHT FINDING — the kit is still DARK; do NOT auto-refactor onto it

Read `ElarionUiKit` fully overnight. **It is still the dark-glass language** (`Glass`/
`GlassDeep` = near-black low-alpha; `Panel`/`Button`/`Slot`/`Card`/`Header` default to
dark glass + cream text). WO-405 §7.1 confirms the **light token set is REMAINING, not
done** — each screen currently carries its OWN local light inversion (my WO-403 included).

**Judgment call (held for owner — NOT executed):** refactoring WO-403's light HUD onto
the kit *as-is* would convert it to DARK glass = a regression AWAY from the mockups.
The correct order (WO-405 §9 step 1) is **light-the-kit FIRST, then refactor**. But
promoting the shared light token set **re-themes EVERY screen at once** (inventory, ATB,
dialogue, arena all draw from this kit) — a global, load-bearing change with no test
gate + no build to verify overnight. WO-405's own header: *"the light-parchment direction
needs an owner build-verify before the full sweep."* So I am **NOT** doing the kit
re-theme or the HUD refactor autonomously. Held for your build-verify. This is the
"stop and leave it for awake judgment" discipline working as intended.

### Ready-to-execute plan (mechanical once you OK the light direction in a build)

**Step 1 — light the kit (one place, re-themes ALL screens):** add a shared light token
set to `ElarionUi` + switch `ElarionUiKit` surface defaults:
`Glass`→parchment `#EDE6D6`@~0.95 · `GlassDeep`→`#E8DFC8`@~0.97 · `Cell`→soft parchment ·
`Track`→warm well `(0.40,0.34,0.24,0.34)` · `Accent`→gilt `#E8B923`@0.95 ·
`AccentSoft`→gilt@0.42 · default text→`ElarionUi.Ink` · rim glow→parchment halo.
Keep state colors (HpRed/ManaBlue/Aether — read fine on light, §2).

**Step 2 — add missing kit builders (WO-405 §3, none exist yet):** `OrnateBar` (framed
HP/mana/XP + RpgUiCatalog frame) · `CircularIconButton` · `AbilityFrame` (rune ring +
radial cooldown + level badge) · `PartyPortrait` · `RunicBorderFrame`.

**Step 3 — refactor WO-403 onto the kit (behavior-preserving):** map my bespoke helpers
→ kit calls: `DressPanel`→`Panel`(+frame); `BuildFramedBar`→`OrnateBar`;
`BuildCircleIconButton`→`CircularIconButton`; `BuildPortrait`→`PartyPortrait`;
`BuildTextButton`→`Button(kind)`; `MakeText`→`Label`; `BuildRunicFrame`→`RunicBorderFrame`;
delete local `LParch/LGilt/LInk` → kit light tokens. Every public setter/event/
`BattleHudGroup`/`TownHudGroup`/`InVillage` stays (§69 skin-only).

**Step 4 — combat group to `hud_mobile_combat.png`** via the new `AbilityFrame` builder
(SPELLS×2 + WEAPON SKILLS×3, cooldown rings, "WAVE n/m"), wired to the EXISTING
`AbilityRequested`/`SetAbilitySlot`/`SetAbilityCooldown`.

Post-approval this sweep is mostly mechanical + parallel-safe (§9) and satisfies the
WO-405 acceptance criterion (one token change re-themes the whole game light).

## 7. First thing to confirm in AM

1. OK to refactor WO-403's `VillageHudController` onto `ElarionUiKit` (behavior-
   preserving) so it stops being a parallel theme? (WO-405 §7.1 says yes.)
2. Build the combat group to `hud_mobile_combat.png` (SPELLS + WEAPON SKILLS)?
3. Spec the **buildings collection** WO (`store.upgradable`/`store.interaction`) — new
   WO number from the master backlog.
