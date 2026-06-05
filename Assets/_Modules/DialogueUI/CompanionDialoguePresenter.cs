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

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            TryInjectPortraitTag(line);
            await base.RunLineAsync(line, token);
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
