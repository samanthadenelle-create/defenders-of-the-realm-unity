// =============================================================================
// ManageArt - WO-2002. The ONE place a Manage surface names a frame or a status
// medallion, and the ONE loader that turns a Resources key into a Sprite.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Manage
//
// ⚠ THE DELIVERED FRAME SET IS NOT INTERCHANGEABLE, AND THAT IS THE WHOLE REASON
// THIS FILE EXISTS. Measured 2026-09-06 in Assets/Resources/RpgUi/manage/ (four
// 512px frames + five 256px status medallions):
//   * frame-tile      - OPAQUE centre
//   * frame-selected  - OPAQUE centre AND its glow BLEEDS OUTSIDE its own rect
//   * frame-locked    - HOLLOW centre
//   * frame-max       - HOLLOW centre
// So they CANNOT be 9-sliced against one another and they cannot be swapped in a
// single Image without the layer under them changing too. The renderer therefore
// paints a PLATE first (always), then the portrait, then the frame as a
// preserve-aspect overlay, and carries frame-selected on a SEPARATE, LARGER rect
// so its bleed has somewhere to go. A frame swap is then a sprite swap, which is
// what WO-2002 asks for - but only because the stack under it never moves.
//
// ⛔ DO NOT 9-SLICE THESE. Do not set Image.type = Sliced on a manage frame; the
// two hollow members have no consistent border inset with the two opaque ones and
// a slice reads as a torn edge on exactly two of the four. Verified by inspection
// of the delivered set, not assumed.
//
// The five status medallion names map EXACTLY onto ManageTileVisualState's five
// members, which is why that enum has five members and not nine (the nine-value
// ManageTileBadge stays the authored state next door in ManageStateModel.cs).
//
// LOADER: same Texture2D fallback and same cache as the shipped Manage art path
// (ManageScreenPanel.LoadManageBuildingSpriteAt, ManageScreenPanel.cs:2176). That
// method is `internal` to DeNelle.Village and unreachable from Core, so this is a
// second implementation of one behaviour - duplicated state, which this repo has
// been burned by repeatedly (CLAUDE.md 2 / 5 / 16).
// ⚠ FOLLOW-UP OWED, HANDED BACK WITH WO-2002: WO-2001 should re-point
// ManageScreenPanel.LoadManageBuildingSpriteAt and HeartPanel.LoadManageSprite at
// THIS method and delete the Village copy, so the tree ends with one loader. That
// is a Village-assembly edit and WO-2002 is forbidden from making it.
// HeartPanel.cs:451-453 already states the rule ("the Heart cannot become the one
// art route with its own loader"); this note is that rule applied to Core.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Core.Manage
{
    /// <summary>Manage art keys and the shared Resources loader. No game rules.</summary>
    public static class ManageArt
    {
        // ── Frames (512px, Assets/Resources/RpgUi/manage/) ────────────────────
        public const string FrameTile = "RpgUi/manage/frame-tile";
        public const string FrameSelected = "RpgUi/manage/frame-selected";
        public const string FrameLocked = "RpgUi/manage/frame-locked";
        public const string FrameMax = "RpgUi/manage/frame-max";

        // ── Status medallions (256px), one per canon-7 state ──────────────────
        public const string StatusAvailable = "RpgUi/manage/status-available";
        public const string StatusLocked = "RpgUi/manage/status-locked";
        public const string StatusInProgress = "RpgUi/manage/status-inprogress";
        public const string StatusQueue = "RpgUi/manage/status-queue";
        public const string StatusMax = "RpgUi/manage/status-max";

        /// <summary>
        /// The frame a state wears. A fixed table, not a computation - and it is a
        /// switch on a STATE enum, never on an item id (the id switch is what canon 9
        /// bans and what the WO-2002 oracle looks for).
        ///
        /// <para>⚠ Only Locked and Max get their own frame. Available, InProgress and
        /// QueueBlocked all wear <see cref="FrameTile"/>, because the delivered set has
        /// no frame for them and inventing one by tinting a hollow frame would read as a
        /// different item class. Their state is carried by the MEDALLION and the state
        /// WORD instead - two channels, neither of them colour alone (the owner is
        /// red/green colourblind).</para>
        /// </summary>
        public static string FrameFor(ManageTileVisualState state)
        {
            switch (state)
            {
                case ManageTileVisualState.Locked: return FrameLocked;
                case ManageTileVisualState.Max: return FrameMax;
                default: return FrameTile;
            }
        }

        /// <summary>
        /// The status medallion a state wears. One glyph per canon-7 state.
        ///
        /// <para>⚠ There is no "unaffordable" glyph in the delivered set and none is faked.
        /// An owned item the player cannot currently afford projects to
        /// <see cref="ManageTileVisualState.Available"/> and wears
        /// <see cref="StatusAvailable"/> - the same call HeartPanel.cs:420-440 records for
        /// the Heart, under owner ruling 15. The refusal is carried by the CTA's
        /// DisabledReasonText, which is the affordance the ruling asks for.</para>
        /// </summary>
        public static string StatusFor(ManageTileVisualState state)
        {
            switch (state)
            {
                case ManageTileVisualState.Locked: return StatusLocked;
                case ManageTileVisualState.InProgress: return StatusInProgress;
                case ManageTileVisualState.QueueBlocked: return StatusQueue;
                case ManageTileVisualState.Max: return StatusMax;
                default: return StatusAvailable;
            }
        }

        // ── Loader ────────────────────────────────────────────────────────────

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Loads a Manage sprite by Resources key, with the Texture2D fallback the shipped
        /// path uses (some delivered PNGs import as textures, not sprites) and a per-key
        /// cache including MISSES, so a bad key costs one lookup rather than one per frame.
        ///
        /// <para>A miss returns null and is announced ONCE per key through FlowTrace - never
        /// swallowed (CLAUDE.md 12: a catch or a fallback that does not log turns a visible
        /// defect into an invisible one). The renderer then makes the Image fully transparent
        /// rather than painting a white box.</para>
        /// </summary>
        public static Sprite LoadSprite(string resourceKey)
        {
            if (string.IsNullOrEmpty(resourceKey)) return null;
            if (Cache.TryGetValue(resourceKey, out Sprite cached)) return cached;

            Sprite art = Resources.Load<Sprite>(resourceKey);
            if (art == null)
            {
                var texture = Resources.Load<Texture2D>(resourceKey);
                if (texture != null)
                {
                    art = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 100f);
                    art.name = texture.name + "_manage";
                }
            }
            if (art == null)
                FlowTrace.Once("Manage", "art-miss:" + resourceKey,
                    "manage art unresolved at Resources/" + resourceKey +
                    " - the slot renders transparent rather than as a white box");

            Cache[resourceKey] = art;
            return art;
        }

        /// <summary>Editor/test hook: drops the cache so a re-import is seen. No production caller.</summary>
        public static void ClearCache() { Cache.Clear(); }
    }
}
