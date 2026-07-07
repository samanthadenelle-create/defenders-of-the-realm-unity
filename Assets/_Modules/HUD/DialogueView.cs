// =============================================================================
// DialogueView (DeNelle.HUD) — the dumb uGUI skin for OUR dialogue (WO-455).
// -----------------------------------------------------------------------------
// Code-built uGUI (canon: NOT UIDocument), styled with ElarionUiKit so it matches
// every other panel. Binds to a DialogueViewModel and renders it: a centered reading
// panel (owner ruling 2026-07-06 — raised clear of the HUD control zones, scrollable
// body) with speaker + text (tap to advance), and an option list at a choice.
// The VIEW holds no game state — it reads the VM and calls Advance/Choose only.
// Self-bootstraps DDOL behind FeatureFlags.CustomDialogue.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Dialogue;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class DialogueView : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!DeNelle.Core.FeatureFlags.CustomDialogue) return; // migration flag (default off)
            var go = new GameObject("DialogueView");
            DontDestroyOnLoad(go);
            go.AddComponent<DialogueView>();
        }

        private DialogueViewModel _vm;
        private GameObject _ui;
        private TMPro.TextMeshProUGUI _speaker;
        private TMPro.TextMeshProUGUI _affiliation;   // guild/shop sub-line under the name (card standard)
        private TMPro.TextMeshProUGUI _body;
        private RectTransform _box;       // the dialogue box (tap to advance)
        private RectTransform _optionsCol;
        private GameObject _tapHint;
        private ElarionUiKit.PortraitHandle _portrait;   // medallion portrait disc (refreshed per Repaint)

        // F8 2026-07-06 (t=328): the dialogue now routes through the modal arbiter
        // (mirrors RumorBoardPanel) so the click-guard classifies its TapAdvance
        // catcher as an intentional modal cover (was 7x false CLICK-BLOCKED per
        // fleet) and world prompts suppress while a conversation owns the screen.
        // Registered BATTLE-ALLOWED (WO-437): dialogue is scripted narrative
        // (tutorial intro/outro around fights, companion meetings, vendor talk) —
        // the battle-lock must never silently tear a conversation down mid-script.
        private PanelHandle _handle;
        private bool _arbiterNotified;

        private void OnEnable() { DialogueService.Opened += OnOpened; }
        private void OnDisable() { DialogueService.Opened -= OnOpened; }

        private void OnOpened(DialogueViewModel vm)
        {
            if (_vm != null) Unbind();
            // A per-node `portrait` command override is scoped to ITS dialogue: clear the sticky
            // static at every open so the previous conversation's forced portrait can't leak onto
            // this one (the speakers block is now the data-driven default; the command re-fires
            // within a dialogue when an override is authored).
            DeNelle.Core.DialoguePortrait.Forced = null;
            _lastCardKey = null;
            _vm = vm;
            _vm.Changed += Repaint;
            _vm.Closed += OnClosed;
            BuildUi();
            Repaint();
        }

        private void OnClosed()
        {
            Unbind();
            _portrait = null;
            if (_ui != null) { Destroy(_ui); _ui = null; }
            if (_arbiterNotified) { PanelManager.NotifyClosed(_handle); _arbiterNotified = false; }
        }

        private void Unbind()
        {
            if (_vm != null) { _vm.Changed -= Repaint; _vm.Closed -= OnClosed; _vm = null; }
        }

        // ── Build the bottom dialogue box ────────────────────────────────────────
        private void BuildUi()
        {
            if (_ui != null) Destroy(_ui);
            _ui = new GameObject("DialogueViewUI");
            _ui.transform.SetParent(transform, false);

            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // F8 2026-07-06 (t=328, "dialogue comes up under hud"): 900 sat BELOW the
            // HUD kit chrome (HudAreasHost canvas = 4000), so the panel rendered under
            // the bottom action bar. Deliberate band: above the gameplay HUD kit (4000)
            // and the Echo workforce HUD (4600), below the battle overlay (5000) and
            // hard modals (30000+).
            canvas.sortingOrder = 4800;
            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            _ui.AddComponent<GraphicRaycaster>();

            // DIALOGUE TEMPLATE (WO-582): the bottom strip is built from the ONE master frame
            // factory using the Blink Dialogue_Panel frame + its pre-styled drop-zones. The VIEW
            // re-styles nothing — it drops the model (speaker / body / choices) into the zones.
            // OWNER RULING 2026-07-06 ("not over top of HUD controls; moved up, readable on
            // mobile; larger; scrollable"): a centered READING PANEL that clears every HUD
            // control zone (HudAreasHost actuals — ActionBar top y=0.150 x0.28-0.72,
            // MoveCluster top y=0.330 x<=0.270, Dock top y=0.430 x<=0.230, ActionRail top
            // y=0.420 x>=0.780, TargetInfo bottom y=0.660). x 0.29-0.71 clears the side
            // thumb clusters with margin; y 0.20 clears the action bar by 0.05; y 0.62
            // stays under TargetInfo. Both HUD areas and this panel anchor by fraction of
            // screen, so the clearance holds at 16:9 and 19.5:9 alike.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "",
                new Vector2(0.29f, 0.20f), new Vector2(0.71f, 0.62f),
                () => _vm?.Close(), withBackdrop: false, frameName: RpgUiCatalog.FrameDialogue);
            _box = chrome.root.GetComponent<RectTransform>();

            // SWEEP 9413 (panel_Dialogue.png: "Brom / Town Crier" + text floated over WORLD
            // geometry — no fill behind them): the FrameDialogue drop-zones are pixel-measured
            // for the OLD LANDSCAPE STRIP art (medallion = a full-height far-left column,
            // header a mid-band at y 0.64-0.93, body a right sub-rect), and only the BODY zone
            // receives a kit ZoneBacking plate — on this tall reading panel those strip
            // fractions scatter the content and leave header/hint on the stretched art's
            // transparent centre. The view now lays out its OWN zones sized for the reading
            // panel, over ONE opaque obsidian plate spanning the whole interior, so every
            // element reads on solid fill inside the single frame. The strip-measured kit
            // zones (and their crest emblem / partial plate) are deactivated on THIS instance.
            if (chrome.layout != null)
            {
                if (chrome.layout.header != null) chrome.layout.header.gameObject.SetActive(false);
                if (chrome.layout.body != null) chrome.layout.body.gameObject.SetActive(false);
                if (chrome.layout.medallion != null) chrome.layout.medallion.gameObject.SetActive(false);
            }

            var contentRoot = chrome.content.transform;
            var interior = new GameObject("DialogueInterior", typeof(Image));
            interior.transform.SetParent(contentRoot, false);
            var irt = interior.GetComponent<RectTransform>();
            // SWEEP 2026-07-06 (fresh 1280x720 capture): the plate at 0.045/0.055 covered the
            // frame's painted interior ornament so the whole panel read as a raw black rectangle
            // with frame edges sticking out. Inset the plate INSIDE the frame border (the
            // landscape-strip art's stretched top/bottom borders are thick on this tall panel).
            irt.anchorMin = new Vector2(0.06f, 0.10f);
            irt.anchorMax = new Vector2(0.94f, 0.935f);
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            var iimg = interior.GetComponent<Image>();
            iimg.color = ElarionUiKit.ObsidianFill;   // the kit's near-black panel fill (opaque)
            iimg.raycastTarget = false;
            interior.transform.SetAsFirstSibling();   // behind every zone, above the frame art

            // Own zones (fractions of the panel interior): portrait top-left, speaker header
            // beside it, reading body below, hint sliver at the body's foot — all INSIDE the
            // one frame, all over the opaque plate.
            // SWEEP 2026-07-06 re-stack (top → bottom): portrait+speaker band, reading body,
            // Continue chip band, then the canonical Close — every band inside the interior
            // plate, none overlapping. The old body (0.24–0.70) left a giant dead band above
            // a Close whose fixed 120-unit box actually spans y 0.12–0.385 of this 453-unit
            // panel on a landscape screen (the kit reserves against the PORTRAIT reference).
            var portraitHost = MakeZone(contentRoot, "PortraitHost", new Vector2(0.075f, 0.71f), new Vector2(0.22f, 0.91f));
            var headerZone   = MakeZone(contentRoot, "SpeakerZone",  new Vector2(0.24f, 0.71f), new Vector2(0.93f, 0.91f));
            var bodyZone     = MakeZone(contentRoot, "BodyZone",     new Vector2(0.075f, 0.515f), new Vector2(0.925f, 0.69f));

            // Tap-to-advance: a transparent button filling the BODY ZONE ONLY (advances lines,
            // not choices). Deliberately contained to the panel — never a full-screen catcher.
            var tapGo = new GameObject("TapAdvance", typeof(Image), typeof(Button));
            tapGo.transform.SetParent(bodyZone, false);
            var trt = tapGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            tapGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var tapBtn = tapGo.GetComponent<Button>();
            tapBtn.transition = Selectable.Transition.None;
            tapBtn.onClick.AddListener(OnBoxTapped);

            // F8 fleet capture ("CLICK-BLOCKED: 'CloseButton' covered by 'TapAdvance'" x7):
            // the kit builds the shared CloseButton BEFORE this catcher, and the frame's
            // measured close zone overlaps the body zone — so the catcher rendered (and
            // raycast) on top of the panel's own Close. Raise the Close to the top of the
            // panel subtree so it stays clickable above the catcher.
            //
            // 2026-07-06 owner ruling (supersedes the interim compact-Close override): the
            // CANONICAL Close size/seat stands on the reading panel. Fresh-capture sweep: at
            // y=0.075 the fixed box's bottom still sat inside the stretched painted bottom
            // border (~0.10 of this panel) — seat it at y=0.12, just above the interior
            // plate's floor (canonical 360x120 size + bottom-centre law preserved; the box
            // then spans y 0.12–0.385, and the Continue chip band starts at 0.40 above it).
            if (chrome.close != null)
            {
                var closeRt = chrome.close.transform as RectTransform;
                if (closeRt != null)
                {
                    closeRt.anchorMin = new Vector2(0.5f, 0.12f);
                    closeRt.anchorMax = new Vector2(0.5f, 0.12f);
                }
                chrome.close.transform.SetAsLastSibling();
            }

            // Speaker name → header zone (left, gilt) with the guild/shop AFFILIATION as a
            // dim sub-line beneath it (owner-ratified card standard: name + affiliation +
            // portrait on every NPC card). Body text → body zone. (Drop, no re-style.)
            _speaker = MakeLabel(headerZone, "Speaker", new Vector2(0f, 0.42f), Vector2.one,
                24, ElarionUi.Gilt, TMPro.FontStyles.Bold, TMPro.TextAlignmentOptions.BottomLeft);
            _affiliation = MakeLabel(headerZone, "Affiliation", Vector2.zero, new Vector2(1f, 0.42f),
                13, ElarionUi.ParchmentDim, TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.TopLeft);
            // SCROLLABLE BODY (owner 2026-07-06: "in case there is more text, scrollable"):
            // the upper region of the body zone hosts the §1.14 kit scroll zone (vertical,
            // clamped, auto-hide scrollbar); the bottom sliver keeps the tap hint clear of
            // the frame's close band. Longer passages scroll instead of overflowing.
            var wellGo = new GameObject("BodyWell", typeof(RectTransform));
            wellGo.transform.SetParent(bodyZone, false);
            var wellRt = wellGo.GetComponent<RectTransform>();
            // Full body zone — the old 0.18 hint sliver is retired (the Continue chip below
            // has its own band between the body and the Close).
            wellRt.anchorMin = Vector2.zero; wellRt.anchorMax = Vector2.one;
            wellRt.offsetMin = Vector2.zero; wellRt.offsetMax = Vector2.zero;
            var scrollZone = ElarionUiKit.MakeScrollZone(wellGo.transform, spacing: 0f, padding: 8);

            _body = MakeLabel(scrollZone.content, "Body", Vector2.zero, Vector2.one,
                17, ElarionUi.Parchment, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            // The scroll column deliberately does NOT control child height (§1.14 kit note —
            // the captured PartyShop collapse, runs 9400/9401), so the label carries its own:
            // a vertical ContentSizeFitter grows it with its text, and the column's own
            // fitter sums that into a scrollable content height.
            var bodyFit = _body.gameObject.AddComponent<ContentSizeFitter>();
            bodyFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            bodyFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            // §1.14 belt-and-braces: wrap + truncate protection on the block. min=max keeps
            // the reading size deterministic (the scroll well, not shrinking text, absorbs
            // long passages).
            ElarionUiKit.FitBlock(_body, minSize: 17f, maxSize: 17f);

            // Tap-to-advance INSIDE the scrolling well: the viewport's raycast surface
            // doubles as the click target (Button = click, ScrollRect = drag; uGUI splits
            // them at the drag threshold) — tapping the text advances, dragging it scrolls.
            var vpBtn = scrollZone.viewport.gameObject.AddComponent<Button>();
            vpBtn.transition = Selectable.Transition.None;
            vpBtn.onClick.AddListener(OnBoxTapped);

            // SWEEP 2026-07-06 (supersedes the bare "tap to continue" italic hint): the fresh
            // capture showed NO visible advance affordance at all — a tap-anywhere contract
            // with no control fails the no-dead-interaction law. The affordance is now a REAL
            // labeled kit button (Continue chip) in its own band between the body and the
            // Close; tapping the body text still advances too (catcher above). Repaint keeps
            // driving its visibility through _tapHint (hidden while options show).
            var contBtn = ElarionUiKit.Button(contentRoot, "Continue", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.30f, 0.40f), new Vector2(0.70f, 0.495f), OnBoxTapped);
            if (contBtn != null)
            {
                var contLbl = contBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                if (contLbl == null && contBtn.transform.parent != null)
                    contLbl = contBtn.transform.parent.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                if (contLbl != null) { contLbl.text = "Continue"; ElarionUiKit.FitSingleLine(contLbl); }
            }
            _tapHint = contBtn != null ? contBtn.gameObject : null;

            // Speaker portrait → the frame's medallion socket (if present). The actual sprite is
            // resolved + REFRESHED every Repaint (RefreshPortrait), because a per-node `portrait`
            // command can change the speaker portrait mid-conversation. Built once here, repainted live.
            // Portrait → the view's OWN top-left socket (the strip-measured kit medallion —
            // a full-height left column on this tall panel — is deactivated above).
            _portrait = ElarionUiKit.Portrait(portraitHost,
                ResolveSpeakerPortrait(_vm != null ? _vm.Speaker : null, out _), active: false);
            if (_portrait != null && _portrait.image != null) _portrait.image.raycastTarget = false;
            // OWNER F8 t=322: "can we lose yellow circle around image?" — the kit's
            // Portrait always adds a gold Ring overlay; hide it here (kit untouched —
            // HUD/battle portraits keep theirs). The portrait reads plain on the plate.
            if (_portrait != null && _portrait.ring != null) _portrait.ring.gameObject.SetActive(false);

            // Options column — INSIDE the panel (fresh-capture sweep: the old screen-anchored
            // column at screen y 0.30–0.52 laid its plates over the panel's Close band). It
            // now parents to the panel content, spanning the body + Continue bands (built on
            // demand; the Continue chip hides while options show, so nothing collides).
            var col = new GameObject("Options");
            col.transform.SetParent(contentRoot, false);
            _optionsCol = col.AddComponent<RectTransform>();
            _optionsCol.anchorMin = new Vector2(0.10f, 0.40f);
            _optionsCol.anchorMax = new Vector2(0.90f, 0.70f);
            _optionsCol.offsetMin = Vector2.zero; _optionsCol.offsetMax = Vector2.zero;
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8; vlg.childControlHeight = true; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;
            vlg.childAlignment = TextAnchor.LowerCenter;
        }

        private void OnBoxTapped()
        {
            if (_vm == null) return;
            if (!_vm.ShowingOptions) _vm.Advance();   // tapping the box advances lines, not choices
        }

        // ── Render the VM ────────────────────────────────────────────────────────
        private void Repaint()
        {
            if (_vm == null || _ui == null) return;
            bool open = _vm.IsOpen;
            _ui.SetActive(open);
            if (!open) return;

            // Register + announce to the modal arbiter on the FIRST visible paint.
            // (DialogueService raises Opened BEFORE vm.Begin(), so IsOpen is still false
            // inside OnOpened — notifying there would trip the arbiter's isOpen-verify
            // false-Fail. A command-only dialogue that closes before its first open
            // paint never registers, correctly.)
            if (!_arbiterNotified)
            {
                if (_handle == null)
                    _handle = PanelManager.RegisterBattleAllowed("Dialogue",
                        () => _vm?.Close(), () => _ui != null && _ui.activeSelf);
                _arbiterNotified = true;
                PanelManager.NotifyOpened(_handle);
            }

            if (_speaker != null) { _speaker.text = _vm.Speaker; _speaker.gameObject.SetActive(!string.IsNullOrEmpty(_vm.Speaker)); }
            if (_affiliation != null)
            {
                var rec = DialogueCatalog.FindSpeaker(_vm.Speaker);
                string aff = rec != null ? rec.Affiliation : null;
                _affiliation.text = aff ?? "";
                _affiliation.gameObject.SetActive(!string.IsNullOrEmpty(aff));
            }
            if (_body != null) _body.text = _vm.Text;
            RefreshPortrait();

            BuildOptions();
            if (_tapHint != null) _tapHint.SetActive(!_vm.ShowingOptions && !string.IsNullOrEmpty(_vm.Text));
        }

        private void BuildOptions()
        {
            if (_optionsCol == null) return;
            for (int i = _optionsCol.childCount - 1; i >= 0; i--) Destroy(_optionsCol.GetChild(i).gameObject);
            if (!_vm.ShowingOptions) return;

            var labels = _vm.OptionLabels;
            for (int i = 0; i < labels.Count; i++)
            {
                int idx = i;
                var go = new GameObject("Opt" + i, typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(_optionsCol, false);
                go.GetComponent<LayoutElement>().minHeight = 48;
                var b = ElarionUi.PanelStone;
                go.GetComponent<Image>().color = new Color(b.r, b.g, b.b, 0.96f);
                go.GetComponent<Button>().onClick.AddListener(() => _vm?.Choose(idx));

                var lbl = MakeLabel(go.transform, "L", new Vector2(0.04f, 0f), new Vector2(0.96f, 1f),
                    15, ElarionUi.Parchment, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.Left);
                lbl.text = labels[i];
                lbl.raycastTarget = false;
            }
        }

        // ── Speaker → portrait mapping (WO-583; speakers block, card standard 2026-07-02) ──
        // The card is DATA-DRIVEN: the catalog's top-level `speakers` block declares
        // { name, affiliation, portrait } per speaker. Resolve by priority, never blank /
        // never throw:
        //   1) an AUTHORED per-node `portrait` command (back-compat OVERRIDE) — sets
        //      DeNelle.Core.DialoguePortrait.Forced to a Resources sprite path;
        //   2) the speakers-block record's portrait path (the data-driven default);
        //   3) the speaker name mapped to a class portrait (Knight/Ranger/Wizard/Healer);
        //   4) null → RefreshPortrait draws the styled SILHOUETTE (never a raw tinted disc).
        private static Sprite ResolveSpeakerPortrait(string speaker, out string source)
        {
            string forced = DeNelle.Core.DialoguePortrait.Forced;
            if (!string.IsNullOrEmpty(forced))
            {
                var sp = Resources.Load<Sprite>(forced);
                if (sp != null) { source = forced + " (command)"; return sp; }
            }
            var rec = DialogueCatalog.FindSpeaker(speaker);
            if (rec != null && !string.IsNullOrEmpty(rec.Portrait))
            {
                var sp = Resources.Load<Sprite>(rec.Portrait);
                if (sp != null) { source = rec.Portrait; return sp; }
            }
            var cls = ElarionUiKit.PortraitForClass(speaker);
            if (cls != null) { source = "class:" + speaker; return cls; }
            source = "silhouette";
            return null;
        }

        // Repaint the medallion portrait from the current speaker / speakers block / forced
        // override. Called every Repaint so a per-node portrait command (or a speaker change)
        // updates the socket live; a speaker with no resolvable art gets the styled hooded
        // silhouette — NEVER the raw tan placeholder disc (the "Sylas yellow blank").
        private string _lastCardKey;   // one FlowTrace card line per speaker, not per tap
        private void RefreshPortrait()
        {
            if (_portrait == null || _portrait.image == null) return;
            string speaker = _vm != null ? _vm.Speaker : null;
            var sp = ResolveSpeakerPortrait(speaker, out string source);
            if (sp != null)
            {
                _portrait.image.sprite = sp;
                _portrait.image.color = Color.white;
                _portrait.image.preserveAspect = true;
            }
            else
            {
                _portrait.image.sprite = SilhouetteSprite;
                _portrait.image.color = Color.white;
                _portrait.image.preserveAspect = true;
            }

            var rec = DialogueCatalog.FindSpeaker(speaker);
            string card = (speaker ?? "<narration>") + "|" + source;
            if (card != _lastCardKey)
            {
                _lastCardKey = card;
                DeNelle.Core.Diagnostics.FlowTrace.Step("Dialogue",
                    $"card {(string.IsNullOrEmpty(speaker) ? "<narration>" : speaker)}: " +
                    $"affiliation={(rec != null && !string.IsNullOrEmpty(rec.Affiliation) ? rec.Affiliation : "<none>")} " +
                    $"portrait={source}");
            }
        }

        // ── Silhouette placeholder (card standard: styled, never a flat tinted circle) ───
        // Procedurally drawn ONCE into a Texture2D and cached: a dark obsidian-toned disc
        // carrying a near-black hooded-figure bust (hood peak + head + shoulders), so an
        // unportraited speaker (Sylas / Brom / Sable) reads as "a person, art pending"
        // instead of a raw color quad. Pure UnityEngine drawing — no kit change needed.
        private static Sprite _silhouette;
        private static Sprite SilhouetteSprite
        {
            get
            {
                if (_silhouette == null) _silhouette = BuildSilhouetteSprite();
                return _silhouette;
            }
        }

        private static Sprite BuildSilhouetteSprite()
        {
            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            var clear  = new Color(0f, 0f, 0f, 0f);
            var disc   = new Color(0.16f, 0.15f, 0.19f, 1f);   // dark obsidian slate
            var figure = new Color(0.045f, 0.04f, 0.06f, 1f);  // near-black hooded figure

            float c = (size - 1) * 0.5f;
            float rDisc = size * 0.5f - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    if (dx * dx + dy * dy > rDisc * rDisc) { tex.SetPixel(x, y, clear); continue; }

                    // Normalized coords (0..1, y up).
                    float nx = x / (float)(size - 1);
                    float ny = y / (float)(size - 1);

                    bool inFigure = false;
                    // Head: circle centred just above middle.
                    float hx = nx - 0.5f, hy = ny - 0.58f;
                    if (hx * hx + hy * hy <= 0.155f * 0.155f) inFigure = true;
                    // Hood peak: triangle rising above the head to a point.
                    if (!inFigure && ny >= 0.58f && ny <= 0.82f)
                    {
                        float half = 0.16f * (1f - (ny - 0.58f) / 0.24f);   // narrows to the peak
                        if (Mathf.Abs(nx - 0.5f) <= half) inFigure = true;
                    }
                    // Shoulders: wide ellipse low in the disc.
                    if (!inFigure)
                    {
                        float sx = (nx - 0.5f) / 0.34f, sy = (ny - 0.12f) / 0.30f;
                        if (sx * sx + sy * sy <= 1f && ny < 0.42f) inFigure = true;
                    }

                    tex.SetPixel(x, y, inFigure ? figure : disc);
                }
            }
            tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sp.name = "DialogueSilhouette";
            return sp;
        }

        // Bare fraction-anchored rect inside the panel interior (the view's own drop-zones —
        // the FrameDialogue kit zones are strip-measured and unusable on the reading panel).
        private static RectTransform MakeZone(Transform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static TMPro.TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 aMin, Vector2 aMax,
            int size, Color col, TMPro.FontStyles style, TMPro.TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.fontSize = size; t.color = col; t.fontStyle = style; t.alignment = align;
            t.textWrappingMode = TMPro.TextWrappingModes.Normal; t.raycastTarget = false;
            return t;
        }
    }
}
