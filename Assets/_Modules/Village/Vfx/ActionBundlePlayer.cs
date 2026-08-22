// =============================================================================
// ActionBundlePlayer — presentation-side action-bundle player (WO-671 §3).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
// Canon: docs/ACTION_KEYWORD_REGISTRY_ARCHITECTURE.md §3/§4, WO-671 §3/§4.
//
// ONE keyword = one full action bundle: PlayAction(target, keyword, actor)
// fires animation + pooled VFX (timed, bone-attached) + SFX for hero and
// enemies alike, resolved from motion-castings.json rows (ActionBundleCatalog).
//
// PRESENTATION-LAYER (ARCHITECTURE §2): gameplay objects never play effects —
// callers hand this player an actor transform and a keyword; it observes and
// styles. It NEVER drives combat state or damage.
//
// THE THREE CHANNELS (arch §3/§4 laws):
//   Animation — routed through the EXISTING ActorAnimator drive for the keyword
//     (PlayAttack/PlayCast/PlayWindUp/PlayHit/Die/PlayVictory). Absolutely NO
//     Animator.CrossFade of raw clips — runtime clip swap is Phase 2 (arch §3);
//     the baked controllers keep their tuned transitions. Keywords with no
//     existing drive verb (taunt, block, locomotion …) play effects-only
//     (anim=none in the trace). playOneShot rows also skip the animator
//     entirely: effects overlay, base state undisturbed (WO-671 §1).
//   VFX — after row.vfxDelay seconds, VFXManager.PlayKey(row.vfxKey, bone) —
//     pooled by the shared Hovl pool, never Instantiate. attachBone resolves
//     via the humanoid Animator.GetBoneTransform where possible, name-search
//     fallback with a Warn (mirrors WeaponTrailController's anchor ladder).
//   SFX — row.sfxId names a Resources/Sfx/<sfxId> clip played through the
//     existing CoreServices.Audio?.PlaySfx seam (the GameSfx / EnemyCombatAudio
//     convention — the SfxId enum lives in DeNelle.Audio, which Village cannot
//     reference, so the AudioClip overload IS the seam per GameSfx's header).
//
// DOUBLE-FIRE GUARD (arch §4 one-owner rule): abilities.json keeps VFX authority
// for ability casts (HeroAbilities.PlayCastVfxKey). An ability-driven cast must
// use the ability path, NOT this player — if a caller passes an ability-managed
// keyword (cast/castChannel/skill1/skill2) without suppressVfx:true, this player
// Warns and suppresses the row VFX so the effect can never fire twice.
//
// EXAMPLE USAGE (WO-671 deliverable — capability only, no gameplay call sites yet):
//   // Hero: the knight's taunt moment at battle start — full bundle
//   // (anim if a drive exists, VFX on the resolved bone after its delay, SFX):
//   ActionBundlePlayer.PlayAction("knight", ActionKeywords.Taunt, heroTransform);
//
//   // Enemy: an orc-warrior heavy swing (inherits fallback: orc-warrior -> orc
//   // -> humanoid), effects timed by the row's vfxDelay/attachBone:
//   ActionBundlePlayer.PlayAction("orc-warrior", ActionKeywords.Heavy, enemy.transform);
//
//   // Ability-managed keyword from inside an ability flow: the ability path owns
//   // the VFX (abilities.json), so opt out of the row's vfx explicitly:
//   ActionBundlePlayer.PlayAction("knight", ActionKeywords.Cast, heroTransform, suppressVfx: true);
// =============================================================================

using System;
using System.Collections;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Static presentation service: plays the full (target, keyword) action
    /// bundle — animation via the existing <see cref="ActorAnimator"/> drive,
    /// pooled VFX via <see cref="VFXManager.PlayKey"/> after the row's delay,
    /// SFX via the <see cref="CoreServices.Audio"/> seam. Never touches gameplay
    /// state; never CrossFades raw clips (arch §3). See the class header for the
    /// hero/enemy usage examples.
    /// </summary>
    public static class ActionBundlePlayer
    {
        private const string System = "Action";
        private const float SfxVolume = 0.6f;   // GameSfx-range combat one-shot level

        // Hidden coroutine host for delayed VFX (the VFXManager-singleton pattern,
        // self-bootstrapped: no scene wiring, no drag-drop).
        private static ActionBundleRunner s_runner;

        /// <summary>
        /// Plays the action bundle for (<paramref name="target"/>, <paramref name="keyword"/>)
        /// on <paramref name="actorTransform"/> (the actor ROOT — hero, enemy, pet).
        /// Resolves the row through the registry's inherits chain; a miss warns and
        /// plays nothing (never silent, never a fallback effect).
        ///
        /// DOUBLE-FIRE GUARD: an ability-driven cast must use the ability path
        /// (abilities.json vfx via HeroAbilities) — passing an ability-managed
        /// keyword (cast/castChannel/skill1/skill2) here without
        /// <paramref name="suppressVfx"/>=true logs a Warn and suppresses the row
        /// VFX (one owner per concern, arch §4).
        /// </summary>
        /// <param name="target">Registry target — enemy family or hero class ("knight", "orc-warrior").</param>
        /// <param name="keyword">Closed-vocabulary keyword (<see cref="ActionKeywords"/>).</param>
        /// <param name="actorTransform">The acting rig's root transform (bone + animator resolved beneath it).</param>
        /// <param name="suppressVfx">True = caller owns the VFX (ability path) — the row's vfxKey is not fired.</param>
        /// <returns>True when a row resolved and the bundle (or its effects-only subset) was dispatched.</returns>
        public static bool PlayAction(string target, string keyword, Transform actorTransform,
                                      bool suppressVfx = false)
        {
            if (actorTransform == null)
            {
                FlowTrace.Warn(System, $"PlayAction('{target}.{keyword}') rejected — null actorTransform.");
                return false;
            }

            if (!ActionBundleCatalog.TryResolve(target, keyword, out var row, out string resolvedFrom))
                return false;   // TryResolve already warned the full chain

            // ── Double-fire guard (arch §4): abilities.json owns ability-cast VFX ──
            bool vfxAllowed = !suppressVfx;
            if (vfxAllowed && IsAbilityManaged(keyword) && !string.IsNullOrEmpty(row.vfxKey))
            {
                FlowTrace.Warn(System,
                    $"PlayAction('{target}.{keyword}'): keyword is ABILITY-MANAGED — abilities.json keeps " +
                    "vfx authority for casts (one-owner rule). Row vfx suppressed; use the ability path " +
                    "or pass suppressVfx:true to acknowledge.");
                vfxAllowed = false;
            }

            // ── Animation: the EXISTING drive only (no raw-clip CrossFade — arch §3) ──
            string animState = "none";
            if (!row.playOneShot)
            {
                var actor = ResolveActor(actorTransform);
                if (actor != null)
                    animState = DriveAnimation(actor, keyword);
                else
                    FlowTrace.Warn(System,
                        $"PlayAction('{target}.{keyword}'): no ActorAnimator on/under '{actorTransform.name}' — effects only.");
            }
            // playOneShot: effects overlay only — base animator state undisturbed (WO-671 §1).

            // ── VFX: pooled PlayKey on the resolved bone after row.vfxDelay ──
            string boneLabel = "actor";
            if (vfxAllowed && !string.IsNullOrEmpty(row.vfxKey))
            {
                Transform bone = ResolveAttachBone(actorTransform, row.attachBone, out boneLabel);
                if (row.vfxDelay > 0f)
                    EnsureRunner().Schedule(row.vfxKey, bone, actorTransform, row.vfxDelay);
                else
                    FireVfx(row.vfxKey, bone, actorTransform);
            }

            // ── SFX: through the existing audio seam (null-conditional) ──
            if (!string.IsNullOrEmpty(row.sfxId))
                PlaySfx(row.sfxId);

            // ── Logging (WO-671 §4, binding): the trace step + the consume line ──
            FlowTrace.Step(System,
                $"bundle '{target}.{keyword}': anim={animState} " +
                $"vfx={(vfxAllowed && !string.IsNullOrEmpty(row.vfxKey) ? row.vfxKey : "none")}@{row.vfxDelay}s " +
                $"bone={boneLabel} sfx={(string.IsNullOrEmpty(row.sfxId) ? "none" : row.sfxId)}");
            Debug.Log($"[MotionCaster] '{target}.{keyword}' -> '{row.clip}' " +
                $"({(row.manual ? "manual" : string.IsNullOrEmpty(row.source) ? "auto" : row.source)})" +
                (resolvedFrom != target ? $" [via '{resolvedFrom}']" : string.Empty));
            return true;
        }

        // ── Animation routing ─────────────────────────────────────────────────

        /// <summary>Keywords whose VFX are owned by the ability path (abilities.json —
        /// HeroAbilities.PlayCastVfxKey), per arch §4. The row's vfxKey never fires for
        /// these unless the caller is the ability system itself (which suppresses).</summary>
        private static bool IsAbilityManaged(string keyword)
        {
            return keyword == ActionKeywords.Cast
                || keyword == ActionKeywords.CastChannel
                || keyword == ActionKeywords.Skill1
                || keyword == ActionKeywords.Skill2;
        }

        /// <summary>
        /// Routes the keyword through the existing <see cref="ActorAnimator"/> verb —
        /// the baked-controller drive surface (guarded, param-cached). Returns the
        /// state label for the trace, "none" when no existing drive maps this keyword
        /// (locomotion/taunt/block/… — effects-only until a drive verb exists).
        /// </summary>
        private static string DriveAnimation(ActorAnimator actor, string keyword)
        {
            switch (keyword)
            {
                case ActionKeywords.Attack0: actor.PlayAttack(0); return "Attack(0)";
                case ActionKeywords.Attack1: actor.PlayAttack(1); return "Attack(1)";
                case ActionKeywords.Attack2: actor.PlayAttack(2); return "Attack(2)";
                case ActionKeywords.Attack3: actor.PlayAttack(3); return "Attack(3)";

                // skill1/skill2 are the Cast_q..r variant slots (canon §5) — cast variants.
                case ActionKeywords.Cast:        actor.PlayCast(0); return "Cast(0)";
                case ActionKeywords.CastChannel: actor.PlayCast(0); return "Cast(0)";
                case ActionKeywords.Skill1:      actor.PlayCast(1); return "Cast(1)";
                case ActionKeywords.Skill2:      actor.PlayCast(2); return "Cast(2)";

                case ActionKeywords.WindUp:  actor.PlayWindUp();  return "WindUp";
                case ActionKeywords.Hit:     actor.PlayHit(HitDirection.Front); return "Hit(Front)";
                case ActionKeywords.Victory: actor.PlayVictory(); return "Victory";

                case ActionKeywords.Death0: actor.Die(DeathDirection.Fall);        return "Die(Fall)";
                case ActionKeywords.Death1: actor.Die(DeathDirection.Left);        return "Die(Left)";
                case ActionKeywords.Death2: actor.Die(DeathDirection.Right);       return "Die(Right)";
                case ActionKeywords.Death3: actor.Die(DeathDirection.Front);       return "Die(Front)";
                case ActionKeywords.Death4: actor.Die(DeathDirection.Back);        return "Die(Back)";
                case ActionKeywords.Death5: actor.Die(DeathDirection.Assassinate); return "Die(Assassinate)";

                default:
                    // No existing drive verb for this keyword (heavy, taunt, block,
                    // locomotion …) — effects-only. NOT a raw-clip CrossFade (arch §3).
                    return "none";
            }
        }

        private static ActorAnimator ResolveActor(Transform actorTransform)
        {
            var actor = actorTransform.GetComponentInParent<ActorAnimator>();
            if (actor == null) actor = actorTransform.GetComponentInChildren<ActorAnimator>(true);
            return actor;
        }

        // ── Bone resolution ───────────────────────────────────────────────────

        /// <summary>
        /// Resolves the row's attachBone under the actor: humanoid
        /// <see cref="Animator.GetBoneTransform"/> for the known aliases first,
        /// then a deep name search (Warn — name matches are fragile), else the
        /// actor root (Warn). <paramref name="label"/> reports what won, for the trace.
        /// </summary>
        private static Transform ResolveAttachBone(Transform actorTransform, string attachBone, out string label)
        {
            if (string.IsNullOrEmpty(attachBone))
            {
                label = "actor";
                return actorTransform;
            }

            var animator = actorTransform.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman && TryMapHumanBone(attachBone, out HumanBodyBones human))
            {
                Transform t = animator.GetBoneTransform(human);
                if (t != null)
                {
                    label = attachBone;
                    return t;
                }
            }

            // Name-search fallback — works for props ("weapon") and non-humanoid rigs,
            // but is fragile, so it self-reports (WO-671 §3).
            Transform found = FindDeepChild(actorTransform, attachBone);
            if (found != null)
            {
                FlowTrace.Warn(System,
                    $"attachBone '{attachBone}' resolved by NAME SEARCH under '{actorTransform.name}' " +
                    "(no humanoid mapping) — prefer a humanoid bone alias.");
                label = attachBone + "(name)";
                return found;
            }

            FlowTrace.Warn(System,
                $"attachBone '{attachBone}' NOT FOUND under '{actorTransform.name}' — using the actor root.");
            label = "actor(fallback)";
            return actorTransform;
        }

        /// <summary>Known attachBone aliases → humanoid bones ("hand.r"/"hand_r"/"righthand" …).</summary>
        private static bool TryMapHumanBone(string name, out HumanBodyBones bone)
        {
            switch (name.Trim().ToLowerInvariant())
            {
                case "hand.r": case "hand_r": case "righthand": case "right_hand":
                    bone = HumanBodyBones.RightHand; return true;
                case "hand.l": case "hand_l": case "lefthand": case "left_hand":
                    bone = HumanBodyBones.LeftHand; return true;
                case "head":  bone = HumanBodyBones.Head;  return true;
                case "chest": bone = HumanBodyBones.Chest; return true;
                case "spine": bone = HumanBodyBones.Spine; return true;
                case "hips": case "root": case "pelvis":
                    bone = HumanBodyBones.Hips; return true;
                case "foot.r": case "foot_r": case "rightfoot":
                    bone = HumanBodyBones.RightFoot; return true;
                case "foot.l": case "foot_l": case "leftfoot":
                    bone = HumanBodyBones.LeftFoot; return true;
                default:
                    bone = HumanBodyBones.Hips; return false;
            }
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null) return null;
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeepChild(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        // ── Effects dispatch ──────────────────────────────────────────────────

        /// <summary>Pooled Hovl spawn on the bone (parented so it rides the swing) —
        /// VFXManager.PlayKey no-ops loudly (throttled) on an unknown key.</summary>
        private static void FireVfx(string vfxKey, Transform bone, Transform actorFallback)
        {
            Transform at = bone != null ? bone : actorFallback;
            if (at == null) return;   // actor despawned before the delay elapsed
            VFXManager.PlayKey(vfxKey, at.position, at.rotation, at);
        }

        /// <summary>
        /// Plays the row's SFX through the existing seam: sfxId names an
        /// "Sfx/&lt;sfxId&gt;" audio key (the GameSfx / EnemyCombatAudio drop-in
        /// convention), resolved via DeNelle.Core.AudioAssetLoader (Addressables-first,
        /// Resources-fallback) and routed via CoreServices.Audio?.PlaySfx (mixer,
        /// mobile-safe). A missing clip warns once per id — never silent, never a throw.
        /// </summary>
        private static void PlaySfx(string sfxId)
        {
            var clip = DeNelle.Core.AudioAssetLoader.LoadClip("Sfx/" + sfxId, optional: true);
            if (clip == null)
            {
                FlowTrace.Once(System, "sfx-missing:" + sfxId,
                    $"sfxId '{sfxId}' has no clip at audio key 'Sfx/{sfxId}' (AudioAssetLoader found it in " +
                    "neither Addressables nor Resources) — no sound plays. Drop a CC0 WAV at that key " +
                    "(GameSfx convention) or fix the row's sfxId.");
                return;
            }
            CoreServices.Audio?.PlaySfx(clip, SfxVolume);
        }

        // ── Delayed-VFX runner (hidden coroutine host, self-bootstrapped) ─────

        private static ActionBundleRunner EnsureRunner()
        {
            if (s_runner != null) return s_runner;
            var go = new GameObject("[ActionBundlePlayer]");
            go.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(go);
            s_runner = go.AddComponent<ActionBundleRunner>();
            return s_runner;
        }

        /// <summary>Coroutine host for vfxDelay scheduling. Hidden, scene-persistent;
        /// holds no gameplay state — a destroyed bone at fire time falls back to the
        /// actor root, a destroyed actor is a clean no-op.</summary>
        private sealed class ActionBundleRunner : MonoBehaviour
        {
            public void Schedule(string vfxKey, Transform bone, Transform actorFallback, float delay)
            {
                StartCoroutine(FireAfter(vfxKey, bone, actorFallback, delay));
            }

            private IEnumerator FireAfter(string vfxKey, Transform bone, Transform actorFallback, float delay)
            {
                yield return new WaitForSeconds(delay);
                FireVfx(vfxKey, bone, actorFallback);
            }
        }
    }
}
