import io, subprocess, os
ROOT = r'D:\eoa'

def sh(a):
    return subprocess.run(a, capture_output=True, text=True, cwd=ROOT)

def commit(paths, msg, forbid=()):
    real = []
    for p in paths:
        real.append(p)
        m = p + '.meta'
        if os.path.exists(os.path.join(ROOT, m)):
            real.append(m)
    sh(['git', 'add', '--'] + real)
    staged = sh(['git', 'diff', '--cached', '--name-only']).stdout.split()
    if not staged:
        print('  (nothing staged) SKIP'); return
    strays = [p for p in staged if any(f in p for f in forbid)]
    if strays:
        print('  STOP - forbidden staged:', strays); raise SystemExit(1)
    io.open(os.path.join(ROOT, '.git', 'CMSG'), 'w', encoding='utf-8', newline='\n').write(
        msg + "\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>\n")
    c = sh(['git', 'commit', '-F', '.git/CMSG'])
    print('  committed %d file(s) %s' % (len(staged), '' if c.returncode == 0 else 'ERR ' + (c.stderr or c.stdout)[:200]))


CODEX_MSG = """feat(dungeons+world): the Codex six - kit, exit beacons, wall tiers, arena music, troop visuals

Implemented by the Codex seat in a separate worktree (D:\\eoa-codex-six, branch
codex/wo-six, nothing committed there); staged, re-gated and committed by the
CLI seat per CLAUDE.md section 11.

  WO-595   the 24-piece KayKit dungeon kit - catalog, builder, themes,
           colliders, moving platform, seeded composer
  WO-1135  tracked URP wall-tier materials + FBX importer remaps; three raid
           scenes rebuilt and all four raid NavMeshes rebaked
  WO-517   arena return music now derives Village/Overworld from the hero's
           ACTUAL return context instead of guessing
  WO-1143  the catapult
  WO-957   one true exit per dungeon, verified
  WO-1047  verified already fixed by WO-1132

WO-1143 - THE CATAPULT WAS NEVER A ROTATION BUG. The owner reported it
"oversized and standing vertical instead of horizontal". Codex reproduced it and
proved the thing on screen was the OVERSIZED FALLBACK CAPSULE, not the catapult
model at all - the addressable had not settled when the troop was skinned, and a
troop is skinned exactly once. The fix is generic post-Addressables-settle
recovery for troop models (not a catapult special case) plus a readable
horizontal siege-machine fallback, so even the un-resolved path reads as a
siege engine rather than a broken pillar. Evidence in docs/ui-evidence/
wo1143_catapult_before.png / _after.png: after, it is a low wheeled machine
beside a footman at correct relative scale.

WO-1135 - the colourblind gate is MET, not asserted. wo1135_wall_tiers_
grayscale.png shows three distinct SILHOUETTES with colour stripped: a jagged
low palisade, a riveted lattice, a solid runed steel wall. That was the
acceptance criterion and it is evidenced, not claimed.

RE-GATED IN THE PRIMARY TREE, and the number moved: Codex measured 244/253 in
its worktree and reported nine reds as pre-existing art/font/gear debt. In this
tree - which HAS the gitignored art packs - the same code measures 253/254 with
ONE red. Its nine were worktree artifacts, exactly as it suspected. The
surviving red is staff_A (WO-1136), geometrically undecidable and unfixable in
code. raid-wall-material, red since it was authored yesterday, is now GREEN.

Excluded per Codex's own instruction: Assets/EnemyContent materials/.fbm
(Unity-generated on clean import) and eoa-codex-six.slnx (worktree-local).

COMPILE_GATE_OK + DataRegression 253/254, both on fresh logs."""

HUD_MSG = """fix(hud): measured label fit, and five SFX errors that were never errors

WO-1144 - FOUR DEFECTS, FOUR DIFFERENT CAUSES, all from one captured frame
(autopilot break_24_error.png, identical in 8/8 runs). The screenshot was the
data; every one of these looks fine in the layout code.

  "Tap to collec"   the string is WIDER THAN ANY LEGIBLE FONT can render it.
                    The chip is 220 ref px by law (it shares a right edge with
                    two sibling rail chips), label rect ~202 px, and
                    "Tap to collect" measures ~214 px at the 30 px floor. There
                    is no size at which it fits, so Truncate cut it. Now
                    "Collect", authored in canon-strings.
  "Manag..."        a SENTENCE in a word-sized box - ManageFaceLabel is the
                    WO-1027 session-shape line ("Manage - 2 of 3 idle") in a
                    ~144 px face. Split into label + badge; the Core label is
                    deliberately UNCHANGED so SessionShapeRegression's pins hold.
  "TIER UP!"        a world-space TextMesh spawned at the hero, so its screen
                    size is a function of camera distance. Exactly the class
                    CombatTextLayer's own header names; the parry/riposte sites
                    were migrated and this one was missed. Now CombatText.Show.
  wave / Start Now  hud-areas.json mounts BOTH compass and waveBlock in the
                    calm(town) status area, and the compass strip owns the same
                    band by construction. The wave block now hangs a fixed-px
                    band off the mount's bottom edge.

LATENT DEFECT FOUND WHILE MEASURING: the old Start Wave button resolved to
~46 ref px tall - SIXTY-SIX PIXELS UNDER MinTouchPx - and nothing caught it,
because ClampMinTouch no-ops pre-layout while rect.height is still 0. A touch
floor that cannot see the control it guards is not a floor.

The oracle MEASURES: every authored line is run through a new
MeasureLineWidthPx that sums the real font's glyph advances, at two landscape
aspects, against boxes pinned by source lint. Add a word to a canon string and
the number moves and the suite fails. CollectorTellRegression was also
re-pointed - it grepped for literal copy INSIDE the formatter, so it would have
failed on a correct fix.

WO-1054 - FIVE SFX "errors" THAT WERE NEVER ERRORS. ProceduralSfx reads
LoadClip("Sfx/" + name) ?? Generate(id), and the line above it says
"Missing -> fall through to synth". Nothing is silent. But LoadClip's optional
parameter - whose own doc names "a synth-fallback SFX key" as the canonical
case - was never passed, so the loader took its safe-loud default and logged
FlowTrace.Fail claiming the clip was "REQUIRED by its caller". That sentence was
false. Five call sites now declare themselves optional; the ?? fallback is
untouched.

Why this was worth fixing immediately rather than ticketing: a FALSE error is
worse than no log. It trips F8, lands in the owner's inbox, and repeated often
enough it trains every seat to skim past errors - the exact instinct sections 12
and 14 exist to build in the opposite direction. It fired again mid-session and
interrupted a real investigation to report a non-bug.

⛔ ALSO RESTORED HERE: HudLabelFitRegression's registration in
DataRegression.RunAll. The CLI seat copied DataRegression.cs WHOLESALE from the
Codex worktree (based on an older commit) and clobbered it. The registration
lived only in the working tree, so git had no record to conflict on and the loss
was silent - caught only because RegressionMarkerRegression noticed an oracle
that exposes Run(out string) and is referenced by nobody. COPY HUNKS FROM
ANOTHER LANE, NEVER A WHOLE SHARED FILE."""

print('LANE: the Codex six')
commit([
    'Assets/Resources/Data/dungeon-kit.json',
    'Assets/Resources/Walls/iron_wall.fbx.meta',
    'Assets/Resources/Walls/steel_wall.fbx.meta',
    'Assets/Resources/Walls/wood_wall.fbx.meta',
    'Assets/Resources/Walls/Materials',
    'Assets/Resources/Walls/Materials.meta',
    'Assets/Scenes/RaidBase_fortified_garrison.unity',
    'Assets/Scenes/RaidBase_fortified_garrison/NavMesh.asset',
    'Assets/Scenes/RaidBase_mage_enclave.unity',
    'Assets/Scenes/RaidBase_mage_enclave/NavMesh.asset',
    'Assets/Scenes/RaidBase_raider_camp_small.unity',
    'Assets/Scenes/RaidBase_raider_camp_small/NavMesh.asset',
    'Assets/_Modules/Village/Arena/BattleArena.cs',
    'Assets/_Modules/Village/Troops/TroopFactory.cs',
    'Assets/_Modules/Village/World/WorldMusicDirector.cs',
    'Assets/_Modules/Dungeons/RoomForge/DungeonKitMovingPlatform.cs',
    'Assets/Editor/Regression/AddressableTroopVisualRegression.cs',
    'Assets/Editor/Regression/ArenaReturnMusicRegression.cs',
    'Assets/Editor/Regression/DungeonKitRegression.cs',
    'Assets/Editor/RoomForge/DungeonKitBuilder.cs',
    'Assets/Editor/WallTools/WallTierProofCapture.cs',
    'Assets/Editor/TroopTools',
    'Assets/Editor/TroopTools.meta',
    'docs/ui-evidence/wo1135_wall_tiers_color.png',
    'docs/ui-evidence/wo1135_wall_tiers_grayscale.png',
    'docs/ui-evidence/wo1143_catapult_before.png',
    'docs/ui-evidence/wo1143_catapult_after.png',
], CODEX_MSG, forbid=('EnemyContent', '.slnx', 'BOARD.html', 'CLI_LANES'))

print('LANE: HUD label fit + SFX false errors')
commit([
    'Assets/_Modules/HUD/Kit/HudKitController.cs',
    'Assets/_Modules/Core/HudModel/HudActionBarModel.cs',
    'Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs',
    'Assets/_Modules/Core/UI/HudStrings.cs',
    'Assets/_Modules/Village/Progression/TierSystem.cs',
    'Assets/_Modules/Audio/ProceduralSfx.cs',
    'Assets/_Modules/Audio/AudioService.cs',
    'Assets/_Modules/Village/Enemies/EnemyCombatAudio.cs',
    'Assets/Editor/Regression/HudLabelFitRegression.cs',
    'Assets/Editor/Regression/CollectorTellRegression.cs',
    'Assets/Editor/Regression/DataRegression.cs',
    'Assets/Resources/Data/Canonical/canon-strings.json',
    'Assets/StreamingAssets/Data/Canonical/canon-strings.json',
], HUD_MSG, forbid=('EnemyContent', '.slnx', 'BOARD.html', 'CLI_LANES'))
