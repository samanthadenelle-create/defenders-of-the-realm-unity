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
using System.Collections;
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

        // BUG 1: when the Blink body resolves before a base body exists (e.g. Village async
        // HeroBodySwapper still building, OR the hub baked placeholder which never gets a
        // 'HeroBody'), we cache the pending swap and re-attempt for a BOUNDED window as the body
        // may still arrive. Past the window with no body, we release the handle and keep the
        // existing base body shown (never-naked) — on a bodyless hub hero this is EXPECTED.
        private const int MaxBodyWaitFrames = 120;   // ~2s at 60fps — mirrors HeroBowAttachment
        private GameObject _pendingPrefab;
        private ArmorDef   _pendingArmor;
        private string     _pendingAddress;
        private int        _pendingGeneration;
        private Coroutine  _bodyWaitRoutine;

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
            ClearPendingSwap();
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
            ClearPendingSwap();   // a queued body-wait from a prior request no longer applies
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
                        $"Stale armor load for '{address}' (gen {generation} != {_equipGeneration}) — deferring release.");
                    // BUG 2 FIX: do NOT call Addressables.Release(op) synchronously here — we are
                    // INSIDE the SDK's OnHandleCompleted dispatch over this same handle. A re-entrant
                    // Release invalidates the handle the SDK is still reading (handle.Status), which
                    // throws "Attempting to use an invalid operation handle". Defer the release to the
                    // next frame so the SDK finishes its dispatch first.
                    if (_armorHandle.Equals(op)) { _armorHandle = default; _armorHandleOpen = false; }
                    DeferRelease(op);
                    return;
                }

                if (!op.IsValid() || op.Status != AsyncOperationStatus.Succeeded || op.Result == null)
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
                // BUG 1: no Blink base body to seat/retarget against YET. Two cases:
                //   • Village: HeroBodySwapper's async body build is still in flight — the body
                //     will appear shortly. Cache the pending swap + re-attempt for a bounded window.
                //   • Hub (MainCastle_Hall): HeroBodySwapper never runs, so the baked placeholder
                //     hero has no 'HeroBody' and never will. The bounded poll expires quietly, we
                //     release the handle, and keep the placeholder body shown (never-naked). This is
                //     EXPECTED on a bodyless hero — downgrade from Fail to a single Warn/Once.
                CachePendingSwap(prefab, armor, address);
                FlowTrace.Once("ArmorVisual", $"armorbody-wait:{armor.id}",
                    $"BuildArmorBody: no '{BodyChildName}' base body yet for armor '{armor.id}' — " +
                    "waiting (bounded) for an async Blink body; will keep base body if none arrives.");
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

            // ── URP MATERIAL SAFETY NET (pink-'Body' fix, MAGENTA-MATERIAL probe) ─────────
            // Blink armor packs can ship Built-in/Standard (or Phong) shaders; a URP player
            // STRIPS those → the armored SkinnedMeshRenderer (named "Body") falls back to
            // Hidden/InternalErrorShader = magenta. HeroBodySwapper.RetargetMaterialsToUrp fixes
            // legacy Resources bodies, but this Addressables armor OVERLAY never retargeted — so a
            // Blink "Body" rendered pink in OuterWorld. Swap any null/Standard/InternalError
            // shader to URP/Lit before the body is shown.
            EnsureMaterialsUrp(instance);

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

            // RENDER-VERIFY (owner directive 2026-06-19: "anything that renders can be broken —
            // check render==true and roll back the error"). retarget==true + a valid avatar can
            // STILL leave armor-only-in-T-pose if no skinned mesh is enabled or the controller
            // drives nothing (the companion symptom). PROVE the armored body can render a posed
            // character BEFORE we hide the base. If it can't, ROLL BACK and keep the base body —
            // the failure self-reports (Fail -> break-log) and never reaches the player as broken.
            if (!VerifyArmorRendersNow(instance, armor.id))
            {
                RollbackArmor(instance, $"render-verify failed for armor '{armor.id}' (no visible skinned mesh or no bound animator)");
                return;
            }

            // Armor proven renderable -> now safe to hide the base body. Pass the armored INSTANCE
            // so HideBaseBody can detect whether this Blink set is FULL-BODY (ships its own head/
            // hands/skin — the documented Blink "full-body outfit SET", BLINK_NOTES.md) vs pieces-
            // only. A full-body set + kept base skin = TWO heads/hands on TWO animators that drift
            // out of phase — the owner-reported "head and armor not in sync / parts not joined".
            HideBaseBody(baseBody, instance);

            // RE-SEAT EQUIPPED PROPS onto the ARMORED body's hands. The off-hand/weapons were seated on
            // the now-hidden BASE hand; a Blink full-body set has slightly different rig proportions, so
            // the visible armor hand sits elsewhere -> the owner-reported "shield hangs off the arm".
            // EquipmentController re-points its animator at the armor body + re-equips (no magic offsets;
            // it resolves the hand by humanoid bone id on the new rig).
            var equip = GetComponentInParent<EquipmentController>();
            if (equip != null) equip.ReseatForBody(instance);

            // WO-455: if this is a story companion, re-point its locomotion animator at the ARMORED
            // body — else StoryCompanion keeps driving the now-hidden base animator and the visible
            // armored body T-poses (owner-reported "companion changed gear, still T-pose").
            var companionSwap = GetComponent<StoryCompanion>();
            if (companionSwap != null)
                companionSwap.SetActiveAnimator(instance != null ? instance.GetComponentInChildren<Animator>() : null);

            FlowTrace.Step("ArmorVisual",
                $"BuildArmorBody: armored body '{armor.id}' shown (address='{address}'), base body hidden.");

            // DEFERRED pose-verify: the animator hasn't evaluated yet this frame. A bound animator
            // whose controller has NO clip for this rig leaves the body frozen in bind/T-pose —
            // the owner-reported "companion is just armor in a T-pose". Watch the next few frames;
            // if nothing ever drives the rig, ROLL BACK to the base body.
            if (isActiveAndEnabled)
                StartCoroutine(VerifyPoseThenMaybeRollback(instance, armor.id, _equipGeneration));
        }

        // URP MATERIAL SAFETY NET: Blink armor packs can ship Built-in/Standard shaders that a URP
        // player strips → magenta Hidden/InternalErrorShader on the body (owner-seen "pink" in
        // OuterWorld; the MAGENTA-MATERIAL probe named it: renderer 'Body', scene OuterWorld). Swap
        // any null/Standard/InternalError shader on the armored instance to URP/Lit, preserving each
        // material's colour + albedo. Mirrors HeroBodySwapper.RetargetMaterialsToUrp, scoped to the
        // Addressables armor overlay (which never retargeted). One-shot per equip — acceptable cost.
        private static void EnsureMaterialsUrp(GameObject instance)
        {
            if (instance == null) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (lit == null)
            {
                FlowTrace.Warn("ArmorVisual",
                    "EnsureMaterialsUrp: no URP/Lit or Standard shader found — leaving authored materials (may render magenta).");
                return;
            }

            int fixedCount = 0;
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.materials; // instance copies — safe to mutate + reassign
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    var sh = m.shader;
                    string sn = sh != null ? sh.name : null;
                    bool broken = sh == null
                                  || sn == "Standard"
                                  || (sn != null && sn.Contains("InternalErrorShader"));
                    if (!broken) continue;

                    // Preserve authored colour + albedo across the shader swap.
                    Color col = m.HasProperty("_Color") ? m.color : Color.white;
                    Texture tex = m.HasProperty("_MainTex") ? m.mainTexture : null;
                    m.shader = lit;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
                    if (m.HasProperty("_Color")) m.color = col;
                    if (tex != null)
                    {
                        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                        if (m.HasProperty("_MainTex")) m.mainTexture = tex;
                    }
                    changed = true;
                    fixedCount++;
                }
                if (changed) r.materials = mats;
            }

            if (fixedCount > 0)
                FlowTrace.Step("ArmorVisual",
                    $"EnsureMaterialsUrp: retargeted {fixedCount} magenta/Built-in material(s) on the armored body to URP/Lit.");
        }

        // RENDER-VERIFY (synchronous, no camera/scene dependency): the armored instance MUST have
        // >=1 ENABLED SkinnedMeshRenderer carrying a sharedMesh AND a bound humanoid animator
        // (runtimeAnimatorController + valid avatar). Traces the exact counts so a capture splits
        // "no visible mesh" vs "no animator binding" with zero guessing. Returns false => caller rolls back.
        private bool VerifyArmorRendersNow(GameObject instance, string armorId)
        {
            if (instance == null)
            {
                FlowTrace.Fail("ArmorVisual", $"VerifyArmorRenders: armor '{armorId}' instance is null.");
                return false;
            }

            int total = 0, enabledSkin = 0, withMesh = 0;
            var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in skins)
            {
                if (r == null) continue;
                total++;
                if (r.enabled) enabledSkin++;
                if (r.sharedMesh != null) withMesh++;
            }

            var anim = instance.GetComponentInChildren<Animator>();
            string ctrl = (anim != null && anim.runtimeAnimatorController != null) ? anim.runtimeAnimatorController.name : "<null>";
            bool avatarOk = anim != null && anim.avatar != null && anim.avatar.isValid;
            bool animBound = anim != null && anim.runtimeAnimatorController != null && avatarOk;
            bool renders = enabledSkin > 0 && withMesh > 0;

            FlowTrace.Step("ArmorVisual",
                $"VerifyArmorRenders armor='{armorId}' on '{name}': skinned total={total} enabled={enabledSkin} withMesh={withMesh}; " +
                $"animator='{(anim == null ? "<none>" : anim.name)}' controller='{ctrl}' avatarValid={avatarOk} => renders={renders} animBound={animBound}");

            if (!renders || !animBound)
            {
                FlowTrace.Fail("ArmorVisual",
                    $"VerifyArmorRenders FAILED armor='{armorId}' on '{name}': renders={renders} (enabledSkin={enabledSkin}, withMesh={withMesh}) " +
                    $"animBound={animBound} (controller='{ctrl}', avatarValid={avatarOk}).");
                return false;
            }
            return true;
        }

        // ROLL BACK a half-built armor: drop the armored instance, RE-SHOW the base body (never a
        // hidden base + broken armor), release the handle. The base body is the never-naked fallback.
        // Control-flow safety — always runs (not behind the FlowTrace toggle).
        private void RollbackArmor(GameObject instance, string reason)
        {
            FlowTrace.Fail("ArmorVisual", $"RollbackArmor on '{name}': {reason} — restoring base body, dropping armor.");
            if (_armorInstance == instance) _armorInstance = null;
            if (instance != null) Destroy(instance);
            RestoreBaseBody();
            ReleaseArmorHandle();
        }

        // DEFERRED pose-verify: over the next few frames confirm the armored body is actually
        // PLAYING a clip (layer-0 clipCount > 0), not frozen in the bind/T-pose. Only rolls back
        // if NOTHING ever drives the rig within the window (robust against a transient 0 during a
        // first-frame transition). A rollback shows the base body (never-naked), never an invisible hero.
        private IEnumerator VerifyPoseThenMaybeRollback(GameObject instance, string armorId, int generation)
        {
            // The TRUE animating test is whether normalizedTime ADVANCES frame-to-frame.
            // GetCurrentAnimatorClipInfoCount(0) > 0 is a FALSE POSITIVE: a state with an Idle clip
            // assigned reports clipCount=1 even while FROZEN (not ticked / speed 0) — which is exactly
            // the owner-reported visible T-pose the old check passed. Measure advancement, and capture
            // WHY (animSpeed, the Speed param) so a capture names the cause, not just the symptom.
            const int MaxPoseFrames = 8;
            bool clipAssigned = false, advancing = false, animActive = false, hasSpeed = false;
            int lastCount = -1; float prevNT = -1f, speedVal = 0f, animSpeed = 0f;
            for (int i = 0; i < MaxPoseFrames; i++)
            {
                yield return null;
                // Superseded by a newer swap / unequip / disable — that owner handles it now.
                if (generation != _equipGeneration || instance == null || _armorInstance != instance)
                    yield break;

                var anim = instance.GetComponentInChildren<Animator>();
                if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) continue;
                animActive = true; animSpeed = anim.speed;
                lastCount = anim.GetCurrentAnimatorClipInfoCount(0);
                if (lastCount > 0) clipAssigned = true;
                float nt = anim.GetCurrentAnimatorStateInfo(0).normalizedTime;
                if (prevNT >= 0f && Mathf.Abs(nt - prevNT) > 0.0001f) advancing = true;
                prevNT = nt;
                if (!hasSpeed)
                    foreach (var p in anim.parameters)
                        if (p.name == "Speed") { hasSpeed = true; speedVal = anim.GetFloat("Speed"); break; }
            }

            FlowTrace.Step("ArmorVisual",
                $"VerifyPose armor='{armorId}' on '{name}': animActive={animActive} clipAssigned={clipAssigned} " +
                $"normalizedTimeADVANCING={advancing} (the REAL test) animSpeed={animSpeed:0.00} " +
                $"hasSpeedParam={hasSpeed} speed={speedVal:0.00} clipCount={lastCount}.");

            // FROZEN: active + a clip assigned but normalizedTime never advanced = the visible T-pose.
            if (animActive && clipAssigned && !advancing)
                FlowTrace.Fail("ArmorVisual",
                    $"FROZEN armor body '{armorId}' on '{name}': clip assigned but normalizedTime NOT advancing " +
                    $"(animSpeed={animSpeed:0.00}, hasSpeedParam={hasSpeed}, speed={speedVal:0.00}) — the rig is not being " +
                    "ticked/driven = the owner's T-pose. The old clipCount check was a false 'OK'.");

            if (!clipAssigned)
                RollbackArmor(instance,
                    $"deferred pose-verify: armored body never assigned a clip (lastClipCount={lastCount})");
        }

        // BUG 1: cache a swap whose Blink body resolved before a base body existed, and start a
        // BOUNDED frame poll waiting for the base body (Village async build). On a hub bodyless
        // hero the poll simply expires and we keep the base body — no naked hero, no error spam.
        private void CachePendingSwap(GameObject prefab, ArmorDef armor, string address)
        {
            _pendingPrefab     = prefab;
            _pendingArmor      = armor;
            _pendingAddress    = address;
            _pendingGeneration = _equipGeneration;
            if (_bodyWaitRoutine == null && isActiveAndEnabled)
                _bodyWaitRoutine = StartCoroutine(WaitForBaseBodyThenBuild());
        }

        private void ClearPendingSwap()
        {
            _pendingPrefab  = null;
            _pendingArmor   = null;
            _pendingAddress = null;
            if (_bodyWaitRoutine != null) { StopCoroutine(_bodyWaitRoutine); _bodyWaitRoutine = null; }
        }

        private IEnumerator WaitForBaseBodyThenBuild()
        {
            for (int frame = 0; frame < MaxBodyWaitFrames; frame++)
            {
                yield return null;

                // Superseded: a newer equip (or unequip/disable) bumped the generation. Drop the
                // pending swap; that newer request (or OnDisable) owns the handle/base body now.
                if (_pendingArmor == null || _pendingGeneration != _equipGeneration)
                {
                    _bodyWaitRoutine = null;
                    yield break;
                }

                if (ResolveBaseBody() != null)
                {
                    // The async Blink base body arrived — finish the swap we deferred.
                    GameObject prefab = _pendingPrefab;
                    ArmorDef   armor  = _pendingArmor;
                    string     addr   = _pendingAddress;
                    _bodyWaitRoutine = null;
                    _pendingPrefab = null; _pendingArmor = null; _pendingAddress = null;
                    BuildArmorBody(prefab, armor, addr);
                    yield break;
                }
            }

            // Bounded window expired with no base body — the hub bodyless-placeholder case. Release
            // the addressable handle and keep whatever body is already shown (never-naked contract).
            _bodyWaitRoutine = null;
            ArmorDef pending = _pendingArmor;
            _pendingPrefab = null; _pendingArmor = null; _pendingAddress = null;
            ReleaseArmorHandle();
            FlowTrace.Once("ArmorVisual", $"armorbody-nobody:{pending?.id ?? "<null>"}",
                $"BuildArmorBody: no '{BodyChildName}' base body appeared for armor '{pending?.id ?? "<null>"}' " +
                "within the wait window — bodyless hero (expected in the hub); kept existing body, released handle.");
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
        //
        // DOUBLED-BODY FIX (2026-06-19, owner "head and armor not in sync / parts not joined"):
        // Blink armor sets are documented FULL-BODY OUTFIT SETS (HumanMale/HumanFemale — they ship
        // their OWN head/hands/skin, BLINK_NOTES.md). If we KEEP the base body's head/hands visible
        // while ALSO showing a full-body armor instance, the hero carries TWO heads + TWO hand sets
        // driven by TWO separate animators (base + armor instance). Those animators evaluate on
        // different cull/update phases and DRIFT — exactly the reported desync. So we first detect
        // whether THIS armor set ships its own skin: if it does (full-body), hide EVERYTHING on the
        // base (one body, one animator → joined + in-sync). Only when the armor is pieces-only (no
        // own head/hands) do we keep the base skin visible under the overlay (the never-naked path).
        private void HideBaseBody(Transform baseBody, GameObject armorInstance)
        {
            if (baseBody == null) return;

            // Does the armored instance carry its OWN skin (head/hands/face/hair)? If yes it is a
            // full-body set and the base skin must be hidden too (no doubled, desyncing body).
            bool armorHasOwnSkin = ArmorShipsOwnSkin(armorInstance);

            var renderers = baseBody.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int hidden = 0, keptSkin = 0;
            // Snapshot ONLY what we actually hid, so RestoreBaseBody re-enables exactly that set.
            var hiddenList = new System.Collections.Generic.List<SkinnedMeshRenderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                // FULL-BODY armor set: hide ALL base renderers (incl. skin) so only the armor body's
                // single rig drives the visible character — no second head/hands on a second animator.
                // PIECES-ONLY armor: keep the base skin (head/hands/hair) visible UNDER the armor
                // pieces, hiding only the torso/limb/clothing the armor replaces (never-naked path).
                if (!armorHasOwnSkin && IsSkinRenderer(r.name)) { keptSkin++; continue; }
                if (r.enabled) { r.enabled = false; hidden++; }
                hiddenList.Add(r);
            }
            _hiddenBaseRenderers = hiddenList.ToArray();

            if (armorHasOwnSkin)
            {
                FlowTrace.Step("ArmorVisual",
                    $"HideBaseBody: armor is a FULL-BODY set (ships its own skin) — hid ALL {hidden} base " +
                    "renderer(s) so one rig drives the character (no doubled/desyncing head+hands).");
            }
            else if (keptSkin == 0)
            {
                // SELF-REPORT (§12): pieces-only armor but we recognised no skin mesh on the base body
                // — the hero may read bare. Means the base body's skin renderers don't match the
                // IsSkinRenderer keywords (wrong base resolved, or unexpected mesh names) — pinpoint.
                FlowTrace.Warn("ArmorVisual",
                    $"HideBaseBody: kept 0 skin renderer(s) on '{baseBody.name}' " +
                    $"({renderers?.Length ?? 0} found, hid {hidden}) — no recognised skin mesh; " +
                    "hero may still read bare under the pieces-only armor. Check base-body renderer names.");
            }
            else
            {
                FlowTrace.Step("ArmorVisual",
                    $"HideBaseBody: pieces-only armor — hid {hidden} base clothing renderer(s), kept {keptSkin} skin renderer(s) under the armor.");
            }
        }

        // Detect whether a Blink armor INSTANCE ships its OWN skin (head/hands/face/hair) — i.e. it
        // is a full-body outfit set, not armor pieces. Scans the instance's SkinnedMeshRenderers for
        // a skin-keyword name (the same vocabulary IsSkinRenderer uses for the base). Conservative:
        // a single recognised head/hand/face mesh on the armor => full-body. Traced so a capture
        // shows exactly which path HideBaseBody took (and why) with zero guessing.
        private static bool ArmorShipsOwnSkin(GameObject armorInstance)
        {
            if (armorInstance == null) return false;
            var skins = armorInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int total = 0, skinNamed = 0;
            bool hasHead = false;
            var names = new System.Text.StringBuilder();
            foreach (var r in skins)
            {
                if (r == null) continue;
                total++;
                if (names.Length > 0) names.Append(", ");
                names.Append(r.name);
                string n = r.name.ToLowerInvariant();
                // Owner fix 2026-06-20: Blink full-body sets ship the body under generic names ('Body'/
                // 'Torso'/'Legs'), which the base IsSkinRenderer vocabulary (head/hand/face) misses — so a
                // full-body set false-read as pieces-only and the base head/hands stayed = double head.
                // Count body/torso too, and flag a HEAD renderer (face/hair) as a definitive full-body tell.
                bool head = n.Contains("head") || n.Contains("face") || n.Contains("hair");
                bool bodyish = n.Contains("body") || n.Contains("torso") || n.Contains("legs");
                if (head) hasHead = true;
                if (head || bodyish || IsSkinRenderer(r.name)) skinNamed++;
            }
            // Full-body if it ships a HEAD (it replaces the face → must hide the base head) OR >=2 skin/
            // body meshes (head+body / body+hands). A lone pieces-only overlay (skinNamed<2, no head)
            // stays pieces-only so the base skin shows under it. §12: the renderer-name list is logged so
            // a residual case still self-identifies.
            bool fullBody = hasHead || skinNamed >= 2;
            FlowTrace.Step("ArmorVisual",
                $"ArmorShipsOwnSkin: {total} skinned renderer(s) [{names}], skinNamed={skinNamed} " +
                $"hasHead={hasHead} => fullBody={fullBody}.");
            return fullBody;
        }

        // Blink/Tripo base-body renderer names that are SKIN / face / hair and must stay visible UNDER
        // the armor overlay (armor SET prefabs bring no skin). Everything NOT matched here (torso, legs,
        // arms, feet, built-in cloth) is a mesh the armor replaces and is hidden by HideBaseBody.
        private static bool IsSkinRenderer(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            n = n.ToLowerInvariant();
            return n.Contains("head") || n.Contains("hand") || n.Contains("neck") ||
                   n.Contains("face") || n.Contains("ear")  || n.Contains("eye")  ||
                   n.Contains("brow") || n.Contains("lash") || n.Contains("hair") ||
                   n.Contains("beard") || n.Contains("moustache") || n.Contains("mustache") ||
                   n.Contains("teeth") || n.Contains("tongue");
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

            // WO-455: re-resolve a story companion's locomotion animator back to the restored base
            // body (the swap had re-pointed it at the now-removed armor instance).
            GetComponent<StoryCompanion>()?.RebindAnimator();

            if (shown > 0)
                FlowTrace.Step("ArmorVisual", $"RestoreBaseBody: re-enabled {shown} base SkinnedMeshRenderer(s).");

            // Re-seat equipped props back onto the BASE body's hands — the armor body (and our re-pointed
            // animator) is going away, so the off-hand must not be left following a destroyed armor rig.
            if (baseBody != null)
            {
                var equip = GetComponentInParent<EquipmentController>();
                if (equip != null) equip.ReseatForBody(baseBody.gameObject);
            }
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

        // BUG 2: release a handle ONE FRAME LATER, off the SDK's OnHandleCompleted dispatch. Calling
        // Addressables.Release(op) synchronously inside the Completed delegate is re-entrant — the SDK
        // is still mid-dispatch over that handle and then reads handle.Status on an already-released
        // (invalid) handle. Deferring a frame lets the dispatch finish before we free it. Guarded by
        // IsValid() at release-time in case something else already released it.
        private void DeferRelease(AsyncOperationHandle<GameObject> op)
        {
            // Can't StartCoroutine on a disabled/destroyed component — release directly in that case
            // (we are no longer inside the original Completed dispatch by the time OnDisable runs).
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
