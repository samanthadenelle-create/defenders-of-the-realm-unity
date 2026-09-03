using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

/// <summary>
/// Scrolls a material's albedo UVs over time (Lana Studio "Casual RPG VFX" pack).
///
/// <para>WO-DEFECT-2 (owner felt-test 2026-09-03, F8 device capture seq 4678): the stock pack
/// script called <c>SetTextureOffset("_MainTex", ...)</c> UNCONDITIONALLY, every frame. The pack's
/// materials were re-authored onto <c>Universal Render Pipeline/Particles/Unlit</c>, which exposes
/// <c>_BaseMap</c> and does NOT declare the legacy built-in name <c>_MainTex</c>. Unity therefore
/// logged, once per frame per emitter:</para>
/// <code>
/// Material 'Add_offset04 (Instance)' with Shader 'Universal Render Pipeline/Particles/Unlit'
/// doesn't have a texture property '_MainTex'
/// </code>
/// <para>The CONSEQUENCE is not just log noise: the scroll silently never applied, so every effect
/// wearing one of these materials rendered as a STATIC, un-animated sheet. That is the owner's
/// "displays just ugly" report against the Night Store aura
/// (<c>Assets/Resources/VFX/Aura/top_down_starfall_line_blue.prefab</c>, which wears
/// <c>Add_offset04.mat</c> and carries this component).</para>
///
/// <para>The fix is in the CALLER, deliberately: the materials are SHARED vendor-pack assets and
/// re-serializing one would silently change every other user of it. This resolves the scroll target
/// per material, preferring the URP name and falling back to the legacy one, and no-ops (with a
/// one-shot FlowTrace) when the material declares neither.</para>
///
/// <para>Also fixed here: the original indexed <c>rend.materials</c> INSIDE <c>Update</c>. That
/// property allocates a fresh Material[] and instantiates every slot on EVERY access, which is where
/// the "(Instance)" in the captured error came from and which leaked a material array per frame per
/// emitter. Resolution is now cached and done once.</para>
/// </summary>
public class UVscroll : MonoBehaviour
{
    // Scroll main texture based on time
    public int materialId = 0;
    public float scrollSpeedX = 0.5f;
    public float scrollSpeedY = 0.5f;
    Renderer rend;

    // URP's Particles/Unlit (and Lit) declare "_BaseMap". The built-in/legacy pipeline used
    // "_MainTex". Resolve ONCE against the real material rather than assuming either name.
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    Material _target;      // the instance material we scroll (null = nothing to do)
    int _propertyId;       // the texture property that material actually declares
    bool _resolved;        // resolution is attempted exactly once

    void Start()
    {
        rend = GetComponent<Renderer>();
        Resolve();
    }

    /// <summary>
    /// Bind <see cref="_target"/> + <see cref="_propertyId"/> once. Leaves <see cref="_target"/>
    /// null (a permanent no-op) when there is no renderer, no such slot, or the material declares
    /// neither texture property — never throws, never spams.
    /// </summary>
    void Resolve()
    {
        _resolved = true;
        _target = null;

        if (rend == null) return;

        // Touch .materials ONCE (it instantiates); index defensively.
        var mats = rend.materials;
        if (mats == null || materialId < 0 || materialId >= mats.Length)
        {
            FlowTrace.Once("UVscroll", "slot:" + name + ":" + materialId,
                $"UVscroll on '{name}': materialId {materialId} is out of range " +
                $"(renderer has {(mats == null ? 0 : mats.Length)} material slot(s)) - scroll disabled.");
            return;
        }

        var m = mats[materialId];
        if (m == null) return;

        if (m.HasProperty(BaseMapId))      { _target = m; _propertyId = BaseMapId; }
        else if (m.HasProperty(MainTexId)) { _target = m; _propertyId = MainTexId; }
        else
        {
            // The material declares NEITHER name. Scrolling is impossible; say so once, by name,
            // so a future occurrence identifies itself instead of being re-diagnosed from scratch.
            FlowTrace.Once("UVscroll", "noprop:" + m.shader?.name + ":" + m.name,
                $"UVscroll on '{name}': material '{m.name}' (shader '{(m.shader != null ? m.shader.name : "<none>")}') " +
                "declares neither '_BaseMap' nor '_MainTex' - UV scroll disabled for it, effect will render STATIC. " +
                "Fix at source: give the material a URP albedo slot.");
        }
    }

    void Update()
    {
        if (!_resolved) Resolve();
        if (_target == null) return;

        float offsetX = Time.time * scrollSpeedX;
        float offsetY = Time.time * scrollSpeedY;

        _target.SetTextureOffset(_propertyId, new Vector2(offsetX, offsetY));
    }
}
