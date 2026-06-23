// =============================================================================
// HeroBodySwapper — at scene start, swap the hero's placeholder Wizard body
// for the FBX matching the player's chosen HeroClass (Knight / Ranger / Mage).
// -----------------------------------------------------------------------------
// VillageSceneBuilder bakes the scene with a Wizard placeholder (since the
// builder runs at edit time and doesn't see runtime state). This component
// runs at runtime, reads GameStateService.State.HeroClass, and if the choice
// isn't Mage it loads the matching FBX from Resources/Heroes/<slug>.fbx,
// destroys the old "HeroBody" child, and instantiates the new mesh in its
// place. Idempotent — only runs once per scene.
//
// Resources/Heroes/<slug>.fbx is the canonical pickup path because Resources
// is auto-included in player builds (no Addressables wiring needed for the
// hand-imported Tripo FBXs).
// =============================================================================

using DeNelle.Core.Combat; // ActorAnimator for post-swap battle ready / full anim drive (Village + World)
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;          // FlowTrace / Guard — §12 instrument the async body-build
using UnityEngine;
using UnityEngine.AddressableAssets;      // Blink base body lives OUTSIDE Resources (gitignored) → Addressables
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class HeroBodySwapper : MonoBehaviour
    {
        private const float TargetHeightMeters = 1.75f;
        // Global animator playback multiplier for the hero (Mixamo clips run fast).
        // 0.5 = half speed. Tune here.
        private const float HeroAnimSpeed = 0.5f;

        // WO-376: the swapped hero's guarded animator + the Yarn runner we hook so the
        // hero RE-asserts a clean idle when dialogue starts (and on complete). Cached so
        // OnDestroy can unsubscribe and a deferred FindDialogueRunner retry can wire late.
        private ActorAnimator _heroActor;
        private Yarn.Unity.DialogueRunner _dialogueRunner;

        // Blink LowPoly base body address (set on the prefab by BlinkAddressableMarker.MarkBlinkGear).
        // The gitignored base body lives OUTSIDE Resources, so it loads via Addressables — NOT
        // Resources.Load. Every class shares this one rig; the class only picks the DEFAULT armor.
        private const string BlinkBaseBodyAddress = "hero/base/HumanMale";

        // The async base-body load handle — ONE owner (this component); released on OnDestroy so the
        // Blink base prefab never leaks. Open-flag guards a double-release.
        private AsyncOperationHandle<GameObject> _baseBodyHandle;
        private bool _baseBodyHandleOpen;

        private void Start()
        {
            using var _ = FlowTrace.Enter("HeroBody", "Start (Blink base body)");

            HeroClass cls = ResolveHeroClass();
            string slug = SlugFor(cls) ?? "Mage"; // Mage maps to null in SlugFor → controller/abilities default

            // Remove the old (baked placeholder) body FIRST and snapshot its controller as a
            // last-ditch fallback. The Blink base loads async, so we destroy the placeholder now
            // and the new body appears a frame later (the hero root + camera persist).
            RuntimeAnimatorController controllerSnapshot = null;
            var old = transform.Find("HeroBody");
            if (old != null)
            {
                var oldAnim = old.GetComponentInChildren<Animator>();
                if (oldAnim != null) controllerSnapshot = oldAnim.runtimeAnimatorController;
                Destroy(old.gameObject);
            }

            FlowTrace.Step("HeroBody", $"class={cls} slug={slug} — kicking Blink base load '{BlinkBaseBodyAddress}'.");
            BeginBlinkBaseLoad(cls, slug, controllerSnapshot);
        }

        // Kick off the async Addressables load of the Blink LowPoly base body. GUARDED (§12, no
        // silent failure): a throw or a failed handle logs via FlowTrace.Fail and falls back to the
        // legacy Resources/Heroes body so the hero is NEVER left bodyless.
        private void BeginBlinkBaseLoad(HeroClass cls, string slug, RuntimeAnimatorController controllerSnapshot)
        {
            AsyncOperationHandle<GameObject> handle;
            try
            {
                handle = Addressables.LoadAssetAsync<GameObject>(BlinkBaseBodyAddress);
            }
            catch (System.Exception ex)
            {
                FlowTrace.Fail("HeroBody",
                    $"Blink base load threw for '{BlinkBaseBodyAddress}': {ex.GetType().Name}: {ex.Message} — " +
                    "falling back to legacy Resources/Heroes body (no bodyless hero).");
                BuildLegacyResourcesBody(cls, slug, controllerSnapshot);
                return;
            }

            _baseBodyHandle = handle;
            _baseBodyHandleOpen = true;

            handle.Completed += op =>
            {
                if (this == null) return; // destroyed mid-load
                if (op.Status != AsyncOperationStatus.Succeeded || op.Result == null)
                {
                    FlowTrace.Fail("HeroBody",
                        $"Blink base load FAILED for '{BlinkBaseBodyAddress}' (status={op.Status}) — " +
                        "falling back to legacy Resources/Heroes body (no bodyless hero).");
                    ReleaseBaseBodyHandle();
                    BuildLegacyResourcesBody(cls, slug, controllerSnapshot);
                    return;
                }

                // CRITICAL async-ordering (the "sliding statue / null animator" class): ALL post-load
                // wiring runs INSIDE this callback, never synchronously after the async kickoff.
                BuildBlinkHeroBody(op.Result, cls, slug, controllerSnapshot);
            };
        }

        private void ReleaseBaseBodyHandle()
        {
            if (!_baseBodyHandleOpen) return;
            _baseBodyHandleOpen = false;
            if (_baseBodyHandle.IsValid()) Addressables.Release(_baseBodyHandle);
            _baseBodyHandle = default;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // BLINK PATH: build the playable hero body from the Blink LowPoly base prefab, then run
        // the full post-load wiring. Blink ships CLEAN URP materials + a CLEAN bind pose, so the
        // Tripo/CC5 workarounds (RetargetMaterialsToUrp, ApplyExtractedTexture, ApplyFlatSteelStopgap,
        // ApplyClassTint, the embedded camera/listener strip, PlantFeetOnGround's CC5 sole nudge) are
        // BYPASSED — those methods stay in the file (dead for Blink, reachable for the legacy path).
        // ─────────────────────────────────────────────────────────────────────────────
        private void BuildBlinkHeroBody(GameObject prefab, HeroClass cls, string slug,
                                        RuntimeAnimatorController controllerSnapshot)
        {
            using var _ = FlowTrace.Enter("HeroBody", $"BuildBlinkHeroBody class={cls}");

            // Build through the ONE shared factory (same path the companion + enemies use). Blink
            // ships a clean bind pose, so FixTripoMaterials stays OFF and we do NOT apply a forward
            // yaw guess — the Blink rig exports facing +Z already; AlignBodyFacingToRoot below
            // self-corrects from the skeleton if any residual remains (no hard-coded -90° hack).
            float targetH = (cls == HeroClass.Knight) ? TargetHeightMeters * 1.0286f : TargetHeightMeters;
            GameObject body = null;
            Guard.Try("HeroBody", "VisualFactory.Skin (Blink base)", () =>
            {
                body = VisualFactory.Skin(transform, prefab, new SkinOptions
                {
                    FitHeight = targetH,
                    StripColliders = true,
                    SeatOnGround = true,
                    FixTripoMaterials = false,
                });
            });
            if (body == null)
            {
                FlowTrace.Fail("HeroBody",
                    "VisualFactory.Skin returned null for the Blink base — falling back to legacy Resources body.");
                ReleaseBaseBodyHandle();
                BuildLegacyResourcesBody(cls, slug, controllerSnapshot);
                return;
            }
            body.name = "HeroBody"; // EquipmentController.CacheRig finds it by transform.Find("HeroBody")
            FlowTrace.Step("HeroBody", "Blink base body skinned + named 'HeroBody'.");

            WireHeroBody(body, cls, slug, controllerSnapshot, isBlink: true);
        }

        // LEGACY FALLBACK: the pre-Blink Resources/Heroes/<slug>.fbx body, kept so a missing Blink
        // pack (gitignored / not yet marked Addressable) never leaves the hero bodyless. Runs the
        // full Tripo/CC5 material + facing pipeline as before.
        private void BuildLegacyResourcesBody(HeroClass cls, string slug,
                                              RuntimeAnimatorController controllerSnapshot)
        {
            using var _ = FlowTrace.Enter("HeroBody", $"BuildLegacyResourcesBody slug={slug}");

            // §12: the Resources.Load + Skin were un-Guarded (unlike the Blink path) and a null
            // body bailed SILENTLY — the playable hero went bodyless with no self-report. Guard both
            // and Fail-loud on null so a run pinpoints WHICH step left the hero without a body.
            GameObject prefab = null;
            Guard.Try("HeroBody", $"Resources.Load Heroes/{slug}", () =>
            {
                prefab = Resources.Load<GameObject>("Heroes/" + slug);
            });
            if (prefab == null)
            {
                FlowTrace.Fail("HeroBody",
                    "DEF-229 — Blink base unavailable AND Resources/Heroes/" + slug +
                    ".fbx is MISSING. The hero is left bodyless. Mark the Blink base Addressable " +
                    "(Defenders → Catalog → Mark Blink Gear Addressable) or import the legacy FBX.");
                return;
            }

            float targetH = (cls == HeroClass.Knight) ? TargetHeightMeters * 1.0286f : TargetHeightMeters;
            // WO-326: -90f is the proven legacy forward-yaw for the Tripo/AccuRIG FBXs.
            float forwardYaw = -90f;
            GameObject body = null;
            Guard.Try("HeroBody", "VisualFactory.Skin (legacy Resources)", () =>
            {
                body = VisualFactory.Skin(transform, prefab, new SkinOptions
                {
                    FitHeight = targetH,
                    StripColliders = true,
                    SeatOnGround = true,
                    FixTripoMaterials = false,
                    LocalRotation = Quaternion.Euler(0f, forwardYaw, 0f),
                });
            });
            if (body == null)
            {
                FlowTrace.Fail("HeroBody",
                    $"VisualFactory.Skin returned null for legacy Resources/Heroes/{slug} — hero left bodyless.");
                return;
            }
            body.name = "HeroBody";

            WireHeroBody(body, cls, slug, controllerSnapshot, isBlink: false);
        }

        // Shared post-load wiring for BOTH the Blink and legacy body. On the Blink path the
        // Tripo/CC5 material + embedded-strip + sole-nudge steps are skipped (clean assets).
        private void WireHeroBody(GameObject body, HeroClass cls, string slug,
                                  RuntimeAnimatorController controllerSnapshot, bool isBlink)
        {
            // DEF-232 spawn-time validation: the camera (SmartMobileCamera) follows the hero
            // ROOT by tag; HeroLocomotion drives that same root. The visible body must sit
            // centred over the root (no horizontal offset) or the camera frames empty space
            // beside her. Log LOUDLY if Skin left the body off-centre so this regression is
            // caught at the source instead of re-surfacing as a "camera stays to her right" bug.
            ValidateBodyCentredOverRoot(body);

            // DEF-235: plant the feet on the ground (LEGACY ONLY). The CC5/AccuRIG bodies float a
            // few cm because their SkinnedMeshRenderer bounds carry bind-pose padding. Blink ships a
            // clean bind pose seated by VisualFactory.SeatOnGround, so the CC5 sole nudge is BYPASSED.
            StripRigidbodies(body);
            if (!isBlink)
            {
                PlantFeetOnGround(body);
                // Tripo FBXs embed a CAMERA / AudioListener; strip both so only the hero's follow
                // camera drives the display. Blink prefabs are clean — no embedded artifacts.
                foreach (var cam in body.GetComponentsInChildren<Camera>(true))
                    if (cam != null) Destroy(cam);
                foreach (var al in body.GetComponentsInChildren<AudioListener>(true))
                    if (al != null) Destroy(al);
                // Tripo/CC5 materials are Phong/Lambert/null → retarget to URP. Blink ships clean
                // URP materials already, so this pass is BYPASSED (no double-process).
                RetargetMaterialsToUrp(body);
            }
            // Owner 2026-05-25 "the rig exists, the animation exists" — wire the
            // REAL Walk/Cast animation. The per-class controller is generated by
            // HeroAnimatorSetup (Defenders → Animation → Setup <Class> Animator),
            // which extracts the FBX's embedded NLA takes (Walk = longer take,
            // Cast = shorter) and builds an Idle/Walk/Cast machine on the Speed
            // (float) + Cast (trigger) params HeroLocomotion/HeroAbilities drive.
            //
            // The OLD path snapshotted the *placeholder* body's controller — but
            // the baked placeholder (Wizard.fbx) is MISSING, so its Animator and
            // the snapshot were always null and this whole block was skipped,
            // leaving the hero un-animated (the "sliding statue"). Load the
            // controller directly from Resources/Heroes/<slug>.controller instead;
            // fall back to any carried-over snapshot only if that asset is absent.
            var controller = Resources.Load<RuntimeAnimatorController>("Heroes/" + slug)
                             ?? controllerSnapshot;

            var anim = body.GetComponentInChildren<Animator>();
            if (anim == null) anim = body.AddComponent<Animator>();
            // CRITICAL (WO-174 "no walk / statue"): the per-class controllers are built from
            // HUMANOID Mixamo/iClone clips (HeroAnimatorFactory). A Humanoid clip can ONLY pose the
            // rig through an Avatar — with NO avatar the Animator freezes in its bind/T-pose. The
            // Blink base body (and the legacy FBX) carry their generated Humanoid avatar through
            // VisualFactory.Skin (it instantiates the prefab root), so the live Animator normally
            // has a valid avatar here. Self-report if it does NOT, so a run pinpoints the
            // missing-avatar case (the "sliding statue") instead of silently posing the T-pose.
            // GOLD STANDARD (mirror HeroArmorVisual.RetargetToBaseAnimator / VerifyArmorRendersNow):
            // the armor OVERLAY self-protects against a no-avatar / no-controller body, but the BASE
            // body it sits on did NOT — it Warn'd then BOUND ANYWAY onto a guaranteed-T-pose rig. A
            // Humanoid clip can ONLY pose through a valid avatar; with NO avatar OR no controller the
            // playable hero ships as a sliding statue. Treat that as a hard FAIL (not a warn-proceed):
            // self-report loudly, then trigger the legacy Resources fallback (a different body whose
            // own avatar may be valid) ONLY on the Blink path — never bind a known-T-pose rig silently.
            bool avatarOk = anim.avatar != null && anim.avatar.isValid;
            if (!avatarOk || controller == null)
            {
                FlowTrace.Fail("HeroBody",
                    $"body '{body.name}' is un-animatable after Skin (isBlink={isBlink}): " +
                    $"avatarValid={avatarOk}, controller={(controller != null ? controller.name : "<null>")} — " +
                    "a humanoid clip would hold the bind/T-pose (the playable-hero sliding-statue ship path).");

                if (isBlink && avatarOk == false)
                {
                    // The Blink base resolved but carries no valid Humanoid avatar — fall back to the
                    // legacy Resources/Heroes body, whose own generated avatar may bind cleanly. Tear
                    // down the half-built Blink body + release its handle first (no two bodies, no leak).
                    FlowTrace.Warn("HeroBody",
                        "Blink base has no valid avatar — falling back to the legacy Resources body (no T-pose ship).");
                    if (body != null) Destroy(body);
                    ReleaseBaseBodyHandle();
                    BuildLegacyResourcesBody(cls, slug, controllerSnapshot);
                    return;
                }
                // Legacy path (or Blink with a valid avatar but no controller): we have no second body
                // to fall back to. Do NOT silently bind a guaranteed-T-pose. Self-report has fired
                // above; continue only far enough to bind whatever controller exists so a later
                // HeroAnimatorSetup re-run can recover — the deferred verify below still self-reports.
                if (controller == null)
                    FlowTrace.Fail("HeroBody",
                        "No controller at Resources/Heroes/" + slug + ".controller — run " +
                        "Defenders → Animation → Setup " + slug + " Animator. Hero will not animate.");
            }

            // DEF-232 (facing) self-correct — replaces the hard-coded -90° guess above for any body
            // whose authored forward ISN'T +X. Needs the valid Humanoid avatar ensured just above, so
            // it runs here (not in the FORWARD CORRECTION block). No-op for already-aligned bodies.
            AlignBodyFacingToRoot(body, anim);
            if (controller != null)
            {
                anim.runtimeAnimatorController = controller;
            }
            // applyRootMotion=false: the Walk clip's baked root curves would fight
            // HeroLocomotion's Slerp on the parent and the hero would never appear
            // to turn. Root motion off → HeroLocomotion owns movement/rotation; the
            // clip only drives the visible mesh.
            anim.applyRootMotion = false;
            // Owner 2026-05-30: Mixamo clips play too fast on the hero. Scale global
            // animator playback to half speed. Works regardless of which controller is
            // loaded (it's a multiplier on all clip playback). Tune via HeroAnimSpeed.
            anim.speed = HeroAnimSpeed;
            // Keep animating even when the follow camera frames just past the hero
            // edge, else Unity freezes the rig and it T-poses on re-entry to view.
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.keepAnimatorStateOnDisable = true;
            // Rebind reconnects bone references after the FBX instantiate finishes,
            // else the new mesh T-poses for ~10 frames after a swap.
            anim.Rebind();

            // DEFERRED POSE-VERIFY (gold standard: mirror HeroArmorVisual.VerifyPoseThenMaybeRollback):
            // a bound avatar + controller can STILL freeze the playable hero in bind/T-pose if the
            // controller drives no clip for THIS rig. The animator hasn't evaluated yet this frame, so
            // watch the next several frames; if the rig never animates, FlowTrace.Fail so the run
            // self-reports the "playable hero T-pose" — the owner must never be the one to notice.
            if (isActiveAndEnabled && anim != null)
                StartCoroutine(VerifyHeroPoseThenReport(anim, body != null ? body.name : "<null>", isBlink));

            // EquipmentController — activates the real KayKit weapon-mesh equip on the
            // swapped hero. It lives on the hero ROOT (same object as GearLoadout): its
            // CacheRig() finds the Animator via transform.Find("HeroBody") (the body we
            // just swapped/rebound), and Awake/OnEnable resolve GearLoadout via
            // GetComponent<GearLoadout>() — BOTH only resolve from the root. Ensure the
            // GearLoadout exists FIRST (lazy-add, same idiom as line ~274 below) so the
            // controller's OnEnable reads a live loadout and equips on enable. Guard so a
            // re-run never double-adds ([DisallowMultipleComponent] would also reject it).
            if (!TryGetComponent(out GearLoadout equipLoadout)) equipLoadout = gameObject.AddComponent<GearLoadout>();
            equipLoadout.Refresh();
            if (GetComponent<EquipmentController>() == null)
                gameObject.AddComponent<EquipmentController>();
            // Seed the Knight's default SHIELD (off-hand) the first time a Knight body is built —
            // only if nothing's persisted, so it never overrides a player's later choice. The data
            // row 'knight_shield_starter' (category "shield") resolves to the shield_A visual and
            // EquipmentController attaches it to LeftHand on the OnGearChanged this fires (the
            // controller is already added above, so it's subscribed). Knight-specific, like the
            // height tweak — Tripo donor carries no weapon/shield, so we attach them as gear.
            if (cls == HeroClass.Knight && equipLoadout.EquippedOffHand == null)
                Guard.Try("HeroBody", "seed knight default shield",
                    () => equipLoadout.EquipOffHandById("knight_shield_starter"));
            // ARMOR RENDER (HeroArmorVisual): show EQUIPPED Blink armor on the BODY by swapping
            // in the full-body armored skinned-mesh and humanoid-retargeting it to THIS hero's
            // animator (hiding the base "HeroBody" while armored, restoring it on unequip). It
            // subscribes to GearLoadout.OnGearChanged and reflects the current EquippedArmor on
            // enable, so the body lights up the moment armor is equipped. Added on the same root
            // as GearLoadout/EquipmentController; [DisallowMultipleComponent] guards a re-run.
            // (The Mage/default body skips the swap above and is registered by HeroControlEnsurer.)
            if (GetComponent<HeroArmorVisual>() == null)
                gameObject.AddComponent<HeroArmorVisual>();

            if (controller != null)
            {
                bool hasSpeedParam = false;
                foreach (var p in anim.parameters)
                {
                    if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float)
                    { hasSpeedParam = true; break; }
                }
                if (!hasSpeedParam)
                {
                    Debug.LogWarning(
                        "[HeroBodySwapper] Controller '" + controller.name +
                        "' has no Speed (float) parameter — Walk transitions won't fire.");
                }
                else
                {
                    anim.SetFloat("Speed", 0f); // open in Idle, not mid-Walk
                }
            }
            // WO-376 HERO POSE INIT: drive the controller into a clean, UPRIGHT IDLE on
            // scene load (and thus during the dialogue intro that plays right after). Two
            // things were leaving the hero in a bad/hunched non-idle pose on entry:
            //   1. Animator state ENTRY is deferred — right after Rebind()+SetFloat the
            //      controller may not have evaluated into "Locomotion" yet, so the rig
            //      shows its raw resting/bind-adjacent pose for the first frames (the
            //      "hunched/drooping" look the owner sees on load and in dialogue).
            //   2. Stray Any-State combat triggers (Cast/Attack/Victory/WindUp/Hit) left
            //      latched from a prior scene/playthrough yank the hero straight into a
            //      combat/cast pose on spawn.
            // Force the issue: clear those triggers, pin Speed=0, and PLAY the Locomotion
            // (Idle) state at normalized time 0 so the hero is deterministically standing
            // in the relaxed idle from frame one. Guarded by name so a controller without
            // these params/state is a safe no-op. Does NOT touch the Walk/Cast/combat
            // transitions — those still fire from gameplay as before.
            DriveIdlePose(anim);
            // ALWAYS re-cache the _animator field on BOTH HeroLocomotion (drives
            // Speed → Walk) and HeroAbilities (fires Cast). This was previously
            // gated on the (always-null) snapshot AND only touched HeroLocomotion,
            // which is the core reason the hero never animated. Their private
            // fields still point at the destroyed placeholder animator; reflection
            // write because the fields are private.
            // DEF-221: the BODY slug and the ABILITY slug can differ. The Cleric loads
            // its own body/controller (slug "Cleric") but fires the Mage loadout until a
            // dedicated cleric/healer kit lands — so route its abilities to "Mage".
            string abilitySlug = (cls == HeroClass.Cleric) ? "Mage" : slug;
            int recached = 0;
            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string n = mb.GetType().Name;
                if (n != "HeroLocomotion" && n != "HeroAbilities") continue;
                // §12: the reflection writes were un-Guarded (Debug.Log only). A throw here
                // (renamed field / signature drift) would have left HeroLocomotion/HeroAbilities
                // pointing at the DESTROYED placeholder animator — the hero would never animate,
                // silently. Guard each so one bad component logs + is skipped, never aborts the wire.
                MonoBehaviour mbLocal = mb;
                Guard.Try("HeroBody", $"recache _animator on {n}", () =>
                {
                    var f = mbLocal.GetType().GetField("_animator",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) { f.SetValue(mbLocal, anim); recached++; }
                });
                // WO-36: bind the hero class on HeroAbilities so a Knight/Ranger
                // casts its OWN abilities.json loadout. The field defaulted to
                // "mage" and was never reassigned, so every class fired the Mage
                // kit. slug is "Knight"/"Ranger"/"Mage"; SetHeroClass lower-cases it.
                if (n == "HeroAbilities")
                    Guard.Try("HeroBody", "HeroAbilities.SetHeroClass", () =>
                        mbLocal.GetType().GetMethod("SetHeroClass",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                          ?.Invoke(mbLocal, new object[] { abilitySlug }));
            }
            // §12 self-report: recached==0 means NEITHER HeroLocomotion nor HeroAbilities got the
            // live animator — their fields still point at the destroyed placeholder, so the hero
            // cannot animate. That is a hard FAIL the run must surface, not a quiet Debug.Log.
            if (recached == 0)
                FlowTrace.Fail("HeroBody",
                    "Animator re-cache wrote 0 components — neither HeroLocomotion nor HeroAbilities " +
                    "received the live animator (renamed _animator field?); the hero will not animate.");
            FlowTrace.Step("HeroBody",
                "Animator wired: controller=" +
                $"{(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL")}, " +
                $"avatar={(anim.avatar != null ? anim.avatar.name : "none")}, " +
                $"clips={anim.runtimeAnimatorController?.animationClips?.Length ?? 0}, " +
                $"re-cached {recached} component(s) (HeroLocomotion/HeroAbilities).");
            // Owner 2026-05-20: Tripo's Send To Unity feature extracted the
            // Knight basecolor PNG. Copied into Resources/Textures/Knight.png
            // so the runtime can apply the real texture rather than the
            // stale class tint. Same hook works for Ranger if/when its
            // Send To Unity export lands.
            // WO (2026-06-10) KNIGHT SPECKLE — PROPER FIX:
            //   The Knight now routes through the SAME normal texture path as every other class.
            //   The speckle was a wrong-UV atlas (medievalknight3dmodel_basecolor = a different
            //   model); ApplyExtractedTexture now binds the mesh's OWN UV-matched bake
            //   (remesh_12_combined_Bake_Diffuse), so each UV island samples its matching region
            //   and the speckle is gone. The flat-steel stopgap is no longer the routed Knight
            //   path — it is kept ONLY as a fallback for the (unexpected) case where the correct
            //   basecolor fails to load, so the Knight degrades to solid steel rather than the
            //   cyan/grey/null-slot fallback. ApplyExtractedTexture returns false when no diffuse
            //   was bound (load failed / no texPath).
            // TEXTURE / TINT (LEGACY ONLY): the Tripo/CC5 bodies need a runtime material pass
            // (ApplyExtractedTexture → flat-steel/class-tint fallback). Blink bodies ship clean URP
            // materials + their own basecolor, so this whole block is BYPASSED — the Blink hero is
            // textured by its prefab. The methods stay in the file for the legacy path.
            if (!isBlink)
            {
                bool textured = ApplyExtractedTexture(body, cls);
                if (cls == HeroClass.Knight)
                {
                    // Knight: real human_tank basecolor binds via ApplyExtractedTexture; flat steel
                    // is the load-failed fallback only (degrades to solid armour grey, not cyan).
                    if (!textured)
                        ApplyFlatSteelStopgap(body);
                }
                else
                {
                    // Safety net: untextured legacy body gets a class tint so it isn't solid white.
                    ApplyClassTint(body, cls);
                }
            }
            // DEF: the Ranger/Archer fires arrows via the projectile system but held
            // NOTHING — give him a visible bow. Bow goes in the LEFT (off/bow) hand
            // since HeroAimIK draws with the RightHand IK goal. Cosmetic only; the
            // component null-guards a missing bone and never blocks the hero.
            // Attachment: uses LeftHand bone (via GetBoneTransform); sizing via
            // HeroBowAttachment.BowHeldLength + NormalizeInto (reviewed/fixed to 0.92m).
            if (cls == HeroClass.Ranger)
                HeroBowAttachment.AttachTo(gameObject, body);

            // Default gear + visuals (Chunk default gear + shop): ensure GearLoadout so
            // level-1 catalog entries (knight sword+plate, ranger bow+leather, mage staff+robes,
            // cleric mace+light) are applied at spawn. Then attach visuals via the applier
            // (sword/staff/mace on RightHand; bow skipped in applier -- ranger uses the
            // dedicated HeroBowAttachment on LeftHand to avoid duplicate back+held).
            // Re-apply later from shop equip flows. Applier now uses corrected scales/pos/euler
            // proportional to ~1.8m hero (see GearVisualApplier for exact values).
            if (!TryGetComponent(out GearLoadout loadout)) loadout = gameObject.AddComponent<GearLoadout>();
            loadout.Refresh();

            // CLASS DEFAULT ARMOR (owner pick 2026-06-18): the hero spawns already WEARING the
            // picked Blink set so HeroArmorVisual swaps it onto the playable body at spawn — Knight=
            // Centurion, Ranger=BeastHunter, Mage=Dragonic. It is just the LEVEL-1 default: seeded
            // only when the player has NOT already chosen armor for this class (the per-class equip
            // PlayerPrefs key is absent), so a later manual equip is never overridden on respawn.
            // EquipArmorById persists the choice + fires OnGearChanged (HeroArmorVisual reacts), and
            // intentionally bypasses the weight-class gate (Dragonic is heavy but the Mage default) —
            // this is the owner's explicit cosmetic default, not an auto-best pick.
            SeedClassDefaultArmor(loadout, cls);

            GearVisualApplier.Apply(body.transform, loadout);

            // Ensure the guarded ActorAnimator is on the hero root (finds the Animator on the
            // new "HeroBody" child). This wires the full shared animation pipeline (same as
            // DTT/PatriciaLight): SetLocomotion (speed for idle/move), SetCombatStance (battle
            // ready idle stance vs casual), PlayAttack/PlayCast (class-specific), PlayHit,
            // Die. HeroLocomotion/Abilities already resolve and drive it; re-attach here post-swap
            // so Village + World scenes get correct hero anims (Idle/BattleReady, Movement,
            // Attack/Cast per class, Hit, Death) without falling back to T-pose or legacy only.
            if (!TryGetComponent(out ActorAnimator actor)) actor = gameObject.AddComponent<ActorAnimator>();
            // WO-376: spawn RELAXED, not in a combat/battle-ready stance. The hero loads
            // into a town/dialogue context, not a fight — forcing the combat stance here
            // was the wrong default (the WO is explicit: "Don't force SetPose(Combat) by
            // default"). InCombat is flipped to true by the wave/battle flow when a real
            // threat starts (WaveManager) and back to false on victory; at scene load and
            // during the dialogue intro the hero holds the clean upright idle. Re-assert
            // the idle pose through the guarded ActorAnimator too, in case the body-swap
            // timing means the actor only just resolved its Animator.
            actor.SetCombatStance(false); // relaxed idle on load (waves flip this true)
            actor.SetLocomotion(0f);      // ensure the locomotion blend opens at Idle
            DriveIdlePose(actor.Animator);
            _heroActor = actor;

            // WO-376: HOLD the idle during the dialogue intro. The Yarn DialogueRunner may
            // already be hosted (intro/FTUE) or may be hosted lazily by DialogueService on the
            // first Play; subscribe now if present, else retry next frame so a runner that
            // spins up just after the swap is still hooked. On dialogue start (and complete) we
            // re-pin the relaxed idle so the hero never holds a stale combat/hunched pose while
            // a story line is on screen.
            HookDialogueIdle();

            FlowTrace.Step("HeroBody",
                $"Hero body wired complete: class={cls} slug={slug} isBlink={isBlink} body='{body.name}'.");
            Debug.Log($"[HeroBodySwapper] Hero body ready ({(isBlink ? "Blink base" : "legacy " + slug + ".fbx")}).");
        }

        /// <summary>
        /// Seed the per-class DEFAULT Blink armor so the hero spawns wearing it (Knight=Centurion,
        /// Ranger=BeastHunter, Mage=Dragonic, Cleric=Centurion). Only applied when the player has NOT
        /// already chosen armor for this class (the GearLoadout per-class equip PlayerPrefs key is
        /// absent) — so a manual equip from the equip UI is never overridden on respawn. Delegates to
        /// GearLoadout.EquipArmorById (persists + fires OnGearChanged for HeroArmorVisual). It is the
        /// LEVEL-1 default only; the player can change armor at any time.
        /// </summary>
        private static void SeedClassDefaultArmor(GearLoadout loadout, HeroClass cls)
        {
            if (loadout == null) return;

            string armorId = DefaultArmorIdFor(cls);
            if (string.IsNullOrEmpty(armorId)) return;

            // Mirror GearLoadout's per-class persistence key (stable contract): if the player has
            // already interacted with this class's armor slot (any value, incl. the none-sentinel),
            // leave their choice alone. Absent key => first spawn => seed the owner's default.
            // Use the SAME job string GearLoadout persists under (HeroAbilities.HeroClass — which
            // routes Cleric→"mage"); fall back to the enum name when no HeroAbilities is present.
            var abilities = loadout.GetComponent<HeroAbilities>();
            string jobKey = abilities != null && !string.IsNullOrEmpty(abilities.HeroClass)
                ? abilities.HeroClass.ToLowerInvariant()
                : ClassKey(cls);
            string key = "dotr-equip-armor-" + jobKey;
            if (PlayerPrefs.HasKey(key))
            {
                FlowTrace.Step("HeroBody",
                    $"SeedClassDefaultArmor: '{key}' already set — keeping the player's armor choice (no reseed).");
                return;
            }

            FlowTrace.Step("HeroBody", $"SeedClassDefaultArmor: equipping default '{armorId}' for {cls}.");
            Guard.Try("HeroBody", $"equip default armor '{armorId}'", () => loadout.EquipArmorById(armorId));
        }

        /// <summary>The owner-picked DEFAULT Blink armor catalog id per class (cosmetic level-1 look).
        /// Ids match Assets/Resources/Data/Canonical/armor.json (Blink rows, loadVia="addressable").</summary>
        private static string DefaultArmorIdFor(HeroClass cls) => cls switch
        {
            HeroClass.Knight => "blink_armor_centurion",    // gear/armor/Centurion_Male
            HeroClass.Ranger => "blink_armor_beasthunter",  // gear/armor/BeastHunter_Male
            HeroClass.Mage   => "blink_armor_dragonic",     // gear/armor/Dragonic_Male
            HeroClass.Cleric => "blink_armor_centurion",    // heavy default (shares the Knight plate)
            _                => null,
        };

        // Lower-cased class key matching GearLoadout.PrefJobKey() (HeroAbilities.HeroClass lower-cased).
        private static string ClassKey(HeroClass cls) => cls.ToString().ToLowerInvariant();

        /// <summary>
        /// WO-376: subscribe to the shared Yarn runner's start/complete events so the hero
        /// re-asserts a clean upright idle whenever dialogue is on screen. Idempotent — never
        /// double-subscribes. If no runner is hosted yet (DialogueService hosts lazily), retry
        /// on the next frame so a runner created just after the swap is still wired.
        /// </summary>
        // PERF (coroutine fork-bomb fix): single-flight guard — same bug as HeroLocomotion's
        // gate hook. The retry loop re-called HookDialogueIdle(), which re-started a retry
        // coroutine each frame when no runner existed. Now one retry runs and polls directly.
        private bool _retryingHook;

        private void HookDialogueIdle()
        {
            if (_dialogueRunner != null) return; // already hooked

            var runner = Object.FindObjectOfType<Yarn.Unity.DialogueRunner>();
            if (runner == null)
            {
                if (!_retryingHook) { _retryingHook = true; StartCoroutine(RetryHookDialogueIdle()); }
                return;
            }

            _dialogueRunner = runner;
            if (runner.onDialogueStart != null)    runner.onDialogueStart.AddListener(OnDialogueIdle);
            if (runner.onDialogueComplete != null) runner.onDialogueComplete.AddListener(OnDialogueIdle);
        }

        private System.Collections.IEnumerator RetryHookDialogueIdle()
        {
            // Poll directly — do NOT call HookDialogueIdle() here (that re-spawns coroutines).
            for (int i = 0; i < 5 && _dialogueRunner == null && this != null; i++)
            {
                yield return null;
                var runner = Object.FindObjectOfType<Yarn.Unity.DialogueRunner>();
                if (runner != null)
                {
                    _dialogueRunner = runner;
                    if (runner.onDialogueStart != null)    runner.onDialogueStart.AddListener(OnDialogueIdle);
                    if (runner.onDialogueComplete != null) runner.onDialogueComplete.AddListener(OnDialogueIdle);
                    break;
                }
            }
            _retryingHook = false;
        }

        /// <summary>WO-376: re-pin the relaxed idle pose when a dialogue starts/completes.</summary>
        private void OnDialogueIdle()
        {
            if (_heroActor == null) return;
            _heroActor.SetCombatStance(false);
            _heroActor.SetLocomotion(0f);
            DriveIdlePose(_heroActor.Animator);
        }

        private void OnDestroy()
        {
            if (_dialogueRunner != null)
            {
                if (_dialogueRunner.onDialogueStart != null)    _dialogueRunner.onDialogueStart.RemoveListener(OnDialogueIdle);
                if (_dialogueRunner.onDialogueComplete != null) _dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueIdle);
            }
            // Release the Blink base-body Addressables handle (ONE owner) so the prefab never leaks.
            ReleaseBaseBodyHandle();
        }

        /// <summary>
        /// WO-376: deterministically pose the freshly-swapped hero in a clean, upright IDLE
        /// for scene load + the dialogue intro that follows. Clears any latched Any-State
        /// combat triggers (so a stale Cast/Attack/Victory from a prior scene can't pull the
        /// hero into a combat pose on spawn), pins the locomotion blend to Speed=0 (Idle clip),
        /// and PLAYs the "Locomotion" state at normalized time 0 so the rig is standing in idle
        /// from the first frame instead of showing its deferred-entry resting/hunched pose.
        /// Every access is name/param guarded — a controller missing a param or the state is a
        /// safe no-op. Leaves the Walk/Cast/combat transitions intact (they fire from gameplay).
        /// </summary>
        /// <summary>
        /// DEFERRED pose-verify for the PLAYABLE BASE BODY (gold standard: mirrors
        /// HeroArmorVisual.VerifyPoseThenMaybeRollback). Over the next several frames confirm the
        /// hero rig is actually PLAYING a clip on layer 0 (GetCurrentAnimatorClipInfoCount(0) > 0),
        /// i.e. NOT frozen in the bind/T-pose. The armor overlay self-protects + rolls back; the base
        /// body has no second overlay to swap to, so here we cannot roll back — instead we FAIL LOUD
        /// (FlowTrace.Fail → break-log) so the run self-reports the "playable hero T-pose" and the
        /// owner is never the one to notice. Robust against a transient first-frame 0 (only reports
        /// when NOTHING ever drives the rig within the window).
        /// </summary>
        private System.Collections.IEnumerator VerifyHeroPoseThenReport(Animator anim, string bodyName, bool isBlink)
        {
            const int MaxPoseFrames = 6;
            bool everPlayed = false;
            int lastCount = -1;
            for (int i = 0; i < MaxPoseFrames; i++)
            {
                yield return null;
                if (this == null || anim == null) yield break;
                if (anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
                {
                    lastCount = anim.GetCurrentAnimatorClipInfoCount(0);
                    if (lastCount > 0) { everPlayed = true; break; }
                }
            }

            FlowTrace.Step("HeroBody",
                $"VerifyHeroPose body='{bodyName}' isBlink={isBlink}: everPlayed={everPlayed} " +
                $"lastClipCount={lastCount} (>=1 means an animation drives the rig; otherwise frozen T-pose).");

            if (!everPlayed)
                FlowTrace.Fail("HeroBody",
                    $"PLAYABLE HERO T-POSE: body '{bodyName}' (isBlink={isBlink}) never animated within " +
                    $"{MaxPoseFrames} frames (lastClipCount={lastCount}) — the controller drives no clip for " +
                    "this rig (missing/invalid avatar, no Locomotion state, or wrong controller). The hero " +
                    "ships as a sliding statue. Re-run Defenders → Animation → Setup <Class> Animator / verify the rig's avatar.");
        }

        private static void DriveIdlePose(Animator anim)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return;

            // Snapshot which trigger/float params this controller actually declares so we
            // never poke a missing parameter (the project's well-documented param-spam pitfall).
            bool hasSpeed = false, hasCast = false, hasAttack = false,
                 hasVictory = false, hasWindUp = false, hasHit = false;
            foreach (var p in anim.parameters)
            {
                switch (p.name)
                {
                    case "Speed":   if (p.type == AnimatorControllerParameterType.Float)   hasSpeed   = true; break;
                    case "Cast":    if (p.type == AnimatorControllerParameterType.Trigger) hasCast    = true; break;
                    case "Attack":  if (p.type == AnimatorControllerParameterType.Trigger) hasAttack  = true; break;
                    case "Victory": if (p.type == AnimatorControllerParameterType.Trigger) hasVictory = true; break;
                    case "WindUp":  if (p.type == AnimatorControllerParameterType.Trigger) hasWindUp  = true; break;
                    case "Hit":     if (p.type == AnimatorControllerParameterType.Trigger) hasHit     = true; break;
                }
            }

            // Clear stray Any-State combat triggers so none of them yanks the hero out of
            // Locomotion the moment the controller starts evaluating.
            if (hasCast)    anim.ResetTrigger("Cast");
            if (hasAttack)  anim.ResetTrigger("Attack");
            if (hasVictory) anim.ResetTrigger("Victory");
            if (hasWindUp)  anim.ResetTrigger("WindUp");
            if (hasHit)     anim.ResetTrigger("Hit");

            // Pin the locomotion blend to Idle and force the state to be entered NOW, so the
            // rig stands in the idle pose from frame zero instead of its deferred-entry pose.
            if (hasSpeed) anim.SetFloat("Speed", 0f);
            int locoHash = Animator.StringToHash("Locomotion");
            if (anim.HasState(0, locoHash))
                anim.Play(locoHash, 0, 0f);
            anim.Update(0f); // evaluate immediately so the idle pose shows this frame
        }

        private static HeroClass ResolveHeroClass()
        {
            // V1 LOCK (2026-06-23): ff.knightonly forces the hero to Knight at the BUILD chokepoint,
            // not only at selection. The PetSelect-bypass flow auto-routes to the castle WITHOUT
            // calling GameStateService.ChooseHero (the only other KnightOnly enforcer), so a None/
            // persisted state otherwise fell through to the Mage default below and the hero rendered
            // as a Mage ("still not a knight", data-proven 2026-06-23). Forcing here makes the single-
            // Knight north star hold on every path. Flip OFF (ff.knightonly=0) to restore free class.
            if (DeNelle.Core.FeatureFlags.KnightOnly) return HeroClass.Knight;
            var svc = GameStateService.Instance;
            if (svc == null) return HeroClass.Mage;
            var opt = svc.State?.HeroClass.ToNullable();
            return opt ?? HeroClass.Mage;
        }

        /// <summary>
        /// Tripo FBXs come in with Phong / Lambert / Blinn materials that URP
        /// can't render — the mesh shows as a transparent magenta ghost. Walk
        /// the renderers, pull each material's diffuse / normal / emission
        /// (whichever property names happen to be present on the source), and
        /// rebuild under URP/Lit. Falls through to URP/Simple Lit (cheaper on
        /// mobile) then Standard as a final safety net so the hero is never
        /// rendered with a null shader.
        /// </summary>
        private static void RetargetMaterialsToUrp(GameObject body)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                            ?? Shader.Find("Standard");
            if (litShader == null)
            {
                Debug.LogWarning("[HeroBodySwapper] No URP/Lit or Standard shader available — material fix skipped.");
                return;
            }

            int converted = 0, skipped = 0;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    // Already a URP shader? Preserve — upstream art may have
                    // intentionally placed URP/Unlit on emissive accents etc.
                    string srcShaderName = src.shader != null ? src.shader.name : "";
                    if (srcShaderName.StartsWith("Universal Render Pipeline/", System.StringComparison.Ordinal))
                    { skipped++; continue; }

                    // Diffuse / base texture — try every common property name.
                    Texture baseTex = null;
                    if (src.HasProperty("_MainTex"))      baseTex = src.GetTexture("_MainTex");
                    if (baseTex == null && src.HasProperty("_BaseMap"))       baseTex = src.GetTexture("_BaseMap");
                    if (baseTex == null && src.HasProperty("_BaseColorMap")) baseTex = src.GetTexture("_BaseColorMap");

                    // Base colour — try URP first, then Built-in.
                    Color baseColor = Color.white;
                    if (src.HasProperty("_BaseColor"))      baseColor = src.GetColor("_BaseColor");
                    else if (src.HasProperty("_Color"))     baseColor = src.GetColor("_Color");

                    // Normal + emission, if the source authored them.
                    Texture normalTex = null;
                    if (src.HasProperty("_BumpMap"))                   normalTex = src.GetTexture("_BumpMap");
                    if (normalTex == null && src.HasProperty("_NormalMap")) normalTex = src.GetTexture("_NormalMap");
                    Texture emissionTex = null;
                    Color   emissionColor = Color.black;
                    if (src.HasProperty("_EmissionMap"))   emissionTex   = src.GetTexture("_EmissionMap");
                    if (src.HasProperty("_EmissionColor")) emissionColor = src.GetColor("_EmissionColor");

                    // Tiling / offset for whichever map slot held the diffuse.
                    Vector2 tiling = Vector2.one, offset = Vector2.zero;
                    if (src.HasProperty("_MainTex"))
                    { tiling = src.GetTextureScale("_MainTex"); offset = src.GetTextureOffset("_MainTex"); }
                    else if (src.HasProperty("_BaseMap"))
                    { tiling = src.GetTextureScale("_BaseMap"); offset = src.GetTextureOffset("_BaseMap"); }

                    var newMat = new Material(litShader);
                    newMat.name = (src.name ?? "Tripo") + " (URP)";
                    if (newMat.HasProperty("_BaseColor")) newMat.SetColor("_BaseColor", baseColor);
                    if (newMat.HasProperty("_Color"))     newMat.SetColor("_Color",     baseColor);
                    if (baseTex != null)
                    {
                        if (newMat.HasProperty("_BaseMap"))
                        { newMat.SetTexture("_BaseMap", baseTex); newMat.SetTextureScale("_BaseMap", tiling); newMat.SetTextureOffset("_BaseMap", offset); }
                        if (newMat.HasProperty("_MainTex"))
                        { newMat.SetTexture("_MainTex", baseTex); newMat.SetTextureScale("_MainTex", tiling); newMat.SetTextureOffset("_MainTex", offset); }
                    }
                    if (normalTex != null && newMat.HasProperty("_BumpMap"))
                    {
                        newMat.SetTexture("_BumpMap", normalTex);
                        newMat.EnableKeyword("_NORMALMAP");
                    }
                    if (emissionTex != null || emissionColor.maxColorComponent > 0.001f)
                    {
                        if (newMat.HasProperty("_EmissionColor")) newMat.SetColor("_EmissionColor", emissionColor);
                        if (emissionTex != null && newMat.HasProperty("_EmissionMap"))
                            newMat.SetTexture("_EmissionMap", emissionTex);
                        newMat.EnableKeyword("_EMISSION");
                        newMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                    // DEF-6: Tripo FBXs frequently export with inverted face
                    // normals. URP's default back-face cull (Cull=2) renders
                    // the inside surfaces, producing the "dark purple spiky
                    // creature" seen in screenshots 1-3. Disabling culling
                    // (Cull=0 = Off) renders both sides so the mesh is visible
                    // regardless of which way its normals point.
                    if (newMat.HasProperty("_Cull"))
                        newMat.SetFloat("_Cull", 0f); // 0 = Off (double-sided)

                    if (newMat.HasProperty("_Smoothness")) newMat.SetFloat("_Smoothness", 0.15f);
                    if (newMat.HasProperty("_Metallic"))   newMat.SetFloat("_Metallic",   0f);
                    mats[i] = newMat;
                    converted++;
                }
                r.sharedMaterials = mats;
            }
            Debug.Log("[HeroBodySwapper] RetargetMaterialsToUrp: converted=" +
                      converted + ", skipped (already URP)=" + skipped);
        }

        private static string SlugFor(HeroClass cls) => cls switch
        {
            HeroClass.Knight => "Knight",
            HeroClass.Ranger => "Ranger",
            // Mage now has its own paid-for Tripo FBX at Resources/Heroes/Mage.fbx
            // (24 MB) per docs/port-notes/tripo-asset-pipeline.md. Swap in like
            // the other two; HeroAnimatorSetup writes Mage.controller alongside.
            HeroClass.Mage   => "Mage",
            // DEF-221: the Cleric now has a DEDICATED body (Resources/Heroes/Cleric.fbx,
            // from People/Human/human_Cleric) + Cleric.controller (a copy of the Mage
            // caster kit). It still fires the Mage ability LOADOUT — abilitySlug below
            // routes Cleric → "Mage" for SetHeroClass — until a cleric/healer kit exists.
            HeroClass.Cleric => "Cleric",
            _ => null,
        };

        // (Removed NeedsForwardFlip — the per-class -Z/+Z guess was superseded by
        // the single consistent +X→+Z ForwardYaw correction at swap time, WO-174.)

        /// <summary>
        /// Loads a per-class basecolor PNG out of Resources/Textures/
        /// (extracted via Tripo's Send-To-Unity flow on the owner's side)
        /// and assigns it to every URP material on the body. When this
        /// returns true a real diffuse was bound and the subsequent
        /// <see cref="ApplyClassTint"/> is a no-op for that material (it
        /// preserves textures). Returns FALSE when no diffuse was bound
        /// (no texPath for the class, or the texture failed to load) so the
        /// caller can fall back to a flat material instead of an untextured slot.
        /// </summary>
        private static bool ApplyExtractedTexture(GameObject body, HeroClass cls)
        {
            // DEF-267: separate the two body pipelines.
            //  • CC5 bodies (Ranger / Cleric) = ONE combined mesh + ONE baked PBR atlas, imported
            //    with materialImportMode=None → renderer slots come in null/default. The right fix
            //    is to build ONE URP/Lit from the single atlas and REPLACE every slot.
            //  • Legacy Tripo bodies (Mage / Knight) = multi-material exports whose slots DO import
            //    (materialImportMode=2) but with NO base-color texture bound, so they render GREY in
            //    the build (the DEF-267 symptom: "URP-but-untextured"). Their Send-To-Unity diffuse
            //    PNG lives in Resources/Heroes/Textures/*. PAINT that diffuse onto the EXISTING slots
            //    in place — replacing them all with one shared material would collapse the body's
            //    separate UV islands onto a single material and mis-sample. Each slot already maps
            //    its own region of the single Tripo atlas, so painting the same atlas onto each is
            //    correct and preserves the per-slot setup.
            // WO-286: ALL four heroes are now re-rigged single-atlas meshes (AccuRIG,
            // 2026-06-06). Each FBX shipped EXTRA texture sets (Knight a stray
            // remesh_*_Bake, Ranger a Motion_Dummy_Female PBR set) plus metallic/normal
            // maps the import bound into the slots — that's the blotchy purple-camo Mage +
            // patchy-red Knight (wrong albedo + a normal/metallic read as colour). The
            // "paint _BaseMap onto existing slots" path LEFT those stray maps in place, so
            // it never cleaned up. Build ONE fresh URP/Lit from JUST the real basecolor and
            // REPLACE every slot (no metallic/normal assigned → no colour-space artefact).
            // Each slot's own UVs still sample the single atlas correctly. So: combined
            // path for ALL classes now, not only the old CC5 Ranger/Cleric.
            bool isCc5Combined = true;
            string texPath = cls switch
            {
                // DEF-267: the Mage is a LEGACY Tripo body. The build log showed Mage rendering
                // GREY because (a) its FBX material remap points at an EXTERNAL material that
                // doesn't resolve in the build → no _BaseMap, and (b) NO texPath existed here so
                // ApplyExtractedTexture returned early and never bound a diffuse. Bind the Mage's
                // basecolor atlas from the NORMAL Resources/Heroes/Textures/ folder. We use a
                // normal folder (not the sibling *.fbm/ extraction) deliberately: the working
                // Ranger/Cleric pipeline copies its atlas into a plain Heroes/<x>_tex/ folder
                // because textures inside an FBX's *.fbm import-artifact folder are NOT reliably
                // Resources.Load-able in a player build. Heroes/Textures/* is a guaranteed-loadable
                // plain folder. This is the same wizard atlas, just from the reliable location.
                // WO-286: the re-rigged Mage mesh's own basecolor is the "witch" atlas
                // (it shipped base-color only); the old "wizard" path bound a different
                // mesh's texture → wrong colors. Point at the mesh's own basecolor.
                // FIX (2026-06-06): the PROJECT Mage is a legacy Tripo mesh — its UV-matched
                // atlas is its OWN embedded diffuse (Mage.fbm/tripo_mat_9b343081, copied into
                // Textures/), NOT the actors/ dwarf basecolor (a different mesh). CC5 heroes
                // (Ranger/Cleric) use the actors originals; Tripo heroes use their own.
                HeroClass.Mage => "Heroes/Textures/tripo_mat_9b343081_Pbr_Diffuse",
                // DEF-267: the Knight is also a LEGACY Tripo body whose FBX material remap doesn't
                // resolve → grey. WO-35 had returned null (to dodge a blood-splatter look the owner
                // disliked), but that left the Knight GREY in the build, which is worse. Bind the
                // clean medieval-knight atlas from the plain (reliably loadable) Heroes/Textures/
                // folder — textured beats grey for go-live. ApplyClassTint's steel fallback now
                // only fires on any slot this atlas genuinely doesn't cover.
                // WO-286: the re-rigged Knight mesh ships TWO sets — its real
                // knight_basecolor and a stray remesh_12_combined_Bake raw bake. Bind the
                // knight basecolor (the old medievalknight path was a different mesh).
                // PARKED (2026-06-06): the Knight FBX is a broken combo — its mesh material
                // is 'tripo_mat_18e58c3e', which has NO matching atlas in-project (tripo_1afe2e5e,
                // remesh_12, knight_basecolor, HumanTank all read wrong). This needs a CLEAN
                // Knight model re-import, not a texture pick.
                // WO (2026-06-10) KNIGHT SPECKLE — PROPER FIX:
                //   The speckle was a WRONG-UV atlas: 'medievalknight3dmodel_basecolor' is a
                //   DIFFERENT model's texture, so each UV island of THIS mesh sampled an unrelated
                //   region → blotchy/patchy/"speckled". The Knight.fbx is a Tripo model that was
                //   REMESHED (remesh_12) and baked; its CORRECT, UV-matched basecolor is the bake
                //   that ships in the FBX's sibling Knight.fbm/ folder: remesh_12_combined_Bake_Diffuse.
                //   That PNG is already copied into the reliably-loadable plain Heroes/Textures/
                //   folder (textures inside an *.fbm/ import-artifact folder don't Resources.Load
                //   reliably in a player build — Textures/ is guaranteed-loadable, confirmed
                //   textureType=Default, sRGB, valid Texture2D). Binding the mesh's OWN bake means
                //   every UV island samples its matching region → no speckle. If this load fails,
                //   ApplyExtractedTexture warns + returns and the Start() path falls back to the
                //   flat-steel stopgap, so the Knight is never left speckled.
                // WO (2026-06-10) KNIGHT BODY SWAP → human_tank:
                //   The Knight body is now the clean, textured human_tank export (copied over
                //   Resources/Heroes/Knight.fbx, re-imported Humanoid). Its UV-matched atlas is
                //   HumanTank_basecolor, already copied into the reliably-loadable plain
                //   Heroes/Textures/ folder (textureType=Default, sRGB, valid Texture2D). Binding
                //   the mesh's OWN basecolor means every UV island samples its matching region —
                //   a real textured Knight, not the flat-steel stopgap. If this load fails,
                //   ApplyExtractedTexture returns false and Start() falls back to flat steel.
                // WO-481 Slice 4 (2026-06-23) ARMORED KNIGHT PROMOTION: the playable Knight is now
                //   the ARMORED Tripo model (armor baked into the mesh — the pivot's static-armor body),
                //   promoted over Resources/Heroes/Knight.fbx. Its UV-matched basecolor is the Tripo
                //   PBR export medieval_knight_3d_model_basecolor, copied (distinct name) into the
                //   reliably-loadable plain Heroes/Textures/ folder as KnightArmored_basecolor. Binding
                //   the mesh's OWN basecolor means every UV island samples its matching region — a real
                //   textured armored Knight, not the flat-steel stopgap. If this load fails,
                //   ApplyExtractedTexture returns false and Start() falls back to flat steel.
                HeroClass.Knight => "Heroes/Textures/KnightArmored_basecolor",
                // DEF-229 (2026-06-03): the Ranger body is now the CC5/CC_Base adult
                // archer (InstaLOD-remeshed: ONE combined mesh + ONE baked PBR atlas),
                // imported Humanoid by PeopleCharacterImporter.ImportRangerCC5 into
                // Resources/Heroes/Ranger.fbx with its baked diffuse copied to
                // Resources/Heroes/Ranger_tex/. The combined bake is a single atlas, so
                // painting that one diffuse onto every body slot (skin/body/tongue) is
                // correct — each slot samples its UV region. Repointed off the retired
                // Tripo "archer v2" basecolor (Textures/Ranger) to the CC5 bake so the
                // selected archer reads as the adult ranger, not the old spiky youth.
                // FIX (2026-06-06): bind the Ranger's ORIGINAL basecolor from the source actor's
                // .fbm (ranger.fbm/ranger_basecolor) copied into Textures/ — the model's own
                // shipped atlas. (Archer_basecolor read dark; it was a different/old archer bake.)
                HeroClass.Ranger => "Heroes/Textures/ranger_basecolor",
                // DEF-232/229 (2026-06-03): the Cleric (Healer/Elara body) is now the
                // owner's fresh CC5/CC_Base adult cleric, imported Humanoid by
                // PeopleCharacterImporter.ImportClericCC5 with its baked basecolor copied
                // to Resources/Heroes/Cleric_tex/. Bind that diffuse so the Cleric reads
                // textured (was falling through to a flat class tint before).
                // WO-286: the re-rigged Cleric is the only clean set — its own basecolor
                // is Cleric_basecolor (the old HumanCleric_tex path was the prior CC5 body).
                HeroClass.Cleric => "Heroes/Textures/Cleric_basecolor",
                _ => null,
            };
            if (string.IsNullOrEmpty(texPath)) return false;
            var tex = Resources.Load<Texture2D>(texPath);
            if (tex == null)
            {
                Debug.LogWarning("[HeroBodySwapper] DEF-267 baked/basecolor diffuse not found at Resources/" +
                                 texPath + " for " + cls + " — body will fall back to a class tint.");
                return false;
            }

            // For CC5 combined bodies: build ONE URP/Lit from the single baked atlas and assign it
            // to every slot (slots came in null/default). Runtime-built URP/Lit renders in WebGL
            // (RetargetMaterialsToUrp uses the same path).
            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Sprites/Default");
            Material baked = (isCc5Combined && lit != null) ? new Material(lit) { name = cls + " (CC5 baked)" } : null;
            if (baked != null)
            {
                if (baked.HasProperty("_BaseMap"))   baked.SetTexture("_BaseMap", tex);
                if (baked.HasProperty("_MainTex"))   baked.SetTexture("_MainTex", tex);
                if (baked.HasProperty("_BaseColor")) baked.SetColor("_BaseColor", Color.white);
                if (baked.HasProperty("_Color"))     baked.SetColor("_Color", Color.white);

                // Clear stray normal/metallic/occlusion maps the FBX import bound into the shader —
                // a normal or metallic map sampled in the wrong colour space reads as "speckled"/patchy
                // colour (the patchy Knight, blotchy Mage). Drive colour from the basecolor atlas ONLY.
                if (baked.HasProperty("_BumpMap"))          baked.SetTexture("_BumpMap", null);
                if (baked.HasProperty("_NormalMap"))        baked.SetTexture("_NormalMap", null);
                if (baked.HasProperty("_MetallicGlossMap")) baked.SetTexture("_MetallicGlossMap", null);
                if (baked.HasProperty("_SpecGlossMap"))     baked.SetTexture("_SpecGlossMap", null);
                if (baked.HasProperty("_OcclusionMap"))     baked.SetTexture("_OcclusionMap", null);
                baked.DisableKeyword("_NORMALMAP");
                baked.DisableKeyword("_METALLICSPECGLOSSMAP");
                baked.DisableKeyword("_OCCLUSIONMAP");

                // Knight armour sheen — keep metallic LOW so the basecolor atlas drives the
                // visible colour. ROOT CAUSE of the "colorless Knight" in the WebGL build:
                // metallic=0.7 on a flat basecolor (no metallic map) makes ~70% of the surface
                // colour come from ENVIRONMENT reflection, not albedo. The editor/Standalone
                // have rich skybox+reflection so the steel read fine; the WebGL build has weak/
                // flat environment reflection (reduced reflection probes + URP StripUnusedVariants),
                // so the metal had almost nothing to reflect and rendered as a desaturated grey/
                // white "colorless" hero. The companion Knight (StoryCompanionInjector.BindClassDiffuse)
                // never sets metallic and renders fine — match that. A small metallic + moderate
                // smoothness still gives an armour highlight without washing out the basecolor.
                if (cls == HeroClass.Knight)
                {
                    if (baked.HasProperty("_Metallic"))   baked.SetFloat("_Metallic", 0.1f);
                    if (baked.HasProperty("_Smoothness")) baked.SetFloat("_Smoothness", 0.35f);

                    // WO (2026-06-10) KNIGHT BODY SWAP → hero-tank: the rigged AccuRIG CC_Base
                    // Knight ships a UV-matched NORMAL map alongside its basecolor (copied to
                    // Heroes/Textures/HeroTank_Normal, imported textureType=NormalMap). Unlike the
                    // stray import-bound maps cleared above (which read as colour artefacts on the
                    // OTHER classes), this is the mesh's OWN tangent-space normal — bind it so the
                    // armour plates/cloth catch surface detail. Knight-ONLY: the other classes ship
                    // base-color only and keep _BumpMap cleared. Null-guarded — a missing normal
                    // is a clean no-op (the basecolor still drives the look).
                    // WO-481 Slice 4: the armored Tripo Knight ships a UV-matched NORMAL map alongside
                    // its basecolor (medieval_knight_3d_model_normal, copied to Heroes/Textures/
                    // KnightArmored_normal, imported textureType=NormalMap). Bind it so the armour
                    // plates/cloth catch surface detail. Null-guarded — a missing normal is a clean no-op.
                    var knightArmoredNormal = Resources.Load<Texture2D>("Heroes/Textures/KnightArmored_normal");
                    if (knightArmoredNormal != null && baked.HasProperty("_BumpMap"))
                    {
                        baked.SetTexture("_BumpMap", knightArmoredNormal);
                        baked.EnableKeyword("_NORMALMAP");
                    }
                }
            }

            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    if (baked != null) r.sharedMaterial = baked;
                    continue;
                }
                for (int i = 0; i < mats.Length; i++)
                {
                    if (baked != null)
                    {
                        // For all (unified CC5 path): always replace slots with the baked material that has
                        // the correct class basecolor atlas bound to _BaseMap and white _BaseColor.
                        // This ensures vibrant correct textures regardless of what the FBX import remapped
                        // (or left null). Stray metallic/normal from import are discarded (they were causing
                        // color artefacts like patchy red/purple).
                        mats[i] = baked;
                    }
                    else if (mats[i] != null)
                    {
                        // Legacy Tripo (Mage/Knight): paint the diffuse onto the EXISTING URP slot.
                        // These slots are already URP/Lit (RetargetMaterialsToUrp ran first) but with
                        // no _BaseMap bound → grey. Bind the atlas and clear any dark/zero _BaseColor
                        // so the texture (not a leftover tint) drives the look.
                        if (mats[i].HasProperty("_BaseMap")) mats[i].SetTexture("_BaseMap", tex);
                        if (mats[i].HasProperty("_MainTex")) mats[i].SetTexture("_MainTex", tex);
                        if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", Color.white);
                        if (mats[i].HasProperty("_Color"))     mats[i].SetColor("_Color", Color.white);
                    }
                }
                r.sharedMaterials = mats;
            }
            Debug.Log("[HeroBodySwapper] DEF-267 ApplyExtractedTexture: " + cls +
                      " diffuse '" + texPath + "' bound (mode=" + (isCc5Combined ? "CC5-replace" : "Tripo-paint") + ").");
            return true;
        }

        /// <summary>
        /// KNIGHT STOPGAP (2026-06-09): replace EVERY renderer slot on the Knight body with one
        /// flat, solid steel-grey URP/Lit material — NO basecolor/normal/metallic/occlusion maps.
        /// The Knight FBX has no UV-matched atlas in-project (mesh material 'tripo_mat_18e58c3e';
        /// every candidate atlas mis-samples → the speckled/patchy look). A clean solid armour grey
        /// is presentable for go-live; a correct Knight re-import can restore textures later.
        /// Knight-ONLY — never called for Ranger/Mage/Cleric, whose textured paths work.
        /// </summary>
        private static void ApplyFlatSteelStopgap(GameObject body)
        {
            if (body == null) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");
            if (lit == null)
            {
                Debug.LogWarning("[HeroBodySwapper] Knight stopgap: no URP/Lit or Standard shader — skipped.");
                return;
            }

            // Believable plate-armour grey (#7A7E85), low metallic so the basecolor (not the
            // environment reflection) drives the look, moderate smoothness for a soft steel sheen.
            Color steel = new Color(0.478f, 0.494f, 0.522f, 1f); // #7A7E85
            var mat = new Material(lit) { name = "Knight (flat steel stopgap)" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", steel);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", steel);
            // Explicitly NO maps — clear any the shader defaults in and disable their keywords so
            // nothing samples a wrong-UV texture and re-introduces the speckle.
            if (mat.HasProperty("_BaseMap"))          mat.SetTexture("_BaseMap", null);
            if (mat.HasProperty("_MainTex"))          mat.SetTexture("_MainTex", null);
            if (mat.HasProperty("_BumpMap"))          mat.SetTexture("_BumpMap", null);
            if (mat.HasProperty("_NormalMap"))        mat.SetTexture("_NormalMap", null);
            if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", null);
            if (mat.HasProperty("_SpecGlossMap"))     mat.SetTexture("_SpecGlossMap", null);
            if (mat.HasProperty("_OcclusionMap"))     mat.SetTexture("_OcclusionMap", null);
            if (mat.HasProperty("_EmissionMap"))      mat.SetTexture("_EmissionMap", null);
            mat.DisableKeyword("_NORMALMAP");
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            mat.DisableKeyword("_OCCLUSIONMAP");
            mat.DisableKeyword("_EMISSION");
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", 0.1f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);

            int slots = 0;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                int count = (r.sharedMaterials != null && r.sharedMaterials.Length > 0)
                    ? r.sharedMaterials.Length : 1;
                var mats = new Material[count];
                for (int i = 0; i < count; i++) { mats[i] = mat; slots++; }
                r.sharedMaterials = mats;
            }
            Debug.Log("[HeroBodySwapper] Knight flat-steel stopgap applied to " + slots +
                      " slot(s) — solid #7A7E85, no maps (speckle fix; awaiting clean Knight re-import).");
        }

        /// <summary>
        /// If the URP material on the body still ended up white (Tripo's
        /// embedded textures sometimes don't survive import), paint each
        /// material with a class tint so the body reads coloured anyway.
        /// Skipped when a real diffuse texture is already bound (e.g. the
        /// extracted basecolor PNG from Tripo's Send-To-Unity flow).
        /// </summary>
        private static void ApplyClassTint(GameObject body, HeroClass cls)
        {
            Color tint = cls switch
            {
                // DEF-231: the Knight (Grom) is a LEGACY Tripo body with NO baked
                // basecolor (ApplyExtractedTexture returns null for Knight), so this
                // flat tint IS his armour colour. The old steel (0.78,0.80,0.86) read
                // dark/muddy/desaturated in scene light. Lift the value to a brighter,
                // cleaner cool steel (keep it cool — blue-biased, NOT muddy brown) so the
                // armour reads crisp without going flat-white.
                HeroClass.Knight => new Color(0.92f, 0.94f, 1.00f),   // bright cool steel
                HeroClass.Ranger => new Color(0.40f, 0.50f, 0.34f),   // hunter / leaf green
                _                => new Color(0.60f, 0.45f, 0.85f),   // mage fallback
            };

            // Owner 2026-05-26: forcing a flat tint "lost the Knight's colour" — it
            // cleared the textured detail. Restore the original rule: paint the tint
            // ONLY when a material has no diffuse texture. That preserves every real
            // texture — the Ranger's fresh v2 basecolor, the Mage's embedded texture,
            // and whatever the Knight imported — and the tint is just the fallback so
            // an untextured mesh still reads coloured instead of solid white.
            // WO-330 anti-cyan: a CC5/AccuRIG combined mesh can import its renderer slots as
            // NULL (or Unity's Default-Material), which URP renders as a bright cyan/magenta
            // UNLIT error shape — the "solid cyan silhouette". A null slot must be REPLACED with
            // a real URP/Lit material (tinted), not skipped, or the hero stays cyan. Build the
            // lit shader once for that case.
            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");

            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    // WO-330: a null/default slot is the cyan culprit — give it a fresh lit,
                    // tinted material so it is never left on URP's error/default shader.
                    if (m == null)
                    {
                        if (lit == null) continue;
                        var fill = new Material(lit) { name = cls + " (tint fill)" };
                        if (fill.HasProperty("_BaseColor")) fill.SetColor("_BaseColor", tint);
                        if (fill.HasProperty("_Color"))     fill.SetColor("_Color", tint);
                        if (cls == HeroClass.Knight)
                        {
                            if (fill.HasProperty("_Metallic"))   fill.SetFloat("_Metallic", 0.35f);
                            if (fill.HasProperty("_Smoothness")) fill.SetFloat("_Smoothness", 0.45f);
                        }
                        mats[i] = fill;
                        continue;
                    }
                    Texture tex = null;
                    if (m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
                    if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
                    if (tex != null) continue;   // preserve real textures
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                    if (m.HasProperty("_Color"))     m.SetColor("_Color", tint);
                    // DEF-231 (KNIGHT ONLY): the tinted Knight armour was reading dark
                    // because the runtime URP material's matte 0.15 smoothness / 0 metallic
                    // gives it no specular lift, so flat steel sits dim in scene light. Give
                    // the untextured Knight a touch of metallic + higher smoothness so the
                    // armour catches a soft highlight and reads as crisp steel — NOT a mirror
                    // (kept well under chrome) and NOT muddy. Scoped to Knight so Mage/Ranger/
                    // Cleric keep their existing matte look.
                    if (cls == HeroClass.Knight)
                    {
                        if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", 0.35f);
                        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.45f);
                    }
                }
                r.sharedMaterials = mats;
            }
        }

        private static void NormalizeHeight(GameObject go, float targetHeight)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y <= 0.01f) return;
            float scale = targetHeight / b.size.y;
            go.transform.localScale *= scale;

            // Owner 2026-05-20 ("archer appears halfway under surface"): the
            // Tripo Ranger FBX pivots near the mesh centre, so after scaling
            // the lower half drops below the hero root's Y=0. Recompute the
            // post-scale bounds and lift the body so the feet land at y=0.
            Bounds b2 = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b2.Encapsulate(renderers[i].bounds);
            float feetOffset = b2.min.y - go.transform.position.y;
            if (feetOffset < 0f)
                go.transform.localPosition -= new Vector3(0f, feetOffset, 0f);
        }

        private static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                if (c != null) Destroy(c);
        }

        private static void StripRigidbodies(GameObject go)
        {
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
                if (rb != null) Destroy(rb);
        }

        /// <summary>
        /// DEF-232 guard: assert the swapped body's render bounds sit centred (XZ) over the
        /// hero ROOT — the transform the follow camera tracks and HeroLocomotion drives. A
        /// horizontal offset is the exact "camera stays to her right / body pivots in place"
        /// regression (an off-pivot mesh rotated after seating). Logs LOUDLY so it is caught at
        /// the spawn source rather than re-surfacing as a camera complaint, and re-centres as a
        /// runtime safety net so the player is never left with the body off to one side.
        /// </summary>
        private void ValidateBodyCentredOverRoot(GameObject body)
        {
            if (body == null) return;
            var rends = body.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            Vector3 root = transform.position;
            float dx = b.center.x - root.x;
            float dz = b.center.z - root.z;
            float planarOffset = Mathf.Sqrt(dx * dx + dz * dz);

            // ~0.35 m tolerance: a hand/weapon prop can pull the encapsulated centre slightly
            // off the body's own midline without it reading as "offset to the side".
            const float MaxPlanarOffset = 0.35f;
            if (planarOffset > MaxPlanarOffset)
            {
                Debug.LogError(
                    "[HeroBodySwapper] DEF-232 — swapped body '" + body.name + "' is OFF-CENTRE " +
                    planarOffset.ToString("F2") + "m from the hero root (dx=" + dx.ToString("F2") +
                    ", dz=" + dz.ToString("F2") + "). The follow camera tracks the ROOT, so the " +
                    "body would appear offset to one side and 'pivot in place'. Re-centring as a " +
                    "safety net — investigate the mesh pivot / Skin seating.");
                // Safety-net re-centre: shift the body so its bounds centre sits over the root's XZ.
                body.transform.position -= new Vector3(dx, 0f, dz);
            }
        }

        /// <summary>
        /// DEF-232 (facing) self-correct. The legacy fix rotated EVERY hero body by a hard-coded
        /// -90° on the assumption all meshes import facing +X. That's a guessed Euler: a body that
        /// imports facing some other axis is left angled, so the character STRAFES across the screen
        /// ("walks at 45°") even though HeroLocomotion drives the ROOT dead-straight (proven by
        /// world-coords). Here we DERIVE the body's true forward from the skeleton — the hip-to-hip
        /// vector is the character's lateral axis on EVERY humanoid rig, so forward = right × up is
        /// rig-independent (no guess) — then rotate the body by only the RESIDUAL needed to match the
        /// root's heading. Residual + 5° deadzone ⇒ an already-aligned body (the working heroes) gets
        /// a NO-OP; this can ONLY fix a misaligned body, never disturb a correct one. Non-humanoid /
        /// unmappable rigs fall back to the base seat untouched.
        /// </summary>
        private void AlignBodyFacingToRoot(GameObject body, Animator anim)
        {
            if (body == null || anim == null) return;
            if (!anim.isHuman || anim.avatar == null || !anim.avatar.isValid) return;

            var leftLeg  = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var rightLeg = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            if (leftLeg == null || rightLeg == null) return;     // can't derive — keep the base seat

            // Character RIGHT = left-hip → right-hip (laterally placed on every humanoid), ground-projected.
            Vector3 rightDir = rightLeg.position - leftLeg.position;
            rightDir.y = 0f;
            if (rightDir.sqrMagnitude < 1e-6f) return;           // degenerate pose — bail safely
            rightDir.Normalize();

            // FORWARD = right × up (Unity left-handed: Cross((1,0,0),(0,1,0)) == (0,0,1)).
            Vector3 bodyForward = Vector3.Cross(rightDir, Vector3.up);

            Vector3 rootForward = transform.forward;
            rootForward.y = 0f;
            if (rootForward.sqrMagnitude < 1e-6f) rootForward = Vector3.forward;
            rootForward.Normalize();

            float curYaw = Mathf.Atan2(bodyForward.x, bodyForward.z) * Mathf.Rad2Deg;
            float tgtYaw = Mathf.Atan2(rootForward.x, rootForward.z) * Mathf.Rad2Deg;
            float delta  = Mathf.DeltaAngle(curYaw, tgtYaw);

            const float DeadzoneDeg = 5f;
            if (Mathf.Abs(delta) <= DeadzoneDeg)
            {
                Debug.Log($"[HeroBodySwapper] facing OK — body within {delta:F1}° of root heading; no correction.");
                return;
            }

            // Rotate the body about its OWN origin (position preserved) to face the root heading.
            body.transform.rotation = Quaternion.AngleAxis(delta, Vector3.up) * body.transform.rotation;
            Debug.Log($"[HeroBodySwapper] facing self-correct: skeleton forward {curYaw:F0}° vs root {tgtYaw:F0}° → rotated body {delta:F0}° (derive-from-skeleton, replaces guessed -90°).");

            // A yaw on an off-pivot mesh can shift its bounds — re-centre XZ and re-plant feet so the
            // body stays dead-on the root the camera follows.
            ValidateBodyCentredOverRoot(body);
            PlantFeetOnGround(body);
        }

        /// <summary>
        /// DEF-235: drop the swapped body so the lowest VISIBLE skinned vertex (the feet/soles)
        /// rests exactly at the hero root's Y. VisualFactory.SeatOnGround already aligned the
        /// RENDER-BOUNDS base to root.y, but a SkinnedMeshRenderer's bounds carry bind-pose
        /// padding — the bounds floor sits a few cm BELOW the actual posed soles — so the feet
        /// hover. We bake each skinned mesh into its current pose, walk its world-space vertices
        /// to find the true lowest point, and nudge the body DOWN by (lowestVertexY - root.y).
        ///
        /// Y-ONLY (does not touch XZ, so it never disturbs DEF-232's centring) and HERO-SCOPED
        /// (VisualFactory.SeatOnGround stays unchanged, so enemies/structures are unaffected).
        /// Clamped: only ever corrects a small UPWARD float — it never LIFTS the body, and it
        /// never sinks more than MaxDrop, so a bad measurement (e.g. a stray low vert on a cape)
        /// can't bury the hero under the terrain.
        /// </summary>
        private void PlantFeetOnGround(GameObject body)
        {
            if (body == null) return;

            float rootY = transform.position.y;
            float lowestY = float.PositiveInfinity;
            bool measured = false;

            // Prefer skinned meshes baked into their CURRENT pose — the static prefab mesh is in
            // bind pose and its lowest vertex won't match the posed soles. Bake into a scratch
            // mesh, transform each vertex to world space, and track the minimum Y.
            var skinned = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned != null && skinned.Length > 0)
            {
                var baked = new Mesh();
                foreach (var smr in skinned)
                {
                    if (smr == null || smr.sharedMesh == null) continue;
                    smr.BakeMesh(baked); // baked is in the SMR's local space, current pose
                    var verts = baked.vertices;
                    Transform t = smr.transform;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        float wy = t.TransformPoint(verts[i]).y;
                        if (wy < lowestY) lowestY = wy;
                    }
                    measured = true;
                }
                Object.Destroy(baked);
            }

            // Fallback for any non-skinned (static MeshRenderer) body: walk its shared meshes'
            // vertices directly in world space. Rare for the hero, but keeps this robust.
            if (!measured)
            {
                foreach (var mf in body.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    // BUILD-SAFE: sharedMesh.vertices THROWS on an FBX mesh that isn't
                    // Read/Write-enabled (the import default in a player build). Skip those —
                    // PlantFeetOnGround then degrades to "not measured" and leaves the body
                    // where SeatOnGround placed it, rather than crashing.
                    if (!mf.sharedMesh.isReadable) continue;
                    var verts = mf.sharedMesh.vertices;
                    Transform t = mf.transform;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        float wy = t.TransformPoint(verts[i]).y;
                        if (wy < lowestY) lowestY = wy;
                    }
                    measured = true;
                }
            }

            if (!measured || float.IsInfinity(lowestY)) return;

            // gap > 0 means the soles float ABOVE the ground (the DEF-235 symptom). gap <= 0 means
            // the feet already touch or sit below — leave those alone (never LIFT the body; sinking
            // is handled by SeatOnGround / terrain follow elsewhere).
            float gap = lowestY - rootY;
            const float MinFloat = 0.001f; // ignore sub-mm noise
            const float MaxDrop  = 0.30f;  // never sink more than 30 cm on a bad measurement
            if (gap <= MinFloat) return;
            float drop = Mathf.Min(gap, MaxDrop);
            body.transform.position -= new Vector3(0f, drop, 0f);
            Debug.Log("[HeroBodySwapper] DEF-235 planted feet: lowered body " +
                      drop.ToString("F3") + "m (measured float gap " + gap.ToString("F3") + "m).");
        }
    }
}
