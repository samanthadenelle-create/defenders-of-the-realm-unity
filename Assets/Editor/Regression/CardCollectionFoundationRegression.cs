using System;
using System.Collections.Generic;
using System.IO;
using DeNelle.Core;
using DeNelle.Core.UI;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class CardCollectionFoundationRegression
    {
        private sealed class MemoryCache : ICardCollectionCache
        {
            public string Value;
            public string Read() => Value;
            public void Write(string json) => Value = json;
        }

        [MenuItem("Tools/DeNelle/Regression/Run Card Collection Foundation")]
        public static void RunAll()
        {
            if (!Run(out string reason)) throw new InvalidOperationException(reason);
            Debug.Log(reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string res = File.ReadAllText(Path.Combine(root, "Assets/Resources/Data/Canonical/card-collections.json"));
            string stream = File.ReadAllText(Path.Combine(root, "Assets/StreamingAssets/Data/Canonical/card-collections.json"));
            if (!string.Equals(res, stream, StringComparison.Ordinal)) failures.Add("packaged dual copies differ");

            var cache = new MemoryCache();
            var catalog = new CardCollectionCatalog(() => res, cache, "1.0.0");
            var doc = catalog.Resolve();
            var defenses = doc.Collections.Find(c => c.CollectionId == "build-defenses");
            if (defenses == null || defenses.Items.Count != 5) failures.Add("five-item defense fixture missing");
            if (CardCollectionPaging.PageCount(5) != 2 || CardCollectionPaging.FirstIndex(1, 5) != 4)
                failures.Add("four-up paging does not send fifth card to page two");

            string remote = res.Replace("\"version\": 1", "\"version\": 2");
            if (!catalog.AcceptRemote(remote, CardCollectionCatalog.Sha256(remote), out string acceptReason))
                failures.Add("valid remote rejected: " + acceptReason);
            if (catalog.Resolve().Version != 2) failures.Add("newer verified cache did not win");
            string before = cache.Value;
            if (catalog.AcceptRemote(remote, "bad-hash", out _)) failures.Add("bad hash accepted");
            if (!string.Equals(before, cache.Value, StringComparison.Ordinal)) failures.Add("bad remote overwrote standing cache");
            if (catalog.AcceptRemote(remote, CardCollectionCatalog.Sha256(remote), 1, out _)) failures.Add("bad size accepted");

            string incompatible = remote.Replace("\"minimumClientVersion\": \"0\"", "\"minimumClientVersion\": \"99.0\"");
            if (catalog.AcceptRemote(incompatible, CardCollectionCatalog.Sha256(incompatible), out _)) failures.Add("incompatible remote accepted");

            string api = @"{""success"":true,""serverNowMs"":1900000000000,""collection"":{""collection_id"":""build-defenses"",""requested_collection_id"":""build-defenses"",""used_fallback"":false,""context"":""build"",""title"":""DB Defenses"",""subtitle"":""Remote ordered fixture"",""icon"":{""key"":""collection/defenses"",""url"":null,""sha256"":null},""version"":3,""min_client_version"":""1.0.0"",""items"":[{""sku"":""tower_ballista"",""kind"":""tower"",""version"":1,""definition"":{""title"":""Ballista"",""purpose"":""Stops air threats"",""cost"":{""wood"":20}},""packaged_fallback_key"":""Portraits/tower_ballista"",""fallback_sku"":null,""expiry_behavior"":""fallback"",""asset"":null,""display_order"":20,""badge"":null,""visibility"":{}},{""sku"":""tower_ground_archer"",""kind"":""tower"",""version"":1,""definition"":{""title"":""Archer Tower"",""purpose"":""Guards the ground""},""packaged_fallback_key"":""Portraits/tower_ground_archer"",""fallback_sku"":null,""expiry_behavior"":""fallback"",""asset"":null,""display_order"":10,""badge"":""READY"",""visibility"":{}}]}}";
            var apiModel = catalog.ResolveApiEnvelope(api, "build-defenses", out bool apiFallback, out string apiReason);
            if (apiFallback || apiModel == null || apiModel.Title != "DB Defenses" || apiModel.Cards.Count != 2 ||
                apiModel.Cards[0].StableId != "tower_ground_archer" || apiModel.Cards[0].Title != "Archer Tower")
                failures.Add("exact API envelope did not adapt in DB order: " + apiReason);
            var fallbackModel = catalog.ResolveApiEnvelope(@"{""success"":false,""code"":""COLLECTION_UNAVAILABLE""}",
                "build-defenses", out bool didFallback, out _);
            if (!didFallback || fallbackModel == null || fallbackModel.Cards.Count != 5)
                failures.Add("unavailable API response did not resolve packaged fallback");

            string remoteService = File.ReadAllText(Path.Combine(root,
                "Assets/_Modules/Core/Data/CardCollectionRemoteService.cs"));
            if (!remoteService.Contains("/api/catalog/collection?collectionId=") ||
                !remoteService.Contains("TryWrite(collectionId, body)") ||
                !remoteService.Contains("TryRead(collectionId)") ||
                !remoteService.Contains("ResolveApiEnvelope(null, collectionId"))
                failures.Add("runtime DB collection reader lost fetch, verified cache, or packaged fallback");
            string browser = File.ReadAllText(Path.Combine(root,
                "Assets/_Modules/Village/BuildMode/BuildCollectionBrowser.cs"));
            if (!browser.Contains("ResolveAsync(collection.CollectionId") ||
                !browser.Contains("foreach (var card in _remoteCollection.Cards)") ||
                !browser.Contains("result.Add(card.StableId)"))
                failures.Add("Build collection browser is not consuming the DB-resolved ordered card list");

            PanelManager.CloseAll(); WorldHold.ResetForTests();
            var go = new GameObject("FocusedModalHost regression");
            try
            {
                var host = go.AddComponent<FocusedModalHost>();
                if (!host.Open() || !WorldHold.IsHeld || WorldHold.Count != 1 || !Mathf.Approximately(Time.timeScale, 0f))
                    failures.Add("focused host did not acquire one pause lease");
                host.Push(); host.Pop();
                if (!host.IsOpen || host.NavigationDepth != 1 || WorldHold.Count != 1)
                    failures.Add("nested navigation briefly released pause");
                host.Close(); host.Close();
                if (WorldHold.IsHeld || !Mathf.Approximately(Time.timeScale, 1f) || PanelManager.AnyOpen)
                    failures.Add("idempotent close leaked pause/modal ownership");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); PanelManager.CloseAll(); WorldHold.ResetForTests(); }

            if (failures.Count > 0) { reason = "CARD_COLLECTION_FOUNDATION_FAIL: " + string.Join("; ", failures); return false; }
            reason = "CARD_COLLECTION_FOUNDATION_OK: dual fallback, ordered 4+1 paging, hash/version/cache rejection, nested idempotent pause hold";
            return true;
        }
    }
}
