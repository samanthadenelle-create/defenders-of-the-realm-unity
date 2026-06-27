// =============================================================================
// DeNelle.Editor.HudComposer.HudStubGenerator — MVVM stub emitter for HUD Composer.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only). Turns each authored HudScreenMapping into
// THREE files, written into the chosen DeNelle.HUD output folder:
//   • <Screen>PanelMvvm.cs  — the dumb View (ElarionUiKit chrome + 9-slice bg, binds
//     CoreServices.HudModel.<accessor>, copies field->widget, renders ONLY when
//     HudContextModel.Context == its context). ZERO logic.
//   • <Screen>VM.cs         — a THIN ViewModel adapting the Core model to the View +
//     the context-gate + aggregated Changed + view-only state / command stubs.
//   • <Screen>PanelBootstrap.cs — spawns one View per scene (mirrors the proven
//     PartyShopPanelMvvmBootstrap / VillageHudBootstrap lifecycle).
//
// Templates are VERBATIM strings with __TOKEN__ placeholders (no interpolation, so
// the generated C# braces are literal + self-balanced). Generated code targets the
// WO-541 One-Model API by NAME; if WO-541 Stage 1 has not landed the window TODO-flags
// the dependency before allowing generation.
//
// ASCII-only Debug.Log strings; [HUD Composer] tag.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.HudComposer
{
    public static class HudStubGenerator
    {
        public const string LogTag = "[HUD Composer]";

        /// <summary>Result of one screen generation: the three written project paths.</summary>
        public sealed class GenerateResult
        {
            public string ViewPath;
            public string VmPath;
            public string BootstrapPath;
        }

        // =====================================================================
        // Public entry — write the three stubs for one mapping.
        // =====================================================================
        public static GenerateResult GenerateForMapping(string outputFolder, HudScreenMapping map, DataSourceInfo ds)
        {
            if (map == null) return null;

            string folder = string.IsNullOrEmpty(outputFolder) ? "Assets/_Modules/HUD/Generated" : outputFolder;
            EnsureFolder(folder);

            string screen = HudMappingAsset.Sanitize(map.screenName);
            string viewClass = screen + "PanelMvvm";
            string vmClass = screen + "VM";
            string bootClass = screen + "PanelBootstrap";

            var tokens = BuildTokens(map, ds, screen, viewClass, vmClass, bootClass);

            bool isHudModel = ds != null && ds.Kind == DataSourceKind.HudModelRecord;
            string viewSrc = Apply(isHudModel ? ViewTemplateHudModel : ViewTemplateGeneric, tokens);
            string vmSrc = Apply(isHudModel ? VmTemplateHudModel : VmTemplateGeneric, tokens);
            string bootSrc = Apply(BootstrapTemplate, tokens);

            var result = new GenerateResult
            {
                ViewPath = Path.Combine(folder, viewClass + ".cs").Replace('\\', '/'),
                VmPath = Path.Combine(folder, vmClass + ".cs").Replace('\\', '/'),
                BootstrapPath = Path.Combine(folder, bootClass + ".cs").Replace('\\', '/')
            };

            WriteFile(result.ViewPath, viewSrc);
            WriteFile(result.VmPath, vmSrc);
            WriteFile(result.BootstrapPath, bootSrc);

            map.generatedViewPath = result.ViewPath;
            map.generatedVmPath = result.VmPath;
            map.generatedBootstrapPath = result.BootstrapPath;

            Debug.Log($"{LogTag} generated stubs for '{map.screenName}' -> {result.ViewPath}, {result.VmPath}, {result.BootstrapPath}");
            return result;
        }

        // =====================================================================
        // Token table
        // =====================================================================
        private static Dictionary<string, string> BuildTokens(
            HudScreenMapping map, DataSourceInfo ds, string screen, string viewClass, string vmClass, string bootClass)
        {
            string ninePath = ResolveNineSlicePath(map.nineSliceGuid);
            string modelFull = ds != null ? ds.FullTypeName : (string.IsNullOrEmpty(map.modelTypeName) ? "<unassigned>" : map.modelTypeName);
            string modelShort = ds != null ? ds.ShortName : ShortOf(map.modelTypeName);
            string accessor = ds != null && !string.IsNullOrEmpty(ds.FacadeAccessor) ? ds.FacadeAccessor : "/* TODO accessor */";

            return new Dictionary<string, string>
            {
                { "__SCREEN__", string.IsNullOrEmpty(map.screenName) ? screen : map.screenName },
                { "__VIEWCLASS__", viewClass },
                { "__VMCLASS__", vmClass },
                { "__BOOTCLASS__", bootClass },
                { "__CTX__", map.context.ToString() },
                { "__MODELFULL__", modelFull },
                { "__MODELSHORT__", modelShort },
                { "__ACCESSOR__", accessor },
                { "__NINESLICEPATH__", string.IsNullOrEmpty(ninePath) ? "(none)" : ninePath },
            };
        }

        private static string Apply(string template, Dictionary<string, string> tokens)
        {
            string s = template;
            foreach (var kv in tokens) s = s.Replace(kv.Key, kv.Value);
            return s;
        }

        private static string ShortOf(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "object";
            int i = fullName.LastIndexOf('.');
            return i >= 0 && i < fullName.Length - 1 ? fullName.Substring(i + 1) : fullName;
        }

        private static string ResolveNineSlicePath(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return "";
            return AssetDatabase.GUIDToAssetPath(guid) ?? "";
        }

        // =====================================================================
        // File IO
        // =====================================================================
        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void WriteFile(string assetPath, string contents)
        {
            try
            {
                string dir = Path.GetDirectoryName(assetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(assetPath, contents);
                AssetDatabase.ImportAsset(assetPath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LogTag} failed to write '{assetPath}': {e.Message}");
            }
        }

        // =====================================================================
        // TEMPLATES (verbatim — __TOKEN__ placeholders; "" => one literal quote)
        // =====================================================================

        // ---- View (HudModel-record source: fully wired skin) -----------------
        private const string ViewTemplateHudModel = @"// =============================================================================
// __VIEWCLASS__ — AUTO-GENERATED by HUD Composer (Tools > DeNelle > HUD Composer).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD   (refs Core ONLY — never a Village edge)
//
// A DUMB SKIN (WO-541 One-Model): ElarionUiKit chrome + a 9-slice background, binds a
// __VMCLASS__, copies model field -> widget on Changed, and RENDERS ONLY when
// HudContextModel.Context == HudContext.__CTX__. ZERO game logic lives here.
//
// Data source : __MODELFULL__   (CoreServices.HudModel.__ACCESSOR__)
// 9-slice bg  : __NINESLICEPATH__
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core;
using DeNelle.Core.HudModel;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class __VIEWCLASS__ : MonoBehaviour, IPanelView
    {
        // 9-slice background chosen in HUD Composer. TODO: load this sprite via your
        // sprite pipeline (Resources/Addressables) and assign it to _background.sprite.
        private const string NineSliceAssetPath = ""__NINESLICEPATH__"";

        private __VMCLASS__ _vm;
        private GameObject _ui;
        private Image _background;

        // TODO: cache the widgets you copy model fields into (labels, bars, icons).

        private void OnEnable()  { EnsureBuilt(); Rebind(); }
        private void OnDisable() { Unbind(); }
        private void OnDestroy() { Unbind(); if (_ui != null) Destroy(_ui); _ui = null; }

        // The model can register AFTER this view (producers spin up in Stage 2); poll
        // until present so we never miss the first push.
        private void Update()
        {
            if (_vm == null && CoreServices.HudModel != null) Rebind();
        }

        private void Rebind()
        {
            if (CoreServices.HudModel == null) return;
            Bind(new __VMCLASS__());
        }

        // -- IPanelView ---------------------------------------------------------
        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as __VMCLASS__;
            if (_vm == null) return;
            _vm.Activate();
            _vm.Changed += Render;
            Render();
        }

        public void Unbind()
        {
            if (_vm != null)
            {
                _vm.Changed -= Render;
                _vm.Dispose();
                _vm = null;
            }
        }

        // -- Render: copy model field -> widget, gated by context ---------------
        private void Render()
        {
            if (_vm == null) return;

            // CONTEXT GATE (model-driven, not self-gated): show only in our context.
            bool show = _vm.ShouldRender;
            if (_ui != null) _ui.SetActive(show);
            if (!show) return;

            var m = _vm.Model;
            if (m == null) return;

            // ==== USER EDITS — copy fields from `m` into your widgets below. ====
            // e.g. HeroVitalsModel: _hpLabel.text = m.Hp + ""/"" + m.MaxHp;
            // TODO: bind every widget this screen shows. The View NEVER computes —
            //       all math/derivation already happened in the producer.
        }

        // -- Chrome (presentation only) -----------------------------------------
        private void EnsureBuilt()
        {
            if (_ui != null) return;
            _ui = ElarionUiKit.BuildModalCanvas(""__VIEWCLASS__UI"", 30000);

            var panel = ElarionUiKit.Panel(_ui.transform, new Vector2(0.05f, 0.05f), new Vector2(0.45f, 0.30f));
            _background = panel != null ? panel.GetComponent<Image>() : null;
            // TODO: when NineSliceAssetPath resolves to a Sprite, set:
            //   _background.sprite = <loaded>; _background.type = Image.Type.Sliced;

            ElarionUiKit.Header(panel != null ? panel.transform : _ui.transform, ""__SCREEN__"");

            // TODO: build the rest of the widgets (labels/bars/icons) under `panel`.
        }
    }
}
";

        // ---- View (generic source: View binds VM, VM holds the TODO seam) -----
        private const string ViewTemplateGeneric = @"// =============================================================================
// __VIEWCLASS__ — AUTO-GENERATED by HUD Composer (Tools > DeNelle > HUD Composer).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD   (refs Core ONLY — never a Village edge)
//
// DUMB SKIN bound to __VMCLASS__. The chosen data source (__MODELFULL__) is NOT a
// Core HudModel record, so it may not be reachable from DeNelle.HUD — the VM carries
// the wiring TODO (move the VM to DeNelle.Village, or expose the source via a Core
// service/registry like CoreServices.Hud). The View still renders ONLY in
// HudContext.__CTX__.
//
// 9-slice bg  : __NINESLICEPATH__
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class __VIEWCLASS__ : MonoBehaviour, IPanelView
    {
        private const string NineSliceAssetPath = ""__NINESLICEPATH__"";

        private __VMCLASS__ _vm;
        private GameObject _ui;

        private void OnEnable()  { EnsureBuilt(); Bind(new __VMCLASS__()); }
        private void OnDisable() { Unbind(); }
        private void OnDestroy() { Unbind(); if (_ui != null) Destroy(_ui); _ui = null; }

        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as __VMCLASS__;
            if (_vm == null) return;
            _vm.Activate();
            _vm.Changed += Render;
            Render();
        }

        public void Unbind()
        {
            if (_vm != null) { _vm.Changed -= Render; _vm.Dispose(); _vm = null; }
        }

        private void Render()
        {
            if (_vm == null) return;
            bool show = _vm.ShouldRender;
            if (_ui != null) _ui.SetActive(show);
            if (!show) return;

            // ==== USER EDITS — copy fields from the VM into your widgets below. ====
            // TODO: bind widgets to the VM's projected, presentation-ready properties.
        }

        private void EnsureBuilt()
        {
            if (_ui != null) return;
            _ui = ElarionUiKit.BuildModalCanvas(""__VIEWCLASS__UI"", 30000);
            var panel = ElarionUiKit.Panel(_ui.transform, new Vector2(0.05f, 0.05f), new Vector2(0.45f, 0.30f));
            ElarionUiKit.Header(panel != null ? panel.transform : _ui.transform, ""__SCREEN__"");
            // TODO: build the rest of the widgets under `panel`.
        }
    }
}
";

        // ---- VM (HudModel-record source) -------------------------------------
        private const string VmTemplateHudModel = @"// =============================================================================
// __VMCLASS__ — AUTO-GENERATED thin ViewModel by HUD Composer (WO-541 One-Model).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD   (refs Core ONLY)
//
// In the One-Model design the heavy lifting lives in the Core model + its producer.
// This VM is THIN: it adapts CoreServices.HudModel.__ACCESSOR__ to the View, exposes
// the context-gate, aggregates Changed (model + context), and carries any view-only
// state + commands. Same cross-assembly pattern as CoreServices.Hud; NEVER touches
// Village types.
// =============================================================================

using System;
using DeNelle.Core;
using DeNelle.Core.HudModel;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.HUD
{
    public sealed class __VMCLASS__ : IPanelViewModel
    {
        public string Title => ""__SCREEN__"";
        public event Action Changed;

        private bool _active;

        /// <summary>The bound Core model (read-only); null until its producer registers.</summary>
        public __MODELSHORT__ Model => CoreServices.HudModel?.__ACCESSOR__;

        /// <summary>True only in this screen's context (HudContextModel.Context).</summary>
        public bool ShouldRender =>
            CoreServices.HudModel != null &&
            CoreServices.HudModel.Context != null &&
            CoreServices.HudModel.Context.Context == HudContext.__CTX__;

        // -- view-only state (TODO: fields the View needs but the model does not own) --

        /// <summary>Subscribe to model + context Changed so the View re-renders on either.</summary>
        public void Activate()
        {
            if (_active) return;
            _active = true;
            var hm = CoreServices.HudModel;
            if (hm == null) return;
            var model = hm.__ACCESSOR__;
            if (model != null) model.Changed += Raise;
            // Context drives the show/hide gate; skip the double-subscribe when this VM IS the context model.
            if (hm.Context != null && !ReferenceEquals(hm.Context, model)) hm.Context.Changed += Raise;
        }

        public void Close()
        {
            // TODO: route a 'dismiss' command if this screen is dismissable (modal context).
        }

        public void Dispose()
        {
            _active = false;
            var hm = CoreServices.HudModel;
            if (hm == null) return;
            var model = hm.__ACCESSOR__;
            if (model != null) model.Changed -= Raise;
            if (hm.Context != null && !ReferenceEquals(hm.Context, model)) hm.Context.Changed -= Raise;
        }

        private void Raise() => Changed?.Invoke();
    }
}
";

        // ---- VM (generic source: context-gate only + wiring TODO) ------------
        private const string VmTemplateGeneric = @"// =============================================================================
// __VMCLASS__ — AUTO-GENERATED thin ViewModel by HUD Composer.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD   (refs Core ONLY)
//
// The chosen data source (__MODELFULL__) is NOT a Core HudModel record. In the
// One-Model architecture the RIGHT move is to surface its data through a Core model
// (add a record + producer per WO-541) so this VM stays in DeNelle.HUD. Until then:
//   • EITHER move this VM into DeNelle.Village and bind __MODELFULL__ directly, OR
//   • expose the source via a Core service (mirror CoreServices.Hud) and read it here.
// The View is held to the same dumb-skin contract regardless.
// =============================================================================

using System;
using DeNelle.Core;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.HUD
{
    public sealed class __VMCLASS__ : IPanelViewModel
    {
        public string Title => ""__SCREEN__"";
        public event Action Changed;

        private bool _active;

        // TODO: bind __MODELFULL__ here once it is Core-reachable (see header).
        public object Model => null;

        /// <summary>True only in this screen's context (HudContextModel.Context, WO-541).</summary>
        public bool ShouldRender =>
            CoreServices.HudModel != null &&
            CoreServices.HudModel.Context != null &&
            CoreServices.HudModel.Context.Context == DeNelle.Core.HudModel.HudContext.__CTX__;

        public void Activate()
        {
            if (_active) return;
            _active = true;
            var hm = CoreServices.HudModel;
            if (hm != null && hm.Context != null) hm.Context.Changed += Raise;
            // TODO: also subscribe to the real data source's change signal.
        }

        public void Close() { }

        public void Dispose()
        {
            _active = false;
            var hm = CoreServices.HudModel;
            if (hm != null && hm.Context != null) hm.Context.Changed -= Raise;
        }

        private void Raise() => Changed?.Invoke();
    }
}
";

        // ---- Bootstrap (mirrors PartyShopPanelMvvmBootstrap / VillageHudBootstrap) ----
        private const string BootstrapTemplate = @"// =============================================================================
// __BOOTCLASS__ — AUTO-GENERATED bootstrap by HUD Composer.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD. Spawns one __VIEWCLASS__ per gameplay scene (mirrors
// PartyShopPanelMvvmBootstrap / VillageHudBootstrap). The View self-gates on
// HudContextModel.Context, so it is safe to spawn everywhere — it shows nothing
// outside HudContext.__CTX__.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.HUD
{
    public static class __BOOTCLASS__
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureFirst()
        {
            SpawnInScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SpawnInScene(scene);

        private static void SpawnInScene(Scene scene)
        {
            if (!scene.IsValid()) return;

            // GLOBAL dedupe across all loaded scenes.
            foreach (var existing in Object.FindObjectsByType<__VIEWCLASS__>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null) return;
            }

            // TODO: skip front-end/menu scenes (Title/HeroSelect/Intro/Store/GameOver)
            //       if this screen should not exist there.
            var go = new GameObject(""__VIEWCLASS__"");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<__VIEWCLASS__>();
        }
    }
}
";
    }
}
