import os, shutil, subprocess
from pathlib import Path

# CLAUDE.md sec.0 (owner ruling 2026-08-09): the repo root is MACHINE-DEPENDENT
# (C:\eoa on one seat, D:\eoa on another) - resolve it from this script's own
# location, never hardcode a drive letter. dev/tmp/<script>.py -> parents[2].
# SRC is the Codex linked worktree, a SIBLING of the repo root by convention.
DST = str(Path(__file__).resolve().parents[2])
SRC = str(Path(DST).parent / (Path(DST).name + '-codex-six'))

# Explicit allowlist. Codex's own exclusions honoured:
#   - Assets/EnemyContent materials/.fbm  (Unity-generated during clean import)
#   - eoa-codex-six.slnx                  (worktree-local solution file)
# Nothing is copied that is not named here.
TRACKED = [
    'Assets/Editor/Regression/DataRegression.cs',
    'Assets/Resources/Data/dungeon-kit.json',
    'Assets/Resources/Walls/iron_wall.fbx.meta',
    'Assets/Resources/Walls/steel_wall.fbx.meta',
    'Assets/Resources/Walls/wood_wall.fbx.meta',
    'Assets/Scenes/RaidBase_fortified_garrison.unity',
    'Assets/Scenes/RaidBase_fortified_garrison/NavMesh.asset',
    'Assets/Scenes/RaidBase_mage_enclave.unity',
    'Assets/Scenes/RaidBase_mage_enclave/NavMesh.asset',
    'Assets/Scenes/RaidBase_raider_camp_small.unity',
    'Assets/Scenes/RaidBase_raider_camp_small/NavMesh.asset',
    'Assets/_Modules/Village/Arena/BattleArena.cs',
    'Assets/_Modules/Village/Troops/TroopFactory.cs',
    'Assets/_Modules/Village/World/WorldMusicDirector.cs',
    'WorkOrders/WORK_ORDER_1047_dungeon_prop_targetable_and_orange_cube.md',
    'WorkOrders/WORK_ORDER_1135_wall_tier_materials_untracked.md',
    'WorkOrders/WORK_ORDER_517_arena_return_music_context.md',
    'WorkOrders/WORK_ORDER_595_kaykit_dungeon_kit.md',
    'WorkOrders/WORK_ORDER_957_exit_beacon_on_every_stairwell.md',
]

NEW_FILES = [
    'Assets/Editor/Regression/AddressableTroopVisualRegression.cs',
    'Assets/Editor/Regression/ArenaReturnMusicRegression.cs',
    'Assets/Editor/Regression/DungeonKitRegression.cs',
    'Assets/Editor/RoomForge/DungeonKitBuilder.cs',
    'Assets/Editor/WallTools/WallTierProofCapture.cs',
    'Assets/_Modules/Dungeons/RoomForge/DungeonKitMovingPlatform.cs',
    'docs/ui-evidence/wo1135_wall_tiers_color.png',
    'docs/ui-evidence/wo1135_wall_tiers_grayscale.png',
    'docs/ui-evidence/wo1143_catapult_before.png',
    'docs/ui-evidence/wo1143_catapult_after.png',
]

NEW_DIRS = [
    'Assets/Editor/TroopTools',
    'Assets/Resources/Walls/Materials',
]


def copy_one(rel):
    s = os.path.join(SRC, rel.replace('/', os.sep))
    d = os.path.join(DST, rel.replace('/', os.sep))
    if not os.path.exists(s):
        print('  MISSING in source:', rel)
        return 0
    os.makedirs(os.path.dirname(d), exist_ok=True)
    shutil.copy2(s, d)
    for m in (rel + '.meta',):
        sm = os.path.join(SRC, m.replace('/', os.sep))
        if os.path.exists(sm):
            shutil.copy2(sm, os.path.join(DST, m.replace('/', os.sep)))
    return 1


n = 0
print('--- tracked edits ---')
for r in TRACKED:
    n += copy_one(r)
print('--- new files ---')
for r in NEW_FILES:
    n += copy_one(r)
print('--- new directories ---')
for r in NEW_DIRS:
    s = os.path.join(SRC, r.replace('/', os.sep))
    d = os.path.join(DST, r.replace('/', os.sep))
    if not os.path.isdir(s):
        print('  MISSING dir:', r)
        continue
    shutil.copytree(s, d, dirs_exist_ok=True)
    cnt = sum(len(f) for _, _, f in os.walk(d))
    print('  copied dir %s (%d files)' % (r, cnt))
    n += cnt
    sm = s + '.meta'
    if os.path.exists(sm):
        shutil.copy2(sm, d + '.meta')

print('\ncopied %d file(s)' % n)

# brace/NUL check every .cs that arrived
bad = []
for root, _, files in os.walk(os.path.join(DST, 'Assets')):
    for f in files:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(root, f)
        rel = os.path.relpath(p, DST).replace(os.sep, '/')
        if rel in TRACKED or rel in NEW_FILES or any(rel.startswith(d) for d in NEW_DIRS):
            c = open(p, encoding='utf-8', errors='replace').read()
            if c.count('{') != c.count('}') or '\x00' in c:
                bad.append(rel)
print('brace/NUL check:', 'ALL CLEAN' if not bad else bad)
