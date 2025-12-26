## Fixing ScriptableObject creation/config in UnityMCP (design + plan)

### Goal
Make ScriptableObject (SO) creation + configuration **reliable** when driven by an MCP client, specifically for:
- Correct folder/name placement
- Setting `[SerializeField] private` and inherited serialized fields
- UnityEngine.Object references (GUID/path-based)
- Arrays/lists and nested structs (e.g. `timeline.Array.data[0].eventDef`)

### Non-goals
- A reflection-based “set any C# value” system (we should use Unity serialization paths)
- Runtime (player) support; this is editor tooling
- A full schema/introspection UI (we can add minimal discovery later)

---

### Observed failure modes (what users see)
- Wrong placement: assets named “New …” landing in the wrong folder/category even though type is correct.
- Partial setup: asset creates, but setting references/lists/nested fields fails.
- Type lookup flakiness: `CreateInstance` fails during compilation/domain reload, or type resolution is too narrow.
- Field visibility mismatch: reflection misses `[SerializeField] private` and inherited serialized fields.

---

## Core principle
All writes should go through `SerializedObject` / `SerializedProperty` using Unity **property paths**, not reflection.

This is the robust way to support:
- private `[SerializeField]` fields
- inherited serialized fields
- arrays/lists (resize + assign)
- nested structs/classes
- object reference assignment

---

## Proposed new UnityMCP tools (Unity-side bridge)

### Tool A: `create_scriptable_object_asset`
Create an SO asset, optionally applying an initial patch set.

#### Request (JSON)
- `typeName` (string): namespace-qualified type name (accept assembly-qualified too)
- `folderPath` (string): `Assets/...` folder
- `assetName` (string): file name without extension
- `overwrite` (bool, optional; default false): if false, use `AssetDatabase.GenerateUniqueAssetPath`
- `patches` (array, optional): same format as Tool B

Example:
```json
{
  "typeName": "Game.Interactions.InteractionDefinition",
  "folderPath": "Assets/Scriptable Objects/Interactions",
  "assetName": "Int_Coffee_IntoCup",
  "patches": [
    { "propertyPath": "displayName", "op": "set", "value": "Coffee Into Cup" },
    { "propertyPath": "requiredTags.Array.size", "op": "array_resize", "value": 2 },
    { "propertyPath": "requiredTags.Array.data[0]", "op": "set", "ref": { "guid": "..." } },
    { "propertyPath": "requiredTags.Array.data[1]", "op": "set", "ref": { "path": "Assets/Tags/Tag_ReceivesLiquid.asset" } }
  ]
}
```

#### Response (JSON)
- `guid` (string)
- `path` (string)
- `typeNameResolved` (string)
- `patchResults` (array): per-patch result entries
- `warnings` (array of string)

#### Errors (structured)
- `compiling_or_reloading` (retryable)
- `type_not_found`
- `invalid_folder_path`
- `asset_create_failed`

#### Unity-side implementation notes
- Normalize/validate `folderPath`:
  - forward slashes
  - ensure prefix `Assets/`
  - create folders recursively (`AssetDatabase.IsValidFolder` + `AssetDatabase.CreateFolder`)
- Block during compilation/domain reload:
  - if `EditorApplication.isCompiling` or `EditorApplication.isUpdating` return `compiling_or_reloading`
- Resolve type:
  - try `Type.GetType(typeName)`
  - else scan `AppDomain.CurrentDomain.GetAssemblies()` and match `FullName`
- Create + place:
  - `ScriptableObject.CreateInstance(resolvedType)`
  - `AssetDatabase.GenerateUniqueAssetPath` unless `overwrite==true`
  - `AssetDatabase.CreateAsset(instance, path)`
- Apply patches using Tool B logic via `SerializedObject`.
- Persist:
  - `EditorUtility.SetDirty(asset)`
  - `AssetDatabase.SaveAssets()`

---

### Tool B: `set_serialized_properties`
Patch serialized fields on an existing target using Unity property paths.

#### Request (JSON)
- `target` (object): exactly one of:
  - `guid` (string)
  - `path` (string)
- `patches` (array): patch objects

#### Patch object schema
- `propertyPath` (string): Unity serialized path (examples below)
- `op` (string):
  - `set` (default)
  - `array_resize`
- `value` (any, optional): primitives/numbers/strings
- `ref` (object, optional): for `UnityEngine.Object` references:
  - `guid` (string, optional)
  - `path` (string, optional)
  - `typeName` (string, optional; validation only)
- `expectType` (string, optional): used only for better error messages

Example property paths:
- `displayName`
- `requiredTags.Array.size`
- `requiredTags.Array.data[0]`
- `timeline.Array.data[0].eventDef`

#### Response (JSON)
- `targetGuid`, `targetPath`, `targetTypeName`
- `results` (array):
  - `propertyPath`
  - `op`
  - `ok` (bool)
  - `message` (string, optional)
  - `resolvedPropertyType` (string, optional)

#### Unity-side implementation notes
- Resolve the target by GUID/path.
- Use `SerializedObject`:
  - `var so = new SerializedObject(target);`
  - `var prop = so.FindProperty(propertyPath);`
- Apply per-op:
  - `array_resize`: set `prop.intValue` (for `.Array.size`) or set `prop.arraySize` if targeting the array property
  - `set`:
    - primitives: set the correct `SerializedProperty` field (`stringValue`, `intValue`, `floatValue`, `boolValue`)
    - enums: set `enumValueIndex`
    - object refs: load via GUID/path and set `objectReferenceValue`
- Prefer batching `so.ApplyModifiedProperties()` once, but allow per-patch apply if array resizing requires it.

---

## Shared infrastructure (recommended)

### Path normalization
- Normalize to `Assets/...` and forward slashes.
- Ensure folder exists (recursive creation).
- Use `AssetDatabase.GenerateUniqueAssetPath` to avoid collisions.

### Type resolution
- Differentiate clearly between:
  - “type not found”
  - “compiling/reloading”

### Error reporting contract
Return structured errors with:
- `code` (stable)
- `message` (human-readable)
- `details` (input + resolved context: path, typeName, propertyPath)

---

## Acceptance tests (definition of done)
These should be runnable as EditMode tests in a small Unity test project using the MCP endpoints.

1. Create asset in nested folder
   - Pass folder that doesn’t exist
   - Verify folder is created, asset placed correctly
   - Verify response includes `guid` and `path`

2. Set private serialized field
   - SO has `[SerializeField] private string displayName;`
   - Patch `displayName`, verify it persists

3. Set inherited serialized field
   - Base class has a serialized field
   - Patch via property path, verify persistence

4. Set object reference by GUID and by path
   - Patch reference field using GUID
   - Patch reference field using path

5. Resize list + assign elements
   - Set `.Array.size`, then set `.Array.data[i]`

6. Nested struct field
   - Patch `timeline.Array.data[0].eventDef`

7. Compilation-safe behavior
   - When compiling/reloading, return `compiling_or_reloading` (retryable), not a generic failure

---

## Open question (to map to the exact fix)
When you say “so many fails”, which are you seeing most often?
- A) failing to create the SO at all
- B) create succeeds but patching refs/lists fails
- C) create + patch succeed but it ends up in the wrong folder/name

If you paste one representative failure payload/error, we can map it directly to a specific missing op/type/path rule.