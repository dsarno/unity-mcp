# JSON Properties Issue Report - manage_material Tool

## Summary
Encountered multiple JSON parsing errors when attempting to create materials with initial properties using the `manage_material` tool's `create` action. The issue stems from how JSON is serialized/deserialized between the Python MCP tool layer and the C# Unity handler.

## What Worked ✅

### Successful Material Creation (without properties)
```python
mcp_unityMCP_manage_material(
    action="create",
    material_path="Assets/Materials/BlueMetallic.mat",
    shader="Standard"
)
```
**Result**: ✅ Success - Material created

### Successful Material Creation (with simple properties dict)
```python
mcp_unityMCP_manage_material(
    action="create",
    material_path="Assets/Materials/BlueMetallic.mat",
    shader="Standard",
    properties={"color": [0.0, 0.3, 1.0, 1.0], "metallic": 1.0, "smoothness": 0.8}
)
```
**Result**: ✅ Success - Material created with properties

### Successful Property Setting (separate call)
```python
mcp_unityMCP_manage_material(
    action="set_material_shader_property",
    material_path="Assets/Materials/YellowGlowing.mat",
    property="_Color",
    value=[1,1,0,1]
)
```
**Result**: ✅ Success - Property set correctly

## What Failed ❌

### Issue 1: Malformed JSON String in Tool Call
**Attempted**:
```python
mcp_unityMCP_manage_material(
    action="create",
    material_path="Assets/Materials/RedGlowing.mat",
    shader="Standard",
    properties={"color: [1.0, 0.0, 0.0, 1.0], "_EmissionColor": [1.0, 0.0, 0.0, 1.0]}
)
```
**Error**: `{"status":"error","message":"Invalid JSON in properties"}`

**Root Cause**: The tool call syntax was malformed - missing closing quote and improper JSON structure. This was a user error in constructing the tool call, not a system issue.

### Issue 2: Incomplete JSON Object
**Attempted**:
```python
mcp_unityMCP_manage_material(
    action="create",
    material_path="Assets/Materials/YellowGlowing.mat",
    shader="Standard",
    properties={"color: [1.0, 1.0, 0.0, 1.0], 
)
```
**Error**: `{"status":"error","message":"Invalid JSON in properties"}`

**Root Cause**: Incomplete JSON object - missing closing brace and proper key-value formatting.

### Issue 3: Invalid JSON Format (missing quotes on keys)
**Attempted**:
```python
mcp_unityMCP_manage_material(
    action="create",
    material_path="Assets/Materials/YellowGlowing.mat",
    shader="Standard",
    properties={"_Color": 1,1,0,1}
)
```
**Error**: `{"status":"error","message":"Invalid JSON in properties"}`

**Root Cause**: Invalid Python dict syntax - `1,1,0,1` is not a valid value (should be `[1,1,0,1]`).

## Code Flow Analysis

### Python Layer (manage_material.py)
1. Accepts `properties` as `dict[str, Any] | str`
2. Has `parse_json_if_string()` helper that attempts to parse stringified JSON
3. Passes `properties` directly to C# handler via `params_dict`

### C# Layer (ManageMaterial.cs)
1. Receives `properties` as `JToken` from `@params["properties"]`
2. Checks if it's a string - if so, tries to parse as JSON:
   ```csharp
   if (propsToken.Type == JTokenType.String)
   {
       try { properties = JObject.Parse(propsToken.ToString()); }
       catch { return new { status = "error", message = "Invalid JSON in properties" }; }
   }
   ```
3. If it's already a `JObject`, uses it directly
4. Iterates through properties and calls `MaterialOps.TrySetShaderProperty()`

## The Real Issue

The actual problem encountered was **user error** in constructing the tool calls, not a system bug. However, there are some potential edge cases:

### Potential Issue: Stringified JSON Handling
If `properties` comes through as a stringified JSON object (e.g., `'{"_Color": [1,0,0,1]}'`), the C# code attempts to parse it. However, if the Python layer sends it as a dict, it should arrive as a `JObject` directly.

### Potential Issue: Array Serialization
When passing arrays like `[1,0,0,1]` in the properties dict, they need to be properly serialized. The Python layer should handle this correctly when sending to C#, but there may be edge cases with nested structures.

## Working Examples

### Example 1: Simple Color Property
```python
properties = {
    "_Color": [1.0, 0.0, 0.0, 1.0]
}
```

### Example 2: Multiple Properties
```python
properties = {
    "_Color": [0.0, 0.3, 1.0, 1.0],
    "_Metallic": 1.0,
    "_Glossiness": 0.8
}
```

### Example 3: With Emission
```python
properties = {
    "_Color": [1.0, 0.0, 0.0, 1.0],
    "_EmissionColor": [1.0, 0.0, 0.0, 1.0]
}
```

## Recommendations

1. **For Tool Users**: Always use proper Python dict syntax when passing `properties`:
   - Use lists for arrays: `[1,0,0,1]` not `1,0,0,1`
   - Use proper key-value pairs: `{"key": value}`
   - Ensure all strings are properly quoted

2. **For System Improvement**: Consider adding better error messages that indicate:
   - Which part of the JSON is malformed
   - What the expected format should be
   - Example of correct usage

3. **Testing**: Add unit tests that verify:
   - Dict properties are correctly serialized
   - Stringified JSON properties are correctly parsed
   - Array values in properties are handled correctly
   - Edge cases like empty dicts, null values, etc.

## Conclusion

The "Invalid JSON in properties" errors were primarily due to malformed tool call syntax. The system itself appears to handle valid JSON/dict properties correctly. The workaround of creating materials first and then setting properties separately works reliably, but it would be more efficient to set properties during creation if the JSON format is correct.


