# Unity MCP Tool Error Report
## Session: Scene Setup with Materials and Primitives
## Date: 2025-12-05

## Summary
This report documents all parameter and tool call errors encountered while setting up a Unity scene with four primitives (Cube, Sphere, Cylinder, Plane) and their respective materials.

---

## Error Categories

### 1. Material Path Format Issues

#### Error 1.1: Missing "Assets/" Prefix
**Tool:** `manage_material` (create action)  
**Attempted Paths:**
- `Materials/BlueMetallic`
- `Materials/RedGlowing`
- `Materials/GreenMetallic`
- `Materials/YellowGlowing`

**Error Message:**
```
"Path must start with Assets/"
```

**Resolution:** Added "Assets/" prefix to all paths.

**Recommendation:** 
- Update tool documentation to clearly state path format requirements
- Consider auto-prefixing "Assets/" if path doesn't start with it
- Provide clearer error message with example: "Path must start with Assets/ (e.g., Assets/Materials/MyMaterial.mat)"

---

#### Error 1.2: Missing .mat Extension
**Tool:** `manage_material` (set_material_color, set_material_shader_property actions)  
**Attempted Paths:**
- `Assets/Materials/BlueMetallic`
- `Assets/Materials/RedGlowing`
- `Assets/Materials/GreenMetallic`
- `Assets/Materials/YellowGlowing`

**Error Message:**
```
"Could not find material at path: Assets/Materials/BlueMetallic"
```

**Resolution:** Added `.mat` extension to all paths.

**Recommendation:**
- Document that material paths require `.mat` extension
- Consider auto-appending `.mat` if extension is missing
- Or accept paths without extension and handle internally
- Provide error message: "Material not found. Did you mean 'Assets/Materials/BlueMetallic.mat'?"

---

### 2. Invalid Tool Call Parameters

#### Error 2.1: Missing Required Parameters for manage_gameobject
**Tool:** `manage_gameobject`  
**Issue:** Multiple consecutive calls without any parameters

**Error Message:**
```
"Error calling tool: Tool call arguments for mcp were invalid."
```

**Context:** Attempting to add Light component to Directional Light GameObject

**Attempted Calls:** ~20+ calls with empty parameter sets

**Root Cause:** Tool requires `action` parameter, but calls were made without any parameters

**Recommendation:**
- Provide more specific error message: "Missing required parameter 'action'. Valid actions: create, modify, delete, find, add_component, remove_component, set_component_property, get_components, get_component, duplicate, move_relative"
- Consider showing which parameters are required vs optional in error message

---

####
 Error 2.2: Invalid JSON in component_properties
**Tool:** `manage_gameobject` (set_component_property action)  
**Attempted Value:** `[object Object]` (JavaScript object notation passed as string)

**Error Message:**
```
"Invalid JSON in component_properties: Expecting value: line 1 column 2 (char 1)"
```

**Context:** Attempting to set MeshRenderer component properties to assign materials

**Root Cause:** Passed JavaScript object notation instead of valid JSON object

**Recommendation:**
- Document expected JSON format for component_properties
- Provide example in error message: "Expected JSON object, e.g., {\"sharedMaterial\": \"Assets/Materials/MyMaterial.mat\"}"
- Consider accepting material assignment through a dedicated parameter rather than generic component_properties

---

#### Error 2.3: Wrong Parameter Type for slot
**Tool:** `manage_material` (assign_material_to_renderer action)  
**Attempted Value:** `slot: "0"` (string)

**Error Message:**
```
"Parameter 'slot' must be one of types [integer, null], got string"
```

**Resolution:** Removed slot parameter (defaults to 0) or use integer value

**Recommendation:**
- Document that slot must be integer, not string
- Consider auto-converting string numbers to integers: "0" → 0
- Or provide clearer error: "Parameter 'slot' must be an integer (e.g., 0) or null, got string '0'"

---

### 3. Console Read Tool Issues

#### Error 3.1: Missing Required Parameters
**Tool:** `read_console`  
**Error Message:**
```
"Error calling tool: Tool call arguments for mcp were invalid."
```

**Context:** Multiple attempts to read Unity console without proper parameters

**Root Cause:** Tool requires `action` parameter (get or clear), but calls were made without it

**Recommendation:**
- Document that `action` parameter is required
- Consider making action optional with default "get" behavior
- Provide error message: "Missing required parameter 'action'. Use 'get' to retrieve console messages or 'clear' to clear console."

---

## Successful Patterns

### Material Creation
✅ Correct format: `Assets/Materials/MaterialName.mat`

### Material Property Setting
✅ Correct format: `Assets/Materials/MaterialName.mat` with `.mat` extension

### Material Assignment
✅ Correct format: `assign_material_to_renderer` with `target` (GameObject name), `material_path` (with .mat), and `search_method: "by_name"`

---

## Tool-Specific Recommendations

### manage_material Tool
1. **Path Normalization:**
   - Auto-prefix "Assets/" if missing
   - Auto-append ".mat" if extension missing
   - Or provide clear documentation about exact format required

2. **Error Messages:**
   - Include suggested corrections in error messages
   - Show example paths in error messages

### manage_gameobject Tool
1. **Parameter Validation:**
   - Validate required parameters before processing
   - Provide list of valid actions when action is missing
   - Show which parameters are required for each action

2. **Component Property Setting:**
   - Consider dedicated methods for common operations (e.g., assign_material)
   - Provide JSON schema/example for component_properties
   - Validate JSON format before attempting to parse

### read_console Tool
1. **Default Behavior:**
   - Make `action` optional with default "get"
   - Or require it but provide clear error message

---

## Testing Recommendations

1. **Path Format Tests:**
   - Test with/without "Assets/" prefix
   - Test with/without ".mat" extension
   - Test with various path formats

2. **Parameter Type Tests:**
   - Test slot parameter with string vs integer
   - Test component_properties with various JSON formats
   - Test missing required parameters

3. **Error Message Tests:**
   - Verify error messages are helpful and actionable
   - Ensure error messages include examples where applicable

---

## Additional Observations

1. **Material Assignment Success:**
   - Initial assignment calls returned success but materials weren't actually applied
   - Second round of assignments worked correctly
   - Possible race condition or Unity state issue?

2. **Scene State:**
   - Scene had existing Directional Light (not created in this session)
   - Materials were created successfully after fixing path issues
   - All primitives were created successfully

---

## Priority Recommendations

### High Priority
1. Fix path format handling (auto-normalize or better error messages)
2. Fix manage_gameobject parameter validation and error messages
3. Fix read_console to have default action or better error message

### Medium Priority
1. Add JSON schema/validation for component_properties
2. Consider dedicated material assignment method
3. Add type coercion for common cases (string "0" → integer 0)

### Low Priority
1. Investigate material assignment race condition
2. Add more examples to documentation
3. Consider path auto-completion suggestions


