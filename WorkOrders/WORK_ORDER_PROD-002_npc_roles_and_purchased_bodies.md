# PROD-002 — NPCs: retire the doors that lead nowhere, cast the people we bought

**Status:** FIXED — AWAITING OWNER FELT-TEST TO CLOSE. Prior status: DONE — **both deliverables shipped**, PENDING OWNER FELT-VERIFY (§4 boxes 4/5). ⚠ **TWO 2026-08-20 FOLLOW-UPS ARE RECORDED IN §0 BELOW** — one of Deliverable A's stated reasons was FALSE when written and has since been MADE TRUE (`890ff5656`), and Deliverable B grew a second half nobody knew was missing (`79c1e61b`, `9a2d1faae`).

**Deliverable A (retire the dead interact doors) is DONE** (2026-08-18, commit `233613615`, owner
ruled **(a)**): all three doors are shut — `collector_lumbermill` + `arcane-tower` earlier, and
**`barracks` in that commit** via `BarracksNpcInjector` + `BuildingInteractable.HasNoTalkDoor` (both
sites, so the building's own prompt does not silently come back). The once-teach drillmaster toast is
**REMOVED, not re-pointed** — option (b) was rejected by the owner. The drillmaster BODY stays; only
the affordance went. The `barracks_intro` `SeenTutorials` key is deliberately left in the schema and
simply stops being written.
**Deliverable B (cast the purchased bodies) is DONE** — 12 rows retagged, awaiting the same felt-verify.
⛔ NOT PUSHABLE until the checklist is confirmed — owner rule: *"never push if everything in prod
ticket isnt tested"*.
⚠ **Gate status:** commit `233613615` records `REGRESSION 206/210 = BASELINE`. It does **not** claim a
`COMPILE_GATE_OK` marker — treat the compile gate as unproven for this change until one is run.
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

## 0. ⚠ ADDENDUM 2026-08-20 — two things this ticket got half-right, corrected in the open

*(Appended, not rewritten. The body below is the record of what was decided on 08-17/08-18 and stays
exactly as it was written; this section is what later turned out to be true.)*

### 0.1 Deliverable A's stated reason — *"Manage owns training"* — WAS NOT TRUE WHEN WRITTEN

Commit `233613615` closed the **barracks** talk-door on the premise that the Manage screen already
owned training. **It did not.** `ManageScreenVM.BuildTroopsBrowse` only ever emitted an **Upgrade**
CTA — there was no Train row anywhere in it. So the door was closed against a replacement that had
never been built, and for two days the ONLY way into troop training was to **fail** the
army-readiness check on the Raids screen and be redirected there. The in-game help still told the
player to train at the Barracks — an instruction that had become impossible to follow.

Owner, on a live device build 2026-08-20: *"under manage i see option to upgrade the troops, but i
dont se a way to train troops."*

**FIXED in `890ff5656`, by making the premise true rather than by re-opening the door.** The
Manage ▸ Troops tab now emits a verb-led **TRAIN** row per trainable troop beside the existing
UPGRADE row, plus a free *"Armies — saved compositions"* entry into the muster. **The barracks
talk-door STAYS CLOSED** — canon is ONE Queues door and this is it, so Deliverable A's *decision*
stands; only its *justification* had to be earned after the fact. Pinned by the new suite
`ManageTroopsTrainDoorRegression` `[manage-train-door]`, run RED-then-GREEN (reverted VM → 8
failures naming the exact defect). The false comment at `BuildingInteractable.cs` was **corrected in
place, not deleted** — a comment recording a wrong REASON is how the next seat repeats the mistake.

> **The lesson this ticket now carries:** *"the replacement already exists"* is a claim that must be
> read at source before a door is closed on it. It was not, and it cost the owner two days of a
> feature that was fully built, shipped and regression-covered — and simply had no button.

### 0.2 Deliverable B (the purchased bodies) had a SECOND half — the animator, not the mesh

Retagging the 12 catalog rows put the right *bodies* in town. It did nothing about what those
bodies were *doing*, and two separate paths were both feeding town NPCs the **hero's combat
animation**:

1. **`79c1e61b`** — the QUEST CAST was still on staged KayKit placeholders. `QuestCastNpcInjector`'s
   two rows now name purchased CraftPix bodies through the same folder-qualified-slug contract the
   catalog rows use: **Village Elder → `CraftPixPeople/NPC_King`** (one of only two bodies no
   structure row claims, so the Elder's face appears nowhere else in the hub) and **Fenn Wildmane →
   `CraftPixPeople/NPC_Peasant_4`**. Same commit stopped the three NPC injectors arming a CraftPix
   person with `KayKitNpcIdle` — that controller plays the **Knight's combat standby**
   (`m-standby-idle`), right for a knight and wrong for a shopkeeper. New suite
   `NpcIdleControllerRegression` `[npc-idle-controller]`.
2. **`9a2d1faae` — a CORRECTION TO 79c1e61b, stated in the open.** That commit claimed
   `AC_CraftPixTownsfolk` already played a civilian clip. **It did not, and the claim was never
   checked** — the GUIDs said `Assets/Action/Shared/Shared_Idle.fbx` and `Shared_Walk_Forward.fbx`,
   i.e. the **HERO's mixamo locomotion**, and all **14** CraftPix bodies share that one controller.
   So every vendor, every wandering villager and both quest NPCs had been standing combat-ready and
   walking the hero's walk. Repointed to the civilian Supercyan `common_people@idle` /
   `common_people@walk` via the new editor entry `DeNelle.Editor.CraftPixTownsfolkAnimatorSetup`,
   which drives Unity's own `AnimatorController` API (never a hand-edit of the asset) and refuses to
   swap in a clip that is not imported Humanoid.

⚠ Both halves are **animator/appearance** work and headless gates cannot see them. The felt-verify
this ticket is already pending now covers them too: **look at whether the townspeople stand like
civilians.**

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

> ### STATUS 2026-08-18: ALL 3 DONE. The owner ruled **(a)** — close the door, retire the toast.
> **Done:** `collector_lumbermill`, `arcane-tower`, **`barracks`** (commit `233613615`). No ruling
> outstanding; the three options below are kept as the record of the decision, not as an open question.
>
> **It was not the one-line change this ticket implied.** The three structures reach their door by
> three DIFFERENT paths — `barracks` via `BarracksNpcInjector` (gated `ff.barracks`, default OFF),
> `arcane-tower` via `BuildingInteractable` (the building itself, no NPC involved), and
> `collector_lumbermill` via the vendor-injector roster.
>
> **And a structure has TWO doors, which are coupled.** The front NPC's `CastleNpcInteractable` AND
> the building's own prompt open the same dialogue; `MarkNpcCovered` makes the building defer so only
> one fires. ⛔ Remove the NPC's door alone and the building's prompt **silently comes back** — it was
> only suppressed while covered. The door would appear to MOVE rather than close. So the fix is one
> rule (`BuildingInteractable.HasNoTalkDoor`) consulted at BOTH sites: the NPC declines to open a
> door, the building declines to offer one, and the NPC injector returns BEFORE `MarkNpcCovered` so
> it never suppresses the building on behalf of a door that does not exist.
>
> **Deliberately an explicit list, not a derived "has no vendor row" rule.** A derived rule would
> also strip **Brom** (rumour board) and the **Herbalist** (alchemy), whose doors are their ONLY
> entrance — silently deleting real service access while looking like a tidy generalisation.
>
> ### ⛔ WHY BARRACKS IS HELD — the finding that changes the ticket
> The drillmaster's Talk opens **only** `DialogueService.PlayStructure("barracks", …)` — structure
> dialogue, **not** a training panel. By this ticket's own rule that is a dead door and should close.
> **But `BarracksNpcInjector` fires a once-teach toast:**
> > *"Elarion needs soldiers. The drillmaster at the Barracks trains them."*
>
> Close the door and that toast points the player at nothing. It is arguably already wrong — the
> drillmaster does not train anyone today; Manage does. Either way the resolution is **player-facing
> copy**, which is the owner's call, so the door stays until she makes it. Three options:
> **(a)** close the door and retire the toast; **(b)** close the door and re-point the copy at Manage
> (*"…train them from Manage"*); **(c)** keep the door because the drillmaster SHOULD open training,
> in which case this is a missing feature, not a dead door — and that is a different ticket.
> ⚠ Low urgency either way: `ff.barracks` defaults OFF, so it is not reaching most players today.

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
| 4 | The cast bodies are the purchased CraftPix people, not KayKit | Owner on a live Play session, 2026-08-17: *"i reset character and let it rebuild and all is perfect"* | ☑ |
| 5 | No NPC spawns as a missing-prefab placeholder | Owner observation (same pass) + the `npcModel` oracle resolving 12/12 to real `.prefab`s | ☑ |
| 6 | Sylas / `RecruitCompanion` resolved — kept or retired **by owner ruling**, not inference | Owner decision recorded in this file | ☐ |
| 7 | Compile gate green | `COMPILE_GATE_OK` by marker on a fresh log | ☑ |
| 8 | Regression no worse than baseline (206/210) | `DataRegression` = **206/210, baseline**; `npcModel` oracle 12/12, every row a real `.prefab` | ☑ |

### ⚠ OPEN RISK, NOT A CLOSED ITEM — stale saves showed artifacts a fresh one does not

On the FIRST verification pass, against an EXISTING save, the owner saw the **farm on fire** and a
**barracks that looked invisible**. Both vanished after *"i reset character and let it rebuild"*.

⛔ **"It went away on reset" is not the same as "it is fixed", and the difference matters here
because the game is LIVE.** The owner could reset; a player who already has a town cannot be told to.
If this reproduces on any pre-existing save, it reaches real players on the next update.

What is actually known, from data rather than inference:
- `state.buildingDamage` in the live save was **`{}` — empty**. Nothing was damaged, so the fire was
  **NOT** a legitimate `StructureDamageVisuals` state (fire arms at HP ≤ 0.25 =
  `1 - RepairTarget.DamageFraction`). Whatever lit it, it was not the damage data.
- The save's `baseLayout` listed `collector_farm` and `barracks` normally, at level 1, with no
  damage entry — so the persisted layout was healthy too.
- Both symptoms sat on structures seated by the **bake/injector path on a first town load** — the
  same path recorded on the `tower_ground_archer` row as never reaching the catalog code. That makes
  stale seated state the leading candidate, but it is a CANDIDATE: nobody has captured a trace of
  the burning farm, so the cause is unproven and must not be written up as solved.
- ⚠ It is NOT attributable to the PROD-002 swap on the evidence available — the swap changes
  `repo.npcModel` (which body an NPC wears) and touches no structure mesh, health or VFX path.
  That is an argument from what the change *touches*, not a captured proof of innocence.

**To settle it:** F8 while a stale-save town is showing the burning farm, and read the `[Flow:*]`
lines around the structure — specifically whether a pooled VFX was reparented onto it
(`VFXManager.ReturnToPool` / WO-929 deferred-reparent is a known class) versus
`StructureDamageVisuals` arming a fire on a bad fraction. Until that trace exists this stays OPEN.

## 5. Not in scope
- No change to the Manage screen or any building flow.
- No new dialogue content — this ticket removes dead doors and re-skins bodies.
- The Realm Store's own storefront + vendor is **PROD-003**.
