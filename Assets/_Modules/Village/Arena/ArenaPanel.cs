// =============================================================================
// ArenaPanel — code-built uGUI ENTRY + RESULT UI for the async-PvP Arena.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// WO-336 RESKIN: modernised to the game's SLEEK direction so the Arena reads as
// the SAME designed UI as the new combat HUD (VillageHudController / HudTheme):
//
//   • SLEEK / MINIMAL — dark, semi-transparent "glass" panels (low alpha so the
//     scene shows through), thin chrome, a SINGLE hairline gold accent rule per
//     surface (a HINT of Elarion runic gold, not a heavy stone frame).
//   • CLEAN TYPOGRAPHY — cream parchment text on glass, a gilt crest title with
//     letter-spacing, the ElarionUi typography ladder (FontTitle..FontMicro).
//   • GENEROUS SPACING — opponent cards are padded glass tiles with breathing
//     room, a darker recessed "well" behind the stake→purse line, a tier pip.
//   • RESPONSIVE — CanvasScaler ScaleWithScreenSize (1080×1920 ref, match 0.5)
//     and fraction-of-parent anchors so it reflows on phone + web/tablet.
//   • WEBGL-SAFE — the whole UI build is wrapped in try/catch and the procedural
//     rounded sprite is failure-safe (falls back to a flat tinted quad) so a
//     texture/exception under WebGL can NEVER blank the panel (same guard as
//     HudTheme / VillageHudController, PIPELINE_STATE §8 + WO-334).
//
// HudTheme lives in DeNelle.HUD which Village must NOT reference (CLAUDE.md §5),
// so the identical sleek look is reproduced HERE from the canonical ElarionUi
// palette (DeNelle.Core.UI — the one assembly Village may reference). Same dark
// glass + thin gold rim recipe, sourced from the same colours, so both surfaces
// read as one game without a forbidden HUD<->Village edge.
//
// TWO SCREENS (wiring UNCHANGED):
//   ENTRY  - SKR balance + W/L header; one card per seeded opponent (name, tier,
//            garrison, stake -> purse); a RAID button disabled + red when you
//            can't afford the stake. Confirm = ArenaMode.TryStartRaid, panel hides.
//   RESULT - on ArenaMode.OnRaidEnded: VICTORY/DEFEAT banner, the SKR delta, the
//            updated balance + record, a button back to the entry screen.
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
        private TMPro.TextMeshProUGUI _headerSkr;
        private TMPro.TextMeshProUGUI _headerRecord;
        private bool _subscribed;

        // ── Sleek palette (sourced from ElarionUi — mirrors HudTheme's recipe) ──
        // Village can reference Core.UI but NOT DeNelle.HUD, so we rebuild the same
        // dark-glass look from the canonical colours rather than calling HudTheme.
        private static readonly Color Glass     = new Color(0.06f, 0.07f, 0.09f, 0.66f);
        private static readonly Color GlassDeep = new Color(0.04f, 0.05f, 0.07f, 0.82f);
        private static readonly Color Track     = new Color(0.0f,  0.0f,  0.0f,  0.45f);
        private static readonly Color Cell      = new Color(0.10f, 0.11f, 0.14f, 0.80f);
        private static readonly Color AccentSoft = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.30f);
        private static readonly Color Accent     = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f);

        /// <summary>Open the Arena entry screen (creates the overlay if needed).</summary>
        public void Open()
        {
            try
            {
                if (_ui == null) BuildRoot();
                Subscribe();
                ShowEntry();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[ArenaPanel] Open failed (UI may be partial): " + e);
            }
        }

        /// <summary>Tear the overlay down.</summary>
        public void Close()
        {
            Unsubscribe();
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _entryRoot = _resultRoot = null;
            _headerSkr = _headerRecord = null;
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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _ui.AddComponent<GraphicRaycaster>();

            AddImage(_ui.transform, "Scrim", Vector2.zero, Vector2.one, ElarionUi.Scrim, rounded: false);
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

            // Title row: gilt crest glyph + spaced title, with a thin gold rule under.
            AddLabel(_entryRoot.transform, ElarionUi.CrestGlyph + "  ARENA", 0.925f, 0.985f,
                     ElarionUi.Gilt, ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, spacing: 6f, bold: true);
            AddLabel(_entryRoot.transform, "ASYNC PvP", 0.895f, 0.925f,
                     ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, spacing: 4f);
            AddRule(_entryRoot.transform, 0.886f, 0.06f, 0.94f);

            BuildStatsHeader(_entryRoot.transform, 0.815f, 0.882f);

            // One card per seeded opponent — padded glass tiles with breathing room.
            float top = 0.785f;
            float cardH = 0.165f;
            float gap = 0.022f;
            var opponents = ArenaCatalog.All;
            for (int i = 0; i < opponents.Count; i++)
            {
                float y1 = top - i * (cardH + gap);
                float y0 = y1 - cardH;
                BuildOpponentCard(_entryRoot.transform, opponents[i], y0, y1);
            }

            // WO-388 debug toggle: A/B the player's OWN castle vs the seeded opponent
            // base as the defender. Default OFF (the verified seeded path). Tapping it
            // flips ArenaMode.UsePlayerCastle and re-renders the entry so the pill
            // reflects the new state.
            BuildUseMyCastleToggle(_entryRoot.transform);

            AddButton(_entryRoot.transform, "Close", new Vector2(0.30f, 0.70f),
                      new Vector2(0.025f, 0.075f), Glass, Close, ButtonKind.Neutral);
        }

        // ── "Use My Castle" toggle — a sleek labelled pill (matches the panel idiom:
        // built from the same glass/gold helpers; tap flips the flag + re-renders). ──
        private void BuildUseMyCastleToggle(Transform parent)
        {
            bool on = ArenaMode.Instance.UsePlayerCastle;

            // A recessed well behind the toggle so it reads as a setting, not a card.
            var well = AddImage(parent, "CastleToggleWell", new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.18f), Track);
            AddLabel(well.transform, "DEFENDER BASE", 0.55f, 0.96f, ElarionUi.ParchmentDim,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Left, 0.04f, 0.62f, spacing: 3f);
            AddLabel(well.transform, on ? "My Castle" : "Seeded opponent", 0.06f, 0.55f, ElarionUi.Parchment,
                     ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, 0.04f, 0.62f, bold: true);

            // The pill: green/ON shows "USE MY CASTLE", neutral/OFF shows "SEEDED".
            string label = on ? "MY CASTLE  *" : "USE MY CASTLE";
            Color pill = on
                ? new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.92f)
                : Glass;
            ButtonKind kind = on ? ButtonKind.Confirm : ButtonKind.Neutral;
            AddButton(well.transform, label, new Vector2(0.18f, 0.78f), new Vector2(0.18f, 0.82f),
                      pill, ToggleUseMyCastle, kind);
        }

        private void ToggleUseMyCastle()
        {
            ArenaMode.Instance.UsePlayerCastle = !ArenaMode.Instance.UsePlayerCastle;
            ShowEntry();   // re-render so the pill + label reflect the new state
        }

        // ── SKR balance + W/L record header — two recessed glass wells. ────────
        private void BuildStatsHeader(Transform parent, float y0, float y1)
        {
            var rec = ArenaProgressStore.Current;

            var skrWell = AddImage(parent, "SkrWell", new Vector2(0.06f, y0), new Vector2(0.49f, y1), Track);
            AddLabel(skrWell.transform, "BALANCE", 0.55f, 0.95f, ElarionUi.ParchmentDim,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, spacing: 3f);
            _headerSkr = AddLabel(skrWell.transform, ArenaWalletService.Balance + " SKR", 0.06f, 0.55f,
                     ElarionUi.Gilt, ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);

            var recWell = AddImage(parent, "RecWell", new Vector2(0.51f, y0), new Vector2(0.94f, y1), Track);
            AddLabel(recWell.transform, "RECORD", 0.55f, 0.95f, ElarionUi.ParchmentDim,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, spacing: 3f);
            _headerRecord = AddLabel(recWell.transform, $"{rec.Wins}W / {rec.Losses}L   ({rec.Streak} streak)", 0.06f, 0.55f,
                     ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
        }

        private void BuildOpponentCard(Transform parent, ArenaOpponentDef opp, float y0, float y1)
        {
            var card = AddPanel(parent, new Vector2(0.04f, y0), new Vector2(0.96f, y1), deep: false);

            // Tier pip — small gilt badge top-left.
            var pip = AddImage(card.transform, "Tier", new Vector2(0.035f, 0.62f), new Vector2(0.155f, 0.92f),
                               new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.22f));
            AddLabel(pip.transform, "T" + opp.Tier, 0f, 1f, ElarionUi.Gilt,
                     ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);

            // Name (beside the pip) + flavour beneath.
            AddLabel(card.transform, opp.DisplayName, 0.60f, 0.94f, ElarionUi.Parchment,
                     ElarionUi.FontHead, TMPro.TextAlignmentOptions.Left, 0.18f, 0.66f, bold: true);
            AddLabel(card.transform, opp.Flavour, 0.40f, 0.60f, ElarionUi.ParchmentDim,
                     ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.04f, 0.66f);

            // Stake -> purse line in a recessed well across the card bottom.
            var stakeWell = AddImage(card.transform, "Stake", new Vector2(0.035f, 0.08f), new Vector2(0.66f, 0.34f), Track);
            AddLabel(stakeWell.transform,
                     $"Garrison 1 boss + {opp.GuardCount}   |   Stake {opp.Wager}  ->  Win {opp.WinPurse} SKR",
                     0f, 1f, ElarionUi.Gold, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.03f, 0.97f, bold: true);

            // RAID button — green when affordable, dimmed red gate otherwise.
            bool canAfford = ArenaWalletService.CanAfford(opp.Wager);
            string btnLabel = canAfford ? $"RAID   {opp.Wager} SKR" : "NEED MORE SKR";
            ButtonKind kind = canAfford ? ButtonKind.Confirm : ButtonKind.Danger;
            Color btnColor = canAfford
                ? new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.92f)
                : new Color(ElarionUi.Danger.r, ElarionUi.Danger.g, ElarionUi.Danger.b, 0.45f);

            var btn = AddButton(card.transform, btnLabel, new Vector2(0.68f, 0.97f),
                                new Vector2(0.10f, 0.42f), btnColor,
                                () => { if (canAfford) ConfirmRaid(opp); }, kind);
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
                RefreshHeader();
            }
        }

        private void RefreshHeader()
        {
            if (_headerSkr != null) _headerSkr.text = ArenaWalletService.Balance + " SKR";
            if (_headerRecord != null)
            {
                var rec = ArenaProgressStore.Current;
                _headerRecord.text = $"{rec.Wins}W / {rec.Losses}L   ({rec.Streak} streak)";
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

            _resultRoot = AddPanel(_ui.transform, new Vector2(0.12f, 0.28f), new Vector2(0.88f, 0.72f), deep: true);

            bool win = result == ArenaResult.Win;
            string banner = win ? "VICTORY" : "DEFEAT";
            Color bannerColor = win ? ElarionUi.Affordable : ElarionUi.Danger;
            AddLabel(_resultRoot.transform, banner, 0.78f, 0.95f, bannerColor, ElarionUi.FontTitle + 12,
                     TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, spacing: 8f, bold: true);
            AddRule(_resultRoot.transform, 0.76f, 0.18f, 0.82f);

            string oppName = opp != null ? opp.DisplayName : "opponent";
            AddLabel(_resultRoot.transform,
                     win ? $"You raided {oppName} and seized the purse."
                         : $"{oppName} held their walls. Your stake is forfeit.",
                     0.62f, 0.73f, ElarionUi.Parchment, ElarionUi.FontBody,
                     TMPro.TextAlignmentOptions.Center, 0.08f, 0.92f);

            // SKR delta in a recessed well.
            string deltaTxt = (skrDelta >= 0 ? "+" : "") + skrDelta + " SKR";
            var deltaWell = AddImage(_resultRoot.transform, "Delta", new Vector2(0.28f, 0.44f), new Vector2(0.72f, 0.58f), Track);
            AddLabel(deltaWell.transform, deltaTxt, 0f, 1f,
                     win ? ElarionUi.Gilt : ElarionUi.Danger, ElarionUi.FontTitle + 4,
                     TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);

            AddLabel(_resultRoot.transform, StatsLine(), 0.32f, 0.40f,
                     ElarionUi.Gold, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, spacing: 2f);

            AddButton(_resultRoot.transform, "Back to Arena", new Vector2(0.34f, 0.66f),
                      new Vector2(0.16f, 0.28f), ElarionUi.GoldButton, ShowEntry, ButtonKind.Gold);
            AddButton(_resultRoot.transform, "Close", new Vector2(0.34f, 0.66f),
                      new Vector2(0.04f, 0.13f), Glass, Close, ButtonKind.Neutral);
        }

        private static string StatsLine()
        {
            var rec = ArenaProgressStore.Current;
            return $"SKR {ArenaWalletService.Balance}      {rec.Wins}W / {rec.Losses}L      Streak {rec.Streak}";
        }

        // ====================================================================
        // SLEEK uGUI helpers — dark glass + thin gold rim (HudTheme recipe, local)
        // ====================================================================

        // A sleek panel: dark glass rounded rect + a single thin gold rule underline.
        private GameObject AddPanel(Transform parent, Vector2 min, Vector2 max, bool deep = false)
        {
            var p = AddImage(parent, "Panel", min, max, deep ? GlassDeep : Glass);
            AddRimUnderline(p);
            return p;
        }

        // A rounded glass image (sleek). rounded:false = a flat tinted quad (scrim).
        private static GameObject AddImage(Transform parent, string name, Vector2 min, Vector2 max,
            Color color, bool rounded = true)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = min; r.anchorMax = max;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            if (rounded)
            {
                var sprite = RoundedSprite;
                img.sprite = sprite;
                img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            }
            return go;
        }

        // Thin gold rule (hairline) at a given y across [x0,x1] — a HINT of runic gold.
        private void AddRule(Transform parent, float y, float x0, float x1)
        {
            var go = new GameObject("Rule", typeof(Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(x0, y); r.anchorMax = new Vector2(x1, y);
            r.offsetMin = new Vector2(0f, -1f); r.offsetMax = new Vector2(0f, 1f);
            var img = go.GetComponent<Image>();
            img.color = Accent;
            img.raycastTarget = false;
        }

        // A single faint gold rule hugging the panel's bottom edge (HudTheme.AddRim).
        private void AddRimUnderline(GameObject panel)
        {
            var go = new GameObject("Accent", typeof(Image));
            go.transform.SetParent(panel.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0f);
            rt.anchorMax = new Vector2(0.94f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 1.5f);
            rt.anchoredPosition = new Vector2(0f, 1.5f);
            var img = go.GetComponent<Image>();
            img.color = AccentSoft;
            img.raycastTarget = false;
            go.transform.SetAsLastSibling();
        }

        private static TMPro.TextMeshProUGUI AddLabel(Transform parent, string text, float y0, float y1,
            Color color, int size, TMPro.TextAlignmentOptions align,
            float x0 = 0.03f, float x1 = 0.97f, float spacing = 0f, bool bold = false)
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
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            if (bold) t.fontStyle = TMPro.FontStyles.Bold;
            return t;
        }

        private enum ButtonKind { Gold, Neutral, Confirm, Danger }

        // anchorX = (centerX, halfWidth); anchorY = (y0, y1) of the button rect.
        private Button AddButton(Transform parent, string label, Vector2 anchorX, Vector2 anchorY,
            Color bg, System.Action onClick, ButtonKind kind)
        {
            var go = new GameObject("Btn_" + label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f - anchorX.y, anchorY.x);
            r.anchorMax = new Vector2(0.5f + anchorX.y, anchorY.y);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = bg;
            var sprite = RoundedSprite;
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            StyleButtonColors(btn);
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            // Text on gold uses dark ink for contrast; everything else cream parchment.
            Color textColor = kind == ButtonKind.Gold ? ElarionUi.Ink : ElarionUi.Parchment;
            var tt = AddLabel(go.transform, label, 0f, 1f, textColor, ElarionUi.FontBody,
                              TMPro.TextAlignmentOptions.Center, 0f, 1f, spacing: 1f, bold: true);
            tt.raycastTarget = false;
            return btn;
        }

        // Clean subtle brightness feedback (no colour shift) — HudTheme.StyleButtonColors.
        private static void StyleButtonColors(Button button)
        {
            if (button == null) return;
            button.transition = Selectable.Transition.ColorTint;
            var cb = button.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
            cb.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.selectedColor    = cb.highlightedColor;
            cb.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            cb.colorMultiplier  = 1f;
            cb.fadeDuration     = 0.07f;
            button.colors = cb;
        }

        private static void DestroyScreen(ref GameObject screen)
        {
            if (screen != null) Destroy(screen);
            screen = null;
        }

        // ── Procedural rounded sprite (lazily built once; WebGL failure-safe) ──
        // Mirrors HudTheme.RoundedFrame: a 9-sliced white rounded-rect for crisp
        // modern corners. If the Texture2D build throws under WebGL we fall back to
        // null and Images render as flat tinted quads — the panel never blanks.
        private static Sprite _rounded;
        private static bool _roundedTried;
        private static Sprite RoundedSprite
        {
            get
            {
                if (!_roundedTried)
                {
                    _roundedTried = true;
                    try { _rounded = BuildRoundedSprite(); }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("[ArenaPanel] rounded sprite build failed (flat quad): " + e.Message);
                        _rounded = null;
                    }
                }
                return _rounded;
            }
        }

        private static Sprite BuildRoundedSprite()
        {
            const int size = 32;
            const int radius = 6;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedRectDistance(x, y, size, size, radius);
                    byte a = (byte)Mathf.Clamp((int)((1f - d) * 255f), 0, 255);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static float RoundedRectDistance(int x, int y, int w, int h, int radius)
        {
            float fx = x + 0.5f, fy = y + 0.5f;
            float dx = Mathf.Max(Mathf.Max(radius - fx, fx - (w - radius)), 0f);
            float dy = Mathf.Max(Mathf.Max(radius - fy, fy - (h - radius)), 0f);
            float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
            return Mathf.Clamp01(dist + 0.5f);
        }
    }
}
