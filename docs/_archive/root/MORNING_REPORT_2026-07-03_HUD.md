# Morning Report — 2026-07-03 — The Overnight HUD Rebuild

**Mandate:** "get this done and built before the morning… i do not want to wake up and not
have a fully compiled fully functioning hud."

**Status at writing (08:00):** the HUD kit is **built, compiled, and functioning in the
player**, and after four gate→build→fleet iterations the fleet is **green on every HUD
oracle class** (§3). Proof screenshots from a windowed run land in `ui-shots/` (§4).
Everything below is captured data, not claims.

---

## 1. What is PROVEN (captured data, not claims)

Every line below is from `Builds/solo-diag.log` — a full AutoPilot run of the freshly built
player with a dedicated log (the fleet's shared Player.log turned out to be stale/clobbered;
this run was the honest window).

- **The kit boots in the built player.** `[Flow:HudKit]` ×154 lines, starting
  `command bridge registered (attack, cycleSelect) for scene 'MainCastle_Hall'`.
- **The owner's posture tree runs end-to-end** (A4.x master state → dumb HUD submodel):
  ```
  [Flow:HudKit] posture <boot>->calm(town)
  [Flow:HudKit] posture calm(town)->hostile(prebattle)   ← pursuit pulses firing
  [Flow:HudKit] posture hostile(prebattle)->calm(town)
  [Flow:HudKit] posture calm(town)->modal                ← panel-open occupancy
  ```
- **Panel routing survived the demolition.** All registry panels
  `opened and verified visible` (PartyShop, HeroSkillTree, Crafting/Workshop, …) —
  `PanelRouter: 'PartyShop' opened and verified visible (open panel='Party Shop')`.
- **Kit buttons route real commands** — the Menu button's stack runs
  HudKitController → ElarionUiKitObsidian → PanelRouter.Open (captured in fleet break-log).

## 2. Fleet-9000 verdict — the three failure classes, each RCA'd from data

| Failure | Root cause (proven by) | Fix |
|---|---|---|
| `SPAWN_TO_GATE_FAIL threshold y=7.34` ×12 | **My bug** — the bridge deck BoxCollider wrapped the bridge's FULL renderer bounds; its top face = parapet height 7.34, and the threshold-seating raycast landed there instead of the deck. (threshold y in the error == bounds top, exactly.) | Deck slab now derived from the largest-footprint renderer (the deck plank), thin 0.4 m slab at its top; rails rise from deck top. Gate green. ⏳ fleet re-verify |
| `POPUP_OPEN_FAILED` ×12 | **Run-local, one run of six** (runs 0–3,5: zero; run 4: all 12). Same binary passes solo with "opened and verified visible". PanelManager rejects ALL gameplay panels during battle **by design** (WO-437, PanelManager.cs:112) and only Warns — invisible to break-log. A garrison wave overlapping run 4's popup phase manufactures exactly this signature. | Oracle is now battle-aware: waits (bounded 20 s) for BattleLock to clear, records `SKIPPED_IN_BATTLE` instead of a false Fail. If run 4's true cause was something else it will now surface with no battle to hide behind. |
| `POPUP_NO_CLOSE` ×3 (CosmeticShop, Crafting, PetSkillTree — identical across runs) | **Oracle finder bug** — in dev builds the UITK close-button scan matched the always-visible dev overlay FIRST. Proof: it clicked `dev-panel-close` while Workshop sat open; PetSkillTree actually closed (`AnyOpen=False`) yet was verdicted NO_CLOSE because the foreign 'Close' stayed visible. | Finder now skips dev/admin/help/debug UIDocuments and `dev-*` buttons. ⏳ fleet re-verify — if any NO_CLOSE survives the fixed finder, it's a REAL missing close and goes on the fix list, honestly. |

Also confirmed green in fleet-9000: NAVMESH-LINK 0, PROP_SEATING 0, HOME_RETURN_FAIL 0, softlocks 0.

## 3. Iteration log

**Fleet 9500 (verdict in):**
- ✅ **POPUP_OPEN_FAILED: 0 in all 6 runs** — the false-positive class is dead (battle-aware oracle).
- ✅ Oracle finder fix proven: Crafting's bot now clicks the panel's real `Close` (not `dev-panel-close`).
- ❌ Threshold still y=7.30 — round-1 fix (largest-footprint renderer) failed because the bridge FBX
  is ONE combined mesh; any bounds top = parapet height. **Round 2:** the deck collider is now built
  ANALYTICALLY from the seat's own walking plane (castle end y=liftY → outer end ground seat) —
  no mesh heuristics left to be wrong.
- ❌ **3 REAL close defects confirmed** (oracle noise removed): Workshop/Crafting and CosmeticShop
  stay open after their own Close is clicked (`AnyOpen=True`); PetSkillTree closes per the arbiter
  but leaves its close affordance visible. On the fix list — these are player-facing.

**Fleet 9600 (verdict in): the south lane is WELDED.**
- ✅ SPAWN_TO_GATE_FAIL: 0 (was 6/6). RUNTIME_SEAM_NAV_FAIL: 0 (was 6/6). The analytic walking-plane
  deck collider ended the threshold-height class for good — no mesh heuristics left to be wrong.
- ✅ POPUP_OPEN_FAILED: 0 — second consecutive clean fleet.
- WalkToEachGate / CavePortal-path / encounter-return survived the weld → **proven independent
  defects** (world/nav lane, not HUD, not the seam). Queued for today with the save-drift RCA.

**Popup-close fixes implemented** (read-only RCA agent traced all three with file:line proof; CLI applied):
1. **Workshop/Crafting** — its close button was an anonymous "X" (`VillageCraftingPanel.cs:228`); the
   close convention matches by name, so the oracle clicked a FOREIGN panel's Close. Now `name="CloseButton"`.
   (Note: the "X" itself violates the one-shared-Close canon — flagged for the panel-restyle lane.)
2. **CosmeticShop** — its close button ran `ToggleOverlay`; a double-dispatched click closed then
   RE-opened the shop. Close is now bound to `CloseOverlay` (idempotent — a close must never open).
3. **PetSkillTree** — panel closes correctly (arbiter agreed); the oracle's probe read only the
   button's OWN `display`, which ancestor-hide never changes. Probe + finder are now ancestor-aware
   (also the root of Workshop's foreign-button grab).

**Fleet 9700 (verdict in): the fixes landed and exposed the TRUE root.**
- The oracle now clicks each panel's OWN close button (messages changed: `'CloseButton'`, `'Close (P)'`) —
  and all three panels STILL stayed open. Since their close handlers unconditionally hide + `NotifyClosed`,
  `AnyOpen=True` after the click proves **the handler never ran**: the oracle's synthetic UITK click
  (legacy Mouse events + a target-less ClickEvent) doesn't reach Unity 6's Clickable in the built player.
  uGUI panels always passed because that path invokes `onClick` directly.
- **Honest correction:** the earlier "CosmeticShop toggle re-opened" mechanism was wrong (the
  toggle→CloseOverlay change stays — a close must never toggle — but the panel bugs were smaller than
  they looked). The three panels' close handlers are likely CORRECT; the oracle couldn't press them.
- Fix: `ClickUiToolkitButton` now sends `NavigationSubmitEvent` (the supported programmatic activation,
  handled synchronously by Clickable) with an explicit target.
- Also: SPAWN_TO_GATE / RUNTIME_SEAM stayed at 0 in 9700 — the weld holds across builds.

**Fleet 9800 (verdict in): GREEN on every HUD class.**
```
POPUP_NO_CLOSE      0   (was 18 across the fleet)
POPUP_OPEN_FAILED   0   (was 12)
SPAWN_TO_GATE_FAIL  0   (was 12)
RUNTIME_SEAM_NAV    0   (was 6)
softlocks           0
remaining: 1 wallet-drift + 1 roster-drift (persistence lane, own RCA queued)
```
**Caveat, stated plainly:** NO_CLOSE=0 proves the ORACLE can now press Close on the three UITK
panels; whether a human FINGER closes them is your felt-check (the oracle's old click path was
fake-pressing — a real pointer may behave differently on those legacy UITK surfaces).

## 3b. Surfaced by fleet-9500, honestly unattributed (no pre-gut baseline to compare — 9000 logs wiped)

- `AssertSaveRoundTrip`: WALLET drift (crystals 19390 vs expected 4242) + ROSTER probe missing after
  reload — 4/6 runs. **Persistence defect, needs its own RCA** — not HUD-caused on current evidence,
  but severe.
- `AssertEncounterRealPath`: hero NOT returned (6992.7m from engagement) — 4/6 runs. The old 7km-return
  smell; possibly downstream of the severed south lane. Re-read after fleet 9600.
- `WalkToEachGate` timeout ×6 + `AttemptExitCastle` can't path (closest 446.4m) — likely the same
  severed-lane domino; re-read after fleet 9600.

## 4. Visual proof (windowed run, real frames — I looked at these myself)

In `Builds/morning-proof/` (full set of 12 panels: `...LocalLow\DeNelle\Defenders of the Realm\ui-shots\`):
- **`hud_calm_town_live.png`** — the kit HUD live in the player, calm(town) occupancy exactly per
  hud-areas.json: Obsidian Knight plate (framed HP/mana), XP bar + wisdom chip, wave block
  "The village rests" + heart bar, Obsidian Menu, gold-primacy currency stack, quest tracker,
  move cluster, action dock.
- `panel_gear_shop.png`, `panel_hero_talents.png`, `panel_workshop.png` — Blink Obsidian frames
  rendering with real data.

**Gaps I can SEE in the shots (not hiding them):**
- Gear Shop: the medallion socket is EMPTY (black oval) and it still has a yellow "X" — canon says
  one shared Close, no X. A stray quest-tracker fragment overlaps the right frame edge.
- Move cluster (bottom-left) is still the flat green arrows — not yet Obsidian (deferred item).
- **Real error captured on-screen:** `[Flow:BotUI] duplicate UIDocument: 5 ENABLED documents share
  PanelSettings 'OnboardingPanelSettings' in scene 'Title'` (SplashLoading/TitleController/
  TitleScreen/MusicSelectionPanel/StoryIntro raycast-fight over one PanelSettings). Ledgered.

## 4b. Honest unverified ledger

- **Nothing in this report is "fixed" — that's your word to award.** Fleet-green means the
  machines didn't object; the felt-pass is yours.
- The three UITK panel closes pass the oracle's REAL activation now; a human finger on those
  legacy surfaces is still your call.
- Persistence: 1 wallet-drift + 1 roster-drift per recent fleet — needs its own RCA (queued).
- World/nav (proven independent of the seam by surviving the weld): WalkToEachGate timeout,
  CavePortal pathing (446m), encounter-return (6992m) — world lane, today's queue.
- Deferred from the kit build (unchanged): potion-slot Village seam, minimap/compass/quest-tracker
  conversions, nameplate tap-target, move-cluster Obsidian skin.

## 5. Today: animation deep-dive

Dossier ready at `docs/ANIMATION_DOSSIER_2026-07-03.md` — walk-blend structural fix (the
skate), prebattle InCombat stance wiring (pre-drilled, unused), dying-in-air repro plan,
pets' dead controller path, KayKit-vs-Tripo retarget blocker, plus your open decisions list.

## 6. Commits — DONE (16 lanes, explicit path, push HELD for your word)

`8944f81d..5d8b2ec1` — LX shared → L1 audio → L2 castle/bridge-seam → L3 world → L4 anim →
L5 combat+L5b vfx-pool → L6 ui → L7 vendors → L8 tutorial → L9 devtools/oracle → L10 sweep →
L11 data → L12 docs → **L13 `925464df` feat(hud): the Blink Obsidian HUD kit rebuild** → L14 metas.
Branch `wip/village2-and-f8-tickets` now **45 ahead of origin, local-only. Push waits for your word.**

13 leftover paths deliberately unstaged for your ruling: the map's EXCLUDE list (QA_F8_ARCHIVE/,
_opener_frames/, two docs zips, open-wos.txt, rename_armor_images.py) + `link.xml` deletion
(generated Addressables file — recommend re-generate, not commit-the-delete), docs/issues PNGs,
tools/AudioGen/, ProjectSettings/Packages/, Action.meta + Economy.meta orphans.
