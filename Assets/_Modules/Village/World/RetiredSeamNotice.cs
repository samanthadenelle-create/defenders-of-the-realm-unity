// =============================================================================
// RetiredSeamNotice -- the honest "this door is shut, and here is why" affordance
// for a seam that CavePortalRepointInjector has NEUTRALIZED.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World
//
// TICKET (owner F8 seq 645, 2026-08-02): "yes, good, this is deactivated (outpost)
// as it is still broken (We could add something about update coming)".
// The owner APPROVES that the walk-up outpost seam is disabled (ff.raidwalk OFF,
// WO-771: the raid loop is Teleport/Deploy) -- but a player who walks up to the
// baked overworld cave currently gets SILENCE: no prompt, no message, a dead hole
// in the world that reads as a bug. This component replaces that silence with a
// short, honest, ASCII line.
//
// WHAT IT REUSES (deliberately NO new UI was invented):
//   * DeNelle.Village.MobileInteractButton -- the project's ONE interact path
//     (there is no IInteractable interface). Request() is a PER-FRAME claim, so we
//     re-request every frame while the hero is in range, exactly like
//     DungeonExitInteractable / MineNode / DungeonPortal.
//   * DeNelle.Village.BuildFeedbackToast.ShowInfo(msg, seconds) -- the established
//     NEUTRAL one-liner toast (no denied buzz, caller-chosen lifetime). It is a
//     standalone code-built uGUI canvas with no build-mode coupling, so it is the
//     existing toast path rather than a bespoke panel.
//
// STATUS READS BY TEXT, NEVER BY COLOUR (the owner is red/green colourblind): the
// button label itself states "Outpost Sealed"; the toast states the reason.
//
// RUNTIME-ONLY, like its installer: this component is ADDED at runtime by
// CavePortalRepointInjector.NeutralizeOutpostTriggers. Main_Castle_Overworld is
// NEVER hand-edited (CLAUDE.md sec.3 -- resave-corruption history), so nothing here
// is serialized into a scene. ff.raidwalk ON => the injector takes the repoint
// branch and never installs this, so the walk-up entry behaves exactly as before.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    /// <summary>
    /// Proximity notice hung on a seam that has been neutralized: shows the shared
    /// interact prompt reading "Outpost Sealed" when the hero walks up, and pops the
    /// neutral info toast explaining that it reopens in a future update. Purely
    /// informational -- it never routes, never loads a scene, never re-enables the seam.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetiredSeamNotice : MonoBehaviour
    {
        private const string Sys = "Seam";

        // Player-facing copy. ASCII only (a tofu scan fails non-ASCII) and NO date --
        // "a future update" is the honest promise; we do not imply when.
        private const string PromptLabel = "Outpost Sealed";
        private const string NoticeText = "Outpost sealed -- reopening in a future update";

        // Cheap: a poll on an interval, mirroring DungeonExitInteractable.CheckInterval,
        // not per-frame distance math.
        private const float CheckInterval = 0.2f;
        private const float NoticeSeconds = 3.6f;      // toast lifetime
        private const float MinRadius = 5f;            // walk-up reach floor
        private const float MaxRadius = 14f;           // never a 40m "why is this shouting at me" zone
        private const float AutoNoticeCooldown = 60f;  // re-approach spam guard

        private float _radius = 8f;
        private string _destination = "";

        private Transform _hero;
        private bool _heroFound;
        private bool _isInRange;
        private float _nextProximityCheck;
        private float _promptMuteUntil;     // hide the button while the toast is being read
        private float _nextAutoNoticeAt;    // earliest unscaled time an approach may auto-toast

        /// <summary>
        /// Idempotently attach the notice to <paramref name="host"/> (the neutralized seam's
        /// GameObject). Safe to call on every scene load / every injector pass -- an existing
        /// notice is refreshed in place, never duplicated. Returns the component, or null if
        /// the host is gone.
        /// </summary>
        public static RetiredSeamNotice Install(GameObject host, float promptRadius, string destination)
        {
            if (host == null) return null;
            return Guard.Try(Sys, "install retired-seam notice", () =>
            {
                var notice = host.GetComponent<RetiredSeamNotice>();
                bool fresh = notice == null;
                if (fresh) notice = host.AddComponent<RetiredSeamNotice>();
                notice.enabled = true;   // re-arm one disabled by an ff.raidwalk ON pass
                notice._radius = Mathf.Clamp(promptRadius <= 0f ? 8f : promptRadius, MinRadius, MaxRadius);
                notice._destination = string.IsNullOrEmpty(destination) ? "(unset)" : destination;
                if (fresh)
                {
                    FlowTrace.Step(Sys,
                        $"RetiredSeamNotice armed on '{host.name}' -> '{notice._destination}' " +
                        $"(prompt r={notice._radius:F1}m, copy=\"{NoticeText}\")");
                }
                return notice;
            }, null);
        }

        private void ResolveHero()
        {
            if (_heroFound) return;
            // Player tag first (canon sec.7) -- independent of HeroLocomotion's enabled state;
            // fall back to the locomotion component only if the tag is somehow unset.
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) { _hero = tagged.transform; _heroFound = true; return; }
            var loco = Object.FindAnyObjectByType<HeroLocomotion>();
            if (loco != null) { _hero = loco.transform; _heroFound = true; }
        }

        private void Update()
        {
            if (!_heroFound) { ResolveHero(); if (!_heroFound) return; }
            // The hero rig can be replaced (body-swap) after we cached it -- re-resolve rather
            // than dereferencing a destroyed Transform (DungeonPortal DEF-40 lesson).
            if (_hero == null) { _heroFound = false; _isInRange = false; return; }

            // Build/authoring mode (and any modal panel, which Request() itself ignores):
            // drop the claim and stay quiet.
            if (MobileInteractButton.Suppressed)
            {
                MobileInteractButton.Release(this);
                return;
            }

            if (Time.time >= _nextProximityCheck)
            {
                _nextProximityCheck = Time.time + CheckInterval;
                bool wasInRange = _isInRange;
                float distSqr = (_hero.position - transform.position).sqrMagnitude;
                _isInRange = distSqr <= _radius * _radius;

                // On the APPROACH edge, deliver the reason once even if the player never taps
                // (a dead portal must not be silent). Cooldown-guarded so pacing back and forth
                // cannot spam the toast.
                if (_isInRange && !wasInRange)
                {
                    FlowTrace.Step(Sys,
                        $"hero reached RETIRED seam '{name}' -> '{_destination}' (sealed; prompt armed)");
                    if (Time.unscaledTime >= _nextAutoNoticeAt) ShowNotice(auto: true);
                }
            }

            // Request() is a PER-FRAME claim -- re-issue it every frame in range. While the
            // toast is up we deliberately stand down so the button cannot overlap the message.
            if (_isInRange && Time.unscaledTime >= _promptMuteUntil)
                MobileInteractButton.Request(this, PromptLabel, OnTap);
            else
                MobileInteractButton.Release(this);
        }

        private void OnTap() => ShowNotice(auto: false);

        private void ShowNotice(bool auto)
        {
            _nextAutoNoticeAt = Time.unscaledTime + AutoNoticeCooldown;
            _promptMuteUntil = Time.unscaledTime + NoticeSeconds;
            // Never let a missing/failed toast path throw out of Update -- log and carry on.
            bool shown = Guard.Try(Sys, "show retired-seam notice toast",
                () => BuildFeedbackToast.ShowInfo(NoticeText, NoticeSeconds));
            if (shown)
                FlowTrace.Step(Sys,
                    $"retired-seam notice shown ({(auto ? "approach" : "tap")}) on '{name}' -> " +
                    $"'{_destination}': \"{NoticeText}\"");
            else
                FlowTrace.Warn(Sys,
                    $"retired-seam notice toast FAILED on '{name}' -> '{_destination}' " +
                    "(player got no explanation for the sealed seam)");
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
            _isInRange = false;
        }
    }
}
