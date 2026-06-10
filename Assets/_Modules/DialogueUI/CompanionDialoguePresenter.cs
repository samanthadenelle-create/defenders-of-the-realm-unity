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

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            TryInjectPortraitTag(line);
            await base.RunLineAsync(line, token);
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
