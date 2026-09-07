using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Session progress for the first Build Mode walkthrough. Intermediate steps are deliberately
    /// not persisted: only a successful check-mark placement completes the guide forever.
    /// </summary>
    public static class BuildFirstUseGuide
    {
        public enum Step { Category, Item, MoveGhost, Rotate, Confirm, Complete }

        public const string CompletionKey = "build.first_use.completed.v1";
        private static Step _step = Step.Category;

        public static bool IsComplete => PlayerPrefs.GetInt(CompletionKey, 0) == 1;
        public static Step Current => IsComplete ? Step.Complete : _step;

        public static string Copy
        {
            get
            {
                switch (Current)
                {
                    case Step.Category:  return "First build: select a category.";
                    case Step.Item:      return "Now select an item to build.";
                    // WO-1411: the placement phases name the WORDS the ghost rail now carries
                    // (PLACE / ROTATE / CANCEL). The old Confirm copy said "tap the check mark",
                    // which described the retired D17 glyph — a banner that names a symbol the
                    // screen no longer draws is worse than no banner.
                    case Step.MoveGhost: return "Place it - drag, then PLACE. Pinch in or out to zoom.";
                    case Step.Rotate:    return "Tap ROTATE to choose its facing.";
                    case Step.Confirm:   return "Tap PLACE to build it.";
                    default:             return string.Empty;
                }
            }
        }

        public static void BeginSession()
        {
            if (!IsComplete) _step = Step.Category;
        }

        /// <summary>
        /// WO-1411 — THE PHASE OWNS THE BANNER. A ghost is armed, so whatever the player
        /// did to get here (the collection browser, the palette carousel, a Manage
        /// "Build defense" door), the pick steps are OVER.
        ///
        /// ⚠ THIS IS THE DEFECT, not a tidy-up. <see cref="Advance"/> only moves when the
        /// CURRENT step is the expected one, and <see cref="CategorySelected"/> /
        /// <see cref="ItemSelected"/> are raised ONLY by BuildCollectionBrowser. Arming from
        /// the palette carousel therefore left _step on <see cref="Step.Category"/>, and the
        /// build HUD's first-run hint (which reads <see cref="Copy"/> every time it shows)
        /// went on saying "First build: select a category." over a ghost the player was
        /// already dragging — exactly what the 07:02 capture shows. Jumping FORWARD to the
        /// placement phase costs nothing on the browser path (it is already past here) and
        /// fixes every other entry door at once.
        /// </summary>
        public static void GhostArmed()
        {
            if (IsComplete) return;
            if (_step == Step.Category || _step == Step.Item) _step = Step.MoveGhost;
        }

        public static void CategorySelected() => Advance(Step.Category, Step.Item);
        public static void ItemSelected() => Advance(Step.Item, Step.MoveGhost);
        public static void GhostMoved() => Advance(Step.MoveGhost, Step.Rotate);
        public static void Rotated() => Advance(Step.Rotate, Step.Confirm);

        public static void PlacementConfirmed()
        {
            if (Current != Step.Confirm) return;
            _step = Step.Complete;
            PlayerPrefs.SetInt(CompletionKey, 1);
            PlayerPrefs.Save();
        }

        private static void Advance(Step expected, Step next)
        {
            if (!IsComplete && _step == expected) _step = next;
        }

#if UNITY_EDITOR
        public static void ResetForTests()
        {
            _step = Step.Category;
            PlayerPrefs.DeleteKey(CompletionKey);
        }
#endif
    }
}
