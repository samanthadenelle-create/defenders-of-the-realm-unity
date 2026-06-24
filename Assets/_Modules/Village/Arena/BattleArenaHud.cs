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
        private Action _onFlee;

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
            return hud;
        }

        public void SetFleeHandler(Action onFlee) => _onFlee = onFlee;

        /// <summary>Push the primary-target state (frac 0..1, foes remaining). Logic -> view.</summary>
        public void SetPrimary(string title, float frac, int remaining)
        {
            if (_title != null && title != null) _title.text = title;
            if (_enemyFill != null) _enemyFill.fillAmount = Mathf.Clamp01(frac);
            if (_remain != null) _remain.text = remaining > 1 ? (remaining + " foes remain") : "1 foe remains";
        }

        /// <summary>Show the win/loss banner (family-friendly + encouraging), then self-destruct.</summary>
        public void ShowResult(bool won)
        {
            if (_liveGroup != null) _liveGroup.SetActive(false);
            string line = won ? "Victory!  The realm is safer because of you!"
                              : "Fall back and regroup, hero.";
            var banner = AddPanel(_canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  new Vector2(0f, 60f), new Vector2(760f, 130f), Dark);
            var label = AddText(banner.transform, line, 30, won ? Win : Gold, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
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

            // BOTTOM RIGHT: Flee button.
            var flee = AddButton(_liveGroup.transform, "Flee", new Vector2(1f, 0f), new Vector2(1f, 0f),
                                 new Vector2(-110f, 56f), new Vector2(160f, 56f), () => _onFlee?.Invoke());
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
