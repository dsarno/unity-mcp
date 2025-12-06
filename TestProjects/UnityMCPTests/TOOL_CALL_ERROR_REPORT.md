# Tool Call Error Report
## Session: Scene Setup with Primitives and Materials

### Summary
During the scene setup task, multiple tool calls failed due to parameter type mismatches. The tools expect specific Python types (lists, dicts) but were receiving string representations instead.

### MCP-Level Error Messages (Console Output)
The following error messages appeared repeatedly in the MCP client console/log:

1. **Generic Invalid Arguments Error** (appeared ~15+ times):
   ```
   Process with MCP model provided invalid arguments to mcp tool.
   ```

2. **Specific Parameter Type Error** (appeared multiple times):
   ```
   Process with MCP invalid type for parameter 'component_properties' in tool mana...
   ```
   (Message appears truncated, but refers to `manage_gameobject` tool)

These MCP-level errors occurred before the Python tool handlers were even invoked, indicating the issue is in the parameter validation/serialization layer between the MCP client and the Python tool handlers.

### Error Sequence Observed in Console

The console showed multiple batches of errors, each corresponding to a retry attempt:

**Batch 1: "Positioning the objects, adding a Light component..."**
- 5× `Process with MCP model provided invalid arguments to mcp tool.`
- 1× `Process with MCP invalid type for parameter 'component_properties' in tool mana...`
- 1× `Process with MCP model provided invalid arguments to mcp tool.`

**Batch 2: "Fixing the tool calls. Positioning objects..."**
- 5× `Process with MCP model provided invalid arguments to mcp tool.`
- 1× `Process with MCP invalid type for parameter 'component_properties' in tool mana...`
- 1× `Process with MCP model provided invalid arguments to mcp tool.`

**Batch 3: "Positioning objects and adding the Light component:"**
- 4× `Process with MCP model provided invalid arguments to mcp tool.`
- (Additional errors likely truncated)

This pattern indicates that:
1. Multiple tool calls were attempted in parallel (5-6 calls per batch)
2. All failed at the MCP validation layer
3. The assistant retried with different parameter formats
4. Each retry produced the same validation errors

---

## Error Category 1: GameObject Position Parameter

### MCP-Level Error
```
Process with MCP model provided invalid arguments to mcp tool.
```

### Python Tool-Level Error Message
```
Parameter 'position' must be one of types [array, null], got string
```

### Attempts Made
1. **First attempt**: Passed `position=[-2,0,0]` as a string literal
   - **Tool**: `manage_gameobject` with `action=create`, `primitive_type=Cube`
   - **What I passed**: `position="[-2,0,0]"` (string representation)
   - **What tool expects**: `list[float]` (actual Python list)
   - **Location**: Lines 29-30 in `manage_gameobject.py`:
     ```python
     position: Annotated[list[float],
                         "Position as [x,y,z]. Must be an array."] | None = None,
     ```

2. **Subsequent attempts**: Tried various formats including:
   - `position=[-2,0,0]` (still interpreted as string)
   - Multiple objects with different position values

### Root Cause
The tool calling interface appears to be serializing array parameters as strings rather than preserving them as actual Python lists. The type annotation `list[float]` expects a native Python list, but the serialization layer is converting it to a JSON string.

### Successful Workaround
Created objects without position initially, then attempted to modify positions (which also failed - see Error Category 2).

---

## Error Category 2: Component Properties Parameter

### MCP-Level Error
```
Process with MCP invalid type for parameter 'component_properties' in tool mana...
Process with MCP model provided invalid arguments to mcp tool.
```

### Python Tool-Level Error Message
```
Parameter 'component_properties' must be one of types [object, null], got string
```

### Attempts Made
Multiple attempts to modify GameObject transforms using `component_properties`:

1. **Attempt 1**: Tried to set Transform.localPosition
   - **Tool**: `manage_gameobject` with `action=modify`, `target="Cube"`
   - **What I passed**: `component_properties="{'Transform': {'localPosition': {'x': -2.0, 'y': 0.0, 'z': 0.0}}}"`
   - **What tool expects**: `dict[str, dict[str, Any]]` (actual Python dict)
   - **Location**: Lines 52-58 in `manage_gameobject.py`:
     ```python
     component_properties: Annotated[dict[str, dict[str, Any]],
                                     """Dictionary of component names to their properties..."""] | None = None,
     ```

2. **Attempt 2**: Tried Python dict syntax
   - **What I passed**: `component_properties={'Transform': {'localPosition': {'x': -2.0, 'y': 0.0, 'z': 0.0}}}`
   - **Result**: Still received as string

3. **Multiple similar attempts** for:
   - Cube position: `[-2, 0, 0]`
   - Sphere position: `[0, 0, 0]`
   - Cylinder position: `[2, 0, 0]`
   - Plane position: `[0, -1.5, 0]`
   - Directional Light position: `[0, 3, 0]` and rotation: `[50, -30, 0]`

### Root Cause
Same as Error Category 1 - the serialization layer is converting Python dicts to strings before they reach the tool handler. The type annotation expects a native Python dict, but receives a stringified version.

---

## Error Category 3: Material Properties Parameter

### MCP-Level Error
```
Process with MCP model provided invalid arguments to mcp tool.
```

### Python Tool-Level Error Message
```
Parameter 'properties' must be one of types [object, null], got string
```

### Attempts Made
1. **Attempt 1**: Tried to create material with initial properties
   - **Tool**: `manage_material` with `action=create`, `material_path="Materials/BlueMetallic"`
   - **What I passed**: `properties="{'color': [0.0, 0.0, 1.0, 1.0], 'metallic': 1.0}"`
   - **What tool expects**: `dict[str, Any]` (actual Python dict)
   - **Location**: Lines 36-37 in `manage_material.py`:
     ```python
     properties: Annotated[dict[str, Any], 
                          "Initial properties to set {name: value}. Must be a JSON object."] | None = None,
     ```

2. **Attempt 2**: Tried Python dict literal
   - **What I passed**: `properties={'color': [0.0, 1.0, 0.0, 1.0], 'metallic': 1.0}`
   - **Result**: Still received as string

### Root Cause
Same serialization issue - dict parameters are being stringified.

### Successful Workaround
Created materials without properties, then used separate `set_material_shader_property` calls to configure them individually.

---

## Error Category 4: Material Color Parameter

### MCP-Level Error
```
Process with MCP model provided invalid arguments to mcp tool.
```

### Python Tool-Level Error Message
```
Parameter 'color' must be one of types [array, array, null], got string
```

### Attempts Made
1. **Attempt 1**: Tried to set material color directly
   - **Tool**: `manage_material` with `action=set_material_color`
   - **What I passed**: `color="[0,0,1,1]"` or `color=[0,0,1,1]`
   - **What tool expects**: `Union[list[float], list[int]]` (actual Python list)
   - **Location**: Lines 44-45 in `manage_material.py`:
     ```python
     color: Annotated[Union[list[float], list[int]], 
                     "Color as [r,g,b] or [r,g,b,a]. Must be an array."] | None = None,
     ```

2. **Multiple attempts** for all four materials:
   - BlueMetallic: `color=[0,0,1,1]`
   - RedGlowing: `color=[1,0,0,1]`
   - GreenMetallic: `color=[0,1,0,1]`
   - YellowGlowing: `color=[1,1,0,1]`

### Root Cause
Same serialization issue - list parameters are being stringified.

### Successful Workaround
Used `set_material_shader_property` with `property="_Color"` and `value=[r,g,b,a]` array, which worked correctly.

---

## Error Category 5: Move Relative Offset Parameter

### MCP-Level Error
```
Process with MCP model provided invalid arguments to mcp tool.
```

### Python Tool-Level Error Message
```
Parameter 'offset' must be one of types [array, null], got string
```

### Attempts Made
1. **Attempt 1**: Tried to use `move_relative` action to position objects
   - **Tool**: `manage_gameobject` with `action=move_relative`, `target="Cube"`
   - **What I passed**: `offset="[-2,0,0]"` or `offset=[-2,0,0]`
   - **What tool expects**: `list[float]` (actual Python list)
   - **Location**: Lines 77-79 in `manage_gameobject.py`:
     ```python
     offset: Annotated[list[float],
                      "Offset from original/reference position as [x,y,z]. Must be an array."] | None = None,
     ```

2. **Multiple attempts** for all objects:
   - Cube: `offset=[-2,0,0]`
   - Sphere: `offset=[0,0,0]`
   - Cylinder: `offset=[2,0,0]`
   - Plane: `offset=[0,-1.5,0]`
   - Directional Light: `offset=[0,3,0]`

### Root Cause
Same serialization issue - list parameters are being stringified.

---

## Successful Operations

Despite the errors, the following operations succeeded:

1. **GameObject Creation** (without position):
   - ✅ Created Cube primitive
   - ✅ Created Sphere primitive
   - ✅ Created Cylinder primitive
   - ✅ Created Plane primitive
   - ✅ Created Directional Light GameObject

2. **Material Creation**:
   - ✅ Created BlueMetallic material (Standard shader)
   - ✅ Created RedGlowing material (Standard shader)
   - ✅ Created GreenMetallic material (Standard shader)
   - ✅ Created YellowGlowing material (Standard shader)

3. **Material Property Setting** (using `set_material_shader_property`):
   - ✅ Set `_Metallic` property on BlueMetallic
   - ✅ Set `_Color` property on all materials
   - ✅ Set `_EmissionColor` on RedGlowing and YellowGlowing
   - ✅ Set `_EMISSION` flag on RedGlowing and YellowGlowing

4. **Material Assignment**:
   - ✅ Assigned BlueMetallic to Cube
   - ✅ Assigned RedGlowing to Sphere
   - ✅ Assigned GreenMetallic to Cylinder
   - ✅ Assigned YellowGlowing to Plane

---

## Remaining Issues

The following operations were **NOT completed** due to parameter type errors:

1. ❌ Positioning GameObjects (all still at origin [0,0,0])
2. ❌ Rotating Directional Light
3. ❌ Adding Light component to Directional Light GameObject
4. ❌ Setting Light properties (type, intensity)

---

## Technical Analysis

### Pattern Identified
All errors follow the same pattern:
- **Expected Type**: Native Python types (`list[float]`, `dict[str, Any]`)
- **Received Type**: String representations of those types
- **Root Cause**: Serialization layer converting native types to strings

### Type Annotations in Code
The tools use strict type annotations:
- `manage_gameobject.py`: `position: Annotated[list[float], ...]`
- `manage_gameobject.py`: `component_properties: Annotated[dict[str, dict[str, Any]], ...]`
- `manage_gameobject.py`: `offset: Annotated[list[float], ...]`
- `manage_material.py`: `color: Annotated[Union[list[float], list[int]], ...]`
- `manage_material.py`: `properties: Annotated[dict[str, Any], ...]`

### Why Some Operations Worked
Operations that worked used:
- Simple string parameters (`name`, `target`, `material_path`)
- Single value parameters (`value` as float/int/bool)
- The `value` parameter in `set_material_shader_property` which accepts `Union[list, float, int, str, bool]` and has JSON parsing logic (lines 58-76 in `manage_material.py`)

---

## Key Finding: C# vs Python Parameter Handling

### C# Side (Unity Editor)
The C# handlers (`ManageGameObject.HandleCommand`, `ManageAsset.HandleCommand`) **accept JSON strings** for complex parameters:
- `componentProperties` can be a JSON string (see `MCPToolParameterTests.cs` line 156)
- `properties` can be a JSON string (see `MCPToolParameterTests.cs` line 92)
- These are parsed and coerced on the C# side

### Python Side (MCP Server)
The Python tool handlers use **strict type annotations**:
- `position: Annotated[list[float], ...]` - expects native Python list
- `component_properties: Annotated[dict[str, dict[str, Any]], ...]` - expects native Python dict
- `offset: Annotated[list[float], ...]` - expects native Python list
- `color: Annotated[Union[list[float], list[int]], ...]` - expects native Python list
- `properties: Annotated[dict[str, Any], ...]` - expects native Python dict

### The Mismatch
When calling tools through the MCP interface:
1. I pass Python native types: `position=[-2,0,0]`, `component_properties={'Transform': {...}}`
2. **MCP client layer** validates parameters and shows: `"Process with MCP model provided invalid arguments to mcp tool."`
3. FastMCP serializes these to JSON for transport
4. The Python tool handler receives them as **strings** instead of native types
5. Pydantic validation fails because it expects `list[float]` but receives `str`
6. Python tool returns: `"Parameter 'X' must be one of types [array/object, null], got string"`

### Error Flow
```
MCP Client (Cursor/IDE)
  ↓ [Parameter validation fails]
  "Process with MCP model provided invalid arguments to mcp tool."
  ↓ [If validation passes, continues to...]
FastMCP Serialization Layer
  ↓ [Converts Python types to JSON strings]
Python Tool Handler (Pydantic validation)
  ↓ [Type mismatch detected]
  "Parameter 'X' must be one of types [array/object, null], got string"
```

### Evidence from Code
- `manage_material.py` lines 58-76: Has JSON parsing logic for the `value` parameter (which accepts `Union[list, float, int, str, bool]`)
- `manage_gameobject.py` lines 100-112: Comment says "Removed manual JSON parsing" and "FastMCP enforces JSON object" - but this assumes FastMCP preserves types
- Tests show C# side accepts JSON strings, but Python side expects native types

## Recommendations

1. **Investigate FastMCP Serialization**: The FastMCP framework should preserve native Python types (lists, dicts) when serializing tool parameters. If it's converting them to strings, this is a bug in FastMCP or the configuration.

2. **Add JSON Parsing Layer**: Similar to how `manage_material.py` handles the `value` parameter (lines 58-76), add JSON parsing for `position`, `offset`, `component_properties`, `color`, and `properties` parameters. This would provide backward compatibility and handle the serialization issue.

3. **Type Coercion Helper**: Create a utility function similar to `parse_json_if_string` in `manage_material.py` that can coerce string representations back to native types when detected.

4. **Unify Parameter Handling**: Decide whether parameters should be:
   - **Option A**: Always accept JSON strings (like C# side) and parse them
   - **Option B**: Always accept native Python types (current intent) and fix serialization
   - **Option C**: Accept both (add parsing as fallback)

5. **Documentation**: Update tool documentation to clarify the expected parameter format and provide examples of both JSON string and native type usage.

6. **Testing**: Add integration tests that verify array and dict parameters are correctly passed through the entire MCP call chain (Python → JSON → C# → Unity).

---

## Files Referenced

- `/Users/davidsarno/unity-mcp/Server/src/services/tools/manage_gameobject.py`
- `/Users/davidsarno/unity-mcp/Server/src/services/tools/manage_material.py`

