using UnityEngine;
using TMPro;
using Yarn.Unity.Attributes;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System;

#pragma warning disable CS8618

#nullable enable

namespace Yarn.Unity.Addons.ClassicRPG
{
    // ROOT-CAUSE UX FIX (dialogue option selection felt clunky / mode-inconsistent):
    // ----------------------------------------------------------------------------
    // Previously this was a bare Selectable that ONLY implemented ISubmitHandler.
    // Consequences the owner felt in play:
    //   * MOUSE CLICK did nothing — there was no IPointerClickHandler, so an option
    //     could only be chosen via the EventSystem "Submit" event (keyboard).
    //   * SPACE did nothing — under the project's InputSystemUIInputModule the
    //     default "Submit" action binds Enter / gamepad-A but NOT Space (the legacy
    //     StandaloneInputModule used to include Space; the Input System default does
    //     not). So Submit only fired on Enter.
    //   * HOVER + KEYBOARD DIVERGED — hovering an option showed Selectable's hover
    //     tint but never moved the EventSystem's *selected* object, so Enter still
    //     acted on the arrow-key selection, not the hovered one ("back to the mouse"
    //     friction).
    // Fix, additive (the existing onSubmit/onSelected path the presenter wires is
    // untouched):
    //   * IPointerClickHandler  -> mouse click submits the option directly.
    //   * IPointerEnterHandler   -> hovering sets the EventSystem selection to this
    //     option, unifying hover with keyboard so Enter/Space act on what's hovered.
    //   * Update()               -> when this option is the selected one, Space (and,
    //     as a legacy fallback, Return) also submits it.
    public class OptionItem : Selectable, ISelectHandler, IDeselectHandler, ISubmitHandler, IPointerClickHandler, IPointerEnterHandler
    {
        [SerializeField, MustNotBeNull] GameObject selectionIcon;
        [SerializeField, MustNotBeNull] TMP_Text text;

        public Action? onSubmit = null;
        public Action? onSelected = null;

        // Guard so a single activation (e.g. holding Space, or a click that also
        // raises Submit) only fires the choice once.
        private bool _submitted = false;

        // RUNAWAY-RECURSION FIX (crash: thousands of repeated OptionItem.Submit /
        // OptionItem.Update frames freeze the game on an options-after-options
        // dialogue):
        // ----------------------------------------------------------------------
        // onSubmit -> completion.TrySetResult resumes the presenter's
        // `await completion.Task` SYNCHRONOUSLY INLINE (UniTask/YarnTask
        // continuations run on the current call stack, not a scheduler). So the
        // dialogue advances and RE-PRESENTS a fresh set of options WITHIN this
        // same Submit() / Update() call frame. The new first OptionItem is
        // auto-selected, and because `Keyboard.wasPressedThisFrame` stays true
        // for the WHOLE frame, that brand-new instance immediately submits again
        // -> next options -> ... unbounded recursion until the stack blows.
        //
        // The per-INSTANCE `_submitted` flag cannot stop this: every cascade
        // level is a DIFFERENT, freshly-instantiated OptionItem with
        // `_submitted == false`. The guard must be GLOBAL to the option system.
        //
        // s_submitInFlight: set the instant ANY option begins submitting, and
        // only cleared when a NEW options view is freshly built
        // (RPGDialoguePresenter.RunOptionsAsync -> OptionItem.BeginOptionsView()).
        // While a submit's continuation is unwinding, NO OptionItem (old or new)
        // may submit, so the re-entrant cascade is ignored.
        //
        // s_lastSubmitFrame: a same-frame backstop. Even once the next options
        // view legitimately resets s_submitInFlight, the still-held Space key
        // reads as pressed for the rest of THIS frame; refusing a second submit
        // within the same frame index prevents the immediate re-trigger while
        // leaving the very next frame free for a genuine new keypress.
        private static bool s_submitInFlight = false;
        private static int s_lastSubmitFrame = -1;

        /// <summary>
        /// Called by the presenter when a fresh options view is shown, before any
        /// option can be chosen. Clears the global in-flight submit guard so the
        /// new option set can accept exactly one submit. Re-entrant submits that
        /// arrive while the previous selection's continuation is still unwinding
        /// are ignored because this has not yet been called for the new view.
        /// </summary>
        public static void BeginOptionsView()
        {
            s_submitInFlight = false;
        }

        public bool Selected { get => selectionIcon.activeInHierarchy; set => selectionIcon.SetActive(value); }
        public string Text { get => text.text; set => text.text = value; }

        public TMP_Text TextView => text;

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            this.Selected = false;
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            this.Selected = true;
            onSelected?.Invoke();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Submit();
        }

        // Mouse click selects/submits the option directly — no keyboard needed.
        public void OnPointerClick(PointerEventData eventData)
        {
            Submit();
        }

        // Hovering an option makes it the EventSystem-selected option, so keyboard
        // Submit (Enter/Space) acts on the hovered option — mouse and keyboard unify
        // instead of diverging.
        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (EventSystem.current != null && !_submitted
                && EventSystem.current.currentSelectedGameObject != gameObject)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        // The InputSystem default "Submit" action does not include Space, so we add
        // Space (and a Return fallback) here for whichever option is currently
        // selected/highlighted. Runs only while this is the selected object.
        private void Update()
        {
            if (_submitted) { return; }
            if (EventSystem.current == null) { return; }
            if (EventSystem.current.currentSelectedGameObject != gameObject) { return; }

            if (SpaceOrReturnPressedThisFrame())
            {
                Submit();
            }
        }

        private void Submit()
        {
            // Per-instance guard: this option already fired.
            if (_submitted) { return; }

            // GLOBAL guard: a submit is already being processed (its inline
            // continuation may be re-presenting options on this very call stack).
            // Refuse so the new option set's auto-selected item cannot cascade.
            if (s_submitInFlight) { return; }

            // Same-frame backstop: a single held Space must not satisfy two
            // option views in the same frame.
            if (s_lastSubmitFrame == Time.frameCount) { return; }

            _submitted = true;
            s_submitInFlight = true;
            s_lastSubmitFrame = Time.frameCount;

            // onSubmit -> completion.TrySetResult resumes the presenter inline.
            // s_submitInFlight stays true for the whole synchronous unwind and is
            // only reset by BeginOptionsView() when a genuinely new options view
            // is built, so any re-entrant submit during the unwind is ignored.
            onSubmit?.Invoke();
        }

        private static bool SpaceOrReturnPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                return kb.spaceKey.wasPressedThisFrame
                    || kb.enterKey.wasPressedThisFrame
                    || kb.numpadEnterKey.wasPressedThisFrame;
            }
            return false;
#else
            return Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
        }
    }
}