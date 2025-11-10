using System;
using MCPForUnity.Editor.Helpers;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Service for managing cache operations
    /// </summary>
    public class CacheManagementService : ICacheManagementService
    {
        /// <summary>
        /// Clear the local uvx cache for the MCP server package
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public bool ClearUvxCache()
        {
            try
            {
                var pathService = MCPServiceLocator.Paths;
                string uvxPath = pathService.GetUvxPath();
                
                if (string.IsNullOrEmpty(uvxPath))
                {
                    McpLog.Error("UVX not found. Please install UV/UVX first.");
                    return false;
                }
                
                // Get the package name
                string packageName = "mcp-for-unity";
                
                // Run uvx cache clean command
                string args = $"cache clean {packageName}";
                
                if (ExecPath.TryRun(uvxPath, args, Application.dataPath, out var stdout, out var stderr, 30000))
                {
                    McpLog.Info($"UVX cache cleared successfully: {stdout}");
                    return true;
                }
                else
                {
                    McpLog.Warn($"Failed to clear uvx cache: {stderr}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"Error clearing uvx cache: {ex.Message}");
                return false;
            }
        }
    }
}
