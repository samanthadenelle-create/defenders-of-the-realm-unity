# Felt-test walkthrough — the 2026-08-24 APK

**Build:** `Builds/Android/DefendersOfTheRealm.apk` · 545 MB · commit `e2e07f1c0`
**Content:** `R2_PUSH_OK 43 uploaded` → `R2_PARITY_OK 43 object(s) verified`
**Server:** `api/` deployed to production, same commit.

⭐ **Ordered by where you STAND in the game, not by ticket number** — so you walk one lap instead of
jumping between town, store, dungeon and raid.

⚠ **Two things make this build different from every previous one:**
- **Ads are LIVE.** `RewardedAdSkip` went `defaultOn: true` in `f62bfa28d`. Never run on hardware.
- **The mainnet purchase path is LIVE and the server now answers.** `/api/purchases/quote` and
  `/api/auth/session` were returning **404** until tonight.

---

# ⭐ If you only have one sitting — these six, in this order

Ranked by most-likely-broken.

| # | What | WO | Why first |
|---|---|---|---|
| 1 | **Boot: are enemies real art or capsules?** | 1124 / PROD-011 | ⛔ **Fails SILENTLY** — no error on screen. Went wrong three times (08-18, 08-19, 08-20). **Gates everything else** |
| 2 | **Night Market prices — read every digit** | UI-001 | Never screenshot-proven, and the defect was *wrong prices* |
| 3 | **Watch an ad to skip** | 1120 + 1125 | Went live 12 hours ago, never run on hardware |
| 4 | **Dungeon → town, look at the shield** | PROD-005 | The ticket admits the ancestor bug is unverified |
| 5 | **Offline download, then airplane mode** | PROD-010 | 2 minutes, and it is the last thing on a shipped feature |
| 6 | **One real mainnet purchase** | 1159 | First money that would ever move |

---

## 1. Boot / first load

**Look for:** enemies as real art, buildings as real models — ⛔ **not tinted capsules, not grey
placeholders.**
⚠ **Fail tell:** capsules with **no error on screen**. If you see them, this build's R2 bundles never
reached the CDN. *(They did — `R2_PARITY_OK 43` — but this is the check that proves it on device.)*

## 2. Town — walk a lap

| Where | Look for | WO | Fail tell |
|---|---|---|---|
| Town HUD | Collectors chip reads the whole **"Tap to collect"**; bottom bar says **"Manage"**, not "Manag…" | **1144** | Any mid-word cut; "TIER UP!" painted over the world tree |
| A damaged structure | Repair marker **hugs the hut (~3 m)**, grass visible through it, the full word "Repair" | **PROD-013** | A giant opaque purple slab ~20 m tall, label sheared to "Rep" |
| Lumberyard / foundry / silo | The pile **steps up in five visible tiers** and spills past the frame at cap | **903** | A bare frame while the bank has stock, or an abstract fill **bar** (that is the silent fallback) |
| South plaza (12, 0, −32) | A **~4 m Realm Store building**; one interact opens the store | **PROD-003** | A tiny ~1.2 m model, or nothing. ⭐ Test on an **existing save** too |
| Lumbermill / Barracks / Arcane Tower | ⛔ **No `[F]` prompt** at those three; all six vendors open in one interact | **PROD-002** | Talk prompt returns, or NPCs standing in combat stance. ⚠ **Test a stale save** |
| Anywhere currency shows | The word **"Glimmer" appears nowhere**; owned cosmetics still owned | **1126** | Any glimmer readout. ⭐ Expect **24 quests / 63 stages** |
| First sight of the Arcane light column | A **2D Echo dialogue** pops, portrait only, **once ever** | **1151** | No explanation, an Echo 3D body spawns, or it repeats on reload |
| Right after a town fight | Walk away — **normal speed immediately** | **1127** | Hero at ~4% speed / controls frozen after the reward screen |

## 3. Build menu

| Where | Look for | WO | Fail tell |
|---|---|---|---|
| Build → **Build Town** | Headers **Producers / Storage / Trade / Civic** with dividers | **1167** | One flat run of 16 tiles, or a building vanished |
| Place something unaffordable | Refusal on its **own opaque plate**, clear of the red footprint | **1106** | Reason crammed into the ghost pill, red bleeding through the glyphs |
| Town palette → Crystal Mine, then Arcane Spire | `mine_crystal` **buildable**; Spire reads **200 crystals / 160 iron** | **PROD-015** | Crystal Mine absent, or Spire still **500**. ⛔ Not the Cathedral (240) |
| PLACE phase | Right-edge rail: check / rotate / X **sprites**, small round Done | **1010** | "Rot" as text, an Orient or Flag button, "FREE" on a card |

## 4. Manage / Queues

| Where | Look for | WO | Fail tell |
|---|---|---|---|
| Manage → Defense → a structure with an upgrade | Tap **Upgrade**, then **the same spot again** → **"Finish Now"** with a crystal cost | **1058** | Second tap cancels, or starts a different building |
| Armies / Loadouts | Controls in **horizontal rows** — 4 chips one row, Name/Save/Muster one strip | **1056** | Muster CTA covering "Train queue: 0 of 5 used" |
| ⭐ Build queue → **"Watch an ad to skip 10 min"** | A **real ad**, then exactly the skip minutes | **1120** | Skip granted with **no ad** (the old stub), or an ad paying crystals |
| The toast after the ad | Wording matches what happened — "Time skipped." / "Ad closed early…" / "used your ad skips" | **1125** | You sit through a full ad and get **"No ad available right now."** |

## 5. Realm Store / Night Market

| Where | Look for | WO | Fail tell |
|---|---|---|---|
| ⭐ The Night Market, landscape | **Read every price.** Folk's Thanks = **120 SKR** (not 20); Ingot Crate = **36 SKR** (not 6) | **UI-001** | Leading digit occluded, cards overlapping, grey slab across the bottom 35% |
| Default shelf | **≤15 SKUs**, every line a resource you can actually receive | **1118** | Cosmetic/vapour packs still listed |
| A pack card → confirm | Card shows **both** `≈ $2.99` **and** the exact SKR — and that SKR is **what the wallet asks you to sign** | **1158** | Card SKR ≠ wallet SKR |
| Count the wallet prompts | ⭐ Exactly **one** | **1157** | Two on the session's first purchase. ⚠ Older notes say "2-then-1" — **1 is the target** |
| Buy, mainnet | sign → settle → contents granted | **1159** | Paid-but-not-granted. ⛔ Must be a **real ladder SKU** — a canary proves nothing |
| Consumables → **2× Harvest (4h)** | Doubles the **rate**; buying again **extends** the timer | **1119** | Shows 4.0× on a second buy, or starts with a full bank |

## 6. Gear Shop (a *different* screen from the store)

| Where | Look for | WO | Fail tell |
|---|---|---|---|
| Vendor at the armorer/forge | Slim name column, large **3D render**, stat diff, Purchase/Sell + Equip | **501** | Fat crammed rows; **blank** preview (must fall back to a 2D glyph, never blank) |
| Select a weapon | Signed deltas, matchup %, `HOT-SWAP READY`; buying does **not** reassign hot-swap | **1068** | Unsigned deltas, or a purchase silently reassigning |
| Buy something | Card price == **gold actually removed** | **1064** | Displayed ≠ debited |

## 7. Daily chest / Season / Ledger

| Where | Look for | WO | Fail tell |
|---|---|---|---|
| Daily Chest | Bottom-centre **Close** reads as the whole word; ad button changes its **word** by state | **1051** | Close reads **"lose"** (leading C eaten). ⭐ Test **both `ff.blinkchrome` states** |
| Chest payout | **500 free / 1,000 after ad**, once per UTC day | **1064** | Re-arms the same day |
| Monthly Ledger | **Five week tabs**; Close **inside** the frame; selected tab by **underline + label**, not colour | **1150** | Close half off the top; thirty identical tiles |
| Season Track | Only purchasable thing is the **lane unlock** | **1053** | A "buy tiers / catch up" affordance, mystery boxes, a countdown |

## 8. Raid

| Where | Look for | WO | Fail tell |
|---|---|---|---|
| Deploy the catapult | **Horizontal, roughly troop-scale** — compare to a footman | **1143** | A huge upright tower-shaped object |
| Lose a siege, read the report | Names the **looted collector and amount**, and it matches what vanished | **1139** | Loss with no report line, or crystals reduced |

## 9. Dungeon + portal

| Where | Look for | WO | Fail tell |
|---|---|---|---|
| Circle a portal on foot | Reads as a lit doorway **from every heading** | **1062** | One heading collapses to black shards or a flat sticker; magenta at the arch |
| Approach both sides | Trees do not cut through the arch; camera never inside a trunk | **1156** | Foliage intersecting. ⚠ Still owes a device capture |
| A container after a room | A real **chest** with an Open prompt; drops have distinguishable shapes | **1132** | A grey cube you attack, or a dead tap while enemies live |
| ⭐ **Dungeon → town**, look at the shield | On the off-hand arm, pose **unchanged after the port** | **PROD-005** | Right until the port, then wrong and stays wrong |
| Any enemy with an affinity | Same weapon → **three different numbers** (×1.25 / ×1.0 / ×0.75) | **1065** | Any enemy takes 0, or all three identical |
| Long ranged session (20+ min) | VFX **keep appearing** after many ranged kills | **1155** | Trails quietly stop. ⚠ Wants an F8 dump, not a feel |

## 10. Settings — 2 minutes, high value

**Settings → Offline → Download**, then ⭐ **airplane mode**, then cold start.
**Look for:** a real MB figure running to 100%; then with the **radio off**, the game opens and buildings
and enemies render. **WO:** PROD-010.
⛔ **A "download complete" message alone proves nothing** — the 08-19 version reported success and
downloaded **zero bytes**. The radio must be off.

## 11. Away from the game

**Lock screen / notification shade.** Horde notification at the right lead (15/30/60 min), saying
**"Return to defend live"**; opening the app clears it. **WO:** 1184.
⚠ **Fail tell:** no notification, wrong lead, a stale alert, or ⛔ **any shield/IAP offer paired with it.**
⭐ **PARTIAL — judge the two halves separately:** the on-screen **LOOKOUT REPORT banner may not appear at
all** (it is hosted on a legacy `UIDocument`, which renders blank in player builds). **Do not fail the
ticket on the banner** — the notification half is the substance.

---

## ⚠ Needs state you may not have — these waste a session if hit cold

- **1142** — an Arcane Spire **built and saved**, then quit and relaunch
- **1139** — a **full collector breaking during a lost siege**
- **1058** — a structure eligible to upgrade **plus** crystals to finish it
- **903** — a resource you can move from **0 to cap**
- **1161** — a profile that **has not built the weapons roof yet**
- **1151, 1010** — a **fresh profile** (once-ever beats)
- **1157/1158/1159** — wallet connected, mainnet, real SKR, a **real ladder SKU**
- **1184** — a **level-3 lookout** for the force-size line (level 2 must send none)

## ⭐ Partially shipped — do not fail these for the missing half

- **1153** — `gate_stone` is **palette-locked**, so no player can place a gate. Nothing to feel.
- **1161** — the ruled scope shipped; §6's display duplication (`collector_forge`/`forge` both "Forge")
  did not. The original "Iron — NEEDS: Forge" symptom may still be felt.
- **1053** — cosmetic and SKR reward rows are **deliberately unauthored**. Not a bug.
- **PROD-003** — **no vendor NPC body** yet; the building has a proximity component only.
- **1127** — only the in-place arena path was exercised.

## Not felt-testable — close on evidence, not eyes

**1130** (r2 push in ship chain) · **1102** (per-instance logfiles) · **1145** (F8 ack highwater) ·
**1138** (hollow-pass ratchet) · **1054** (optional SFX false error) · **992** (dead code) ·
**1137** (fallback catalog palette) · **1063** (gear umbrella — contract only).
