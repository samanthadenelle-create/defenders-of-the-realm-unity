# PROD-002 — NPCs: retire the doors that lead nowhere, cast the people we bought

**Status:** READY TO IMPLEMENT — §3 needs an owner casting table before code
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

**Fill this in and the code half is mechanical:**

| Role | Current placeholder | → CraftPix prefab |
|---|---|---|
| Armorer | — | |
| Weaponsmith | — | |
| Jeweler (Sable) | — | |
| Marketplace (Coppin) | — | |
| Rumor Board (Brom) | — | |
| Herbalist | — | |
| Lumbermill (ambient) | `Ranger` | |
| Barracks (ambient) | `Paladin_with_Helmet` | |
| Arcane Tower (ambient) | `Mage` | |

Available: `NPC_King`, `NPC_Queen`, `NPC_RichCitizen_1..4`, `NPC_CityDweller_1..2`, `NPC_Peasant_1..6`.

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
