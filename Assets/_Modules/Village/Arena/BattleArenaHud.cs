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
using DeNelle.Core.UI;   // WO-556: shared Obsidian panel chrome for the victory summary

namespace DeNelle.Village.Arena
{
    /// <summary>Battle overlay: primary-target bar + Flee + result banner. Driven by BattleArena.</summary>
    public sealed class BattleArenaHud : MonoBehaviour
    {
        private Canvas _canvas;
        // WO-563: the legacy TOP-CENTRE primary panel (title + enemy HP bar + "N foes remain")
        // was REMOVED — the 9-zone battle HUD owns the enemy-target readout now. Only the Flee
        // button + the centre result/victory banner remain on this overlay.
        private GameObject _liveGroup;   // hosts the Flee button (hidden when the banner shows)
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
            // WO-563: the legacy top-centre primary panel was removed outright (the 9-zone owns
            // the enemy-target readout), so there is no longer a duplicate to suppress.
            return hud;
        }

        public void SetFleeHandler(Action onFlee) => _onFlee = onFlee;

        /// <summary>
        /// ENGAGE INTRO CARD (encounter feedback): a brief centre overlay naming the engaged foe
        /// (e.g. "Orc Warband - Battle!") so the pull-into-the-fight has an on-screen cause. Built
        /// on the HUD's OWN canvas (a sibling of the live group). Self-destructs after
        /// <paramref name="seconds"/>. ASCII-only text (legacy runtime font).
        /// </summary>
        public void ShowIntro(string foeLabel, float seconds = 1.6f)
        {
            if (_canvas == null) return;
            var card = AddPanel(_canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                new Vector2(0f, 150f), new Vector2(720f, 96f), Dark);
            var label = AddText(card.transform, string.IsNullOrEmpty(foeLabel) ? "Battle!" : foeLabel,
                                34, Gold, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            StartCoroutine(DestroyAfter(card.gameObject, seconds));
        }

        private System.Collections.IEnumerator DestroyAfter(GameObject go, float s)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, s));
            if (go != null) Destroy(go);
        }

        // WO-556: continue latch — Continue button + the auto-timeout both route here; the
        // deferred home-return must fire at most once.
        private bool _continued;

        // Star glyphs (TMP LiberationSans SDF renders these real star symbols — replaces the old
        // ASCII '*'/'-'). A true sprite-art star is a later polish; no star sprite ships today.
        private const string StarFilled = "★";   // ★
        private const string StarEmpty  = "☆";   // ☆

        /// <summary>
        /// WO-556 ITEM 1 — the REAL victory summary (promotes the old 2.5s banner). On a WIN it
        /// builds a shared Obsidian panel (<see cref="ElarionUiKit.BuildObsidianPanel"/>) with the
        /// title, a star row (3/2/1, sprite-style star glyphs), the battle TIME taken, an itemized
        /// reward list, and a Continue button that fires <paramref name="onContinue"/> (the deferred
        /// home-return). A long auto-timeout guards against a softlock if the player never taps it.
        /// On a LOSS it shows a brief regroup panel and self-closes (the controller returns home
        /// immediately). Logic -> view: all numbers are pushed in; the view reads no game state.
        /// </summary>
        public void ShowResult(bool won, int stars, float durationSeconds,
                               BattleRewardSummary rewards, Action onContinue, float autoTimeoutSeconds = 20f)
        {
            if (_liveGroup != null) _liveGroup.SetActive(false);
            // The fight is over — tear the 9-zone battle HUD down now so it can't sit on top of
            // the summary (it is a separate, high-sorting canvas).
            if (_hud9 != null) { _hud9.Close(); _hud9 = null; }

            if (!won)
            {
                ShowLossPanel();
                return;
            }

            ShowVictorySummary(Mathf.Clamp(stars, 0, 3), Mathf.Max(0f, durationSeconds), rewards, onContinue, autoTimeoutSeconds);
        }

        // WO-556: the rich win summary on the shared Obsidian chrome.
        private void ShowVictorySummary(int stars, float durationSeconds,
                                        BattleRewardSummary rewards, Action onContinue, float autoTimeoutSeconds)
        {
            // Close + Continue both fire the deferred return (Close == "I'm done reading").
            Action continueAction = () => Continue(onContinue);

            var chrome = ElarionUiKit.BuildObsidianPanel(
                _canvas.transform, "Victory!",
                new Vector2(0.16f, 0.14f), new Vector2(0.84f, 0.86f),
                onClose: continueAction);
            var content = chrome.content != null ? chrome.content.transform : _canvas.transform;

            // Subtitle — encouraging, family-friendly.
            ElarionUiKit.Label(content, "The realm is safer because of you!", 0.84f, 0.90f,
                               ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center,
                               0.06f, 0.94f);

            // Star row — 3 slots, filled to the earned count. Big gold stars; unearned dim.
            BuildStarRow(content, stars);

            // Battle time taken (M:SS).
            ElarionUiKit.Label(content, "Time  " + FormatTime(durationSeconds), 0.58f, 0.66f,
                               ElarionUi.Gilt, ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center,
                               0.06f, 0.94f, bold: true);

            // Itemized spoils.
            ElarionUiKit.Label(content, "Spoils", 0.49f, 0.56f, ElarionUi.Gilt, ElarionUi.FontLabel,
                               TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);

            string rewardBlock =
                $"+{rewards.Xp} XP\n" +
                $"+{rewards.Wisdom} Wisdom\n" +
                $"+{rewards.Wood} Wood    +{rewards.Iron} Iron\n" +
                (string.IsNullOrEmpty(rewards.GearName) ? "No gear this time" : "Gear:  " + rewards.GearName);
            var block = ElarionUiKit.Label(content, rewardBlock, 0.20f, 0.48f, ElarionUi.Parchment,
                                           ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.08f, 0.92f);
            if (block != null) block.lineSpacing = 8f;

            // Continue button (primary gold CTA).
            ElarionUiKit.Button(content, "Continue", ElarionUiKit.ButtonKind.Gold,
                                new Vector2(0.32f, 0.05f), new Vector2(0.68f, 0.14f), continueAction);

            // Softlock guard: auto-continue after a long timeout if the player never taps.
            StartCoroutine(AutoContinueAfter(autoTimeoutSeconds, onContinue));
        }

        // WO-556: a brief loss panel; the controller returns the hero home immediately, so this
        // just shows + self-destructs (no Continue, recovery timing owned by BattleArena).
        private void ShowLossPanel()
        {
            var chrome = ElarionUiKit.BuildObsidianPanel(
                _canvas.transform, "Defeat",
                new Vector2(0.22f, 0.34f), new Vector2(0.78f, 0.66f),
                onClose: Close);
            var content = chrome.content != null ? chrome.content.transform : _canvas.transform;
            ElarionUiKit.Label(content, "Fall back and regroup, hero.", 0.40f, 0.62f,
                               ElarionUi.Parchment, ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center,
                               0.06f, 0.94f);
            StartCoroutine(CloseAfter(2.5f));
        }

        // Build a centred 3-slot star row, filled to the earned count.
        private void BuildStarRow(Transform parent, int stars)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 3; i++)
            {
                sb.Append(i < stars ? StarFilled : StarEmpty);
                if (i < 2) sb.Append("  ");
            }
            var row = ElarionUiKit.Label(parent, sb.ToString(), 0.68f, 0.82f,
                                         ElarionUi.Gold, ElarionUi.FontTitle + 16,
                                         TMPro.TextAlignmentOptions.Center, 0.10f, 0.90f, bold: true);
            if (row != null) row.characterSpacing = 4f;
        }

        // Seconds -> "M:SS".
        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        // WO-556: fire the deferred home-return exactly once, then tear the summary down.
        private void Continue(Action onContinue)
        {
            if (_continued) return;
            _continued = true;
            onContinue?.Invoke();
            Close();
        }

        private System.Collections.IEnumerator AutoContinueAfter(float seconds, Action onContinue)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(1f, seconds));
            Continue(onContinue);
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

            // WO-563: the TOP-CENTRE primary panel (encounter title + enemy HP bar + "N foes
            // remain") was removed — the 9-zone battle HUD owns the enemy-target readout now.

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
