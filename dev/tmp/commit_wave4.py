import io, subprocess, os
ROOT = r'D:\eoa'
FORBID = ('AdGateService', 'LevelPlayInitializer', 'RewardedAdManager', 'PurchaseGate',
          'PackStore', 'BuildTimerService', 'MON_')   # Codex's live MON lane - not mine to stage

def sh(a): return subprocess.run(a, capture_output=True, text=True, cwd=ROOT)

def commit(paths, msg):
    real = []
    for p in paths:
        real.append(p)
        m = p + '.meta'
        if os.path.exists(os.path.join(ROOT, m)): real.append(m)
    sh(['git', 'add', '--'] + real)
    staged = sh(['git', 'diff', '--cached', '--name-only']).stdout.split()
    if not staged:
        print('  (nothing staged) SKIP'); return
    strays = [p for p in staged if any(f in p for f in FORBID)]
    if strays:
        print('  STOP - another lane staged:', strays); raise SystemExit(1)
    io.open(os.path.join(ROOT, '.git', 'CMSG'), 'w', encoding='utf-8', newline='\n').write(
        msg + "\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>\n")
    c = sh(['git', 'commit', '-F', '.git/CMSG'])
    print('  committed %d file(s) %s' % (len(staged), '' if c.returncode == 0 else 'ERR ' + (c.stderr or c.stdout)[:200]))


print('LANE: hollow-pass ratchet (WO-1138)')
commit([
    'Assets/Editor/Regression/HollowPassScanner.cs',
    'Assets/Editor/Regression/HollowPassFixtures.cs',
    'Assets/Editor/Regression/RegressionMarkerRegression.cs',
],
"""feat(gate): the hollow-pass ratchet reads CONTROL FLOW, not a 4-line window (WO-1138)

A hollow pass is a regression case that returns GREEN while asserting nothing -
`if (dependencyMissing) { notes.Add("SKIPPED..."); return; }` where notes feed
the SUCCESS string, so a skip IS a pass. The detector that existed caught ONE of
six such sites in CosmeticApplyRegression on 2026-08-21; the other five escaped
because it inspected a ~4-LINE WINDOW around the return. Its coverage was a
function of CODE FORMATTING - the least reliable signal available.

HollowPassScanner replaces the window with a brace-depth walk: backward from each
return to the innermost enclosing block (4 lines or 400), the block header for
the guard condition, and the statements at the block's own nesting depth for the
exonerating evidence. Everything it reads is braces and statement boundaries,
which the compiler already agrees with, so reindenting or wrapping a string moves
nothing. Four arms: missing-dependency, says-skip, negated-guard (the 2026-08-16
evasion), and vacuous-against-an-absent-fixture - the RaidCooldown case-5 family,
which NO token scan catches at any window length.

⭐ IT KEEPS THE RETIRED DETECTOR EXECUTABLE AS A CONTROL. HollowPassFixtures holds
the six pre-fix sites as source text and runs BOTH detectors over them:
  new scanner 6, retired window 1  - reproducing the historical miss exactly.
SelfTest goes RED if the new scanner ever stops finding strictly more than the
old one. A fix that proves itself against the thing it replaced.

It also cannot pass hollowly itself: unbalanced braces, a mask mismatch or a
throw sets analysisError, empties the results and FAILS - "unscannable oracle
cannot be certified."

THE SWEEP FOUND 27 MORE, ledgered PER SITE on (file, arm, condensed guard) -
deliberately NOT line numbers, which would hand back the formatting-dependence
this ticket deletes. The retired baseline was per-FILE, so it hid every hollow
pass in a file including new ones; a site row now excuses exactly one guard, and
a second in the same file fails next run. Each row names what is absent and which
resolution it is owed: fixture-absent FAILS, harness-capability declares a
VISIBLE stand-down, content-absent asserts THROUGH. Tuning went 404 -> 27 by
reading actual sites, never by loosening a threshold.

⛔ Also here: RULE 2 excluded HollowPassFixtures.cs by name. That file stores fake
oracle source as TEST DATA in verbatim string constants, so the text scan read
"class CleanSuite { Run(out string ...) }" and demanded it be registered - a
guard mistaking test data for production code. A DECLARATION INSIDE A STRING
LITERAL IS NOT A DECLARATION, the same class as counting braces inside quoted
source. Narrow named exclusion; the general fix (strip verbatim literals before
the scan) is a wider change to a load-bearing gate and is left flagged rather
than silently absorbed.

Gate: COMPILE_GATE_OK + REGRESSION_OK 255/255, both on fresh logs.""")

print('LANE: VFX mirror-join oracle')
commit([
    'Assets/Editor/Regression/VfxLoopFlagRegression.cs',
    'Assets/Editor/Regression/VfxMirrorPairSet.cs',
    'Assets/Editor/PortalCircleVfxMirror.cs',
    'Assets/Editor/TalentPointerVfxMirror.cs',
    'Assets/Editor/StatusVfxMirrors.cs',
    'WorkOrders/WORK_ORDER_1057_vfx_loop_registry_and_stuck_loop_dump.md',
],
"""feat(gate): assert the catalog row POINTS AT the mirror - the gap two oracles left

The owner's "random vfx stuck around" was a permanent loop leak, and the reason
it shipped is that TWO GREEN ORACLES each asserted correctly and neither asked
the question between them:

  VfxLoopFlagRegression      row IsLoop  vs  THE PREFAB THAT ROW POINTS AT
                             (both the pack copy, both loop)      -> green
  SurfaceImpactVfxRegression THE MIRROR is one-shot
                             (it was)                             -> green

NEITHER ASSERTED THAT THE ROW POINTS AT THE MIRROR. The bug lived exactly in the
gap between two correct assertions.

CheckRowPointsAtMirror closes it, in VfxLoopFlagRegression because a row pointing
at the wrong prefab is a fact about the ROW - and because that suite already
walks every row of both catalogs and resolves every prefab reference, so the join
is one more question asked of an object already in hand, in the same pass. Two
answers about one row can never drift or run against different catalog states.

It READS the catalog asset from disk and resolves each row's serialized prefab
reference to a path, then compares against the table the mirror BUILDERS read -
never recomputing a guid from the generator, which would only prove the generator
agrees with itself. Positive control written into the file: in the shipped state
PP_WoodImpacts held the pack guid, GetAssetPath returns the pack path,
TryMirrorForSource hits, and it FAILS naming the key and both paths.

⭐ AND IT PRINTS ITS OWN VACUITY. The summary always reports redirectable pairs,
mirrors loadable here, rows joined, rows already at a mirror, and unresolvable
rows; if no mirror loads or no row joins it says VACUOUS ON THIS MACHINE. A run
that COULD NOT have failed no longer looks identical to one that could - which is
the root disease behind every gate finding this week.

Supporting: VfxMirrorPairSet gives the four builders' source->mirror pairs ONE
home (DeNelle.Editor -> DeNelle.EditorRegression is one-way, so a table declared
in a builder is invisible to its own gate). The three builders now read their
paths back out of it rather than restating them.

Recorded on WO-1057, NOT fixed: two loops held by a SINGLE release path that a
torn-down projectile never reaches - PP_FireBall (the default for EVERY ranged
enemy) and icebasedprojectile_Projectile. ProjectileMover has no timeout, no
OnDisable, no OnDestroy, and Arrive() is the only caller of _onArrive, so a
projectile torn down in flight strands its loop permanently, mid-air. That fix is
loop POLICY and WO-1057 owns policy; a finder should not set it quietly.""")

print('LANE: inventory rail (WO-1133)')
commit([
    'Assets/_Modules/Village/Hero/InventoryUIBuilder.cs',
    'Assets/_Modules/Village/Hero/InventoryPaperDoll.cs',
    'Assets/_Modules/Village/Hero/InventorySidebar.cs',
    'Assets/_Modules/Village/Hero/InventoryGrid.cs',
    'Assets/_Modules/Village/Hero/HeroInventoryController.cs',
    'Assets/_Modules/Village/Hero/HeroPreviewViewer.cs',
    'Assets/_Modules/Core/UI/InventoryStrings.cs',
    'Assets/Editor/Regression/InventoryArmoryRailRegression.cs',
    'Assets/Editor/Regression/DataRegression.cs',
    'Assets/Resources/Data/Canonical/canon-strings.json',
    'Assets/StreamingAssets/Data/Canonical/canon-strings.json',
    'WorkOrders/WORK_ORDER_1133_inventory_screen_redesign.md',
],
"""feat(ui): the bag becomes a rail, and the dead preview box is cut (WO-1133)

Owner: "there is no benefit to the gear view like it is, and opening isnt much
better". The UI seat's design answered it by inverting the question - the good
gear screen ALREADY EXISTS (EquipmentPanel, 1,452 lines of bound MVVM, called
from six sites) and the bag already had a hidden door to it. So the empty navy
rectangle was a broken preview box sitting on top of a button that opens the
working one. Cut the door, keep the room.

REMOVAL IS ~HALF THE TICKET, so it leads: the empty preview box, the gold VIEW
GEAR ribbon and the whole left hero card are gone (the PanelRouter route to
EquipmentPanel survives as the rail's Gear section); the top tab row is deleted -
it clipped its own selected label and could not carry counts; the full-width
gold hint bar is replaced by an always-present pane; the 78x72 cell literal is
gone (it was far under MinTouchPx) and cell size is now DERIVED from the measured
stage width; the 25-plate empty-page padding is gone - an empty section shows one
authored sentence instead of 23 decorative sockets.

⛔ THE PROMOTION IS DELIBERATELY NOT THE PAYLOAD, and this is the important part.
Mid-implementation the owner pressed F8 twice (seq 3585, 3586, via two different
entry routes) and the RT probe fired from EquipmentPanel itself:

  "the preview render texture is a UNIFORM clear colour - the preview box is
   blank at the SOURCE, not at the panel. Fix the model/culling, not the RawImage."

So BOTH previews are blank, and D1's "promote the working gear screen" would have
promoted an empty box. The bag can no longer show one - its niche mounts 3D only
through a DrewContent evidence gate and falls back to a 2D portrait - but the
EquipmentPanel route is untouched and still shows one. Stated plainly rather than
shipped as if fixed.

Diagnosis located, NOT concluded (canon 12): layer filtering is eliminated (URP
masks are Everything), and lighting is eliminated (an unlit model reads black,
which differs from the clear colour by more than the probe threshold - diff==0
means nothing rasterized). Two survivors: ComputeBounds sums world-space
Renderer.bounds on a clone instantiated THIS FRAME at (-5000,-5000,0), and a
SkinnedMeshRenderer with updateWhenOffscreen=false can report the SOURCE body's
AABB - aiming the camera ~7000 units off, at an empty frustum; or the clone has
no drawable renderers. The bag's mount now probes tagged "Inventory" (EquipmentPanel
tags "Equip") so future captures are attributable, and a new camera-framing trace
prints aim point vs actual clone position.

⚠ D3'S RAIL GEOMETRY DID NOT SATISFY ITS OWN MinTouchPx CLAIM, reported rather
than quietly absorbed: at 2670x1200 the scaler yields ~965 ref px of canvas, so
D3's 132 device px entry is ~106 REF px - under the 112 floor. Seven entries at
the real floor need 832 ref px, more than the panel has. The rail is authored AT
the floor and scrolls (~5 of 7 visible); D3 forbids leaning on ClampMinTouch, so
that was the only compliant option, and the oracle asserts the arithmetic.

All 44 authored strings landed verbatim in BOTH canon-strings copies (cmp clean,
ASCII). The oracle's six cases each use an INDEPENDENT authority - zone ratios
are parsed out of source and checked against the WO's numbers, not recomputed
from the layout's own constants.""")
