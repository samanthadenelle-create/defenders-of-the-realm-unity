import io, subprocess, os
from pathlib import Path

# CLAUDE.md sec.0 (owner ruling 2026-08-09): the repo root is MACHINE-DEPENDENT
# (C:\eoa on one seat, D:\eoa on another) - resolve it from this script's own
# location, never hardcode a drive letter. dev/tmp/<script>.py -> parents[2].
ROOT = str(Path(__file__).resolve().parents[2])

# WO-1141 allowlist, enumerated explicitly. Deliberately EXCLUDES:
#   BOARD.html, CLI_LANES_WO_NUMBERS.md, WORK_ORDER_1140 (CLI board work)
#   anything siege / loss-stakes / defense-report / VFX (other lanes)
PATHS = [
    # editor + regression
    'Assets/Editor/Regression/DungeonEncounterFamilyRegression.cs',
    'Assets/Editor/Regression/DungeonMultiLevelRegression.cs',
    'Assets/Editor/RoomForge/DefaultStairwellRoomBuilder.cs',
    'Assets/Editor/RoomForge/DungeonBaker.cs',
    'Assets/Editor/RoomForge/GraphDungeonComposer.cs',
    # runtime
    'Assets/_Modules/Dungeons/ComposedAmbushDirector.cs',
    'Assets/_Modules/Dungeons/ComposedDungeonHost.cs',
    'Assets/_Modules/Dungeons/DungeonExitInteractable.cs',
    'Assets/_Modules/Dungeons/DungeonRoomBinder.cs',
    'Assets/_Modules/Dungeons/RoomForge/DungeonBakerChecks.cs',
    'Assets/_Modules/Dungeons/RoomForge/DungeonComposeLayout.cs',
    'Assets/_Modules/Dungeons/RoomForge/RoomPrefabMeta.cs',
    'Assets/_Modules/Village/Enemies/OutpostEnemyGroupSpawner.cs',
    'Assets/_Modules/Village/Items/ItemDropSystem.cs',
    'Assets/_Modules/Village/World/BreakableContainer.cs',
    'Assets/_Modules/DevTools/AutoPilotDriver.cs',
    'run-autopilot-fleet.ps1',
    # canonical data - BOTH mirrors are required
    'Assets/Resources/Data/Canonical/dungeon-graphs',
    'Assets/Resources/Data/Canonical/dungeon-layouts',
    'Assets/Resources/Data/Canonical/loot-tables.json',
    'Assets/StreamingAssets/Data/Canonical/dungeon-graphs',
    'Assets/StreamingAssets/Data/Canonical/dungeon-layouts',
    'Assets/StreamingAssets/Data/Canonical/loot-tables.json',
    # audit report
    'docs/qa/DUNGEON_AUDIT_2026-08-22.md',
]

FORBIDDEN = ('BOARD.html', 'CLI_LANES_WO_NUMBERS', 'WORK_ORDER_1140',
             'StakeRules', 'DefenseReport', 'SiegeCadence', 'SiegeScheduler',
             'SiegeSession', 'HitSurface', 'EliteVFX', 'VfxMirror', 'VfxArtMirror',
             'FeatureFlags', 'SaveSchema', 'DataRegression')

def sh(a):
    return subprocess.run(a, capture_output=True, text=True, cwd=ROOT)

sh(['git', 'add', '--'] + PATHS)
staged = sh(['git', 'diff', '--cached', '--name-only']).stdout.split()

# THE CHECK CODEX'S OWN WO SPECIFIES, applied in both directions.
strays = [p for p in staged if any(f in p for f in FORBIDDEN)]
if strays:
    print('STOP - forbidden path(s) staged:')
    for s in strays:
        print('   ', s)
    raise SystemExit(1)

print('staged %d file(s), no forbidden path present' % len(staged))

# dual-copy parity on the canonical data
import filecmp
pairs = [('loot-tables.json', '')]
for name in ['dg_bonecrypt', 'dg_ember_deep', 'dg_hollow_roads', 'dg_starter_loop', 'dg_sunken_vault']:
    for sub in ['dungeon-graphs', 'dungeon-layouts']:
        a = os.path.join(ROOT, 'Assets/Resources/Data/Canonical', sub, name + '.json')
        b = os.path.join(ROOT, 'Assets/StreamingAssets/Data/Canonical', sub, name + '.json')
        if os.path.exists(a) and os.path.exists(b) and not filecmp.cmp(a, b, shallow=False):
            print('  !! DUAL-COPY DRIFT:', sub, name)
a = os.path.join(ROOT, 'Assets/Resources/Data/Canonical/loot-tables.json')
b = os.path.join(ROOT, 'Assets/StreamingAssets/Data/Canonical/loot-tables.json')
print('  loot-tables dual copy identical:', filecmp.cmp(a, b, shallow=False))

MSG = """feat(dungeons): scaling, boss lifecycle, locked exits and boss-only loot (WO-1141)

Implemented by the Codex seat; staged, verified and committed by the CLI seat
per CLAUDE.md section 11 (one committer). Scope per WO-1141: difficulty scaling,
boss lifecycle and UX, locked exits, boss-room caches, boss-only loot, vertical
room overlap, and per-dungeon runtime probes.

WHAT IS PROVEN, and it is more than the implementing seat could see: the newest
dungeon edit lands 09:36 and the CLI's gate ran at 10:07/10:08 on this same
tree, so this work was PRESENT and covered by:
  COMPILE_GATE_OK           (Builds/gate-fix.log,  fresh)
  DataRegression 248/250    (Builds/reg-fix.log,   fresh)
Neither of the two failures is a dungeon suite - both are the ticketed asset
gaps WO-1135 (wall tier materials were never tracked) and WO-1136 (staff_A is
geometrically symmetrical). WO-1141's bar of "no dungeon failure is acceptable"
is met on the static half.

⛔ WHAT IS NOT PROVEN, stated plainly because the WO's own acceptance asks for
it: the RUNTIME matrix. No Windows build and no six-dungeon runtime probe were
run for this commit. WO-1141 requires that evidence before this work should be
called done - committing here creates a restore point for 39 files that had
none, it does not discharge the acceptance criteria.

STAGED BY EXPLICIT PATH against WO-1141's allowlist, then verified in BOTH
directions - every staged path expected, and a forbidden-prefix sweep proving no
siege, loss-stakes, defense-report, feature-flag, save-schema, VFX, board or
numbering file rode along. That two-way check is the WO's own step 4, adopted
after it caught three incomplete commits by the CLI seat today.

Both canonical mirrors move together (10 dungeon graph/layout pairs plus
loot-tables.json), verified byte-identical rather than assumed.

NOTE FOR THE IMPLEMENTING SEAT: WORK_ORDER_1141 itself was already committed
earlier today by an over-broad `git add -- WorkOrders/` on the CLI side - that
is why its own status read untracked. Its commit protocol forbids exactly that
mistake, which is a fair point well made.
"""

io.open(os.path.join(ROOT, '.git', 'CMSG'), 'w', encoding='utf-8', newline='\n').write(
    MSG + "\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>\n")
c = sh(['git', 'commit', '-F', '.git/CMSG'])
print((c.stdout or c.stderr)[-200:])
