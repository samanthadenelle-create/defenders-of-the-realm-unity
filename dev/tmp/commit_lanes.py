import io, subprocess, os
from pathlib import Path

# CLAUDE.md sec.0 (owner ruling 2026-08-09): the repo root is MACHINE-DEPENDENT
# (C:\eoa on one seat, D:\eoa on another) - resolve it from this script's own
# location, never hardcode a drive letter. dev/tmp/<script>.py -> parents[2].
ROOT = str(Path(__file__).resolve().parents[2])

def sh(a):
    return subprocess.run(a, capture_output=True, text=True, cwd=ROOT)

def commit(paths, msg):
    real = []
    for p in paths:
        real.append(p)
        m = p + '.meta'
        if os.path.exists(os.path.join(ROOT, m)):
            real.append(m)
    sh(['git', 'add', '--'] + real)
    st = sh(['git', 'diff', '--cached', '--name-only']).stdout.strip()
    if not st:
        print('  (nothing staged) SKIP')
        return
    body = msg + "\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>\n"
    io.open(os.path.join(ROOT, '.git', 'CMSG'), 'w', encoding='utf-8', newline='\n').write(body)
    c = sh(['git', 'commit', '-F', '.git/CMSG'])
    ok = '' if c.returncode == 0 else 'ERR ' + (c.stderr or c.stdout)[:300]
    print('  committed %d file(s) %s' % (len(st.splitlines()), ok))


VFX_MSG = """feat(vfx): wire elite/boss tells + the five tagged surface impacts

WO-874 - THE CONTROLLER IS NOW GENUINELY ATTACHED. The owner ruled WIRE on
2026-08-04 and reconfirmed it 2026-08-21, but commit 4c1da079 had delivered the
tell via STATICS instead, routing around the ruling with no reversal recorded:
AddComponent<EliteVFXController> returned ZERO hits repo-wide and the aura and
OnEliteAttack had never once run. Enemy.EnsureEliteVfx now attaches and arms it
from the end of Configure - the one place every spawn path passes, including
pooled reuse. A source-lint asserts the literal AddComponent plus public
instance ArmForTier/OnEliteAttack via reflection, so text and type must BOTH
agree and a comment cannot satisfy it. DragonBoss gets its spawn entrance (E8).

POOLED-DOWNGRADE LEAK, caught by EnemyPoolResetRegression and fixed here:
ArmForTier called auraParticles.Play() ABOVE its plain-tier early return, so a
body that had lived as an ELITE and was reused as a PLAIN mob started its aura
and never stopped it - a trash enemy wearing an elite's glow, dependent on what
the pooled body WAS in a previous life, which is the hardest class of bug to
reproduce. The Play() moved below the guard and the plain branch now Stop()s
explicitly. _eliteVfx is exempted from the pool-reset sweep with its reasoning
recorded (re-fetched + re-armed by Configure; downgrade handled in ArmForTier).

E9 as written was STALE: 8 of 11 ladder rows already have catalog entries, not
0. Three still need an owner tag - Boss_AttackImpact, Boss_PhaseTransition,
Boss_Telegraph. Hooks are live and firing; they fall through to the procedural
burst until tagged. NOTHING WAS PICKED - the owner tags VFX keys, the CLI maps
key to hook verbatim.

WO-887 - THE SURFACE HALF IS UNBLOCKED. The original refusal concluded "the
surface signal does not exist" from the shared physics LAYER. It was measuring
the wrong thing: WallSegment.Tier has been public 1..3 the whole time
(WallTier Wood/Iron/ReinforcedSteel). Resolution: wall tier 1 to Wood, tier 2-3
and gates to Metal, enemies/hero/troops to Flesh, other structures to Stone,
Sand deliberately unreachable. The five owner-tagged PP_* keys are mapped
VERBATIM into a new runtime HitSurface home rather than appended to VFXType,
which is serialised by ordinal and single-owner.

The five source prefabs carried demo geometry on the ROOT (mesh + pack material
+ a SPHERE COLLIDER - a physics body on an impact effect) and emitted 5/sec ON
LOOP. The mirror builder strips all of it; verified on disk after the run: 0
colliders, 0 mesh filters, 0 looping layers across all five.

Note the mirrors REPAIR the art rather than pinning around it: no OwnerPinned
entry was added, so the loop-flag derivation now reads the truth instead of
being told to ignore it. The store.beacon.near pin and its seven siblings are
untouched.

Gate: COMPILE_GATE_OK + DataRegression 248/250 on fresh logs. The two reds are
the ticketed asset gaps WO-1135 (wall tier materials were never tracked) and
WO-1136 (staff_A is geometrically symmetrical) - neither fixable in code."""

STAKES_MSG = """feat(siege): loss stakes are COLLECTOR LOOTING - and the rival was deleted

THE THEFT THE OWNER WANTED ALREADY SHIPPED. A first pass built a flat
15%-of-BANKED-resources take with a 20% protected floor. While verifying it, the
mechanic she had actually described was found already live as WO-664:
ResourceCollector.OnSiegeDestroyed steals floor(_pending * 0.5) when a collector
breaks, LastLootStolen records it, WaveDamageReport already renders a "looted"
line, and EnemyBrain PRIORITISES collectors before the generic structure
fallback (SiegeRoleValue 0.85 * (1 + fill*0.75), so a full one scores 1.49 vs an
empty 0.85 - raiders go for the ones worth robbing).

So the bank take was a RIVAL SYSTEM: a second theft, from a different pool, on a
different trigger, through a different ledger. Deleted - StealFraction,
ProtectedFloorFraction, ProtectedFloor, TakeFrom, the bank-parameter Build
overload, and the entire EconomyService debit in ApplyStakes. The builder no
longer even READS the bank; as the implementing note put it, knowing is the
first half of taking.

NOTHING DEBITS THE WALLET FOR A SIEGE. ResourceCollector already removed the
resources from its own _pending when it broke, so the deleted debit would have
CHARGED THE PLAYER TWICE for one siege. BuildStakes is now a READ: it sums each
broken collector's own LastLootStolen into per-resource buckets. Report figure
and actual loss are the same number by identity, not by two calculations
agreeing.

RULING (owner 2026-08-22, after asking how CoC does it): COLLECTOR LOOTING ONLY,
RaidLootFraction stays 0.5, no bank theft. Player-facing rule: WHAT YOU HAVE
COLLECTED IS SAFE; WHAT IS STILL IN THE BUILDING IS AT RISK. CoC loots
collectors heavily AND storages lightly, but its storage half only survives on
shields, village guard, the loot cart and matchmaking limits - we have none of
that scaffolding, so bank theft would make us harsher than the game we model.
Agency, not severity, is the retention variable: collector loss is fully
preventable by collecting, so it converts into return visits instead of
resentment.

CRYSTAL COLLECTORS ARE NOT LOOTABLE, enforced twice independently - at the
steal (LootTakenFrom returns 0 for Crystals) and at the ledger (IsLootable
allows only Wood/Iron/Food). A player cannot tell harvested crystals from
PURCHASED ones; they are the same wallet, so any crystal loss reads as losing
bought currency.

A bug that would have shipped: a destroyed collector is never repairable
(WO-753), so LastLootStolen sits on it all session and "sum every broken
collector" would have RE-ANNOUNCED THE SAME ROBBERY on every later siege. A
session-scoped break stamp now counts only breaks within this siege.

The oracle uses hand-worked literals, never expressions over the constants -
(Wood,800) to 400, (Crystals,800) to 0 - so changing RaidLootFraction turns it
RED, which is the alarm you want on a player-money rule. It also asserts by
REFLECTION that the deleted bank methods do NOT exist, so re-adding the rival
system fails the gate at the moment it is written.

Also corrected here: BreakableContainerChestRegression demanded a literal
RollAndDeposit call and went red against BETTER code - the chest now captures
ONE roll and routes it to either delivery path, where RollAndDeposit would roll
a SECOND time and could pay something other than what the mote showed. The
assertion named a METHOD instead of the BEHAVIOUR, so improving the
implementation broke the oracle. It now pins the behaviour.

FeatureFlags.Siege is ON and PROVEN: defense-report, siege-cadence,
siege-spawn-authority and siege-loss-stakes all green on a fresh log.
No schema bump - additive default-on-read. v38 stands."""

print('LANE: elite/boss VFX + surface impacts (WO-874, WO-887)')
commit([
    'Assets/_Modules/Village/Enemies/Enemy.cs',
    'Assets/_Modules/Village/Enemies/EliteVFXController.cs',
    'Assets/_Modules/Village/Enemies/DragonBoss.cs',
    'Assets/_Modules/Village/Vfx/HitSurface.cs',
    'Assets/Editor/SurfaceImpactVfxMirrors.cs',
    'Assets/Editor/Regression/SurfaceImpactMirrorSet.cs',
    'Assets/Editor/Regression/SurfaceImpactVfxRegression.cs',
    'Assets/Editor/Regression/EliteVfxWiringRegression.cs',
    'Assets/Editor/Regression/EnemyPoolResetRegression.cs',
    'Assets/Resources/VFX/Impact',
    'Assets/Resources/VFX/Impact.meta',
], VFX_MSG)

print('LANE: loss stakes -> collector looting (WO-1139)')
commit([
    'Assets/_Modules/Core/Defense/StakeRules.cs',
    'Assets/_Modules/Village/Waves/DefenseReportBuilder.cs',
    'Assets/_Modules/Village/Buildings/Progression/ResourceCollector.cs',
    'Assets/_Modules/Village/UI/Defense/DefenseReportPanel.cs',
    'Assets/Editor/Regression/SiegeLossStakesRegression.cs',
    'Assets/Editor/Regression/DefenseReportContractRegression.cs',
    'Assets/Editor/Regression/BreakableContainerChestRegression.cs',
    'Assets/_Modules/Core/State/SaveSchema.cs',
    'Assets/_Modules/Core/FeatureFlags.cs',
    'Assets/_Modules/Village/Waves/SiegeScheduler.cs',
    'Assets/_Modules/Village/Waves/SiegeSession.cs',
    'Assets/Editor/Regression/DataRegression.cs',
], STAKES_MSG)
