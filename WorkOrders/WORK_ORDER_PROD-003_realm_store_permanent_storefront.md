# PROD-003 — The Realm Store gets its own permanent storefront

**Status:** FIXED — AWAITING OWNER FELT-TEST TO CLOSE. Prior status: IMPLEMENTED — AWAITING OWNER FELT-VERIFY (2026-08-18; owner ruled §4 = (a), NO ruling outstanding).
Shipped across two commits: `233613615` (placement) and `72a43ea36` (scale, collider, oracle).
DONE: storefront baked into Main_Castle_Overworld at (12,0,-32) by `Assets/Editor/RealmStorePlacer.cs`; RealmStoreVendor
opens PanelId.RealmStore via the PackStoreBootstrap opener; NOT in structures-catalog.json or build-categories.json
(both dual copies verified identical); not an IDamageableStructure; Coppin's shortcut intact and first in dialogues.json.
**§3.4 SCALE — CLOSED.** Now routed through `VisualFactory.Skin` with `FitHeight` (the same seam `StructureFactory.Create`
uses), so the scale is DERIVED, never typed. Measured: scale 6.46, boundsSize (6.68, 4.00, 7.71), height exactly 4.00 m
against the 1.0 building-base cadence. Was ~1.2 m in a town of 4 m buildings.
**COLLIDER — CLOSED, and the ticket's stated mechanism was WRONG.** The re-run was never blocked by the
"add a collider if none" guard (the FBX imports `addColliders: 0` and the placer destroys the prior instance first).
The real defect: `Renderer.bounds` is a WORLD-space AABB assigned straight into a LOCAL-space BoxCollider on a root
yawed ~20.5 deg, so the box was inflated by the yaw even when the mesh was right. Now always recomputed with the root
temporarily unrotated. Old size (1.034, 0.620, 1.195) centre z=0.4999 -> new size (4.705, 4.000, 6.474) centre (0, 2.000, 0).
**§3.6 ORACLE — CLOSED.** `Assets/Editor/Regression/RealmStorefrontRegression.cs`, tag `[realm-storefront]`, registered in
DataRegression (suite count 210 -> 211). Asserts MEASURED values, not existence: baked exactly once at the resolved
placement, height within 10% of the derived target, base seated, vendor present, every script resolved and checked against
IDamageableStructure, and absent from all FOUR catalog artifacts (whole normalised values, never substrings — both
`structures-catalog.json` copies carry an authoring note saying "Realm Store" in prose that a grep would have gone red on).
OPEN — 1 gap:
  1. §3.2 THERE IS NO VENDOR NPC. `RealmStoreVendor` is a 6 m proximity component on the BUILDING; no NPC body is placed.
     ⛔ WHICH BODY IS A CREATIVE CALL FOR THE OWNER — deliberately not invented. Blocks nothing else.
DURABILITY NOTE (mitigated, not closed): placed by a standalone editor script, not CastleHubBuilder. It survives a
CastleHubRoot rebuild (scene-root sibling) but is NOT recreated by a hub rebuild from an empty scene. That loss is now
LOUD — the `[realm-storefront]` suite goes red and both scripts carry headers naming the coupling. Making CastleHubBuilder
own it is structural work for its own ticket.
GATES (2026-08-18): `COMPILE_GATE_OK` · `REALM_STORE_PLACED_OK` · `NAVMESH_BAKE_OK 1 surface` ·
`REALM_STORE_REACHABLE_OK nearest walkable 0.08m` (was 0.33 m) · `REGRESSION 207/211` — the same 4 known-baseline reds,
no new red. Scene delta +1,163 bytes (no resave bloat).
Owner-felt items still open: how it LOOKS at 4 m, whether the position reads as its own establishment rather than a second
stall beside Coppin, and a walk-up interact test (§6.6). ⛔ NOT PUSHABLE until felt-verified — owner rule:
*"never push if everything in prod ticket isnt tested"*.
**Minted:** 2026-08-17 (CLI seat) — minted as WO-1117 minutes before the PROD series was ruled; RENUMBERED, not duplicated. 1117 returned to the legacy pool unused.
**Lane:** Monetization / town. Touches the build catalog and the hub scene builder.
**Provenance:** owner, 2026-08-17, from a device screenshot: *"the store offers a place to buy
potions, sell stuff, then realm store, seems like the Realm store should be more prominent? or at
least first under store options"* → *"can we give monetization its own Storefront?"* → ***"something
with a static location not destructible"***.

---

## 1. What it looks like today, and why that is wrong

The **only** monetization surface in the game is a dialogue option on **Coppin, a produce vendor**:

```
Coppin — Marketplace
"Come, have a look before the good pieces go."
  > I'd like to buy.
  > I'd like to sell.
  > Show me the Realm Store.      ← the entire store, third in a list, below the scroll fold
  > Just passing through.
```

The screenshot that prompted this shows a **scrollbar** on that option list — so on a Seeker the store
sits at or past the fold of a conversation the player has to walk up to and start.

**Interim fix already applied (2026-08-17):** the Realm Store option is now FIRST in Coppin's list
(`dialogues.json`, both dual copies synced). That is a two-minute mitigation, not the answer — it
makes the store visible *once you are already talking to a produce seller about turnips*.

> ### THE REAL PROBLEM IS CATEGORY, NOT ORDER
> The Realm Store is not a vendor's inventory. It is the game's storefront. Reaching it through
> another merchant's small talk misclassifies it, and no amount of reordering fixes a category error.

---

## 2. ⛔ THE REQUIREMENT THAT SHAPES EVERYTHING: STATIC AND INDESTRUCTIBLE

Owner ruling, verbatim: ***"something with a static location not destructible"***.

This is a **deliberate exception** to the town model and it must be recorded as one, because it
contradicts a rule that is otherwise binding. CLAUDE.md §8 records the live monetization model as the
**player-built town** — *"strategic placement ALWAYS ON — movable functional storefronts + vendor
NPCs"*. Every other storefront is placed by the player, movable, sellable, and damageable.

**The Realm Store must be none of those, and the reason is not aesthetic:**

- **Sellable** → a player sells their own store and cannot buy anything again.
- **Movable** → a player can bury it behind walls or in an unreachable corner.
- **Destructible** → a raid takes the store OFFLINE. Revenue becomes a function of whether the last
  wave reached a particular building.
- **Player-placed at all** → a brand-new player has no store until they choose to build one, which is
  exactly backwards for the first-session player who is most likely to spend.

Every one of those is a self-inflicted outage on the only surface that earns money. A storefront that
can be lost is a storefront that will be lost, and the player who loses it has no way to know why the
game stopped selling to them.

**Therefore:** baked into the hub by the scene builder at a fixed location, absent from the build
catalog entirely, and NOT an `IDamageableStructure`.

⚠ Do NOT implement "indestructible" as a huge HP pool or a damage-immunity flag on the normal
structure path. It must not be a `IDamageableStructure` at all — anything that participates in the
damage system participates in its bugs, and a raid that "can't quite" destroy the store is one balance
change away from destroying it.

---

## 3. Scope

1. **A baked storefront in `Main_Castle_Overworld`** — placed by the hub builder (`CastleHubBuilder`
   or the appropriate partial; ⚠ `VillageSceneBuilder.cs` is a §9 serialization bottleneck — one
   agent at a time, and check which builder actually owns the hub before editing).
2. **Its own vendor NPC**, distinct from Coppin, whose single purpose is the Realm Store. One
   interact → `PanelRouter.Open(PanelId.RealmStore)`. The opener is already registered at boot by
   `PackStoreBootstrap`, so this is a door, not a system.
3. **NOT in the build catalog.** It must not appear in the build palette, cannot be placed, moved,
   sold or upgraded. It is world furniture, like the Heart.
4. **Visually distinct and legible at a distance** — this is the one building a player should be able
   to find without being told where it is.
   **ART IS STAGED (2026-08-17):** `Assets/Resources/Structures/RealmStore.fbx` — a dedicated
   owner-purchased model, git-tracked via a per-asset `.gitignore` negation, albedo verified bound
   (`RealmStore_basecolor`), and confirmed `Resources.Load<GameObject>`-able by
   `TripoStructureMaterialAudit.VerifyCatalogArt`. ⚠ It is deliberately **NOT** wired to a
   structures-catalog row — per §3.3 this building must never enter the build palette, and giving it
   a catalog row is precisely how it would. The hub builder instantiates it directly.
5. **Keep Coppin's Realm Store option** as a convenience shortcut (already reordered to first). Two
   doors to one panel is fine; zero prominent doors is the defect.
6. **A regression oracle:** the storefront exists in the baked scene, is NOT in the build catalog, and
   does NOT implement `IDamageableStructure`. ⚠ Pin it against the ARTIFACT, not just the code — the
   2026-08-17 dungeon lesson (WO-1049 §5b) is that a gate on the producer cannot see a stale bake, and
   this building lives in a baked scene.

---

## 4. ✅ RULED 2026-08-18 — option **(a)**, the south plaza (was: OWNER RULING NEEDED)

> **The owner ruled (a).** Sited at **(12, 0, -32)** — south plaza, across the open centre from
> Coppin at (0,0,32), offset ~16 m east of the south-gate corridor because (0,0,-32) is exactly
> where the Jeweler was removed from for blocking the south door. Nothing below is open; it is the
> record of the decision.

The requirement is "static", which means the location is a one-time decision that is expensive to
change later (it is baked, and saves reference the world). Options:

- **(a) The south plaza / market square**, near Coppin. Thematically coherent, and the player already
  walks there. ⚠ But it keeps the store adjacent to the produce vendor it is trying to be
  distinguished from.
- **(b) Beside the Heart of Elarion.** The most-visited spot in the town and the one every player
  learns first. ⚠ It also puts a cash register next to the game's sacred object — a tonal call only
  the owner can make.
- **(c) On the main path between the spawn point and the castle.** Highest unavoidable footfall.
  ⚠ Highest risk of reading as intrusive.

**Recommendation: (a)**, but sited across the plaza from Coppin rather than beside him, so the two
read as different establishments in one market rather than two stalls of the same one. It gets
footfall without ambushing the player at the Heart.

---

## 5. Explicitly NOT in scope

- Do NOT change what the Realm Store SELLS, or any pricing.
- Do NOT touch the three payment refusal layers.
- Do NOT add the storefront to `build-categories.json` — that is the bug this WO exists to prevent.
- Do NOT make it damageable "for realism".

## 6. Acceptance

1. A visually distinct Realm Store building stands at a fixed hub location on a fresh save AND an
   existing one.
2. It cannot be selected, moved, sold or upgraded, and does not appear in the build palette.
3. It takes no damage during a wave that reaches it, and is not an `IDamageableStructure`.
4. Its NPC opens `PanelId.RealmStore` in one interact.
5. Coppin's shortcut still works.
6. `COMPILE_GATE_OK` + `REGRESSION_OK`, plus a screenshot — this is a visual change and the
   screenshot is the data.
