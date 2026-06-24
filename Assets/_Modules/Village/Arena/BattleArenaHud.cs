// =============================================================================
// BattleArenaHud — the WO-482 battle overlay VIEW (dumb presentation).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// Logic/presentation split (HP-B2B law): this is a DUMB VIEW. BattleArena (logic)
// pushes state in (SetPrimary / ShowResult) + wires the Flee handler; the view never
// reads game state. It layers the battle-specific chrome ON TOP of the existing combat
// HUD (which already shows hero HP/mana/abilities via HeroAbilitiesHudBridge), per the
// owner design doc "use the existing Battle HUD":
//   - TOP CENTRE : encounter title + primary enemy HP bar + "N foes remain"
//   - BOTTOM RIGHT: Flee button (retreat -> return to the open world)
//   - CENTRE     : victory / defeat banner (bright, encouraging, family-friendly)
//
// Code-built uGUI (NO UXML -- UXML does not render in player builds, learned the hard
// way; PIPELINE_STATE S8). WebGL-safe solid sprites. ASCII logs.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DeNelle.Village.Arena
{
    /// <summary>Battle overlay: primary-target bar + Flee + result banner. Driven by BattleArena.</summary>
    public sealed class BattleArenaHud : MonoBehaviour
    {
        private Canvas _canvas;
        private Text _title;
        private Image _enemyFill;
        private Text _remain;
        private GameObject _liveGroup;   // primary bar + flee (hidden when the banner shows)
        private Image _primaryPanel;     // the TOP-CENTRE title + enemy bar (suppressed when the 9-zone owns the top)
        private Action _onFlee;

        // Flee tap-to-confirm (anti-misfire). First tap ARMS the button ("Tap again to flee?")
        // for a short window; a second tap inside the window actually flees; otherwise it
        // disarms back to "Flee". This prevents an accidental tap from bailing the fight.
        private Button _fleeBtn;
        private Image _fleePanel;
        private Text _fleeLabel;
        private bool _fleeArmed;
        private System.Collections.IEnumerator _fleeDisarm;
        private const float FleeConfirmWindow = 2f;   // seconds the armed state stays live

        // Owner-tunable Flee anchor: TOP-LEFT corner, well away from the bottom-right ability
        // arc / basic-attack / joystick zones. Small + de-emphasised (a retreat, not a primary
        // action). anchoredPosition is from the top-left pivot (x right, y down).
        private static readonly Vector2 FleePivot  = new Vector2(0f, 1f);   // top-left anchor
        private static readonly Vector2 FleeOffset = new Vector2(96f, -52f);// in from the corner
        private static readonly Vector2 FleeSize   = new Vector2(140f, 48f);

        private static readonly Color FleeIdle  = new Color(0.42f, 0.20f, 0.20f, 0.78f); // de-emphasised
        private static readonly Color FleeArmed = new Color(0.85f, 0.30f, 0.26f, 0.95f); // bright "confirm"

        // WO-498 — the new 9-zone mobile battle HUD bones. Spawned alongside this overlay
        // when ff.battlehud9zone is ON (BattleHud9Zone.Create self-no-ops + returns null when
        // OFF). Tracked so it tears down with this overlay on Close/ShowResult.
        private BattleHud9Zone _hud9;

        private static readonly Color Gold   = new Color(0.92f, 0.78f, 0.36f);
        private static readonly Color Dark   = new Color(0.06f, 0.07f, 0.10f, 0.82f);
        private static readonly Color Danger = new Color(0.80f, 0.24f, 0.22f);
        private static readonly Color Win    = new Color(0.40f, 0.80f, 0.45f);

        /// <summary>Build the overlay canvas (and an EventSystem if none exists) and return it.</summary>
        public static BattleArenaHud Create()
        {
            var go = new GameObject("BattleArenaHud");
            DontDestroyOnLoad(go);
            var hud = go.AddComponent<BattleArenaHud>();
            hud.Build();
            // WO-498 — spawn the 9-zone mobile battle HUD bones alongside (flag-gated; returns
            // null + no-ops when ff.battlehud9zone is OFF, so the legacy overlay is unchanged).
            hud._hud9 = BattleHud9Zone.Create();
            // WO-507 (avoid a DOUBLE HUD): when the 9-zone is APPLIED it owns the top of the
            // screen (Zone 2 enemy family overview @ top-centre, Zone 3 timer @ top-right) and the
            // hero/ability readouts. So SUPPRESS this overlay's duplicate TOP-CENTRE primary panel
            // (encounter title + enemy HP bar + "N foes remain"). We KEEP the pieces the 9-zone
            // bones DON'T have: the top-left Flee+confirm (separate corner) and the centre
            // victory/defeat RESULT banner + stars (ShowResult). When the 9-zone is OFF the legacy
            // overlay is unchanged (the primary panel stays).
            if (hud._hud9 != null) hud.SuppressPrimaryForHud9();
            return hud;
        }

        /// <summary>WO-507 — hide the duplicate top-centre primary panel when the 9-zone HUD
        /// is active (it provides the enemy family overview + timer up top). Flee + the result
        /// banner are untouched, so nothing is lost.</summary>
        private void SuppressPrimaryForHud9()
        {
            if (_primaryPanel != null) _primaryPanel.gameObject.SetActive(false);
        }

        public void SetFleeHandler(Action onFlee) => _onFlee = onFlee;

        /// <summary>Push the primary-target state (frac 0..1, foes remaining). Logic -> view.</summary>
        public void SetPrimary(string title, float frac, int remaining)
        {
            if (_title != null && title != null) _title.text = title;
            if (_enemyFill != null) _enemyFill.fillAmount = Mathf.Clamp01(frac);
            if (_remain != null) _remain.text = remaining > 1 ? (remaining + " foes remain") : "1 foe remains";
        }

        /// <summary>
        /// Show the win/loss banner (family-friendly + encouraging), then self-destruct.
        /// WO-505: <paramref name="stars"/> (0..3) draws the earned star rating under the
        /// line on a WIN — filled glyphs for earned stars, dim glyphs for the rest. 0 (a
        /// loss) shows no stars. Glyph rating only (bones); a sprite pass is the owner's later.
        /// </summary>
        public void ShowResult(bool won, int stars = 0)
        {
            if (_liveGroup != null) _liveGroup.SetActive(false);
            string line = won ? "Victory!  The realm is safer because of you!"
                              : "Fall back and regroup, hero.";
            var banner = AddPanel(_canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  new Vector2(0f, 60f), new Vector2(760f, 160f), Dark);
            var label = AddText(banner.transform, line, 30, won ? Win : Gold, TextAnchor.UpperCenter);
            var lr = label.rectTransform;
            lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f);
            lr.pivot = new Vector2(0.5f, 1f); lr.anchoredPosition = new Vector2(0f, -18f);
            lr.sizeDelta = new Vector2(-24f, 48f);

            // WO-505 star rating row (win only). ASCII glyphs (the legacy runtime font is
            // ASCII-only): '*' = earned star, '-' = unearned, spaced for readability. A sprite
            // star pass is the owner's later polish; these bones prove the wiring.
            if (won && stars > 0)
            {
                int max = Mathf.Max(stars, 3);
                var sb = new System.Text.StringBuilder(max * 2);
                for (int i = 0; i < max; i++) sb.Append(i < stars ? "* " : "- ");
                var starLabel = AddText(banner.transform, sb.ToString().TrimEnd(), 40, Gold, TextAnchor.LowerCenter);
                var sr = starLabel.rectTransform;
                sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 0f);
                sr.pivot = new Vector2(0.5f, 0f); sr.anchoredPosition = new Vector2(0f, 14f);
                sr.sizeDelta = new Vector2(-24f, 52f);
            }

            StartCoroutine(CloseAfter(2.5f));
        }

        public void Close()
        {
            // WO-498 — tear the 9-zone bones down with this overlay (it is a separate canvas).
            if (_hud9 != null) { _hud9.Close(); _hud9 = null; }
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        private System.Collections.IEnumerator CloseAfter(float s)
        {
            yield return new WaitForSeconds(s);
            Close();
        }

        // ── build ────────────────────────────────────────────────────────────
        private void Build()
        {
            EnsureEventSystem();

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5000;  // above the gameplay HUD
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            gameObject.AddComponent<GraphicRaycaster>();

            _liveGroup = new GameObject("Live");
            _liveGroup.transform.SetParent(transform, false);
            var lg = _liveGroup.AddComponent<RectTransform>();
            Stretch(lg);

            // TOP CENTRE: encounter title + enemy HP bar + remaining count.
            var top = AddPanel(_liveGroup.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                               new Vector2(0f, -54f), new Vector2(560f, 78f), Dark);
            _primaryPanel = top;
            _title = AddText(top.transform, "Orc Warband", 22, Gold, TextAnchor.UpperCenter);
            var tr = _title.rectTransform; tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f); tr.anchoredPosition = new Vector2(0f, -6f); tr.sizeDelta = new Vector2(-16f, 26f);

            var barBg = AddPanel(top.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                 new Vector2(0f, -8f), new Vector2(520f, 18f), new Color(0f, 0f, 0f, 0.6f));
            _enemyFill = AddImage(barBg.transform, Danger);
            var fr = _enemyFill.rectTransform; Stretch(fr); fr.offsetMin = new Vector2(2f, 2f); fr.offsetMax = new Vector2(-2f, -2f);
            _enemyFill.type = Image.Type.Filled; _enemyFill.fillMethod = Image.FillMethod.Horizontal;
            _enemyFill.fillOrigin = (int)Image.OriginHorizontal.Left; _enemyFill.fillAmount = 1f;

            _remain = AddText(top.transform, "", 15, new Color(0.85f, 0.85f, 0.9f), TextAnchor.LowerCenter);
            var rr = _remain.rectTransform; rr.anchorMin = new Vector2(0f, 0f); rr.anchorMax = new Vector2(1f, 0f);
            rr.pivot = new Vector2(0.5f, 0f); rr.anchoredPosition = new Vector2(0f, 4f); rr.sizeDelta = new Vector2(-16f, 20f);

            // TOP-LEFT (separate, safe corner): Flee button with tap-to-confirm. Deliberately
            // far from the bottom-right ability arc / basic-attack / joystick so it can never be
            // tapped by accident while reaching for a skill.
            _fleePanel = AddPanel(_liveGroup.transform, FleePivot, FleePivot, FleeOffset, FleeSize, FleeIdle);
            _fleeBtn = _fleePanel.gameObject.AddComponent<Button>();
            _fleeBtn.targetGraphic = _fleePanel;
            _fleeBtn.onClick.AddListener(OnFleeTapped);
            _fleeLabel = AddText(_fleePanel.transform, "Flee", 20, Color.white, TextAnchor.MiddleCenter);
            Stretch(_fleeLabel.rectTransform);
        }

        // First tap arms ("Tap again to flee?"); a second tap inside the window actually flees.
        // An idle window disarms it back to "Flee" so a stray tap is harmless.
        private void OnFleeTapped()
        {
            if (_fleeArmed)
            {
                if (_fleeDisarm != null) { StopCoroutine(_fleeDisarm); _fleeDisarm = null; }
                _onFlee?.Invoke();
                return;
            }

            _fleeArmed = true;
            if (_fleePanel != null) _fleePanel.color = FleeArmed;
            if (_fleeLabel != null) { _fleeLabel.text = "Tap again to flee?"; _fleeLabel.fontSize = 16; }
            if (_fleeDisarm != null) StopCoroutine(_fleeDisarm);
            _fleeDisarm = DisarmFleeAfter(FleeConfirmWindow);
            StartCoroutine(_fleeDisarm);
        }

        private System.Collections.IEnumerator DisarmFleeAfter(float s)
        {
            yield return new WaitForSeconds(s);
            _fleeArmed = false;
            _fleeDisarm = null;
            if (_fleePanel != null) _fleePanel.color = FleeIdle;
            if (_fleeLabel != null) { _fleeLabel.text = "Flee"; _fleeLabel.fontSize = 20; }
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(es);
            }
        }

        // ── tiny uGUI builders (solid sprites, WebGL-safe) ─────────────────────
        private static Image AddPanel(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = col;
            var rt = img.rectTransform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return img;
        }

        private static Image AddImage(Transform parent, Color col)
        {
            var go = new GameObject("Img");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = col;
            return img;
        }

        private static Text AddText(Transform parent, string s, int size, Color col, TextAnchor anchor)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = s; t.fontSize = size; t.color = col; t.alignment = anchor;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                  ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Button AddButton(Transform parent, string label, Vector2 aMin, Vector2 aMax,
                                         Vector2 pos, Vector2 size, Action onClick)
        {
            var panel = AddPanel(parent, aMin, aMax, pos, size, Danger);
            var btn = panel.gameObject.AddComponent<Button>();
            btn.targetGraphic = panel;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var t = AddText(panel.transform, label, 22, Color.white, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            return btn;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
