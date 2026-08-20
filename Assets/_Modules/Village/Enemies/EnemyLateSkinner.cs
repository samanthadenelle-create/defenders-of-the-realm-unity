// =============================================================================
// EnemyLateSkinner — turns a placeholder capsule back into a real enemy the moment
// its family's content arrives.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village (Village/Enemies). Attached ONLY by EnemyFactory.Build,
// and only when the body could not be skinned at spawn time.
//
// ⛔ WHY THIS EXISTS (2026-08-20).
// Enemy art moved out of Resources onto the CDN, and the old loader answered a cold
// cache by BLOCKING on Addressables — the exact pattern that deadlocked the device for
// three minutes on the structure seam the same day (proof, cited to file and line, in
// EnemyContentWarmer.cs's header). The fix is to never wait. But "never wait" on its
// own would trade a hang for a permanent defect: an enemy spawned two seconds before
// its family bundle lands would wear a coloured capsule for the whole encounter, and
// the owner's captured symptom was exactly that capsule.
//
// So the contract has two halves and BOTH are required:
//   1. EnemyAssetLoader returns null instead of waiting  (no hang), and
//   2. this component re-skins when the content lands     (no permanent capsule).
// Half 1 without half 2 is not a fix, it is a nicer-looking bug.
//
// HOW IT DEGRADES, LOUDLY AND DIFFERENTLY PER CAUSE:
//   • NOT YET DOWNLOADED -> keep the capsule, keep polling, swap in the real body on
//     arrival, and say so. Self-destructs on success.
//   • GENUINELY MISSING  -> stop. Say ONCE, at error level, that this enemy will not
//     re-skin and what has to be shipped to fix it. Polling forever on an address
//     Addressables has already refused is how a log becomes noise.
//   • STILL WAITING PAST THE DEADLINE -> report once and stop, so a stuck CDN does not
//     leave a poller per enemy running for the rest of the session.
//
// ⛔ NOTHING HERE BLOCKS. It is a dictionary probe on a timer, on the player loop.
// Assets/Editor/Regression/EnemyLoadBoundedRegression.cs fails the build if a blocking
// wait appears in this file.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Polls the enemy residency cache and replaces this enemy's placeholder capsule with the
    /// real skinned body as soon as the art is resident. Self-destructs once it succeeds, once
    /// the asset is proven missing, or once it has waited past <see cref="GiveUpSeconds"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyLateSkinner : MonoBehaviour
    {
        /// <summary>Seconds between residency probes. A dictionary lookup — cheap enough that a
        /// wave of these costs nothing, slow enough that it never shows up in a profile.</summary>
        public const float PollSeconds = 0.5f;

        /// <summary>How long to keep hoping. Past this the download is not "in progress" in any
        /// useful sense and one report beats a permanent poller per enemy.</summary>
        public const float GiveUpSeconds = 120f;

        private EnemyDef _def;
        private string _model;
        private float _height;
        private float _sizeScale;
        private float _armedAt;
        private float _nextPollAt;

        /// <summary>
        /// Arm a re-skin on <paramref name="go"/>. Idempotent — a re-arm on the same body just
        /// refreshes the model. Does nothing (and costs nothing) if the model is already resident,
        /// because then the caller would not have needed a capsule in the first place.
        /// </summary>
        internal static void Arm(GameObject go, EnemyDef def, string model, float height, float sizeScale)
        {
            if (go == null || string.IsNullOrEmpty(model)) return;

            string address = EnemyAssetLoader.EnemyAddrPrefix + model;

            // Proven-missing: there is nothing to wait for. EnemyFactory.ReportNoRenderableMesh has
            // already said so at error level; do not also arm a poller that can never succeed.
            if (EnemyContentWarmer.IsKnownAbsent<GameObject>(address)) return;
            if (EnemyContentWarmer.IsSettled && !EnemyContentWarmer.IsRegisteredAddress(address)) return;

            var skinner = go.GetComponent<EnemyLateSkinner>();
            if (skinner == null) skinner = go.AddComponent<EnemyLateSkinner>();
            skinner._def = def;
            skinner._model = model;
            skinner._height = height;
            skinner._sizeScale = sizeScale;
            skinner._armedAt = Time.realtimeSinceStartup;
            skinner._nextPollAt = 0f;

            // Kick the family fetch from here too. Cheap and idempotent, and it means an enemy
            // armed by a path that did not go through the loader still pulls its family bundle.
            EnemyAssetLoader.PrewarmFamily(model);

            FlowTrace.Once("Enemy", "late-skin-armed-" + model,
                $"'{model}' (id '{(def != null ? def.Id : "?")}') spawned as a PLACEHOLDER CAPSULE and armed a " +
                $"late re-skin on family '{EnemyContentWarmer.FamilyOf(model)}'. The capsule is temporary by " +
                "construction — if it is still there when the family lands, this component failed and the " +
                "next line will say so.");
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextPollAt) return;
            _nextPollAt = now + PollSeconds;

            if (string.IsNullOrEmpty(_model)) { Destroy(this); return; }

            string address = EnemyAssetLoader.EnemyAddrPrefix + _model;

            // ---- Give up: the asset is proven missing. -----------------------
            if (EnemyContentWarmer.IsKnownAbsent<GameObject>(address) ||
                (EnemyContentWarmer.IsSettled && !EnemyContentWarmer.IsRegisteredAddress(address)))
            {
                FlowTrace.Once("Enemy", "late-skin-missing-" + _model,
                    $"late re-skin of '{_model}' (id '{(_def != null ? _def.Id : "?")}') ABANDONED: '{address}' is " +
                    "GENUINELY MISSING, not slow — Addressables has no usable location for it and " +
                    "Assets/Resources/Enemies no longer exists. This body keeps its tinted capsule for the rest " +
                    "of the launch. Fix by shipping that address in the enemy Addressable group; nothing about " +
                    "waiting longer can help. VISUAL defect only — the enemy still spawns, moves and fights.");
                Destroy(this);
                return;
            }

            // ---- Not resident yet: keep the capsule, keep waiting. -----------
            // A dictionary probe. It cannot download, pump, sleep or deadlock.
            if (!EnemyAssetLoader.IsResident<GameObject>(address))
            {
                if (now - _armedAt > GiveUpSeconds)
                {
                    FlowTrace.Once("Enemy", "late-skin-timeout-" + _model,
                        $"late re-skin of '{_model}' has been waiting {now - _armedAt:F0}s for family " +
                        $"'{EnemyContentWarmer.FamilyOf(_model)}' (familyDownloading=" +
                        $"{EnemyContentWarmer.IsFamilyDownloading(EnemyContentWarmer.FamilyOf(_model))}, " +
                        $"catalogState={EnemyContentWarmer.State}, pending={EnemyContentWarmer.PendingRequests}) — " +
                        "giving up polling so one stuck fetch does not leave a poller per enemy running. The body " +
                        "keeps its capsule. Check CDN reachability for this family's bundle. Nothing hung.");
                    Destroy(this);
                }
                return;
            }

            // ---- Resident. Rebuild the REAL body, same recipe as the spawn. --
            var vis = EnemyFactory.TrySkinBody(gameObject, _def, _model, _height);
            if (vis == null)
            {
                // Resident but unskinnable means the render-verify inside TrySkinBody rejected it
                // (no enabled renderer / no mesh). That is an ASSET defect, not a timing one, and
                // TrySkinBody has already logged which check failed. Do not spin on it.
                FlowTrace.Once("Enemy", "late-skin-unskinnable-" + _model,
                    $"late re-skin of '{_model}' ABANDONED: the asset at '{address}' IS resident but produced no " +
                    "renderable body (see the render-verify line above). Keeping the capsule. This is an asset " +
                    "defect, not a download problem.");
                Destroy(this);
                return;
            }

            // Real body is in. Remove the placeholder.
            var capsule = transform.Find(EnemyFactory.CapsuleName);
            if (capsule != null) Destroy(capsule.gameObject);

            // ActorAnimator re-binds itself: its EnsureAnimator re-runs GetComponentInChildren
            // whenever its cached Animator is null, and the capsule never had one. So the drives
            // Enemy.cs issues (SetLocomotion / PlayAttack / Die) pick up the new rig with no
            // explicit re-wire here — verified at Assets/_Modules/Core/Combat/ActorAnimator.cs.
            FlowTrace.Step("Enemy",
                $"LATE RE-SKIN OK: '{_model}' (id '{(_def != null ? _def.Id : "?")}') swapped its placeholder " +
                $"capsule for the real body {Time.realtimeSinceStartup - _armedAt:F1}s after spawn, once family " +
                $"'{EnemyContentWarmer.FamilyOf(_model)}' arrived. This is the path that makes 'never block' " +
                "survivable: the game kept running and the enemy is correct now.");

            Destroy(this);
        }
    }
}
