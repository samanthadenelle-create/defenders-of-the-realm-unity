# WORK ORDER 576 — Building "Talk" dead-end fix (Farm + resource buildings)

**Status:** IMPLEMENTED (edit-only; NOT gated/committed — for orchestrator reconcile)
**Date:** 2026-06-28
**Silo:** Village / Dialogue (post-Yarn-removal cleanup)
**Origin:** Owner felt-test — "the FARM building has a talk function that seems to go nowhere."
**Branch base:** `wip/village2-and-f8-tickets` @ `4f51e085` (ff-merged into the worktree first)

---

## 1. RCA — how "Talk" routes, and why the farm felt dead

Home hub = **MainCastle_Hall**. The farm is the **Windmill** storefront: `CastleVendorNpcInjector`
spawns a static peasant NPC there with `structureId = "farm"`, label "Windmill"
(`CastleVendorNpcInjector.cs:105-106`). The building's own `BuildingInteractable` defers its prompt to
that NPC (`MarkNpcCovered`, `CastleVendorNpcInjector.cs:317`), so in the castle you interact with the
**NPC**, not the building.

The HUD **Talk** button is gated by `TalkPromptRegistry.Count > 0` and routes a press to the nearest
in-range NPC's `Interact` (`TalkHudBridge.cs:65,107-116`). A castle NPC registers Talk whenever the hero
is in range — unconditionally (`CastleVendorNpcInjector.cs:476`). So the farmer always lights the Talk
button.

`CastleNpcInteractable.Interact()` picks its branch from `ResolveRoute(structureId)`
(`CastleVendorNpcInjector.cs:561-563`, pre-fix):

```
upgrade-panel  when  IsUpgradableId && !IsShoppableId && !HasTalkFunctionId
talk-dialogue  otherwise
```

For `"farm"`: `IsUpgradableId("farm")` is **true** (it's a resource building —
`ResourceBuildingProgression.FarmId="farm"`, `ResourceBuildingProgression.cs:173,227`), it is **not**
shoppable (`buildings.json` farm has no `isShoppable`), and **not** a talk-function id. So farm →
**`upgrade-panel`** → `PanelRouter.Open(PanelId.BuildingUpgrade, "farm")`.

**Why it felt like "Talk goes nowhere":** the YarnSpinner removal (WO-557) deleted the parameterized
`StructureMenu` Yarn node that used to give the farm a real conversational beat (greeting + Buy/Sell/Talk/
Upgrade). After removal, pressing **Talk** on the farmer produces an **upgrade spreadsheet**, never a
conversation — the "talk function" leads to no talk. There is **no `"farm"` entry in `dialogues.json`**
(it held only `brom_intro`, `pet-house`, `SylasFirstMeeting`, `CompanionMeeting`), so the NPC had no line
to say. The Talk affordance survived the Yarn deletion; its conversational destination did not.

Cited: `CastleVendorNpcInjector.cs:469-528,561-563`; `TalkHudBridge.cs:65,107-116`;
`Tutorial/DialogueService.cs:83-109` (`PlayStructure`: conversation → shop → else false);
`dialogues.json` (no resource-building convos); `buildings.json:34-46` (farm, not shoppable).

## 2. General case — which other buildings shared the dead/disconnected Talk

Per-structure trace of the live castle NPCs (route + outcome, pre-fix):

| Structure (NPC)        | Shoppable | Upgradable | Conversation | Route (pre-fix) | Outcome |
|------------------------|-----------|------------|--------------|-----------------|---------|
| **farm** (Windmill)    | no  | yes (resource) | none | upgrade-panel | upgrade UI, never talks ← **reported** |
| **lumbermill**         | no  | yes | none | upgrade-panel | upgrade UI, never talks (same) |
| **arcane-tower**       | no  | yes (tier) | none | upgrade-panel | upgrade UI, never talks (same) |
| **armorer** / **forge**| yes | yes | none | talk-dialogue | shop opens — OK |
| **market** / **jeweler**| yes | — | none | talk-dialogue | shop opens — OK |
| **pet-house** (Echo)   | no  | no  | **yes** | talk-dialogue | Echo Warden dialogue — OK |
| **barracks** (drillmaster)\* | no | yes (tier) | none | talk-dialogue (`HasTalkFunctionId`) | **silent dead-end** — `PlayStructure` returns false (no convo, no shop) and the NPC path has **no fallback** |

\* No castle vendor NPC currently spawns a barracks role, but the dead-end is real for any barracks
talk surface (`BuildingInteractable` barracks falls to a "coming soon" note).

So **farm, lumbermill, arcane-tower** share the "Talk → upgrade panel, never a conversation" disconnect,
and **barracks** is a genuine silent no-op. (`BuildingInteractable` resource buildings open the upgrade
panel directly and never expose a Talk button, so they are unaffected; the issue is the NPC-fronted hub.)

## 3. Fix chosen — flavor dialogue (warm) + a no-dead-end safety net

**Chosen: author short flavor conversations** for the resource/upgrade-only NPCs and route their Talk to
that conversation, keeping Upgrade on the HUD context/Upgrade button (it already lights via
`HudBuildingFocus.Set` for upgradable ids). Rationale:
- These are visible **NPCs** (a peasant, a woodcutter, an arcanist). An NPC you can't talk to reads worse
  than one with a line. The owner's world is NPC-rich and conversational (memory: living village).
- Purely **additive + low-risk**: new JSON entries + one routing predicate; no behavior change for shops.
- **Oracle-safe**: `AutoPilotDriver.AssertVendorTalkRoute` only *asserts* that **shoppable** vendors route
  to `talk-dialogue` (still true); non-shoppable routes are informational (`AutoPilotDriver.cs:1180-1191`).

Changes:
1. **`dialogues.json` (both Resources + StreamingAssets copies)** — added 4 brief conversations: `farm`
   (Miller), `lumbermill` (Woodcutter), `arcane-tower` (Arcanist), `barracks` (Drillmaster). Each is a
   `portrait`-setting entry node → a 2-line flavor node that ends cleanly (lines-only terminal node ends
   via `DialogueRunner.PostLines`→`End`, confirmed `DialogueRunner.cs:152-164`). Portraits exist
   (`Resources/Portraits/{farm,lumbermill,arcane-tower,barracks}.jpg`).
2. **`CastleVendorNpcInjector.cs` — `ResolveRoute`** now prefers `talk-dialogue` when a conversation is
   authored (`HasConversation` → `DialogueCatalog.Find(id) != null`). So farm/lumbermill/arcane-tower/
   barracks now **talk**; their upgrade stays on the HUD context button.
3. **`CastleVendorNpcInjector.cs` — `Interact()` fallback**: if `PlayStructure` returns false, fall back
   to the building's upgrade panel (mirrors `BuildingInteractable`), else `FlowTrace.Warn` — so **no Talk
   ever silently no-ops again**, even on a future content gap.

### Owner decision flag
> Implemented the **flavor-talk** option (Talk = the NPC says a line; Upgrade = the HUD context button).
> The alternative was **panel-direct** (strip the Talk button from resource NPCs so only Upgrade shows).
> If you'd rather resource NPCs have **no** Talk at all, say so and we'll gate `TalkPromptRegistry.Register`
> on "has a real talk surface" instead. Flavor lines are placeholder copy — easy to reword.

## 4. Vendor/transactional buildings unaffected
Shoppable vendors (forge, armorer, market, jeweler) still route `talk-dialogue` → `PlayStructure` opens
their gear/shop panel (`Tutorial/DialogueService.cs:99-105`). `pet-house` still plays its Echo Warden
conversation. No shop/transaction path was touched.

## 5. Validation
- Brace check `CastleVendorNpcInjector.cs`: **90/90 OK**.
- `dialogues.json` (both copies) parse as valid JSON; ids = brom_intro, pet-house, SylasFirstMeeting,
  CompanionMeeting, **farm, lumbermill, arcane-tower, barracks** (identical in both copies).
- NOT gated/committed (per task) — orchestrator reconciles by explicit path.

## 6. Modified files (for reconcile — explicit paths)
- `Assets/Resources/Data/Canonical/dialogue/dialogues.json`
- `Assets/StreamingAssets/Data/Canonical/dialogue/dialogues.json`
- `Assets/_Modules/Village/NPCs/CastleVendorNpcInjector.cs`
- `WorkOrders/WORK_ORDER_576_building_talk_fix.md` (this file)

## 7. Suggested headless verify (CLI)
- `CompileGate` (compile).
- `AutoPilotDriver.AssertVendorTalkRoute` — should stay green; the new flavor ids now resolve
  `talk-dialogue` (informational for non-shoppable).
- Optional: a `DialogueCatalog.Find` smoke for the 4 new ids; felt-verify (PO) the farmer/woodcutter/
  arcanist/drillmaster each speak when Talk is pressed in MainCastle_Hall.
