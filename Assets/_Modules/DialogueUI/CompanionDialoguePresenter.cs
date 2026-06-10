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
            await base.RunLineAsync(line, token);
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
        public override YarnTask OnDialogueStartedAsync()
        {
            RepairOptionsLayoutOnce();
            ReskinToLightParchmentOnce();
            BuildNameBannerOnce();
            return base.OnDialogueStartedAsync();
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
        public override YarnTask OnDialogueCompleteAsync()
        {
            DeNelle.Core.DialoguePortrait.Forced = null;
            return base.OnDialogueCompleteAsync();
        }
    }
}
