// =============================================================================
// HeroEmote — play a one-shot EMOTE clip (KnightV3's custom DANCE) on the live hero,
// then hand control back to the locomotion controller (owner "try this" 2026-07-03).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The owner's KnightV3.fbx ships an embedded custom DANCE clip. KnightV3EmoteExtractor
// (editor) copies the FBX's embedded clips out to Resources/Heroes/Emotes/KnightV3_*.anim so
// they are runtime-loadable (a sub-asset clip inside an FBX is NOT Resources.Load-able alone).
// This component plays one of those clips as a ONE-SHOT over the hero's Animator via a small
// PlayableGraph, then destroys the graph and restores the locomotion controller — so the dance
// is a transient flourish, not a state you get stuck in.
//
// SCOPE (coordinator: "don't over-build the emote system"): this is the trigger HOOK only —
// no emote wheel / HUD button / cooldown. Wire it from wherever the emote should fire, e.g.:
//   • a HUD emote button           -> HeroEmote.PlayDance(heroRoot)
//   • the victory flow             -> HeroEmote.PlayDance(heroRoot)   // victory flourish
//   • a town idle timer            -> HeroEmote.PlayDance(heroRoot)   // idle flourish
// "heroRoot" is the tagged Player root (same object HeroBodySwapper builds "HeroBody" under).
// =============================================================================

using DeNelle.Core.Diagnostics; // FlowTrace / Guard — §12: instrument the emote play/restore
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DeNelle.Village
{
    /// <summary>
    /// Plays a one-shot emote AnimationClip (e.g. KnightV3's custom dance) on the hero's Animator
    /// via a transient PlayableGraph, then restores the locomotion controller. Attach-on-demand;
    /// the static <see cref="PlayDance"/> resolves the hero animator + the extracted dance clip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroEmote : MonoBehaviour
    {
        // Resources sub-folder the editor extractor writes the standalone emote clips into.
        private const string EmoteResourceFolder = "Heroes/Emotes";

        private PlayableGraph _graph;
        private Animator _anim;
        private RuntimeAnimatorController _savedController;
        private bool _playing;
        private float _endTime;

        /// <summary>
        /// Convenience trigger: find the hero's Animator (on the "HeroBody" child, else in children),
        /// load the extracted DANCE clip from Resources/Heroes/Emotes, and play it one-shot. No-op with a
        /// FlowTrace if the animator or the clip is missing (e.g. clips not yet extracted) — never throws.
        /// </summary>
        public static void PlayDance(GameObject heroRoot)
        {
            if (heroRoot == null) return;
            Animator anim = null;
            var body = heroRoot.transform.Find("HeroBody");
            if (body != null) anim = body.GetComponentInChildren<Animator>();
            if (anim == null) anim = heroRoot.GetComponentInChildren<Animator>();
            if (anim == null)
            {
                FlowTrace.Warn("HeroEmote", "PlayDance: no Animator found under the hero — emote skipped.");
                return;
            }

            AnimationClip dance = ResolveEmoteClip("dance");
            if (dance == null)
            {
                FlowTrace.Warn("HeroEmote",
                    "PlayDance: no dance clip at Resources/Heroes/Emotes (run Defenders → Heroes → " +
                    "Extract KnightV3 Emote Clips first) — emote skipped.");
                return;
            }

            var emote = heroRoot.GetComponent<HeroEmote>();
            if (emote == null) emote = heroRoot.AddComponent<HeroEmote>();
            emote.Play(anim, dance);
        }

        /// <summary>
        /// Load the extracted emote clip whose name contains <paramref name="keyword"/> (e.g. "dance" or
        /// "walk") from Resources/Heroes/Emotes; falls back to the first emote clip found. Null if none.
        /// </summary>
        public static AnimationClip ResolveEmoteClip(string keyword)
        {
            var clips = Resources.LoadAll<AnimationClip>(EmoteResourceFolder);
            if (clips == null || clips.Length == 0) return null;
            if (!string.IsNullOrEmpty(keyword))
            {
                string kw = keyword.ToLowerInvariant();
                foreach (var c in clips)
                    if (c != null && c.name.ToLowerInvariant().Contains(kw)) return c;
            }
            return clips[0];
        }

        /// <summary>
        /// Play <paramref name="clip"/> one-shot over <paramref name="anim"/> via a transient
        /// PlayableGraph. The current controller is saved and restored when the clip's length elapses.
        /// Re-calling while an emote is playing restarts it. Guarded — a build failure logs and no-ops.
        /// </summary>
        public void Play(Animator anim, AnimationClip clip)
        {
            if (anim == null || clip == null) return;

            // Tear down any in-flight emote first (restart), keeping the ORIGINAL saved controller.
            if (_playing) TeardownGraph(restoreController: false);

            _anim = anim;
            if (_savedController == null) _savedController = anim.runtimeAnimatorController;

            bool ok = false;
            Guard.Try("HeroEmote", $"build emote graph '{clip.name}'", () =>
            {
                _graph = PlayableGraph.Create("HeroEmote");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                var output = AnimationPlayableOutput.Create(_graph, "EmoteOut", anim);
                var clipPlayable = AnimationClipPlayable.Create(_graph, clip);
                clipPlayable.SetApplyFootIK(false);
                output.SetSourcePlayable(clipPlayable);
                _graph.Play();
                ok = true;
            });
            if (!ok)
            {
                FlowTrace.Fail("HeroEmote", $"failed to build the emote graph for '{clip.name}' — emote skipped.");
                return;
            }

            _playing = true;
            // One-shot: restore after one clip length (scaled by the animator's speed so a slowed hero
            // still gets the full flourish). Looping clips play a single pass then hand back to locomotion.
            float speed = Mathf.Approximately(anim.speed, 0f) ? 1f : Mathf.Abs(anim.speed);
            _endTime = Time.time + (clip.length / speed);
            FlowTrace.Step("HeroEmote", $"playing emote '{clip.name}' ({clip.length:0.00}s) one-shot on '{anim.name}'.");
        }

        private void Update()
        {
            if (_playing && Time.time >= _endTime)
                TeardownGraph(restoreController: true);
        }

        private void TeardownGraph(bool restoreController)
        {
            _playing = false;
            if (_graph.IsValid()) _graph.Destroy();
            if (restoreController && _anim != null && _savedController != null)
            {
                _anim.runtimeAnimatorController = _savedController;
                _anim.Rebind();
                FlowTrace.Step("HeroEmote", "emote finished — restored locomotion controller.");
                _savedController = null;
            }
        }

        private void OnDisable()  => TeardownGraph(restoreController: true);
        private void OnDestroy()  => TeardownGraph(restoreController: true);
    }
}
