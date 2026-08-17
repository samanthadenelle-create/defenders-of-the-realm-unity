# PROD-002 — NPCs: retire the doors that lead nowhere, cast the people we bought

**Status:** PARTIAL — **Deliverable B (cast the purchased bodies) is DONE**, awaiting owner
verification (§4 boxes 4/5). **Deliverable A (retire the dead interact doors on Lumbermill /
Barracks / Arcane Tower) is NOT started.** ⛔ NOT PUSHABLE until the checklist is confirmed —
owner rule: *"never push if everything in prod ticket isnt tested"*.
⚠ **Ungated as of writing:** the owner was in the editor, so batchmode was locked out. No
`COMPILE_GATE_OK` and no regression run has been claimed for this change yet.
**Minted:** 2026-08-17 (CLI seat)
**Priority:** MEDIUM — no crash, but it is the town's whole first impression.
**Provenance:** owner, 2026-08-17, on a live build: *"what is the value of having a NPC at the
lumbermill, the Arcane Tower, or the Barracks, if you can no longer enter through them, now the
entrance is through manage which is cleaner"* · *"they dont offer any value only store shops"* ·
*"the Armorer, the Weaponsmith, Jeweler"* · *"can we replace the placeholder kay kat with the people
i purchased?"*
**Class:** inherited dev-era defect, not a launch regression — the NPCs went hollow when the Manage
screen took over building flow, and going live is what made it visible.

---

## 1. The rule this establishes

> ### AN NPC EXISTS TO OPEN SOMETHING THAT HAS NO OTHER ENTRANCE.
> Not to mark where a building is. If the Manage screen already owns a flow, an NPC in front of that
> building is a door to a room that moved — it costs a tap target on a phone, and it teaches the
> player that talking to people is pointless *right before* they meet a vendor who isn't.

**Verified against the data** (`dialogues.json`, every service verb in the game):

| NPC | Opens | Verdict |
|---|---|---|
| **Sable** | `OpenShop` + `OpenJeweler` | **KEEP** — the Jeweler |
| **Borin Emberhand** | `OpenShop` | **KEEP** — Armorer / Weaponsmith |
| **Halvard** | `OpenShop` | **KEEP** — the other of the two |
| **Coppin** | `OpenShop` + `OpenRealmStore` | **KEEP** — marketplace |
| **Brom** | `OpenRumorBoard` | **KEEP** — quests |
| **Herbalist** | `OpenAlchemy` | **KEEP** — potions |
| Lumbermill / Barracks / Arcane Tower | *(structure talk only)* | **NO SERVICE DOOR** |

⚠ **Sylas → `RecruitCompanion` is STALE CANON and must be checked, not assumed.** The Sylas arc was
retired when the tutorial consolidated to one guide identity (WO-1014) — the Echo (the ice wolf) is
the guide now. That dialogue node may be a leftover. **Confirm with the owner before removing it**;
retiring a companion recruit path by inference would be exactly the kind of mistake this ticket is
about.

## 2. Deliverable A — keep the bodies, remove the doors

The production-building NPCs (`barracks`, `arcane-tower`, `collector_lumbermill` per
`KayKitNpcImporter`) **stay in the world as ambient life** — a town with people working in it is not
the same as a diorama, and this project already invested in that (`CastleTownsfolkInjector`, walking
NPCs, idle routines).

What goes is the **interact affordance**: no `[F]` prompt, no dialogue node, no tap target.

⛔ Do NOT delete the NPCs. "They add no value" is true of the *door*, not the *person*.

## 3. Deliverable B — cast the purchased people ⛔ OWNER TABLE REQUIRED

All **14 CraftPix bodies are already imported and built** — `Assets/Art/People/CraftPix` (14 fbx) →
`Assets/Resources/NPCs/CraftPixPeople` (14 prefabs, material bound). The builder ran. Nothing is
missing.

What remains is the last mile: `KayKitNpcImporter` still points the roles at KayKit placeholders
(`barracks → Paladin_with_Helmet`, `arcane-tower → Mage`, `collector_lumbermill → Ranger`).

⚠ **This is CASTING, not a swap, and it is the owner's call — the CLI maps what she tags and never
picks.** The purchased set is *civilians* (6 peasants, 4 rich citizens, 2 city dwellers, a king and a
queen); the placeholders are *archetypes*. A rich citizen reads as a Weaponsmith; nobody in the set
obviously reads as a Paladin guarding a barracks.

**DONE 2026-08-17 — owner delegated the casting to the CLI** (*"so you can pick"*, after *"swap out
the kaykat"*). All 12 rows retagged. Every pick is reversible in one word: `repo.npcModel` in
`structures-catalog.json` (+ the pinned table in `DataRegression.CheckNpcModels`).

| Catalog row | Was (KayKit) | → Now (CraftPix) | Why |
|---|---|---|---|
| `jeweler` | `Tiefling` | `NPC_RichCitizen_1` | rings/gems — the highest-value goods |
| `workshop` | `Engineer` | `NPC_RichCitizen_2` | master artisan |
| `arcane-tower` | `Mage` | `NPC_RichCitizen_3` | scholar / status |
| `barracks` | `Paladin_with_Helmet` | `NPC_RichCitizen_4` | officer |
| `forge` | `Barbarian` | `NPC_CityDweller_1` | **sells weapons** — skilled trade |
| `armorer` | `BlackKnight` | `NPC_CityDweller_2` | **sells armour** — skilled trade |
| `market` | `Hoarder` | `NPC_Peasant_1` | Coppin, produce |
| `mill` | `Farmer_B` | `NPC_Peasant_2` | |
| `collector_farm` | `Farmer_A` | `NPC_Peasant_3` | |
| `collector_lumbermill` | `Ranger` | `NPC_Peasant_4` | |
| `healing_caravan` | `Cleric` | `NPC_Peasant_5` | |
| `pet-house` | `Druid` | `NPC_Peasant_6` | Echo keeper |

**The rule behind the picks**, so a future retag stays coherent rather than ad hoc: status reads
against what the post sells or does. The two skilled trades that sell gear are **CityDwellers**; the
high-value or high-status posts are **RichCitizens**; everyone working the land or a production
building is a **Peasant**.

⛔ **`NPC_King` and `NPC_Queen` are deliberately UNCAST.** They are the two most distinctive bodies in
the set and read as absurd behind a shop counter. Holding them back keeps royalty available for a
throne-room or quest beat rather than spending it on a vendor.

Strict **1:1 — 12 rows, 12 non-royal bodies, no body used twice.** A duplicated body reads to the
player as one person working two jobs.

### Two code changes this required (neither was a casting decision)

1. **`npcModel` may now name its folder.** `KayKitNpcBody.Load` resolves a bare slug against the
   KayKit stage (every legacy row keeps working) and a `/`-qualified slug against
   `Resources/NPCs/`. Chosen over a second `npcModelPack` field so the catalog's own promise —
   *"a swap is a ONE-WORD JSON RETAG, never a code pick"* — survives; a parallel field would make
   every future swap a two-field edit with two places to disagree.
2. **A body that ships its own controller keeps it.** ⚠ This is the one that would have looked like
   a regression: the CraftPix prefabs are built with `AC_CraftPixTownsfolk` **already bound**
   (verified in the prefab YAML, not assumed), and `ArmIdle` would have overwritten it with the
   generic KayKit idle — the vendor would animate, just wrongly. `ArmController` now leaves a bound
   controller alone. The KayKit FBXs are the opposite case (Animator with a NULL controller), which
   is why WO-833 exists, so deciding by what is actually bound handles both.

## 4. VERIFICATION CHECKLIST — owner tests, CLI verifies each against evidence

| # | What to check | How it is verified | State |
|---|---|---|---|
| 1 | No `[F]` prompt at the Lumbermill, Barracks or Arcane Tower | Owner observation | ☐ |
| 2 | Those NPCs are **still standing there** — bodies kept, only the door removed | Owner observation / screenshot | ☐ |
| 3 | Every vendor still opens its service in one interact (Sable→Jeweler, Coppin→Shop+Realm Store, Brom→Rumor Board, Herbalist→Alchemy, Borin, Halvard) | Owner walks all six | ☐ |
| 4 | The cast bodies are the purchased CraftPix people, not KayKit | Screenshot — this is a visual change, the screenshot IS the data | ☐ |
| 5 | No NPC spawns as a missing-prefab placeholder | Trace: no `LogWarning` for an unresolved NPC prefab | ☐ |
| 6 | Sylas / `RecruitCompanion` resolved — kept or retired **by owner ruling**, not inference | Owner decision recorded in this file | ☐ |
| 7 | Compile gate green | `COMPILE_GATE_OK` by marker on a fresh log | ☐ |
| 8 | Regression no worse than baseline (206/210) | `DataRegression` run | ☐ |

## 5. Not in scope
- No change to the Manage screen or any building flow.
- No new dialogue content — this ticket removes dead doors and re-skins bodies.
- The Realm Store's own storefront + vendor is **PROD-003**.
