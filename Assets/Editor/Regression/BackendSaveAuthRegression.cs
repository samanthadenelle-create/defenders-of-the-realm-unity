using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>WO-1211: boot reads never sign; writes use the shared auth authority.</summary>
    public static class BackendSaveAuthRegression
    {
        private const string StatePath = "Assets/_Modules/Core/State/GameStateService.cs";
        private const string SignerPath = "Assets/_Modules/Core/Web3/BackendRequestSigner.cs";

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("BACKEND_SAVE_AUTH_OK - " + reason);
            else Debug.LogError("BACKEND_SAVE_AUTH_FAIL - " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                string state = Strip(File.ReadAllText(StatePath));
                string signer = Strip(File.ReadAllText(SignerPath));
                string load = Method(state, "LoadFromBackend");
                string save = Method(state, "SendCurrentSnapshot");
                string warm = Method(signer, "WarmUpSessionAsync");
                string cached = Method(signer, "TryAttachCachedSession");

                Require(failures, load, "TryAttachCachedSession", "boot load no longer uses cached-only proof");
                Require(failures, load, "BackendRequestSigner.IsGuestIdentity", "boot load no longer routes guests through shared proof when enforcement is off");
                Reject(failures, load, "TryAttachAsync", "boot load may mint/sign again");
                Reject(failures, load, "SignMessageBase58", "boot load directly signs again");

                Require(failures, save, "BackendRequestSigner.TryAttachAsync", "save write bypasses shared auth");
                Require(failures, save, "BackendRequestSigner.IsGuestIdentity", "save no longer routes guests through shared proof when enforcement is off");
                if (!Regex.IsMatch(save, @"!await\s+DeNelle\.Core\.Web3\.BackendRequestSigner\.TryAttachAsync\s*\([^;]+?\)\s*\)\s*\{[^{}]*?return\s+false\s*;", RegexOptions.Singleline))
                    failures.Add("failed shared save auth is not structurally bound to refusal/requeue");

                Reject(failures, warm, "MintSessionAsync", "connect/auto-resume warm-up signs during boot");
                Reject(failures, warm, "SignMessageBase58", "connect/auto-resume signs during boot");
                Require(failures, warm, "first authenticated action will mint", "deferred-mint posture is missing");

                Require(failures, cached, "X-Guest-Id", "guest proof was dropped from cached-only reads");
                Require(failures, cached, "SessionUsable", "wallet cached proof does not validate expiry/wallet");
                Require(failures, cached, "X-Session", "cached wallet session header is missing");
                Require(failures, cached, "X-Wallet", "cached session is no longer paired with wallet identity");

                Reject(failures, state, "TryAttachAuthHeaders", "GameStateService still owns a second auth rail");
                Reject(failures, state, "FetchNonce(", "GameStateService still owns nonce fetching");
                Reject(failures, state, "SignMessageBase58", "GameStateService still owns wallet signing");
                Reject(failures, state, "dotr-save:v1:", "GameStateService still constructs auth messages");
            }
            catch (Exception ex) { failures.Add("oracle threw " + ex.GetType().Name + ": " + ex.Message); }

            reason = failures.Count == 0
                ? "boot uses cached-only proof, guests retain headers, writes use the sole shared signer"
                : string.Join(" | ", failures.ToArray());
            return failures.Count == 0;
        }

        private static string Strip(string source)
        {
            source = Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline);
            return Regex.Replace(source, @"//[^\r\n]*", "");
        }

        private static string Method(string source, string name)
        {
            var declaration = Regex.Match(source,
                @"\b(?:public|private|internal|protected)\s+(?:static\s+)?(?:async\s+)?[\w<>]+\s+" +
                Regex.Escape(name) + @"\s*\(");
            if (!declaration.Success) throw new InvalidOperationException("method not found: " + name);
            int nameAt = declaration.Index;
            int open = source.IndexOf('{', nameAt);
            if (open < 0) throw new InvalidOperationException("method body not found: " + name);
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
            }
            throw new InvalidOperationException("unterminated method: " + name);
        }

        private static void Require(List<string> failures, string source, string token, string message)
        { if (!source.Contains(token)) failures.Add(message); }

        private static void Reject(List<string> failures, string source, string token, string message)
        { if (source.Contains(token)) failures.Add(message); }
    }
}
