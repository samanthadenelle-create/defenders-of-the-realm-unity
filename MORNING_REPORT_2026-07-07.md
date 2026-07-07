# MORNING REPORT — 2026-07-07 overnight session

**Build:** `Builds/Windows/DefendersOfTheRealm.exe` — fresh, COMPILE_GATE_OK, fleet 3/3 runs with
ZERO regressions (only the 3 pre-existing knowns + one truncation-class label). Local commits
`75bffabd` → `88d6fbc9` → `3b4cfeac`. **NOT pushed — your felt-pass gates the push.**

## 1. THE WEAPON RCA (your #1 ask) — data-proven, band-aids named
Full docs: `docs/RCA_WEAPON_OFFSETS_2026-07-07.md` + `docs/WEAPON_TRANSFORM_CENSUS_2026-07-07.md`.
- **Your offsets DID load and apply** — Player.log shows the NUDGE/DELTA lines with your exact values.
- **Proven cause 1 (what you saw in town):** out of combat the gear renders the SHEATHE pose — a
  second orientation system with hard-coded eulers that never consulted your offsets. The Seating
  Editor tunes drawn-only by design (it literally force-draws the weapon while you edit).
- **Proven cause 2 (drawn):** the editor preview skipped parent-scale compensation (hand bone lossy
  1.666, captured) — your approved 0.46 booted at 0.276 world, 40% smaller, every boot.
- **Fixed:** one shared compensation source (preview == boot, WYSIWYG restored); your sword scale
  converted 0.46→0.766 in all three offset files so the look you APPROVED is what boots; sheathed
  poses now consume `<mesh>@sheathed` registry entries; **the Seating Editor has a Drawn/Sheathed
  toggle** — dial the back pose in-game exactly like the grip, it persists.
- **Band-aid remove-list (7 items, census-cited)** awaits your decisions — table in the RCA doc
  (global +180 yaw baked over hand-dialed values, five stacked grip rotations, dead code, 3 offset
  files, inconsistent scale compensation...).

## 2. EVERY-SCREEN PASS — 44 captures reviewed, 2 rounds of fixes
**Kit-wide (heals ~14 panels each):** gold-text-on-gold-plate → one luminance rule (INK on gold
plates; you're colorblind — this was unreadable everywhere); the Close-band reservation used the
portrait height constant on landscape canvases (under-reserved 78%) — root of every
button-under-Close collision, fixed at the factory.
**Your five F8 tickets:** vitals bars contained (0.94 was proven insufficient in-build; now 0.88 +
end-cap inset), Wisdom chip reads "SKILL 434", duplicate XP bar removed, resource rows labeled
Gold/Wood/Iron/Food/Crystal + collapsed dock says "Resources", palette = centered content-sized
dock, death panel flows icon/text/button (multi-line aware).
**Combat HUD:** Q/W/E/R medallions now arc tightly around the attack pill (old math was fractions
of the zone rect — reproduced the captured scatter arithmetically); enemy portrait/name/Lv contained
with measured ellipsize; energy-sword pill fitted (verified in capture).
**Panels:** Dialogue rebuilt in-frame with a real Continue button (was a raw black box);
BugReport Send clear of Close; RaidDeploy one clean action row; Victory rewards no longer under
Continue (icons 40px); Settings title in-panel + labeled On/Off toggles.

## 3. DEFERRED (task board #7 — deliberate, not forgotten)
Truncation epidemic (tabs/nodes/buttons on 15+ panels — needs the bounded auto-fit in the kit label
builders with a capture loop, too risky blind); dead top-right notch (source still unproven — no
theory-fix); capture-rig camera never moves (9 identical world shots); minor panel list (Crafting
dead band, RealmStore prices, hotbar keybind labels, 'Troll vs Orcish Raider' banner mismatch...).
Plus pre-existing: white Paladin albedo (fix named in its own error line), WO-602 home return.

## 4. YOUR MORNING PATH
1. Launch the exe → open Seating Editor → **Sheathed** tab → dial the back pose for sword + shield
   (60 seconds, saves like the grip) → F8 anything off.
2. Felt-pass the screens (dialogue, shop, victory, settings, combat HUD).
3. Say the word → I push everything → you record → web-URL submission starts the review clock.
