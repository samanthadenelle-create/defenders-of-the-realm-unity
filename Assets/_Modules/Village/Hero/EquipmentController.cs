// =============================================================================
// EquipmentController — visually equips real (KayKit) weapon meshes on a Humanoid
// hero by attaching them to the rig's hand bones, driven by the existing Gear-v1
// equip data (GearLoadout / WeaponDef). Armor is stubbed (entry point wired, no
// visual yet — assets incoming).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS / WHAT IT GENERALIZES:
//   Gear-v1 already attaches a *primitive cube* sword/staff/mace to the RightHand
//   bone via GearVisualApplier.AttachWeaponVisual (GearVisualApplier.cs:104-203,
//   parent resolved at :137 GetBoneTransform(HumanBodyBones.RightHand)), and the
//   Ranger gets a real KayKit bow via HeroBowAttachment (LeftHand bone, prop loaded
//   from Resources/Heroes/Props + bounds-normalized). This controller GENERALIZES
//   that pattern to ALL weapon classes using the real KayKit weapon meshes:
//     • resolve the equipped weapon id (from GearLoadout.EquippedWeapon, the SAME
//       data model — no new gear model invented),
//     • map the id -> a KayKit mesh + per-weapon grip offset/rotation,
//       (mesh attaches to RightHand; shields -> LeftHand),
//     • instantiate, parent, destroy the previous prop on swap (no stacking),
//     • re-attach whenever GearLoadout.OnGearChanged fires (the SAME event the
//       cube path already raises on equip-change).
//   The legacy cube GearVisualApplier stays as the no-mesh fallback (it null-guards
//   and is gated OFF by EnablePrimitiveGear), so nothing double-stacks: when a real
//   mesh resolves we use it; otherwise we keep the existing behaviour.
//
// MESH-LOADING GAP (important):
//   The KayKit weapon FBXs live under Assets/Models/KayKit/.../KayKit Fantasy
//   Weapons Bits 1.0/Assets/fbx(unity)/ — that folder is NOT a Resources folder
//   (and the pack is gitignored), so Resources.Load CANNOT reach them at runtime /
//   in a build. This mirrors the exact constraint HeroBowAttachment documents for
//   the bow. The build-safe convention already used for the bow is to COPY the
//   needed KayKit props into Assets/Resources/Heroes/Props/ (committed, Resources-
//   loadable). So this controller loads each weapon mesh from
//       Resources/Heroes/Props/Weapons/<meshName>
//   FIRST; if absent (mesh not yet copied), it falls back to a tinted primitive so
//   the hero still reads as armed. ACTION FOR ART/CLI: drop sword_A/D/G, staff_A,
//   wand_A, bow_A, dagger_A, axe_A, hammer_A, shield_A (as prefabs or fbx) into
//   Assets/Resources/Heroes/Props/Weapons/ to light up the real meshes. Until then
//   the primitive fallback renders.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Geometry;

namespace DeNelle.Village
{
    /// <summary>
    /// Component on a hero. Reads the hero's equipped weapon (via GearLoadout, the
    /// Gear-v1 data model) and attaches a real KayKit weapon mesh to the Humanoid
    /// rig's RightHand bone (shields -> LeftHand) with a per-weapon grip transform.
    /// Re-attaches on equip-change. Armor is a wired-but-no-op stub for now.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentController : MonoBehaviour
    {
        // Resources sub-path the build-safe KayKit weapon props are copied into
        // (mirrors HeroBowAttachment's "Heroes/Props/Bow"). See file header / gap note.
        private const string WeaponPropResourceDir = "Heroes/Props/Weapons/";
        // Reference standing height for heldLength presets (GearVisualApplier / NavMeshAgent canon).
        private const float RefHeroHeightM = 1.8f;

        private const string PropName = "EquipmentProp_Weapon";

        // ── Weapon-id -> KayKit mesh + grip map ──────────────────────────────────
        // TODO data-driven: add a `visualMesh` (string) + `grip` (pos/euler/scale) to
        // weapons.json and read them off WeaponDef instead of this hardcoded table.
        // For now this maps the ACTUAL ids in weapons.json (mage_*, knight_*, ranger_*,
        // aegis_*) onto owned KayKit Fantasy Weapons Bits meshes. The grip values seat
        // the hilt/grip in the palm for a ~1.8m Humanoid hero; tune against a screenshot.
        // Weapon family — gates the grip-point algorithm. Sword/Blade uses the geometric
        // hilt-spike inference (grip the HANDLE below the crossguard). Everything else keeps
        // its proven centre/root grip but the same hook exists to extend later
        // (Staff -> mid-shaft, Bow -> riser/centre, etc.).
        private enum WeaponClass { Sword, Dagger, Axe, Hammer, Staff, Wand, Bow, Shield }

        // WO-435: "melee" = every hand-held shafted/bladed weapon that grips from its own
        // geometry (handle below the head/crossguard, primary axis pointing out of the fist).
        // ALL of these now share ONE seating path: bounds-normalize -> SeatByHandle (grip from
        // mesh) -> rig-hand-axis rotation + per-archetype nudge. Bow (own NormalizeInto + LeftHand
        // centre-grip) and Shield (centre-grip, LeftHand) are NOT melee and keep their own paths.
        private static bool IsMelee(WeaponClass k) =>
            k == WeaponClass.Sword || k == WeaponClass.Dagger || k == WeaponClass.Axe ||
            k == WeaponClass.Hammer || k == WeaponClass.Staff || k == WeaponClass.Wand;

        private sealed class WeaponVisual
        {
            public string mesh;          // KayKit mesh name under Resources/Heroes/Props/Weapons/
            public bool leftHand;        // shields -> LeftHand; everything else RightHand
            public Vector3 gripPos;      // local position on the hand bone
            public Vector3 gripEuler;    // local rotation on the hand bone
            public float heldLength;     // longest-axis target length (m) after bounds-normalize
            public Color tint;           // fallback-primitive tint when the mesh isn't present
            public WeaponClass kind;     // family — drives the grip-point inference path
            public bool native;          // prop is authored grip-at-origin + oriented (e.g. Blink) — trust it: skip normalize/hilt-seat
        }

        // Per-archetype grip presets (one place to tune each weapon family's seat).
        // GRIP-POINT (WO-435): gripPos for melee is now ZERO — the grip point is DERIVED from
        // the mesh by SeatByHandle (handle-from-geometry), not hand-typed. The old per-archetype
        // Y-offsets ("0.02/0.05 everywhere") were the §4 smell: a constant applied asset-agnostic
        // to every FBX regardless of its own handle pivot. Only Shield keeps a deliberate non-zero
        // gripPos (its centre-grip seat) and the bow stays zero (its own NormalizeInto path).
        private static WeaponVisual Sword(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = false, kind = WeaponClass.Sword,
            gripPos = Vector3.zero, gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 0.65f, tint = new Color(0.74f, 0.75f, 0.78f)   // ~36% of RefHeroHeightM (GearVisualApplier canon)
        };
        private static WeaponVisual Dagger(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = false, kind = WeaponClass.Dagger,
            gripPos = Vector3.zero, gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 0.40f, tint = new Color(0.70f, 0.72f, 0.76f)
        };
        private static WeaponVisual Axe(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = false, kind = WeaponClass.Axe,
            gripPos = Vector3.zero, gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 0.80f, tint = new Color(0.68f, 0.66f, 0.62f)
        };
        private static WeaponVisual Hammer(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = false, kind = WeaponClass.Hammer,
            gripPos = Vector3.zero, gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 0.85f, tint = new Color(0.66f, 0.66f, 0.68f)
        };
        private static WeaponVisual Staff(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = false, kind = WeaponClass.Staff,
            gripPos = Vector3.zero, gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 1.30f, tint = new Color(0.60f, 0.50f, 0.40f)
        };
        private static WeaponVisual Wand(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = false, kind = WeaponClass.Wand,
            gripPos = Vector3.zero, gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 0.45f, tint = new Color(0.55f, 0.45f, 0.62f)
        };
        private static WeaponVisual Bow(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = true, kind = WeaponClass.Bow,   // bow goes in the off/bow (LEFT) hand
            // owner spec: bow longest->Y, grip=center; TUNABLE — nudge gripEuler on playtest
            // NormalizeInto already seats the bow to spec deterministically: LONGEST axis
            // (limbs/nock-to-nock) -> local +Y (upright), NARROWEST -> +X (thin left-right,
            // curve depth -> +Z forward), bounds-CENTRE at the grip root origin (hand grips
            // the middle of the curve). So gripEuler stays ZERO here — exactly the
            // proven-correct value HeroBowAttachment uses for the Ranger's held bow
            // (HeroBowAttachment.GripLocalEuler == (0,0,0); a prior +91 Z tweak rotated the
            // already-correct bow ~90° sideways — that WAS the "bow is turned" bug). Keep this
            // a single value: if a touch off in-hand, nudge ONLY this gripEuler on playtest.
            gripPos = new Vector3(0f, 0f, 0f), gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 0.92f, tint = new Color(0.36f, 0.22f, 0.10f)
        };
        private static WeaponVisual Shield(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = true, kind = WeaponClass.Shield,   // shields -> LeftHand per spec
            gripPos = new Vector3(-0.05f, 0f, 0f), gripEuler = new Vector3(-58f, 16f, -90f),  // Offset Forge + hand-bone nudge 2026-06-23: shield_A rot (-58,16,-90).
            heldLength = 0.45f, tint = new Color(0.58f, 0.60f, 0.64f)   // ~25% of RefHeroHeightM — torso-scale buckler
        };

        // Shallow copy of a preset so the Addressable/fallback paths can flip `native` WITHOUT
        // mutating the shared cached IdMap instance (Resolve may return a cached preset).
        private static WeaponVisual CopyOf(WeaponVisual v) => new WeaponVisual
        {
            mesh = v.mesh, leftHand = v.leftHand, gripPos = v.gripPos, gripEuler = v.gripEuler,
            heldLength = v.heldLength, tint = v.tint, kind = v.kind, native = v.native
        };

        // Mark a preset as a NATIVE prop — a grip-at-origin, correctly-oriented authored prefab
        // (e.g. a Blink weapon: Sword1h_01 sits at the origin, identity rotation). Equip() then
        // routes it through SeatNative (trust its pivot) instead of the bounds-normalize +
        // hilt-inference legacy path that reverse-engineers a grip for raw Tripo/KayKit FBX.
        // Use for any Blink/authored .prefab dropped into Resources/Heroes/Props/Weapons.
        private static WeaponVisual Native(WeaponVisual v) { v.native = true; return v; }

        // Exact-id overrides keyed by the ids actually present in weapons.json.
        // (Falls through to the keyword classifier below for anything not listed.)
        // TODO data-driven: delete this once weapons.json carries visualMesh/grip.
        private static readonly Dictionary<string, WeaponVisual> IdMap =
            new Dictionary<string, WeaponVisual>(System.StringComparer.OrdinalIgnoreCase)
        {
            // Mage — wand at low tier, staff higher.
            { "mage_starter",        Wand("wand_A")   },
            { "mage_oak",            Staff("staff_A") },
            { "mage_arcane",         Staff("staff_B") },
            { "mage_void",           Staff("staff_C") },
            { "aegis_aetherstaff",   Staff("staff_D") },

            // Knight — sword tiers -> sword_A / sword_D / sword_G by tier.
            { "knight_starter",      Native(Sword("sword_A")) },   // Blink Sword1h_01 prefab (grip-at-origin)
            { "knight_iron",         Sword("sword_D") },
            { "knight_oath",         Sword("sword_F") },
            { "knight_dawn",         Sword("sword_G") },
            { "aegis_emberbrand",    Sword("sword_G") },

            // Ranger — bows (LeftHand). NOTE: the Ranger's held bow is ALSO provided by
            // HeroBowAttachment; see EquipBestForHero() where we skip bows to avoid a
            // duplicate. Kept here so a non-ranger equipping a bow still gets one.
            { "ranger_starter",      Bow("bow_A") },
            { "ranger_yew",          Bow("bow_B") },
            { "ranger_storm",        Bow("bow_C") },
            { "ranger_eclipse",      Bow("bow_C") },
            { "aegis_heartwood_longbow", Bow("bow_C") },

            // Cleric — censer reads closest to a mace/hammer; use hammer_A stand-in.
            { "aegis_hallowed_censer", Hammer("hammer_A") },
        };

        // ── Runtime state ────────────────────────────────────────────────────────
        private Animator _animator;
        private float _cachedHeroHeightM;   // measured once per body; 0 = not yet measured
        private GearLoadout _loadout;

        // PACKAGE de-dupe (owner F8 2026-07-03 "holding two swords, shield 180°"): when the hero body
        // BAKES its own weapon/shield/helmet (Paladin package — HeroBodySwapper tags the SAME root with
        // PackageBakedGearMarker), the KayKit weapon-mesh + shield-mesh prop attach is SKIPPED so the
        // baked gear is the only gear visible. Cheap GetComponent on the root — equip is event-driven,
        // not a hot loop (and LateAttachRetry early-outs on it). Loadout/stat/armor-tint stay fully active.
        private bool PackageBakedGear => GetComponent<PackageBakedGearMarker>() != null;
        private GameObject _currentWeaponProp;
        private string _currentWeaponId;
        private int _armorTier;

        // ── IN-GAME SEATING EDITOR support (WO-577, Offset Forge slice 2) ─────────────
        // The live on-screen Seating Editor (SeatingEditorOverlay) edits the offset of the
        // CURRENTLY equipped weapon/off-hand by eye. To preview live + reproduce the runtime
        // seat exactly, it needs the same inputs the attach path used. These are captured on
        // each attach (main-hand below; off-hand mirror further down) and consumed by the
        // public editor API at the bottom of this class. Inert when no editor is open.
        private string      _currentWeaponMeshKey;     // offset id (mesh name, e.g. "sword_A")
        private WeaponClass _currentWeaponKind;
        private bool        _currentWeaponMelee;
        private float       _currentWeaponHeldLength;
        private Vector3     _currentWeaponGripPos;
        private Vector3     _currentWeaponGripEuler;
        private bool        _currentWeaponNative;
        private string  _currentOffHandMeshKey;
        private float   _currentOffHandHeldLength;
        private Vector3 _currentOffHandGripPos;
        private Vector3 _currentOffHandGripEuler;
        private bool    _currentOffHandNative;
        // While a seating edit is live: suspend the auto idle/combat hold so the grip root
        // the editor drives is not stomped by ApplyHoldPose, and remember which slot is edited.
        private bool _seatingEditActive;
        private bool _seatEditOffHand;
        private int  _seatEditMode = -1;   // -1 unseated, 0 nudge(geometry), 1 vertical(fullOverride)
        // SHEATHED seating edit (2026-07-07): the owner dials the BACK (sheathed) pose live; the
        // edit runs in the back-socket frame and saves under "<meshKey>@sheathed" (see ApplyHoldPose).
        private bool _seatEditSheathed;

        // Registry key suffix for owner-authored SHEATHED poses. Registry keys are arbitrary
        // strings (verified: plain Dictionary + JsonUtility string field, no sanitization), so
        // '@' passes through save/load/remove untouched.
        private const string SheathedKeySuffix = "@sheathed";

        // AUTHORED SCALE per slot (2026-07-07 WYSIWYG scale-parity fix): the owner-dialed uniform
        // scale (offsets.json fo.scale, default 1). The rendered local scale for a compensated slot
        // is ALWAYS ParentScaleCompensation(parent) * authoredScale — one composition shared by the
        // attach path, ApplyHoldPose re-parents, and the Seating Editor preview, so what the owner
        // approves in the editor is byte-identical to every subsequent boot.
        private float _weaponAuthoredScale  = 1f;
        private float _offHandAuthoredScale = 1f;

        // ── ARMOR TINT (WO-567) ──────────────────────────────────────────────────────
        // The combat-pivot north star keeps ONE static hero model — armor is NOT a mesh swap
        // (Blink junked). To make "equipped armor" READ on the body, higher armor tiers tint the
        // hero BODY via a MaterialPropertyBlock accent (a base-color multiply, richer with tier).
        // CHEAP + LEAK-FREE: an MPB never instances a material (mirrors HeroArmorRimLight). It
        // COEXISTS with HeroArmorRimLight's emission MPB — both use the GetPropertyBlock-merge
        // pattern, so the base-color tint (this) and the rarity rim GLOW (rim light) stack.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock _armorMpb;
        private readonly List<SkinnedMeshRenderer> _bodyRenderers = new List<SkinnedMeshRenderer>();
        private readonly List<Color> _bodyBaseColors = new List<Color>();   // authored base color per renderer (multiply target)
        private bool _armorTintDirty;

        // OFF-HAND (shield) prop — attached to the OFF hand (LeftHand) alongside the main weapon.
        // Mirrors the main-hand prop lifecycle (destroy-on-swap, no stacking). Driven off
        // GearLoadout.EquippedOffHand on the SAME OnGearChanged event the main weapon uses.
        private const string OffHandPropName = "EquipmentProp_OffHand";
        private GameObject _currentOffHandProp;
        private string _currentOffHandId;

        // OFF-HAND Addressables handle (Blink shields load via "gear/weapon/Shield1h_XX"). ONE owner
        // (this controller) — released on every off-hand swap / detach / OnDisable so a Blink shield
        // prefab never leaks. Its OWN generation counter rejects a stale async completion that lands
        // after the player swapped/unequipped the off-hand mid-load (no ghost shield).
        private AsyncOperationHandle<GameObject> _offHandHandle;
        private bool _offHandHandleOpen;
        private int _offHandGeneration;

        // Addressables equip (WO-Item, Blink gear): when the equipped WeaponDef loads its
        // prefab via Addressables (loadVia=="addressable" or a "gear/" address in prefabPath),
        // we LoadAssetAsync the prefab and attach on completion. The handle is held here and
        // Addressables.Release'd on the next swap / unequip / OnDisable so a Blink prefab never
        // leaks (§2b.1 pooling discipline — ONE owner of the handle, the attach system). The
        // generation counter rejects a stale async completion that resolves AFTER the player
        // already swapped to a different weapon (no ghost prop from an out-of-date load).
        private AsyncOperationHandle<GameObject> _weaponHandle;
        private bool _weaponHandleOpen;
        private int _equipGeneration;

        // If true, a bow equip is skipped here because HeroBowAttachment owns the
        // ranger's held bow (set when the hero already carries that component).
        private bool _deferBowToBowAttachment;

        // ── Hold-state (idle lowered  vs  combat ready) ──────────────────────────────
        // Driven off the SAME combat signal the camera/locomotion use: HeroLocomotion
        // computes `engaged = WaveManager present || moving` and calls ActorAnimator
        // .SetCombatStance(engaged). We mirror that here (auto fallback below) and expose
        // SetCombatActive(bool) so any caller (HeroLocomotion / a future CombatState
        // registry) can drive it authoritatively. The pose is applied at the ATTACH level
        // — a local-rotation offset on the grip root — so no hero animation clip is needed
        // for the held-low vs held-ready read (the bonus; the GRIP is the priority).
        private Transform _gripRoot;          // current weapon's grip-root transform

        // WO-VFX-WEAPON-TRAILS: read-only accessor so WeaponTrailController can anchor the blade
        // trail on the actual held weapon's grip root (the moving prop transform) rather than the
        // bare hand bone. Null until a weapon is equipped/attached; the trail controller falls back
        // to the RightHand bone (then a synthetic child) when this is null.
        public Transform GripRoot => _gripRoot;
        private Vector3 _baseGripEuler;       // the weapon's neutral grip rotation
        private bool _combatActive;           // current hold state (false = idle/lowered)
        private bool _combatExplicit;         // a caller drove SetCombatActive -> stop auto-mirroring
        private WaveManager _waveManager;     // auto-fallback combat signal (same as locomotion)

        // ── CARRY STATE: sheathed (town / out-of-combat) ↔ drawn (in-combat) ─────────
        // OWNER DESIGN (2026-07-04): out of combat the weapon is SHEATHED on the BACK — NOT
        // gripped in the hand — and is DRAWN to the hand only in combat.
        //
        // WHY THIS FIXES THE ~60° OVERWORLD FLOAT (context-delta RCA): the weapon is parented to
        // the hand bone, so it FOLLOWS the animated hand identically in both contexts — the seat
        // itself is not globally broken (battle proves it correct). The ONLY per-context rotation
        // in the whole attach path was the retired IdleHoldOffsetEuler = (55,0,0), applied by the
        // old ApplyHoldPose ONLY when out of combat and ZERO in combat. Composed onto _baseGripRot
        // (which yaws the blade +90° via _swordGripEuler), that 55° local-X tilt reads as a ~55-60°
        // world YAW — exactly the owner's "~-60° in Y, correct in battle, wrong in town." Rather
        // than tuning that hack, we remove it: out of combat the hand grip is not shown AT ALL (the
        // weapon is on the back), and in combat the grip uses the SAME seat battle uses (pure
        // _baseGripRot). So the hand grip only ever renders in the context that seats it right.
        //
        // The in-combat signal is the CANONICAL one (HeroLocomotion.IsWaveInCombat, HeroLocomotion.cs
        // :552-563 — a live wave Countdown/Active OR BattleArena.AnyBattleInProgress); the Update()
        // auto-fallback below mirrors it so the draw matches the just-shipped calm/combat idle split.
        //
        // Sheathed pose is VISUAL (owner felt-tunes) — Inspector-exposed with deterministic defaults.
        // The weapon rides a back socket (created under the Chest/Spine bone by ResolveBackSocket)
        // laid diagonally across the back; the off-hand/shield rides the same socket, opposite side.
        [SerializeField] private Vector3 _sheatheWeaponLocalPos   = new Vector3(-0.10f, 0.12f, -0.15f);
        // SHEATHED SWORD ROTATION — DERIVED, not guessed (owner F8 fix 2026-07-04): the on-back sword
        // rotation is no longer a magic hand-typed euler (the old (8,0,158) had ZERO relationship to the
        // weapon geometry OR the chest bone's rig-specific axes — the §4 smell, exactly why it sat wrong
        // while the DRAWN seat was right). ApplyHoldPose now builds the base sheathe orientation from the
        // BODY's own axes with the SAME Quaternion.LookRotation(flat, blade) construction the correct
        // battle draw uses (ComputeMeleeGripRotation, the "secret") — see ComputeSheathRotation. This
        // field is the persisted AUTHORED NUDGE composed ON TOP of that derived base (Inspector/owner
        // felt-tune, never auto-overwritten), mirroring how _swordGripEuler nudges the drawn seat.
        // Default ZERO = pure geometric sheathe (component is always runtime AddComponent, so this code
        // default applies — no scene/prefab serializes an old value over it).
        [SerializeField] private Vector3 _sheatheWeaponLocalEuler = Vector3.zero;
        // Diagonal the sheathed blade leans across the back, degrees off straight-up toward the OFF
        // (main-hand-opposite) shoulder — a natural baldric carry. Authored/persisted; owner felt-tunes.
        [SerializeField] private float _sheatheBladeDiagonalDeg = 28f;
        [SerializeField] private Vector3 _sheatheOffHandLocalPos   = new Vector3(0.12f, 0.06f, -0.17f);
        // AUTHORED CORRECTION (§4 sanctioned manual nudge, owner live felt-tune 2026-07-04 — manual=true,
        // never auto-overwritten): sheathed shield-on-back rotation. Base (0,90,12); owner Z+=180 →
        // (0,90,192) for the face-on-back read. Y+=180 is NOT baked here — ApplyGlobalWeaponYaw
        // composes the same universal flip weapons use (owner 2026-07-05).
        [SerializeField] private Vector3 _sheatheOffHandLocalEuler = new Vector3(0f, 90f, 192f);

        // Resolved attach targets + the DRAWN local transform, so the carry-state can move each prop
        // between its hand (drawn) and the back socket (sheathed) with no re-equip. _baseGripRot holds
        // the drawn WEAPON rotation; the off-hand keeps its own drawn rotation (it has no rig-axis grip).
        private Transform  _weaponHand;
        private Vector3    _weaponDrawnLocalPos;
        private Transform  _offHandHand;
        private Vector3    _offHandDrawnLocalPos;
        private Quaternion _offHandDrawnLocalRot = Quaternion.identity;
        private Transform  _backSocket;   // lazily created under Chest/Spine — the shared sheathe anchor

        // ── SWORD GRIP ORIENTATION (rig-relative) ────────────────────────────────────
        // THE FIX (task #36 follow-up): the grip POINT (handle below the crossguard) is
        // correct, but the blade DIRECTION was wrong — it lay across the torso. Cause:
        // the grip root was parented under the RightHand bone and its blade axis (prop
        // local +Y) was left aligned to the BONE's local +Y. On this rig the hand bone's
        // local axes do NOT world-align the way a generic prop frame assumes, so "blade
        // +Y" came out pointing sideways across the body instead of forward from the fist.
        //
        // We now build the grip-root's base rotation FROM the hand bone's own axes so the
        // blade extends along the way a fist naturally "points" when gripping a sword, and
        // the grip axis runs along the bone (palm→fingers). Which of the hand bone's local
        // axes is the "point" (forward-from-fist) vs the "grip" (along the bone) is
        // RIG-SPECIFIC, so both are EXPOSED below for an in-Inspector nudge without a
        // recompile. Defaults are the most plausible for a Humanoid RightHand (Unity's
        // Mecanim convention: the bone's local +Y runs down the forearm toward the
        // fingertips = the grip/point line; +Z is roughly the palm normal). Tune on the
        // real rig if the first pick reads off.

        // The hand-bone local axis the BLADE should extend along (forward from the fist).
        // Default +Y (along the finger line — a held blade continues the forearm/finger
        // direction). Flip sign or switch axis in the Inspector if the blade still reads
        // sideways/backward on this rig.
        [SerializeField] private Vector3 _handBladeAxis = new Vector3(0f, 1f, 0f);

        // The hand-bone local axis the weapon's GRIP/edge plane should align to (keeps the
        // flat of the blade oriented sanely — roughly the palm normal). Default +Z.
        [SerializeField] private Vector3 _handGripUpAxis = new Vector3(0f, 0f, 1f);

        // Final calibration nudge applied ON TOP of the rig-derived orientation, in the
        // grip root's local space (after it's been pointed down the hand axis). Lets the
        // owner perfect the read (e.g. blade forward-and-slightly-up from the fist) from
        // the Inspector against the real hand bone — no recompile. The idle/combat hold
        // offset composes on top of THIS (see ApplyHoldPose), so the hold tilt stays
        // relative to the corrected ready orientation (no double-apply).
        [SerializeField] private Vector3 _swordGripEuler = new Vector3(-25f, 90f, 0f);  // owner felt-test: "model" (sword prop) Y+90 (was (-25,0,0))

        // WO-435: per-archetype calibration nudges, the staff/mace/etc. equivalents of
        // _swordGripEuler. ALL melee now run the same rig-aware grip path (bounds-derived
        // SeatByHandle + ComputeMeleeGripRotation); these are the additive manual-correction
        // nudges applied ON TOP in the corrected local frame, treated as CANON (never auto-
        // overwritten, per WEAPON_ARMOR_ORIENT_LOGIC §4). Defaulted to ZERO so generalizing
        // the path does NOT regress the existing look — only the sword keeps its proven -25°
        // nudge above. Inspector-exposed for tuning each family against the real rig with no
        // recompile. (Dagger reuses _swordGripEuler — same bladed archetype.)
        // RC5 FIX (2026-07-04): these were ZERO, so an un-corrected staff/wand/axe/mace inherited the
        // bone's raw local axes and read SIDEWAYS across the torso (only sword/dagger had the proven
        // nudge). The SHARED rig-hand-axis correction on this rig (KnightV3 CC_Base RightHand) is
        // Y=+90 — it appears in EVERY felt-approved melee correction (sword_A via _swordGripEuler's
        // +90, sword_G offset (0,90,0), the old axe_A offset (-25,90,0)). So the SANE DEFAULT that
        // makes a NEW weapon inherit a working "points out of the fist" grip is +90 Y, plus the -25 X
        // forward-lean only for BLADED heads (sword/dagger/axe). "Corrections teach the default"
        // (WEAPON_ARMOR_ORIENT_LOGIC §4-step-5): axe now carries (-25,90,0) here and its redundant
        // offsets.json entry is REMOVED — net rotation for axe_A is IDENTICAL (was default(0)∘offset
        // (-25,90,0); now default(-25,90,0)∘no-offset), so no regression, but a new axe works.
        // NEEDS-CAPTURE: staff/wand/mace exact forward-lean (0 vs -25) confirmed by the build capture;
        // 0 tilt is the safe neutral for a symmetric shaft. These remain Inspector-tunable + CANON
        // (never auto-overwritten). Any per-mesh delta still layers on top via offsets.json.
        [SerializeField] private Vector3 _staffGripEuler = new Vector3(0f, 90f, 0f);
        [SerializeField] private Vector3 _wandGripEuler  = new Vector3(0f, 90f, 0f);
        [SerializeField] private Vector3 _axeGripEuler   = new Vector3(-25f, 90f, 0f);
        [SerializeField] private Vector3 _maceGripEuler  = new Vector3(0f, 90f, 0f);

        // Owner 2026-07-05: universal held-weapon Y flip (all families, hero + enemy).
        private const float WeaponGlobalYawDeg = 180f;

        /// <summary>Composes the global held-weapon Y correction onto a grip rotation.</summary>
        internal static Quaternion ApplyGlobalWeaponYaw(Quaternion rot)
            => rot * Quaternion.Euler(0f, WeaponGlobalYawDeg, 0f);

        // Cached base rotation for the current grip root (rig-derived + _swordGripEuler),
        // expressed in the hand bone's local space. ApplyHoldPose offsets from this.
        private Quaternion _baseGripRot = Quaternion.identity;

        private void Awake()
        {
            CacheRig();
            _loadout = GetComponent<GearLoadout>();
            // The ranger's bow is owned by HeroBowAttachment; don't double-attach.
            _deferBowToBowAttachment = GetComponent<HeroBowAttachment>() != null;
        }

        private void OnEnable()
        {
            // ORDER-INDEPENDENT SUBSCRIBE (BUG 1 fix): on the COMPANION body, BuildPlaceholder
            // adds the EquipmentController BEFORE the GearLoadout (the hero swapper does the
            // reverse). If we only ever subscribed here, a controller added first would resolve
            // _loadout == null, NEVER subscribe, and silently miss the BindOwnerClass->Refresh->
            // OnGearChanged that carries the companion's bow — so its weapon never attached.
            // EnsureLoadoutSubscribed re-resolves + (re)subscribes idempotently, and Update()
            // below keeps retrying until the loadout (and the Humanoid rig) come online.
            EnsureLoadoutSubscribed();
            // Always pull the latest local user settings before seating props (persistentDataPath
            // attachment-offsets.json wins over shipped defaults per id).
            AttachmentOffsetRegistry.Reload();
            EquipBestForHero();
        }

        // Idempotent: resolve the GearLoadout (it may be added AFTER this controller on the
        // companion) and subscribe exactly once. Returns true when subscribed to a live loadout.
        private bool _subscribed;
        private bool EnsureLoadoutSubscribed()
        {
            if (_loadout == null) _loadout = GetComponent<GearLoadout>();
            if (_loadout != null && !_subscribed)
            {
                _loadout.OnGearChanged += HandleGearChanged;
                _subscribed = true;
                FlowTrace.Step("Equip", $"subscribed to GearLoadout.OnGearChanged on '{name}'");
            }
            return _subscribed;
        }

        // RIG-READINESS RETRY (BUG 1 fix): the companion's Animator finishes Humanoid Rebind a
        // few frames AFTER BuildPlaceholder fires BindOwnerClass, so the first EquipBestForHero
        // sees !isHuman / null hand bones and skips (exactly why the archer's bow never showed).
        // Mirror HeroBowAttachment's short retry: poll until the loadout is subscribed AND the
        // weapon prop is up, then stop. Cheap, self-terminating; off once equipped.
        private int _attachRetries;
        private void LateAttachRetry()
        {
            if (PackageBakedGear) return;                // baked-gear hero has no props to attach — nothing to retry
            if (_attachRetries > 180) return;            // ~3s @60fps then give up quietly
            bool nowSubscribed = EnsureLoadoutSubscribed();
            CacheRig();
            bool rigReady = _animator != null && _animator.isHuman;
            bool needWeapon = _loadout != null && _loadout.EquippedWeapon != null && _currentWeaponProp == null;
            bool needOffHand = _loadout != null && _loadout.EquippedOffHand != null && _currentOffHandProp == null;
            if (nowSubscribed && rigReady && (needWeapon || needOffHand))
            {
                FlowTrace.Step("Equip", $"LateAttachRetry firing on '{name}' " +
                    $"(rigReady={rigReady} needWeapon={needWeapon} needOffHand={needOffHand} retry={_attachRetries})");
                EquipBestForHero();
            }
            _attachRetries++;
        }

        private void OnDisable()
        {
            if (_loadout != null && _subscribed) _loadout.OnGearChanged -= HandleGearChanged;
            _subscribed = false;
            // Release any open Addressables weapon handle so a Blink prefab never leaks
            // when the hero is disabled/destroyed (the load may even still be in flight).
            ReleaseWeaponHandle();
            _offHandGeneration++;          // reject any in-flight off-hand completion
            DestroyCurrentOffHand();
            ReleaseOffHandHandle();
        }

        private void HandleGearChanged() => EquipBestForHero();

        /// <summary>
        /// Re-seat the equipped main-hand + off-hand onto a NEW body's bones. Called by HeroArmorVisual
        /// when an armored Blink body swaps in: the props were seated on the now-HIDDEN base hand, and a
        /// Blink full-body set has slightly different rig proportions, so the VISIBLE armor hand sits
        /// elsewhere — the "shield hangs off the arm" symptom. Re-point the animator at the new body and
        /// re-equip so the props follow the visible hands. No magic offsets — the equip path resolves the
        /// hand by humanoid bone id on the new rig. No-op until the new rig is a ready Humanoid.
        /// </summary>
        public void ReseatForBody(GameObject body)
        {
            if (body == null) return;
            var anim = body.GetComponentInChildren<Animator>();
            if (anim == null || !anim.isHuman) return;   // need a humanoid rig to seat on bones
            _animator = anim;
            _cachedHeroHeightM = 0f;   // new rig proportions → re-measure for heldLength scale
            _backSocket = null;   // new body → the old chest-anchored sheathe socket is stale; re-create under the new chest
            FlowTrace.Step("Equip", $"ReseatForBody: re-seating equipped props onto '{body.name}' bones (animator='{anim.name}').");
            EquipBestForHero();
        }

        /// <summary>
        /// Re-reads the hero's currently equipped weapon from GearLoadout and shows the
        /// matching mesh. This is the hook into the EXISTING equip-change event — no new
        /// gear model. Safe to call repeatedly (idempotent on an unchanged id).
        /// </summary>
        public void EquipBestForHero()
        {
            AttachmentOffsetRegistry.Reload();
            if (_loadout == null) _loadout = GetComponent<GearLoadout>();
            // Pass the WeaponDef (not just the id) so the attach path can read prefabPath /
            // loadVia and resolve a Blink Addressable weapon — the data-driven equip.
            Equip(_loadout != null ? _loadout.EquippedWeapon : null);
            // OFF-HAND: attach (or detach) the shield/off-hand to the OFF hand on the SAME event.
            EquipOffHand(_loadout != null ? _loadout.EquippedOffHand : null);
        }

        /// <summary>
        /// Show the weapon mesh for <paramref name="weaponId"/> (an id from weapons.json),
        /// attaching it to the Humanoid hand bone with the mapped grip offset. Passing null
        /// or an empty id unequips. Destroys the previous prop first (no stacking).
        /// Resolves the WeaponDef from the catalog so the data-driven (Addressable) path is
        /// used when the def carries an Addressable prefabPath.
        /// </summary>
        public void Equip(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) { Equip((WeaponDef)null, null); return; }
            Equip(GearCatalog.FindWeapon(weaponId), weaponId);
        }

        /// <summary>
        /// Data-driven equip: attaches the weapon described by <paramref name="def"/>. When the
        /// def's prefab loads via Addressables (Blink gear) the prefab is loaded async and
        /// attached a frame later; otherwise the existing hardcoded Resources map is used. A
        /// null def unequips. Safe to call repeatedly (idempotent on an unchanged id).
        /// </summary>
        public void Equip(WeaponDef def)
        {
            Equip(def, def != null ? def.id : null);
        }

        // Core equip. <paramref name="def"/> may be null (e.g. an id with no catalog row, or a
        // bare-id call) — then we fall back to the keyword/Resources Resolve path on the id.
        private void Equip(WeaponDef def, string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) && def != null) weaponId = def.id;

            string ownerName = name;
            using var _ = FlowTrace.Enter("Equip", $"attach '{weaponId ?? "<null>"}' to '{ownerName}'");

            // PACKAGE de-dupe: the Paladin body bakes its own sword — do NOT attach a second KayKit mesh
            // (owner F8 "holding two swords"). Loadout still tracks the equipped weapon; only the visible
            // prop is suppressed. Legacy Tripo Knight (no marker) is unaffected.
            if (PackageBakedGear)
            {
                FlowTrace.Step("Equip",
                    $"PACKAGE baked-gear hero '{ownerName}' — SKIP weapon-mesh attach for '{weaponId ?? "<null>"}' " +
                    "(baked Paladin sword wins; de-dupes the second sword).");
                return;
            }

            // Idempotent: same weapon already shown -> nothing to do.
            if (string.Equals(_currentWeaponId, weaponId, System.StringComparison.OrdinalIgnoreCase)
                && _currentWeaponProp != null)
            {
                FlowTrace.Step("Equip", $"idempotent — '{weaponId}' already shown; no-op");
                return;
            }

            // New equip request — invalidate any in-flight async load + drop the old prop/handle.
            _equipGeneration++;
            DestroyCurrentWeapon();
            ReleaseWeaponHandle();
            _currentWeaponId = weaponId;

            if (string.IsNullOrEmpty(weaponId)) { FlowTrace.Step("Equip", "empty id -> unequip"); return; } // unequip

            WeaponVisual vis = Resolve(weaponId);
            if (vis == null) { FlowTrace.Fail("Equip", $"Resolve('{weaponId}') returned null — nothing to attach"); return; }
            FlowTrace.Step("Equip", $"resolved vis: mesh='{vis.mesh}' kind={vis.kind} leftHand={vis.leftHand} native={vis.native}");

            // The ranger's held bow is HeroBowAttachment's job — skip here to avoid two bows.
            // NOTE: this only applies to the HERO (which carries HeroBowAttachment). A COMPANION
            // archer has NO HeroBowAttachment, so _deferBowToBowAttachment is false and its bow is
            // attached HERE — that is the path BUG 1 needed working (its bow now seats like the hero's).
            if (_deferBowToBowAttachment && vis.mesh != null && vis.mesh.StartsWith("bow"))
            {
                FlowTrace.Step("Equip", "bow deferred to HeroBowAttachment (hero owns the held bow) -> skip");
                return;
            }

            CacheRig();
            if (_animator == null || !_animator.isHuman)
            {
                // Generic/invalid avatar OR the Humanoid rig hasn't finished Rebind yet (companion
                // attach-before-rebind). Skip now; Update()'s LateAttachRetry re-fires once the rig
                // reports Humanoid (mirrors HeroBowAttachment's retry). NOT a hard fail.
                FlowTrace.Warn("Equip", $"rig not Humanoid yet on '{ownerName}' " +
                    $"(animator={(_animator != null ? "present,!isHuman" : "null")}) — deferring to LateAttachRetry");
                return;
            }

            HumanBodyBones boneId = vis.leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;

            // ── INSTANTIATION-TIME ATTACH OVERRIDE (WO-510 slice 1) ───────────────────
            // An OPTIONAL rig-profiles.json may name the attach transform by hierarchy path
            // (rig-agnostic — we never rename the model's bones). The override is AUTHORITATIVE;
            // the humanoid avatar (below) is the FALLBACK. ZERO behaviour change when no profile
            // exists. heroRoot = the GameObject owning the Animator avatar we seat on; rigId =
            // its name (the model/prefab name key, e.g. "Knight"). A dead override path SCREAMS
            // (FlowTrace.Fail) then falls through to the avatar — never silently swallowed.
            GameObject heroRoot = _animator.gameObject;
            string rigId = heroRoot.name;
            Transform hand = null;
            if (RigAttachmentRegistry.TryResolve(heroRoot, rigId, vis.leftHand, out var overrideAnchor, out var how))
            {
                hand = overrideAnchor;
                FlowTrace.Step("Offset", $"attach rig={rigId} hand={(vis.leftHand ? "L" : "R")} -> '{hand.name}' (via json-override)");
            }
            else
            {
                if (how != null && how.StartsWith("missing"))
                    FlowTrace.Fail("Offset", $"attach rig={rigId} hand={(vis.leftHand ? "L" : "R")} override path absent in model ({how}); falling back to avatar");

                hand = FlowTrace.Try("Equip", $"GetBoneTransform({boneId})",
                    () => _animator.GetBoneTransform(boneId), null);
                FlowTrace.Step("Offset", $"attach rig={rigId} hand={(vis.leftHand ? "L" : "R")} -> '{(hand != null ? hand.name : "<null>")}' (via avatar)");
            }

            if (hand == null)
            {
                // LOUD: a Humanoid rig with a missing hand bone is exactly why the companion bow
                // would not show. Fail-level so it rolls up in the capture, not a quiet warning.
                FlowTrace.Fail("Equip", $"Humanoid rig on '{ownerName}' has NO {boneId} bone — " +
                    $"weapon '{weaponId}' NOT attached (this is the null-bone BUG 1 cause if it fires).");
                return;
            }
            FlowTrace.Step("Equip", $"hand bone resolved: {boneId} -> '{hand.name}'");

            // ── DATA-DRIVEN PREFAB RESOLUTION (WO-Item, Blink Addressables) ───────────
            // If the equipped def loads via Addressables (Blink gear: prefabPath is an
            // address like "gear/weapon/Sword1h_01"), load it async and attach on completion.
            // Otherwise the EXISTING hardcoded Tripo/Resources map runs unchanged.
            if (LoadsViaAddressable(def))
            {
                FlowTrace.Step("Equip", $"branch: ADDRESSABLE ('{def.prefabPath}')");
                BeginAddressableEquip(def, vis, hand, weaponId, _equipGeneration);
                return;
            }

            FlowTrace.Step("Equip", $"branch: RESOURCES map (mesh='{vis.mesh}')");
            GameObject prop = LoadWeaponMesh(vis.mesh) ?? BuildFallbackPrimitive(vis);
            if (prop == null) { FlowTrace.Fail("Equip", $"prop load+fallback both null for mesh '{vis.mesh}'"); return; }

            AttachLoadedProp(prop, vis, hand, weaponId);
        }

        // ── Addressable weapon load (Blink gear) ─────────────────────────────────────
        // True when the def's prefab must be loaded via Addressables: an explicit
        // loadVia=="addressable" OR a prefabPath that uses the shared "gear/" address scheme
        // (BlinkAddressableMarker / BlinkGearSource). Legacy/Tripo rows (null/empty) return false
        // → the existing Resources map runs (no behaviour change for current ids).
        private static bool LoadsViaAddressable(WeaponDef def)
        {
            if (def == null) return false;
            if (!string.IsNullOrEmpty(def.loadVia) &&
                def.loadVia.Equals("addressable", System.StringComparison.OrdinalIgnoreCase))
                return true;
            return !string.IsNullOrEmpty(def.prefabPath) &&
                   def.prefabPath.StartsWith("gear/", System.StringComparison.OrdinalIgnoreCase);
        }

        // Kick off the async Addressables load of the Blink weapon prefab. Attaches on
        // completion (the weapon appears a frame later — fine). The handle is stored +
        // released on the next swap/unequip/OnDisable. A failed/invalid handle is GUARDED:
        // FlowTrace.Warn + fall back to the hardcoded Resources map (or its primitive) so the
        // hero is NEVER left unarmed because a Blink prefab didn't resolve (WO-425 invariant).
        private void BeginAddressableEquip(
            WeaponDef def, WeaponVisual vis, Transform hand, string weaponId, int generation)
        {
            string address = def.prefabPath;
            FlowTrace.Step("Gear", $"Addressable equip begin: id='{weaponId}' address='{address}'");

            AsyncOperationHandle<GameObject> handle;
            try
            {
                handle = Addressables.LoadAssetAsync<GameObject>(address);
            }
            catch (System.Exception ex)
            {
                FlowTrace.Fail("Gear", $"Addressable load threw for '{address}': {ex.Message} — " +
                                       "falling back to Resources map (hero stays armed).");
                FallbackResourcesAttach(vis, hand, weaponId);
                return;
            }

            _weaponHandle = handle;
            _weaponHandleOpen = true;

            handle.Completed += op =>
            {
                // Stale: the player swapped weapons (or unequipped/disabled) while this load was
                // in flight — a newer equip already owns the slot. Release THIS handle and bail so
                // we never attach a ghost prop from an out-of-date request.
                if (generation != _equipGeneration)
                {
                    // BUG 2 FIX: do NOT call Addressables.Release(op) synchronously here — we are
                    // INSIDE the SDK's OnHandleCompleted dispatch over this same handle. A re-entrant
                    // Release invalidates the handle the SDK is still reading (handle.Status), which
                    // throws "Attempting to use an invalid operation handle". Defer to the next frame.
                    if (_weaponHandle.Equals(op)) { _weaponHandle = default; _weaponHandleOpen = false; }
                    DeferRelease(op);
                    return;
                }

                if (!op.IsValid() || op.Status != AsyncOperationStatus.Succeeded || op.Result == null)
                {
                    FlowTrace.Fail("Gear", $"Addressable load FAILED for '{address}' " +
                        $"(status={op.Status}) — falling back to Resources map (hero stays armed).");
                    ReleaseWeaponHandle();
                    FallbackResourcesAttach(vis, hand, weaponId);
                    return;
                }

                // Blink prefab is authored grip-at-origin + oriented → seat as NATIVE. Copy the
                // preset so we never flip `native` on the shared cached IdMap instance.
                var nativeVis = CopyOf(vis);
                nativeVis.native = true;
                // GUARDED Instantiate (TGVRU): a bad/destroyed-mid-frame prefab must not throw or
                // silently attach nothing — Guard.Try, null-check, Fail-loud, then fall back to
                // the Resources map so the hero is never left unarmed by an Addressable hiccup.
                GameObject prop = null;
                Guard.Try("Gear", $"instantiate addressable weapon '{address}'",
                    () => prop = Instantiate(op.Result));
                if (prop == null)
                {
                    FlowTrace.Fail("Gear", $"Addressable Instantiate returned null for '{address}' " +
                        $"(id='{weaponId}') — falling back to Resources map (hero stays armed).");
                    ReleaseWeaponHandle();
                    FallbackResourcesAttach(vis, hand, weaponId);
                    return;
                }
                AttachLoadedProp(prop, nativeVis, hand, weaponId);
                FlowTrace.Step("Gear", $"Addressable equip attached: id='{weaponId}' address='{address}'");
            };
        }

        // Armed-hero fallback: a Blink Addressable resolve failed — attach via the hardcoded
        // Resources map (or the tinted primitive) so the hero is never unarmed. Re-checks the
        // generation guard caller-side; here we just attach with the already-resolved vis.
        private void FallbackResourcesAttach(WeaponVisual vis, Transform hand, string weaponId)
        {
            // The Resources map vis is NON-native (legacy normalize/hilt path). Copy + clear the
            // native flag so we never mutate the shared cached IdMap preset.
            var fb = CopyOf(vis);
            fb.native = false;
            GameObject prop = LoadWeaponMesh(fb.mesh) ?? BuildFallbackPrimitive(fb);
            if (prop == null) return;
            AttachLoadedProp(prop, fb, hand, weaponId);
        }

        // Shared attach + grip/orient (§4) for an ALREADY-LOADED prop — used by both the
        // synchronous Resources path and the async Addressables completion. Strips physics,
        // seats via the grip root (NATIVE = trust pivot; else bounds-normalize + hilt-infer),
        // parents to the hand, and computes the rig-aware base grip rotation + hold pose.
        private void AttachLoadedProp(GameObject prop, WeaponVisual vis, Transform hand, string weaponId)
        {
            using var _ = FlowTrace.Enter("Equip", $"AttachLoadedProp '{weaponId}' -> '{hand.name}' (native={vis.native} kind={vis.kind})");
            prop.name = PropName;
            // Cosmetic only — strip physics/colliders a prefab might carry.
            foreach (var c in prop.GetComponentsInChildren<Collider>(true)) if (c != null) Destroy(c);
            foreach (var rb in prop.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) Destroy(rb);

            // ── WEAPON MATERIAL RECOVERY (deal-breaker fix 2026-07-16 "knight starts with no weapon") ──
            // The knight_starter prop (sword_A / Sword1h_01) carries material 'LowPolyWeaponMegaPack' on the
            // BUILT-IN STANDARD shader (that pack is gitignored; Standard is a Built-in-RP shader). Under URP a
            // Standard-shader material renders MAGENTA in-editor and is STRIPPED from the built player (its
            // shader resolves to Hidden/InternalErrorShader -> invisible/pink), so the weapon attaches + passes
            // VerifyWeaponRendersNow (enabled renderer + mesh + parented) yet is NOT VISIBLE in the hand — the
            // owner's "no weapon". MagentaGuard recovers exactly this class, but it only sweeps on scene-LOAD and
            // this prop is instantiated at RUNTIME (equip) AFTER that sweep, so the guard never reaches it. Recover
            // the prop's broken materials HERE, at attach time (mirrors MagentaGuard's fresh-URP/Lit-assigned-to-
            // renderer pattern — a fresh instance assigned into sharedMaterials STICKS in the build; an in-place
            // shader mutation of the shared asset does not). Idempotent: an already-URP prop is left untouched.
            RecoverWeaponMaterialsToUrp(prop, weaponId);

            // Seat via a grip root.
            //  • WO-478 (default): NATIVE props (Blink grip-at-origin, e.g. knight_starter/sword_A)
            //    trust SeatNative — scale to heldLength, preserve authored pivot/orientation.
            //    Geometry inference is reserved for non-native Tripo/KayKit FBX (sword_D/F/G, staff_*).
            //  • DEPRECATED (ff.weapongripinfer): when ON, native melee ALSO runs the legacy
            //    NormalizeInto + SeatHiltLowerHalf + ComputeMeleeGripRotation path (superseded
            //    WO-435 "BUG 2 FIX" — see WORK_ORDER_478_weapon_grip_trust_native_pivot.md).
            // ── OFFSET RESOLUTION (WO-551: geometry-first, offset = nudge-on-top) ─────
            // GEOMETRY IS THE DEFAULT for all conforming melee. We ALWAYS true the prop
            // (NormalizeInto: longest axis -> +Y, narrowest -> +X) and, for melee, seat the grip
            // by geometry (SeatByHandle: hilt-forward). The Offset Forge offset is then a small
            // CALIBRATION NUDGE applied ON TOP of that trued+seated frame — NOT a replacement
            // that skips geometry. (The b773176d `seatNativeAuthored` bypass made the manual
            // offset the AUTHORITY — backwards vs the owner principle "trust the geometry" — and
            // dialed against the RAW pivot, so it never reproduced in-game = "handle still wrong".)
            // An all-zero entry == pure geometry. A weapon that genuinely breaks the
            // wide-Y/narrow-XZ pattern can opt OUT of geometry per-entry with "fullOverride":true
            // (the EXCEPTION, native-only). Key = mesh name (Forge save-id, e.g. "sword_A") then id.
            string offsetKey = !string.IsNullOrEmpty(vis.mesh) ? vis.mesh : weaponId;
            bool hasOffset = AttachmentOffsetRegistry.TryGetOffset(offsetKey, out var fo) ||
                             (offsetKey != weaponId && AttachmentOffsetRegistry.TryGetOffset(weaponId, out fo));
            bool meleeSeat = IsMelee(vis.kind);
            // EXCEPTION (opt-in, native-only): a non-conforming prop bypasses geometry and
            // reproduces the Forge raw-pivot frame exactly (legacy replacement). Default false,
            // so a normal authored entry NEVER skips geometry — it only adds a nudge on top.
            // OWNER CONVENTION (WO-577, 2026-06-28): fullOverride now means "author from the
            // 100%-VERTICAL baseline (geometry) + a saved DELTA", written by the in-game Seating
            // Editor — NOT the old raw-pivot/SeatNative bypass (which the WO-551 notes flagged as
            // the backwards approach that never reproduced in-game). Dropped the `&& vis.native`
            // gate so a vertical-delta can be authored for ANY weapon (Tripo/KayKit included).
            // SAFE: only entries with fo.fullOverride==true take this path, and none existed
            // before the editor — the DEFAULT geometry+nudge path (WO-551) is byte-for-byte intact.
            bool fullOverride = hasOffset && fo.fullOverride;

            var gripRoot = new GameObject(PropName);
            // WO-478: native melee trusts authored pivot unless ff.weapongripinfer restores inference.
            bool trustNativePivot = vis.native && !fullOverride &&
                (!meleeSeat || !FeatureFlags.WeaponGripInfer);
            float heldLen = ProportionalHeldLength(vis.heldLength);
            FlowTrace.Step("Equip",
                $"heldLength '{weaponId}' kind={vis.kind}: archetype={vis.heldLength:0.###}m " +
                $"proportional={heldLen:0.###}m hero={ResolveHeroHeightM():0.###}m");
            if (trustNativePivot)
            {
                FlowTrace.Step("Equip", meleeSeat
                    ? "seat: NATIVE melee (WO-478 trust grip-at-origin + scale)"
                    : "seat: NATIVE (trust authored grip-at-origin, scale-only)");
                SeatNative(prop, gripRoot.transform, heldLen);
            }
            else
            {
                // DEPRECATED geometry inference — ff.weapongripinfer for native melee, default for
                // non-native Tripo/KayKit FBX. Ref: WORK_ORDER_478_weapon_grip_trust_native_pivot.md
                FlowTrace.Step("Equip", meleeSeat
                    ? (vis.native
                        ? "seat: DEPRECATED GEOMETRY (ff.weapongripinfer) — NormalizeInto + SeatHiltLowerHalf"
                        : "seat: GEOMETRY — NormalizeInto (longest->+Y) + SeatHiltLowerHalf (hilt=lower half, blade +Y)")
                    : "seat: GEOMETRY — NormalizeInto (bounds-true)");
                NormalizeInto(prop, gripRoot.transform, heldLen, ResolveHiltFromKind(vis.kind));
                if (meleeSeat)
                {
                    FlowTrace.Try("Equip", "SeatHiltLowerHalf", () => SeatHiltLowerHalf(prop, gripRoot.transform));
                    FlowTrace.Step("Equip", $"trued+seated: grip-shift localY={prop.transform.localPosition.y:0.###} (geometry hilt-lower-half{(fullOverride ? ", vertical-delta" : "")} infer={FeatureFlags.WeaponGripInfer})");
                }
            }

            gripRoot.transform.SetParent(hand, false);
            _weaponParentCompensate = true;
            _weaponAuthoredScale = 1f;   // no offset yet — reset; the offset branches below record fo.scale
            CompensateParentScale(gripRoot.transform);
            gripRoot.transform.localPosition = vis.gripPos;

            // Hold state: store the base grip euler so the idle<->combat pose can offset
            // from it; apply the current pose immediately.
            _gripRoot = gripRoot.transform;
            _baseGripEuler = vis.gripEuler;
            _currentWeaponProp = gripRoot;

            // Capture the attach inputs the in-game Seating Editor (WO-577) needs to live-preview
            // + reproduce this seat. Cheap struct copies; consumed only when an editor opens.
            _currentWeaponMeshKey    = offsetKey;
            _currentWeaponKind       = vis.kind;
            _currentWeaponMelee      = meleeSeat;
            _currentWeaponHeldLength = heldLen;
            _currentWeaponGripPos    = vis.gripPos;
            _currentWeaponGripEuler  = vis.gripEuler;
            _currentWeaponNative     = vis.native;

            // Base orientation:
            //  • ALL melee (sword/dagger/axe/hammer/staff/wand — WO-435): build the grip-root
            //    rotation FROM the hand bone's own axes so the primary axis extends forward from
            //    the fist (not across the torso). The prop's primary/grip line is local +Y
            //    (NormalizeInto + SeatByHandle put the longest axis there); we point that down the
            //    hand's blade axis and keep the flat aligned to the hand's grip-up axis, then add
            //    the per-archetype calibration nudge (sword/dagger -> _swordGripEuler, default -25;
            //    others default 0 so the path generalizes WITHOUT regressing the existing look).
            //    Previously staff/wand/axe/hammer used identity gripEuler + NO rig rotation, so they
            //    inherited the bone's local axes and read sideways across the body — this is the fix.
            //  • Bow/shield: keep the proven per-weapon gripEuler (already seated correctly by
            //    their own NormalizeInto + preset euler).
            // WO-478: native melee keeps the prefab frame + per-archetype calibration nudge only.
            // DEPRECATED (ff.weapongripinfer): native melee uses ComputeMeleeGripRotation like Tripo FBX.
            if (fullOverride)
            {
                _baseGripEuler = fo.eulerRot;
                _baseGripRot = Quaternion.Euler(fo.eulerRot);
            }
            else if (trustNativePivot && meleeSeat)
            {
                _baseGripRot = Quaternion.Euler(vis.gripEuler) * Quaternion.Euler(MeleeGripNudge(vis.kind));
                _baseGripEuler = _baseGripRot.eulerAngles;
            }
            else if (IsMelee(vis.kind))
                _baseGripRot = ComputeMeleeGripRotation(vis.kind);
            else
                _baseGripRot = Quaternion.Euler(_baseGripEuler);

            FlowTrace.Step("Equip", $"attached '{weaponId}' on '{name}': gripPos={vis.gripPos} " +
                $"baseEuler={_baseGripRot.eulerAngles} kind={vis.kind} native={vis.native} " +
                $"trustNative={trustNativePivot} infer={FeatureFlags.WeaponGripInfer}");

            // ── OFFSET FORGE NUDGE (WO-551: applied ON TOP of geometry) ──────────────
            // The authored offset is a CALIBRATION NUDGE relative to the trued+seated runtime
            // frame, NOT a replacement: COMPOSE the rotation onto the geometric grip, ADD the
            // position, MULTIPLY the scale. So the pipeline is true -> seat-by-handle -> nudge.
            // An all-zero entry is a no-op == pure geometry (the conforming-sword case). The
            // fullOverride EXCEPTION (resolved above) instead REPLACED the frame; here it applies
            // only its residual pos/scale on the raw-pivot seat. The key is the weapon's mesh name
            // (e.g. "sword_A", what the Forge defaults its save-id to) with a fallback to the id.
            if (fullOverride)
            {
                // OVERRIDE: reproduce the Seating-Editor preview EXACTLY (raw pivot pose). Scale
                // composes through the ONE shared seam (CompensateParentScale = comp * authored):
                // WYSIWYG break proven 2026-07-07: preview lacked compensate (hand lossy 1.666) —
                // owner-dialed 0.46 rendered 0.276 at boot. Preview + attach + hold-pose now all
                // render ParentScaleCompensation(parent) * fo.scale, so the approved size persists.
                gripRoot.transform.localPosition = vis.gripPos + fo.pos;
                _weaponAuthoredScale = fo.scale > 0f ? fo.scale : 1f;
                CompensateParentScale(gripRoot.transform, _weaponAuthoredScale);
                FlowTrace.Step("Offset", $"OVERRIDE '{offsetKey}': raw-pivot pos={fo.pos} rot={fo.eulerRot} scale={fo.scale:0.###}");
            }
            else if (hasOffset)
            {
                // NUDGE on top of geometry: +pos, *rot (in the seated local frame), *scale.
                bool nudged = fo.pos != Vector3.zero || fo.eulerRot != Vector3.zero ||
                              (fo.scale > 0f && Mathf.Abs(fo.scale - 1f) > 1e-4f);
                gripRoot.transform.localPosition = vis.gripPos + fo.pos;
                _baseGripRot = _baseGripRot * Quaternion.Euler(fo.eulerRot);
                _baseGripEuler = _baseGripRot.eulerAngles;
                if (fo.scale > 0f && Mathf.Abs(fo.scale - 1f) > 1e-4f)
                {
                    // Record the authored multiplier so ApplyHoldPose's re-parent compensate
                    // (CompensateParentScale) re-composes comp * authored instead of wiping it.
                    _weaponAuthoredScale = fo.scale;
                    gripRoot.transform.localScale = gripRoot.transform.localScale * fo.scale;
                }
                FlowTrace.Step("Offset", nudged
                    ? $"NUDGE '{offsetKey}' on geometry: +pos={fo.pos} *rot={fo.eulerRot} *scale={fo.scale:0.###}"
                    : $"offset '{offsetKey}' is all-zero — pure geometry (no nudge).");
            }
            else
            {
                FlowTrace.Step("Offset", trustNativePivot
                    ? $"no offset stored for '{offsetKey}' — native pivot kept (WO-478)."
                    : $"no offset stored for '{offsetKey}' — pure geometry grip kept.");
            }

            _baseGripRot = ApplyGlobalWeaponYaw(_baseGripRot);
            _baseGripEuler = _baseGripRot.eulerAngles;

            LogGripSeatDiagnostics(prop, gripRoot.transform, hand, weaponId,
                trustNativePivot ? "WO-478-native" : "geometry-infer");

            // RENDER-VERIFY + ROLLBACK (TGVRU, owner directive 2026-06-19: "anything that renders
            // can be broken — check render==true and roll back the error"). The prop can load +
            // attach but be INVISIBLE (no enabled Renderer / no mesh) or SEATED WRONG (the grip
            // root never landed under the hand bone). PROVE it renders + is parented under the
            // resolved hand BEFORE we leave it on the hero; on fail, destroy the half-attached prop
            // and clear the slot so no stray/invisible weapon is left behind (the never-left-broken
            // contract — the failure self-reports to the break-log instead of reaching the player).
            // MUST run while the prop is still parented to the hand (verify asserts that seat) — only
            // AFTER it passes do we record the draw target + apply the carry state (which may reparent
            // the prop onto the back sheathe socket when out of combat).
            if (!VerifyWeaponRendersNow(gripRoot, hand, weaponId))
            {
                RollbackWeaponProp(gripRoot,
                    $"render-verify failed for weapon '{weaponId}' (no visible renderer or not parented to '{hand.name}')");
                return;
            }

            // Record the DRAWN target (hand + final local pos); _baseGripRot already holds the drawn
            // rotation. ApplyHoldPose now PLACES the prop by combat state: on the hand in combat, on
            // the back socket (sheathed) out of combat — so the hand grip only shows where it seats right.
            _weaponHand = hand;
            _weaponDrawnLocalPos = gripRoot.transform.localPosition;
            ApplyHoldPose();
        }

        // RENDER-VERIFY (synchronous, no camera/scene dependency): the attached weapon prop MUST
        // have >=1 ENABLED Renderer carrying a sharedMesh AND its grip root MUST be parented under
        // the resolved hand bone. Traces the exact counts so a capture splits "no visible mesh" vs
        // "wrong/unparented seat" with zero guessing. Returns false => caller rolls back + unequips.
        private bool VerifyWeaponRendersNow(GameObject prop, Transform handBone, string weaponId)
        {
            if (prop == null)
            {
                FlowTrace.Fail("Equip", $"VerifyWeaponRenders: weapon '{weaponId}' prop is null.");
                return false;
            }

            int total = 0, enabledRen = 0, withMesh = 0;
            foreach (var r in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                total++;
                if (r.enabled) enabledRen++;
                // A SkinnedMeshRenderer exposes sharedMesh; MeshRenderer's mesh lives on a sibling
                // MeshFilter. Treat either source as "has a mesh".
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) mesh = mf.sharedMesh; }
                if (mesh != null) withMesh++;
            }

            bool renders = enabledRen > 0 && withMesh > 0;
            bool seated = prop.transform.parent == handBone;

            FlowTrace.Step("Equip",
                $"VerifyWeaponRenders weapon='{weaponId}' on '{name}': renderers total={total} enabled={enabledRen} " +
                $"withMesh={withMesh}; gripParent='{(prop.transform.parent != null ? prop.transform.parent.name : "<null>")}' " +
                $"expectedHand='{handBone.name}' => renders={renders} seated={seated}");

            if (!renders || !seated)
            {
                FlowTrace.Fail("Equip",
                    $"VerifyWeaponRenders FAILED weapon='{weaponId}' on '{name}': renders={renders} " +
                    $"(enabled={enabledRen}, withMesh={withMesh}) seated={seated} " +
                    $"(gripParent='{(prop.transform.parent != null ? prop.transform.parent.name : "<null>")}', expected='{handBone.name}').");
                return false;
            }
            return true;
        }

        // ROLL BACK a half-attached weapon: destroy the grip root (and its loaded prop child) and
        // clear the current-weapon slot so no stray/invisible weapon is left on the hand. Never
        // behind the FlowTrace toggle — control-flow safety always runs. The hero ends UNARMED but
        // CLEAN (no ghost prop) — and the Fail above self-reports why so it can be fixed at root.
        private void RollbackWeaponProp(GameObject gripRoot, string reason)
        {
            FlowTrace.Fail("Equip", $"RollbackWeaponProp on '{name}': {reason} — destroying half-attached prop.");
            if (_currentWeaponProp == gripRoot) _currentWeaponProp = null;
            if (_gripRoot != null && gripRoot != null && _gripRoot == gripRoot.transform) _gripRoot = null;
            if (gripRoot != null) Destroy(gripRoot);
            ReleaseWeaponHandle();
        }

        // Release the held Addressables weapon handle (no-op if none open). ONE owner of the
        // handle — called on every swap / unequip / OnDisable so a Blink prefab never leaks.
        private void ReleaseWeaponHandle()
        {
            if (!_weaponHandleOpen) return;
            _weaponHandleOpen = false;
            if (_weaponHandle.IsValid())
                Addressables.Release(_weaponHandle);
            _weaponHandle = default;
        }

        // BUG 2: release a handle ONE FRAME LATER, off the SDK's OnHandleCompleted dispatch. Calling
        // Addressables.Release(op) synchronously inside the Completed delegate is re-entrant — the SDK
        // is still mid-dispatch over that handle and then reads handle.Status on an already-released
        // (invalid) handle. Deferring a frame lets the dispatch finish before we free it.
        private void DeferRelease(AsyncOperationHandle<GameObject> op)
        {
            if (!isActiveAndEnabled)
            {
                if (op.IsValid()) Addressables.Release(op);
                return;
            }
            StartCoroutine(ReleaseNextFrame(op));
        }

        private static IEnumerator ReleaseNextFrame(AsyncOperationHandle<GameObject> op)
        {
            yield return null;
            if (op.IsValid()) Addressables.Release(op);
        }

        // ── SWORD GRIP-POINT INFERENCE — ⚠ RETIRED 2026-07-04 (RC2) ──────────────────
        // NO LONGER CALLED. Kept only as reference for why the single-path seat exists.
        // This branched on a crossguard width-spike vs a bottom-16% fallback and could FLIP
        // the weapon 180° based on which end read wider — non-deterministic per weapon, and it
        // degraded to a blind fallback in a build (unreadable FBX verts) = the editor≠build bug.
        // ALL melee now seat via the deterministic, never-flipping SeatHiltLowerHalf (below).
        //
        // Owner's rule (asset-attachment-inference-engine, task #36):
        //   1. Longest axis -> Y (NormalizeInto already did this; we work in prop-local Y).
        //   2. Find the HILT = a WIDTH SPIKE: bin vertices along Y, measure the X/Z cross-
        //      section extent per bin. Profile: thin blade -> WIDE crossguard flare -> thin
        //      grip. The flare locates the blade/handle boundary.
        //   3. Grip = centre of the HANDLE segment (between the hilt spike and the pommel
        //      end, on the SHORT side — the blade is the long side). Re-seat so that grip
        //      point sits at the origin, blade pointing +Y (outward).
        //   4. Fallback: no clear spike (glaive / flat profile) -> grip the bottom ~16% of
        //      the length near the pommel.
        // Works on prop-LOCAL coordinates (relative to `parent`, the gripRoot) so it is
        // independent of the mesh's own pivot/scale.
        // DEPRECATED (WO-478): superseded by SeatHiltLowerHalf for geometry inference; retained
        // only for reference — not called on the default attach path. Enable ff.weapongripinfer
        // to use the legacy inference stack (SeatHiltLowerHalf, not this method).
        private static void SeatByHandle(GameObject prop, Transform parent)
        {
            if (!TryLocalBounds(prop, parent, out Bounds b)) return;
            float yMin = b.center.y - b.extents.y;
            float yMax = b.center.y + b.extents.y;
            float length = yMax - yMin;
            if (length < 1e-4f) return;

            // Sample mesh vertices into Y-bins; per bin track max |x| and |z| (half-width).
            const int Bins = 48;
            var widthHi = new float[Bins];       // max cross half-extent per bin
            var hit = new bool[Bins];
            CollectWidthProfile(prop, parent, yMin, length, Bins, widthHi, hit);

            // Locate the hilt: the bin with the largest width spike. Compare against the
            // median width to decide whether the spike is "real" (a crossguard) or the
            // profile is basically flat (no clear hilt -> fallback).
            float median = MedianOfHit(widthHi, hit);
            int spikeBin = -1; float spikeW = 0f;
            for (int i = 0; i < Bins; i++)
            {
                if (!hit[i]) continue;
                if (widthHi[i] > spikeW) { spikeW = widthHi[i]; spikeBin = i; }
            }

            // grip point in prop-local Y (relative to parent origin).
            float gripY;
            bool bladePointsPositiveY;
            float binH = length / Bins;
            bool clearSpike = spikeBin >= 0 && median > 1e-5f && spikeW >= median * 1.6f;
            FlowTrace.Step("Equip", $"SeatByHandle DEPRECATED: clearSpike={clearSpike} spikeBin={spikeBin} median={median:0.###} spikeW={spikeW:0.###}");

            if (clearSpike)
            {
                // The handle is the SHORTER segment on one side of the spike; the blade is
                // the longer side. Distances from the spike to each end:
                float spikeY = yMin + (spikeBin + 0.5f) * binH;
                float toMin = spikeY - yMin;     // length of the segment below the spike
                float toMax = yMax - spikeY;     // length of the segment above the spike
                if (toMin <= toMax)
                {
                    // handle is below the spike -> blade is above (+Y). Grip = centre of
                    // [yMin .. spikeY].
                    gripY = (yMin + spikeY) * 0.5f;
                    bladePointsPositiveY = true;
                }
                else
                {
                    // handle is above the spike -> blade is below; we'll flip so blade -> +Y.
                    gripY = (spikeY + yMax) * 0.5f;
                    bladePointsPositiveY = false;
                }
            }
            else
            {
                // FALLBACK: no clear crossguard. Grip the bottom ~16% near one end (treat the
                // narrower-tipped end as the pommel). Pick the end whose extreme bin is
                // narrower as the BLADE TIP, so the pommel/grip is the opposite (wider) end.
                const float HandleFrac = 0.16f;
                float wLow = FirstHitWidth(widthHi, hit, false);   // width near yMin
                float wHigh = FirstHitWidth(widthHi, hit, true);   // width near yMax
                bool pommelAtMin = wLow >= wHigh; // wider end = pommel/grip side
                if (pommelAtMin)
                {
                    gripY = yMin + length * HandleFrac * 0.5f;
                    bladePointsPositiveY = true;
                }
                else
                {
                    gripY = yMax - length * HandleFrac * 0.5f;
                    bladePointsPositiveY = false;
                }
            }

            // 1) Shift so the grip point sits at the parent origin (hand bone).
            Vector3 lp = prop.transform.localPosition;
            lp.y -= gripY;
            prop.transform.localPosition = lp;

            // 2) Ensure the BLADE points +Y (outward from the hand). If the blade is on the
            //    -Y side, flip 180° about local X — this rotates about the grip point because
            //    the grip is now at the origin.
            if (!bladePointsPositiveY)
            {
                prop.transform.localRotation =
                    Quaternion.AngleAxis(180f, Vector3.right) * prop.transform.localRotation;
                prop.transform.localPosition =
                    Quaternion.AngleAxis(180f, Vector3.right) * prop.transform.localPosition;
            }
        }

        // ── HILT-LOWER-HALF SEAT (WO-577 owner convention, 2026-06-28) ───────────────
        // From the 100%-vertical baseline (NormalizeInto: longest axis -> +Y, bounds-centre at
        // origin), the owner's FIXED rule is: the HILT is the LOWER HALF (-Y), the blade points
        // UP (+Y), and the hand grips the hilt on that lower half. This removes SeatByHandle's
        // which-end-is-the-hilt ambiguity (no flip): the grip is always in the bottom portion.
        // Default grip = ~18% up from the bottom (hilt centre near the pommel); a clear width
        // spike (crossguard) WITHIN the lower half refines the exact grip Y. Prop-LOCAL coords
        // relative to <paramref name="parent"/> (the grip root). Used by the vertical-authoring /
        // fullOverride path + the in-game Seating Editor preview; the default path keeps SeatByHandle.
        private static void SeatHiltLowerHalf(GameObject prop, Transform parent)
        {
            if (!TryLocalBounds(prop, parent, out Bounds b)) return;
            float yMin = b.center.y - b.extents.y;
            float yMax = b.center.y + b.extents.y;
            float length = yMax - yMin;
            if (length < 1e-4f) return;
            float mid = yMin + length * 0.5f;

            // Default: hilt centre ~18% up from the bottom.
            float gripY = yMin + length * 0.18f;

            // Refine within the LOWER half only: a crossguard width spike marks the blade/handle
            // boundary; grip the centre of the handle segment below it.
            const int Bins = 48;
            var widthHi = new float[Bins];
            var hit = new bool[Bins];
            CollectWidthProfile(prop, parent, yMin, length, Bins, widthHi, hit);
            float median = MedianOfHit(widthHi, hit);
            float binH = length / Bins;
            int spikeBin = -1; float spikeW = 0f;
            for (int i = 0; i < Bins; i++)
            {
                if (!hit[i]) continue;
                float by = yMin + (i + 0.5f) * binH;
                if (by > mid) break;                       // lower half only
                if (widthHi[i] > spikeW) { spikeW = widthHi[i]; spikeBin = i; }
            }
            if (spikeBin >= 0 && median > 1e-5f && spikeW >= median * 1.6f)
            {
                float spikeY = yMin + (spikeBin + 0.5f) * binH;
                gripY = (yMin + spikeY) * 0.5f;            // centre of the handle below the crossguard
            }

            // Shift so the grip point sits at the parent origin (hand bone). NEVER flips — blade
            // stays +Y by the owner rule.
            Vector3 lp = prop.transform.localPosition;
            lp.y -= gripY;
            prop.transform.localPosition = lp;
            FlowTrace.Step("Equip", $"SeatHiltLowerHalf: gripY={gripY:0.###} spikeBin={spikeBin} spikeW={spikeW:0.###} median={median:0.###} shiftedY={prop.transform.localPosition.y:0.###}");
        }

        // WO-478 §12: dump seated transforms so headless equip captures prove native vs infer path.
        private static void LogGripSeatDiagnostics(GameObject prop, Transform gripRoot, Transform hand,
            string weaponId, string path)
        {
            if (prop == null || gripRoot == null) return;
            FlowTrace.Step("Equip",
                $"WO-478 seat dump [{path}] '{weaponId}': prop.localPos={prop.transform.localPosition} " +
                $"prop.localEuler={prop.transform.localRotation.eulerAngles} " +
                $"gripRoot.localPos={gripRoot.localPosition} gripRoot.localEuler={gripRoot.localRotation.eulerAngles} " +
                $"hand='{(hand != null ? hand.name : "<null>")}'");
        }

        // Bin mesh vertices along prop-local Y; record max |z| per bin (Z = wide axis, thickest
        // at hilt). Vertices are transformed mesh-local -> parent-local so the profile is
        // measured in the same frame the grip math uses.
        private static void CollectWidthProfile(
            GameObject prop, Transform parent, float yMin, float length,
            int bins, float[] widthHi, bool[] hit)
        {
            float inv = bins / length;
            foreach (var mf in prop.GetComponentsInChildren<MeshFilter>(true))
            {
                // BUILD-SAFE: sharedMesh.vertices THROWS ("Not allowed to access vertices")
                // when the mesh isn't Read/Write-enabled — the default for imported FBX in a
                // player build. Skip non-readable meshes; SeatByHandle then degrades to the
                // bounds-based grip (no vertex access). This was the in-build sword crash.
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var verts = mf.sharedMesh.vertices;
                Transform mt = mf.transform;
                for (int v = 0; v < verts.Length; v++)
                {
                    Vector3 world = mt.TransformPoint(verts[v]);
                    Vector3 local = parent.InverseTransformPoint(world);
                    int bin = Mathf.Clamp((int)((local.y - yMin) * inv), 0, bins - 1);
                    float w = Mathf.Abs(local.z);
                    if (!hit[bin] || w > widthHi[bin]) widthHi[bin] = w;
                    hit[bin] = true;
                }
            }
            // Skinned meshes (rare for a held prop, but be safe): use renderer bounds slabs.
            foreach (var smr in prop.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMesh == null || !smr.sharedMesh.isReadable) continue;
                var verts = smr.sharedMesh.vertices;
                Transform mt = smr.transform;
                for (int v = 0; v < verts.Length; v++)
                {
                    Vector3 world = mt.TransformPoint(verts[v]);
                    Vector3 local = parent.InverseTransformPoint(world);
                    int bin = Mathf.Clamp((int)((local.y - yMin) * inv), 0, bins - 1);
                    float w = Mathf.Abs(local.z);
                    if (!hit[bin] || w > widthHi[bin]) widthHi[bin] = w;
                    hit[bin] = true;
                }
            }
        }

        private static float MedianOfHit(float[] vals, bool[] hit)
        {
            var list = new List<float>();
            for (int i = 0; i < vals.Length; i++) if (hit[i]) list.Add(vals[i]);
            if (list.Count == 0) return 0f;
            list.Sort();
            return list[list.Count / 2];
        }

        // Width of the first hit bin scanning from one end (fromTop=true -> highest Y).
        private static float FirstHitWidth(float[] widthHi, bool[] hit, bool fromTop)
        {
            if (fromTop)
            {
                for (int i = widthHi.Length - 1; i >= 0; i--) if (hit[i]) return widthHi[i];
            }
            else
            {
                for (int i = 0; i < widthHi.Length; i++) if (hit[i]) return widthHi[i];
            }
            return 0f;
        }

        /// <summary>Removes the currently-shown weapon prop (no-op if none).</summary>
        public void Unequip()
        {
            _equipGeneration++;          // invalidate any in-flight async load
            DestroyCurrentWeapon();
            ReleaseWeaponHandle();
            _currentWeaponId = null;
            _gripRoot = null;
        }

        // ── OFF-HAND (shield) attach ─────────────────────────────────────────────────
        /// <summary>
        /// Attach the off-hand/shield described by <paramref name="def"/> to the hero's OFF hand
        /// (LeftHand bone), mirroring the main-hand attach path. A null def DETACHES the off-hand.
        /// Reuses the same Resources/primitive resolve + grip/seat the main path uses (the off-hand
        /// goes through the Shield preset, which already seats centre-grip on the LeftHand). Safe to
        /// call repeatedly (idempotent on an unchanged id).
        ///
        /// BLINK FIX (2026-06-19, "shield hangs off the body, not on the arm"): a Blink shield row
        /// (category=="shield", loadVia=="addressable", prefabPath "gear/weapon/Shield1h_XX") must
        /// load its REAL addressable prefab — the old path ignored Addressables entirely and loaded
        /// the legacy Tripo "shield_A" Resources mesh, whose foreign pivot + the non-zero gripPos
        /// (-0.05) left the shield floating beside the forearm instead of strapped to the hand. We
        /// now branch: Blink shields go through the SAME async Addressables path the main weapon uses
        /// (and seat NATIVE — trust the authored grip), Tripo/Resources shields keep the sync path.
        /// </summary>
        /// <summary>String-id convenience: resolve the off-hand (shield) WeaponDef from the catalog
        /// and equip it. Used by the Gear Preview to mirror the equipped shield. Null/empty detaches.</summary>
        public void EquipOffHand(string offHandId)
        {
            EquipOffHand(string.IsNullOrEmpty(offHandId) ? null : GearCatalog.FindWeapon(offHandId));
        }

        public void EquipOffHand(WeaponDef def)
        {
            string id = def != null ? def.id : null;

            // PACKAGE de-dupe: the Paladin body bakes its own shield — do NOT attach a KayKit/Blink shield
            // (owner F8 "shield is 180 degrees wrong" — that was an ATTACHED shield; skipping it leaves the
            // correctly-baked one). Loadout still tracks the off-hand; only the visible prop is suppressed.
            if (PackageBakedGear)
            {
                FlowTrace.Step("Equip",
                    $"PACKAGE baked-gear hero '{name}' — SKIP off-hand/shield attach for '{id ?? "<null>"}' " +
                    "(baked Paladin shield wins; no wrong-oriented attached shield).");
                return;
            }

            // Idempotent: same off-hand already shown -> nothing to do.
            if (string.Equals(_currentOffHandId, id, System.StringComparison.OrdinalIgnoreCase)
                && _currentOffHandProp != null)
                return;

            // New off-hand request — invalidate any in-flight async off-hand load + drop the old prop/handle.
            _offHandGeneration++;
            DestroyCurrentOffHand();
            ReleaseOffHandHandle();
            _currentOffHandId = id;
            if (string.IsNullOrEmpty(id)) return;   // detach

            // Resolve the off-hand visual. An off-hand item is a shield; Resolve maps shield ids
            // to the Shield preset (LeftHand, centre-grip). Force the Shield seat so a non-shield
            // off-hand id (future 1h offhand) still seats sanely on the off hand.
            WeaponVisual vis = Resolve(id);
            if (vis == null || vis.kind != WeaponClass.Shield)
                vis = Shield(vis != null && !string.IsNullOrEmpty(vis.mesh) ? vis.mesh : "shield_A");

            using var _ = FlowTrace.Enter("Equip", $"attach off-hand '{id}' to '{name}'");

            CacheRig();
            if (_animator == null || !_animator.isHuman)
            {
                FlowTrace.Warn("Equip", $"off-hand: rig not Humanoid yet on '{name}' — deferring to LateAttachRetry");
                return;
            }

            // ── INSTANTIATION-TIME ATTACH OVERRIDE (WO-510 slice 1) ───────────────────
            // Off-hand (shield) always seats on the LEFT hand; consult the rig-profiles.json
            // override (leftHand) first, authoritative over the humanoid avatar. ZERO change
            // when no profile exists. A dead override path SCREAMS then falls to the avatar.
            GameObject heroRoot = _animator.gameObject;
            string rigId = heroRoot.name;
            Transform hand = null;
            if (RigAttachmentRegistry.TryResolve(heroRoot, rigId, true, out var overrideAnchor, out var how))
            {
                hand = overrideAnchor;
                FlowTrace.Step("Offset", $"attach rig={rigId} hand=L -> '{hand.name}' (via json-override)");
            }
            else
            {
                if (how != null && how.StartsWith("missing"))
                    FlowTrace.Fail("Offset", $"attach rig={rigId} hand=L override path absent in model ({how}); falling back to avatar");

                hand = FlowTrace.Try("Equip", "GetBoneTransform(LeftHand)",
                    () => _animator.GetBoneTransform(HumanBodyBones.LeftHand), null);
                FlowTrace.Step("Offset", $"attach rig={rigId} hand=L -> '{(hand != null ? hand.name : "<null>")}' (via avatar)");
            }
            if (hand == null)
            {
                FlowTrace.Fail("Equip", $"Humanoid rig on '{name}' has NO LeftHand bone — " +
                    $"off-hand '{id}' NOT attached.");
                return;
            }

            // BLINK SHIELD (Addressable): load the real prefab async + seat NATIVE (trust the
            // authored grip-at-origin). Mirrors the main-hand Addressable branch + its stale-load
            // guard. A failed/throwing handle falls back to the sync Resources path (never unequipped).
            if (LoadsViaAddressable(def))
            {
                FlowTrace.Step("Equip", $"off-hand branch: ADDRESSABLE ('{def.prefabPath}')");
                BeginAddressableOffHand(def, vis, hand, id, _offHandGeneration);
                return;
            }

            FlowTrace.Step("Equip", $"off-hand branch: RESOURCES map (mesh='{vis.mesh}')");
            GameObject prop = LoadWeaponMesh(vis.mesh) ?? BuildFallbackPrimitive(vis);
            if (prop == null) { FlowTrace.Fail("Equip", $"off-hand prop null for mesh '{vis.mesh}'"); return; }

            FlowTrace.Step("Equip", $"off-hand seated: id='{id}' mesh='{vis.mesh}' hand='{hand.name}'");
            AttachOffHandProp(prop, vis, hand, id);
        }

        // Kick off the async Addressables load of a Blink off-hand (shield) prefab + attach on
        // completion. Mirrors BeginAddressableEquip (main hand): the off-hand handle has ONE owner,
        // the generation guard rejects a stale completion, and any failure GUARDS back to the sync
        // Resources path so the off-hand is never silently dropped because a Blink prefab hiccupped.
        private void BeginAddressableOffHand(
            WeaponDef def, WeaponVisual vis, Transform hand, string id, int generation)
        {
            string address = def.prefabPath;
            FlowTrace.Step("Gear", $"Addressable off-hand equip begin: id='{id}' address='{address}'");

            AsyncOperationHandle<GameObject> handle;
            try
            {
                handle = Addressables.LoadAssetAsync<GameObject>(address);
            }
            catch (System.Exception ex)
            {
                FlowTrace.Fail("Gear", $"Addressable off-hand load threw for '{address}': {ex.Message} — " +
                                       "falling back to Resources shield (hero keeps a shield).");
                FallbackResourcesOffHand(vis, hand, id);
                return;
            }

            _offHandHandle = handle;
            _offHandHandleOpen = true;

            handle.Completed += op =>
            {
                // Stale: the player swapped/unequipped the off-hand while this load was in flight.
                if (generation != _offHandGeneration)
                {
                    if (_offHandHandle.Equals(op)) { _offHandHandle = default; _offHandHandleOpen = false; }
                    DeferRelease(op);
                    return;
                }

                if (!op.IsValid() || op.Status != AsyncOperationStatus.Succeeded || op.Result == null)
                {
                    FlowTrace.Fail("Gear", $"Addressable off-hand load FAILED for '{address}' " +
                        $"(status={op.Status}) — falling back to Resources shield (hero keeps a shield).");
                    ReleaseOffHandHandle();
                    FallbackResourcesOffHand(vis, hand, id);
                    return;
                }

                // Blink shield prefab is authored grip-at-origin + oriented → seat NATIVE.
                var nativeVis = CopyOf(vis);
                nativeVis.native = true;
                GameObject prop = null;
                Guard.Try("Gear", $"instantiate addressable off-hand '{address}'",
                    () => prop = Instantiate(op.Result));
                if (prop == null)
                {
                    FlowTrace.Fail("Gear", $"Addressable off-hand Instantiate returned null for '{address}' " +
                        $"(id='{id}') — falling back to Resources shield (hero keeps a shield).");
                    ReleaseOffHandHandle();
                    FallbackResourcesOffHand(vis, hand, id);
                    return;
                }
                AttachOffHandProp(prop, nativeVis, hand, id);
                FlowTrace.Step("Gear", $"Addressable off-hand attached: id='{id}' address='{address}'");
            };
        }

        // Off-hand fallback: a Blink Addressable resolve failed — seat via the sync Resources map
        // (or the tinted primitive) so the hero is never left with the off slot blank. Non-native.
        private void FallbackResourcesOffHand(WeaponVisual vis, Transform hand, string id)
        {
            var fb = CopyOf(vis);
            fb.native = false;
            GameObject prop = LoadWeaponMesh(fb.mesh) ?? BuildFallbackPrimitive(fb);
            if (prop == null) return;
            AttachOffHandProp(prop, fb, hand, id);
        }

        // Release the held Addressables off-hand handle (no-op if none open). ONE owner — called on
        // every off-hand swap / detach / OnDisable so a Blink shield prefab never leaks.
        private void ReleaseOffHandHandle()
        {
            if (!_offHandHandleOpen) return;
            _offHandHandleOpen = false;
            if (_offHandHandle.IsValid())
                Addressables.Release(_offHandHandle);
            _offHandHandle = default;
        }

        // Seat an off-hand prop on the off hand. Shields are centre-gripped (their own NormalizeInto
        // + preset euler, like the bow) — they do NOT run the melee handle-inference / rig-axis
        // rotation. Kept separate from the main-hand AttachLoadedProp so the off-hand prop has its
        // own lifecycle reference (no clobbering the main weapon's grip-root / hold-pose state).
        private void AttachOffHandProp(GameObject prop, WeaponVisual vis, Transform hand, string id)
        {
            using var _ = FlowTrace.Enter("Equip", $"AttachOffHandProp '{id}' -> '{hand.name}' (kind={vis.kind})");
            prop.name = OffHandPropName;
            foreach (var c in prop.GetComponentsInChildren<Collider>(true)) if (c != null) Destroy(c);
            foreach (var rb in prop.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) Destroy(rb);

            // OFFSET RESOLUTION (WO-577): an off-hand can carry an authored VERTICAL-baseline
            // offset (fullOverride) from the in-game Seating Editor — keyed by mesh name then id.
            // fullOverride replaces the seat; a plain nudge composes on top of the preset grip
            // (same as main-hand) before ApplyGlobalWeaponYaw.
            string offsetKey = !string.IsNullOrEmpty(vis.mesh) ? vis.mesh : id;
            bool hasOffset = AttachmentOffsetRegistry.TryGetOffset(offsetKey, out var fo) ||
                             (offsetKey != id && AttachmentOffsetRegistry.TryGetOffset(id, out fo));
            bool fullOverride = hasOffset && fo.fullOverride;

            var gripRoot = new GameObject(OffHandPropName);
            float heldLen = ProportionalHeldLength(vis.heldLength);
            FlowTrace.Step("Equip",
                $"off-hand heldLength '{id}' kind={vis.kind}: archetype={vis.heldLength:0.###}m " +
                $"proportional={heldLen:0.###}m hero={ResolveHeroHeightM():0.###}m");
            if (fullOverride)
            {
                // VERTICAL baseline (geometry, longest->+Y) + the saved delta. Shields are not
                // melee, so no hilt-lower-half; the owner dials the full strap pose from vertical.
                FlowTrace.Step("Equip", $"off-hand seat: GEOMETRY-VERTICAL + saved DELTA pos={fo.pos} rot={fo.eulerRot} scale={fo.scale:0.###}");
                NormalizeInto(prop, gripRoot.transform, heldLen, resolveHilt: false);
                gripRoot.transform.SetParent(hand, false);
                gripRoot.transform.localPosition = vis.gripPos + fo.pos;
                gripRoot.transform.localRotation = Quaternion.Euler(fo.eulerRot);
                gripRoot.transform.localScale    = Vector3.one * (fo.scale > 0f ? fo.scale : 1f);
                _offHandAuthoredScale = fo.scale > 0f ? fo.scale : 1f;
                _offHandParentCompensate = false;   // owner dialed fo.scale by eye under this bone — don't re-solve it
            }
            // NATIVE Blink shield: trust the authored grip-at-origin + orientation (scale-only), and
            // seat dead-centre in the hand (zero gripPos/euler) like the bow's proven off-hand seat —
            // the foreign-pivot + (-0.05) offset of the legacy path is exactly what made the shield
            // dangle beside the forearm. Tripo/Resources shields keep the bounds-normalize + preset
            // grip (their pivot is unknown, so normalize centres them deterministically).
            else if (vis.native)
            {
                FlowTrace.Step("Equip", "off-hand seat: NATIVE (trust authored grip-at-origin, scale-only)");
                SeatNative(prop, gripRoot.transform, heldLen);
                gripRoot.transform.SetParent(hand, false);
                _offHandParentCompensate = true;
                _offHandAuthoredScale = 1f;   // the nudge block below records fo.scale if present
                CompensateParentScale(gripRoot.transform);
                gripRoot.transform.localPosition = Vector3.zero;
                gripRoot.transform.localRotation = Quaternion.identity;
            }
            else
            {
                FlowTrace.Step("Equip", "off-hand seat: NormalizeInto + preset grip (Tripo/Resources shield)");
                NormalizeInto(prop, gripRoot.transform, heldLen, resolveHilt: false);
                gripRoot.transform.SetParent(hand, false);
                _offHandParentCompensate = true;
                _offHandAuthoredScale = 1f;   // the nudge block below records fo.scale if present
                CompensateParentScale(gripRoot.transform);
                gripRoot.transform.localPosition = vis.gripPos;
                gripRoot.transform.localRotation = Quaternion.Euler(vis.gripEuler);
            }

            // OFFSET FORGE NUDGE (mirror main-hand): compose onto the seated frame, then global Y.
            if (!fullOverride && hasOffset)
            {
                bool nudged = fo.pos != Vector3.zero || fo.eulerRot != Vector3.zero ||
                              (fo.scale > 0f && Mathf.Abs(fo.scale - 1f) > 1e-4f);
                gripRoot.transform.localPosition += fo.pos;
                gripRoot.transform.localRotation =
                    gripRoot.transform.localRotation * Quaternion.Euler(fo.eulerRot);
                if (fo.scale > 0f && Mathf.Abs(fo.scale - 1f) > 1e-4f)
                {
                    // Record the authored multiplier so ApplyHoldPose's re-parent compensate
                    // re-composes comp * authored instead of wiping it (scale-parity 2026-07-07).
                    _offHandAuthoredScale = fo.scale;
                    gripRoot.transform.localScale = gripRoot.transform.localScale * fo.scale;
                }
                FlowTrace.Step("Offset", nudged
                    ? $"off-hand NUDGE '{offsetKey}' on geometry: +pos={fo.pos} *rot={fo.eulerRot} *scale={fo.scale:0.###}"
                    : $"off-hand offset '{offsetKey}' is all-zero — pure geometry (no nudge).");
            }

            gripRoot.transform.localRotation = ApplyGlobalWeaponYaw(gripRoot.transform.localRotation);

            _currentOffHandProp = gripRoot;
            // Capture off-hand attach inputs for the in-game Seating Editor (WO-577).
            _currentOffHandMeshKey    = offsetKey;
            _currentOffHandHeldLength = heldLen;
            _currentOffHandGripPos    = vis.gripPos;
            _currentOffHandGripEuler  = vis.gripEuler;
            _currentOffHandNative     = vis.native;

            // RENDER-VERIFY + DETACH-ON-FAIL (TGVRU): the shield can attach but be invisible (no
            // enabled renderer / no mesh) or seated on the wrong bone. PROVE it renders + is parented
            // under the resolved off hand (LeftHand) BEFORE leaving it on the hero; on fail destroy
            // it + clear the slot so no stray/invisible shield is left behind. Self-reports the why.
            if (!VerifyWeaponRendersNow(gripRoot, hand, id))
            {
                FlowTrace.Fail("Equip",
                    $"AttachOffHandProp: render-verify failed for off-hand '{id}' (no visible renderer or not parented to '{hand.name}') — detaching.");
                if (_currentOffHandProp == gripRoot) _currentOffHandProp = null;
                if (gripRoot != null) Destroy(gripRoot);
                return;
            }
            FlowTrace.Step("Equip", $"AttachOffHandProp: off-hand '{id}' verified rendered + seated on '{hand.name}' " +
                $"(native={vis.native}, localPos={gripRoot.transform.localPosition}, worldPos={gripRoot.transform.position}). " +
                "§12: if the owner still sees it off the arm, this is the exact landed seat to tune the Blink grip against.");

            // Record the DRAWN off-hand target, then place by carry state (drawn on the off-hand in
            // combat, sheathed on the back socket out of combat) — same as the main weapon, so the
            // shield is not shown floating on the arm in town (owner design 2026-07-04).
            _offHandHand = hand;
            _offHandDrawnLocalPos = gripRoot.transform.localPosition;
            _offHandDrawnLocalRot = gripRoot.transform.localRotation;
            ApplyHoldPose();
        }

        private void DestroyCurrentOffHand()
        {
            if (_currentOffHandProp != null)
            {
                Destroy(_currentOffHandProp);
                _currentOffHandProp = null;
            }
            _offHandHand = null;   // drop the resolved draw target so a stale (old-body) hand is never reused
        }

        // ── Hold state: idle (lowered) ↔ combat (drawn/raised) ───────────────────────
        /// <summary>
        /// Authoritative hold-state driver. Call from HeroLocomotion / a combat-state
        /// registry with the SAME `engaged` flag that feeds ActorAnimator.SetCombatStance,
        /// e.g. <c>GetComponent&lt;EquipmentController&gt;()?.SetCombatActive(engaged);</c>.
        /// Once called, the auto WaveManager-mirror fallback turns off (the caller owns it).
        /// false = sword lowered at the side; true = sword drawn/ready.
        /// </summary>
        public void SetCombatActive(bool active)
        {
            _combatExplicit = true;
            if (_combatActive == active && _gripRoot != null) { ApplyHoldPose(); return; }
            _combatActive = active;
            ApplyHoldPose();
        }

        /// <summary>Current hold state (false = idle/lowered, true = combat/ready).</summary>
        public bool CombatActive => _combatActive;

        // Auto-mirror fallback: if no caller drives SetCombatActive, derive the combat
        // hold the same way HeroLocomotion does — the blade rides ready ONLY while a wave is
        // genuinely live (Countdown/Active), not merely because a WaveManager exists in the
        // scene. The hub/town keeps an idle WaveManager, so presence alone must NOT draw the
        // weapon, or the hero holds it combat-ready in town. Cheap poll; the pose only
        // re-applies on a state change.
        private void Update()
        {
            // BUG 1: keep retrying the equip until the loadout subscribed + the Humanoid rig is
            // ready and the weapon/off-hand prop is actually up (companion attach-after-rebind).
            LateAttachRetry();

            // ARMOR TINT (WO-567): re-apply once the body renderers come online if an early
            // SetArmorTier landed before the HeroBody existed (cheap; clears the flag on success).
            if (_armorTintDirty) ApplyArmorTint();

            // While the in-game Seating Editor drives the grip root, the auto idle/combat hold
            // must not stomp the previewed pose (WO-577).
            if (_seatingEditActive) return;
            if (_combatExplicit || _gripRoot == null) return;
            if (_waveManager == null) _waveManager = Object.FindAnyObjectByType<WaveManager>();
            // CANONICAL in-combat signal — MUST match HeroLocomotion.IsWaveInCombat (BattleLock +
            // wave Active + imminent Countdown only). The old mirror treated ANY Countdown as drawn,
            // leaving the sword out for minutes while the animator read calm town idle.
            bool active = IsHeroCombatEngaged(_waveManager);
            if (active != _combatActive)
            {
                _combatActive = active;
                ApplyHoldPose();
            }
        }

        /// <summary>Mirror of <c>HeroLocomotion.IsWaveInCombat</c> — BattleLock (arena / in-scene
        /// duel) OR wave Active OR Countdown in its final imminent window only.</summary>
        private static bool IsHeroCombatEngaged(WaveManager wm)
        {
            if (DeNelle.Core.Combat.BattleLock.IsInBattle()) return true;
            if (wm == null) return false;
            if (wm.Phase == WavePhase.Active) return true;
            if (wm.Phase == WavePhase.Countdown && wm.CountdownRemaining <= 5f) return true;
            return false;
        }

        // CARRY STATE (owner design 2026-07-04): place each prop by combat state — DRAWN to the
        // hand in combat (the SAME seat battle uses, pure _baseGripRot), SHEATHED on the back socket
        // out of combat. This is the fix for the ~60° overworld float: the in-hand grip only ever
        // renders in combat (where it seats right); out of combat there is no hand grip to look wrong,
        // and the retired IdleHoldOffsetEuler tilt (the single context-dependent rotation) is gone.
        // Runs on every combat-state change (Update auto-mirror / SetCombatActive) and once per attach.
        private void ApplyHoldPose()
        {
            // A live SHEATHED seating edit pins the props to the back socket even if combat starts —
            // the owner is dialing the back pose (Update's auto-mirror is already suspended while
            // _seatingEditActive; this covers an explicit SetCombatActive caller mid-edit).
            bool drawn = _combatActive && !(_seatingEditActive && _seatEditSheathed);
            Transform back = drawn ? null : ResolveBackSocket();

            // ── Main weapon ──
            if (_gripRoot != null)
            {
                if (!drawn && back != null)
                {
                    _gripRoot.SetParent(back, false);
                    // Back-socket bones carry a different lossyScale than the hand — always compensate
                    // so the attach-authored multiplier (fo.scale) survives the carry-state re-parent.
                    CompensateParentScale(_gripRoot, _weaponAuthoredScale);
                    _gripRoot.localPosition = _sheatheWeaponLocalPos;
                    // DERIVED sheathe rotation (the fix): build the base orientation from the body's
                    // own axes via the SAME LookRotation(flat, blade) construction the correct battle
                    // draw uses (ComputeMeleeGripRotation), then compose the persisted authored nudge —
                    // instead of the old hand-guessed magic euler that ignored the chest-bone axes.
                    _gripRoot.localRotation = ComputeSheathRotation(back);
                    // Sheathed pose: explicit "<meshKey>@sheathed" wins; else fall back to the drawn
                    // offset ("<meshKey>") as a nudge on this built-in back pose (town carry fix).
                    ApplySheathedOffset(_gripRoot, _currentWeaponMeshKey);
                }
                else if (_weaponHand != null)
                {
                    // Drawn (or sheathed with no back bone on this rig — never leave it floating unparented).
                    _gripRoot.SetParent(_weaponHand, false);
                    if (_weaponParentCompensate) CompensateParentScale(_gripRoot, _weaponAuthoredScale);
                    _gripRoot.localPosition = _weaponDrawnLocalPos;
                    _gripRoot.localRotation = _baseGripRot;
                }
            }

            // ── Off-hand / shield ──
            if (_currentOffHandProp != null)
            {
                var offT = _currentOffHandProp.transform;
                if (!drawn && back != null)
                {
                    offT.SetParent(back, false);
                    CompensateParentScale(offT, _offHandAuthoredScale);
                    offT.localPosition = _sheatheOffHandLocalPos;
                    // DE-BAND-AID NOTE (2026-07-07): _sheatheOffHandLocalEuler (the hand-tuned magic
                    // euler, owner Z+=180 correction 2026-07-04) is now only the DEFAULT under the
                    // @sheathed offset seam — an owner-authored "<meshKey>@sheathed" registry entry
                    // (Seating Editor, Sheathed mode) supersedes it below. Kept, not removed: with no
                    // entry this line is the exact shipped pose (zero regression).
                    offT.localRotation = ApplyGlobalWeaponYaw(Quaternion.Euler(_sheatheOffHandLocalEuler));
                    ApplySheathedOffset(offT, _currentOffHandMeshKey);
                }
                else if (_offHandHand != null)
                {
                    offT.SetParent(_offHandHand, false);
                    if (_offHandParentCompensate) CompensateParentScale(offT, _offHandAuthoredScale);
                    offT.localPosition = _offHandDrawnLocalPos;
                    offT.localRotation = _offHandDrawnLocalRot;
                }
            }
        }

        // How a sheathed registry entry was resolved (explicit @sheathed vs drawn-key fallback).
        private enum SheathedOffsetSource { None, Explicit, DrawnFallback }

        // Resolve the offset that should refine a sheathed (back-socket) pose:
        //   1) "<meshKey>@sheathed" — owner-authored back pose (Seating Editor, Sheathed mode).
        //   2) "<meshKey>" — FALLBACK: reuse the drawn offset as a nudge on the built-in back pose
        //      so offsets dialed/saved without the @sheathed suffix still affect town carry.
        // Drawn fullOverride entries are NEVER applied as absolute on the back (hand frame ≠ socket).
        private static bool TryResolveSheathedOffset(string meshKey, out AttachmentOffset fo,
                                                     out SheathedOffsetSource source)
        {
            fo = default;
            source = SheathedOffsetSource.None;
            if (string.IsNullOrEmpty(meshKey)) return false;
            if (AttachmentOffsetRegistry.TryGetOffset(meshKey + SheathedKeySuffix, out fo))
            {
                source = SheathedOffsetSource.Explicit;
                return true;
            }
            if (AttachmentOffsetRegistry.TryGetOffset(meshKey, out fo))
            {
                source = SheathedOffsetSource.DrawnFallback;
                return true;
            }
            return false;
        }

        // OWNER-AUTHORABLE SHEATHED POSE consumption (root fix 2026-07-07): after the built-in
        // sheathe pose is applied, refine it from the registry in the BACK-SOCKET frame:
        //   • explicit @sheathed + fullOverride → absolute pos/rot in the socket frame.
        //   • explicit @sheathed + nudge → +pos, built-in rot ∘ Euler(rot).
        //   • drawn-key fallback → +pos ONLY by default: the drawn euler was authored in the HAND
        //     frame (e.g. sword_A (117,-61,-111)); composing it onto the chest-socket sheathe
        //     rotation is a frame mismatch. ff.sheathdrawnrot=1 restores the full pos+rot compose
        //     (the 0492d7dc behavior) as the owner's A/B backup.
        // Scale is deliberately untouched — scale is owned by the attach path (comp * authored).
        private static void ApplySheathedOffset(Transform t, string meshKey)
        {
            if (t == null || string.IsNullOrEmpty(meshKey)) return;
            if (!TryResolveSheathedOffset(meshKey, out var fo, out var source)) return;
            bool absolute = source == SheathedOffsetSource.Explicit && fo.fullOverride;
            if (absolute)
            {
                t.localPosition = fo.pos;
                t.localRotation = Quaternion.Euler(fo.eulerRot);
            }
            else
            {
                t.localPosition += fo.pos;
                bool composeRot = source == SheathedOffsetSource.Explicit
                                  || DeNelle.Core.FeatureFlags.SheathedDrawnRotFallback;
                if (composeRot)
                    t.localRotation = t.localRotation * Quaternion.Euler(fo.eulerRot);
            }
            if (source == SheathedOffsetSource.Explicit)
                FlowTrace.Step("Offset", $"sheathed offset '{meshKey}{SheathedKeySuffix}' applied: " +
                    $"pos={fo.pos} rot={fo.eulerRot} full={fo.fullOverride}");
            else
                FlowTrace.Step("Offset", $"sheathed FALLBACK (drawn '{meshKey}' on back pose): " +
                    $"pos={fo.pos} rot={(DeNelle.Core.FeatureFlags.SheathedDrawnRotFallback ? fo.eulerRot.ToString() : "SKIPPED (pos-only, ff.sheathdrawnrot=0)")}");
        }

        // Lazily create the shared BACK sheathe socket under the Chest bone (fallback Spine, then
        // UpperChest) — the exact torso-anchor pattern GearVisualApplier uses for chest armor
        // (GearVisualApplier.cs:217-218). Returns null on a non-Humanoid / torso-less rig, in which
        // case ApplyHoldPose keeps the prop on the hand (no back socket = never unparented/floating).
        // Cleared on a body swap (ReseatForBody) so it re-creates under the new visible body's chest.
        // Owner F8 2026-07-06 "Shield larger than hero": props are bounds-normalized to their
        // proportional heldLength at the WORLD ORIGIN (unit scale), then SetParent(bone, false)
        // preserves LOCAL scale — so the rendered size gets multiplied by the bone's lossyScale,
        // which carries the VisualFactory.Fit body-normalization factor (≠1 on CC/AccuRig rigs).
        // This divides it back out so the world-size solve survives parenting. Re-applied on every
        // re-parent (hand <-> back socket) since different bones can carry different lossy scales.
        // Skipped for owner-dialed fullOverride scales (those were tuned by eye under the bone).
        private bool _weaponParentCompensate, _offHandParentCompensate;

        // ONE SOURCE OF TRUTH for the parent-scale factor (2026-07-07): used by both the runtime
        // CompensateParentScale below AND ApplySeatingPreview, so the Seating Editor renders the
        // exact scale composition every subsequent boot renders.
        // WYSIWYG break proven 2026-07-07: preview lacked compensate (hand lossy 1.666) —
        // owner-dialed 0.46 rendered 0.276 at boot.
        private static Vector3 ParentScaleCompensation(Transform parent)
        {
            if (parent == null) return Vector3.one;
            Vector3 ls = parent.lossyScale;
            if (ls.x <= 1e-4f || ls.y <= 1e-4f || ls.z <= 1e-4f) return Vector3.one;
            return new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
        }

        private static void CompensateParentScale(Transform gripRoot, float authoredScale = 1f)
        {
            var p = gripRoot != null ? gripRoot.parent : null;
            if (p == null) return;
            Vector3 ls = p.lossyScale;
            if (ls.x <= 1e-4f || ls.y <= 1e-4f || ls.z <= 1e-4f) return;
            if (authoredScale <= 0f) authoredScale = 1f;
            // comp * authored — the owner-dialed offsets.json scale (fo.scale) survives every
            // re-parent instead of being wiped back to pure 1/lossy (scale-parity fix 2026-07-07).
            gripRoot.localScale = ParentScaleCompensation(p) * authoredScale;
            // §12: log the RESULTING world size, not just the math — capture 9403 showed the
            // sheathed shield still rendering oversized while these compensate lines fired,
            // so the proof must be the rendered bounds, not the applied scale.
            Bounds wb = default; bool hasB = false;
            foreach (var r in gripRoot.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                if (!hasB) { wb = r.bounds; hasB = true; } else wb.Encapsulate(r.bounds);
            }
            FlowTrace.Step("Equip",
                $"parent-scale compensate: parent='{p.name}' lossy=({ls.x:0.###},{ls.y:0.###},{ls.z:0.###}) " +
                $"authored={authoredScale:0.###} " +
                $"-> worldBounds={(hasB ? wb.size.ToString("0.###") : "<no renderer>")} " +
                "(the proportional solve should read here as heldLength * authored on the longest axis)");
        }

        private Transform ResolveBackSocket()
        {
            if (_backSocket != null) return _backSocket;   // a destroyed Unity object compares == null → re-created
            if (_animator == null || !_animator.isHuman) return null;
            Transform anchor = _animator.GetBoneTransform(HumanBodyBones.Chest);
            if (anchor == null) anchor = _animator.GetBoneTransform(HumanBodyBones.Spine);
            if (anchor == null) anchor = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (anchor == null) return null;
            var go = new GameObject("SheatheSocket_Back");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            _backSocket = go.transform;
            FlowTrace.Step("Equip", $"ResolveBackSocket on '{name}': sheathe anchor under bone '{anchor.name}'.");
            return _backSocket;
        }

        // ── DERIVED SHEATHE ROTATION (owner F8 fix 2026-07-04 — "the secret is on battle") ──────────
        // The DRAWN (battle) seat is correct because it is DERIVED from the rig, never guessed:
        // ComputeMeleeGripRotation builds Quaternion.LookRotation(up, blade) from the HAND bone's own
        // axes so the prop's blade line (prop-local +Y, put there by NormalizeInto + SeatHiltLowerHalf)
        // and its flat-plane normal (prop-local +Z) land forward-out-of-the-fist. The OLD sheathe used a
        // hand-typed magic euler (8,0,158) with no relationship to geometry OR the chest-bone axes, so it
        // sat wrong. This DERIVES the on-back orientation the SAME way — from the BODY's own axes — with
        // the identical LookRotation(flat, blade) construction, so the sheathed sword sits right the way
        // the drawn one does. On the back we want the blade laid diagonally (up the spine, leaning toward
        // the off shoulder — a baldric carry) with the flat of the blade against the back (facing out).
        //   • prop +Y (blade)  -> worldBlade  (up, tilted _sheatheBladeDiagonalDeg toward the off shoulder)
        //   • prop +Z (flat)   -> worldFlat = body backward (blade lies flat on the back, edge out)
        // Built in WORLD from the body's axes, then expressed in the socket's LOCAL frame so it follows
        // the chest bone through animation/turning exactly like the drawn seat follows the hand. Finally
        // the persisted authored nudge (_sheatheWeaponLocalEuler, owner felt-tune) composes on top —
        // the sheathe equivalent of _swordGripEuler nudging the drawn seat.
        private Quaternion ComputeSheathRotation(Transform socket)
        {
            Transform body = _animator != null ? _animator.transform : transform;
            // Off shoulder = opposite the main (right) hand → toward body-left. Blade leans that way.
            Vector3 offShoulder = -body.right;
            float rad = _sheatheBladeDiagonalDeg * Mathf.Deg2Rad;
            Vector3 worldBlade = (body.up * Mathf.Cos(rad) + offShoulder * Mathf.Sin(rad)).normalized;
            Vector3 worldFlat  = -body.forward;   // flat of the blade rests against the back, edge out
            // LookRotation(forward, upwards): +Z -> forward, +Y -> upwards. We want prop +Z -> flat and
            // prop +Y -> blade — the SAME axis mapping ComputeMeleeGripRotation uses (LookRotation(up, blade)).
            Quaternion worldTarget = Quaternion.LookRotation(worldFlat, worldBlade);
            // Express in the socket's local frame (the socket follows the chest bone), then the nudge.
            Quaternion localBase = Quaternion.Inverse(socket.rotation) * worldTarget;
            return localBase * Quaternion.Euler(_sheatheWeaponLocalEuler);
        }

        // Force the given slot's prop to its DRAWN (in-hand) seat regardless of combat state — used by
        // the in-game Seating Editor so a tune session always edits the in-hand grip, never the sheathed
        // back pose. No-op if that slot has no prop / no resolved hand yet.
        private void DrawForEditing(bool offHand)
        {
            if (offHand)
            {
                if (_currentOffHandProp != null && _offHandHand != null)
                {
                    var t = _currentOffHandProp.transform;
                    t.SetParent(_offHandHand, false);
                    t.localPosition = _offHandDrawnLocalPos;
                    t.localRotation = _offHandDrawnLocalRot;
                }
            }
            else if (_gripRoot != null && _weaponHand != null)
            {
                _gripRoot.SetParent(_weaponHand, false);
                _gripRoot.localPosition = _weaponDrawnLocalPos;
                _gripRoot.localRotation = _baseGripRot;
            }
        }

        // Build the sword grip-root's LOCAL rotation (in the hand bone's space) so the blade
        // extends forward from the fist. The prop's blade/grip line is local +Y; we rotate
        // the grip root so its +Y points along the hand bone's _handBladeAxis and its +Z
        // along the hand bone's _handGripUpAxis — i.e. the prop frame is rebuilt to match the
        // hand's natural "point" + grip axes rather than assuming a world-aligned bone. The
        // serialized _swordGripEuler is then applied in that corrected local frame as a final
        // calibration nudge (e.g. tip the blade forward-and-slightly-up). Rig-specific: the
        // axis choices are exposed so they can be re-picked in the Inspector without a recompile.
        // WO-435: generalized to ALL melee. <paramref name="kind"/> selects the per-archetype
        // calibration nudge (sword/dagger -> _swordGripEuler; staff/wand/axe/mace -> their own
        // field, defaulted 0). The rig-hand-axis basis is identical across families — every melee
        // weapon's primary axis is prop-local +Y (placed there by NormalizeInto + SeatByHandle),
        // so the same blade/grip-up axes seat it forward-from-the-fist; only the residual nudge differs.
        private Quaternion ComputeMeleeGripRotation(WeaponClass kind)
        {
            Vector3 blade = _handBladeAxis.sqrMagnitude > 1e-6f ? _handBladeAxis.normalized : Vector3.up;
            Vector3 up    = _handGripUpAxis.sqrMagnitude > 1e-6f ? _handGripUpAxis.normalized : Vector3.forward;

            // Orthonormalize `up` against `blade` so the basis is valid even if the two
            // chosen axes aren't perfectly perpendicular on this rig.
            up = up - Vector3.Dot(up, blade) * blade;
            if (up.sqrMagnitude < 1e-6f)
            {
                // Degenerate (axes parallel) — pick any axis not collinear with the blade.
                up = Mathf.Abs(blade.y) < 0.9f ? Vector3.up : Vector3.forward;
                up = up - Vector3.Dot(up, blade) * blade;
            }
            up.Normalize();

            // Rotation mapping prop-local (+Y up, +Z forward) onto (blade, up): Quaternion
            // .LookRotation builds a frame whose +Z = forward, +Y = up. We want the prop's
            // +Y (its primary line) to land on `blade` and its +Z (its flat-plane normal) on
            // `up`, so feed forward=up, upwards=blade.
            Quaternion rigAligned = Quaternion.LookRotation(up, blade);
            return rigAligned * Quaternion.Euler(MeleeGripNudge(kind));
        }

        // The per-archetype additive calibration nudge (CANON; never auto-overwritten). Sword and
        // dagger share the bladed _swordGripEuler; the rest map to their own inspector field.
        private Vector3 MeleeGripNudge(WeaponClass kind)
        {
            switch (kind)
            {
                case WeaponClass.Sword:
                case WeaponClass.Dagger: return _swordGripEuler;
                case WeaponClass.Staff:  return _staffGripEuler;
                case WeaponClass.Wand:   return _wandGripEuler;
                case WeaponClass.Axe:    return _axeGripEuler;
                case WeaponClass.Hammer: return _maceGripEuler;
                default:                 return Vector3.zero;
            }
        }

        // ── ARMOR VISUAL (WO-567 — static-model TINT, NOT a mesh swap) ───────────────
        /// <summary>
        /// Drive the hero's armor look from the worn tier (0 = none … 5 = legendary). The
        /// combat-pivot north star keeps ONE static hero model — so this does NOT swap a mesh or
        /// revive Blink. Instead it tints the BODY with a tier accent (richer with tier) via a
        /// MaterialPropertyBlock, so equipping better armor is VISIBLE on the static model.
        /// Driven by GearLoadout.PushArmorTierToBody on every equip change, and by the Gear
        /// Preview (HeroPreviewViewer) for the showcase. Cheap + leak-free (MPB, no instancing).
        /// </summary>
        public void SetArmorTier(int tier)
        {
            _armorTier = Mathf.Max(0, tier);
            _armorTintDirty = true;
            ApplyArmorTint();   // body may not be ready yet — stays dirty + retried in Update
        }

        /// <summary>Current armor tier (0 = none).</summary>
        public int ArmorTier => _armorTier;

        // Owner-tunable BONES (OWNER-DECISION: felt-tune freely). Per-tier MULTIPLIER applied to
        // the body's authored base color, so tier 0 restores the original EXACTLY and higher tiers
        // add a metal sheen. Hues track ArmorVfxMap's rarity bands (cool steel → blue → violet →
        // gold) but as an albedo multiply (armor metal), NOT the additive rim GLOW the rim light
        // owns — the two reads compose. Kept gentle so the hero's skin/face never discolors hard.
        private static readonly Color[] ArmorTintByTier =
        {
            new Color(1.00f, 1.00f, 1.00f),  // 0 none — identity (no tint)
            new Color(0.97f, 0.98f, 1.00f),  // 1 common — faint cool steel
            new Color(0.90f, 0.94f, 1.00f),  // 2 uncommon — light steel-blue
            new Color(0.82f, 0.89f, 1.04f),  // 3 rare — cool blue sheen
            new Color(0.88f, 0.80f, 1.04f),  // 4 epic — violet sheen
            new Color(1.05f, 0.92f, 0.64f),  // 5 legendary — warm gold sheen
        };

        private static Color ArmorTintMultiplier(int tier)
        {
            if (tier <= 0) return Color.white;
            int i = Mathf.Clamp(tier, 0, ArmorTintByTier.Length - 1);
            return ArmorTintByTier[i];
        }

        // Apply (or clear) the tier tint on the hero BODY renderers via MPB. MULTIPLIES the captured
        // authored base color by the tier accent (tier 0 = identity restore). MERGE pattern
        // (GetPropertyBlock first) so HeroArmorRimLight's emission set is preserved. No-op (and stays
        // dirty) until the body renderers exist, so an early SetArmorTier re-applies once the body is up.
        private void ApplyArmorTint()
        {
            if (!ResolveBodyRenderers()) return;   // body not ready — stay dirty, retried in Update
            if (_armorMpb == null) _armorMpb = new MaterialPropertyBlock();

            Color mul = ArmorTintMultiplier(_armorTier);
            int applied = 0;
            for (int i = 0; i < _bodyRenderers.Count; i++)
            {
                var smr = _bodyRenderers[i];
                if (smr == null) continue;
                Color a = _bodyBaseColors[i];
                Color tinted = new Color(a.r * mul.r, a.g * mul.g, a.b * mul.b, a.a);
                smr.GetPropertyBlock(_armorMpb);          // merge — keep rim emission etc.
                _armorMpb.SetColor(BaseColorId, tinted);
                _armorMpb.SetColor(ColorId, tinted);      // Built-in/Standard fallback
                smr.SetPropertyBlock(_armorMpb);
                applied++;
            }
            _armorTintDirty = false;
            FlowTrace.Step("Equip",
                $"ApplyArmorTint on '{name}' tier={_armorTier} mul=({mul.r:0.00},{mul.g:0.00},{mul.b:0.00}) -> {applied} body renderer(s).");
        }

        // Resolve + cache the hero BODY SkinnedMeshRenderers (the static model) and snapshot each
        // one's authored base color, so the tint MULTIPLIES (never wipes a baked tint; tier 0 restores
        // exactly). SkinnedMeshRenderer only — the character body — so weapon/shield MeshRenderer props
        // are never tinted. Re-scans when the cache is empty/stale (body swap). False when no body yet.
        private bool ResolveBodyRenderers()
        {
            bool stale = _bodyRenderers.Count == 0;
            for (int i = 0; i < _bodyRenderers.Count && !stale; i++)
                if (_bodyRenderers[i] == null) stale = true;
            if (stale)
            {
                _bodyRenderers.Clear();
                _bodyBaseColors.Clear();
                GetComponentsInChildren(true, _bodyRenderers);
                foreach (var smr in _bodyRenderers)
                {
                    Color c = Color.white;
                    var mat = smr != null ? smr.sharedMaterial : null;
                    if (mat != null)
                    {
                        if (mat.HasProperty(BaseColorId)) c = mat.GetColor(BaseColorId);
                        else if (mat.HasProperty(ColorId)) c = mat.GetColor(ColorId);
                    }
                    _bodyBaseColors.Add(c);
                }
            }
            return _bodyRenderers.Count > 0;
        }

        // ═════════════════════════════════════════════════════════════════════════════
        //  IN-GAME SEATING EDITOR API (WO-577, Offset Forge slice 2)
        //  Drives the offset of the CURRENTLY equipped weapon/off-hand live, by eye, on the
        //  REAL hero — the runtime parallel of the editor-only Offset Forge window. The
        //  SeatingEditorOverlay (DeNelle.Village.UI) is the on-screen UI; this is the model.
        //  what-you-see-is-what-you-save: the preview mirrors the exact attach math so a Save
        //  (-> offsets.json via AttachmentOffsetRegistry) reproduces the previewed pose on the
        //  next equip / scene load. DEV-only — gated by the caller (AdminOverlay dev tools).
        // ═════════════════════════════════════════════════════════════════════════════

        /// <summary>Snapshot handed to the editor when an edit session begins.</summary>
        public struct SeatingEditInfo
        {
            public bool    valid;
            public bool    offHand;
            public bool    sheathed;      // true = editing the BACK (sheathed) pose (key gets "@sheathed")
            public string  offsetKey;     // what the offset is saved under (mesh name [+ "@sheathed"])
            public string  label;         // human label (weapon/off-hand id)
            public bool    melee;
            public Vector3 pos;           // seeded from any existing saved offset
            public Vector3 euler;
            public float   scale;
            public bool    fullOverride;
        }

        /// <summary>True while a seating edit session is live (auto hold suspended).</summary>
        public bool SeatingEditActive => _seatingEditActive;

        /// <summary>Does the requested slot currently have an equipped prop to edit?</summary>
        public bool HasSeatingTarget(bool offHand) =>
            (offHand ? _currentOffHandProp : _currentWeaponProp) != null;

        /// <summary>
        /// Begin editing the offset of the equipped <paramref name="offHand"/> slot. Seeds the
        /// returned info from any existing saved offset, suspends the auto idle/combat hold, and
        /// applies the (seeded) preview so the live model immediately reflects the editable pose.
        /// Returns false (info.valid=false) when that slot has no prop equipped.
        /// </summary>
        public bool BeginSeatingEdit(bool offHand, out SeatingEditInfo info)
            => BeginSeatingEdit(offHand, false, out info);

        /// <summary>
        /// Overload (2026-07-07): <paramref name="sheathed"/>=true edits the BACK (sheathed) pose —
        /// the prop is forced to the back socket (not drawn to the hand), the offset is keyed
        /// "&lt;meshKey&gt;@sheathed", and the preview runs in the back-socket frame per the
        /// ApplyHoldPose consumption contract. This makes the town carry pose owner-authorable via
        /// the SAME registry the drawn seat uses (the root fix for "my offsets are invisible in town").
        /// </summary>
        public bool BeginSeatingEdit(bool offHand, bool sheathed, out SeatingEditInfo info)
        {
            info = default;
            var grip = offHand ? _currentOffHandProp : _currentWeaponProp;
            if (grip == null)
            {
                FlowTrace.Warn("Offset", $"BeginSeatingEdit: no {(offHand ? "off-hand" : "weapon")} prop equipped on '{name}'.");
                return false;
            }

            string key = offHand ? _currentOffHandMeshKey : _currentWeaponMeshKey;
            string id  = offHand ? _currentOffHandId      : _currentWeaponId;
            string baseKey = !string.IsNullOrEmpty(key) ? key : id;
            AttachmentOffset fo = default;
            SheathedOffsetSource src = SheathedOffsetSource.None;
            bool has = false;
            if (!string.IsNullOrEmpty(baseKey))
            {
                if (sheathed)
                    has = TryResolveSheathedOffset(baseKey, out fo, out src);
                else
                    has = AttachmentOffsetRegistry.TryGetOffset(baseKey, out fo);
            }
            string offsetKey = sheathed && !string.IsNullOrEmpty(baseKey)
                ? baseKey + SheathedKeySuffix
                : baseKey;

            info.valid        = true;
            info.offHand      = offHand;
            info.sheathed     = sheathed;
            info.offsetKey    = offsetKey;
            info.label        = !string.IsNullOrEmpty(id) ? id : offsetKey;
            info.melee        = !offHand && _currentWeaponMelee;
            info.pos          = has ? fo.pos      : Vector3.zero;
            info.euler        = has ? fo.eulerRot : Vector3.zero;
            info.scale        = has && fo.scale > 0f ? fo.scale : 1f;
            if (sheathed)
                info.fullOverride = has && src == SheathedOffsetSource.Explicit && fo.fullOverride;
            else
                info.fullOverride = has ? fo.fullOverride : true;

            _seatingEditActive = true;
            _seatEditOffHand   = offHand;
            _seatEditSheathed  = sheathed;
            _seatEditMode      = -1;   // force a re-seat on the first preview (drawn mode)
            if (sheathed)
            {
                // Sheathed edit tunes the BACK pose — force the sheathed placement (ApplyHoldPose
                // treats combat as inactive while _seatEditSheathed) so the preview math runs in
                // the back-socket frame the runtime consumption uses.
                ApplyHoldPose();
                if (ResolveBackSocket() == null)
                    FlowTrace.Warn("Offset", $"BeginSeatingEdit(sheathed): no back socket on '{name}' — " +
                        "sheathed preview would run in the hand frame; pose may not reproduce.");
            }
            else
            {
                // The drawn edit tunes the IN-HAND seat — make sure the edited prop is DRAWN to its
                // hand (not sheathed on the back), so the preview math runs in the correct frame.
                DrawForEditing(offHand);
            }
            ApplySeatingPreview(info.pos, info.euler, info.scale, info.fullOverride);
            FlowTrace.Step("Offset", $"BeginSeatingEdit '{info.offsetKey}' offHand={offHand} sheathed={sheathed} seed pos={info.pos} euler={info.euler} scale={info.scale:0.###} full={info.fullOverride}");
            return true;
        }

        /// <summary>
        /// Live-preview an offset on the equipped slot being edited. Mirrors the attach seat math
        /// so the preview == the saved runtime result. <paramref name="fullOverride"/> true =
        /// VERTICAL baseline (hilt-lower-half for melee) + this rotation as the absolute in-hand
        /// pose; false = NUDGE on top of the geometric rig-aware grip (legacy WO-551). Re-seats the
        /// prop child only when the baseline mode flips (cheap; no destroy / async reload).
        /// </summary>
        public void ApplySeatingPreview(Vector3 pos, Vector3 euler, float scale, bool fullOverride)
        {
            bool offHand = _seatEditOffHand;
            var grip = offHand ? _currentOffHandProp : _currentWeaponProp;
            if (grip == null) return;
            var grt = grip.transform;
            if (grt.childCount == 0) return;

            // SHEATHED edit (2026-07-07): pos/euler live in the BACK-SOCKET frame per the
            // ApplyHoldPose consumption contract — no hand-grip composition, no global yaw.
            if (_seatEditSheathed)
            {
                ApplySheathedSeatingPreview(grt, offHand, pos, euler, fullOverride);
                return;
            }

            var child = grt.GetChild(0).gameObject;

            bool    melee   = !offHand && _currentWeaponMelee;
            float   held    = offHand ? _currentOffHandHeldLength : _currentWeaponHeldLength;
            Vector3 gripPos = offHand ? _currentOffHandGripPos    : _currentWeaponGripPos;
            if (scale <= 0f) scale = 1f;

            int wantMode = fullOverride ? 1 : 0;
            bool nativeMeleePreview = melee && _currentWeaponNative && !FeatureFlags.WeaponGripInfer;
            if (_seatEditMode != wantMode)
            {
                // Reset the grip root so the child re-seat math (measured in parent-local) is clean.
                grt.localRotation = Quaternion.identity;
                grt.localScale    = Vector3.one;
                grt.localPosition = Vector3.zero;
                if (nativeMeleePreview && wantMode == 0)
                {
                    SeatNative(child, grt, held > 0f ? held : 1f);
                }
                else
                {
                    NormalizeInto(child, grt, held > 0f ? held : 1f,
                        ResolveHiltFromKind(offHand ? WeaponClass.Shield : _currentWeaponKind));
                    if (melee)
                        SeatHiltLowerHalf(child, grt);
                }
                _seatEditMode = wantMode;
            }

            // Compose the grip-root transform exactly as the attach path does for this mode.
            Quaternion baseRot;
            if (fullOverride)
                baseRot = Quaternion.identity;                                   // delta IS the pose
            else if (nativeMeleePreview)
            {
                baseRot = Quaternion.Euler(_currentWeaponGripEuler) *
                          Quaternion.Euler(MeleeGripNudge(_currentWeaponKind));
            }
            else if (melee)
                baseRot = ComputeMeleeGripRotation(_currentWeaponKind);          // rig-aware grip
            else
                baseRot = Quaternion.Euler(offHand ? _currentOffHandGripEuler : _currentWeaponGripEuler);

            grt.localPosition = gripPos + pos;
            grt.localRotation = ApplyGlobalWeaponYaw(baseRot * Quaternion.Euler(euler));
            // WYSIWYG break proven 2026-07-07: preview lacked compensate (hand lossy 1.666) —
            // owner-dialed 0.46 rendered 0.276 at boot. Mirror the runtime scale composition
            // EXACTLY: a compensated slot renders ParentScaleCompensation(parent) * scale —
            // the SAME helper CompensateParentScale (attach + hold-pose) composes from.
            bool compensate = offHand ? _offHandParentCompensate : _weaponParentCompensate;
            grt.localScale = (compensate ? ParentScaleCompensation(grt.parent) : Vector3.one) * scale;

            // Keep the editor's mirror of base state coherent so a later EndSeatingEdit / hold
            // re-apply uses the previewed orientation as the base (no snap-back).
            if (!offHand)
            {
                _baseGripRot   = grt.localRotation;
                _baseGripEuler = _baseGripRot.eulerAngles;
            }
        }

        // SHEATHED-pose preview (2026-07-07): applies pos/euler in the BACK-SOCKET frame exactly
        // per ApplySheathedOffset's consumption contract, so what the owner dials on the back is
        // byte-identical to what ApplyHoldPose reproduces in town on every boot:
        //   • fullOverride=true  → localPosition = pos, localRotation = Euler(euler) — absolute in
        //     the socket frame, NO global yaw (the authored value IS the pose).
        //   • fullOverride=false → nudge composed on the built-in sheathe pose (derived
        //     ComputeSheathRotation for the weapon; _sheatheOffHandLocal* default for the shield).
        // Scale is deliberately untouched — the sheathe never owns scale (the attach path does).
        private void ApplySheathedSeatingPreview(Transform grt, bool offHand,
            Vector3 pos, Vector3 euler, bool fullOverride)
        {
            Transform back = ResolveBackSocket();
            if (back == null)
            {
                FlowTrace.Warn("Offset", $"ApplySheathedSeatingPreview: no back socket on '{name}' — cannot preview the sheathed pose.");
                return;
            }
            if (grt.parent != back)
            {
                grt.SetParent(back, false);
                bool comp = offHand ? _offHandParentCompensate : _weaponParentCompensate;
                if (comp) CompensateParentScale(grt, offHand ? _offHandAuthoredScale : _weaponAuthoredScale);
            }

            Vector3    basePos;
            Quaternion baseRot;
            if (offHand)
            {
                basePos = _sheatheOffHandLocalPos;
                baseRot = ApplyGlobalWeaponYaw(Quaternion.Euler(_sheatheOffHandLocalEuler));
            }
            else
            {
                basePos = _sheatheWeaponLocalPos;
                baseRot = ComputeSheathRotation(back);
            }

            if (fullOverride)
            {
                grt.localPosition = pos;
                grt.localRotation = Quaternion.Euler(euler);
            }
            else
            {
                grt.localPosition = basePos + pos;
                grt.localRotation = baseRot * Quaternion.Euler(euler);
            }
        }

        /// <summary>
        /// Persist the edited offset to offsets.json (via AttachmentOffsetRegistry) under the slot's
        /// id, reload the registry, and keep the live preview. Returns the writable dev path + a
        /// copy-pasteable JSON snippet for the owner to bake into the repo offsets.json. In the
        /// editor it also writes the repo file directly. FlowTrace'd per §12.
        /// </summary>
        public bool SaveSeating(Vector3 pos, Vector3 euler, float scale, bool fullOverride,
                                out string devPath, out string snippet)
        {
            devPath = null; snippet = null;
            string key = _seatEditOffHand ? _currentOffHandMeshKey : _currentWeaponMeshKey;
            if (string.IsNullOrEmpty(key)) key = _seatEditOffHand ? _currentOffHandId : _currentWeaponId;
            if (string.IsNullOrEmpty(key))
            {
                FlowTrace.Fail("Offset", "SaveSeating: no offset key for the edited slot — nothing saved.");
                return false;
            }
            // Sheathed edits persist under "<key>@sheathed" — verified the registry does NO key
            // sanitization ('@' passes save/load/remove untouched), so the drawn entry is never clobbered.
            if (_seatEditSheathed) key += SheathedKeySuffix;
            if (scale <= 0f) scale = 1f;

            bool ok = AttachmentOffsetRegistry.SaveOffset(key, pos, euler, scale, fullOverride, out devPath, out snippet);
            if (!ok)
            {
                FlowTrace.Step("Offset", $"SaveSeating '{key}': WRITE FAILED (see warnings).");
                return false;
            }

            FlowTrace.Step("Offset",
                $"SaveSeating '{key}': pos={pos} euler={euler} scale={scale:0.###} full={fullOverride} -> {devPath}");

            // Re-seat from the persisted local config immediately — preview-only was the WYSIWYG gap.
            bool offHand    = _seatEditOffHand;
            bool sheathed   = _seatEditSheathed;
            _seatingEditActive = false;
            _seatEditSheathed  = false;
            _seatEditMode      = -1;
            EquipBestForHero();
            ApplyHoldPose();

            BeginSeatingEdit(offHand, sheathed, out _);
            ApplySeatingPreview(pos, euler, scale, fullOverride);
            return true;
        }

        /// <summary>Re-equip from the (reloaded) registry to PROVE the saved file reproduces the pose.</summary>
        public void ReapplySeatingFromRegistry()
        {
            AttachmentOffsetRegistry.Reload();
            bool wasEditing = _seatingEditActive;
            bool offHand    = _seatEditOffHand;
            bool sheathed   = _seatEditSheathed;
            _seatingEditActive = false;     // allow the re-attach to seat normally
            _seatEditSheathed  = false;
            _seatEditMode = -1;
            EquipBestForHero();
            if (wasEditing)
            {
                // Re-enter edit on the freshly attached prop so the panel stays live (same carry mode).
                BeginSeatingEdit(offHand, sheathed, out _);
            }
        }

        /// <summary>End the edit session: restore the auto sheathe/draw carry state on both props.</summary>
        public void EndSeatingEdit()
        {
            if (!_seatingEditActive) return;
            _seatingEditActive = false;
            _seatEditSheathed  = false;
            _seatEditMode = -1;
            // Re-apply the carry state so both props resume drawn (combat) / sheathed (town) cleanly.
            ApplyHoldPose();
            FlowTrace.Step("Offset", $"EndSeatingEdit on '{name}'.");
        }

        // ── Internals ──────────────────────────────────────────────────────────────
        // Scale archetype heldLength (authored for RefHeroHeightM) to this hero's measured height.
        private float ProportionalHeldLength(float archetypeMetersAtRefHero)
        {
            if (archetypeMetersAtRefHero <= 0f) return archetypeMetersAtRefHero;
            return archetypeMetersAtRefHero * (ResolveHeroHeightM() / RefHeroHeightM);
        }

        private float ResolveHeroHeightM()
        {
            if (_cachedHeroHeightM > 0.01f) return _cachedHeroHeightM;
            float measured = MeasureHeroBodyHeightM();
            _cachedHeroHeightM = measured > 0.5f ? measured : RefHeroHeightM;
            FlowTrace.Step("Equip",
                $"hero standing height={_cachedHeroHeightM:0.###}m (ref={RefHeroHeightM:0.###}m)");
            return _cachedHeroHeightM;
        }

        // Renderer bounds on HeroBody (skips equipped props) — same frame GearVisualApplier targets.
        private float MeasureHeroBodyHeightM()
        {
            var body = transform.Find("HeroBody");
            if (body == null) return 0f;
            bool any = false;
            Bounds b = default;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var n = r.gameObject.name;
                if (n.StartsWith("EquipmentProp") || n.StartsWith("GearVisual")) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? b.size.y : 0f;
        }

        private void CacheRig()
        {
            if (_animator != null && _animator.isHuman) return;
            _cachedHeroHeightM = 0f;
            using var _ = FlowTrace.Enter("Equip", $"CacheRig on '{name}'");
            // Body lives under "HeroBody" on the hero root (same convention as GearLoadout
            // / GearVisualApplier). Fall back to any child Animator.
            var body = transform.Find("HeroBody");
            _animator = body != null ? body.GetComponentInChildren<Animator>() : null;
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            FlowTrace.Step("Equip",
                $"CacheRig: animator={(_animator != null ? _animator.name : "<null>")} " +
                $"isHuman={(_animator != null && _animator.isHuman)} (body='{(body != null ? body.name : "<none>")}')");
        }

        private void DestroyCurrentWeapon()
        {
            if (_currentWeaponProp != null)
            {
                Destroy(_currentWeaponProp);
                _currentWeaponProp = null;
            }
            _weaponHand = null;   // drop the resolved draw target so a stale (old-body) hand is never reused
        }

        // ── ARMED-HERO INVARIANT (regression surface) ────────────────────────────────
        /// <summary>
        /// True when the def loads its prefab via Addressables (Blink "gear/" scheme or an
        /// explicit loadVia=="addressable"). Public so the headless DataRegression can pick the
        /// same load path this controller does when asserting the armed-hero invariant.
        /// </summary>
        public static bool IsAddressableWeapon(WeaponDef def) => LoadsViaAddressable(def);

        /// <summary>
        /// The build-safe Resources mesh path the controller would load for <paramref name="weaponId"/>
        /// on the NON-Addressable path (e.g. "Heroes/Props/Weapons/sword_A"). Resolve() never
        /// returns null for a non-empty id (it defaults to a sword family), so this always yields a
        /// path — the armed-hero guarantee: the hero attaches at worst a tinted primitive, never nothing.
        /// Public so DataRegression can assert the Resources prop exists for the auto-equipped starters.
        /// </summary>
        public static string ResolveWeaponMeshResourcePath(string weaponId)
        {
            var vis = Resolve(weaponId);
            return vis != null && !string.IsNullOrEmpty(vis.mesh)
                ? WeaponPropResourceDir + vis.mesh
                : null;
        }

        /// <summary>
        /// Map a weapons.json id -> a WeaponVisual: exact-id table first, then the catalog
        /// row's prefabPath + category (icon/title and held mesh stay in lockstep), then
        /// keyword classification on the id (future ids still resolve to a sensible family).
        /// </summary>
        private static WeaponVisual Resolve(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            if (IdMap.TryGetValue(weaponId, out var hit)) return hit;

            var def = GearCatalog.FindWeapon(weaponId);
            if (def != null)
            {
                var fromCatalog = VisualFromCatalog(def);
                if (fromCatalog != null) return fromCatalog;
            }

            string id = weaponId.ToLowerInvariant();
            // Order matters: more specific keywords first.
            if (id.Contains("bow"))     return Bow("bow_A");
            if (id.Contains("dagger"))  return Dagger("dagger_A");
            if (id.Contains("axe"))     return Axe("axe_A");
            if (id.Contains("hammer") || id.Contains("mace")) return Hammer("hammer_A");
            if (id.Contains("staff"))   return Staff("staff_A");
            if (id.Contains("wand") || id.Contains("scepter") || id.Contains("scept"))
                                        return Wand("wand_A");
            if (id.Contains("shield"))  return Shield("shield_A");
            // Job-coded ids without a weapon keyword.
            if (id.StartsWith("mage"))  return Staff("staff_A");
            if (id.StartsWith("ranger"))return Bow("bow_A");
            // Default: a sword (knight / generic melee).
            return Sword("sword_A");
        }

        // Derive the held mesh from the catalog row (ITEM_MODEL §3/§4): prefabPath names the
        // Resources prop; category picks the grip family. Blink Addressables rows keep `native`.
        private static WeaponVisual VisualFromCatalog(WeaponDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.prefabPath)) return null;

            string mesh;
            if (LoadsViaAddressable(def))
            {
                // Address is e.g. "gear/weapon/Sword1h_01" — last segment is the load key.
                int slash = def.prefabPath.LastIndexOf('/');
                mesh = slash >= 0 ? def.prefabPath.Substring(slash + 1) : def.prefabPath;
            }
            else
            {
                mesh = System.IO.Path.GetFileName(def.prefabPath);
            }
            if (string.IsNullOrEmpty(mesh)) return null;

            WeaponVisual vis = VisualForCategory(def.category, mesh);
            return LoadsViaAddressable(def) ? CopyOf(Native(vis)) : vis;
        }

        private static WeaponVisual VisualForCategory(string category, string mesh)
        {
            switch ((category ?? "").ToLowerInvariant())
            {
                case "bow":    return Bow(mesh);
                case "dagger": return Dagger(mesh);
                case "axe":    return Axe(mesh);
                case "hammer":
                case "mace":   return Hammer(mesh);
                case "staff":  return Staff(mesh);
                case "wand":   return Wand(mesh);
                case "shield": return Shield(mesh);
                default:       return Sword(mesh);
            }
        }

        /// <summary>
        /// Loads a KayKit weapon mesh from the build-safe Resources path (prefab first,
        /// then a model/fbx GameObject). Returns null when the prop hasn't been copied
        /// into Resources/Heroes/Props/Weapons yet (see file header gap note).
        /// </summary>
        private static GameObject LoadWeaponMesh(string meshName)
        {
            using var _ = FlowTrace.Enter("Equip", $"LoadWeaponMesh '{meshName ?? "<null>"}'");
            if (string.IsNullOrEmpty(meshName)) return null;
            string path = WeaponPropResourceDir + meshName;
            var prefab = Resources.Load<GameObject>(path);
            FlowTrace.Step("Equip",
                $"LoadWeaponMesh: path='{path}' prefab={(prefab != null ? "found" : "MISSING -> primitive fallback")}");
            return prefab != null ? Instantiate(prefab) : null;
        }

        /// <summary>
        /// Tinted-primitive stand-in for when the real KayKit mesh isn't in Resources yet
        /// (keeps the hero visibly armed). One thin box; NormalizeInto sizes it to heldLength.
        /// </summary>
        private static GameObject BuildFallbackPrimitive(WeaponVisual vis)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            // A thin tall box reads as a blade/haft; NormalizeInto puts the long axis up.
            go.transform.localScale = new Vector3(0.05f, 1f, 0.05f);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (sh != null)
                {
                    var mat = new Material(sh);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", vis.tint);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", vis.tint);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.6f);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);
                    mr.sharedMaterial = mat;
                }
            }
            return go;
        }

        // Bladed melee: Z thickest at hilt → handle at the short Y end. Bow/shield/staff/wand skip.
        private static bool ResolveHiltFromKind(WeaponClass kind) =>
            kind == WeaponClass.Sword || kind == WeaponClass.Dagger ||
            kind == WeaponClass.Axe || kind == WeaponClass.Hammer;

        // ── Bounds-normalize (WeaponBoundsOrient: Y-long, X-narrow, Z-wide — BINDING canon) ──
        private static void NormalizeInto(GameObject prop, Transform parent, float targetLength,
                                        bool resolveHilt = true)
            => WeaponBoundsOrient.NormalizeInto(prop, parent, targetLength,
                WeaponBoundsOrient.GripAnchor.Centre, resolveHilt);

        // Seat a NATIVE prop (authored grip-at-origin + correct orientation, e.g. Blink): trust the
        // prefab — parent at identity, scale to the target held length by the LONGEST bound, and do
        // NOT re-centre (re-centring is what moved the grip to mid-blade on the normalize path). The
        // prefab origin (the grip) stays at the gripRoot origin → the hand holds the handle, not the blade.
        private static void SeatNative(GameObject prop, Transform parent, float targetLength)
        {
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = Vector3.zero;
            prop.transform.localRotation = Quaternion.identity;
            prop.transform.localScale = Vector3.one;
            if (TryLocalBounds(prop, parent, out Bounds b))
            {
                float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (longest > 1e-4f) prop.transform.localScale = Vector3.one * (targetLength / longest);
            }
        }

        private static bool TryLocalBounds(GameObject prop, Transform parent, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (var r in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                Bounds wb = r.bounds;
                Vector3 c = parent.InverseTransformPoint(wb.center);
                Vector3 e = parent.InverseTransformVector(wb.extents);
                var lb = new Bounds(c, new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z)) * 2f);
                if (!any) { bounds = lb; any = true; } else bounds.Encapsulate(lb);
            }
            return any;
        }

        // ── WEAPON MATERIAL RECOVERY HELPERS (deal-breaker fix 2026-07-16) ────────────
        // Cached URP/Lit shader for the runtime weapon-material recovery. Shader.Find is not free,
        // so resolve once (mirrors MagentaGuard._lit). Null on a broken build => recovery no-ops loudly.
        private static Shader _urpLit;

        // A shader that renders MAGENTA (or nothing) under URP, or is missing/stripped in the build.
        // Same predicate MagentaGuard.IsBrokenShader uses — kept local so this silo never edits MagentaGuard.
        private static bool IsBrokenPropShader(Shader sh)
        {
            if (sh == null) return true;
            string sn = sh.name;
            if (string.IsNullOrEmpty(sn)) return true;
            return sn == "Standard"
                || sn == "Standard (Specular setup)"
                || sn.StartsWith("Legacy Shaders/")
                || sn.IndexOf("InternalError", System.StringComparison.Ordinal) >= 0;
        }

        // Recover every broken-shader material on the just-attached weapon prop to a FRESH URP/Lit
        // (carrying colour + albedo + emission), assigned back into the renderer's sharedMaterials so
        // the recovery STICKS in the built player. Idempotent + null-guarded (a valid-URP prop is a no-op).
        private static void RecoverWeaponMaterialsToUrp(GameObject prop, string weaponId)
        {
            if (prop == null) return;
            if (_urpLit == null) _urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (_urpLit == null)
            {
                FlowTrace.Warn("HeroWeapon",
                    $"no URP/Lit shader found — cannot recover weapon '{weaponId ?? "<null>"}' material (may render magenta/invisible).");
                return;
            }

            var freshFor = new Dictionary<Material, Material>();
            int scanned = 0, recovered = 0;
            foreach (var r in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var work = r.sharedMaterials;
                if (work == null) continue;
                bool changed = false;
                for (int i = 0; i < work.Length; i++)
                {
                    var m = work[i];
                    if (m == null) continue;
                    scanned++;
                    if (!IsBrokenPropShader(m.shader)) continue;   // valid URP/Unlit -> leave it alone
                    string dead = m.shader != null ? m.shader.name : "<null>";
                    if (!freshFor.TryGetValue(m, out var fresh))
                    {
                        fresh = BuildRecoveredWeaponMaterial(m);   // fresh URP/Lit, once per unique source
                        freshFor[m] = fresh;
                        recovered++;
                        // ROOT FIX 2026-07-19 (device: knight_starter magenta): the source .mat
                        // (Blink MegaWeaponPack1/LowPolyWeaponMegaPack) is now re-authored to ship URP/Lit,
                        // so on a correctly-imported build this recovery does NOT fire at all (the shader
                        // reads URP/Lit -> IsBrokenPropShader false -> skipped). It STILL fires as a backstop
                        // only when a gitignored pack is re-imported fresh and its .mat reverts to Built-in
                        // Standard. That is a KNOWN, FULLY-HANDLED condition — the fresh URP/Lit carries the
                        // authored albedo/colour, so the weapon renders correctly. It is therefore NOT a live
                        // break: log Step (Player.log only), NOT Fail (which would spam the F8 break-log as a
                        // false live-break on every equip). A genuinely unrecoverable case (URP/Lit shader not
                        // found) still Fails, above.
                        FlowTrace.Step("HeroWeapon",
                            $"weapon '{weaponId ?? "<null>"}' material '{m.name}' shipped on dead shader '{dead}' -> " +
                            "auto-healed to FRESH URP/Lit (assigned to renderer, sticks in build). Expected only when a " +
                            "gitignored weapon pack is re-imported to Built-in Standard; source .mat now ships URP/Lit.");
                    }
                    work[i] = fresh;
                    changed = true;
                }
                if (changed) r.sharedMaterials = work;   // the assignment is what makes the recovery stick
            }
            FlowTrace.Step("HeroWeapon",
                $"weapon '{weaponId ?? "<null>"}' material recovery: scanned {scanned} slot(s), recovered {recovered} " +
                "broken-shader material(s) to URP/Lit (0 recovered = prop already valid URP).");
        }

        // Build a FRESH URP/Lit carrying the authored colour + albedo + emission read robustly off the
        // dead/stripped SOURCE (mirrors MagentaGuard.BuildRecoveredMaterial). HasProperty-gated reads are
        // safe here: this runs on the owner's real GPU where the shader table resolves; the -nographics
        // fleet never equips a real weapon prop through this path. Defaults to white, never magenta.
        private static Material BuildRecoveredWeaponMaterial(Material src)
        {
            Color col = Color.white;
            if (src != null && src.HasProperty("_Color")) col = src.GetColor("_Color");
            else if (src != null && src.HasProperty("_BaseColor")) col = src.GetColor("_BaseColor");

            Texture tex = null;
            if (src != null && src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
            if (tex == null && src != null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");

            Color emis = (src != null && src.HasProperty("_EmissionColor")) ? src.GetColor("_EmissionColor") : Color.black;

            var fresh = new Material(_urpLit)
            { name = ((src != null && src.name != null) ? src.name : "Weapon") + "_UrpRecovered" };
            if (fresh.HasProperty("_BaseColor")) fresh.SetColor("_BaseColor", col);
            if (fresh.HasProperty("_Color")) fresh.SetColor("_Color", col);
            if (tex != null)
            {
                if (fresh.HasProperty("_BaseMap")) fresh.SetTexture("_BaseMap", tex);
                if (fresh.HasProperty("_MainTex")) fresh.SetTexture("_MainTex", tex);
            }
            if (emis != Color.black && fresh.HasProperty("_EmissionColor"))
            {
                fresh.SetColor("_EmissionColor", emis);
                fresh.EnableKeyword("_EMISSION");
            }
            if (fresh.HasProperty("_Surface")) fresh.SetFloat("_Surface", 0f);   // URP: 0 = Opaque
            if (fresh.HasProperty("_ZWrite"))  fresh.SetFloat("_ZWrite", 1f);
            fresh.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            return fresh;
        }
    }

    /// <summary>
    /// Marker on a hero ROOT whose body BAKES its own weapon/shield/helmet (the Paladin hero package,
    /// loaded via ff.heropackage in HeroBodySwapper.BuildPackageHeroBody). EquipmentController checks
    /// for it (PackageBakedGear) and SKIPS the KayKit weapon-mesh + shield-mesh prop attach so the baked
    /// gear is the only gear shown — no duplicate second sword, no wrongly-oriented attached shield
    /// (owner F8 2026-07-03). Added by the swapper's package wiring BEFORE the EquipmentController, so even
    /// the first synchronous equip on AddComponent already sees it. Loadout/stat/armor-tint are unaffected;
    /// only the visible prop attach is suppressed. The legacy Tripo Knight never carries this marker, so
    /// its attach path stays byte-for-byte intact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PackageBakedGearMarker : MonoBehaviour { }
}
