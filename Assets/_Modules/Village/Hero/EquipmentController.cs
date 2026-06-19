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
using DeNelle.Core.Diagnostics;

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
            heldLength = 0.95f, tint = new Color(0.74f, 0.75f, 0.78f)
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
            gripPos = new Vector3(-0.05f, 0f, 0f), gripEuler = new Vector3(0f, -10f, 0f),
            heldLength = 0.55f, tint = new Color(0.58f, 0.60f, 0.64f)
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
        private GearLoadout _loadout;
        private GameObject _currentWeaponProp;
        private string _currentWeaponId;
        private int _armorTier;

        // OFF-HAND (shield) prop — attached to the OFF hand (LeftHand) alongside the main weapon.
        // Mirrors the main-hand prop lifecycle (destroy-on-swap, no stacking). Driven off
        // GearLoadout.EquippedOffHand on the SAME OnGearChanged event the main weapon uses.
        private const string OffHandPropName = "EquipmentProp_OffHand";
        private GameObject _currentOffHandProp;
        private string _currentOffHandId;

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
        private Vector3 _baseGripEuler;       // the weapon's neutral grip rotation
        private bool _combatActive;           // current hold state (false = idle/lowered)
        private bool _combatExplicit;         // a caller drove SetCombatActive -> stop auto-mirroring
        private WaveManager _waveManager;     // auto-fallback combat signal (same as locomotion)

        // Idle = sword LOWERED at the side: tilt the blade down/back from the ready pose.
        // Combat = sword DRAWN/raised: the weapon's neutral (base) grip euler.
        // These are additive offsets on the grip root's LOCAL rotation, applied on top of
        // the per-weapon base grip. Tune on playtest; only melee blades visibly read it.
        private static readonly Vector3 IdleHoldOffsetEuler   = new Vector3(55f, 0f, 0f);
        private static readonly Vector3 CombatHoldOffsetEuler = new Vector3(0f, 0f, 0f);

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
        [SerializeField] private Vector3 _swordGripEuler = new Vector3(-25f, 0f, 0f);

        // WO-435: per-archetype calibration nudges, the staff/mace/etc. equivalents of
        // _swordGripEuler. ALL melee now run the same rig-aware grip path (bounds-derived
        // SeatByHandle + ComputeMeleeGripRotation); these are the additive manual-correction
        // nudges applied ON TOP in the corrected local frame, treated as CANON (never auto-
        // overwritten, per WEAPON_ARMOR_ORIENT_LOGIC §4). Defaulted to ZERO so generalizing
        // the path does NOT regress the existing look — only the sword keeps its proven -25°
        // nudge above. Inspector-exposed for tuning each family against the real rig with no
        // recompile. (Dagger reuses _swordGripEuler — same bladed archetype.)
        [SerializeField] private Vector3 _staffGripEuler = new Vector3(0f, 0f, 0f);
        [SerializeField] private Vector3 _wandGripEuler  = new Vector3(0f, 0f, 0f);
        [SerializeField] private Vector3 _axeGripEuler   = new Vector3(0f, 0f, 0f);
        [SerializeField] private Vector3 _maceGripEuler  = new Vector3(0f, 0f, 0f);

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
            // Auto-read the equipped weapon on enable (the same data the cube path uses).
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
            DestroyCurrentOffHand();
        }

        private void HandleGearChanged() => EquipBestForHero();

        /// <summary>
        /// Re-reads the hero's currently equipped weapon from GearLoadout and shows the
        /// matching mesh. This is the hook into the EXISTING equip-change event — no new
        /// gear model. Safe to call repeatedly (idempotent on an unchanged id).
        /// </summary>
        public void EquipBestForHero()
        {
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
            Transform hand = FlowTrace.Try("Equip", $"GetBoneTransform({boneId})",
                () => _animator.GetBoneTransform(boneId), null);
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
                FlowTrace.Warn("Gear", $"Addressable load threw for '{address}': {ex.Message} — " +
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
                    FlowTrace.Warn("Gear", $"Addressable load FAILED for '{address}' " +
                        $"(status={op.Status}) — falling back to Resources map (hero stays armed).");
                    ReleaseWeaponHandle();
                    FallbackResourcesAttach(vis, hand, weaponId);
                    return;
                }

                // Blink prefab is authored grip-at-origin + oriented → seat as NATIVE. Copy the
                // preset so we never flip `native` on the shared cached IdMap instance.
                var nativeVis = CopyOf(vis);
                nativeVis.native = true;
                GameObject prop = Instantiate(op.Result);
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

            // Seat via a grip root.
            //  • BUG 2 FIX: a MELEE weapon NEVER trusts a "native" pivot. The Blink/Tripo sword
            //    prefabs are tagged native (grip-at-origin), but their authored pivot is NOT actually
            //    at the handle — so SeatNative left the hilt floating and the blade clipped the fist.
            //    Per §4 (derive the grip from mesh bounds + name, do NOT trust a wrong pivot), every
            //    melee weapon now runs the SAME proven KayKit path: NormalizeInto (bounds-orient,
            //    longest->+Y) -> SeatByHandle (re-seat the HANDLE end at the origin) -> the rig-hand-
            //    axis rotation (ComputeMeleeGripRotation, applied below). The hilt sits in the palm.
            //  • NON-melee native props (none reach here today — bow/shield use their own paths) keep
            //    SeatNative (trust the authored grip-at-origin). The `native` flag now only gates
            //    NON-melee seating.
            var gripRoot = new GameObject(PropName);
            bool meleeSeat = IsMelee(vis.kind);
            if (vis.native && !meleeSeat)
            {
                FlowTrace.Step("Equip", "seat: NATIVE (trust authored grip-at-origin, scale-only)");
                SeatNative(prop, gripRoot.transform, vis.heldLength);
            }
            else
            {
                if (vis.native && meleeSeat)
                    FlowTrace.Step("Equip", "seat: MELEE override — native pivot NOT trusted (BUG 2 §4 derive)");
                else
                    FlowTrace.Step("Equip", "seat: NormalizeInto + (melee) SeatByHandle (derive grip from bounds)");
                NormalizeInto(prop, gripRoot.transform, vis.heldLength);
                // GRIP-POINT INFERENCE (WO-435 — generalized to ALL melee): NormalizeInto centres
                // by BOUNDS (mid-shaft / mid-blade), not the grip. SeatByHandle re-seats so the
                // HANDLE (the narrow end below the head/crossguard) sits at the origin and the
                // head/blade points +Y. The width-spike profile finds a staff's grip (or falls back
                // to the narrower-tipped pommel end) exactly as it does for a sword.
                if (meleeSeat)
                    FlowTrace.Try("Equip", "SeatByHandle", () => SeatByHandle(prop, gripRoot.transform));
            }

            gripRoot.transform.SetParent(hand, false);
            gripRoot.transform.localPosition = vis.gripPos;

            // Hold state: store the base grip euler so the idle<->combat pose can offset
            // from it; apply the current pose immediately.
            _gripRoot = gripRoot.transform;
            _baseGripEuler = vis.gripEuler;
            _currentWeaponProp = gripRoot;

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
            // BUG 2 FIX: ALL melee (native Blink swords included) now use the rig-hand-axis rotation,
            // because melee is seated via the derived NormalizeInto+SeatByHandle path above (its
            // primary axis is prop-local +Y) — NOT the authored native frame. Only NON-melee native
            // props use their preset gripEuler directly.
            if (IsMelee(vis.kind))
                _baseGripRot = ComputeMeleeGripRotation(vis.kind);
            else
                _baseGripRot = Quaternion.Euler(_baseGripEuler);

            FlowTrace.Step("Equip", $"attached '{weaponId}' on '{name}': gripPos={vis.gripPos} " +
                $"baseEuler={_baseGripRot.eulerAngles} kind={vis.kind} native={vis.native}");
            ApplyHoldPose();
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

        // ── SWORD GRIP-POINT INFERENCE ───────────────────────────────────────────────
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

        // Bin mesh vertices along prop-local Y; record the max cross-section half-extent
        // (max(|x|,|z|)) per bin. Vertices are transformed mesh-local -> parent-local so the
        // profile is measured in the same frame the grip math uses.
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
                    float w = Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.z));
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
                    float w = Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.z));
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
        /// call repeatedly (idempotent on an unchanged id). NOTE: the off-hand never uses the
        /// Addressable async path — current shield rows are Resources/Tripo (build-safe + sync).
        /// </summary>
        public void EquipOffHand(WeaponDef def)
        {
            string id = def != null ? def.id : null;

            // Idempotent: same off-hand already shown -> nothing to do.
            if (string.Equals(_currentOffHandId, id, System.StringComparison.OrdinalIgnoreCase)
                && _currentOffHandProp != null)
                return;

            DestroyCurrentOffHand();
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

            Transform hand = FlowTrace.Try("Equip", "GetBoneTransform(LeftHand)",
                () => _animator.GetBoneTransform(HumanBodyBones.LeftHand), null);
            if (hand == null)
            {
                FlowTrace.Fail("Equip", $"Humanoid rig on '{name}' has NO LeftHand bone — " +
                    $"off-hand '{id}' NOT attached.");
                return;
            }

            GameObject prop = LoadWeaponMesh(vis.mesh) ?? BuildFallbackPrimitive(vis);
            if (prop == null) { FlowTrace.Fail("Equip", $"off-hand prop null for mesh '{vis.mesh}'"); return; }

            FlowTrace.Step("Equip", $"off-hand seated: id='{id}' mesh='{vis.mesh}' hand='{hand.name}'");
            AttachOffHandProp(prop, vis, hand, id);
        }

        // Seat an off-hand prop on the off hand. Shields are centre-gripped (their own NormalizeInto
        // + preset euler, like the bow) — they do NOT run the melee handle-inference / rig-axis
        // rotation. Kept separate from the main-hand AttachLoadedProp so the off-hand prop has its
        // own lifecycle reference (no clobbering the main weapon's grip-root / hold-pose state).
        private void AttachOffHandProp(GameObject prop, WeaponVisual vis, Transform hand, string id)
        {
            prop.name = OffHandPropName;
            foreach (var c in prop.GetComponentsInChildren<Collider>(true)) if (c != null) Destroy(c);
            foreach (var rb in prop.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) Destroy(rb);

            var gripRoot = new GameObject(OffHandPropName);
            NormalizeInto(prop, gripRoot.transform, vis.heldLength);

            gripRoot.transform.SetParent(hand, false);
            gripRoot.transform.localPosition = vis.gripPos;
            gripRoot.transform.localRotation = Quaternion.Euler(vis.gripEuler);

            _currentOffHandProp = gripRoot;
        }

        private void DestroyCurrentOffHand()
        {
            if (_currentOffHandProp != null)
            {
                Destroy(_currentOffHandProp);
                _currentOffHandProp = null;
            }
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

            if (_combatExplicit || _gripRoot == null) return;
            if (_waveManager == null) _waveManager = Object.FindObjectOfType<WaveManager>();
            bool active = _waveManager != null &&
                          (_waveManager.Phase == WavePhase.Countdown ||
                           _waveManager.Phase == WavePhase.Active);
            if (active != _combatActive)
            {
                _combatActive = active;
                ApplyHoldPose();
            }
        }

        // Apply the held-low vs held-ready local rotation to the grip root (attach-level —
        // no animation clip). Additive on top of the per-weapon base grip euler.
        // NOTE (animation hook): if a future pass wants a true arm pose (hand drops to the
        // hip for idle, raises for combat), drive ActorAnimator.SetCombatStance from the
        // same SetCombatActive call — the prop offset here and the body pose stay in sync.
        private void ApplyHoldPose()
        {
            if (_gripRoot == null) return;
            Vector3 offset = _combatActive ? CombatHoldOffsetEuler : IdleHoldOffsetEuler;
            // _baseGripRot is the corrected READY orientation (rig-derived for swords, the
            // per-weapon preset otherwise). The idle/combat hold tilt composes on top of it
            // so the hold offset is relative to the corrected ready pose (no double-apply).
            _gripRoot.localRotation = _baseGripRot * Quaternion.Euler(offset);
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

        // ── ARMOR STUB ───────────────────────────────────────────────────────────
        /// <summary>
        /// Entry point for armor visuals. Wired now so callers (GearLoadout, shop/equip
        /// UI) can drive it, but visually a NO-OP today.
        /// </summary>
        // WIRE STATUS: GearLoadout now CALLS this with the worn armor's tier on every equip
        // change (GearLoadout.PushArmorTierToBody), so the tier flows in live. The VISUAL is
        // still intentionally a recorded NO-OP — body armor art (textures/plate meshes) is not
        // yet authored, and the task brief is explicit: do the paper-doll display, but do NOT
        // force a risky body-mesh attach. The moment armor art lands, implement the swap here
        // (material/texture on HeroBody, or plate pieces parented to Chest/Shoulder/Leg bones
        // keyed off _armorTier) and the wire above lights it up with no further plumbing.
        public void SetArmorTier(int tier)
        {
            _armorTier = Mathf.Max(0, tier);
            // No body visual yet (art-gated) — tier recorded for the future visual pass.
        }

        /// <summary>Current armor tier (0 = none). Exposed for the future armor visual pass.</summary>
        public int ArmorTier => _armorTier;

        // ── Internals ──────────────────────────────────────────────────────────────
        private void CacheRig()
        {
            if (_animator != null && _animator.isHuman) return;
            // Body lives under "HeroBody" on the hero root (same convention as GearLoadout
            // / GearVisualApplier). Fall back to any child Animator.
            var body = transform.Find("HeroBody");
            _animator = body != null ? body.GetComponentInChildren<Animator>() : null;
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        private void DestroyCurrentWeapon()
        {
            if (_currentWeaponProp != null)
            {
                Destroy(_currentWeaponProp);
                _currentWeaponProp = null;
            }
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
        /// Map a weapons.json id -> a WeaponVisual: exact-id table first, then keyword
        /// classification on the id (so future ids still resolve to a sensible family).
        /// </summary>
        private static WeaponVisual Resolve(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            if (IdMap.TryGetValue(weaponId, out var hit)) return hit;

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

        /// <summary>
        /// Loads a KayKit weapon mesh from the build-safe Resources path (prefab first,
        /// then a model/fbx GameObject). Returns null when the prop hasn't been copied
        /// into Resources/Heroes/Props/Weapons yet (see file header gap note).
        /// </summary>
        private static GameObject LoadWeaponMesh(string meshName)
        {
            if (string.IsNullOrEmpty(meshName)) return null;
            string path = WeaponPropResourceDir + meshName;
            var prefab = Resources.Load<GameObject>(path);
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

        // ── Bounds-normalize (generalized from HeroBowAttachment.NormalizeInto) ──────
        // Parents `prop` under `parent`, orienting its LONGEST axis -> parent +Y, its
        // NARROWEST axis -> parent +X, bounds-centre at the origin, scaled so the longest
        // axis is `targetLength` m. Deterministic from renderer bounds — any weapon FBX
        // lands right without hand-guessed Euler. (Kept self-contained so this controller
        // doesn't depend on HeroBowAttachment's private helper.)
        private static void NormalizeInto(GameObject prop, Transform parent, float targetLength)
        {
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = Vector3.zero;
            prop.transform.localRotation = Quaternion.identity;
            prop.transform.localScale = Vector3.one;

            if (!TryLocalBounds(prop, parent, out Bounds b0)) return;
            Vector3 sz = b0.size;
            int lng = (sz.x >= sz.y && sz.x >= sz.z) ? 0 : (sz.y >= sz.z ? 1 : 2);
            int sht = (sz.x <= sz.y && sz.x <= sz.z) ? 0 : (sz.y <= sz.z ? 1 : 2);
            if (sht == lng) sht = (lng + 1) % 3;

            Quaternion alignLong = Quaternion.FromToRotation(Axis(lng), Vector3.up);
            prop.transform.localRotation = alignLong;

            Vector3 shortAfter = alignLong * Axis(sht); shortAfter.y = 0f;
            if (shortAfter.sqrMagnitude > 1e-5f)
                prop.transform.localRotation =
                    Quaternion.FromToRotation(shortAfter.normalized, Vector3.right) * alignLong;

            if (TryLocalBounds(prop, parent, out Bounds b1) && b1.size.y > 1e-4f)
                prop.transform.localScale = Vector3.one * (targetLength / b1.size.y);

            if (TryLocalBounds(prop, parent, out Bounds b2))
                prop.transform.localPosition -= b2.center;
        }

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

        private static Vector3 Axis(int i) =>
            i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

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
    }
}
