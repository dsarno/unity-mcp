using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace Tests.EditMode
{
    /// <summary>
    /// Tests specifically for MCP tool parameter handling issues.
    /// These tests focus on the actual problems we encountered:
    /// 1. JSON parameter parsing in manage_asset and manage_gameobject tools
    /// 2. Material creation with properties through MCP tools
    /// 3. Material assignment through MCP tools
    /// </summary>
    public class MCPToolParameterTests
    {
        [Test]
        public void Test_ManageAsset_ShouldAcceptJSONProperties()
        {
            // ISSUE: manage_asset tool fails with "Parameter 'properties' must be one of types [object, null], got string"
            // ROOT CAUSE: MCP tool parameter validation is too strict - it receives JSON strings but expects objects
            
            // EXPECTED FIX: The MCP tool should:
            // 1. Accept both string and object types for the 'properties' parameter
            // 2. Parse JSON strings into objects internally
            // 3. Provide better error messages
            
            // TEST CASE: This should work but currently fails:
            // mcp_unityMCP_manage_asset with properties={"shader": "Universal Render Pipeline/Lit", "color": [0, 0, 1, 1]}
            
            Assert.Fail("FIX NEEDED: MCP manage_asset tool parameter parsing. " +
                       "The tool should parse JSON strings for the 'properties' parameter instead of rejecting them.");
        }

        [Test]
        public void Test_ManageGameObject_ShouldAcceptJSONComponentProperties()
        {
            // ISSUE: manage_gameobject tool fails with "Parameter 'component_properties' must be one of types [object, null], got string"
            // ROOT CAUSE: MCP tool parameter validation is too strict - it receives JSON strings but expects objects
            
            // EXPECTED FIX: The MCP tool should:
            // 1. Accept both string and object types for the 'component_properties' parameter
            // 2. Parse JSON strings into objects internally
            // 3. Provide better error messages
            
            // TEST CASE: This should work but currently fails:
            // mcp_unityMCP_manage_gameobject with component_properties={"MeshRenderer": {"material": "Assets/Materials/BlueMaterial.mat"}}
            
            Assert.Fail("FIX NEEDED: MCP manage_gameobject tool parameter parsing. " +
                       "The tool should parse JSON strings for the 'component_properties' parameter instead of rejecting them.");
        }

        [Test]
        public void Test_JSONParsing_ShouldWorkInMCPTools()
        {
            // ISSUE: MCP tools fail to parse JSON parameters correctly
            // ROOT CAUSE: Parameter validation is too strict - tools expect objects but receive strings
            
            // EXPECTED FIX: MCP tools should:
            // 1. Parse JSON strings into objects internally
            // 2. Accept both string and object parameter types
            // 3. Provide clear error messages when JSON parsing fails
            
            // TEST CASES that should work:
            // - Material creation: properties={"shader": "Universal Render Pipeline/Lit", "color": [0, 0, 1, 1]}
            // - GameObject modification: component_properties={"MeshRenderer": {"material": "Assets/Materials/BlueMaterial.mat"}}
            
            Assert.Fail("FIX NEEDED: MCP tool JSON parameter parsing. " +
                       "Tools should parse JSON strings internally instead of rejecting them at the parameter validation layer.");
        }

    }
}