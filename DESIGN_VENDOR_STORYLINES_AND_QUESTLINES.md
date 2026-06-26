> ⚠ **STALE — predates the 2026-06-22 single-Knight pivot.** Treat its Blink-hero / party-of-4 / tower-defense-pillar framing as SUPERSEDED (hero = single Tripo "Grom", Blink rig junked, base-defense V2-gated); some architecture/monetization content may still hold. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md` + `docs/COMBAT_PIVOT_NORTHSTAR.md`.

# Design — Vendor "Talk" Storylines → Questlines (Elarion)

**Status:** DESIGN (creative call — owner approves names/text). Feeds WO-109 (NPC Yarn dialogue),
WO-116 (NPC dialogue/bark system), WO-151 (village progression), WO-238/227 (Sylas + cutscene).
**Canon:** Village = **Elarion**. Centre = **Heart of Elarion** (Tree of Life, 0,0,0). Companion =
**Sylas**. Heroes: Sylas (Archer), Grom (Knight), Thrain (Mage), Elara (Cleric). Resources: Wood, Food,
Iron, AetherCrystal ("Crystals"), Glimmer (earned premium). Enemy families: Orc Warband, Skeleton
Legion / Hollow Ones, Stonebelly Trolls, Orc Necromancer. Regions: Verdant Forest (E), Frost Peaks (N),
Stone Mountains (W), Ashen Wastes (S).

---

## 0. The spine that ties everything together — "The Dimming"

The Heart of Elarion is **dimming**: its aether — the realm's lifeblood — is being siphoned by an
**Orc Necromancer** who feeds on it from the Ashen Wastes, which is why the regions corrupt and the
waves never stop. The player can't fix this by fighting alone; Elarion must be **rebuilt and rekindled**.

Every vendor's casual "Talk" line is a small thread; pull it and it becomes a **personal questline**.
Each questline (a) teaches/uses one core system, and (b) yields a **Keystone** — a tangible token of
that craft restored (a forged blade, a full granary, a cut heartgem…). The Resource Steward's master
quest needs those Keystones to **awaken the Spire** and march on the Necromancer. So the vendors aren't
side-shops; they are the **chapters** of the main arc, gated to the player's progression.

**Convergence rule:** collect **N Keystones** (recommend 6 of the 8 vendor Keystones) → Warden Alric's
finale unlocks → **Spire Defense Mode** (DEF-37/38) → Necromancer boss → Heart relit (new-game-plus hook).

---

## 1. How the "Talk" option evolves (the "stemming" mechanic)

Each NPC's **Talk** node is **stage-aware** — it reads Yarn variables and shows a different conversation
as the world changes, so the same button deepens over time:

- **Stage 0 — Stranger:** flavour + a complaint that *seeds* the hook (no quest yet).
- **Stage 1 — Hook:** the complaint becomes an ask → starts the questline.
- **Stage 2..n — In progress:** progress check-ins, lore drips, partial rewards.
- **Stage END — Bonded:** quest done; Talk now offers the upgraded shop/recipe + a standing perk +
  a forward rumor that points at the next vendor (so the web self-advertises).

Gating variables (examples): `$villageLevel`, `$cleared_region_west`, `$keystone_forge`, `$q_forge_stage`.
Talk always also offers the **shop/craft/upgrade** command for that vendor (WO-106/109) so commerce and
story share one entry point.

---

## 2. The vendors (NW Artisan unless noted) — talk storyline + questline

### 2.1 Borin Emberhand — Blacksmith / Forge
**Role:** weapon crafting + upgrades. **System taught:** crafting tiers, Iron economy, a Stone Mountains foray.

- **Talk Stage 0:** "Hmph. A hero with a starter blade. That iron's soft as cheese — and the good ore's
  gone bad since the Heart started to dim." *(seeds: tainted iron / west region)*
- **Talk Stage 1 (Hook):** "My forge's been cold a week. Bring me wood and iron and I'll relight it —
  then we'll see about a real weapon." → starts **"The Last Ember."**
- **Questline — The Last Ember:**
  1. Relight the forge — deliver Wood ×N + Iron ×N (teaches harvest → spend).
  2. Quench the first true blade — bring 1 AetherCrystal (teaches crystal value).
  3. Field-test it — survive/clear a Stonebelly Troll raid at the west gate (teaches combat + the new weapon tier).
  4. Reveal: the ore is *deliberately* fouled — aether-rot traced to the **Stone Mountains**. (hook → region/boss)
- **Rewards:** weapon Tier 2 unlocked in shop; recipe for class weapons; **Keystone: the Emberbrand**.
- **Ties to:** Armorer (shares the salvage), Stone Mountains region clear, combat-feel pass.

### 2.2 Dame Halvard — Armorer
**Role:** armor shop/upgrades. **System taught:** armor tiers, defense waves, a rescue subplot (Grom).

- **Talk Stage 0:** "Stand straight. That padding won't stop an orc's first swing, let alone the second."
- **Talk Stage 1 (Hook):** "I can plate you proper — if you can find me salvage. There's a fallen
  garrison's gear out past the wall. Bring it back; don't bring back their fate." → **"Shields of the Fallen."**
- **Questline — Shields of the Fallen:**
  1. Recover salvage from a cleared enemy camp (teaches camp clear → claim).
  2. Reforge plate at Borin's now-lit forge (cross-vendor: needs The Last Ember Stage 1).
  3. Hold the line — survive a timed defense wave in the new armor (teaches defense + armor tiers).
  4. Reveal: the garrison was **Grom's** old company; one may still be alive in the Frost Peaks. (hook → rescue + hero lore)
- **Rewards:** armor Tier 2 + shield; standing "Halvard's Bulwark" perk (small wall HP buff);
  **Keystone: the Oathshield**.
- **Ties to:** Borin (forge), Grom hero backstory, Frost Peaks region.

### 2.3 Old Pell — Lumbermill
**Role:** wood production. **System taught:** harvest sites, nature/terrain restoration.

- **Talk Stage 0:** "Used to be I'd fell a tree and two'd grow back. Now the wood comes up *grey*. The
  forest's sick, same as the Heart."
- **Talk Stage 1 (Hook):** "Clear the blight off my old grove and I'll get the saws singing again." → **"Roots Run Deep."**
- **Questline — Roots Run Deep:**
  1. Clear a corrupted harvest site in the Verdant Forest (teaches resource nodes + region travel).
  2. Carry a sapling from the Heart of Elarion and plant it (teaches the Heart as living centre).
  3. Defend the sapling through one night-raid (teaches escort/defend).
- **Rewards:** Wood throughput +%; nature population returns to that region (visual + future yields);
  **Keystone: the Heartwood Sapling**.
- **Ties to:** Mother Wren (food/forestry), terrain foundation (DEF-61/62), Verdant region foothold.

### 2.4 Mother Wren — Mill & Granary
**Role:** food → population. **System taught:** Food→Population loop, workers, offline accrual.

- **Talk Stage 0:** "Empty bellies, empty walls, dearie. No one stands a watch on an empty stomach."
- **Talk Stage 1 (Hook):** "Get my mill turning and feed the folk — full bellies make full ranks." → **"Full Bellies, Full Ranks."**
- **Questline — Full Bellies, Full Ranks:**
  1. Restore food flow — build/upgrade the mill (teaches building upgrade + Food faucet).
  2. Grow the population — reach a population threshold (teaches Food→Pop, faster gathering).
  3. Put hands to work — assign workers to a node and let it accrue while away (teaches worker dispatch + offline accrual).
- **Rewards:** population cap +; gathering-speed scaling unlocked; **Keystone: the Full Granary**.
- **Ties to:** Old Pell, worker dispatch (WO-117), offline accrual (WO-115), pet harvest (Fenn).

### 2.5 Sable Vey — Jeweler / Lapidary
**Role:** crystal → gem cutting. **System taught:** gem sockets, rare crystal spawns, a moral subplot + Glimmer.

- **Talk Stage 0:** "Raw aether? Crude. Cut it right and it *sings* — and sings louder in the right hands."
- **Talk Stage 1 (Hook):** "Bring me a flawless crystal. The kind that only surfaces when the veil's
  thin. I'll show you what aether can really do." → **"Aether's Facet."**
- **Questline — Aether's Facet:**
  1. Chase a **rare timed crystal spawn** and harvest it before it fades (teaches rare-spawn loop).
  2. Cut your first heartgem — socket it for a stat/cosmetic boost (teaches gem sockets).
  3. **Choice:** Sable's been quietly selling cut aether to an outside broker. Turn a blind eye for
     cheaper gems, or report it and gain the village's trust. (branching consequence → Coppin/Glimmer)
- **Rewards:** gem socketing on gear; **Keystone: the First Heartgem**. (choice changes a later price/trust modifier)
- **Ties to:** crystal mine + rare spawns (WO-153/154), Glimmer economy (Coppin), the Necromancer (who's the broker's real buyer — late reveal).

### 2.6 Coppin the Trader — Market / Commerce (NE)
**Role:** trade, Glimmer earn path, rumor network. **System taught:** Glimmer faucet, trade routes, the quest board feed.

- **Talk Stage 0:** "Buy, sell, survive — that's the whole song, friend. Trouble is, the roads aren't safe to sing it on."
- **Talk Stage 1 (Hook):** "Clear the road to the next outpost and I'll cut you in on the Glimmer that
  flows down it." → **"The Glimmer Road."**
- **Questline — The Glimmer Road:**
  1. Clear/escort a trade road to an outpost (teaches overworld travel + protect).
  2. Establish a route → a steady **Glimmer** trickle (teaches the earn path; cosmetics, never pay-to-win).
  3. Expand the network → unlocks the **rumor board** that surfaces other vendors' Stage-1 hooks.
- **Rewards:** Glimmer faucet; store discounts; rumor board (acts as the soft quest log). **Keystone: the Trade Charter**.
- **Ties to:** cosmetic store (WO-236), Glimmer earn (DEF-29), Sable's subplot, Brom's board.

### 2.7 Brom Aleward — Innkeeper (SE Housing) — THE QUEST HUB
**Role:** the connective tissue. **System taught:** quest discovery, overworld raids, rest/respawn.

- **Talk Stage 0:** "Pull up a chair. Every tale in Elarion passes through this hearth eventually."
- **Talk (always):** Brom's Talk lists **available rumors** = the live hook list for the other vendors +
  overworld raid alerts. He's how the player *finds* quests without a heavy UI.
- **Personal quest — Last Call:** defend the inn during a surprise raid (teaches a contained defense +
  rewards a respawn/rally point). Late beat: Brom remembers a traveler matching the Necromancer's
  description — places the villain before the finale.
- **Rewards:** rally/respawn point; rumor board upgrades (shows quest stages + rewards); **Keystone: the Hearthstone**.
- **Ties to:** every vendor (board), overworld raids (WO-143/160), Death/respawn screens (WO-235).

### 2.8 Fenn Wildmane — Stablemaster / Pet Trainer (SW Pet)
**Role:** pets. **System taught:** taming, pet abilities, auto-harvest/guard.

- **Talk Stage 0:** "A beast'll follow a kind hand further than gold ever could. Mine all ran off when the Heart dimmed."
- **Talk Stage 1 (Hook):** "Tame one of the wild ones out past the gate and I'll teach you to fight beside it." → **"Wild Hearts."**
- **Questline — Wild Hearts:**
  1. Track and tame a beast in its region (teaches pet acquisition).
  2. Train an ability — e.g. the anti-ranged guard (teaches pet abilities, ties WO-128).
  3. Put it to work — assign the pet to **auto-harvest** a node or **guard** an outpost (teaches pet economy + defense).
- **Rewards:** extra pet slot; pet auto-harvest/guard unlocked; **Keystone: the Bonded Beast**.
- **Ties to:** pet auto-harvest (WO-119), pet anti-ranged (WO-128), node settlements (Fenn's pets garrison them).

### 2.9 Warden Alric — Resource Steward / Upgrade Hall (central NW) — THE MASTER QUEST
**Role:** village progression. **System taught:** tier-ups that consume the others' outputs; the finale.

- **Talk Stage 0:** "Elarion was a fortress once. We can be again — but a wall is only as strong as the
  hands that built it. Bring me the realm's crafts, and I'll bring back the light."
- **Master Questline — Rebuild Elarion (gates on the others):**
  1. Raise the village to Tier 2 (needs Granary + Lumbermill restored → Mother Wren + Old Pell Keystones).
  2. Re-arm the walls (needs Forge + Armorer → Borin + Halvard Keystones).
  3. Rekindle the wards (needs Heartgem + Bonded Beast + a tamed region → Sable + Fenn).
  4. **Finale — Awaken the Spire:** with ≥6 Keystones, the Spire rises; survive **Spire Defense Mode**
     (DEF-37/38), then march on the **Orc Necromancer** in the Ashen Wastes. Win → the Heart is relit.
- **Rewards:** village tiers, automated rampart defenses, the endgame boss, NG+ seed. **Keystone-sink, not -source.**
- **Ties to:** literally every other vendor; Spire (DEF-37/38), Necromancer (WO-190), castle fortification (WO-104/114).

---

## 3. Keystone map (quick reference)

| Vendor | Quest | Keystone | Core system it teaches |
|---|---|---|---|
| Borin (Forge) | The Last Ember | Emberbrand | Weapon crafting / Iron |
| Halvard (Armorer) | Shields of the Fallen | Oathshield | Armor tiers / defense waves |
| Old Pell (Lumbermill) | Roots Run Deep | Heartwood Sapling | Harvest sites / terrain |
| Mother Wren (Mill) | Full Bellies, Full Ranks | Full Granary | Food→Pop / workers / offline |
| Sable (Jeweler) | Aether's Facet | First Heartgem | Gem sockets / rare spawns |
| Coppin (Market) | The Glimmer Road | Trade Charter | Glimmer / trade / rumor board |
| Brom (Inn) | Last Call | Hearthstone | Quest discovery / respawn |
| Fenn (Pets) | Wild Hearts | Bonded Beast | Pets / auto-harvest / guard |
| **Alric (Steward)** | **Rebuild Elarion** | *(consumes ≥6)* | Village tiers → Spire → boss |

---

## 4. Implementation notes (so this is buildable, not just lore)

- **Dialogue:** one Yarn file per NPC in `Assets/Dialogue/NPCs/NPC_<Vendor>.yarn`, started by the
  stationed NPC's `DialogueRunner` (WO-109). The Talk node branches on Yarn vars for the Stage 0..END model.
- **Commands (NPCCommandBridge):** existing `OpenShop/OpenCraft/OpenUpgrade/OpenEquip` plus **new**
  `StartQuest <id>`, `AdvanceQuest <id>`, `CompleteQuest <id>`, `SetFlag <key>`, `GiveKeystone <id>`.
- **Quest state — needs a lightweight `QuestService`** (likely a NEW work order): GameState-backed map of
  `questId → stage` + flags + Keystone set; surfaced in a quest tracker (extend `DailyQuestHud`) and on
  Brom's rumor board. Gate vendor Talk stages off it.
- **Persistence:** quest/Keystone state lives in GameState, **keyed by wallet** (see persistence lane) so
  the questline survives logins.
- **Gating cadence:** tie Stage-1 hooks to `$villageLevel` / region clears so quests unlock in a sane
  order (Forge & Mill early; Jeweler/Pets mid; Alric's finale last).
- **Companion voice:** Sylas can interject one line at each Stage-1 hook (cheap reuse of the companion
  system, WO-238/227) to make the web feel authored.
- **Suggested WOs to spin up:** (a) `QuestService` + tracker UI; (b) Vendor Yarn pack (9 files) +
  NPCCommandBridge quest verbs; (c) Keystone → Spire finale wiring (depends on DEF-37/38).
