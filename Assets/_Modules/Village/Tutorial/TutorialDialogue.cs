// =============================================================================
// TutorialDialogue — the scripted-dialogue delivery queue for the FTUE (WO-277).
// -----------------------------------------------------------------------------
// The tutorial's narrative is delivered as a sequence of spoken lines through the
// SAME world-space speech bubble the ambient townsfolk + the StoryCompanion use
// (TownsfolkBubble). This component owns a queue of lines and a tap-to-advance
// gate so the TutorialDirector can `await` a block of dialogue and continue once
// the player has read (and tapped through) every line.
//
// ── How a line advances ──────────────────────────────────────────────────────
//   • A line shows immediately, then after a short MINIMUM HOLD it accepts a
//     screen tap / click / Space-Enter to advance to the next line. The minimum
//     hold stops the tap that ENTERED a dialogue block from instantly skipping
//     the first line (the same "first tap eats the cinematic" bug
//     StoryIntroController guards against).
//   • A failsafe auto-advance after MaxAutoHold seconds keeps the tutorial from
//     ever wedging if input is lost (e.g. focus stolen on WebGL).
//
// ── Isolation / safety ───────────────────────────────────────────────────────
//   • Lives in DeNelle.Village (can see TownsfolkBubble directly — no reflection).
//   • Every bubble call is null-guarded; with no bubble it degrades to a timed
//     auto-advance so the director never stalls off the village scene.
//   • Reads input through BOTH the new Input System (Pointer/Keyboard) AND the
//     legacy Input (mouse/touch) so it works in every build config the project
//     ships (activeInputHandler = Both).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeNelle.Village
{
    /// <summary>
    /// A tap-to-advance dialogue queue that drives a <see cref="TownsfolkBubble"/>.
    /// The <see cref="TutorialDirector"/> enqueues lines and polls
    /// <see cref="IsIdle"/> to know when a block of dialogue is finished.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialDialogue : MonoBehaviour
    {
        // Minimum seconds a line stays up before a tap can advance it — long
        // enough that the tap which opened the block can't skip line 1.
        private const float MinHold = 0.85f;
        // Hard failsafe: a line auto-advances after this long even with no input,
        // so a lost-focus / no-pointer build never wedges the tutorial.
        private const float MaxAutoHold = 9.0f;

        private TownsfolkBubble _bubble;
        private string _speaker = "";

        private readonly Queue<string> _pending = new Queue<string>();
        private bool _lineUp;
        private float _lineShownAt;
        private bool _prevPressed;   // edge-detect so a held press advances once

        /// <summary>True when no line is showing and the queue is drained.</summary>
        public bool IsIdle => !_lineUp && _pending.Count == 0;

        /// <summary>True while a line is on screen or queued.</summary>
        public bool IsSpeaking => !IsIdle;

        /// <summary>Assigns the speech bubble the dialogue drives (the director wires this).</summary>
        public void SetBubble(TownsfolkBubble bubble) => _bubble = bubble;

        /// <summary>Sets the speaker name shown as the bubble's attribution line.</summary>
        public void SetSpeaker(string speaker) => _speaker = speaker ?? "";

        /// <summary>Queues one line. It shows once the lines ahead of it are tapped through.</summary>
        public void Enqueue(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _pending.Enqueue(line);
        }

        /// <summary>Queues several lines in order.</summary>
        public void Enqueue(IEnumerable<string> lines)
        {
            if (lines == null) return;
            foreach (var l in lines) Enqueue(l);
        }

        /// <summary>
        /// Queues several lines with a one-off speaker name (sets the speaker first).
        /// Convenience for the director's per-scene narration blocks.
        /// </summary>
        public void Say(string speaker, params string[] lines)
        {
            SetSpeaker(speaker);
            if (lines != null)
                foreach (var l in lines) Enqueue(l);
        }

        /// <summary>Clears any queued + on-screen dialogue (e.g. a tutorial skip).</summary>
        public void Clear()
        {
            _pending.Clear();
            _lineUp = false;
            _bubble?.Hide();
        }

        private void Update()
        {
            // Nothing showing → pull the next queued line if there is one.
            if (!_lineUp)
            {
                if (_pending.Count == 0) return;
                ShowNext();
                return;
            }

            float elapsed = Time.unscaledTime - _lineShownAt;

            // Advance on a fresh tap (after the min-hold grace) or the auto failsafe.
            bool advance = (elapsed >= MinHold && ConsumeTap()) || elapsed >= MaxAutoHold;
            if (!advance) return;

            if (_pending.Count > 0)
            {
                ShowNext();
            }
            else
            {
                // Last line tapped through — close the block; IsIdle flips true.
                _lineUp = false;
                _bubble?.Hide();
            }
        }

        private void ShowNext()
        {
            string line = _pending.Dequeue();
            _bubble?.Show(_speaker, line);
            _lineUp = true;
            _lineShownAt = Time.unscaledTime;
        }

        // Returns true on the rising edge of a tap/click/confirm-key this frame.
        // Reads new-Input-System Pointer/Keyboard AND legacy Input so it works in
        // every build config (activeInputHandler = Both).
        private bool ConsumeTap()
        {
            bool pressed = false;

            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed) pressed = true;

            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed)) pressed = true;

            // Legacy fallback (activeInputHandler = Both).
            if (!pressed)
            {
                if (UnityEngine.Input.GetMouseButton(0)) pressed = true;
                else if (UnityEngine.Input.touchCount > 0) pressed = true;
            }

            bool edge = pressed && !_prevPressed;
            _prevPressed = pressed;
            return edge;
        }
    }
}
