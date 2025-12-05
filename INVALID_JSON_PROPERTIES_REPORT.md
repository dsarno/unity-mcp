# Invalid JSON Properties Error Report

## Summary
This report documents the JSON parsing errors encountered when attempting to create materials using the `manage_material` tool with inline `properties` parameter.

## Context
The `manage_material` tool's `create` action accepts an optional `properties` parameter that must be valid JSON. The parameter can be:
- A JSON object (JObject) passed directly
- A JSON string that will be parsed into a JObject

The code that handles this is in `ManageMaterial.cs` lines 389-402:
```csharp
JObject properties = null;
JToken propsToken = @params["properties"];
if (propsToken != null)
{
    if (propsToken.Type == JTokenType.String)
    {
        try { properties = JObject.Parse(propsToken.ToString()); }
        catch (Exception ex) { return new { status = "error", message = $"Invalid JSON in properties: {ex.Message}" }; }
    }
    else if (propsToken is JObject obj)
    {
        properties = obj;
    }
}
```

## Errors Encountered

### Error 1: Incomplete Array Value
**Attempted Input:**
```
{'color':1,00
```

**Error Message:**
```
Invalid JSON in properties: Unexpected end while parsing unquoted property name. Path 'color', line 1, position 13.
```

**Root Cause:**
- Python dictionary syntax (`{}`) instead of JSON (`{}`)
- Incomplete array value - missing closing bracket `]`
- Missing closing brace `}`
- Property name not quoted

**Correct Format:**
```json
{"color": [1, 0, 0, 1]}
```

---

### Error 2: Invalid Property Identifier
**Attempted Input:**
```
{'color':1,1,0,1
```

**Error Message:**
```
Invalid JSON in properties: Invalid JavaScript property identifier character: ,. Path 'color', line 1, position 12.
```

**Root Cause:**
- Python dictionary syntax instead of JSON
- Array values not wrapped in brackets `[]`
- Property name not quoted
- Missing closing brace

**Correct Format:**
```json
{"color": [1, 1, 0, 1]}
```

---

### Error 3: Incomplete JSON Structure
**Attempted Input:**
```
{'color': [1
```

**Error Message:**
```
Invalid JSON in properties: Unexpected end of content while loading JObject. Path 'color[0]', line 1, position 12.
```

**Root Cause:**
- Python dictionary syntax instead of JSON
- Incomplete array (missing values and closing bracket)
- Property name not quoted
- Missing closing brace

**Correct Format:**
```json
{"color": [1, 0, 0, 1]}
```

---

### Error 4: Python Boolean Instead of JSON Boolean
**Attempted Input:**
```
{'color': [1, 1, 0, 1], '_EmissionColor': [1, 1, 0, 1], '_Emission': True}
```

**Error Message:**
```
Invalid JSON in properties: Unexpected character encountered while parsing value: T. Path '_Emission', line 1, position 69.
```

**Root Cause:**
- Python dictionary syntax (`{}`) instead of JSON (`{}`)
- Python boolean `True` instead of JSON boolean `true` (lowercase)
- Property names not quoted (though this might work in some JSON parsers, it's not standard)

**Correct Format:**
```json
{"color": [1, 1, 0, 1], "_EmissionColor": [1, 1, 0, 1], "_Emission": true}
```

---

## Common Mistakes Summary

1. **Using Python dictionary syntax instead of JSON**
   - ❌ `{'key': 'value'}`
   - ✅ `{"key": "value"}`

2. **Using Python boolean values**
   - ❌ `True` or `False`
   - ✅ `true` or `false`

3. **Unquoted property names**
   - ❌ `{color: [1,0,0,1]}`
   - ✅ `{"color": [1,0,0,1]}`

4. **Incomplete JSON structures**
   - ❌ `{"color": [1`
   - ✅ `{"color": [1, 0, 0, 1]}`

5. **Missing array brackets for color/vector values**
   - ❌ `{"color": 1,0,0,1}`
   - ✅ `{"color": [1, 0, 0, 1]}`

## Correct Usage Examples

### Example 1: Simple Color Property
```json
{
  "color": [0, 0, 1, 1]
}
```

### Example 2: Multiple Properties
```json
{
  "color": [1, 0, 0, 1],
  "_Metallic": 1,
  "_Glossiness": 0.8
}
```

### Example 3: Material with Emission
```json
{
  "color": [1, 0, 0, 1],
  "_EmissionColor": [1, 0, 0, 1],
  "_Emission": true
}
```

### Example 4: Using String Format (when passing as string parameter)
When passing `properties` as a string, it must be valid JSON:
```json
"{\"color\": [0, 1, 0, 1], \"_Metallic\": 1}"
```

## Recommendations

1. **Always use valid JSON syntax** - Double quotes for strings, lowercase `true`/`false` for booleans
2. **Use JSON arrays for color/vector values** - `[r, g, b, a]` format
3. **Quote all property names** - Even though some parsers allow unquoted names, it's safer to quote them
4. **Validate JSON before sending** - Use a JSON validator if unsure
5. **Consider using separate API calls** - Instead of inline properties, create the material first, then use `set_material_shader_property` and `set_material_color` actions for better error handling

## Workaround Used

Instead of using inline `properties` during material creation, the following approach was used:
1. Create materials without properties: `create` action with only `materialPath` and `shader`
2. Set color separately: `set_material_color` action
3. Set other properties separately: `set_material_shader_property` action

This approach provides:
- Better error messages (property-specific)
- More granular control
- Easier debugging
- Clearer separation of concerns

## Related Code Locations

- **Material Creation Handler**: `MCPForUnity/Editor/Tools/ManageMaterial.cs` lines 384-441
- **Property Setting Logic**: `MCPForUnity/Editor/Helpers/MaterialOps.cs` lines 15-101
- **Color Parsing**: `MCPForUnity/Editor/Helpers/MaterialOps.cs` lines 106-147


