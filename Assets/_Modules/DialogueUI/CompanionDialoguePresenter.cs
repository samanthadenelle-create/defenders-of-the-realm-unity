// =============================================================================
// CompanionDialoguePresenter — adds speaker portraits to ANY dialogue line.
// -----------------------------------------------------------------------------
// The ClassicRPG RPGDialoguePresenter shows a per-line icon ONLY when the Yarn
// line carries a static `#icon:<spriteName>` metadata tag (RPGDialoguePresenter
// RunLineAsync → Resources.Load<Sprite>). Our speakers are dynamic ($companionName
// resolves to Sylas/Grom/Elara/Thrain), and Yarn hashtags are NOT interpolated —
// so a static tag can't carry the right portrait.
//
// Fix WITHOUT forking the package: subclass the presenter and, just before the
// base draws the line, INJECT an `icon:HeroPortraits/<CharacterName>` entry into
// the (public, mutable) LocalizedLine.Metadata array. The base presenter then
// resolves + shows the portrait through its own code path. Because it keys off
// the line's CharacterName, this works for EVERY speaker automatically — the
// companion today, and vendors / NPCs / lore once those route through Yarn (see
// the "dialogue is the interaction layer" decision).
//
// DELIBERATE ASSEMBLY HOME: this lives in its OWN DeNelle.DialogueUI assembly —
// NOT DeNelle.Village — so the ClassicRPG UI-addon dependency stays isolated to
// the one file that needs it instead of coupling the whole gameplay module to it.
// It has zero Village dependencies (Yarn types only).
//
// Portrait sprites come from the shared PortraitCache (a persistable, lazily-built
// collection) so we never rebuild a Sprite per line. All paths null-guarded: no
// portrait → no injection → base hides the icon cleanly (no blank, no error).
// Requires useIcons=true on the prefab.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using Yarn.Unity;
using Yarn.Unity.Addons.ClassicRPG;

namespace DeNelle.DialogueUI
{
    /// <summary>
    /// RPGDialoguePresenter that shows the speaker's portrait by convention:
    /// a line spoken by "Sylas" gets Resources/HeroPortraits/Sylas. Drop-in
    /// replacement for the base presenter on the DialogueSystem prefab.
    /// </summary>
    public sealed class CompanionDialoguePresenter : RPGDialoguePresenter
    {
        private const string PortraitFolder = "HeroPortraits/";
        private const string IconTagPrefix  = "icon:";

        // One-time guard so the Options-panel layout repair only runs once per
        // hosted presenter (it mutates the live UI hierarchy; re-running is wasteful).
        private bool _optionLayoutRepaired;

        // One-time guard for the light-parchment reskin + name-banner build (both
        // mutate the live UI hierarchy once per hosted presenter).
        private bool _reskinned;

        // The speaker name-banner pieces, built once by BuildNameBannerOnce and
        // re-bound every line. _bannerRoot is the gilt frame Image (shown/hidden per
        // line); _bannerLabel is a CLONE of the prefab's own TMP line text (so it
        // carries the correct, WebGL-safe font asset without this assembly needing a
        // Unity.TextMeshPro reference). Its text/colour are set via the Graphic base
        // + reflection, never via a TMPro-typed handle.
        private GameObject _bannerRoot;
        private Graphic _bannerLabel;
        private System.Reflection.PropertyInfo _bannerTextProp;

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            TryInjectPortraitTag(line);
            UpdateNameBanner(line);
            // Re-assert dark ink on the body + option-preface line text EVERY line.
            // The one-time reskin tints these once, but defend against any per-line
            // state that could regress the colour (and cover the case where the
            // reskin ran before these objects existed). Cheap + idempotent.
            TintLineText(FindDescendant(transform, "Line"), ElarionUi.Ink);
            TintLineText(FindDescendant(transform, "Options"), ElarionUi.Ink);
            await base.RunLineAsync(line, token);
        }

        // ---------------------------------------------------------------------
        // OPTION-TEXT INK FIX (the readable-on-parchment win).
        // ---------------------------------------------------------------------
        // The ROOT of the "yellow on the light box" report: each option button is
        // a fresh Instantiate of the ClassicRPG "Option Item" prefab, whose TMP
        // vertex colour is the package's bright option GREEN (m_fontColor ≈
        // 0.37,1,0.32). On the dark stock box that green is legible; on our LIGHT
        // parchment reskin it washes out to an unreadable yellow-green. The
        // one-time ReskinToLightParchmentOnce tint never reaches these buttons
        // because they don't exist yet at dialogue-start AND are rebuilt every
        // time options are shown — so the green always wins.
        //
        // Fix: override RunOptionsAsync, let the base build + present the options
        // (it instantiates every option item synchronously before its first
        // await), then force each option item's TMP colour to dark ink. We retint
        // for a few frames so it also catches items that finish their typewriter
        // reveal slightly later. OptionItem's selection highlight only toggles a
        // separate icon GameObject (never the text colour), so a flat ink colour
        // is safe for selected + unselected alike.
        public override async YarnTask<DialogueOption?> RunOptionsAsync(
            DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
        {
            var optionsTask = base.RunOptionsAsync(dialogueOptions, cancellationToken);

            // Repaint the freshly-built option items for a short window while the
            // player reads/selects, then leave the awaited choice to resolve.
            for (int frame = 0; frame < 4; frame++)
            {
                TintOptionItems(ElarionUi.Ink);
                if (optionsTask.IsCompleted()) break;
                await YarnTask.Yield();
            }

            return await optionsTask;
        }

        // Force every spawned option button's line text to dark ink. Resolved by
        // hierarchy ("Items" holds the instantiated option items); each item's TMP
        // text is the descendant named "Text". TMP_Text derives from Graphic, so
        // we set Graphic.color without a Unity.TextMeshPro reference, matching the
        // rest of this presenter. Null-guarded + idempotent.
        private void TintOptionItems(Color ink)
        {
            Transform items = FindDescendant(transform, "Items");
            if (items == null) return;
            for (int i = 0; i < items.childCount; i++)
                TintAllTextGraphics(items.GetChild(i), ink);
        }

        // Recolour the option item's TEXT to ink. The ClassicRPG "Option Item"
        // prefab names its TMP child "Label" (NOT "Text"), so we don't match by
        // name. Instead we recolour every Graphic under the item that is NOT an
        // Image — i.e. the TMP_Text label (TMP_Text derives from Graphic, so this
        // reaches it without a Unity.TextMeshPro reference) — while leaving the
        // option's background / selection-icon Images untouched. Null-guarded.
        private static void TintAllTextGraphics(Transform root, Color ink)
        {
            if (root == null) return;
            var graphics = root.GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics)
                if (g != null && !(g is Image)) g.color = ink;
        }

        // The base presenter renders TextWithoutCharacterName (it discards the
        // speaker name). We pull CharacterName back out here and surface it in the
        // gilt banner; the body text stays name-free so the name never double-renders.
        private void UpdateNameBanner(LocalizedLine line)
        {
            if (_bannerRoot == null) return;   // banner build failed / pieces missing
            string speaker = line != null ? line.CharacterName : null;
            bool show = !string.IsNullOrEmpty(speaker);
            _bannerRoot.SetActive(show);
            if (show && _bannerLabel != null && _bannerTextProp != null)
                _bannerTextProp.SetValue(_bannerLabel, speaker);
        }

        // -----------------------------------------------------------------------
        // OPTIONS OVERLAP FIX (WO-337 follow-up — runtime, prefab-free).
        // -----------------------------------------------------------------------
        // The ClassicRPG "Options" panel lays out its preceding-line text ("Text")
        // and its option-button list ("Items") as TWO absolutely-anchored children
        // at FIXED Y positions with FIXED heights and NO parent layout group:
        //   Text  : anchored top, ~120px tall, TMP overflowMode = Overflow
        //   Items : anchored top at a fixed Y just below Text's authored box
        // A long #lastline (e.g. the Echo Warden's 3-line welcome) OVERFLOWS its
        // 120px box downward (Overflow = no clipping) and spills onto the Items
        // list — line text and green option text render word-on-word, both
        // unreadable. Items never reflows below the real (variable) text height
        // because nothing stacks them.
        //
        // Fix at runtime so it needs no in-editor prefab edit and works in builds:
        //   • Put a VerticalLayoutGroup on the "Options" panel so Text-then-Items
        //     STACK (top-aligned, with spacing) instead of overlapping.
        //   • Give the line "Text" a ContentSizeFitter (preferred height) + clamp
        //     its TMP overflow to Truncate, so its box grows to the real line height
        //     and Items always sits BELOW it.
        //   • Give "Items" a ContentSizeFitter so the vertical group sizes it from
        //     its own VerticalLayoutGroup content (the option buttons).
        // Resolved by hierarchy (base fields are private): Options -> Text / Items.
        // Idempotent + fully null-guarded; a missing child just skips that step.
        public override async YarnTask OnDialogueStartedAsync()
        {
            FlowTrace.Step("UI", $"Yarn dialogue started (presenter={name}, timeScale={Time.timeScale})");
            RepairOptionsLayoutOnce();
            ReskinToLightParchmentOnce();
            BuildNameBannerOnce();
            await base.OnDialogueStartedAsync();
            // Re-arm input capture for THIS conversation (paired with the release on
            // complete below). During a dialogue the box SHOULD block world/HUD input;
            // after it ends it must NOT (that leftover blocker is the 'dev tools/Settings
            // dead after Yarn' bug — see OnDialogueCompleteAsync).
            SetOwnGroupsInteractive(true);
        }

        private void RepairOptionsLayoutOnce()
        {
            if (_optionLayoutRepaired) return;
            _optionLayoutRepaired = true;   // attempt once even if pieces are missing

            // The presenter's optionComponents transform is named "Options" in the
            // ClassicRPG prefab; its line text is "Text" and the option list "Items".
            Transform options = FindDescendant(transform, "Options");
            if (options == null) return;

            Transform text  = FindDescendant(options, "Text");
            Transform items = FindDescendant(options, "Items");

            // 1) Stack the line text above the option list inside the panel.
            var vlg = options.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = options.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment        = TextAnchor.UpperLeft;
            vlg.spacing               = 12f;
            vlg.padding               = new RectOffset(24, 24, 24, 24);
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            // Control height so the group reads each child's PREFERRED height
            // (the line text's fitted height, the option list's content height)
            // and flows them top-to-bottom instead of honouring the stale fixed
            // RectTransform heights that caused the overlap.
            vlg.childControlHeight     = true;

            // 2) Line text: clamp overflow + auto-size height so it never bleeds
            //    onto the options below it.
            if (text != null)
            {
                // No direct TMP overflow tweak: the DeNelle.DialogueUI asmdef does not
                // reference Unity.TextMeshPro (CS0103 'TMPro'), and the ContentSizeFitter
                // below already grows the line box to its full preferred height — which is
                // the real mechanism that stops the line bleeding onto the options.
                var fitter = text.GetComponent<ContentSizeFitter>();
                if (fitter == null) fitter = text.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            }

            // 3) Option list: size it from its OWN content (its inner
            //    VerticalLayoutGroup of option buttons) via a ContentSizeFitter,
            //    so the outer group flows it directly below the line text instead
            //    of using the stale fixed height baked into the prefab.
            if (items != null)
            {
                var itemsFitter = items.GetComponent<ContentSizeFitter>();
                if (itemsFitter == null) itemsFitter = items.gameObject.AddComponent<ContentSizeFitter>();
                itemsFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                itemsFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        // -----------------------------------------------------------------------
        // LIGHT-PARCHMENT RESKIN (Phase 1 — procedural, no art).
        // -----------------------------------------------------------------------
        // North star = a warm LIGHT parchment box with dark ink text, not the dark
        // ClassicRPG box. Done as runtime surgery on the prefab's EXISTING objects
        // (resolved by hierarchy, like RepairOptionsLayoutOnce) so the package + the
        // prefab asset are untouched, it works in builds, and it's idempotent.
        //
        // Prefab hierarchy (verified against DialogueSystem.prefab):
        //   Container (backgroundImage)  -> the box backboard Image
        //     Line -> Content -> Icon Holder (portrait niche) + Text (body line)
        //     Options -> Text (option preface) + Items
        // Reskin steps:
        //   1. Container Image -> light parchment fill + rounded sprite + gilt rim.
        //   2. Body "Text" + option "Text" Graphic.color -> dark ink (readable on light).
        //   3. "Icon Holder" -> thin gilt niche frame behind the portrait.
        // All null-guarded; a missing child just skips its step.
        private void ReskinToLightParchmentOnce()
        {
            if (_reskinned) return;
            _reskinned = true;

            // Warm light parchment for the box (opaque enough to read dark ink on).
            Color parchment = new Color(0.93f, 0.88f, 0.76f, 0.98f);
            Color giltRim   = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f);

            // 1) Box backboard -> thin gilt FRAME with a light-parchment interior.
            //    The Container's own Image becomes the gilt frame colour; a parchment
            //    child inset a few px sits ON TOP, leaving only a hairline gilt edge.
            Transform container = FindDescendant(transform, "Container");
            if (container != null)
            {
                var bg = container.GetComponent<Image>();
                if (bg != null)
                {
                    bg.color = giltRim;              // becomes the thin frame edge
                    ElarionUiKit.ApplyRounded(bg);   // rounded 9-slice (flat quad if WebGL build failed)
                    AddParchmentInterior(container.gameObject, parchment);
                }
            }

            // 2) Dark ink on the body + option line text (readable on light parchment).
            TintLineText(FindDescendant(transform, "Line"), ElarionUi.Ink);
            TintLineText(FindDescendant(transform, "Options"), ElarionUi.Ink);

            // 3) Frame the portrait with a thin gilt niche (a tinted Image BEHIND
            //    the existing "Icon Holder", inset so a hairline gilt edge shows).
            Transform iconHolder = FindDescendant(transform, "Icon Holder");
            if (iconHolder != null && iconHolder.parent != null
                && iconHolder.Find("PortraitNiche") == null)
            {
                var niche = new GameObject("PortraitNiche", typeof(Image));
                niche.transform.SetParent(iconHolder, false);
                var nrt = niche.GetComponent<RectTransform>();
                nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
                // Slightly larger than the holder so a gilt frame peeks out around the icon.
                nrt.offsetMin = new Vector2(-6f, -6f);
                nrt.offsetMax = new Vector2(6f, 6f);
                var nimg = niche.GetComponent<Image>();
                nimg.color = giltRim;
                ElarionUiKit.ApplyRounded(nimg);
                nimg.raycastTarget = false;
                niche.transform.SetAsFirstSibling();   // behind the portrait image
            }
        }

        // Recolour the TMP line text under a panel WITHOUT a Unity.TextMeshPro
        // reference: TMP_Text derives from UnityEngine.UI.Graphic, so we find the
        // text Graphic by its "Text" child and set Graphic.color (the body/option
        // line text are the only Graphics named "Text" under these panels).
        private static void TintLineText(Transform panel, Color ink)
        {
            if (panel == null) return;
            Transform text = FindDescendant(panel, "Text");
            if (text == null) return;
            var g = text.GetComponent<Graphic>();
            if (g != null) g.color = ink;
        }

        // The light-parchment interior plate: a rounded Image inset a few px inside
        // the (now gilt) box backboard, so only a hairline gilt frame shows around
        // it. First-sibling = renders above the gilt backboard but behind the line /
        // option content that follows it in the hierarchy. Idempotent.
        private static void AddParchmentInterior(GameObject host, Color parchment)
        {
            if (host == null || host.transform.Find("ParchmentInterior") != null) return;
            var go = new GameObject("ParchmentInterior", typeof(Image));
            go.transform.SetParent(host.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4f, 4f); rt.offsetMax = new Vector2(-4f, -4f);
            var img = go.GetComponent<Image>();
            img.color = parchment;
            ElarionUiKit.ApplyRounded(img);
            img.raycastTarget = false;
            go.transform.SetAsFirstSibling();   // above gilt backboard, behind content
        }

        // -----------------------------------------------------------------------
        // SPEAKER NAME BANNER (built once; bound per line by UpdateNameBanner).
        // -----------------------------------------------------------------------
        // A small gilt-framed plate with a dark-ink TMP label, positioned at the top
        // edge of the line box. The base discards the speaker name (renders
        // TextWithoutCharacterName), so the banner is the only place CharacterName
        // shows. Built by CLONING the prefab's own line "Text" object so the label
        // inherits the correct, build-safe TMP font asset WITHOUT this assembly
        // referencing Unity.TextMeshPro — its .text/.color are driven via reflection
        // + the Graphic base. Idempotent; degrades to no banner if pieces are absent.
        private void BuildNameBannerOnce()
        {
            if (_bannerRoot != null) return;   // already built

            // Anchor the banner over the box backboard so it reads as the box's title.
            Transform container = FindDescendant(transform, "Container");
            if (container == null) return;

            // Source the font/material/component type from the existing body line text.
            Transform lineTextT = null;
            Transform line = FindDescendant(transform, "Line");
            if (line != null) lineTextT = FindDescendant(line, "Text");
            if (lineTextT == null) return;
            var srcGraphic = lineTextT.GetComponent<Graphic>();
            if (srcGraphic == null) return;

            // Gilt frame plate (top-left of the box, overlapping its top edge).
            _bannerRoot = new GameObject("SpeakerBanner", typeof(Image));
            _bannerRoot.transform.SetParent(container, false);
            var rt = _bannerRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.04f, 1f);
            rt.anchorMax = new Vector2(0.04f, 1f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(360f, 64f);
            rt.anchoredPosition = new Vector2(24f, -8f);   // nudge just inside the top edge
            var frame = _bannerRoot.GetComponent<Image>();
            frame.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.95f);
            ElarionUiKit.ApplyRounded(frame);
            frame.raycastTarget = false;

            // Inner parchment plate so the dark-ink name reads on a light chip, not on gold.
            var plate = new GameObject("Plate", typeof(Image));
            plate.transform.SetParent(_bannerRoot.transform, false);
            var prt = plate.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(3f, 3f); prt.offsetMax = new Vector2(-3f, -3f);
            var pimg = plate.GetComponent<Image>();
            pimg.color = new Color(0.96f, 0.92f, 0.82f, 1f);
            ElarionUiKit.ApplyRounded(pimg);
            pimg.raycastTarget = false;

            // Label: CLONE the existing TMP line text (keeps the build-safe font).
            var labelGo = Instantiate(lineTextT.gameObject, plate.transform);
            labelGo.name = "SpeakerLabel";
            // Strip any layout components copied from the body text so it fills the plate.
            foreach (var le in labelGo.GetComponents<LayoutElement>()) Destroy(le);
            foreach (var csf in labelGo.GetComponents<ContentSizeFitter>()) Destroy(csf);
            foreach (Transform child in labelGo.transform) Destroy(child.gameObject);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12f, 0f); lrt.offsetMax = new Vector2(-12f, 0f);
            lrt.localScale = Vector3.one;
            lrt.localRotation = Quaternion.identity;

            _bannerLabel = labelGo.GetComponent<Graphic>();
            if (_bannerLabel != null)
            {
                _bannerLabel.color = ElarionUi.Ink;   // dark ink on the light plate
                _bannerLabel.raycastTarget = false;
                // TMP_Text.text via reflection (no Unity.TextMeshPro ref in this asmdef).
                var t = _bannerLabel.GetType();
                _bannerTextProp = t.GetProperty("text");
                // Shrink the cloned body font + tighten so the name fits the chip.
                var fsProp = t.GetProperty("fontSize");
                if (fsProp != null && fsProp.CanWrite) fsProp.SetValue(_bannerLabel, 34f);
                if (_bannerTextProp != null && _bannerTextProp.CanWrite)
                    _bannerTextProp.SetValue(_bannerLabel, "");   // clear the cloned source text
                // Alignment left as authored; the base never touches this clone (it only
                // rewrites horizontalAlignment on the body line text), so it stays stable.
            }

            _bannerRoot.SetActive(false);   // hidden until a named line arrives
        }

        // Depth-first search for a descendant by exact name (the panel itself or any
        // child). Returns null if absent so the repair degrades gracefully.
        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDescendant(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        // Inject an `icon:<path>` so the base presenter shows a portrait. Priority:
        //   1. DeNelle.Core.DialoguePortrait.Forced (set by the <<portrait>> command —
        //      e.g. "Portraits/forge" for a building NPC, keyed by structure id).
        //   2. else HeroPortraits/<CharacterName> (the companion-by-name convention).
        // No-ops if the line already has an icon tag or no portrait art exists.
        private static void TryInjectPortraitTag(LocalizedLine line)
        {
            if (line == null) return;

            string[] meta = line.Metadata ?? System.Array.Empty<string>();
            foreach (string m in meta)
                if (m != null && m.StartsWith(IconTagPrefix)) return;   // line already specifies an icon

            string path = null;
            string forced = DeNelle.Core.DialoguePortrait.Forced;
            if (!string.IsNullOrEmpty(forced) && PortraitCache.Has(forced))
                path = forced;
            else if (!string.IsNullOrEmpty(line.CharacterName) && PortraitCache.Has(PortraitFolder + line.CharacterName))
                path = PortraitFolder + line.CharacterName;

            if (path == null) return;

            var grown = new string[meta.Length + 1];
            System.Array.Copy(meta, grown, meta.Length);
            grown[meta.Length] = IconTagPrefix + path;                  // base will Resources.Load this
            line.Metadata = grown;
        }

        // Clear any forced portrait when the conversation ends so it never leaks to the next.
        public override async YarnTask OnDialogueCompleteAsync()
        {
            DeNelle.Core.DialoguePortrait.Forced = null;
            FlowTrace.Step("UI", $"Yarn dialogue ended (presenter={name})");
            await base.OnDialogueCompleteAsync();   // let base hide its views first

            // ROOT-CAUSE FIX (caught by the trace below on a real F8 run): after a Yarn
            // dialogue the box's own CanvasGroup 'Container' was left blocksRaycasts=true,
            // a full-screen invisible input eater -> every click died => "dev tools work
            // until I go to Yarn" + "Settings dead after Yarn". The base teardown hides the
            // views visually but does not release raycast blocking, so we do it explicitly:
            // no dialogue active => this presenter must capture NO input.
            SetOwnGroupsInteractive(false);
            ReleaseYarnInputCapture();   // THE actual root cause (see method) — orphaned <Pointer>/press action

            LogInputBlockerState(); // verify: should now read blocksRaycasts=false on every own group
        }

        // THE root cause of "dev tools / UI Toolkit dead after companion Yarn" (found by reading
        // the Yarn package + comparing to its correct pattern). Yarn's LineAdvancer ENABLES an
        // InputAction bound to <Pointer>/press (tap-to-advance) on dialogue start, but its
        // complete-handler only unhooks callbacks — it never .Disable()s the action (unlike
        // RPGDialoguePresenter.skipAction, which DOES). The DialogueRunner persists across
        // conversations, so that orphaned ENABLED pointer action lives on and contends with
        // InputSystemUIInputModule for the pointer device under the new Input System -> UI Toolkit
        // clicks (the dev menu) never resolve for the rest of the session. We close the leak the
        // package leaves open: disable any still-enabled action from the DialogueAdvance asset on
        // completion, and clear any stale EventSystem selection (a destroyed option button left
        // selected when a conversation is Stop()-ed mid-options also disrupts UITK pointer/nav).
        private void ReleaseYarnInputCapture()
        {
            int disabled = 0;
            try
            {
                var enabled = UnityEngine.InputSystem.InputSystem.ListEnabledActions();
                foreach (var a in enabled)
                {
                    if (a == null) continue;
                    string assetName = (a.actionMap != null && a.actionMap.asset != null) ? a.actionMap.asset.name : null;
                    if (assetName == "DialogueAdvance" || a.name == "Advance")
                    {
                        a.Disable();
                        disabled++;
                    }
                }
            }
            catch (System.Exception ex) { FlowTrace.Warn("UI", "ReleaseYarnInputCapture failed: " + ex.Message); }

            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null) es.SetSelectedGameObject(null);

            FlowTrace.Step("UI", $"ReleaseYarnInputCapture: disabled {disabled} dialogue-advance action(s), cleared EventSystem selection");
        }

        // Arm (true) / release (false) raycast capture on every CanvasGroup this presenter
        // owns. Paired across OnDialogueStartedAsync / OnDialogueCompleteAsync so the dialogue
        // box blocks input ONLY while a conversation is on screen, never after it ends.
        private void SetOwnGroupsInteractive(bool on)
        {
            var groups = GetComponentsInChildren<CanvasGroup>(true);
            foreach (var g in groups)
            {
                if (g == null) continue;
                g.blocksRaycasts = on;
                g.interactable = on;
            }
        }

        // ── F8 ROOT-CAUSE TRACE (does not change behaviour) ──────────────────
        // After a Yarn dialogue tears down, the two recurring failures (Settings
        // dead after Yarn / dev tools unclickable) smell like a leftover input
        // blocker: a dialogue scrim/CanvasGroup with blocksRaycasts=true still
        // alive, or a paused Time.timeScale. Dump those so one playtest shows it.
        private void LogInputBlockerState()
        {
            // This presenter's own dialogue canvas groups (the box/scrim it controls).
            var ownGroups = GetComponentsInChildren<CanvasGroup>(true);
            foreach (var g in ownGroups)
            {
                if (g == null) continue;
                FlowTrace.Warn("UI",
                    $"after Yarn end: dialogueGroup '{g.name}' blocksRaycasts={g.blocksRaycasts} " +
                    $"interactable={g.interactable} alpha={g.alpha:0.00}");
            }

            // Any other CanvasGroup in the scene still eating raycasts while invisible
            // (an alpha=0 full-screen blocker is the classic "menu dead" culprit).
            var allGroups = UnityEngine.Object.FindObjectsByType<CanvasGroup>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var g in allGroups)
            {
                if (g == null) continue;
                if (g.blocksRaycasts && g.alpha <= 0.01f)
                    FlowTrace.Warn("UI",
                        $"after Yarn end: INVISIBLE BLOCKER still active — CanvasGroup '{g.name}' " +
                        $"blocksRaycasts=true alpha={g.alpha:0.00} (eats input but draws nothing)");
            }

            FlowTrace.Warn("UI", $"after Yarn end: Time.timeScale={Time.timeScale}" +
                (Time.timeScale <= 0.001f ? "  (STUCK PAUSED — input/anim frozen)" : ""));

            LogUiToolkitBlockerState();
        }

        // ── UI TOOLKIT LAYER TRACE (does not change behaviour) ───────────────
        // The CanvasGroup dump above sees only uGUI. The remaining "dev tools dead
        // after Yarn" symptom is at the UI Toolkit layer: the Settings (HelpMenu)
        // overlay opens after Yarn, but the click on its "Dev tools" button never
        // reaches the callback — i.e. some UIDocument's rootVisualElement is sitting
        // over Settings with pickingMode=Position and swallowing the pointer, OR the
        // EventSystem that drives UI Toolkit pointer input died/duplicated after Yarn.
        // Dump every UIDocument (ordered topmost-first by sortingOrder) + the live
        // EventSystem so one F8 run names the exact interceptor.
        private void LogUiToolkitBlockerState()
        {
            // Fully-qualify all UIElements types: this file imports UnityEngine.UI, whose
            // Image type would clash with UnityEngine.UIElements.Image if we `using` it.
            var docs = UnityEngine.Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Topmost panel first — the highest sortingOrder draws + picks over the rest.
            System.Array.Sort(docs, (a, b) =>
            {
                float sa = a != null ? a.sortingOrder : float.MinValue;
                float sb = b != null ? b.sortingOrder : float.MinValue;
                return sb.CompareTo(sa);
            });

            foreach (var doc in docs)
            {
                if (doc == null) continue;
                bool active = doc.enabled && doc.gameObject.activeInHierarchy;
                bool hasPanel = doc.panelSettings != null;
                float sort = doc.panelSettings != null ? doc.panelSettings.sortingOrder : float.NaN;
                var root = doc.rootVisualElement;
                bool hasRoot = root != null;
                string picking = hasRoot ? root.pickingMode.ToString() : "n/a";
                string display = hasRoot ? root.style.display.value.ToString() : "n/a";
                int childCount = hasRoot ? root.childCount : 0;
                float rw = hasRoot ? root.resolvedStyle.width : 0f;
                float rh = hasRoot ? root.resolvedStyle.height : 0f;

                FlowTrace.Warn("UI",
                    $"after Yarn end: UIDocument '{doc.name}' active={active} hasPanelSettings={hasPanel} " +
                    $"panelSortingOrder={sort} hasRoot={hasRoot} rootPicking={picking} rootDisplay={display} " +
                    $"rootChildCount={childCount} rootResolved={rw:0}x{rh:0}");

                // Candidate pointer interceptor: an ACTIVE document whose root is pickable
                // (Position) AND (near) full-screen — that would sit over Settings and eat
                // every click without drawing anything obvious.
                bool fullScreen = rw >= Screen.width * 0.95f && rh >= Screen.height * 0.95f;
                if (active && hasRoot && root.pickingMode == UnityEngine.UIElements.PickingMode.Position && fullScreen)
                    FlowTrace.Warn("UI",
                        $"after Yarn end: CANDIDATE UI-TOOLKIT INTERCEPTOR — UIDocument '{doc.name}' " +
                        $"is active, rootPicking=Position, full-screen ({rw:0}x{rh:0} vs screen {Screen.width}x{Screen.height}) " +
                        $"panelSortingOrder={sort} — may swallow clicks over Settings");
            }

            // A disabled/duplicated EventSystem after Yarn kills ALL UI clicks (uGUI + UITK).
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null)
                FlowTrace.Warn("UI", "after Yarn end: EventSystem.current is NULL — no UI clicks of any kind will register");
            else
                FlowTrace.Warn("UI",
                    $"after Yarn end: EventSystem.current '{es.name}' enabled={es.enabled} " +
                    $"activeInHierarchy={es.gameObject.activeInHierarchy}");
        }
    }
}
