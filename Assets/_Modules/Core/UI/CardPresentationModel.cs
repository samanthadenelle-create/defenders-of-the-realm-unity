using System;
using System.Collections.Generic;

namespace DeNelle.Core.UI
{
    public enum GenericCardState { Available, Locked, Unavailable, Owned }

    public sealed class GenericCardModel
    {
        public string StableId = "";
        public string ArtworkKey = "";
        public string Title = "";
        public string Purpose = "";
        public string Badge = "";
        public string ContentsOrCost = "";
        public string StateWords = "";
        public string ActionLabel = "";
        public GenericCardState State;
        public Action PrimaryAction;
    }

    public sealed class CardCollectionModel
    {
        public string CollectionId = "";
        public string Title = "";
        public string Subtitle = "";
        public string IconKey = "";
        public IReadOnlyList<GenericCardModel> Cards = Array.Empty<GenericCardModel>();
    }

    public static class CardCollectionPaging
    {
        public const int MaxVisibleCards = 4;
        public static int PageCount(int cardCount) => Math.Max(1, (Math.Max(0, cardCount) + MaxVisibleCards - 1) / MaxVisibleCards);
        public static int FirstIndex(int page, int cardCount)
        {
            int pages = PageCount(cardCount);
            return Math.Max(0, Math.Min(page, pages - 1)) * MaxVisibleCards;
        }
    }
}
