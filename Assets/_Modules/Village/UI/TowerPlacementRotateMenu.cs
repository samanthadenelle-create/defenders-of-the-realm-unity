// =============================================================================
// TowerPlacementRotateMenu — WO-334 (Preview & Rotate tower-placement panel).
// -----------------------------------------------------------------------------
// A modal UIElements panel opened when the player places a tower. It shows a
// live 3D RenderTexture preview (driven by TowerPreviewCamera) and three axis
// sliders (X/Pitch, Y/Yaw, Z/Roll) to dial in the placement rotation before
// committing.
//
// PROCEDURAL UIElements ONLY — NO UXML (CLAUDE.md §8). Every VisualElement is
// built in code; all styles are inline. Renders in player builds by adopting a
// sibling/scene UIDocument's PanelSettings (the documented "code-built UIDocument
// with no PanelSettings renders nothing" trap — see MEMORY).
//
// API:
//   Open(TowerData, double costSkr, Quaternion initialRotation,
//        Action<Quaternion> onConfirm, Action onCancel)
//   Close()
//
// TowerData = DeNelle.Core.Data.TowerData (the real tower ScriptableObject).
// It has NO direct prefab field — the Level-1 visual is upgrades[0].visualPrefab
// and there is no PreviewSprite, so the thumbnail uses a coloured-square fallback.
//
// BUILD-RENDER RISK (flagged in handback): UIToolkit panels rendering in player
// BUILDS is a known project landmine. This is code-built (better than UXML) and
// adopts a live PanelSettings, but it still needs a felt-test in an actual build,
// not just the editor.
// =============================================================================

using System;
using DeNelle.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Village
{
    /// <summary>
    /// Modal "Preview &amp; Rotate" panel for tower placement. Driven by the build
    /// system: call <see cref="Open"/> with the chosen tower, then it invokes the
    /// onConfirm callback with the final <see cref="Quaternion"/> or onCancel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerPlacementRotateMenu : MonoBehaviour
    {
        // ── Palette (from the approved mockup) ──────────────────────────────────
        private static readonly Color PanelBg     = Hex(0x0c, 0x16, 0x25);
        private static readonly Color RuneBorder  = Hex(0x9a, 0x74, 0x20);
        private static readonly Color TitleGold   = Hex(0xee, 0xc8, 0x48);
        private static readonly Color ViewportBg  = Hex(0x05, 0x0c, 0x18);
        private static readonly Color ReadoutBg   = Hex(0x05, 0x0c, 0x18);
        private static readonly Color ReadoutBdr  = Hex(0x38, 0x28, 0x0e);
        private static readonly Color ConfirmBg   = Hex(0x9a, 0x6e, 0x0c);
        private static readonly Color ConfirmBdr  = Hex(0xd4, 0xa0, 0x28);
        private static readonly Color ConfirmTxt  = Hex(0xff, 0xf8, 0xe0);
        private static readonly Color CancelBg    = Hex(0x1a, 0x0c, 0x06);
        private static readonly Color CancelTxt   = Hex(0xb0, 0x78, 0x38);
        private static readonly Color ResetBg     = Hex(0x06, 0x10, 0x1a);
        private static readonly Color ResetTxt    = Hex(0x48, 0x78, 0xa8);
        private static readonly Color RuneStripTxt = Hex(0x6a, 0x4e, 0x14);

        private static readonly Color AxisX = Hex(0xd0, 0x40, 0x40); // Pitch
        private static readonly Color AxisY = Hex(0x38, 0xb8, 0x38); // Yaw
        private static readonly Color AxisZ = Hex(0x38, 0x78, 0xc0); // Roll

        private const string RuneGlyphs = "ᚨ ᚠ ᛗ ᚱ ᛞ ᛊ ᚲ ᛚ ᛈ ᚺ ᛜ ᛒ ᛖ ᚾ ᚢ ᛁ ";
        private const string FontPath   = "Assets/_Modules/Village/Fonts/Cinzel-Regular.ttf";

        // ── State ───────────────────────────────────────────────────────────────
        private UIDocument _document;
        private VisualElement _root;

        private TowerData          _towerData;
        private GameObject         _previewPrefab;   // resolved preview model (TowerData→upgrades[0] OR caller-supplied)
        private string             _displayName;     // caller-supplied name (prefab overload); null → derive from _towerData
        private double             _costSkr;
        private Quaternion         _initialRotation;
        private Vector3            _initialEuler;
        private Action<Quaternion> _onConfirm;
        private Action             _onCancel;

        private int   _snapDegrees = 45;          // 0=off, 15, 45, 90
        private float _xDeg, _yDeg, _zDeg;        // current (snapped) euler values

        private Slider _xSlider, _ySlider, _zSlider;
        private Label  _xReadout, _yReadout, _zReadout;
        private VisualElement _viewport;

        private TowerPreviewCamera _preview;
        private Font _cinzel;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            AdoptPanelSettings();
            TryLoadCinzel();
        }

        private void OnDisable()
        {
            DisposePreview();
            if (_document != null) _document.enabled = false;
        }

        private void Update()
        {
            // CRITICAL: the preview camera is manually driven — re-render every
            // frame so the RenderTexture stays live (URP won't auto-render it).
            if (_preview != null && _preview.IsValid)
                _preview.SetRotation(Quaternion.Euler(_xDeg, _yDeg, _zDeg));
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Open the panel for <paramref name="towerData"/> with the given starting rotation.</summary>
        public void Open(
            TowerData towerData,
            double costSkr,
            Quaternion initialRotation,
            Action<Quaternion> onConfirm,
            Action onCancel)
        {
            _towerData = towerData;
            // The TowerData Level-1 visual is upgrades[0].visualPrefab — resolve it once
            // here so the shared core path is prefab-driven for BOTH overloads.
            GameObject prefab = towerData != null
                                && towerData.upgrades != null
                                && towerData.upgrades.Length > 0
                ? towerData.upgrades[0]?.visualPrefab
                : null;
            string name = towerData != null ? towerData.towerName : "Tower";

            OpenCore(prefab, name, costSkr, initialRotation, onConfirm, onCancel);
        }

        /// <summary>
        /// Prefab overload — drive the same Preview &amp; Rotate UI directly off a
        /// loaded visual prefab + display name (used by Build Mode, which arms a
        /// CatalogEntry with a visualPrefab PATH, not a TowerData SO). Shares the
        /// same UI/preview core as the TowerData overload.
        /// </summary>
        public void Open(
            GameObject previewPrefab,
            string displayName,
            double costSkr,
            Quaternion initialRotation,
            Action<Quaternion> onConfirm,
            Action onCancel)
        {
            _towerData = null;   // prefab path — no SO; tier label falls back to "TIER I"
            OpenCore(previewPrefab, displayName, costSkr, initialRotation, onConfirm, onCancel);
        }

        /// <summary>Shared open path for both overloads — builds the panel + preview off a prefab.</summary>
        private void OpenCore(
            GameObject previewPrefab,
            string displayName,
            double costSkr,
            Quaternion initialRotation,
            Action<Quaternion> onConfirm,
            Action onCancel)
        {
            _previewPrefab   = previewPrefab;
            _displayName     = displayName;
            _costSkr         = costSkr;
            _initialRotation = initialRotation;
            _initialEuler    = NormalizeEuler(initialRotation.eulerAngles);
            _onConfirm       = onConfirm;
            _onCancel        = onCancel;

            _xDeg = _initialEuler.x;
            _yDeg = _initialEuler.y;
            _zDeg = _initialEuler.z;

            Debug.Log($"[Orient] OpenCore: {displayName} prefab={(previewPrefab != null ? previewPrefab.name : "<none>")}");
            BuildPanel();
            BeginPreview();
            Debug.Log($"[Orient] panel built; preview valid={(_preview != null && _preview.IsValid)}");
            ShowPanel();
        }

        /// <summary>Close the panel and tear down the preview rig. Fires NO callback.</summary>
        public void Close()
        {
            Debug.Log("[Orient] Close.");
            DisposePreview();
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (_document != null) _document.enabled = false;
        }

        // ── Panel construction ──────────────────────────────────────────────────

        private void BuildPanel()
        {
            if (_document == null) return;
            var rootVE = _document.rootVisualElement;
            rootVE.Clear();

            // Full-screen dimmer + centred card.
            _root = new VisualElement();
            _root.style.position        = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0;
            _root.style.right = 0; _root.style.bottom = 0;
            _root.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            _root.style.alignItems      = Align.Center;
            _root.style.justifyContent  = Justify.Center;

            var card = new VisualElement();
            card.style.backgroundColor = PanelBg;
            SetBorder(card, RuneBorder, 2);
            SetRadius(card, 10);
            card.style.minWidth   = 440;
            card.style.maxWidth   = 520;
            card.style.paddingTop = 6; card.style.paddingBottom = 6;
            card.style.paddingLeft = 6; card.style.paddingRight = 6;

            card.Add(BuildRuneStrip(false));      // top strip

            var body = new VisualElement();
            body.style.paddingTop = 6; body.style.paddingBottom = 6;
            body.style.paddingLeft = 14; body.style.paddingRight = 14;

            body.Add(BuildHeader());
            body.Add(BuildViewport());
            body.Add(BuildAxisRow("X Axis (Pitch)", AxisX, _xDeg, out _xSlider, out _xReadout, OnXChanged, () => ResetAxis(0)));
            body.Add(BuildAxisRow("Y Axis (Yaw)",   AxisY, _yDeg, out _ySlider, out _yReadout, OnYChanged, () => ResetAxis(1)));
            body.Add(BuildAxisRow("Z Axis (Roll)",  AxisZ, _zDeg, out _zSlider, out _zReadout, OnZChanged, () => ResetAxis(2)));
            body.Add(BuildInfoBar());
            body.Add(BuildControlsRow());

            card.Add(body);
            card.Add(BuildRuneStrip(false));      // bottom strip

            // Side rune strips overlaid (rotated) — decorative.
            var framed = new VisualElement();
            framed.style.flexDirection = FlexDirection.Row;
            framed.Add(BuildRuneStrip(true));
            framed.Add(card);
            framed.Add(BuildRuneStrip(true));

            _root.Add(framed);
            rootVE.Add(_root);
        }

        private VisualElement BuildHeader()
        {
            var row = new VisualElement();
            row.style.flexDirection  = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems     = Align.Center;
            row.style.marginBottom   = 8;

            var left = new VisualElement();
            left.style.flexDirection = FlexDirection.Row;
            left.style.alignItems    = Align.Center;

            var badge = new Label("Preview & Rotate");
            badge.style.fontSize = 11;
            badge.style.color    = ConfirmTxt;
            badge.style.backgroundColor = ConfirmBg;
            badge.style.paddingTop = 2; badge.style.paddingBottom = 2;
            badge.style.paddingLeft = 8; badge.style.paddingRight = 8;
            SetRadius(badge, 4);
            badge.style.marginRight = 10;
            ApplyFont(badge);
            left.Add(badge);

            var title = new Label("TOWER PLACEMENT");
            title.style.fontSize = 15;
            title.style.color    = TitleGold;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 2;
            ApplyFont(title);
            left.Add(title);

            row.Add(left);

            var hammer = new Label("🔨");
            hammer.style.fontSize = 16;
            row.Add(hammer);
            return row;
        }

        private VisualElement BuildViewport()
        {
            _viewport = new VisualElement();
            _viewport.style.height = 200;
            _viewport.style.backgroundColor = ViewportBg;
            SetBorder(_viewport, ReadoutBdr, 1);
            SetRadius(_viewport, 6);
            _viewport.style.marginBottom = 12;
            _viewport.style.alignItems     = Align.Center;
            _viewport.style.justifyContent = Justify.Center;
            return _viewport;
        }

        // axisIndex via the reset closure; slider/readout returned by out params.
        private VisualElement BuildAxisRow(
            string label, Color accent, float initial,
            out Slider slider, out Label readout,
            EventCallback<ChangeEvent<float>> onChange, Action onReset)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems    = Align.Center;
            row.style.marginBottom  = 6;

            var name = new Label(label);
            name.style.fontSize = 11;
            name.style.color    = accent;
            name.style.width    = 96;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(name);

            slider = new Slider(-180f, 180f) { value = initial };
            slider.style.flexGrow = 1;
            slider.style.marginLeft = 4; slider.style.marginRight = 8;
            TintSlider(slider, accent);
            slider.RegisterValueChangedCallback(onChange);
            row.Add(slider);

            readout = new Label($"{Mathf.RoundToInt(initial)}°");
            readout.style.width    = 46;
            readout.style.fontSize = 11;
            readout.style.color    = TitleGold;
            readout.style.backgroundColor = ReadoutBg;
            SetBorder(readout, ReadoutBdr, 1);
            SetRadius(readout, 4);
            readout.style.unityTextAlign = TextAnchor.MiddleCenter;
            readout.style.paddingTop = 3; readout.style.paddingBottom = 3;
            row.Add(readout);

            var reset = new Button(() => onReset()) { text = "↺" };
            reset.style.width = 26; reset.style.height = 22;
            reset.style.marginLeft = 4;
            reset.style.backgroundColor = ResetBg;
            reset.style.color = ResetTxt;
            SetBorder(reset, ReadoutBdr, 1);
            SetRadius(reset, 4);
            reset.style.fontSize = 12;
            row.Add(reset);

            return row;
        }

        private VisualElement BuildInfoBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection  = FlexDirection.Row;
            bar.style.alignItems     = Align.Center;
            bar.style.justifyContent = Justify.SpaceBetween;
            bar.style.backgroundColor = ViewportBg;
            SetBorder(bar, ReadoutBdr, 1);
            SetRadius(bar, 6);
            bar.style.paddingTop = 6; bar.style.paddingBottom = 6;
            bar.style.paddingLeft = 8; bar.style.paddingRight = 8;
            bar.style.marginTop = 6; bar.style.marginBottom = 10;

            var left = new VisualElement();
            left.style.flexDirection = FlexDirection.Row;
            left.style.alignItems    = Align.Center;

            // Thumbnail: TowerData has no PreviewSprite — coloured-square fallback.
            var thumb = new VisualElement();
            thumb.style.width = 36; thumb.style.height = 36;
            thumb.style.backgroundColor = RuneBorder;
            SetRadius(thumb, 4);
            thumb.style.marginRight = 10;
            var thumbIcon = new Label("🏰");
            thumbIcon.style.fontSize = 20;
            thumbIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            thumbIcon.style.flexGrow = 1;
            thumb.Add(thumbIcon);
            left.Add(thumb);

            string towerName = !string.IsNullOrEmpty(_displayName)
                ? _displayName
                : (_towerData != null ? _towerData.towerName : "Tower");
            var nameLabel = new Label($"{towerName}  ·  {TierLabel()}");
            nameLabel.style.fontSize = 12;
            nameLabel.style.color    = ConfirmTxt;
            ApplyFont(nameLabel);
            left.Add(nameLabel);

            bar.Add(left);

            var cost = new Label($"{_costSkr:F0} SKR");
            cost.style.fontSize = 13;
            cost.style.color    = TitleGold;
            cost.style.unityFontStyleAndWeight = FontStyle.Bold;
            bar.Add(cost);

            return bar;
        }

        private VisualElement BuildControlsRow()
        {
            var col = new VisualElement();

            // Row 1: snap dropdown + confirm.
            var row1 = new VisualElement();
            row1.style.flexDirection  = FlexDirection.Row;
            row1.style.justifyContent = Justify.SpaceBetween;
            row1.style.alignItems     = Align.Center;
            row1.style.marginBottom   = 8;

            var snapWrap = new VisualElement();
            snapWrap.style.flexDirection = FlexDirection.Row;
            snapWrap.style.alignItems    = Align.Center;
            var snapLabel = new Label("Snap:");
            snapLabel.style.fontSize = 11;
            snapLabel.style.color    = CancelTxt;
            snapLabel.style.marginRight = 6;
            snapWrap.Add(snapLabel);

            var snapChoices = new System.Collections.Generic.List<string> { "Off", "15°", "45°", "90°" };
            var snapDrop = new DropdownField(snapChoices, SnapIndex(_snapDegrees));
            snapDrop.style.minWidth = 80;
            snapDrop.RegisterValueChangedCallback(evt =>
            {
                _snapDegrees = SnapValue(evt.newValue);
                // Re-apply snap to current values & refresh readouts.
                ApplySnapToAll();
            });
            snapWrap.Add(snapDrop);
            row1.Add(snapWrap);

            var confirm = new Button(OnConfirmClicked) { text = "Confirm Placement" };
            confirm.style.backgroundColor = ConfirmBg;
            confirm.style.color = ConfirmTxt;
            SetBorder(confirm, ConfirmBdr, 1);
            SetRadius(confirm, 6);
            confirm.style.paddingTop = 8; confirm.style.paddingBottom = 8;
            confirm.style.paddingLeft = 16; confirm.style.paddingRight = 16;
            confirm.style.fontSize = 13;
            confirm.style.unityFontStyleAndWeight = FontStyle.Bold;
            ApplyFont(confirm);
            row1.Add(confirm);

            col.Add(row1);

            // Row 2: cancel + reset rotation.
            var row2 = new VisualElement();
            row2.style.flexDirection  = FlexDirection.Row;
            row2.style.justifyContent = Justify.SpaceBetween;

            var cancel = new Button(OnCancelClicked) { text = "Cancel" };
            cancel.style.backgroundColor = CancelBg;
            cancel.style.color = CancelTxt;
            SetBorder(cancel, ReadoutBdr, 1);
            SetRadius(cancel, 6);
            cancel.style.paddingTop = 8; cancel.style.paddingBottom = 8;
            cancel.style.paddingLeft = 16; cancel.style.paddingRight = 16;
            cancel.style.fontSize = 13;
            row2.Add(cancel);

            var reset = new Button(OnResetRotationClicked) { text = "Reset Rotation" };
            reset.style.backgroundColor = ResetBg;
            reset.style.color = ResetTxt;
            SetBorder(reset, ReadoutBdr, 1);
            SetRadius(reset, 6);
            reset.style.paddingTop = 8; reset.style.paddingBottom = 8;
            reset.style.paddingLeft = 16; reset.style.paddingRight = 16;
            reset.style.fontSize = 13;
            row2.Add(reset);

            col.Add(row2);
            return col;
        }

        private VisualElement BuildRuneStrip(bool vertical)
        {
            var strip = new VisualElement();
            if (vertical)
            {
                strip.style.width = 14;
                strip.style.justifyContent = Justify.Center;
                strip.style.alignItems     = Align.Center;
            }
            else
            {
                strip.style.height = 14;
                strip.style.justifyContent = Justify.Center;
                strip.style.alignItems     = Align.Center;
                strip.style.overflow = Overflow.Hidden;
            }

            var glyphs = new Label(RepeatRunes(vertical ? 6 : 10));
            glyphs.style.fontSize = 8;
            glyphs.style.color    = RuneStripTxt;
            glyphs.style.letterSpacing = 3;
            glyphs.style.whiteSpace = WhiteSpace.NoWrap;
            if (vertical)
                glyphs.style.rotate = new StyleRotate(new Rotate(new Angle(90f, AngleUnit.Degree)));
            strip.Add(glyphs);
            return strip;
        }

        // ── Slider / snap callbacks ──────────────────────────────────────────────

        private void OnXChanged(ChangeEvent<float> evt) => SetAxis(0, evt.newValue);
        private void OnYChanged(ChangeEvent<float> evt) => SetAxis(1, evt.newValue);
        private void OnZChanged(ChangeEvent<float> evt) => SetAxis(2, evt.newValue);

        private void SetAxis(int axis, float raw)
        {
            float snapped = SnapAngle(raw);
            switch (axis)
            {
                case 0: _xDeg = snapped; UpdateReadout(_xReadout, _xSlider, snapped, raw); break;
                case 1: _yDeg = snapped; UpdateReadout(_yReadout, _ySlider, snapped, raw); break;
                default: _zDeg = snapped; UpdateReadout(_zReadout, _zSlider, snapped, raw); break;
            }
        }

        private void UpdateReadout(Label readout, Slider slider, float snapped, float raw)
        {
            if (readout != null) readout.text = $"{Mathf.RoundToInt(snapped)}°";
            // If snapping moved the value, reflect it on the slider handle too
            // (guard against feedback by only setting when it differs).
            if (slider != null && !Mathf.Approximately(slider.value, snapped) && !Mathf.Approximately(raw, snapped))
                slider.SetValueWithoutNotify(snapped);
        }

        private float SnapAngle(float raw) =>
            _snapDegrees == 0 ? raw : Mathf.Round(raw / _snapDegrees) * _snapDegrees;

        private void ApplySnapToAll()
        {
            SetAxis(0, _xSlider != null ? _xSlider.value : _xDeg);
            SetAxis(1, _ySlider != null ? _ySlider.value : _yDeg);
            SetAxis(2, _zSlider != null ? _zSlider.value : _zDeg);
        }

        // ── Reset ────────────────────────────────────────────────────────────────

        /// <summary>Reset a single axis (0=X,1=Y,2=Z) to its initialRotation component.</summary>
        private void ResetAxis(int axis)
        {
            switch (axis)
            {
                case 0: _xDeg = _initialEuler.x; _xSlider?.SetValueWithoutNotify(_xDeg); if (_xReadout != null) _xReadout.text = $"{Mathf.RoundToInt(_xDeg)}°"; break;
                case 1: _yDeg = _initialEuler.y; _ySlider?.SetValueWithoutNotify(_yDeg); if (_yReadout != null) _yReadout.text = $"{Mathf.RoundToInt(_yDeg)}°"; break;
                default: _zDeg = _initialEuler.z; _zSlider?.SetValueWithoutNotify(_zDeg); if (_zReadout != null) _zReadout.text = $"{Mathf.RoundToInt(_zDeg)}°"; break;
            }
        }

        private void OnResetRotationClicked()
        {
            ResetAxis(0);
            ResetAxis(1);
            ResetAxis(2);
        }

        // ── Confirm / Cancel ─────────────────────────────────────────────────────

        private void OnConfirmClicked()
        {
            var finalRotation = Quaternion.Euler(SnapAngle(_xDeg), SnapAngle(_yDeg), SnapAngle(_zDeg));
            var cb = _onConfirm;
            Close();
            cb?.Invoke(finalRotation);
        }

        private void OnCancelClicked()
        {
            var cb = _onCancel;
            Close();
            cb?.Invoke();
        }

        // ── Preview ──────────────────────────────────────────────────────────────

        private void BeginPreview()
        {
            DisposePreview();
            GameObject prefab = _previewPrefab;

            _preview = new TowerPreviewCamera();
            bool ok = _preview.Begin(prefab);
            if (ok && _preview.Texture != null && _viewport != null)
            {
                _viewport.style.backgroundImage = Background.FromRenderTexture(_preview.Texture);
                _preview.SetRotation(Quaternion.Euler(_xDeg, _yDeg, _zDeg));
            }
            else
            {
                // Fallback: no Level-1 visual prefab → icon + name label.
                // TODO: live preview — wire a PreviewSprite onto TowerData and
                // render it here when the prefab is unavailable.
                DisposePreview();
                if (_viewport != null)
                {
                    _viewport.Clear();
                    var fb = new Label("🏰\n(no preview)");
                    fb.style.fontSize = 18;
                    fb.style.color = TitleGold;
                    fb.style.unityTextAlign = TextAnchor.MiddleCenter;
                    fb.style.whiteSpace = WhiteSpace.Normal;
                    _viewport.Add(fb);
                }
            }
        }

        private void DisposePreview()
        {
            if (_preview != null)
            {
                _preview.Dispose();
                _preview = null;
            }
            if (_viewport != null)
                _viewport.style.backgroundImage = new StyleBackground((Texture2D)null);
        }

        // ── Show / Hide ──────────────────────────────────────────────────────────

        private void ShowPanel()
        {
            if (_document == null) return;
            _document.enabled = true;
            if (_root != null) _root.style.display = DisplayStyle.Flex;
        }

        // ── PanelSettings adoption (renders in builds) ───────────────────────────

        /// <summary>
        /// Adopt a sibling/scene UIDocument's PanelSettings so this code-built
        /// panel actually renders (a UIDocument with a null PanelSettings draws
        /// nothing — the documented empty-UI trap). Sorts above other panels.
        /// </summary>
        private void AdoptPanelSettings()
        {
            if (_document == null) return;
            if (_document.panelSettings != null) return;

            UIDocument hud = null, any = null;
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (doc == null || doc == _document || doc.panelSettings == null) continue;
                if (any == null) any = doc;
                if (doc.gameObject.name.IndexOf("Hud", StringComparison.OrdinalIgnoreCase) >= 0) { hud = doc; break; }
            }
            var src = hud ?? any;
            if (src != null)
            {
                _document.panelSettings = src.panelSettings;
                _document.sortingOrder  = src.sortingOrder + 12;   // modal — above everything
            }
            else
            {
                Debug.LogWarning("[TowerPlacementRotateMenu] No sibling PanelSettings found — panel will not render. Add a scene UIDocument with a PanelSettings.");
            }
        }

        private void TryLoadCinzel()
        {
#if UNITY_EDITOR
            // Editor-only load of the optional Cinzel font; if absent we fall back
            // to the default serif. NO runtime download (per spec).
            _cinzel = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(FontPath);
#endif
        }

        private void ApplyFont(Label l)  { if (_cinzel != null) l.style.unityFont = _cinzel; }
        private void ApplyFont(Button b) { if (_cinzel != null) b.style.unityFont = _cinzel; }

        // ── Small helpers ────────────────────────────────────────────────────────

        private string TierLabel()
        {
            int tiers = _towerData != null && _towerData.upgrades != null ? _towerData.upgrades.Length : 0;
            return tiers > 0 ? $"TIER {ToRoman(tiers)}" : "TIER I";
        }

        private static string ToRoman(int n)
        {
            switch (n)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                default: return n.ToString();
            }
        }

        private static int SnapIndex(int snap)
        {
            switch (snap) { case 15: return 1; case 45: return 2; case 90: return 3; default: return 0; }
        }

        private static int SnapValue(string choice)
        {
            switch (choice) { case "15°": return 15; case "45°": return 45; case "90°": return 90; default: return 0; }
        }

        /// <summary>Map Unity's 0..360 euler to a slider-friendly −180..180 range.</summary>
        private static Vector3 NormalizeEuler(Vector3 e) =>
            new Vector3(Wrap180(e.x), Wrap180(e.y), Wrap180(e.z));

        private static float Wrap180(float a)
        {
            a %= 360f;
            if (a > 180f)  a -= 360f;
            if (a < -180f) a += 360f;
            return a;
        }

        private static string RepeatRunes(int times)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < times; i++) sb.Append(RuneGlyphs);
            return sb.ToString();
        }

        private static Color Hex(int r, int g, int b) =>
            new Color(r / 255f, g / 255f, b / 255f, 1f);

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
