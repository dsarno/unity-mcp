# Unity GameObject API Test Suite — Tool/Resource Separation

You are running inside CI for the `unity-mcp` repo. Use only the tools allowed by the workflow. Work autonomously; do not prompt the user. Do NOT spawn subagents.

**Print this once, verbatim, early in the run:**
AllowedTools: Write,mcp__unity__manage_editor,mcp__unity__manage_gameobject,mcp__unity__find_gameobjects,mcp__unity__manage_components,mcp__unity__manage_scene,mcp__unity__list_resources,mcp__unity__read_resource,mcp__unity__read_console

---

## Mission
1) Test the new Tool/Resource separation for GameObject management
2) Execute GO tests GO-0..GO-5 in order
3) Verify deprecation warnings appear for legacy actions
4) **Report**: write one `<testcase>` XML fragment per test to `reports/<TESTID>_results.xml`

**CRITICAL XML FORMAT REQUIREMENTS:**
- Each file must contain EXACTLY one `<testcase>` root element
- NO prologue, epilogue, code fences, or extra characters
- Use this exact shape:

<testcase name="GO-0 — Hierarchy with ComponentTypes" classname="UnityMCP.GO-T">
  <system-out><![CDATA[
(evidence of what was accomplished)
  ]]></system-out>
</testcase>

- If test fails, include: `<failure message="reason"/>`
- TESTID must be one of: GO-0, GO-1, GO-2, GO-3, GO-4, GO-5

---

## Test Specs

### GO-0. Hierarchy with ComponentTypes
**Goal**: Verify get_hierarchy now includes componentTypes list
**Actions**:
- Call `mcp__unity__manage_scene(action="get_hierarchy", page_size=10)`
- Verify response includes `componentTypes` array for each item in `data.items`
- Check that Main Camera (or similar) has component types like `["Transform", "Camera", "AudioListener"]`
- **Pass criteria**: componentTypes present and non-empty for at least one item

### GO-1. Find GameObjects Tool
**Goal**: Test the new find_gameobjects tool
**Actions**:
- Call `mcp__unity__find_gameobjects(search_term="Camera", search_method="by_component")`
- Verify response contains `instanceIDs` array in `data`
- Verify response contains pagination info (`pageSize`, `cursor`, `totalCount`)
- **Pass criteria**: Returns at least one instance ID

### GO-2. GameObject Resource Read
**Goal**: Test reading a single GameObject via resource
**Actions**:
- Use the instance ID from GO-1
- Call `mcp__unity__read_resource(uri="unity://scene/gameobject/{instanceID}")` replacing {instanceID} with the actual ID
- Verify response includes: instanceID, name, tag, layer, transform, path
- **Pass criteria**: All expected fields present

### GO-3. Components Resource Read  
**Goal**: Test reading components via resource
**Actions**:
- Use the instance ID from GO-1
- Call `mcp__unity__read_resource(uri="unity://scene/gameobject/{instanceID}/components")` replacing {instanceID} with the actual ID
- Verify response includes paginated component list in `data.items`
- Verify at least one component has typeName and instanceID
- **Pass criteria**: Components list returned with proper pagination

### GO-4. Manage Components Tool
**Goal**: Test the new manage_components tool
**Actions**:
- Create a test GameObject: `mcp__unity__manage_gameobject(action="create", name="GO_Test_Object")`
- Add a component: `mcp__unity__manage_components(action="add", target="GO_Test_Object", component_name="Rigidbody")`
- Set a property: `mcp__unity__manage_components(action="set_property", target="GO_Test_Object", component_name="Rigidbody", component_properties={"mass": 5.0})`
- Verify the component was added and property was set
- Clean up: `mcp__unity__manage_gameobject(action="delete", target="GO_Test_Object")`
- **Pass criteria**: Component added, property set, cleanup successful

### GO-5. Deprecation Warnings
**Goal**: Verify legacy actions log deprecation warnings
**Actions**:
- Call legacy action: `mcp__unity__manage_gameobject(action="find", search_term="Camera", search_method="by_component")`
- Read console using `mcp__unity__read_console` for deprecation warning
- Verify warning mentions "find_gameobjects" as replacement
- **Pass criteria**: Deprecation warning logged

---

## Tool Reference

### New Tools
- `find_gameobjects(search_term, search_method, page_size?, cursor?, search_inactive?)` - Returns instance IDs only
- `manage_components(action, target, component_name?, search_method?, components_to_add?, component_properties?)` - Add/remove/set_property/get_all/get_single

### New Resources  
- `unity://scene/gameobject/{instanceID}` - Single GameObject data
- `unity://scene/gameobject/{instanceID}/components` - All components (paginated)
- `unity://scene/gameobject/{instanceID}/component/{componentName}` - Single component

### Updated Resources
- `manage_scene(action="get_hierarchy")` - Now includes `componentTypes` array in each item

---

## Transcript Minimization Rules
- Do not restate tool JSON; summarize in ≤ 2 short lines
- Per-test `system-out` ≤ 400 chars
- Console evidence: include ≤ 3 lines in the fragment

---

