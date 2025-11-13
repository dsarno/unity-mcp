using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Constants;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// Handles automatic registration of custom tools with the MCP server
    /// </summary>
    public static class CustomToolRegistrationProcessor
    {
        private static bool _isRegistrationEnabled = true;

        static CustomToolRegistrationProcessor()
        {
            // Load saved preference
            _isRegistrationEnabled = EditorPrefs.GetBool(EditorPrefKeys.CustomToolRegistrationEnabled, true);
        }

        /// <summary>
        /// Enable or disable automatic tool registration
        /// </summary>
        public static bool IsRegistrationEnabled
        {
            get => _isRegistrationEnabled;
            set
            {
                _isRegistrationEnabled = value;
                EditorPrefs.SetBool(EditorPrefKeys.CustomToolRegistrationEnabled, value);
            }
        }

        /// <summary>
        /// Register all discovered tools with the MCP server
        /// </summary>
        public static async void RegisterAllTools()
        {
            if (!_isRegistrationEnabled)
            {
                McpLog.Info("Custom tool registration is disabled");
                return;
            }

            try
            {
                McpLog.Info("Starting custom tool registration...");

                var registrationService = MCPServiceLocator.CustomToolRegistration;
                bool success = await registrationService.RegisterAllToolsAsync();

                if (success)
                {
                    McpLog.Info("Custom tool registration completed successfully");
                }
                else
                {
                    McpLog.Warn("Custom tool registration failed - check server logs for details");
                }
            }
            catch (System.Exception ex)
            {
                McpLog.Error($"Error during custom tool registration: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when scripts are reloaded
        /// </summary>
        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            // Invalidate discovery cache to pick up new tools
            var discoveryService = MCPServiceLocator.ToolDiscovery;
            discoveryService.InvalidateCache();

            // Re-register tools after a delay
            if (_isRegistrationEnabled)
            {
                EditorApplication.delayCall += RegisterAllTools;
            }
        }

        /// <summary>
        /// Force re-registration of all tools
        /// </summary>
        public static void ForceReregistration()
        {
            McpLog.Info("Force re-registering custom tools...");

            // Invalidate cache
            var discoveryService = MCPServiceLocator.ToolDiscovery;
            discoveryService.InvalidateCache();

            // Re-register
            RegisterAllTools();
        }

        /// <summary>
        /// Get information about discovered tools
        /// </summary>
        public static string GetToolInfo()
        {
            try
            {
                var discoveryService = MCPServiceLocator.ToolDiscovery;
                var tools = discoveryService.DiscoverAllTools();

                if (tools.Count == 0)
                {
                    return "No custom tools discovered";
                }

                var info = $"Discovered {tools.Count} custom tools:\n";
                foreach (var tool in tools)
                {
                    string status = tool.AutoRegister ? "enabled" : "disabled";
                    info += $"  - {tool.Name} ({status}): {tool.Description}\n";
                }

                return info;
            }
            catch (System.Exception ex)
            {
                return $"Error getting tool info: {ex.Message}";
            }
        }
    }
}
