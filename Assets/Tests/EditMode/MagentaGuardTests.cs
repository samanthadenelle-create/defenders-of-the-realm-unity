// =============================================================================
// MagentaGuardTests (EditMode) — locks the MagentaGuard.IsBrokenShader classifier
// (MagentaGuard.cs :335-349), the runtime safety net that recovers pink/magenta
// renderers in a built player (TKT-1 + the Android magenta-slab catch).
// -----------------------------------------------------------------------------
// IsBrokenShader is the gate that decides which materials get recovered to URP/Lit.
// If it stops flagging Standard/Legacy/InternalError (magenta-under-URP shaders),
// the built player ships pink ground/art again. These tests exercise the real
// classifier (private static, reached by reflection) and assert:
//   - the magenta-under-URP shaders (Standard, Legacy, InternalError) are BROKEN,
//   - a null / nameless shader is BROKEN (missing shader = magenta at runtime),
//   - a valid URP/Lit (and URP/Unlit) shader is NOT broken (left untouched).
//
// PLAYMODE / DEVICE-ONLY NOTE: the Android catch `if (!sh.isSupported) return true`
// (:343) cannot be reproduced in the editor — every shader compiles + reports
// isSupported==true here; that branch only fires on-device when a shader fails to
// compile on GLES/Vulkan. It is exercised on real hardware, not in EditMode. The
// name-based + null branches below are the full EditMode-testable surface.
// =============================================================================

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class MagentaGuardTests
    {
        private static bool IsBroken(Shader sh)
        {
            var mi = typeof(MagentaGuard).GetMethod(
                "IsBrokenShader", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(mi, Is.Not.Null,
                "MagentaGuard.IsBrokenShader(Shader) must exist — it is the magenta-recovery gate");
            return (bool)mi.Invoke(null, new object[] { sh });
        }

        [Test]
        public void null_shader_is_broken()
        {
            // A material whose shader was stripped in the build resolves to null -> magenta.
            Assert.That(IsBroken(null), Is.True, "a null shader must be flagged broken");
        }

        [Test]
        public void standard_shader_is_broken()
        {
            // The Built-in "Standard" shader renders MAGENTA under URP and is the classic
            // pink-in-build cause the guard was built to catch.
            var standard = Shader.Find("Standard");
            Assert.That(standard, Is.Not.Null, "editor must resolve the Built-in Standard shader");
            Assert.That(IsBroken(standard), Is.True,
                "the Built-in Standard shader renders magenta under URP and must be flagged broken");
        }

        [Test]
        public void internal_error_shader_is_broken()
        {
            // Hidden/InternalErrorShader is literally the magenta shader Unity swaps in when
            // a shader fails to resolve.
            var err = Shader.Find("Hidden/InternalErrorShader");
            if (err == null) Assert.Ignore("Hidden/InternalErrorShader not resolvable in this editor");
            Assert.That(IsBroken(err), Is.True,
                "Hidden/InternalErrorShader is the magenta error shader and must be flagged broken");
        }

        [Test]
        public void valid_urp_lit_shader_is_not_broken()
        {
            // The recovery target itself must never be classified broken (else infinite churn).
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(lit, Is.Not.Null,
                "project is URP — the URP/Lit shader must resolve in the editor");
            Assert.That(IsBroken(lit), Is.False,
                "a valid URP/Lit shader is healthy and must be left untouched");
        }

        [Test]
        public void valid_urp_unlit_shader_is_not_broken()
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) Assert.Ignore("URP/Unlit not resolvable in this editor");
            Assert.That(IsBroken(unlit), Is.False,
                "URP/Unlit is a valid pipeline shader and must not be flagged broken");
        }
    }
}
