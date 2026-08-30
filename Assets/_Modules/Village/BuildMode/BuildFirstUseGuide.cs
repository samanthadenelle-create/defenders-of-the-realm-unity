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
                    case Step.MoveGhost: return "Drag to place the ghost. Pinch in or out to zoom.";
                    case Step.Rotate:    return "Tap Rotate to choose its facing.";
                    case Step.Confirm:   return "Tap the check mark to build it.";
                    default:             return string.Empty;
                }
            }
        }

        public static void BeginSession()
        {
            if (!IsComplete) _step = Step.Category;
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
