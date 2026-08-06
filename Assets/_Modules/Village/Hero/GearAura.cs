// =============================================================================
// GearAura - WO-888: the persistent aura an EQUIPPED ITEM grants the hero.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Registry section 6c: "the Aura beat sourced from an item, not a cast". A heal relic
// gives the body a soft RISING restoration column; an elemental weapon faintly smoulders
// on its own socket. Mirrors ArcaneAura's handle discipline and the Pets/AuraController
// seat idea, but holds POOLED VFXType loops rather than instantiating art.
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
        private const float WeaponAuraScaleMul = 0.35f;   // a smoulder on a blade, not a bonfire
        private const float WeaponAuraDensity  = 0.45f;   // "faint" (registry 6c)
        private const float BodyAuraScaleMul   = 0.50f;   // Aura_ItemHeal ships at scale 1.25 (measured) - halve it
        private const float BodyAuraDensity    = 1.00f;   // already authored sparse (1.8/sec measured)

        // Metres above the hero root to seat the BODY aura, so the restoration column reads
        // as rising from the chest rather than out of the ground. Kept low: the phone is
        // LANDSCAPE 2670x1200 and anything that grows upward spends the scarce axis.
        private const float BodyAuraHeight = 0.9f;

        // Re-resolve throttle for the socket-bone re-seat check (a body swap can replace the
        // rig under a live aura). Cheap check, but no reason to do it every frame.
        private const float ReseatCheckSeconds = 1.0f;

        // -- Seat 1: the weapon socket. ONE handle. --
        private VFXHandle _weaponHandle;
        private VFXType   _weaponType = VFXType.None;
        private Transform _weaponSocket;

        // -- Seat 2: the body. ONE handle. --
        private VFXHandle _bodyHandle;
        private VFXType   _bodyType = VFXType.None;

        private GearLoadout _loadout;
        private float _nextReseatCheck;

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
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Refresh();
        }

        private void OnDisable()
        {
            if (_loadout != null) _loadout.OnGearChanged -= Refresh;
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
            if (_weaponHandle == null) return;
            if (Time.time < _nextReseatCheck) return;
            _nextReseatCheck = Time.time + ReseatCheckSeconds;

            if (_weaponSocket == null || !_weaponHandle.IsAlive)
            {
                FlowTrace.Fail("GearAura",
                    "weapon-socket aura STOPPED: its socket bone is gone (body swap / rig rebuild) or the " +
                    "pooled instance died - a loop parented to a destroyed transform plays forever at the " +
                    "world origin and burns a loop slot. Re-resolving on the next equip change.");
                StopWeapon("socket lost");
            }
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

            if (wantWeapon != _weaponType || (_weaponHandle == null) != (wantWeapon == VFXType.None))
            {
                StopWeapon("equip change");
                if (wantWeapon != VFXType.None) StartWeapon(wantWeapon, weapon, weaponWhy);
                else FlowTrace.Step("GearAura", "weapon aura: none (" + weaponWhy + ")");
            }

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

            var mgr = VFXManager.Instance;
            if (mgr == null) return;

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
                mod.SetScaleMul(Mathf.Min(WeaponAuraScaleMul * tier, WeaponAuraScaleMul * TierMaxMul));
                mod.SetEmissionScale(Mathf.Min(WeaponAuraDensity * tier, WeaponAuraDensity * TierMaxMul));
            }

            FlowTrace.Step("GearAura",
                "HELD weapon aura '" + type + "' on socket '" + socket.name + "' (" + why +
                ", tier x" + tier.ToString("0.00") + ").");
        }

        private void StopWeapon(string reason)
        {
            if (_weaponHandle == null) { _weaponType = VFXType.None; _weaponSocket = null; return; }
            var was = _weaponType;
            _weaponHandle.Stop(true);   // immediate: an unequipped weapon's aura must not linger on the hand
            _weaponHandle = null;
            _weaponType   = VFXType.None;
            _weaponSocket = null;
            FlowTrace.Step("GearAura", "released weapon aura '" + was + "' (reason=" + reason + ").");
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
