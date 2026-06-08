// =============================================================================
// ArenaPanel — code-built uGUI ENTRY + RESULT UI for the async-PvP Arena (MVP).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// Code-built uGUI (NO UXML — the project landmine: UXML HUDs come up empty in
// player builds, PIPELINE_STATE §8), screen-space overlay, mobile tap targets.
// Mirrors ShopPanel's construction pattern (Canvas + CanvasScaler + GraphicRaycaster
// + TextMeshProUGUI rows). Palette borrows the ElarionUi role colours so it reads
// as the one designed UI.
//
// TWO SCREENS:
//   ENTRY  - SKR balance + W/L record at the top; one card per seeded opponent
//            (name, tier, garrison, wager, win purse, risk/reward); a RAID button
//            disabled + tinted red when you can't afford the stake. Confirm = stake
//            + start the raid (ArenaMode.TryStartRaid), then the panel hides.
//   RESULT - shown on ArenaMode.OnRaidEnded: WIN/LOSS banner, the SKR delta, the
//            updated balance + record, and a button back to the entry screen.
//
// SCOPE (MVP): seeded opponents, client-stub SKR, simple W/L record. Later bites:
// real matchmaking, on-chain SKR, leaderboard/ELO.
//
// ASCII-only runtime strings.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;

namespace DeNelle.Village.Arena
{
    /// <summary>The Arena entry (pick + wager) and result (win/lose) UI.</summary>
    public sealed class ArenaPanel : MonoBehaviour
    {
        private GameObject _ui;
        private GameObject _entryRoot;
        private GameObject _resultRoot;
        private TMPro.TextMeshProUGUI _headerStats;
        private bool _subscribed;

        /// <summary>Open the Arena entry screen (creates the overlay if needed).</summary>
        public void Open()
        {
            if (_ui == null) BuildRoot();
            Subscribe();
            ShowEntry();
        }

        /// <summary>Tear the overlay down.</summary>
        public void Close()
        {
            Unsubscribe();
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _entryRoot = _resultRoot = null;
            _headerStats = null;
        }

        private void OnDestroy() { Unsubscribe(); if (_ui != null) Destroy(_ui); }

        // -- construction ----------------------------------------------------
        private void BuildRoot()
        {
            _ui = new GameObject("ArenaPanelUI");

            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1100;

            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            _ui.AddComponent<GraphicRaycaster>();

            AddImage(_ui.transform, "Scrim", Vector2.zero, Vector2.one, ElarionUi.Scrim);
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            ArenaMode.Instance.OnRaidEnded += HandleRaidEnded;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (ArenaMode.Instance != null) ArenaMode.Instance.OnRaidEnded -= HandleRaidEnded;
        }

        // ====================================================================
        // ENTRY SCREEN
        // ====================================================================
        private void ShowEntry()
        {
            DestroyScreen(ref _resultRoot);
            DestroyScreen(ref _entryRoot);

            _entryRoot = AddPanel(_ui.transform, new Vector2(0.07f, 0.06f), new Vector2(0.93f, 0.94f));

            AddTitle(_entryRoot.transform, "ARENA - ASYNC PvP", 0.92f, 0.99f);
            _headerStats = AddLabel(_entryRoot.transform, StatsLine(), 0.85f, 0.91f,
                                    ElarionUi.Gilt, 18, TMPro.TextAlignmentOptions.Center);

            // One card per seeded opponent.
            float top = 0.80f;
            float cardH = 0.20f;
            var opponents = ArenaCatalog.All;
            for (int i = 0; i < opponents.Count; i++)
            {
                float y1 = top - i * (cardH + 0.015f);
                float y0 = y1 - cardH;
                BuildOpponentCard(_entryRoot.transform, opponents[i], y0, y1);
            }

            AddButton(_entryRoot.transform, "Close", new Vector2(0.30f, 0.70f),
                      new Vector2(0.02f, 0.08f), ElarionUi.Darken(ElarionUi.PanelStone, 0.02f), Close);
        }

        private void BuildOpponentCard(Transform parent, ArenaOpponentDef opp, float y0, float y1)
        {
            var card = AddImage(parent, "Card_" + opp.Id,
                                new Vector2(0.03f, y0), new Vector2(0.97f, y1), ElarionUi.PanelStoneDark);

            AddLabel(card.transform, $"{opp.DisplayName}   (Tier {opp.Tier})", 0.62f, 0.98f,
                     ElarionUi.Parchment, 19, TMPro.TextAlignmentOptions.Left, 0.04f, 0.66f);
            AddLabel(card.transform, opp.Flavour, 0.40f, 0.62f,
                     ElarionUi.ParchmentDim, 13, TMPro.TextAlignmentOptions.Left, 0.04f, 0.96f);
            AddLabel(card.transform,
                     $"Garrison: 1 boss + {opp.GuardCount} guards   |   Stake: {opp.Wager} SKR   ->   Win: {opp.WinPurse} SKR",
                     0.10f, 0.36f, ElarionUi.Gold, 14, TMPro.TextAlignmentOptions.Left, 0.04f, 0.96f);

            bool canAfford = ArenaWalletService.CanAfford(opp.Wager);
            string btnLabel = canAfford ? $"RAID  ({opp.Wager} SKR)" : "NEED MORE SKR";
            Color btnColor = canAfford ? new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.92f)
                                       : new Color(ElarionUi.Danger.r, ElarionUi.Danger.g, ElarionUi.Danger.b, 0.55f);

            var btn = AddButton(card.transform, btnLabel, new Vector2(0.66f, 0.97f),
                                new Vector2(0.10f, 0.42f), btnColor,
                                () => { if (canAfford) ConfirmRaid(opp); });
            btn.interactable = canAfford;
        }

        private void ConfirmRaid(ArenaOpponentDef opp)
        {
            if (ArenaMode.Instance.TryStartRaid(opp))
            {
                // Hide the overlay during the live raid; the result screen reopens it
                // when OnRaidEnded fires. Keep the GameObject alive (we are subscribed).
                if (_ui != null) _ui.SetActive(false);
            }
            else
            {
                // Couldn't afford / already raiding — refresh the header to show why.
                if (_headerStats != null) _headerStats.text = StatsLine();
            }
        }

        // ====================================================================
        // RESULT SCREEN
        // ====================================================================
        private void HandleRaidEnded(ArenaOpponentDef opp, ArenaResult result, long skrDelta)
        {
            if (_ui == null) BuildRoot();
            _ui.SetActive(true);
            ShowResult(opp, result, skrDelta);
        }

        private void ShowResult(ArenaOpponentDef opp, ArenaResult result, long skrDelta)
        {
            DestroyScreen(ref _entryRoot);
            DestroyScreen(ref _resultRoot);

            _resultRoot = AddPanel(_ui.transform, new Vector2(0.12f, 0.28f), new Vector2(0.88f, 0.72f));

            bool win = result == ArenaResult.Win;
            string banner = win ? "VICTORY" : "DEFEAT";
            Color bannerColor = win ? ElarionUi.Affordable : ElarionUi.Danger;
            AddLabel(_resultRoot.transform, banner, 0.78f, 0.96f, bannerColor, 34,
                     TMPro.TextAlignmentOptions.Center);

            string oppName = opp != null ? opp.DisplayName : "opponent";
            AddLabel(_resultRoot.transform,
                     win ? $"You raided {oppName} and seized the purse."
                         : $"{oppName} held their walls. Your stake is forfeit.",
                     0.62f, 0.74f, ElarionUi.Parchment, 16, TMPro.TextAlignmentOptions.Center);

            string deltaTxt = (skrDelta >= 0 ? "+" : "") + skrDelta + " SKR";
            AddLabel(_resultRoot.transform, deltaTxt, 0.46f, 0.60f,
                     win ? ElarionUi.Gilt : ElarionUi.Danger, 26, TMPro.TextAlignmentOptions.Center);

            AddLabel(_resultRoot.transform, StatsLine(), 0.32f, 0.42f,
                     ElarionUi.Gold, 15, TMPro.TextAlignmentOptions.Center);

            AddButton(_resultRoot.transform, "Back to Arena", new Vector2(0.30f, 0.70f),
                      new Vector2(0.08f, 0.20f), ElarionUi.GoldButton, ShowEntry);
            AddButton(_resultRoot.transform, "Close", new Vector2(0.30f, 0.70f),
                      new Vector2(0.02f, 0.07f), ElarionUi.Darken(ElarionUi.PanelStone, 0.02f), Close);
        }

        private static string StatsLine()
        {
            var rec = ArenaProgressStore.Current;
            return $"SKR: {ArenaWalletService.Balance}      Record: {rec.Wins}W / {rec.Losses}L      Streak: {rec.Streak}";
        }

        // ====================================================================
        // uGUI helpers (ShopPanel-style; anchors are fraction-of-parent rects)
        // ====================================================================
        private static GameObject AddPanel(Transform parent, Vector2 min, Vector2 max)
        {
            var p = AddImage(parent, "Panel", min, max, ElarionUi.PanelStone);
            var outline = p.AddComponent<Outline>();
            outline.effectColor = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f);
            outline.effectDistance = new Vector2(2, 2);
            return p;
        }

        private static GameObject AddImage(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = min; r.anchorMax = max;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private void AddTitle(Transform parent, string text, float y0, float y1)
        {
            AddLabel(parent, text, y0, y1, ElarionUi.Gilt, 26, TMPro.TextAlignmentOptions.Center);
        }

        private static TMPro.TextMeshProUGUI AddLabel(Transform parent, string text, float y0, float y1,
            Color color, int size, TMPro.TextAlignmentOptions align,
            float x0 = 0.03f, float x1 = 0.97f)
        {
            var go = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            return t;
        }

        // anchorX = (centerX, halfWidth); anchorY = (y0, y1) of the button rect.
        private Button AddButton(Transform parent, string label, Vector2 anchorX, Vector2 anchorY,
            Color bg, System.Action onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f - anchorX.y, anchorY.x);
            r.anchorMax = new Vector2(0.5f + anchorX.y, anchorY.y);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = bg;
            var btn = go.GetComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var txt = new GameObject("L", typeof(TMPro.TextMeshProUGUI));
            txt.transform.SetParent(go.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            var tt = txt.GetComponent<TMPro.TextMeshProUGUI>();
            tt.text = label;
            tt.fontSize = 16;
            tt.color = ElarionUi.Parchment;
            tt.alignment = TMPro.TextAlignmentOptions.Center;
            return btn;
        }

        private static void DestroyScreen(ref GameObject screen)
        {
            if (screen != null) Destroy(screen);
            screen = null;
        }
    }
}
