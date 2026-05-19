# User Acceptance Test (UAT) Script — Defenders of the Realm (v2 Unity Foundation)

**Project:** Defenders of the Realm — Unity 6 LTS / URP port
**Owner:** Samantha Denelle / DeNelle Studios
**Derived from:** `docs/v2-unity-port-spec.md` Part 9 (Acceptance gates) and Part 5 Week 8
**Audience:** the PM / tester executing the Week-8 acceptance review — **no engineering knowledge required**.

---

## What this is

This is the step-by-step playthrough a tester follows **once the Week-8 build exists**. It is the human-run companion to `docs/qa/qa-test-plan.md`. When every check below passes, the v2 Unity foundation is "viable" per the spec and the owner can decide: continue to v2.1, pause for the v1 launch, or hand off to a Unity contractor.

**Do not run this before the Week-8 build is delivered.** Weeks 1–4 are committed and compiling; Weeks 5–7 are written but mid-integration. There is no end-to-end playable build yet — that *is* the Week-8 deliverable. If asked to run UAT early, stop and report "no playable build yet".

## Before you start — what you need

1. The Week-8 build artifact:
   - **Android `.apk`** installed on a Seeker phone **or** a Seeker emulator (preferred — this is the target hardware), OR
   - the **Windows `.exe`** for a desktop dry-run if no Seeker is available.
2. The Unity Profiler connected to the running build (an engineer can set this up in 5 minutes) — needed for the FPS and memory checks. If you cannot connect the Profiler, note it and have an engineer capture those two numbers.
3. A test wallet on **devnet**: Phantom on desktop, or a Seeker with its wallet on devnet. It needs a small amount of devnet SOL (free from a faucet — ask an engineer).
4. This document, printed or on a second screen, plus `docs/qa/bug-log.md` open to log anything that fails.
5. A stopwatch (your phone is fine) for the 5-minute timing.

## How to record results

- Each numbered step has a **PASS / FAIL** box. Tick one.
- A **FAIL** is not a stop sign — finish the rest of the script if you can, then log every FAIL as a new row in `docs/qa/bug-log.md` (the log explains how).
- At the end, fill in the **Sign-off** table. If any **Gate** is FAIL, the overall result is **NOT ACCEPTED** and the owner decides next steps.
- Quote what you see on screen verbatim when something looks wrong — exact wording matters for canon checks.

---

## Part A — The 5-minute clean playthrough (village path)

> Spec gate: a 5-minute playthrough on Seeker covering bumper → title → start → village → place a tower → trigger Wave 1 → fight → win or breach → ATB → return → exit to title, with **no crashes and no softlocks**.

**Start the stopwatch when you tap the app icon.**

| # | Step | What you should see | PASS | FAIL |
|---|------|---------------------|------|------|
| A1 | Launch the app fresh (first run, no save). | The DeNelle Studios studio bumper plays for ~3 seconds, then fades out. | ☐ | ☐ |
| A2 | Wait. | The Title screen appears: the Heart-Wing banner artwork, and the tagline text **"By lantern. By oath. By Heart."** | ☐ | ☐ |
| A3 | On first launch only, watch for the story intro. | A short 3-line cold-open story sequence plays (~5 seconds). | ☐ | ☐ |
| A4 | Read the Title screen carefully. | A **Connect Wallet** button and a **Start** button are both visible and tappable. | ☐ | ☐ |
| A5 | Tap **Start**. | The screen fades; the Village scene ("Avalon") loads. Music crossfades — it does not cut abruptly. | ☐ | ☐ |
| A6 | Look at the village. | You see a walled village with a glowing world-tree at the centre (this is **Elarion**, the Heart). The tree glows violet — it is **not** a flat white shape. | ☐ | ☐ |
| A7 | Move the hero around (drag/tap to move, or WASD on desktop). | The hero (the mage, **Blaise**) walks smoothly. The camera follows. The hero does not slide oddly or freeze. | ☐ | ☐ |
| A8 | Open the build menu and place one tower / building on a highlighted buildable tile. | The building appears where you placed it. Your crystal resource count drops by the cost shown. | ☐ | ☐ |
| A9 | Find the wave countdown on the HUD and let it run down (or trigger Wave 1 if a dev button exists). | A countdown reaches zero, then enemies (Hollow Walkers — skeletons) appear at the north gate and march toward the Heart. | ☐ | ☐ |
| A10 | Fight the wave: use the hero's abilities (the Q/W/E/R hotbar) and let the pets and tower attack. | Abilities fire. Enemies take damage and die. The three starter pets (Aether Sprite, Flame Pup, Ice Wolf) attack on their own. | ☐ | ☐ |
| A11 | Play out the wave to its end. | Either you defeat every enemy (**win**), OR an enemy crosses the inner wall and the screen cuts to a battle scene (**breach**). Either outcome is acceptable. | ☐ | ☐ |
| A12 | **If a breach happened:** play the ATB battle. Tap **Attack** / use abilities each turn. | A turn-based battle plays out. The action log scrolls. The battle ends in a win or a loss. | ☐ | ☐ |
| A13 | After the battle (or after winning the wave), confirm where you land. | Control returns to the Village scene. Any damage from the battle is reflected (e.g. Heart HP). The game is responsive — no frozen screen. | ☐ | ☐ |
| A14 | Exit back to the Title screen (pause menu / exit). | The Title screen reappears. Music crossfades back. | ☐ | ☐ |
| A15 | **Stop the stopwatch.** Record the elapsed time. | The full run above completed. Elapsed time: ____ min ____ sec. | ☐ | ☐ |
| A16 | Think back over the whole run. | There were **no crashes** (the app never closed itself) and **no softlocks** (you were never stuck with no way to continue). | ☐ | ☐ |

**Part A timing note:** the run should be playable within roughly 5 minutes. If it took much longer because of slow scenes or confusing flow, that is not an automatic FAIL but note it for the owner.

---

## Part B — The dungeon playthrough (alternate path)

> Spec gate: village → walk to dungeon portal → Healer's Cottage → meet Bryn → walk the rooms → read a lore-stone → one scripted ATB encounter → win → return. **No walk-through-walls bugs.** Lantern light works.

Start a fresh run (or continue from Part A) and get into the Village.

| # | Step | What you should see | PASS | FAIL |
|---|------|---------------------|------|------|
| B1 | In the village, find and enter the dungeon portal / entrance to the Healer's Cottage. | The screen fades; the Healer's Cottage dungeon scene loads. Music crossfades to a quieter ambient track. | ☐ | ☐ |
| B2 | Look at the hero and the lighting. | The hero is lit by a lantern — a warm pool of light that follows the hero around the dark dungeon. | ☐ | ☐ |
| B3 | At the entrance, find the NPC named **Bryn** (the Wanderer). Walk up to Bryn. | A speech bubble appears above Bryn with a line of dialogue. The wording is story prose, not placeholder text like "TODO" or "lorem ipsum". | ☐ | ☐ |
| B4 | Walk the hero through the cottage rooms and corridors. | The hero moves smoothly room to room. The camera follows from a tilted top-down angle. | ☐ | ☐ |
| B5 | **Walk the hero straight into several walls, on purpose.** | The hero stops at the wall or slides along it. The hero **never passes through** a solid wall. | ☐ | ☐ |
| B6 | Find a glowing lore-stone and tap it to read. | A panel opens with a passage of story/journal text. Close it and continue. | ☐ | ☐ |
| B7 | Watch the lantern light as you explore. | The lantern light dims as oil runs low; topping up at an oil stone brightens it again. | ☐ | ☐ |
| B8 | Find a checkpoint shrine and walk into it. | The hero heals to full and a save/checkpoint confirmation appears (a toast or message). | ☐ | ☐ |
| B9 | Walk into a scripted encounter zone (or reach the room where a fight triggers). | The screen cuts to an ATB battle. | ☐ | ☐ |
| B10 | Play and win the ATB battle. | The battle resolves in your favour. | ☐ | ☐ |
| B11 | Confirm where you land after the battle. | Control returns to the **Healer's Cottage dungeon** (NOT the village) — the hero resumes near where the encounter started, with HP/mana carried over. | ☐ | ☐ |
| B12 | Continue to the end-room, then return to the village. | The dungeon completes; control returns to the Village. | ☐ | ☐ |

> **Known risk for B11:** the build may currently return you to the *Village* instead of the dungeon after a dungeon battle (a missing routing branch — see bug-log BUG-008). If that happens, mark B11 FAIL and reference BUG-008.

---

## Part C — Performance gates (Profiler)

> Spec gates: 60 FPS held during village wave + dungeon walking; frame-time spikes ≤ 33 ms; memory ≤ 400 MB. Quality level **Seeker_High**.

With the Unity Profiler connected to the running build:

| # | Step | What you should see | PASS | FAIL |
|---|------|---------------------|------|------|
| C1 | Confirm the build is running at the **Seeker_High** quality level (Settings menu, or ask an engineer). | Quality = Seeker_High. | ☐ | ☐ |
| C2 | Replay the village Wave 1 with the Profiler recording. Read the frame graph. | Frame rate holds at **60 FPS**. No frame-time spike exceeds **33 ms**. | ☐ | ☐ |
| C3 | Walk the dungeon for ~30 seconds with the Profiler recording. | Frame rate holds at **60 FPS**. No frame-time spike exceeds **33 ms**. | ☐ | ☐ |
| C4 | Watch the Profiler memory track across the whole playthrough. | Total memory stays at or below **400 MB**. Record the peak: ____ MB. | ☐ | ☐ |

If you cannot connect the Profiler, have an engineer capture the FPS frame graph and the peak memory number, and paste them here.

---

## Part D — Save persistence

> Spec gate: quit mid-playthrough, relaunch, the save resumes with the same hero HP, pet bond, resources, and wave number.

| # | Step | What you should see | PASS | FAIL |
|---|------|---------------------|------|------|
| D1 | Start a run, play into the village, and note these four numbers: hero HP ____, a pet's bond rank ____, crystal count ____, current wave number ____. | All four values noted. | ☐ | ☐ |
| D2 | Fully quit the app (close it, do not just minimise). | The app closes. | ☐ | ☐ |
| D3 | Relaunch the app and continue the saved game. | The game resumes. | ☐ | ☐ |
| D4 | Check the four numbers from D1 again. | Hero HP, pet bond rank, crystal count, and wave number all match what you recorded in D1. | ☐ | ☐ |

---

## Part E — Wallet & store (devnet)

> Spec gate: wallet connects on devnet via the Solana SDK; a mock pack purchase completes — a devnet transaction goes through and the pack contents land in the game.

| # | Step | What you should see | PASS | FAIL |
|---|------|---------------------|------|------|
| E1 | From the Title screen, tap **Connect Wallet**. Approve the connection in Phantom / the Seeker wallet (on **devnet**). | The wallet connects. The game shows your wallet is connected (and may show a balance). | ☐ | ☐ |
| E2 | Open the pack store. | Five packs render: **Hearth Spark, Lanternlight, Folk's Thanks, Patron of Elarion, Founder's Vow**, each with a price. | ☐ | ☐ |
| E3 | Buy the cheapest pack (**Hearth Spark**) using the **SOL** payment option. Approve the transaction in your wallet. | The transaction is sent on devnet and confirms (a moment's wait). The store shows success. | ☐ | ☐ |
| E4 | Check your game state after the purchase. | The pack's contents have been added to your game (resources / items as listed on the pack). | ☐ | ☐ |
| E5 | Find the transparency display (Title screen or Settings) showing the Rewards Distributor address. | A Solana wallet address is shown for transparency. | ☐ | ☐ |
| E6 | Scan the store for anything that looks like a loot box, gacha, random-reward pull, energy timer, or a stat-boost-for-money item. | None of these exist — packs are convenience/cosmetic only. | ☐ | ☐ |

> **Known risk for E3:** the **SKR** payment rail will fail until the devnet SKR mint address is configured (see bug-log BUG-011). Use the **SOL** rail for E3 — it is the working path. If you must test SKR and it fails cleanly with an error message (no crash, no wrong-address send), that is acceptable behaviour, not a FAIL — but note it.

---

## Part F — Canon-name check (on screen)

> Spec gate: every canon name appears correctly on screen. The exact spelling and wording are non-negotiable.

Go back through the screens you visited and confirm each canon term appears **exactly** as written below. A misspelling or paraphrase is a FAIL.

| # | Canon term — must appear exactly | Where to look | PASS | FAIL |
|---|----------------------------------|---------------|------|------|
| F1 | **DeNelle Studios** | Studio bumper | ☐ | ☐ |
| F2 | **By lantern. By oath. By Heart.** | Title screen tagline | ☐ | ☐ |
| F3 | **Avalon** | Village (town name in UI / loading) | ☐ | ☐ |
| F4 | **Elarion** (also "the Heart") | Village (the world-tree, HUD, or tooltips) | ☐ | ☐ |
| F5 | **Blaise** | Hero name (HUD portrait / character) | ☐ | ☐ |
| F6 | **Alduin the Mournful** | Story intro / lore text (if shown) | ☐ | ☐ |
| F7 | **the Heart-Wing** | Title banner / branding | ☐ | ☐ |

> Note: if **Bryn** appears in dungeon dialogue (Part B), confirm the spelling is "Bryn". Bryn and a few other names (Mara, Tovin, Eira, Aelf, Mira) are flagged as not-yet-canon-sourced (bug-log BUG-012) — if any of those names appear on screen, note exactly which and where.

---

## Sign-off

Tester: ________________________   Date: ____________   Build: ____________
Platform tested: ☐ Seeker device  ☐ Seeker emulator  ☐ Windows EXE

| Gate (spec Part 9) | Covered by | Result |
|--------------------|-----------|--------|
| 1. End-to-end playable (village path), no crashes / softlocks | Part A | ☐ PASS ☐ FAIL |
| 2. Dungeon playable end-to-end, no walk-through walls, lantern works | Part B | ☐ PASS ☐ FAIL |
| 3. 60 FPS held, frame spikes ≤ 33 ms | Part C (C2, C3) | ☐ PASS ☐ FAIL |
| 3b. Memory ≤ 400 MB | Part C (C4) | ☐ PASS ☐ FAIL |
| 4. Wallet connect + pack purchase on devnet | Part E | ☐ PASS ☐ FAIL |
| 5. Audio plays, music crossfades at scene changes | Part A (A5, A14), Part B (B1) | ☐ PASS ☐ FAIL |
| 6. Save state persists across restart | Part D | ☐ PASS ☐ FAIL |
| 7. Canon names correct on screen | Part F | ☐ PASS ☐ FAIL |

**Overall result:** ☐ ACCEPTED   ☐ NOT ACCEPTED (one or more gates FAIL — owner decides: extend, descope, or pause)

Number of FAIL steps logged as bugs in `docs/qa/bug-log.md`: ______

Notes for the owner:
_______________________________________________________________________
_______________________________________________________________________

_Tend the Heart. Hold the dark._
