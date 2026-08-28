using System;
using System.Collections.Generic;
using System.IO;

namespace DeNelle.Editor
{
    /// <summary>Release gate for the synchronous audio bootstrap black-screen class.</summary>
    public static class AudioStartupBoundedRegression
    {
        private const string LoaderPath = "Assets/_Modules/Core/Addressables/AudioAssetLoader.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string source = File.Exists(LoaderPath) ? File.ReadAllText(LoaderPath) : string.Empty;
            if (source.Length == 0) failures.Add("audio loader source missing");

            int method = source.IndexOf("private static bool AddressableRegistered", StringComparison.Ordinal);
            int end = method < 0 ? -1 : source.IndexOf("\n        }", method, StringComparison.Ordinal);
            string body = method >= 0 && end > method ? source.Substring(method, end - method) : string.Empty;
            if (body.Length == 0) failures.Add("AddressableRegistered body not found");
            if (body.IndexOf("locHandle.WaitForCompletion", StringComparison.Ordinal) >= 0 ||
                body.IndexOf("Addressables.LoadResourceLocationsAsync(address", StringComparison.Ordinal) >= 0)
                failures.Add("AddressableRegistered synchronously waits for catalog initialization");
            if (body.IndexOf("Addressables.ResourceLocators", StringComparison.Ordinal) < 0)
                failures.Add("AddressableRegistered does not use the initialized in-memory locator set");

            int resources = source.IndexOf("Resources.Load<T>(key)", StringComparison.Ordinal);
            int addressables = source.IndexOf("Addressables.LoadAssetAsync<T>(key)", StringComparison.Ordinal);
            if (resources < 0 || addressables < 0 || resources > addressables)
                failures.Add("resident Resources audio is not resolved before the synchronous Addressables path");

            if (failures.Count == 0)
            {
                reason = "AUDIO_STARTUP_BOUNDED_OK - resident audio resolves first; catalog probe never blocks";
                return true;
            }
            reason = "AUDIO_STARTUP_BOUNDED_FAIL: " + string.Join(" | ", failures);
            return false;
        }
    }
}
