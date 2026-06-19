// =============================================================================
// HeroArmorVisual — shows the EQUIPPED armor on the hero/companion BODY.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE GAP THIS CLOSES (owner: "equipped but Grom is still naked"):
//   Equipping armor today sets the stat (GearLoadout.ArmorDefense) and records a
//   coarse "tier" on EquipmentController — but the body stays the bare class mesh.
//   The Blink gear bundle ships armor as FULL-BODY skinned-mesh outfit characters
//   (HumanMale/HumanFemale on ONE shared Humanoid rig), addressable under the
//   "Gear" group at keys like "gear/armor/<set>_Male" (see docs/BLINK_NOTES.md +
//   docs/ITEM_MODEL.md). So "show armor" = swap in the armored body and drive it
//   with the hero's existing animator (a humanoid retarget), hiding the base body.
//
// HOW IT WORKS (humanoid mesh-swap — the lowest-risk path that actually shows armor):
//   • Subscribe to GearLoadout.OnGearChanged. On change read EquippedArmor.
//   • Blink set (loadVia=="addressable" OR prefabPath starts "gear/armor/"):
//       Addressables.LoadAssetAsync<GameObject>(prefabPath) -> Instantiate ->
//       parent under the hero root at the base body's local TRS. Copy the base
//       body's Animator.runtimeAnimatorController (+ avatar) onto the instance's
//       Animator, applyRootMotion=false, so it plays the SAME idle/walk/cast.
//       Then HIDE (disable, never destroy) the base body's SkinnedMeshRenderers.
//   • Non-Blink armor (the cloth default) / unequip / null: destroy the armored
//       instance and RE-ENABLE the base body. The base body is the safe fallback —
//       the hero is NEVER left invisible/naked on any failure path.
//   • The Addressables handle has ONE owner (this component): released on every
//       swap / unequip / OnDisable so a Blink prefab never leaks.
//
// WEAPON RE-HOME: the weapon mesh is owned by EquipmentController, which seats it on
//   the base body's HeroBody hand bone. The base body is HIDDEN, not destroyed, so its
//   rig (and the weapon parented to its hand) still poses in lockstep with the armored
//   instance (both share the same controller/clips) — the weapon stays in the visible
//   hand with no cross-component surgery. We DO re-raise nothing on EquipmentController
//   (the other agent owns it); we only subscribe to OnGearChanged.
//
// INSTRUMENTED per §12: FlowTrace.Enter/Step/Try at load/instantiate/retarget/hide/
//   restore, and Fail-loud on a null prefab/avatar so a run self-reports whether the
//   armor body showed or why it didn't.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Component on a hero / companion root. Reads the equipped armor (via GearLoadout)
    /// and, when it is a Blink full-body outfit, swaps the armored skinned-mesh body in
    /// (humanoid-retargeted to the hero's animator) over the hidden base body. A non-Blink
    /// armor / unequip restores the base body. Never leaves the hero invisible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroArmorVisual : MonoBehaviour
    {
        // The base body child every hero/companion build seats under the root (same
        // convention as GearLoadout / EquipmentController / GearVisualApplier).
        private const string BodyChildName = "HeroBody";

        // Name of the instantiated armored body so a re-swap finds + replaces it (and a
        // stray never duplicates). Sits as a sibling of HeroBody under the root.
        private const string ArmorBodyName = "HeroArmorBody";

        private GearLoadout _loadout;

        // The live armored-body instance (null when wearing the base/default body).
        private GameObject _armorInstance;
        // The armor id currently shown — idempotency guard (no rebuild on unchanged id).
        private string _currentArmorId;

        // The base body's SkinnedMeshRenderers we disabled while armored — re-enabled on
        // restore. Cached as a snapshot taken at hide-time so restore touches exactly what
        // we hid (and never a renderer that belongs to the armored instance).
        private SkinnedMeshRenderer[] _hiddenBaseRenderers;

        // Addressables handle for the armored prefab — ONE owner (this component). Released
        // on every swap / unequip / OnDisable so a Blink prefab never leaks.
        private AsyncOperationHandle<GameObject> _armorHandle;
        private bool _armorHandleOpen;

        // Generation counter: a stale async completion (player swapped armor mid-load) is
        // rejected so we never attach a ghost body from an out-of-date request.
        private int _equipGeneration;

        private void OnEnable()
        {
            if (_loadout == null) _loadout = GetComponent<GearLoadout>();
            if (_loadout != null) _loadout.OnGearChanged += HandleGearChanged;
            // Reflect whatever is already equipped on enable (auto-best may have run first).
            HandleGearChanged();
        }

        private void OnDisable()
        {
            if (_loadout != null) _loadout.OnGearChanged -= HandleGearChanged;
            // Tear the armored body down and restore the base body so a disabled/destroyed
            // hero is never left in a half-swapped (or invisible) state, and the handle frees.
            _equipGeneration++;
            RestoreBaseBody();
            DestroyArmorInstance();
            ReleaseArmorHandle();
            _currentArmorId = null;
        }

        private void HandleGearChanged()
        {
            using var _ = FlowTrace.Enter("ArmorVisual", "HandleGearChanged");
            if (_loadout == null) _loadout = GetComponent<GearLoadout>();
            ArmorDef armor = _loadout != null ? _loadout.EquippedArmor : null;
            ApplyArmor(armor);
        }

        /// <summary>
        /// Show <paramref name="armor"/> on the body. A Blink full-body outfit swaps the
        /// armored mesh in; a non-Blink armor / null restores the base body. Idempotent on
        /// an unchanged id. Never leaves the hero invisible — the base body is the fallback.
        /// </summary>
        private void ApplyArmor(ArmorDef armor)
        {
            string id = armor != null ? armor.id : null;

            // Idempotent: same armor already shown -> nothing to do.
            if (string.Equals(_currentArmorId, id, StringComparison.OrdinalIgnoreCase)
                && (armor == null || _armorInstance != null || !IsBlinkArmor(armor)))
            {
                FlowTrace.Step("ArmorVisual", $"ApplyArmor: id='{id ?? "<null>"}' already shown — no-op.");
                return;
            }

            // Any new request invalidates an in-flight async load and drops the old instance.
            _equipGeneration++;
            _currentArmorId = id;

            // A non-Blink armor (the cloth default) or an unequip: restore the base body and
            // drop any armored instance. The base body IS the cloth/default look.
            if (armor == null || !IsBlinkArmor(armor))
            {
                FlowTrace.Step("ArmorVisual",
                    $"ApplyArmor: id='{id ?? "<null>"}' is non-Blink/none — restoring base body.");
                DestroyArmorInstance();
                ReleaseArmorHandle();
                RestoreBaseBody();
                return;
            }

            // Blink full-body outfit — load + swap. Drop the previous armored instance/handle
            // FIRST (no stacking) but keep the base body shown until the new body is in hand,
            // so we never flash an invisible hero mid-swap.
            DestroyArmorInstance();
            ReleaseArmorHandle();

            string address = armor.prefabPath;
            if (string.IsNullOrEmpty(address))
            {
                // Marked addressable but no address — can't load. Stay on the base body.
                FlowTrace.Fail("ArmorVisual",
                    $"ApplyArmor: Blink armor '{id}' has no prefabPath — keeping base body (no naked hero).");
                RestoreBaseBody();
                return;
            }

            BeginAddressableSwap(armor, address, _equipGeneration);
        }

        // Kick off the async Addressables load of the Blink armored body. Attaches on
        // completion (the body appears a frame later — fine). A failed/invalid handle is
        // GUARDED: FlowTrace.Fail + keep the base body shown, so the hero is NEVER naked
        // because a Blink prefab didn't resolve.
        private void BeginAddressableSwap(ArmorDef armor, string address, int generation)
        {
            using var _ = FlowTrace.Enter("ArmorVisual", $"BeginAddressableSwap address='{address}'");

            AsyncOperationHandle<GameObject> handle;
            try
            {
                handle = Addressables.LoadAssetAsync<GameObject>(address);
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("ArmorVisual",
                    $"Addressable load threw for '{address}': {ex.GetType().Name}: {ex.Message} — " +
                    "keeping base body (no naked hero).");
                RestoreBaseBody();
                return;
            }

            _armorHandle = handle;
            _armorHandleOpen = true;
            FlowTrace.Step("ArmorVisual", $"Addressable armor load begin: id='{armor.id}' address='{address}'");

            handle.Completed += op =>
            {
                // Stale: the player swapped armor (or unequipped / disabled) while this load
                // was in flight — a newer request owns the slot. Release THIS handle and bail.
                if (generation != _equipGeneration)
                {
                    FlowTrace.Step("ArmorVisual",
                        $"Stale armor load for '{address}' (gen {generation} != {_equipGeneration}) — released.");
                    if (op.IsValid()) Addressables.Release(op);
                    if (_armorHandle.Equals(op)) { _armorHandle = default; _armorHandleOpen = false; }
                    return;
                }

                if (op.Status != AsyncOperationStatus.Succeeded || op.Result == null)
                {
                    FlowTrace.Fail("ArmorVisual",
                        $"Addressable armor load FAILED for '{address}' (status={op.Status}) — " +
                        "keeping base body (no naked hero).");
                    ReleaseArmorHandle();
                    RestoreBaseBody();
                    return;
                }

                BuildArmorBody(op.Result, armor, address);
            };
        }

        // Instantiate the loaded Blink body, parent it under the hero root at the base body's
        // local TRS, humanoid-retarget it to the base animator, then hide the base body. Every
        // step is Guard.Try'd so one bad object logs + falls back to the base body, never blanks.
        private void BuildArmorBody(GameObject prefab, ArmorDef armor, string address)
        {
            using var _ = FlowTrace.Enter("ArmorVisual", $"BuildArmorBody id='{armor.id}'");

            Transform baseBody = ResolveBaseBody();
            if (baseBody == null)
            {
                // No base body to mirror/seat against — abort the swap rather than risk a
                // floating/mis-placed armored body. (Base body absent => nothing to hide; the
                // hero is whatever else is on the root, never made worse.)
                FlowTrace.Fail("ArmorVisual",
                    $"BuildArmorBody: no '{BodyChildName}' child to seat/retarget against — armor '{armor.id}' skipped.");
                return;
            }

            Animator baseAnim = baseBody.GetComponentInChildren<Animator>();
            if (baseAnim == null || baseAnim.runtimeAnimatorController == null)
            {
                FlowTrace.Fail("ArmorVisual",
                    $"BuildArmorBody: base body has no Animator/controller to retarget — armor '{armor.id}' skipped (base body kept).");
                return;
            }

            GameObject instance = null;
            FlowTrace.Try("ArmorVisual", "instantiate armored body", () =>
            {
                instance = Instantiate(prefab);
            });
            if (instance == null)
            {
                FlowTrace.Fail("ArmorVisual",
                    $"BuildArmorBody: Instantiate returned null for '{address}' — keeping base body.");
                return;
            }
            instance.name = ArmorBodyName;

            // Seat the armored body exactly where the base body sits (same parent + local TRS),
            // so the camera (which follows the root) frames it identically and the forward-yaw /
            // seat-on-ground corrections the base body received are inherited 1:1.
            FlowTrace.Try("ArmorVisual", "seat armored body at base TRS", () =>
            {
                instance.transform.SetParent(transform, false);
                instance.transform.localPosition = baseBody.localPosition;
                instance.transform.localRotation = baseBody.localRotation;
                instance.transform.localScale    = baseBody.localScale;
            });

            // Match the base body's layer so the armored body shares the hero's render/raycast
            // treatment (companions sit on Ignore-Raycast; the player hero on Default).
            SetLayerRecursive(instance, baseBody.gameObject.layer);

            // Strip any embedded FBX helpers (camera / audio listener / light / physics) the
            // Blink prefab might carry, mirroring the base-body build — so the armored body is
            // JUST the mesh, never a stray camera/listener fighting the hero's own.
            StripEmbeddedArtifacts(instance);

            // ── HUMANOID RETARGET ────────────────────────────────────────────────────
            // Drive the armored body with the hero's EXISTING controller + avatar so it plays
            // the same idle/walk/cast. Both are Humanoid on the shared Blink rig, so assigning
            // the runtimeAnimatorController + the avatar makes the instance retarget the clips.
            bool retargeted = RetargetToBaseAnimator(instance, baseAnim, armor.id);
            if (!retargeted)
            {
                // Could not bind a valid humanoid animator — the body would T-pose / float. Do
                // NOT hide the base body; drop the half-built instance and keep the base shown.
                FlowTrace.Fail("ArmorVisual",
                    $"BuildArmorBody: could not retarget '{armor.id}' to the hero animator — " +
                    "dropping armored body, keeping the base body (no naked/static hero).");
                Destroy(instance);
                ReleaseArmorHandle();
                return;
            }

            _armorInstance = instance;

            // Only NOW (the armored body is in hand + animating) hide the base body.
            HideBaseBody(baseBody);

            FlowTrace.Step("ArmorVisual",
                $"BuildArmorBody: armored body '{armor.id}' shown (address='{address}'), base body hidden.");
        }

        // Copy the base body's controller + avatar onto the armored instance's Animator so it
        // retargets the same humanoid clips. Returns false when no valid humanoid avatar can be
        // bound (so the caller keeps the base body rather than show a frozen/T-posed armor body).
        private bool RetargetToBaseAnimator(GameObject instance, Animator baseAnim, string armorId)
        {
            using var _ = FlowTrace.Enter("ArmorVisual", "RetargetToBaseAnimator");

            Animator anim = instance.GetComponentInChildren<Animator>();
            if (anim == null) anim = instance.AddComponent<Animator>();

            // BLINK MIGRATION (no avatar borrow): the base body is now the Blink LowPoly human rig
            // and the armored set is a full-body skinned mesh on the SAME skeleton, so the armored
            // instance ships its OWN valid Humanoid avatar that matches the base 1:1 — it retargets
            // the hero's clips natively with no borrow. Prefer the instance's own avatar; only when
            // it is somehow missing/invalid do we fall back to the base avatar (a true match on the
            // shared rig, not a cross-rig hack). With NO valid avatar at all a humanoid clip can only
            // hold the bind/T-pose — bail so we never show a static body.
            if (anim.avatar == null || !anim.avatar.isValid)
            {
                if (baseAnim.avatar != null && baseAnim.avatar.isValid)
                {
                    // Same rig (docs/BLINK_NOTES.md) → the base avatar IS the armored body's avatar.
                    anim.avatar = baseAnim.avatar;
                    FlowTrace.Step("ArmorVisual",
                        $"armor '{armorId}' had no own avatar — assigned the shared-rig base avatar '{anim.avatar.name}' (true match).");
                }
            }
            else
            {
                FlowTrace.Step("ArmorVisual",
                    $"armor '{armorId}' uses its OWN Humanoid avatar '{anim.avatar.name}' (native retarget — no borrow).");
            }

            if (anim.avatar == null || !anim.avatar.isValid)
            {
                FlowTrace.Fail("ArmorVisual",
                    $"RetargetToBaseAnimator: armor '{armorId}' has no valid Humanoid avatar (own or base) — cannot retarget.");
                return false;
            }

            anim.runtimeAnimatorController = baseAnim.runtimeAnimatorController;
            // applyRootMotion=false: HeroLocomotion owns movement on the ROOT; the clip only
            // poses the visible mesh (same rule as HeroBodySwapper / the base body).
            anim.applyRootMotion = false;
            anim.cullingMode = baseAnim.cullingMode;
            anim.speed = baseAnim.speed;                 // inherit the hero's anim-speed scaling
            anim.keepAnimatorStateOnDisable = true;
            anim.Rebind();                               // reconnect bones after instantiate
            anim.Update(0f);                             // evaluate now so it poses this frame

            FlowTrace.Step("ArmorVisual",
                $"retargeted armor '{armorId}': controller='{anim.runtimeAnimatorController.name}', avatar='{anim.avatar.name}'.");
            return true;
        }

        // Disable (never destroy) the base body's SkinnedMeshRenderers so the hero reads as the
        // armored body. Snapshot exactly what we disabled so RestoreBaseBody re-enables the same.
        private void HideBaseBody(Transform baseBody)
        {
            if (baseBody == null) return;
            var renderers = baseBody.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int hidden = 0;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r.enabled) { r.enabled = false; hidden++; }
            }
            _hiddenBaseRenderers = renderers;
            if (hidden == 0)
            {
                // SELF-REPORT (Blink migration): with the real Blink base body, HideBaseBody should
                // disable >=1 SkinnedMeshRenderer (the Starter_* body meshes). Disabling 0 means the
                // resolved base body carries no enabled SkinnedMeshRenderer (wrong transform resolved,
                // or the base body was already hidden / is a non-skinned placeholder) — the old
                // "disabled 0" symptom. Warn so a run pinpoints it instead of a silently-still-naked hero.
                FlowTrace.Warn("ArmorVisual",
                    $"HideBaseBody: disabled 0 base SkinnedMeshRenderer(s) on '{baseBody.name}' " +
                    $"({renderers?.Length ?? 0} found) — base body may be wrong/already-hidden. " +
                    "Armored body still shown over it.");
            }
            else
            {
                FlowTrace.Step("ArmorVisual", $"HideBaseBody: disabled {hidden} base SkinnedMeshRenderer(s).");
            }
        }

        // Re-enable the base body's renderers we previously hid (re-resolving live ones if the
        // snapshot is stale). The base body is the SAFE fallback — always re-shown on restore.
        private void RestoreBaseBody()
        {
            int shown = 0;
            if (_hiddenBaseRenderers != null)
            {
                foreach (var r in _hiddenBaseRenderers)
                {
                    if (r == null) continue;
                    if (!r.enabled) { r.enabled = true; shown++; }
                }
                _hiddenBaseRenderers = null;
            }

            // Belt-and-braces: re-enable any base-body renderer still off (e.g. snapshot lost
            // across a re-enable), so the hero can never be left invisible.
            Transform baseBody = ResolveBaseBody();
            if (baseBody != null)
            {
                foreach (var r in baseBody.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (r == null) continue;
                    if (!r.enabled) { r.enabled = true; shown++; }
                }
            }

            if (shown > 0)
                FlowTrace.Step("ArmorVisual", $"RestoreBaseBody: re-enabled {shown} base SkinnedMeshRenderer(s).");
        }

        // Resolve the BASE body to hide/seat/retarget against. The player hero names it
        // "HeroBody" (HeroBodySwapper); a companion keeps the Blink/Tripo prefab's own name
        // (StoryCompanionInjector skins via VisualFactory without renaming), so fall back to the
        // child that carries the humanoid Animator. NEVER returns the armored instance itself.
        private Transform ResolveBaseBody()
        {
            Transform named = transform.Find(BodyChildName);
            if (named != null) return named;

            // Fallback: the first direct child with an Animator that ISN'T our armored instance.
            foreach (Transform child in transform)
            {
                if (child == null) continue;
                if (_armorInstance != null && child == _armorInstance.transform) continue;
                if (child.name == ArmorBodyName) continue;
                if (child.GetComponentInChildren<Animator>() != null) return child;
            }
            return null;
        }

        private void DestroyArmorInstance()
        {
            if (_armorInstance != null)
            {
                Destroy(_armorInstance);
                _armorInstance = null;
            }
        }

        // Release the held Addressables armor handle (no-op if none open). ONE owner — called on
        // every swap / unequip / OnDisable so a Blink prefab never leaks.
        private void ReleaseArmorHandle()
        {
            if (!_armorHandleOpen) return;
            _armorHandleOpen = false;
            if (_armorHandle.IsValid())
                Addressables.Release(_armorHandle);
            _armorHandle = default;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        /// <summary>True when the armor loads its body via Addressables (Blink "gear/armor/"
        /// scheme or an explicit loadVia=="addressable"). Mirrors EquipmentController's weapon
        /// rule so both layers pick the same load path. Legacy/Tripo rows (null/empty) => false
        /// (the cloth default — restored as the base body, no swap).</summary>
        private static bool IsBlinkArmor(ArmorDef def)
        {
            if (def == null) return false;
            if (!string.IsNullOrEmpty(def.loadVia) &&
                def.loadVia.Equals("addressable", StringComparison.OrdinalIgnoreCase))
                return true;
            return !string.IsNullOrEmpty(def.prefabPath) &&
                   def.prefabPath.StartsWith("gear/armor/", StringComparison.OrdinalIgnoreCase);
        }

        // Strip helper components a character FBX/prefab can carry (camera / audio listener /
        // light / physics) so the armored body is JUST the mesh — mirrors HeroBodySwapper +
        // StoryCompanionInjector.StripEmbeddedFbxArtifacts. Null-safe; renderers untouched.
        private static void StripEmbeddedArtifacts(GameObject root)
        {
            if (root == null) return;
            foreach (var cam in root.GetComponentsInChildren<Camera>(true)) if (cam != null) Destroy(cam);
            foreach (var al in root.GetComponentsInChildren<AudioListener>(true)) if (al != null) Destroy(al);
            foreach (var lt in root.GetComponentsInChildren<Light>(true)) if (lt != null) Destroy(lt);
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) Destroy(rb);
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) if (c != null) Destroy(c);
        }

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursive(child.gameObject, layer);
        }
    }
}
