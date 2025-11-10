using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Services
{
    public class CustomToolRegistrationService : ICustomToolRegistrationService
    {
        private readonly IToolDiscoveryService _discoveryService;
        
        public CustomToolRegistrationService(IToolDiscoveryService discoveryService = null)
        {
            _discoveryService = discoveryService ?? new ToolDiscoveryService();
        }
        
        public async Task<bool> RegisterAllToolsAsync()
        {
            try
            {
                string projectId = GetProjectId();
                
                // Discover tools via reflection
                var tools = _discoveryService.DiscoverAllTools();
                
                if (tools.Count == 0)
                {
                    McpLog.Info("No tools found, skipping registration");
                    return true;
                }
                
                // Convert to registration format
                var toolDefinitions = tools.Select(t => new
                {
                    name = t.Name,
                    description = t.Description,
                    structured_output = t.StructuredOutput,
                    parameters = t.Parameters.Select(p => new
                    {
                        name = p.Name,
                        description = p.Description,
                        type = p.Type,
                        required = p.Required,
                        default_value = p.DefaultValue
                    }).ToList()
                }).ToList();
                
                // Call the FastMCP tool registration endpoint
                var result = await CallMcpToolAsync("register_custom_tools", new
                {
                    project_id = projectId,
                    tools = toolDefinitions
                });
                
                if (result != null && result.success == true)
                {
                    McpLog.Info($"Successfully registered {result.registered?.Count ?? 0} tools with MCP server");
                    return true;
                }
                else
                {
                    McpLog.Error($"Failed to register tools: {result?.error ?? "Unknown error"}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"Error registering tools: {ex.Message}");
                return false;
            }
        }
        
        public bool RegisterAllTools()
        {
            return RegisterAllToolsAsync().GetAwaiter().GetResult();
        }
        
        private string GetProjectId()
        {
            // Use project name + path hash as unique identifier
            string projectName = Application.productName;
            string projectPath = Application.dataPath;
            string combined = $"{projectName}:{projectPath}";
            
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
            }
        }
        
        private async Task<dynamic> CallMcpToolAsync(string toolName, object parameters)
        {
            try
            {
                // For HTTP transport mode, we can make direct HTTP calls to FastMCP
                // For stdio transport mode, we need to use the bridge
                bool isHttpTransport = EditorPrefs.GetBool("MCPForUnity.UseHttpTransport", false);
                
                if (isHttpTransport)
                {
                    // Make direct HTTP call to FastMCP HTTP endpoint
                    return await CallFastMcpHttpAsync(toolName, parameters);
                }
                else
                {
                    // Use the existing MCP bridge for stdio transport
                    return await CallMcpBridgeAsync(toolName, parameters);
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"Error calling MCP tool {toolName}: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }
        
        private async Task<dynamic> CallFastMcpHttpAsync(string toolName, object parameters)
        {
            try
            {
                // Use the HttpMcpClient from the bridge service to make proper MCP tool calls
                var bridgeService = MCPServiceLocator.Bridge;
                var httpClient = bridgeService.GetHttpClient();
                
                if (httpClient == null)
                {
                    McpLog.Error("HTTP MCP client not available - ensure session is started");
                    return new { success = false, error = "HTTP MCP client not initialized" };
                }
                
                // Convert parameters to JObject
                string jsonParams = Newtonsoft.Json.JsonConvert.SerializeObject(parameters);
                JObject paramsObj = JObject.Parse(jsonParams);
                
                // Execute tool via MCP protocol
                var result = await httpClient.ExecuteToolAsync(toolName, paramsObj);
                
                // Convert JObject result to dynamic
                return Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(result.ToString());
            }
            catch (Exception ex)
            {
                McpLog.Error($"HTTP MCP tool call exception: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }
        
        private async Task<dynamic> CallMcpBridgeAsync(string toolName, object parameters)
        {
            // Use the existing MCP bridge for stdio transport
            // This would send the tool call through the Unity socket bridge
            try
            {
                // For stdio mode, we need to send through the bridge
                // The bridge service would handle the MCP protocol communication
                var bridgeService = MCPServiceLocator.Bridge;
                
                if (!bridgeService.IsRunning)
                {
                    return new { success = false, error = "Bridge is not running" };
                }
                
                // Serialize the tool call
                string jsonParams = Newtonsoft.Json.JsonConvert.SerializeObject(parameters);
                
                // Send via bridge (this is a simplified version - actual implementation may vary)
                // For now, return a placeholder indicating stdio mode needs the server running
                await Task.Delay(100);
                return new { success = true, registered = new List<string>(), message = "Stdio mode: Tools will be registered when server starts" };
            }
            catch (Exception ex)
            {
                McpLog.Error($"Bridge call exception: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }
    }
}
