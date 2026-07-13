// =============================================================================
// SeatingEditorOverlay — WO-577 (Offset Forge slice 2): the IN-GAME seating editor.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// WHAT THIS IS:
//   The runtime parallel of the editor-only Offset Forge window (Tools > Offset
//   Forge). The owner asked for the SAME capability IN THE RUNNING GAME — a live,
//   on-screen seating editor (the felt parity target is the Build Menu's "Orient"
//   live-adjust) to dial in weapon / shield attachment offsets BY EYE on the actual
//   equipped hero, and PERSIST them so item attachments are always correct.
//
// HOW IT WORKS (parity with the build-menu Orient flow):
//   • Opened from the live dev tools (AdminOverlay → "Seating Editor (gear)").
//   • Finds the hero's EquipmentController, selects the equipped main-hand weapon or
//     off-hand shield, and drives its grip-root transform LIVE via the controller's
//     seating-editor API — what-you-see-is-what-you-save (the preview mirrors the
//     exact attach math).
//   • On-screen steppers (−−/−/+/++) + sliders for Rotation X/Y/Z, Position X/Y/Z and
//     uniform Scale. A bottom bar pairs Save / Done like the build bar's Orient/Done.
//   • BASELINE = the owner's "100% vertical" convention: the weapon stands straight up
//     (longest axis → +Y, hilt on the lower half) and the owner nudges from there into
//     the in-hand pose; the saved offset is the DELTA from vertical (fullOverride mode).
//     "Reset to Vertical" returns to that clean baseline.
//   • Save writes offsets.json via AttachmentOffsetRegistry (writable dev file in a
//     build + the repo file in the Editor) and logs a copy-pasteable JSON snippet so
//     the owner can bake it back into the repo offsets.json from a build.
//
// PROCEDURAL UIElements ONLY — NO UXML (CLAUDE.md §8). Adopts a scene UIDocument's
// PanelSettings so a code-built panel actually renders (the documented empty-UI trap).
// DEV-ONLY: launched from the owner dev tools; not exposed to normal players.
// =============================================================================

using System;
using System.Globalization;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Village.UI
{
    [DisallowMultipleComponent]
    public sealed class SeatingEditorOverlay : MonoBehaviour
    {
        // ── Theme (shared dark-glass + gold, like TowerPlacementRotateMenu) ──────────
        private static readonly Color PanelBg    = ElarionUiKit.Glass;
        private static readonly Color RuneBorder = ElarionUiKit.Accent;
        private static readonly Color TitleGold  = ElarionUi.Gilt;
        private static readonly Color ReadoutBg  = ElarionUiKit.Track;
        private static readonly Color ReadoutBdr = ElarionUiKit.AccentSoft;
        private static readonly Color ConfirmBg  = ElarionUi.GoldButton;
        private static readonly Color ConfirmBdr = ElarionUi.Gilt;
        private static readonly Color ConfirmTxt = ElarionUi.Ink;
        private static readonly Color SecBg      = ElarionUiKit.GlassDeep;
        private static readonly Color SecTxt     = ElarionUi.ParchmentDim;

        private static readonly Color AxisX = Hex(0xd0, 0x40, 0x40);
        private static readonly Color AxisY = Hex(0x38, 0xb8, 0x38);
        private static readonly Color AxisZ = Hex(0x38, 0x78, 0xc0);
        private static readonly Color AxisS = ElarionUi.Gilt;

        // ── State ───────────────────────────────────────────────────────────────────
        public bool IsOpen { get; private set; }

        // Ticket #1 (2026-07-07): register with the PanelManager modal arbiter so (a) the
        // softlock watchdog knows a modal owns the screen (owner parked mid-dial fired
        // "possible_softlock" twice in one session) and (b) the editor obeys one-modal-at-a-time.
        // BattleAllowed: it is an owner tool that must stay openable during a battle (drawn dial).
        private PanelHandle _panelHandle;

        private UIDocument    _document;
        private VisualElement _root;
        private VisualElement _panel;

        private EquipmentController _eq;
        private EquipmentController _injected;   // when set, edit THIS controller (e.g. the Gear preview) instead of the world hero
        private bool    _offHand;
        private bool    _sheathed;   // 2026-07-07: carry mode — false = Drawn (in-hand), true = Sheathed (back pose, "@sheathed" key)
        private Vector3 _pos;
        private Vector3 _euler;
        private float   _scale = 1f;
        private bool    _fullOverride = true;
        private string  _offsetKey;

        private Label  _status;
        private Label  _targetLabel;
        private Button _mainBtn, _offBtn, _fullBtn;
        private Button _drawnBtn, _sheathedBtn;
        private VisualElement _body;

        // ── Launch (reflection-friendly entry for AdminOverlay dev tools) ─────────────
        /// <summary>Find-or-create the overlay and open it on the live hero. Returns the instance.</summary>
        public static SeatingEditorOverlay Launch()
        {
            var existing = FindOrCreate();
            existing._injected = null;           // world-hero mode (AdminOverlay dev tools)
            existing.Open();
            return existing;
        }

        /// <summary>Open the seating editor on a SPECIFIC EquipmentController — used by the
        /// Gear screen to orient the weapon shown in its 3D preview (parity with the build
        /// menu's model-select Orient). Distinct name so AdminOverlay's reflection
        /// <c>GetMethod("Launch")</c> stays unambiguous.</summary>
        public static SeatingEditorOverlay LaunchFor(EquipmentController target)
        {
            var existing = FindOrCreate();
            existing._injected = target;
            existing.Open();
            return existing;
        }

        private static SeatingEditorOverlay FindOrCreate()
        {
            var existing = FindAnyObjectByType<SeatingEditorOverlay>();
            if (existing == null)
            {
                var go = new GameObject("SeatingEditorOverlay");
                existing = go.AddComponent<SeatingEditorOverlay>();
            }
            return existing;
        }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            AdoptPanelSettings();
        }

        private void OnDisable() { CloseInternal(false); }

        // ── Open / Close ──────────────────────────────────────────────────────────────
        public void Open()
        {
            _eq = _injected != null ? _injected : ResolveHeroEquipment();
            if (_eq == null)
            {
                Debug.LogWarning("[Seating] no EquipmentController (injected or world hero) — cannot open seating editor.");
                return;
            }

            // Prefer the main-hand weapon; fall back to the off-hand if only a shield is equipped.
            _offHand = false;
            // Town/hub = sheathed on back (what the owner sees); combat = drawn in-hand.
            _sheathed = !_eq.CombatActive;
            if (!_eq.HasSeatingTarget(false) && _eq.HasSeatingTarget(true)) _offHand = true;

            if (!BeginEdit(_offHand))
            {
                Debug.LogWarning("[Seating] hero has no equipped weapon or off-hand to edit.");
                return;
            }

            BuildPanel();
            Show();
        }

        public void Close() => CloseInternal(true);

        private void CloseInternal(bool endEdit)
        {
            if (endEdit && _eq != null) _eq.EndSeatingEdit();
            IsOpen = false;
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (_document != null) _document.enabled = false;
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
        }

        // Begin/refresh the edit session on the requested slot; seed the local state from the
        // controller (which seeds from any saved offset). Returns false when the slot is empty.
        private bool BeginEdit(bool offHand)
        {
            if (_eq == null) return false;
            if (!_eq.BeginSeatingEdit(offHand, _sheathed, out var info) || !info.valid) return false;
            _offHand      = info.offHand;
            _offsetKey    = info.offsetKey;
            _pos          = info.pos;
            _euler        = info.euler;
            _scale        = info.scale > 0f ? info.scale : 1f;
            _fullOverride = info.fullOverride;
            return true;
        }

        private void Apply()
        {
            _eq?.ApplySeatingPreview(_pos, _euler, _scale, _fullOverride);
        }

        // ── Panel construction ─────────────────────────────────────────────────────────
        private void BuildPanel()
        {
            if (_document == null) return;
            _document.enabled = true;
            AdoptPanelSettings();
            var rootVE = _document.rootVisualElement;
            if (rootVE == null)
            {
                Debug.LogWarning("[Seating] rootVisualElement null (no PanelSettings adopted) — aborting BuildPanel.");
                return;
            }
            rootVE.Clear();

            // Transparent full-screen root that lets world input through; only the side panel
            // is pickable, so the hero stays visible + the camera/editor still work behind it.
            _root = new VisualElement();
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0; _root.style.top = 0; _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;

            _panel = new VisualElement();
            _panel.style.position = Position.Absolute;
            _panel.style.right = 12; _panel.style.top = 12; _panel.style.bottom = 12;
            _panel.style.width = 360;
            _panel.style.backgroundColor = PanelBg;
            SetBorder(_panel, RuneBorder, 2);
            SetRadius(_panel, 10);
            _panel.style.paddingTop = 10; _panel.style.paddingBottom = 10;
            _panel.style.paddingLeft = 12; _panel.style.paddingRight = 12;
            _panel.pickingMode = PickingMode.Position;

            _panel.Add(BuildHeader());
            _panel.Add(BuildTargetRow());

            _body = new VisualElement();
            _panel.Add(_body);
            RebuildBody();

            _root.Add(_panel);
            rootVE.Add(_root);
        }

        private VisualElement BuildHeader()
        {
            var col = new VisualElement();
            col.style.marginBottom = 8;

            var title = new Label("⚒  SEATING EDITOR");
            title.style.fontSize = 15;
            title.style.color = TitleGold;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 2;
            col.Add(title);

            var sub = new Label("Orient the equipped gear live — dial from 100% vertical.");
            sub.style.fontSize = 10;
            sub.style.color = SecTxt;
            sub.style.whiteSpace = WhiteSpace.Normal;
            sub.style.marginTop = 2;
            col.Add(sub);
            return col;
        }

        private VisualElement BuildTargetRow()
        {
            var col = new VisualElement();
            col.style.marginBottom = 6;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            _mainBtn = MakeToggle("Main Weapon", !_offHand, () => SwitchTarget(false));
            _offBtn  = MakeToggle("Off-hand",     _offHand, () => SwitchTarget(true));
            _mainBtn.style.marginRight = 6;
            row.Add(_mainBtn);
            row.Add(_offBtn);
            col.Add(row);

            // Carry-mode toggle (2026-07-07): Drawn edits the in-hand seat; Sheathed edits the
            // BACK pose (saved under "<key>@sheathed") — the town carry the owner actually sees.
            var carry = new VisualElement();
            carry.style.flexDirection = FlexDirection.Row;
            carry.style.alignItems = Align.Center;
            carry.style.marginBottom = 4;
            _drawnBtn    = MakeToggle("Drawn (hand)",    !_sheathed, () => SwitchCarry(false));
            _sheathedBtn = MakeToggle("Sheathed (back)",  _sheathed, () => SwitchCarry(true));
            _drawnBtn.style.marginRight = 6;
            carry.Add(_drawnBtn);
            carry.Add(_sheathedBtn);
            col.Add(carry);

            _targetLabel = new Label(TargetText());
            _targetLabel.style.fontSize = 10;
            _targetLabel.style.color = TitleGold;
            _targetLabel.style.whiteSpace = WhiteSpace.Normal;
            col.Add(_targetLabel);
            return col;
        }

        private string TargetText() => _sheathed
            ? $"id: {_offsetKey ?? "<none>"}   -   mode: {(_fullOverride ? "ABSOLUTE back pose" : "NUDGE on built-in sheathe")}"
            : $"id: {_offsetKey ?? "<none>"}   -   mode: {(_fullOverride ? "VERTICAL+delta" : "NUDGE on geometry")}";

        private void RebuildBody()
        {
            if (_body == null) return;
            _body.Clear();

            // Rotation (the Orient core) — −180..180.
            _body.Add(SectionLabel("ROTATION  (Orient — degrees)"));
            _body.Add(BuildRow("Rot X", AxisX, -180f, 180f, 1f, 15f, "0",
                () => _euler.x, v => _euler.x = Wrap180(v)));
            _body.Add(BuildRow("Rot Y", AxisY, -180f, 180f, 1f, 15f, "0",
                () => _euler.y, v => _euler.y = Wrap180(v)));
            _body.Add(BuildRow("Rot Z", AxisZ, -180f, 180f, 1f, 15f, "0",
                () => _euler.z, v => _euler.z = Wrap180(v)));

            // Position — fine nudge in metres.
            _body.Add(SectionLabel("POSITION  (metres)"));
            _body.Add(BuildRow("Pos X", AxisX, -0.5f, 0.5f, 0.005f, 0.02f, "0.000",
                () => _pos.x, v => _pos.x = v));
            _body.Add(BuildRow("Pos Y", AxisY, -0.5f, 0.5f, 0.005f, 0.02f, "0.000",
                () => _pos.y, v => _pos.y = v));
            _body.Add(BuildRow("Pos Z", AxisZ, -0.5f, 0.5f, 0.005f, 0.02f, "0.000",
                () => _pos.z, v => _pos.z = v));

            // Scale — uniform multiplier. DRAWN mode only: the sheathe never owns scale (scale
            // is composed by the attach path — comp * authored — and reused on the back).
            if (!_sheathed)
            {
                _body.Add(SectionLabel("SCALE  (x uniform)"));
                _body.Add(BuildRow("Scale", AxisS, 0.1f, 5f, 0.05f, 0.25f, "0.###",
                    () => _scale, v => _scale = Mathf.Max(0.01f, v)));
            }
            else
            {
                var note = SectionLabel("SCALE — attach-owned (dial it in Drawn mode)");
                _body.Add(note);
            }

            _body.Add(BuildModeRow());
            _body.Add(BuildButtons());

            _status = new Label("Dial the pose, then Save.");
            _status.style.fontSize = 10;
            _status.style.color = SecTxt;
            _status.style.whiteSpace = WhiteSpace.Normal;
            _status.style.marginTop = 6;
            _body.Add(_status);
        }

        private VisualElement BuildModeRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8; row.style.marginBottom = 2;

            string modeText = _sheathed
                ? (_fullOverride ? "Mode: ABSOLUTE back pose" : "Mode: NUDGE on built-in")
                : (_fullOverride ? "Mode: VERTICAL + delta" : "Mode: NUDGE on geometry");
            _fullBtn = MakeToggle(modeText, true, ToggleMode);
            _fullBtn.style.flexGrow = 1;
            row.Add(_fullBtn);
            return row;
        }

        private VisualElement BuildButtons()
        {
            var col = new VisualElement();
            col.style.marginTop = 8;

            // Row 1: Reset to Vertical + Re-equip (verify from file).
            var r1 = new VisualElement();
            r1.style.flexDirection = FlexDirection.Row;
            r1.style.justifyContent = Justify.SpaceBetween;
            r1.style.marginBottom = 6;
            r1.Add(SecButton("Reset to Vertical", OnResetVertical));
            r1.Add(SecButton("Re-equip (verify)", OnReequip));
            col.Add(r1);

            // Row 2: Export JSON + Clear (remove entry).
            var r2 = new VisualElement();
            r2.style.flexDirection = FlexDirection.Row;
            r2.style.justifyContent = Justify.SpaceBetween;
            r2.style.marginBottom = 6;
            r2.Add(SecButton("Export JSON", OnExport));
            r2.Add(SecButton("Clear offset", OnClear));
            col.Add(r2);

            // Row 3 (the build-bar pairing): Save (gold CTA) + Done.
            var r3 = new VisualElement();
            r3.style.flexDirection = FlexDirection.Row;
            r3.style.justifyContent = Justify.SpaceBetween;

            var save = new Button(OnSave) { text = "Save Offset" };
            save.style.flexGrow = 1; save.style.marginRight = 6;
            save.style.backgroundColor = ConfirmBg;
            save.style.color = ConfirmTxt;
            SetBorder(save, ConfirmBdr, 1); SetRadius(save, 6);
            save.style.paddingTop = 8; save.style.paddingBottom = 8;
            save.style.unityFontStyleAndWeight = FontStyle.Bold;
            r3.Add(save);

            var done = new Button(Close) { text = "Done" };
            done.style.width = 96;
            done.style.backgroundColor = SecBg;
            done.style.color = SecTxt;
            SetBorder(done, ReadoutBdr, 1); SetRadius(done, 6);
            done.style.paddingTop = 8; done.style.paddingBottom = 8;
            r3.Add(done);

            col.Add(r3);
            return col;
        }

        // One labeled control row: label · −− · − · slider · value · + · ++
        private VisualElement BuildRow(string label, Color accent, float min, float max,
            float stepSmall, float stepBig, string fmt, Func<float> get, Action<float> set)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            var name = new Label(label);
            name.style.width = 46; name.style.fontSize = 11; name.style.color = accent;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(name);

            var slider = new Slider(min, max) { value = Mathf.Clamp(get(), min, max) };
            slider.style.flexGrow = 1; slider.style.marginLeft = 4; slider.style.marginRight = 4;
            TintSlider(slider, accent);

            var val = new Label(get().ToString(fmt, CultureInfo.InvariantCulture));
            val.style.width = 46; val.style.fontSize = 10; val.style.color = TitleGold;
            val.style.backgroundColor = ReadoutBg;
            SetBorder(val, ReadoutBdr, 1); SetRadius(val, 4);
            val.style.unityTextAlign = TextAnchor.MiddleCenter;
            val.style.paddingTop = 2; val.style.paddingBottom = 2;

            void Commit(float v)
            {
                v = Mathf.Clamp(v, min, max);
                set(v);
                float shown = get();
                slider.SetValueWithoutNotify(Mathf.Clamp(shown, min, max));
                val.text = shown.ToString(fmt, CultureInfo.InvariantCulture);
                Apply();
                Touch();
            }

            slider.RegisterValueChangedCallback(evt => Commit(evt.newValue));

            row.Add(StepBtn("--", () => Commit(get() - stepBig)));
            row.Add(StepBtn("-",  () => Commit(get() - stepSmall)));
            row.Add(slider);
            row.Add(val);
            row.Add(StepBtn("+",  () => Commit(get() + stepSmall)));
            row.Add(StepBtn("++", () => Commit(get() + stepBig)));
            return row;
        }

        // ── Actions ──────────────────────────────────────────────────────────────────
        private void SwitchTarget(bool offHand)
        {
            if (_eq == null) return;
            if (offHand && !_eq.HasSeatingTarget(true)) { SetStatus("No off-hand equipped."); return; }
            if (!offHand && !_eq.HasSeatingTarget(false)) { SetStatus("No main weapon equipped."); return; }
            if (!BeginEdit(offHand)) { SetStatus("Could not switch target."); return; }
            // Off-hand DRAWN is vertical-only (see ToggleMode) — coerce so the saved mode is
            // honoured. SHEATHED honours both modes (nudge composes on the built-in back pose).
            if (_offHand && !_sheathed) _fullOverride = true;
            if (_mainBtn != null) StyleToggle(_mainBtn, !_offHand);
            if (_offBtn  != null) StyleToggle(_offBtn,   _offHand);
            if (_targetLabel != null) _targetLabel.text = TargetText();
            RebuildBody();
            Apply();
        }

        // Carry-mode switch (2026-07-07): re-begins the edit session in the requested carry state —
        // Drawn tunes the in-hand seat (existing flow), Sheathed tunes the BACK pose (offset saved
        // under "<key>@sheathed"). Mirrors SwitchTarget's re-begin + rebuild pattern.
        private void SwitchCarry(bool sheathed)
        {
            if (_eq == null || sheathed == _sheathed) return;
            bool prev = _sheathed;
            _sheathed = sheathed;
            if (!BeginEdit(_offHand)) { _sheathed = prev; SetStatus("Could not switch carry mode."); return; }
            if (_drawnBtn != null)    StyleToggle(_drawnBtn,    !_sheathed);
            if (_sheathedBtn != null) StyleToggle(_sheathedBtn,  _sheathed);
            if (_targetLabel != null) _targetLabel.text = TargetText();
            RebuildBody();
            Apply();
            SetStatus(_sheathed
                ? "SHEATHED: dialing the on-back (town) pose — saved under the @sheathed key."
                : "DRAWN: dialing the in-hand seat.");
        }

        private void ToggleMode()
        {
            // SHEATHED honours both modes: nudge composes on the built-in back pose; fullOverride
            // is the absolute pose in the back-socket frame (see EquipmentController.ApplySheathedOffset).
            if (_sheathed)
            {
                _fullOverride = !_fullOverride;
                if (_fullBtn != null)
                    _fullBtn.text = _fullOverride ? "Mode: ABSOLUTE back pose" : "Mode: NUDGE on built-in";
                if (_targetLabel != null) _targetLabel.text = TargetText();
                Apply();
                Touch();
                SetStatus(_fullOverride
                    ? "ABSOLUTE: pos/rot ARE the back pose (socket frame, no global yaw)."
                    : "NUDGE: pos/rot add on top of the built-in sheathe pose (zero = today's pose).");
                return;
            }
            // The off-hand DRAWN runtime seat only honours the VERTICAL (fullOverride) offset — a
            // plain nudge wouldn't reproduce (its grip is a baked preset). Lock the off-hand to
            // vertical so what-you-save is always what runtime produces.
            if (_offHand)
            {
                _fullOverride = true;
                if (_fullBtn != null) _fullBtn.text = "Mode: VERTICAL + delta (off-hand locked)";
                SetStatus("Off-hand seating is VERTICAL-only (nudge would not reproduce at runtime).");
                return;
            }
            _fullOverride = !_fullOverride;
            if (_fullBtn != null)
                _fullBtn.text = _fullOverride ? "Mode: VERTICAL + delta" : "Mode: NUDGE on geometry";
            if (_targetLabel != null) _targetLabel.text = TargetText();
            Apply();
            Touch();
            SetStatus(_fullOverride
                ? "VERTICAL: weapon stands up (longest to +Y, hilt low); rotation is the absolute in-hand pose."
                : "NUDGE: rotation adds on top of the geometric grip (legacy WO-551 path).");
        }

        private void OnResetVertical()
        {
            // Sheathed reset = back to the BUILT-IN sheathe pose (zero nudge), not "vertical" —
            // an all-zero NUDGE entry is exactly today's derived back pose.
            if (_sheathed)
            {
                _pos = Vector3.zero; _euler = Vector3.zero; _fullOverride = false;
                if (_fullBtn != null) _fullBtn.text = "Mode: NUDGE on built-in";
                RebuildBody();
                Apply();
                SetStatus("Reset to the built-in sheathe pose (zero nudge).");
                return;
            }
            _pos = Vector3.zero; _euler = Vector3.zero; _scale = 1f; _fullOverride = true;
            if (_fullBtn != null) _fullBtn.text = "Mode: VERTICAL + delta";
            RebuildBody();
            Apply();
            SetStatus("Reset to 100% vertical baseline (hilt lower-half, blade up).");
        }

        private void OnSave()
        {
            if (_eq == null) { SetStatus("No hero."); return; }
            bool ok = _eq.SaveSeating(_pos, _euler, _scale, _fullOverride, out string devPath, out string snippet);
            Debug.Log($"[Seating] SAVE {_offsetKey}: {snippet}");
            Debug.Log($"[Seating] dev file: {devPath}");
            if (ok) BeginEdit(_offHand);
            SetStatus(ok
                ? $"Saved '{_offsetKey}' to local settings ({AttachmentOffsetRegistry.DevPath}). Re-equipped from file."
                : $"Save FAILED for '{_offsetKey}' (see Console).");
        }

        private void OnExport()
        {
            // Surface the snippet without committing a write (read-only export).
            var snippet = SnippetPreview();
            Debug.Log($"[Seating] EXPORT {_offsetKey}: {snippet}");
            SetStatus("JSON snippet logged to Console (copy into offsets.json).");
        }

        private void OnClear()
        {
            if (string.IsNullOrEmpty(_offsetKey)) return;
            AttachmentOffsetRegistry.RemoveOffset(_offsetKey);
            OnResetVertical();
            SetStatus($"Cleared saved offset for '{_offsetKey}' (back to pure geometry).");
        }

        private void OnReequip()
        {
            if (_eq == null) return;
            _eq.ReapplySeatingFromRegistry();
            // Re-seed from the freshly attached prop.
            BeginEdit(_offHand);
            RebuildBody();
            SetStatus("Re-equipped from the saved file — this is exactly what runtime produces.");
        }

        private string SnippetPreview()
        {
            var ci = CultureInfo.InvariantCulture;
            return string.Format(ci,
                "{{ \"id\": \"{0}\", \"rot\": {{ \"x\": {1:0.###}, \"y\": {2:0.###}, \"z\": {3:0.###} }}, " +
                "\"pos\": {{ \"x\": {4:0.####}, \"y\": {5:0.####}, \"z\": {6:0.####} }}, " +
                "\"scale\": {7:0.###}, \"fullOverride\": {8} }}",
                _offsetKey, _euler.x, _euler.y, _euler.z, _pos.x, _pos.y, _pos.z,
                _scale <= 0f ? 1f : _scale, _fullOverride ? "true" : "false");
        }

        private void Touch()
        {
            if (_targetLabel != null) _targetLabel.text = TargetText();
        }

        private void SetStatus(string s) { if (_status != null) _status.text = s; }

        // ── Small styled widgets ───────────────────────────────────────────────────────
        private static Button StepBtn(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.width = 26; b.style.height = 22; b.style.marginLeft = 2; b.style.marginRight = 2;
            b.style.backgroundColor = SecBg; b.style.color = TitleGold;
            SetBorder(b, ReadoutBdr, 1); SetRadius(b, 4);
            b.style.fontSize = 11; b.style.paddingLeft = 0; b.style.paddingRight = 0;
            return b;
        }

        private static Button SecButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.backgroundColor = SecBg; b.style.color = SecTxt;
            SetBorder(b, ReadoutBdr, 1); SetRadius(b, 6);
            b.style.paddingTop = 6; b.style.paddingBottom = 6;
            b.style.paddingLeft = 10; b.style.paddingRight = 10;
            b.style.fontSize = 11;
            return b;
        }

        private static Button MakeToggle(string text, bool on, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.paddingTop = 6; b.style.paddingBottom = 6;
            b.style.paddingLeft = 10; b.style.paddingRight = 10;
            b.style.fontSize = 11;
            SetRadius(b, 6);
            StyleToggle(b, on);
            return b;
        }

        private static void StyleToggle(Button b, bool on)
        {
            b.style.backgroundColor = on ? ConfirmBg : SecBg;
            b.style.color = on ? ConfirmTxt : SecTxt;
            SetBorder(b, on ? ConfirmBdr : ReadoutBdr, 1);
        }

        private static Label SectionLabel(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 10; l.style.color = ReadoutBdr;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.letterSpacing = 1;
            l.style.marginTop = 8; l.style.marginBottom = 4;
            return l;
        }

        // ── Hero resolve ───────────────────────────────────────────────────────────────
        private static EquipmentController ResolveHeroEquipment()
        {
            EquipmentController fallback = null;
            foreach (var e in FindObjectsByType<EquipmentController>())
            {
                if (e == null) continue;
                if (fallback == null) fallback = e;
                // Prefer the Player-tagged hero.
                if (e.gameObject.CompareTag("Player")) return e;
                var t = e.transform;
                while (t != null) { if (t.CompareTag("Player")) return e; t = t.parent; }
            }
            return fallback;
        }

        // ── Show / PanelSettings adoption (renders in builds) ───────────────────────────
        private void Show()
        {
            if (_document == null) return;
            _document.enabled = true;
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            IsOpen = true;
            if (_panelHandle == null)
                _panelHandle = PanelManager.RegisterBattleAllowed("SeatingEditor", Close, () => IsOpen);
            PanelManager.NotifyOpened(_panelHandle);
        }

        private void AdoptPanelSettings()
        {
            if (_document == null) return;
            if (_document.panelSettings != null) return;

            UIDocument hud = null, any = null;
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            {
                if (doc == null || doc == _document || doc.panelSettings == null) continue;
                if (any == null) any = doc;
                if (doc.gameObject.name.IndexOf("Hud", StringComparison.OrdinalIgnoreCase) >= 0) { hud = doc; break; }
            }
            var src = hud ?? any;
            if (src != null)
            {
                _document.panelSettings = src.panelSettings;
                // Must sit ABOVE the inventory uGUI canvas (sortingOrder 31000) so the editor
                // renders ON TOP of the panel that launches it — otherwise it hides behind and the
                // caller is forced to Close() the panel first (owner F8: "tool closes the window").
                _document.sortingOrder  = Mathf.Max(src.sortingOrder + 14, 32100);
            }
            else
            {
                Debug.LogWarning("[Seating] No sibling PanelSettings found — panel will not render. Add a scene UIDocument with a PanelSettings.");
            }
        }

        // ── helpers ─────────────────────────────────────────────────────────────────────
        private static float Wrap180(float a)
        {
            a %= 360f;
            if (a > 180f)  a -= 360f;
            if (a < -180f) a += 360f;
            return a;
        }

        private static Color Hex(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        private static void SetBorder(VisualElement ve, Color c, float w)
        {
            ve.style.borderTopColor = c; ve.style.borderRightColor = c;
            ve.style.borderBottomColor = c; ve.style.borderLeftColor = c;
            ve.style.borderTopWidth = w; ve.style.borderRightWidth = w;
            ve.style.borderBottomWidth = w; ve.style.borderLeftWidth = w;
        }

        private static void SetRadius(VisualElement ve, float r)
        {
            ve.style.borderTopLeftRadius = r; ve.style.borderTopRightRadius = r;
            ve.style.borderBottomLeftRadius = r; ve.style.borderBottomRightRadius = r;
        }

        private static void TintSlider(Slider s, Color accent)
        {
            var dragger = s.Q("unity-dragger");
            if (dragger != null) dragger.style.backgroundColor = accent;
            var tracker = s.Q("unity-tracker");
            if (tracker != null) tracker.style.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.35f);
        }
    }
}
