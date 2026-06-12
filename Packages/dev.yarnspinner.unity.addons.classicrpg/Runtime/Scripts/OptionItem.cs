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
            if (_submitted) { return; }
            _submitted = true;
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