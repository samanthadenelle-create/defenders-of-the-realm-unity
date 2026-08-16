// =============================================================================
// GearAura - WO-888: the persistent aura an EQUIPPED ITEM grants the hero.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Registry section 6c: "the Aura beat sourced from an item, not a cast". A heal relic
// gives the body a soft RISING restoration column; an elemental weapon faintly smoulders
// on its own socket. Mirrors ArcaneAura's handle discipline, but holds POOLED VFXType
// loops rather than instantiating art.
// (It used to also cite Pets/AuraController as prior art for the "seat" idea; that file
// was RETIRED by WO-993 with the physical pet stack. GearAura is unaffected and stays —
// it is the HERO's equipped-item aura and never had a pet dependency.)
//
// ## THE RISK THIS COMPONENT EXISTS TO CONTAIN
//
// These are PERSISTENT loops with no natural end - they last as long as the item is worn,
// which can be the whole session. A loop played fire-and-forget permanently consumes one
// of the 20 global slots (VFXManager._maxActiveLoops) and every later aura in the session
// is silently dropped. So the shape here is deliberate:
//
//   * TWO SEATS, TWO SINGLE-SLOT FIELDS. One weapon-socket handle, one body handle. Each
//     seat is a single field, not a collection, so a seat physically cannot hold two loops
//     and "the old aura is still running under the new one" is not representable. Refresh
//     stops a seat before it starts it.
//   * WORST CASE THIS COMPONENT HOLDS 2 LOOPS. Never more, whatever the loadout.
//   * EVERY EXIT PATH STOPS THEM: an equip change that resolves a different (or no) aura
//     (Refresh), unequip (Refresh via OnGearChanged), the socket bone disappearing under a
//     body swap (the re-seat check in Refresh), OnDisable, OnDestroy, and scene unload.
//   * SELF-SUBSCRIBING. Refresh is driven by GearLoadout.OnGearChanged from inside this
//     component, so it does not depend on any particular visual path running.
//
//     WHY THAT MATTERS (reality differed from the work order): WO-888 names
//     GearVisualApplier.Apply as the seam. That call is made - but Apply RETURNS EARLY on
//     its very first branch unless GearVisualApplier.EnablePrimitiveGear is true, and that
//     master switch is OFF by default (placeholder cubes are retired); worse,
//     HeroBodySwapper SKIPS the GearVisualApplier call entirely on the package / KnightV3
//     body paths. Hanging a persistent aura off that seam alone would have shipped a
//     feature that never runs on the real hero body. The Ensure call is still placed there
//     (before the early return) because it is the documented seam, but the subscription is
//     what actually guarantees the refresh.
//
// ## WO-930 (owner felt-test 2026-08-08) - TWO DEFECTS, BOTH ON THE WEAPON SEAT
//
// 1. "even when I switched weapons from the flame blade to the regular sword, the VFX stayed."
//    PROVEN NOT to be a missing notification: Player.log carries
//      [Flow:GearAura] released weapon aura 'Aura_Flame' (reason=equip change).
//      [Flow:GearAura] weapon aura: none (no elemental brand on the equipped weapon)
//    so the equip path DOES reach StopWeapon (both the arena auto-equip and the manual
//    GearLoadout.EquipWeaponById path raise OnGearChanged). What that line did NOT prove is that
//    anything was actually torn down - it was emitted purely on `_weaponHandle != null`, while
//    VFXHandle.Stop() silently no-ops on a stale reference (VFXHandle.cs:85) and the VFXType
//    pool-return's reparent is REFUSED by Unity mid-(de)activation (VFXManager.cs:900 - the Hovl
//    return path was hardened for that at VFXManager.Hovl.cs:397, the VFXType path was not, and
//    the same session logged the refusal for [VFX_Aura_LowHealth] and [VFX_Aura_EnemyCaster]).
//    StopWeapon now holds the instance itself, CHECKS it after Stop, forces it down and
//    FlowTrace.Fails when it survived - so the trace can no longer assert a teardown it never
//    verified, and the orphan cannot outlive the swap whichever of the two paths dropped it.
//
// 2. "they have it coming from the hilt ... what they're doing isn't really flame, it's more like
//    a red smoke." Both are properties of WHERE and HOW the recipe was seated, measured at source
//    - see the constants block. The seat is now the measured BLADE, above the grip, emitting along
//    its length, with the recipe's smoke / ground-shell layers muted while it is worn.
//
// ## WO-959 (owner ruling 2026-08-10, F8 seq 2297) - DRAWN-ONLY WEAPON AURAS
//
// "can we agree to only show the flames on the sword when unsheathed?" - an element weapon aura
// renders ONLY while the weapon is DRAWN. "Unsheathed" maps to the REAL carry state at HEAD:
// EquipmentController.IsWeaponDrawn (its _combatActive, the flag ApplyHoldPose physically seats
// the prop by - hand when drawn, back socket when sheathed - driven per-frame by HeroLocomotion's
// engagement signal / the BattleLock+wave auto-mirror). The gate is ONE clause in Refresh: the
// weapon-seat WANT resolves to None while not drawn, so acquire-on-draw and release-on-sheathe
// both ride the existing verified StartWeapon/StopWeapon paths and cover ALL element auras, not
// just flame. The edge that triggers the re-resolve is EquipmentController.OnCarryStateChanged,
// raised AFTER ApplyHoldPose re-seats the prop (so the blade measure sees it at its new parent);
// the throttled Update reseat check is the belt-and-braces release if that event was missed.
// The BODY seat (heal relics) is NOT gated - the ruling is about weapons.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Holds up to one weapon-socket aura and one body aura for the hero's equipped gear,
    /// re-resolved on every equip change and stopped on every teardown path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GearAura : MonoBehaviour
    {
        // -- Tier bones. GearProgression levels the SAME item in place (WO-808), so a
        // levelled weapon should read as more present without becoming a different effect.
        // Escalation is by SIZE + DENSITY only - never hue (owner is red/green colourblind).
        // Level 1 is identity so a freshly equipped item looks exactly like the recipe.
        private const float TierScalePerLevel    = 0.10f;   // +10% size per level over 1
        private const float TierEmissionPerLevel = 0.15f;   // +15% density per level over 1
        private const float TierMaxMul           = 1.8f;    // ceiling, so a maxed item cannot swamp the hero

        // -- Seating. The item aura is FAINT by design: it is ambient presence, not a combat
        // read, and it must never compete with the HP-state aura, which IS a survival read.
        //
        // ## WEAPON SEAT RE-TUNED 2026-08-08 (owner felt-test, verbatim: "they have it coming
        //    from the hilt ... what they're doing isn't really flame, it's more like a red smoke")
        //
        // MEASURED AT SOURCE off the catalogued recipe (Lana Fire_medium, VFXCatalog.asset row
        // Type:36 -> guid f3d1c3c1..., = Assets/Lana Studio/Casual RPG VFX/Prefabs/Fire/Fire_medium.prefab):
        // it is a CAMPFIRE, five layers, and every one of them has its SHAPE MODULE DISABLED except
        // `sparks`. A disabled shape module emits from a POINT at the transform origin - and the
        // transform it was parented to is the RightHand BONE, which is the hilt. That is complaint
        // one, exactly, and it is a property of the asset, not of the tuning.
        // The layer rates measure: Smoke 50/sec (size 0.8 soft blobs) > Fire 40 > Fire_glow 20 >
        // sparks 15 > root shell 5 (size 3). Thinned to 45% density and 35% scale, the layers that
        // survive the thinning legibly are the two BIG SOFT ones - Smoke and the root shell - which
        // is complaint three ("red smoke"). So the weapon seat now (a) measures the blade and emits
        // ALONG it above the grip, (b) mutes Smoke + the oversized root shell, and (c) stops
        // starving the layers that actually read as fire.
        private const float WeaponAuraScaleMul = 0.18f;   // flames that LICK a 1.2m blade, not engulf it
        private const float WeaponAuraDensity  = 1.00f;   // authored density: the flame layers must read continuous
        private const float BodyAuraScaleMul   = 0.50f;   // Aura_ItemHeal ships at scale 1.25 (measured) - halve it
        private const float BodyAuraDensity    = 1.00f;   // already authored sparse (1.8/sec measured)

        // -- Blade seating (WO-930). The weapon prop EquipmentController builds under the hand is
        // named "EquipmentProp_Weapon" (EquipmentController.cs:69 `PropName`, used for BOTH the grip
        // root and the mesh child). Found under the hand first, then anywhere on the rig (a prop
        // caught mid-reparent by the carry-state re-seat). NOTE (WO-959): a SHEATHED weapon no
        // longer carries its aura at all - the drawn-only gate in Refresh releases it - so the
        // rig-wide search is measurement robustness, not a sheathed-smoulder feature any more.
        private const string WeaponPropName = "EquipmentProp_Weapon";

        // Fraction of the measured grip->tip span kept CLEAR of flame at the grip end. The owner's
        // words: "don't cover the handle ... from the hilt up to the crest of the sword". Driven off
        // the measured length, so a dagger and a greatsword both keep a proportional grip clear.
        private const float HandleClearFrac = 0.30f;

        // How much wider than the blade's own cross-section the flame column is allowed to be. >1 so
        // the fire reads as wrapping the steel rather than hiding inside it.
        private const float BladeFlameWidthMul = 1.8f;

        // Below this measured world length the "blade" is not a blade (a missing mesh, a prop caught
        // mid-swap, a shield). WITHHELD rather than fall back to the hand - a point source on the
        // hand is the exact defect being fixed, so re-introducing it as a fallback would be a lie.
        private const float MinBladeLength = 0.15f;

        // Metres above the hero root to seat the BODY aura, so the restoration column reads
        // as rising from the chest rather than out of the ground. Kept low: the phone is
        // LANDSCAPE 2670x1200 and anything that grows upward spends the scarce axis.
        private const float BodyAuraHeight = 0.9f;

        // Re-resolve throttle for the socket-bone re-seat check (a body swap can replace the
        // rig under a live aura). Cheap check, but no reason to do it every frame.
        private const float ReseatCheckSeconds = 1.0f;

        // Retry cadence while a weapon aura is WANTED but withheld (the blade has not been
        // attached yet). Faster than the held-aura check so the flame appears within a couple of
        // frames of the prop landing rather than a second later; the work is one bounds measure
        // over the two renderers a weapon prop carries.
        private const float PendingRetrySeconds = 0.2f;

        // -- Seat 1: the weapon socket. ONE handle. --
        private VFXHandle _weaponHandle;
        private VFXType   _weaponType = VFXType.None;
        private Transform _weaponSocket;

        // DIRECT reference to the instance the handle owns.
        //
        // WHY A SECOND REFERENCE EXISTS (the whole of defect 1, owner 2026-08-08: "even when I
        // switched weapons from the flame blade to the regular sword, the VFX stayed"):
        // VFXHandle.Stop() is a SILENT NO-OP when its own GameObject reference is already null or
        // stale (VFXHandle.cs:85 `if (_go == null) return;`), and the VFXType pool-return it routes
        // to does an UNGUARDED `transform.SetParent(_poolRoot, false)` (VFXManager.cs:900) which
        // Unity REFUSES, with a logged error, when the host is mid-(de)activation - the WO-929
        // class. Neither of those tells the caller anything. StopWeapon used to log "released
        // weapon aura" purely on `_weaponHandle != null`, so the trace ASSERTED a teardown it had
        // never checked. Holding the instance ourselves is what turns that claim into a
        // verification: after Stop we look at the object and, if it is still live, we say so
        // (FlowTrace.Fail) and put it down ourselves.
        private GameObject _weaponAuraGo;

        // The measured blade frame, cached in the weapon PROP's local space so it survives the
        // prop moving (swing, sheathe) without re-measuring bounds every frame.
        private Transform _bladeAnchor;
        private Vector3   _bladeLocalCenter;
        private Vector3   _bladeLocalDir;
        private float     _bladeWorldLength;
        private float     _bladeWorldHalfWidth;

        // Per-layer shape state we overwrote to stretch a POINT emitter into a BLADE-LENGTH one,
        // and the pristine values to hand back. Same discipline (and the same reason) as
        // VfxLoopModulator: these are POOLED instances, and ReturnToPool resets nothing it did not
        // itself change - so a shaped instance handed to the next user would be silently wrong
        // forever, with no error anywhere.
        private ShapedLayer[] _shapedLayers;

        /// <summary>One layer's pristine shape/emission state while the blade override is applied.</summary>
        private struct ShapedLayer
        {
            public ParticleSystem            Ps;
            public bool                      WasShapeEnabled;
            public ParticleSystemShapeType   WasShapeType;
            public Vector3                   WasShapeScale;
            public Vector3                   WasShapeRotation;
            public Vector3                   WasShapePosition;
            public float                     WasShapeRadius;
            public bool                      WasEmissionEnabled;
            public bool                      Muted;
        }

        // -- Seat 2: the body. ONE handle. --
        private VFXHandle _bodyHandle;
        private VFXType   _bodyType = VFXType.None;

        private GearLoadout _loadout;
        private float _nextReseatCheck;

        // WO-959: the carry-state authority. EquipmentController owns drawn/sheathed (it is the
        // component that physically seats the prop hand vs back socket), so its IsWeaponDrawn is
        // the state and its OnCarryStateChanged is the edge that acquires/releases the weapon
        // seat. Lazily bound: on the companion build path the controller can be added AFTER this
        // component, so every drawn-state read retries the bind (see BindCarryState).
        private EquipmentController _equipment;
        private bool _carrySubscribed;

        /// <summary>True when the loadout WANTS a weapon aura that is not currently held (blade not
        /// attached yet, loop cap hit, socket missing) - the flag that keeps Update retrying.</summary>
        private bool _weaponPending;

        /// <summary>The aura types currently held (VFXType.None when a seat is empty). For diagnostics.</summary>
        public VFXType HeldWeaponAura => _weaponHandle != null ? _weaponType : VFXType.None;
        /// <summary>See <see cref="HeldWeaponAura"/>.</summary>
        public VFXType HeldBodyAura   => _bodyHandle   != null ? _bodyType   : VFXType.None;

        /// <summary>Loops currently held by this component. 0..2 - the budget contribution.</summary>
        public int HeldLoopCount => (_weaponHandle != null ? 1 : 0) + (_bodyHandle != null ? 1 : 0);

        // =====================================================================
        //  Lifecycle - each of these is an EXIT PATH
        // =====================================================================

        private void OnEnable()
        {
            _loadout = GetComponent<GearLoadout>();
            if (_loadout != null) _loadout.OnGearChanged += Refresh;
            BindCarryState();
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Refresh();
        }

        private void OnDisable()
        {
            if (_loadout != null) _loadout.OnGearChanged -= Refresh;
            if (_equipment != null && _carrySubscribed) _equipment.OnCarryStateChanged -= HandleCarryStateChanged;
            _carrySubscribed = false;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            StopAll("OnDisable");
        }

        private void OnDestroy() => StopAll("OnDestroy");

        private void OnSceneUnloaded(Scene _) => StopAll("sceneUnloaded");

        private void Update()
        {
            // A held socket aura whose bone vanished (body swap / rig rebuild) is parented to
            // a destroyed transform. Unity reparents it to the scene root, so it would keep
            // playing in place forever - an orphan loop, the exact failure ArcaneAura's owner-
            // liveness guard was added for. Cheap throttled self-check, same idea.
            if (Time.time < _nextReseatCheck) return;

            if (_weaponHandle == null)
            {
                // WANTED BUT WITHHELD - and it must keep retrying or it never appears at all.
                // GearLoadout raises OnGearChanged BEFORE it re-applies visuals
                // (GearLoadout.EquipWeaponById: `OnGearChanged?.Invoke(); TryReapplyVisuals();`),
                // and Player.log shows exactly that order - the GearAura lines land, THEN
                // "[Flow:Equip] -> attach '<new weapon>'". So on the equip that TURNS the flame on,
                // the new blade does not exist yet when this component first looks for it. Without
                // this retry the seat would sit empty until some later, unrelated gear change.
                _nextReseatCheck = Time.time + PendingRetrySeconds;
                if (_weaponPending) Refresh();
                return;
            }

            _nextReseatCheck = Time.time + ReseatCheckSeconds;

            if (_weaponSocket == null || !_weaponHandle.IsAlive)
            {
                FlowTrace.Fail("GearAura",
                    "weapon-socket aura STOPPED: its socket bone is gone (body swap / rig rebuild) or the " +
                    "pooled instance died - a loop parented to a destroyed transform plays forever at the " +
                    "world origin and burns a loop slot. Re-resolving on the next equip change.");
                StopWeapon("socket lost");
                return;
            }

            // WO-959 belt-and-braces: OnCarryStateChanged is the primary release edge, but if it
            // was missed (the bind raced the first draw, or a body swap re-created the
            // EquipmentController under a live subscription) this throttled check still puts a
            // sheathed weapon's flame out within ReseatCheckSeconds. Same verified StopWeapon.
            if (!IsWeaponDrawn())
            {
                StopWeapon("weapon sheathed (WO-959 reseat check)");
                return;
            }

            // The weapon PROP we measured the blade off was destroyed or replaced under us (a weapon
            // swap that keeps the same element, a sheathe/draw that rebuilds the prop, a rig
            // rebuild). The cached blade frame is now stale, so the flame would hang in the air where
            // the old blade used to be. Release and let Refresh re-measure the new one immediately -
            // Refresh's change guard fires because StopWeapon has reset _weaponType to None.
            if (_bladeAnchor == null)
            {
                FlowTrace.Step("GearAura",
                    "weapon aura RE-SEATING: the measured weapon prop ('" + WeaponPropName + "') is gone " +
                    "(swap / sheathe / rig rebuild) - the cached blade frame is stale, re-measuring.");
                StopWeapon("weapon prop replaced");
                Refresh();
            }
        }

        /// <summary>
        /// Keep the held flame on the BLADE after animation has posed the rig. LateUpdate (not
        /// Update) because the hand bone is animated: a follow written in Update is one frame behind
        /// the pose and the flame visibly lags the sword through a swing.
        /// </summary>
        private void LateUpdate()
        {
            if (_weaponAuraGo == null || _bladeAnchor == null) return;
            if (_weaponHandle == null || !_weaponHandle.IsAlive) return;
            SeatOnBlade();
        }

        // =====================================================================
        //  Refresh - the one resolution path
        // =====================================================================

        /// <summary>
        /// Re-resolve both seats from the current loadout, stopping and starting only what
        /// actually changed. Idempotent; safe to call at any time and from any equip path.
        /// </summary>
        public void Refresh()
        {
            if (_loadout == null) _loadout = GetComponent<GearLoadout>();
            if (_loadout == null) { StopAll("no GearLoadout"); return; }

            // -- Seat 1: weapon socket ------------------------------------------------
            VFXType wantWeapon = VFXType.None;
            string  weaponWhy;
            var weapon = _loadout.EquippedWeapon;
            if (GearAuraMap.TryWeaponAura(weapon, out VFXType wType, out weaponWhy)) wantWeapon = wType;
            bool loadoutWantsWeapon = wantWeapon != VFXType.None;

            // WO-959 (owner ruling, F8 seq 2297): an element aura renders ONLY while the weapon is
            // DRAWN. Sheathed on the back = no flames, whatever the loadout grants. ONE gate here
            // covers ALL element auras, because every weapon-seat aura resolves through this want -
            // and the sheathed release below is the same verified StopWeapon an unequip uses.
            if (wantWeapon != VFXType.None && !IsWeaponDrawn())
            {
                wantWeapon = VFXType.None;
                weaponWhy  = "weapon sheathed - element auras render only while drawn (WO-959)";
            }

            if (wantWeapon != _weaponType || (_weaponHandle == null) != (wantWeapon == VFXType.None))
            {
                // The why doubles as the release reason when the resolve came up empty, so the
                // trace names the real cause (sheathed / no brand) instead of a generic label.
                StopWeapon(wantWeapon == VFXType.None ? weaponWhy : "equip change");
                if (wantWeapon != VFXType.None) StartWeapon(wantWeapon, weapon, weaponWhy);
                else FlowTrace.Step("GearAura", "weapon aura: none (" + weaponWhy + ")");
            }

            // Computed OUTSIDE the change guard, so a seat that is wanted-and-still-empty stays
            // pending even on a Refresh that changed nothing. Deliberately the PRE-GATE want
            // (WO-959): while the loadout grants an aura that is withheld - blade not attached
            // yet, loop cap, or SHEATHED - the Update retry keeps ticking, so the flame appears
            // within PendingRetrySeconds of a draw even if the carry-state event was missed.
            _weaponPending = loadoutWantsWeapon && _weaponHandle == null;

            // -- Seat 2: body ---------------------------------------------------------
            VFXType wantBody = VFXType.None;
            string  bodyWhy;
            if (GearAuraMap.TryBodyAura(_loadout.EquippedRing, _loadout.EquippedAmulet,
                                        out VFXType bType, out bodyWhy)) wantBody = bType;

            if (wantBody != _bodyType || (_bodyHandle == null) != (wantBody == VFXType.None))
            {
                StopBody("equip change");
                if (wantBody != VFXType.None) StartBody(wantBody, bodyWhy);
                else FlowTrace.Step("GearAura", "body aura: none (" + bodyWhy + ")");
            }
        }

        // =====================================================================
        //  WO-959 - the drawn/sheathed gate
        // =====================================================================

        /// <summary>
        /// Resolve + subscribe the carry-state authority (idempotent). Separate from the
        /// GearLoadout subscription because EquipmentController can be added to the hero AFTER
        /// this component (companion build order adds the controller first, the hero swapper
        /// last) - so every drawn-state read retries the bind rather than assuming OnEnable won.
        /// </summary>
        private void BindCarryState()
        {
            if (_equipment == null)
            {
                _equipment = GetComponent<EquipmentController>();
                _carrySubscribed = false;   // a destroyed controller took the old subscription with it
            }
            if (_equipment != null && !_carrySubscribed)
            {
                _equipment.OnCarryStateChanged += HandleCarryStateChanged;
                _carrySubscribed = true;
                FlowTrace.Step("GearAura",
                    "bound to EquipmentController.OnCarryStateChanged (WO-959: weapon auras follow drawn/sheathed).");
            }
        }

        /// <summary>
        /// Carry-state edge (WO-959): acquire the weapon-seat aura on draw, release it on
        /// sheathe - both through the ONE Refresh resolution path, so the release is the same
        /// verified StopWeapon every other exit uses. Raised by EquipmentController.ApplyHoldPose
        /// AFTER the prop is re-seated, so the blade measure on acquire sees the prop in hand.
        /// </summary>
        private void HandleCarryStateChanged(bool drawn)
        {
            FlowTrace.Step("GearAura", "carry state -> " + (drawn ? "DRAWN" : "SHEATHED") +
                " - re-resolving the weapon seat (WO-959: element auras render only on a drawn weapon).");
            Refresh();
        }

        /// <summary>
        /// The drawn/sheathed truth (WO-959). EquipmentController.IsWeaponDrawn is the SAME
        /// predicate ApplyHoldPose physically seats the prop by (hand when drawn, back socket
        /// when sheathed; its state follows BattleLock / live-wave engagement via HeroLocomotion's
        /// per-frame SetCombatActive, or the auto-mirror when nothing drives it). With no
        /// EquipmentController on this rig there IS no sheathe - the prop never leaves the hand -
        /// so the honest answer is drawn: fail-VISIBLE (the pre-WO-959 always-on behaviour),
        /// never a silently withheld aura.
        /// </summary>
        private bool IsWeaponDrawn()
        {
            BindCarryState();
            if (_equipment == null)
            {
                FlowTrace.Throttle("GearAura", "no-equip-carry", 30f,
                    "no EquipmentController on this hero - no sheathe state exists, so the weapon " +
                    "aura is treated as DRAWN (fail-visible; the WO-959 gate is inert on this rig).");
                return true;
            }
            return _equipment.IsWeaponDrawn;
        }

        // =====================================================================
        //  Seat 1 - weapon socket
        // =====================================================================

        private void StartWeapon(VFXType type, WeaponDef weapon, string why)
        {
            Transform socket = ResolveWeaponSocket();
            if (socket == null)
            {
                // No rig / invalid humanoid avatar. Report rather than dumping the aura on the
                // hero root, where a blade smoulder would read as the hero being on fire.
                FlowTrace.Throttle("GearAura", "no-socket", 5f,
                    "weapon aura '" + type + "' skipped: no RightHand bone on this body (invalid humanoid " +
                    "avatar?). Seating an elemental weapon aura on the ROOT would read as the HERO burning, " +
                    "not the weapon, so it is withheld.");
                return;
            }

            // WHERE THE FLAME GOES. The hand bone IS the hilt, so parenting the effect to it and
            // stopping there is what produced "they have it coming from the hilt". Measure the
            // equipped prop's real mesh bounds instead and derive a blade SEGMENT: longest local
            // axis = the blade, grip at the prop origin (EquipmentController seats every melee
            // grip-at-origin - "trust grip-at-origin", :~880), tip = the far extreme, and the first
            // HandleClearFrac of that span stays bare. Nothing here is hard-coded to one sword.
            if (!TryResolveBlade(socket))
            {
                FlowTrace.Throttle("GearAura", "no-blade", 5f,
                    "weapon aura '" + type + "' WITHHELD: no measurable blade on the equipped prop ('" +
                    WeaponPropName + "' missing, no mesh renderer, or a measured span under " +
                    MinBladeLength.ToString("0.00") + "m). Falling back to the hand bone would put a POINT " +
                    "source back on the hilt, which is the defect being fixed - so nothing is shown and " +
                    "this re-tries on the next equip change / prop rebuild.");
                return;
            }

            var mgr = VFXManager.Instance;
            if (mgr == null) return;

            // Still PARENTED to the bone, not to the prop: the prop is destroyed and rebuilt on
            // every swap and sheathe, and a pooled loop parented to it would be destroyed WITH it -
            // which leaks the pool slot silently (VFXHandle.Stop no-ops on a destroyed instance).
            // The blade frame is applied as a world position/rotation each LateUpdate instead.
            _weaponHandle = mgr.PlayAura(type, socket);
            if (_weaponHandle == null)
            {
                FlowTrace.Throttle("GearAura", "weapon-refused", 5f,
                    "weapon aura '" + type + "' refused by VFXManager (loop cap or quality gate) - " +
                    "item auras yield to survival reads by design; will re-try on the next equip change.");
                return;
            }

            _weaponType   = type;
            _weaponSocket = socket;
            _nextReseatCheck = Time.time + ReseatCheckSeconds;

            float tier = TierMulFor(weapon != null ? weapon.id : null, weapon != null ? weapon.rarity : null);
            var mod = _weaponHandle.Modulator;
            if (mod != null)
            {
                // The direct instance handle (see the field's comment) - taken from the modulator,
                // which is a component ON the pooled instance, so no new VFXManager API is needed.
                _weaponAuraGo = mod.gameObject;

                // Scale BEFORE shaping: the shape override is computed in world metres and has to
                // divide by the instance's effective scale, which this call changes.
                mod.SetScaleMul(Mathf.Min(WeaponAuraScaleMul * tier, WeaponAuraScaleMul * TierMaxMul));
                mod.SetEmissionScale(Mathf.Min(WeaponAuraDensity * tier, WeaponAuraDensity * TierMaxMul));
            }

            if (_weaponAuraGo != null)
            {
                ShapeToBlade(_weaponAuraGo);
                SeatOnBlade();
            }

            FlowTrace.Step("GearAura",
                "HELD weapon aura '" + type + "' on the BLADE of '" + _bladeAnchor.name + "' under socket '" +
                socket.name + "' (" + why + ", tier x" + tier.ToString("0.00") + "). Blade segment: length=" +
                _bladeWorldLength.ToString("0.000") + "m halfWidth=" + _bladeWorldHalfWidth.ToString("0.000") +
                "m, grip " + (HandleClearFrac * 100f).ToString("0") + "% bare.");
        }

        private void StopWeapon(string reason)
        {
            // Hand the pooled instance's shape/emission back FIRST, while we still know which
            // layers we touched. Doing it after Stop would be too late - the instance can be
            // re-acquired by another owner the moment it re-enters the pool.
            RestoreShape();

            if (_weaponHandle == null && _weaponAuraGo == null)
            {
                _weaponType    = VFXType.None;
                _weaponSocket  = null;
                _weaponPending = false;
                ClearBlade();
                return;
            }

            var  was      = _weaponType;
            bool wasAlive = _weaponHandle != null && _weaponHandle.IsAlive;

            // immediate: an unequipped weapon's aura must not linger on the hand
            _weaponHandle?.Stop(true);
            _weaponHandle = null;

            // ── VERIFY. Never claim a teardown on faith (CLAUDE.md section 12). ──────────────
            // Two documented ways the Stop above can do NOTHING while still returning cleanly:
            //   (b) VFXHandle.Stop() early-returns silently when its GameObject reference is null
            //       or stale (VFXHandle.cs:85) - the state left behind by a rig rebuild.
            //   (c) VFXManager.ReturnToPool's `SetParent(_poolRoot, false)` (VFXManager.cs:900) is
            //       REFUSED by Unity when the host is mid-(de)activation. The Hovl return path was
            //       hardened for exactly this (VFXManager.Hovl.cs:397 "deactivate in place"); the
            //       VFXType path this aura uses was NOT, and the same session logged that refusal
            //       for [VFX_Aura_LowHealth] / [VFX_Aura_EnemyCaster] / [VFX_Harvest_*].
            // Either way the emitter survives on the hero and the old log line still said
            // "released". So: look at the object, and put it down ourselves if it is still up.
            var go = _weaponAuraGo;
            _weaponAuraGo = null;
            bool forced = false;
            if (go != null && go.activeSelf)
            {
                forced = true;
                // Deactivate IN PLACE and do NOT reparent - reparenting here is the very call
                // Unity refuses mid-(de)activation, and it is not needed: the pool re-seats the
                // parent on the next acquire, and tolerates the object dying with its host.
                go.SetActive(false);
                FlowTrace.Fail("GearAura",
                    "weapon aura '" + was + "' SURVIVED its Stop (reason=" + reason + ", handleWasAlive=" +
                    wasAlive + ", instance='" + go.name + "', parent='" +
                    (go.transform.parent != null ? go.transform.parent.name : "<scene root>") +
                    "'). FORCED inactive in place. This is the orphaned-emitter class (WO-929): the " +
                    "handle's Stop no-opped or the pool-return's reparent was refused, and without this " +
                    "check the flame would keep burning on the hero after the weapon was swapped.");
            }

            _weaponType    = VFXType.None;
            _weaponSocket  = null;
            _weaponPending = false;   // Refresh recomputes it from the loadout on the way back up
            ClearBlade();

            FlowTrace.Step("GearAura",
                "released weapon aura '" + was + "' (reason=" + reason + ", handleWasAlive=" + wasAlive +
                ", verified " + (forced ? "FORCED-DOWN" : "down") + ").");
        }

        /// <summary>
        /// The RightHand bone, matching where GearVisualApplier and EquipmentController seat a
        /// melee weapon. Returns null (never a fallback to the root) when the avatar has no
        /// humanoid hand - see StartWeapon for why a root fallback would be a worse lie.
        /// </summary>
        private Transform ResolveWeaponSocket()
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim == null || !anim.isHuman) return null;
            return anim.GetBoneTransform(HumanBodyBones.RightHand);
        }

        // =====================================================================
        //  Seat 1b - the BLADE (WO-930): measure it, don't assume it
        // =====================================================================

        /// <summary>
        /// The equipped weapon prop. Under the hand first (the normal held case); otherwise the
        /// whole rig, so a SHEATHED weapon - which EquipmentController re-seats under
        /// 'SheatheSocket_Back' - still carries its flame instead of leaving it on an empty hand.
        /// The off-hand prop is a different name ('EquipmentProp_OffHand') so a shield can never
        /// be mistaken for the blade.
        /// </summary>
        private Transform FindWeaponProp(Transform hand)
        {
            if (hand != null)
            {
                var direct = hand.Find(WeaponPropName);
                if (direct != null) return direct;
            }

            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t != null && t.name == WeaponPropName) return t;
            }
            return null;
        }

        /// <summary>
        /// Measure the equipped prop and cache a blade SEGMENT in the prop's local space:
        /// centre, direction, world length and world half-width. Geometry-driven - the longest
        /// local axis of the real mesh bounds is the blade, the grip sits at the prop origin
        /// (EquipmentController seats every melee grip-at-origin), and the tip is whichever
        /// extreme is farther from that origin. A dagger, a greatsword and a staff therefore all
        /// get a correctly-proportioned flame with no per-weapon data and no code change.
        /// False when there is nothing measurable - the caller WITHHOLDS rather than guessing.
        /// </summary>
        private bool TryResolveBlade(Transform hand)
        {
            ClearBlade();

            Transform prop = FindWeaponProp(hand);
            if (prop == null) return false;
            if (!TryLocalMeshBounds(prop, out Bounds b)) return false;

            Vector3 ext = b.extents;
            int axis = (ext.x >= ext.y && ext.x >= ext.z) ? 0 : (ext.y >= ext.z ? 1 : 2);

            float maxA = b.max[axis];
            float minA = b.min[axis];
            float tip  = Mathf.Abs(maxA) >= Mathf.Abs(minA) ? maxA : minA;
            if (Mathf.Abs(tip) < 1e-4f) return false;

            // Grip end of the flame: the same side as the tip, HandleClearFrac of the way out.
            float gripEnd = tip * HandleClearFrac;

            Vector3 axisVec = Vector3.zero;
            axisVec[axis] = tip >= 0f ? 1f : -1f;

            Vector3 centre = b.center;
            centre[axis] = (gripEnd + tip) * 0.5f;

            Vector3 pA = centre; pA[axis] = gripEnd;
            Vector3 pB = centre; pB[axis] = tip;

            float worldLen = (prop.TransformPoint(pB) - prop.TransformPoint(pA)).magnitude;
            if (worldLen < MinBladeLength) return false;

            // Cross-section half-width in WORLD metres (TransformVector carries the prop's scale
            // AND the 1.666 lossy scale the hand bone contributes).
            int o1 = (axis + 1) % 3;
            int o2 = (axis + 2) % 3;
            Vector3 e1 = Vector3.zero; e1[o1] = ext[o1];
            Vector3 e2 = Vector3.zero; e2[o2] = ext[o2];
            float halfWidth = Mathf.Max(prop.TransformVector(e1).magnitude,
                                        prop.TransformVector(e2).magnitude);

            _bladeAnchor         = prop;
            _bladeLocalCenter    = centre;
            _bladeLocalDir       = axisVec;
            _bladeWorldLength    = worldLen;
            _bladeWorldHalfWidth = Mathf.Max(halfWidth, 0.01f);
            return true;
        }

        /// <summary>
        /// Mesh-accurate local-space bounds for <paramref name="root"/>. Renderer.bounds is a WORLD
        /// axis-aligned box, so inverse-transforming it inflates badly for a rotated sword; the mesh's
        /// own local bounds pushed through (root^-1 * renderer) is exact for the mesh AABB.
        /// ParticleSystemRenderers are skipped - a weapon trail is not the blade.
        /// </summary>
        private static bool TryLocalMeshBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            Matrix4x4 toRoot = root.worldToLocalMatrix;

            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                var mf = filters[i];
                if (mf == null || mf.sharedMesh == null) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                Accumulate(mf.sharedMesh.bounds, toRoot * mf.transform.localToWorldMatrix, ref bounds, ref any);
            }

            var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length; i++)
            {
                var smr = skinned[i];
                if (smr == null || smr.sharedMesh == null || !smr.enabled) continue;
                Accumulate(smr.sharedMesh.bounds, toRoot * smr.transform.localToWorldMatrix, ref bounds, ref any);
            }

            return any;
        }

        private static void Accumulate(Bounds local, Matrix4x4 m, ref Bounds acc, ref bool any)
        {
            Vector3 c = local.center;
            Vector3 e = local.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    c.x + ((i & 1) == 0 ? -e.x : e.x),
                    c.y + ((i & 2) == 0 ? -e.y : e.y),
                    c.z + ((i & 4) == 0 ? -e.z : e.z));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any) { acc = new Bounds(p, Vector3.zero); any = true; }
                else       acc.Encapsulate(p);
            }
        }

        /// <summary>
        /// Put the held instance on the blade for this frame: centred on the measured segment and
        /// rotated so its LOCAL +Y runs grip-to-tip. Local +Y is the axis the blade override emits
        /// along (see <see cref="ShapeToBlade"/>) and is also the axis a fire recipe's authored rise
        /// uses, so the flame licks TOWARD the tip instead of drifting sideways.
        /// </summary>
        private void SeatOnBlade()
        {
            var t = _weaponAuraGo.transform;
            t.position = _bladeAnchor.TransformPoint(_bladeLocalCenter);

            Vector3 dir = _bladeAnchor.TransformDirection(_bladeLocalDir);
            if (dir.sqrMagnitude > 1e-6f)
                t.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        }

        /// <summary>
        /// Stretch a POINT emitter into a BLADE-LENGTH one, and mute the layers that read as smoke.
        ///
        /// Every value written here is captured first and handed back by <see cref="RestoreShape"/>
        /// on every exit path, because these are POOLED instances: VFXManager.ReturnToPool resets
        /// only what it itself changed, so an un-restored override would follow the instance into
        /// its next owner forever with no error anywhere (the trap VfxLoopModulator's header
        /// documents, applied to the shape module rather than to emission).
        /// </summary>
        private void ShapeToBlade(GameObject instance)
        {
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            if (systems == null || systems.Length == 0) return;

            _shapedLayers = new ShapedLayer[systems.Length];

            float width = _bladeWorldHalfWidth * 2f * BladeFlameWidthMul;

            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                var rec = new ShapedLayer { Ps = ps };
                if (ps == null) { _shapedLayers[i] = rec; continue; }

                var sh = ps.shape;
                var em = ps.emission;

                rec.WasShapeEnabled    = sh.enabled;
                rec.WasShapeType       = sh.shapeType;
                rec.WasShapeScale      = sh.scale;
                rec.WasShapeRotation   = sh.rotation;
                rec.WasShapePosition   = sh.position;
                rec.WasShapeRadius     = sh.radius;
                rec.WasEmissionEnabled = em.enabled;

                if (ShouldMuteOnBlade(ps, instance, systems.Length))
                {
                    rec.Muted  = true;
                    em.enabled = false;
                }
                else
                {
                    // The shape module works in the system's own scaled space, so world metres are
                    // divided by the instance's effective scale (which SetScaleMul has already set).
                    float eff = EffectiveShapeScale(ps);

                    sh.enabled   = true;
                    sh.shapeType = ParticleSystemShapeType.Box;
                    sh.position  = Vector3.zero;
                    // Box emits along its +Z; -90 about X maps that onto the system's local +Y,
                    // which SeatOnBlade has aligned with the blade. One consistent axis for the
                    // spawn volume AND the emit direction.
                    sh.rotation  = new Vector3(-90f, 0f, 0f);
                    sh.scale     = new Vector3(width / eff, width / eff, _bladeWorldLength / eff);
                }

                _shapedLayers[i] = rec;
            }
        }

        /// <summary>
        /// Which layers of the catalogued fire recipe do NOT belong on a blade. Measured off
        /// Fire_medium (see the constants block): the 'Smoke' layer is the densest in the recipe
        /// (50/sec, 0.8 soft blobs) and the ROOT shell emits size-3 blobs - both are ground-fire
        /// language, and they are exactly what survives when the effect is thinned onto a weapon.
        /// Matched by NAME plus the structural root rule rather than by an index, so a re-authored
        /// or repointed recipe still behaves.
        /// </summary>
        private static bool ShouldMuteOnBlade(ParticleSystem ps, GameObject instance, int layerCount)
        {
            if (ps == null) return false;
            if (ps.gameObject.name.IndexOf("smoke", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            // The oversized root shell, but only when the recipe actually has child layers to carry
            // the effect - a single-system recipe must never be muted into nothing.
            if (layerCount > 1 && ps.gameObject == instance) return true;
            return false;
        }

        /// <summary>
        /// The scale the shape module will be multiplied by. Hierarchy mode uses the full lossy
        /// scale (so the hero rig's 1.666 bone scale counts); Local / Shape modes use only the
        /// system's own transform scale.
        /// </summary>
        private static float EffectiveShapeScale(ParticleSystem ps)
        {
            var main = ps.main;
            Vector3 s = main.scalingMode == ParticleSystemScalingMode.Hierarchy
                ? ps.transform.lossyScale
                : ps.transform.localScale;
            float v = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
            return Mathf.Max(v, 0.0001f);
        }

        /// <summary>Hand every overridden shape/emission value back to the pooled instance. Idempotent.</summary>
        private void RestoreShape()
        {
            if (_shapedLayers == null) return;

            for (int i = 0; i < _shapedLayers.Length; i++)
            {
                var rec = _shapedLayers[i];
                var ps  = rec.Ps;
                if (ps == null) continue;   // a layer was destroyed with its host - skip, never throw

                var em = ps.emission;
                em.enabled = rec.WasEmissionEnabled;
                if (rec.Muted) continue;

                var sh = ps.shape;
                sh.shapeType = rec.WasShapeType;
                sh.scale     = rec.WasShapeScale;
                sh.rotation  = rec.WasShapeRotation;
                sh.position  = rec.WasShapePosition;
                sh.radius    = rec.WasShapeRadius;
                sh.enabled   = rec.WasShapeEnabled;
            }

            _shapedLayers = null;
        }

        private void ClearBlade()
        {
            _bladeAnchor         = null;
            _bladeLocalCenter    = Vector3.zero;
            _bladeLocalDir       = Vector3.up;
            _bladeWorldLength    = 0f;
            _bladeWorldHalfWidth = 0f;
        }

        // =====================================================================
        //  Seat 2 - body
        // =====================================================================

        private void StartBody(VFXType type, string why)
        {
            var mgr = VFXManager.Instance;
            if (mgr == null) return;

            _bodyHandle = mgr.PlayAura(type, transform);
            if (_bodyHandle == null)
            {
                FlowTrace.Throttle("GearAura", "body-refused", 5f,
                    "body aura '" + type + "' refused by VFXManager (loop cap or quality gate).");
                return;
            }

            _bodyType = type;

            // Lift off the feet so the restoration column reads as rising from the torso.
            // Local offset on the instance; it tracks the hero because PlayAura parented it.
            _bodyHandle.SetPosition(transform.position + Vector3.up * BodyAuraHeight);

            var mod = _bodyHandle.Modulator;
            if (mod != null)
            {
                mod.SetScaleMul(BodyAuraScaleMul);
                mod.SetEmissionScale(BodyAuraDensity);
            }

            FlowTrace.Step("GearAura", "HELD body aura '" + type + "' (" + why + ").");
        }

        private void StopBody(string reason)
        {
            if (_bodyHandle == null) { _bodyType = VFXType.None; return; }
            var was = _bodyType;
            _bodyHandle.Stop(true);
            _bodyHandle = null;
            _bodyType   = VFXType.None;
            FlowTrace.Step("GearAura", "released body aura '" + was + "' (reason=" + reason + ").");
        }

        // =====================================================================
        //  Shared
        // =====================================================================

        /// <summary>Stop BOTH seats now. Idempotent; safe with nothing held.</summary>
        public void StopAll(string reason = "StopAll")
        {
            StopWeapon(reason);
            StopBody(reason);
        }

        /// <summary>
        /// Per-instance gear LEVEL -> a size/density multiplier (WO-808 GearProgression: the
        /// same owned item levels up in place). Identity at level 1 and whenever the save state
        /// is unavailable, so an item always reads as its recipe first and its level second.
        /// </summary>
        private static float TierMulFor(string gearId, string rarity)
        {
            if (string.IsNullOrEmpty(gearId)) return 1f;

            var svc = DeNelle.Core.State.GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null) return 1f;

            int level = GearProgression.GearLevelOf(state, gearId);
            if (level <= 1) return 1f;

            // Scale and density share one multiplier so the escalation reads as ONE idea
            // ("more of the same presence"), not two independent knobs drifting apart.
            float mul = 1f + (level - 1) * Mathf.Max(TierScalePerLevel, TierEmissionPerLevel);
            return Mathf.Min(mul, TierMaxMul);
        }

        /// <summary>
        /// Attach the gear-aura driver to the hero once (idempotent). The seam the equip /
        /// visual paths call; the component then subscribes to GearLoadout.OnGearChanged and
        /// keeps itself current from there.
        /// </summary>
        public static GearAura Ensure(Component heroPart)
        {
            if (heroPart == null) return null;
            var loadout = heroPart.GetComponentInParent<GearLoadout>();
            if (loadout == null) return null;                      // not a hero with gear - nothing to do
            var a = loadout.GetComponent<GearAura>();
            if (a == null) a = loadout.gameObject.AddComponent<GearAura>();
            return a;
        }
    }
}
