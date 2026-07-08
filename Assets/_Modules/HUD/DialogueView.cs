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

using System.Collections;
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
            if (!DeNelle.Core.FeatureFlags.CustomDialogue)
            {
                // Instrumentation standard: a declined gate must trace, never skip silently —
                // with the flag off NOTHING can render a dialogue, and that must be readable.
                DeNelle.Core.Diagnostics.FlowTrace.Step("Dialogue",
                    "DialogueView.Bootstrap: ff.customdialogue OFF — view not spawned (no dialogue can render).");
                return;
            }
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

        /// <summary>Probe/observability surface (AutoPilot AssertDialogueChain): TRUE while a
        /// dialogue panel is built and visible (_ui alive + active). This is the P0 re-entrancy
        /// fix's testable invariant — after a Closed-callback synchronously chains into a
        /// successor dialogue, this must stay TRUE; a stale close tearing the successor's panel
        /// down (the frozen-build-mode root) flips it false.</summary>
        public bool IsShowing => _ui != null && _ui.activeSelf;

        private void OnEnable() { DialogueService.Opened += OnOpened; }
        private void OnDisable() { DialogueService.Opened -= OnOpened; }

        // P0 RE-ENTRANCY FIX (owner "still cant do the tower", RCA 2026-07-08): when a dialogue's
        // Closed invocation-list SYNCHRONOUSLY chains into the NEXT dialogue (the tutorial's
        // dialogue.ended -> STEP-ENTER -> Play), the STALE dialogue's Closed handler runs AFTER
        // this view has already rebound to the successor — it destroyed the successor's just-built
        // panel and unbound its handlers, leaving the new VM alive-but-headless: Ended never fired,
        // HeroLocomotion.InputSuppressed stayed TRUE forever, and BuildModeController.Update froze
        // at its first gate (zero PlaceConfirm evaluations — her captured session). The Closed
        // handler is now bound PER-VM and ignores any close for a VM this view no longer shows.
        private System.Action _vmClosedHandler;

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
            var closedFor = vm;   // identity capture — this handler belongs to THIS vm only
            _vmClosedHandler = () => OnClosedFor(closedFor);
            _vm.Closed += _vmClosedHandler;
            BuildUi();
            Repaint();
        }

        private void OnClosedFor(DialogueViewModel vm)
        {
            if (!ReferenceEquals(vm, _vm))
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Dialogue",
                    "stale Closed from a superseded dialogue IGNORED — the successor's panel survives " +
                    "(re-entrancy guard; was the frozen-build-mode root).");
                return;
            }
            OnClosed();
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
            if (_vm != null)
            {
                _vm.Changed -= Repaint;
                if (_vmClosedHandler != null) _vm.Closed -= _vmClosedHandler;
                _vmClosedHandler = null;
                _vm = null;
            }
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

            // DIALOGUE TEMPLATE — F8-1/F8-5 fix (RCA_DIALOGUE_DOUBLE_FRAME_2026-07-07, OPTION A):
            // the panel is built on the WINDOW-family FrameCore (Core_Panel, portrait 1210x1815)
            // instead of the landscape FrameDialogue STRIP — the strip art stretched tall was
            // rectangle #1 of the proven "frame inside a frame" read. FrameCore is drawn for a
            // tall reading window, so the frame SUPPLIES the chrome (canon §4): its measured kit
            // zones + the factory's ObsidianFill body plate replace the deleted per-screen
            // DialogueInterior patch plate (rectangle #2), and the factory's close-band
            // reservation + footer relocation keep content geometrically above the ONE shared
            // Close (rectangle #3 no longer collides). NOTE: panel_window/panel_window_dark are
            // `panel/` role sprites — BuildObsidianPanel resolves frameName under RoleFrame, so
            // they'd fall to the zone-less PROCEDURAL path; FrameCore IS the window frame here.
            // FrameDialogue stays in the catalog for any true bottom-strip use.
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
                () => _vm?.Close(), withBackdrop: false, frameName: RpgUiCatalog.FrameCore);
            _box = chrome.root.GetComponent<RectTransform>();

            var contentRoot = chrome.content.transform;

            // Kit drop-zones (the protected class — the factory's close-band reservation only
            // guards content INSIDE layout.*; laying custom fractions on chrome.content is the
            // documented "unprotected class" failure). Fallback fractions mirror the FrameCore
            // anatomy for the PROCEDURAL path (frame art absent → chrome.layout is null).
            var layout = chrome.layout;
            var headerZone   = (layout != null && layout.header != null) ? layout.header
                : MakeZone(contentRoot, "SpeakerZone",  new Vector2(0.24f, 0.90f),  new Vector2(0.88f, 0.972f));
            var bodyZone     = (layout != null && layout.body != null) ? layout.body
                : MakeZone(contentRoot, "BodyZone",     new Vector2(0.055f, 0.41f), new Vector2(0.945f, 0.855f));
            var footerZone   = (layout != null && layout.footer != null) ? layout.footer
                : MakeZone(contentRoot, "ContinueZone", new Vector2(0.08f, 0.33f),  new Vector2(0.92f, 0.395f));
            var portraitHost = (layout != null && layout.medallion != null) ? layout.medallion
                : MakeZone(contentRoot, "PortraitHost", new Vector2(0.037f, 0.868f), new Vector2(0.220f, 0.988f));

            // The medallion socket hosts the SPEAKER PORTRAIT (refreshed per Repaint), not the
            // factory's generic crest emblem — hide the fallback emblem so the two never stack.
            var emblem = portraitHost.Find("MedallionEmblem");
            if (emblem != null) emblem.gameObject.SetActive(false);

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
            // the kit builds the shared CloseButton BEFORE this catcher — raise the Close to
            // the top of the panel subtree so it stays clickable above the catcher.
            // OPTION A (F8-1/F8-5): the per-view anchor override is REMOVED — the factory's
            // SeatSharedCloseInside owns the seat (canonical 360x120, bottom-centre band), and
            // the factory's close-band reservation already ends layout.body/footer above it.
            if (chrome.close != null)
                chrome.close.transform.SetAsLastSibling();

            // Speaker name → the kit HEADER zone (left, gilt) with the guild/shop AFFILIATION
            // as a dim sub-line beneath it (owner-ratified card standard: name + affiliation +
            // portrait on every NPC card). Body text → body zone. (Drop, no re-style.)
            // FrameCore's header band is thin (~7% of the panel) — FitSingleLine bounds both
            // lines (auto-size + ellipsis, §1.14) so they can never clip in the band.
            _speaker = MakeLabel(headerZone, "Speaker", new Vector2(0f, 0.45f), Vector2.one,
                24, ElarionUi.Gilt, TMPro.FontStyles.Bold, TMPro.TextAlignmentOptions.BottomLeft);
            ElarionUiKit.FitSingleLine(_speaker);
            _affiliation = MakeLabel(headerZone, "Affiliation", Vector2.zero, new Vector2(1f, 0.45f),
                13, ElarionUi.ParchmentDim, TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.TopLeft);
            ElarionUiKit.FitSingleLine(_affiliation);
            // SCROLLABLE BODY (owner 2026-07-06: "in case there is more text, scrollable"):
            // the body zone hosts the §1.14 kit scroll zone (vertical, clamped, auto-hide
            // scrollbar). Longer passages scroll instead of overflowing.
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
            // with no control fails the no-dead-interaction law. The affordance is a REAL
            // labeled kit button (Continue chip). OPTION A: it drops into the kit FOOTER zone
            // — the factory relocates that band to start just ABOVE the shared Close box
            // (footer relocation, close-band reservation), so the chip sits between the body
            // and the Close by RESERVED geometry, not hand fractions. Repaint keeps driving
            // its visibility through _tapHint (hidden while options show).
            var contBtn = ElarionUiKit.Button(footerZone, "Continue", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.26f, 0f), new Vector2(0.74f, 1f), OnBoxTapped);
            if (contBtn != null)
            {
                var contLbl = contBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                if (contLbl == null && contBtn.transform.parent != null)
                    contLbl = contBtn.transform.parent.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                if (contLbl != null) { contLbl.text = "Continue"; ElarionUiKit.FitSingleLine(contLbl); }
            }
            _tapHint = contBtn != null ? contBtn.gameObject : null;

            // Speaker portrait → the frame's MEDALLION socket (layout.medallion — FrameCore's
            // top-left circle socket; the factory's crest emblem is hidden above so the two
            // never stack). The actual sprite is resolved + REFRESHED every Repaint
            // (RefreshPortrait), because a per-node `portrait` command can change the speaker
            // portrait mid-conversation. Built once here, repainted live.
            _portrait = ElarionUiKit.Portrait(portraitHost,
                ResolveSpeakerPortrait(_vm != null ? _vm.Speaker : null, out _), active: false);
            if (_portrait != null && _portrait.image != null) _portrait.image.raycastTarget = false;
            // OWNER F8 t=322: "can we lose yellow circle around image?" — the kit's
            // Portrait always adds a gold Ring overlay; hide it here (kit untouched —
            // HUD/battle portraits keep theirs). The portrait reads plain on the plate.
            if (_portrait != null && _portrait.ring != null) _portrait.ring.gameObject.SetActive(false);

            // Options column — INSIDE the kit BODY zone (the protected class: the factory
            // reservation already ends this zone above the Close band, so option plates can
            // never collide with the shared Close). Spans the body's lower half, growing UP
            // (built on demand; the Continue chip hides while options show).
            var col = new GameObject("Options");
            col.transform.SetParent(bodyZone, false);
            _optionsCol = col.AddComponent<RectTransform>();
            _optionsCol.anchorMin = new Vector2(0.05f, 0f);
            _optionsCol.anchorMax = new Vector2(0.95f, 0.60f);
            _optionsCol.offsetMin = Vector2.zero; _optionsCol.offsetMax = Vector2.zero;
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8; vlg.childControlHeight = true; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;
            vlg.childAlignment = TextAnchor.LowerCenter;

            // F8-1/F8-5 instrumentation (kept per the RCA): log the REAL post-layout geometry
            // of the new kit zones — panel / body / footer / close / continue — once per build.
            // Runs at the END of BuildUi so `continue` is a real rect (the old call fired
            // before the chip existed and honestly logged `<null>`). SYNCHRONOUS
            // (ForceUpdateCanvases) — headless-safe under -nographics.
            TraceDialogueLayout(chrome, bodyZone, footerZone);
        }

        // F8-1/F8-5 (kept per the RCA, re-pointed at the OPTION A anatomy): dump the REAL
        // post-layout geometry of the panel / kit body zone / kit footer zone / Close /
        // Continue, in fractions of the PANEL rect — proves the factory's close-band
        // reservation is protecting the dropped content. ForceUpdateCanvases makes the world
        // corners valid synchronously (headless-safe — no reliance on a render frame).
        private void TraceDialogueLayout(ElarionUiKit.PanelChrome chrome,
            RectTransform bodyZone, RectTransform footerZone)
        {
            if (chrome == null || chrome.root == null) return;
            Canvas.ForceUpdateCanvases();
            var panelRt = chrome.root.GetComponent<RectTransform>();
            var corners = new Vector3[4];
            panelRt.GetWorldCorners(corners);
            Vector3 pMin = corners[0], pMax = corners[2];
            float pw = Mathf.Max(0.001f, pMax.x - pMin.x), ph = Mathf.Max(0.001f, pMax.y - pMin.y);

            string Frac(RectTransform rt)
            {
                if (rt == null) return "<null>";
                rt.GetWorldCorners(corners);
                return string.Format("x {0:F3}-{1:F3} y {2:F3}-{3:F3}",
                    (corners[0].x - pMin.x) / pw, (corners[2].x - pMin.x) / pw,
                    (corners[0].y - pMin.y) / ph, (corners[2].y - pMin.y) / ph);
            }

            var closeRt = chrome.close != null ? chrome.close.transform as RectTransform : null;
            var contRt = _tapHint != null ? _tapHint.transform as RectTransform : null;
            DeNelle.Core.Diagnostics.FlowTrace.Step("DlgLayout",
                "panel worldY " + pMin.y.ToString("F1") + ".." + pMax.y.ToString("F1") +
                " | body " + Frac(bodyZone) +
                " | footer " + Frac(footerZone) +
                " | close " + Frac(closeRt) +
                (closeRt != null
                    ? " (pivot=" + closeRt.pivot.ToString("F2") + " anchors=" + closeRt.anchorMin.ToString("F3") +
                      " sizeDelta=" + closeRt.sizeDelta.ToString("F0") + " parent='" + closeRt.parent.name + "')"
                    : "") +
                " | continue " + Frac(contRt));
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

        // Bare fraction-anchored rect inside the panel interior — FALLBACK ONLY, for the
        // procedural chrome path (frame art absent → chrome.layout is null). On the normal
        // FrameCore path the view uses the kit's layout.{header,body,footer,medallion} zones.
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
