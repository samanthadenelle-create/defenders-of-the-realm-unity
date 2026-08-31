using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Wallet;

namespace DeNelle.Commerce
{
    /// <summary>Registers the canonical pack writer from the rail-neutral Commerce assembly.</summary>
    internal static class PackGrantBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            PackGrantBridge.RegisterApplier(ApplyPackBySku, IsPackOwned);
        }

        private static bool ApplyPackBySku(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku)) return false;
            var pack = PackCatalog.Find(sku);
            if (pack == null)
            {
                FlowTrace.Fail("Store",
                    $"PackGrantBootstrap: '{sku}' is absent from the catalog; verified payment not granted.");
                return false;
            }

            var vm = PackStoreVM.CreateDefault();
            vm.ApplyPackContents(pack);
            return vm.IsOwned(sku);
        }

        private static bool IsPackOwned(string sku)
            => !string.IsNullOrWhiteSpace(sku) && PackStoreVM.CreateDefault().IsOwned(sku);
    }
}
