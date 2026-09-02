// =============================================================================
// EnemyAnimatorLateBinder — binds an enemy's animator controller when its bundle
// arrives, so "we never block" cannot mean "it slides forever".
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village (Village/Enemies). Attached ONLY by
// EnemyAnimatorFactory.Apply, and only when the controller was not resolvable.
//
// ⛔ WHY (2026-08-20). Enemy content is now fetched PER FAMILY, ON DEMAND, and
// EnemyAssetLoader NEVER waits for it — waiting on Addressables from a spawn path is
// the pattern that deadlocked the game for three minutes on the structure seam the
// same day (proof in EnemyContentWarmer.cs's header). A controller that has not landed
// yet therefore resolves to null, and the Animator holds its bind pose while the
// NavMeshAgent slides it: the "sliding statue" already documented in
// EnemyAnimatorFactory. Transient, but permanent-looking, and the fix must be the
// same shape as the body's: keep going, re-bind when it arrives, say so either way.
//
// This is the animator twin of EnemyLateSkinner. Same rules: a dictionary probe on a
// timer, nothing blocking, loud on both outcomes, self-destructs when it is done.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Polls the enemy residency cache for a RuntimeAnimatorController and binds it to an Animator
    /// that spawned without one. Self-destructs on success, on proof the controller is missing, or
    /// once it has waited past <see cref="GiveUpSeconds"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAnimatorLateBinder : MonoBehaviour
    {
        /// <summary>Seconds between residency probes (a dictionary lookup).</summary>
        public const float PollSeconds = 0.5f;

        /// <summary>How long to keep hoping before reporting once and stopping.</summary>
        public const float GiveUpSeconds = 120f;

        private Animator _animator;
        private string _model;
        private string _ctrlName;
        /// <summary>The family token the prewarm was issued for — recorded so the timeout line can
        /// name the family that failed to land, not just the controller (WO-1303).</summary>
        private string _family;
        private float _armedAt;
        private float _nextPollAt;

        /// <summary>Arm a late controller bind on <paramref name="animator"/>. Idempotent.
        /// No-op when the controller is already proven missing — there is nothing to wait for.</summary>
        internal static void Arm(Animator animator, string modelName, string ctrlName)
        {
            if (animator == null || string.IsNullOrEmpty(ctrlName)) return;

            string address = EnemyAssetLoader.EnemyAddrPrefix + ctrlName;
            if (EnemyContentWarmer.IsKnownAbsent<RuntimeAnimatorController>(address)) return;
            if (EnemyContentWarmer.IsSettled && !EnemyContentWarmer.IsRegisteredAddress(address)) return;

            var binder = animator.gameObject.GetComponent<EnemyAnimatorLateBinder>();
            if (binder == null) binder = animator.gameObject.AddComponent<EnemyAnimatorLateBinder>();
            binder._animator = animator;
            binder._model = modelName;
            binder._ctrlName = ctrlName;
            binder._armedAt = Time.realtimeSinceStartup;
            binder._nextPollAt = 0f;

            // ⛔ PREWARM BY THE **MODEL**, NEVER BY THE CONTROLLER (WO-1303).
            // PrewarmFamily's parameter is a model slug or a full "Enemies/..." address: it feeds
            // EnemyContentWarmer.FamilyOf, which takes the text before the first '_'. A CONTROLLER
            // name has no '_' to cut at, so 'SkeletonHumanoid' became the whole family and asked
            // Addressables for the label 'enemyfam-skeletonhumanoid', which has no location — four
            // InvalidKeyExceptions in the owner's 2026-09-02 session (seq 4359/4369/4377/4639), one
            // per spawn, for a family pre-fetch that then never happened. The two other call sites
            // (EnemyFactory.PrewarmForIds, EnemyLateSkinner.Arm) both pass the model; this was the
            // outlier. Still fire-and-forget — see the header on why nothing here may wait.
            string family = EnemyContentWarmer.FamilyOf(modelName);
            if (!string.IsNullOrEmpty(modelName)) EnemyAssetLoader.PrewarmFamily(modelName);
            binder._family = family;

            FlowTrace.Once("EnemyAnim", "late-bind-armed-" + ctrlName,
                $"controller '{address}' for model '{modelName}' (family '{family}', label " +
                $"'{EnemyContentWarmer.LabelFor(family)}', declared={EnemyContentWarmer.IsDeclaredFamilyLabel(family)}) " +
                "is NOT YET DOWNLOADED — the enemy spawns and slides for now, and a late bind is armed. " +
                "Deliberately not waiting: waiting on this seam is what deadlocked the game on 2026-08-20.");
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextPollAt) return;
            _nextPollAt = now + PollSeconds;

            if (_animator == null || string.IsNullOrEmpty(_ctrlName)) { Destroy(this); return; }

            string address = EnemyAssetLoader.EnemyAddrPrefix + _ctrlName;

            if (EnemyContentWarmer.IsKnownAbsent<RuntimeAnimatorController>(address) ||
                (EnemyContentWarmer.IsSettled && !EnemyContentWarmer.IsRegisteredAddress(address)))
            {
                FlowTrace.Once("EnemyAnim", "late-bind-missing-" + _ctrlName,
                    $"late bind of controller '{address}' (model '{_model}') ABANDONED: it is GENUINELY MISSING, " +
                    "not slow. This enemy slides with no animation for the rest of the launch. Fix by shipping " +
                    "that address in the enemy Addressable group (run 'Build Animator Controllers' + " +
                    "EnemyAnimatorSetup, then re-group and upload).");
                Destroy(this);
                return;
            }

            if (!EnemyAssetLoader.IsResident<RuntimeAnimatorController>(address))
            {
                if (now - _armedAt > GiveUpSeconds)
                {
                    FlowTrace.Once("EnemyAnim", "late-bind-timeout-" + _ctrlName,
                        $"late bind of controller '{address}' has waited {now - _armedAt:F0}s " +
                        $"(catalogState={EnemyContentWarmer.State}, pending={EnemyContentWarmer.PendingRequests}, " +
                        $"family='{_family}', familyLocal={EnemyContentWarmer.IsFamilyLocal(_family)}, " +
                        $"familyDownloading={EnemyContentWarmer.IsFamilyDownloading(_family)}) — " +
                        "giving up polling. The enemy keeps sliding. Check CDN reachability for this bundle. " +
                        "Nothing hung.");
                    Destroy(this);
                }
                return;
            }

            var ctrl = EnemyAssetLoader.LoadEnemyController(_ctrlName);
            if (ctrl == null)
            {
                // The resident probe said yes and the typed fetch disagreed — should be impossible.
                // Retry, but never forever: an unbounded poll is its own kind of silent failure.
                if (now - _armedAt > GiveUpSeconds)
                {
                    FlowTrace.Once("EnemyAnim", "late-bind-contradiction-" + _ctrlName,
                        $"late bind of '{address}' ABANDONED after {now - _armedAt:F0}s: the residency probe " +
                        "reports it resident but the typed fetch keeps returning null. That contradiction is an " +
                        "asset/type mismatch (a location addressed as something other than a " +
                        "RuntimeAnimatorController), not a download problem.");
                    Destroy(this);
                }
                return;
            }

            _animator.runtimeAnimatorController = ctrl;
            FlowTrace.Step("EnemyAnim",
                $"LATE BIND OK: controller '{address}' bound to model '{_model}' " +
                $"{now - _armedAt:F1}s after spawn — the enemy stops sliding and animates from here.");
            Destroy(this);
        }
    }
}
