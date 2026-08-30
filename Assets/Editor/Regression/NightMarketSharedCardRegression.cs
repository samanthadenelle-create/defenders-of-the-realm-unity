#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DeNelle.Core.UI;
using DeNelle.Wallet;

namespace DeNelle.Editor.Regression
{
    public static class NightMarketSharedCardRegression
    {
        [MenuItem("Tools/Regression/Run Night Market Shared Cards")]
        public static void RunMenu() { if (!Run(out var r)) throw new Exception(r); Debug.Log(r); }

        public static bool Run(out string reason)
        {
            var cards = new List<GenericCardModel>();
            for (int i = 0; i < 5; i++)
            {
                var source = new StorePackCardModel { Sku="offer-"+i, Name="Offer "+i,
                    Contents="500 Wood, 200 Stone, builder token", PriceMajor="US$2.99",
                    PriceMinor="localized provider price", StateWord="Available" };
                cards.Add(NightMarketCardAdapter.Adapt(source, null));
            }
            if (cards[0].Purpose.IndexOf("builder token", StringComparison.Ordinal) < 0 ||
                cards[0].ContentsOrCost.IndexOf("US$2.99", StringComparison.Ordinal) < 0)
                return Fail("adapter dropped full contents or channel-resolved price", out reason);
            if (NightMarketCardAdapter.Page(cards,0).Count != 4 || NightMarketCardAdapter.Page(cards,1).Count != 1 ||
                CardCollectionPaging.PageCount(cards.Count) != 2)
                return Fail("deterministic 4+1 paging changed", out reason);

            string store = File.ReadAllText("Assets/_Modules/Wallet/PackStore.cs");
            string session = File.ReadAllText("Assets/_Modules/Wallet/NightMarketSharedCardSession.cs");
            if (!store.Contains("OpenBrowser()") || !store.Contains("ShowOffer(sku)") ||
                !store.Contains("EnterConfirmation()") || !store.Contains("_sharedCardSession?.Close()"))
                return Fail("store/detail/confirmation lifecycle hook missing", out reason);
            if (!session.Contains("OpenUnderExistingPanel") || !session.Contains("_host.Push()") ||
                !session.Contains("_host.Pop()") || !session.Contains("_host?.Close()"))
                return Fail("nested focused pause contract missing", out reason);
            if (store.Contains("NightMarketCardAdapter.Adapt(model") && !store.Contains("StorePriceMajor(pack)"))
                return Fail("adapter is not downstream of channel price resolution", out reason);

            reason = "NIGHT_MARKET_SHARED_CARD_OK: neutral resolved-card adapter, deterministic 4+1 paging, nested focused pause, channel price preserved";
            return true;
        }

        private static bool Fail(string value, out string reason) { reason="NIGHT_MARKET_SHARED_CARD_FAIL: "+value; return false; }
    }
}
#endif
