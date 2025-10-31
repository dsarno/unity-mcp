Material Verification Plan.md

### Overall
This is a strong TDD outline that targets the real gap: verifying visual effect, not just API success. I’d keep the three layers (name validation, value round-trip, visual proof) and add a few robustness tests and API refinements to make it deterministic and hard to regress.

### Suggested test additions
- **Wrong type**: Setting a float to a color/vector property or vice versa should return a type-mismatch error.
- **Keyword-dependent props**: Emission and normal mapping often require keywords (`_EMISSION`, `_NORMALMAP`). Verify:
  - Setting `_EmissionColor` enables `_EMISSION`.
  - Setting a normal map sets keyword and texture SRGB/normal-map import type correctly (or returns a clear error).
- **Texture properties**: Assign a texture to `_BaseMap`; read back and ensure the same texture reference (GUID/name) and correct color space flags.
- **Asset vs instance**: Distinguish editing a material asset (by GUID) from a scene instance (`sharedMaterial` vs `material`). Ensure the tool targets the intended one and tests cover both.
- **Persistence**: After setting values, save, force reimport, and reload. Values should persist across domain reload and editor restart (EditMode test).
- **Batch/atomicity**: Set multiple properties at once; if one fails, assert atomic rollback or partial-apply behavior is explicitly defined and tested.
- **Nonexistent shader/variant**: When shader is missing or stripped, return a specific error. Also test URP vs Built-in compatibility explicitly.
- **Color space tolerance**: Validate in both Linear and Gamma projects (or mock with conversions) with a delta threshold to avoid false negatives.
- **MaterialPropertyBlock**: If your tool supports renderer-level overrides, add tests to ensure those don’t mutate the underlying asset (and are clearly reported).

### Make the visual test deterministic (reduce flakiness)
- **Prefer a controlled render over AssetPreview**: AssetPreview is async and can be flaky. Render a quad with a camera into a `RenderTexture` using a known shader.
- **Use URP Unlit for color tests**: Avoid lighting variance; use `Shader.Find("Universal Render Pipeline/Unlit")` for pure color checks.
- **Assert with tolerance**: Check average color and channel dominance with epsilon (e.g., 0.02–0.05 in Linear).

Example helper for visual verification:
```csharp
public static Color ComputeAverage(RenderTexture rt)
{
    var prev = RenderTexture.active;
    RenderTexture.active = rt;
    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
    tex.Apply();
    RenderTexture.active = prev;

    var pixels = tex.GetPixels();
    float r = 0f, g = 0f, b = 0f, a = 0f;
    for (int i = 0; i < pixels.Length; i++)
    {
        r += pixels[i].r; g += pixels[i].g; b += pixels[i].b; a += pixels[i].a;
    }
    float inv = 1f / pixels.Length;
    return new Color(r * inv, g * inv, b * inv, a * inv);
}
```

### Strengthen validations in Test 1 and 4
- **Use shader introspection**: Validate with `Shader.HasProperty`, and for robust checks in EditMode use `ShaderUtil.GetPropertyCount/Name/Type` to produce precise errors (name exists but wrong type vs name doesn’t exist).
- **Map-and-warn policy**: If you support aliases (e.g., `_Color` → `_BaseColor` for URP), treat it as:
  - Opt-in mapping table (configurable, see below).
  - Return a warning with a canonical-name suggestion.
  - Add tests for aliased success and warning presence.

### Round-trip tests (Test 2)
- Parameterize over multiple properties:
  - `_BaseColor` (Color), `_Smoothness` (Float), `_Metallic` (Float), `_BaseMap` (Texture), `_EmissionColor` (Color with keyword).
- Verify:
  - Exact value equality for floats with tolerance.
  - Texture reference equality (GUID/name).
  - Keywords enabled as needed.

### Tool/API enhancements (refine your list)
- **get_material_properties**
  - Input: target (GUID/path), `propertyNames` or IDs.
  - Output per property: `exists`, `type`, `value`, `keywordRequired`, `keywordEnabled`, `canonicalName`, `changed`.
- **set_material_properties**
  - Options: `validate_only` (dry run), `verify` (read-back post-apply), `atomic` (all-or-nothing).
  - Output: `applied`, `changedCount`, `errors[]`, `warnings[]`, `verification` snapshot.
- **validate_material_properties**
  - Returns `INVALID_PROPERTY_NAME`, `TYPE_MISMATCH`, `SHADER_INCOMPATIBLE`, `MISSING_KEYWORD`, `APPLY_FAILED`, with clear messages and canonical suggestions.
- **Property name mapping**
  - Backed by a `ScriptableObject` in the project for shader → alias table; tool loads it and can be tested.
  - Default is strict; aliasing produces warnings.
- **Target addressing**
  - Accept material by GUID and optional sub-asset name; avoid scene instance ambiguity.
- **Visual preview option**
  - Flag to render a deterministic preview (unlit quad) and return a small metric (mean color, variance) alongside the image. Tests assert the metric.

### Concrete tweaks to your current tests
- Test 1: Add wrong-type and alias-with-warning cases.
- Test 2: Make it a parameterized test over key properties and include keyword verification.
- Test 3: Switch to deterministic offscreen render with URP Unlit; assert mean B > R/G by threshold and absolute mean near expected.
- Test 4: Include Built-in/Legacy shader and a URP shader missing the property; expect `SHADER_INCOMPATIBLE` vs `INVALID_PROPERTY_NAME`.

### Implementation tips
- Create materials programmatically with `Shader.Find("Universal Render Pipeline/Lit")` and `Universal Render Pipeline/Unlit` for visual tests.
- Ensure EditMode tests run in `TestProjects/UnityMCPTests` so you can create and reimport assets.
- Handle Linear vs Gamma: compare in Linear space or convert expected to project color space.
- Avoid per-frame logs; only log on failure with concise diffs.

—  
- Your plan is solid; adding type checks, keywords, persistence, batch/atomic behavior, and deterministic visual rendering will make it comprehensive.  
- Expose verify/dry-run flags and structured error codes so tests can assert precise outcomes.