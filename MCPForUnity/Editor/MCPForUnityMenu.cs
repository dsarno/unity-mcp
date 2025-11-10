using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Setup;
using MCPForUnity.Editor.Windows;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor
{
    /// <summary>
    /// Centralized menu items for MCP For Unity
    /// </summary>
    public static class MCPForUnityMenu
    {
        // ========================================
        // Main Menu Items
        // ========================================

        /// <summary>
        /// Show the setup wizard
        /// </summary>
        [MenuItem("Window/MCP For Unity/Setup Wizard", priority = 1)]
        public static void ShowSetupWizard()
        {
            SetupWizard.ShowSetupWizard();
        }

        /// <summary>
        /// Open the main MCP For Unity window
        /// </summary>
        [MenuItem("Window/MCP For Unity/Open MCP Window %#m", priority = 2)]
        public static void OpenMCPWindow()
        {
            MCPForUnityEditorWindow.ShowWindow();
        }
        
        // ========================================
        // Maintenance Menu Items
        // ========================================
        
        /// <summary>
        /// Clear the local uvx cache for the MCP server
        /// </summary>
        [MenuItem("Window/MCP For Unity/Maintenance/Clear UVX Cache", priority = 200)]
        public static void ClearUvxCache()
        {
            if (EditorUtility.DisplayDialog(
                "Clear UVX Cache",
                "This will clear the local uvx cache for the MCP server package. " +
                "The server will be re-downloaded on next launch.\n\n" +
                "Continue?",
                "Clear Cache",
                "Cancel"))
            {
                bool success = MCPServiceLocator.Cache.ClearUvxCache();
                
                if (success)
                {
                    EditorUtility.DisplayDialog(
                        "Success",
                        "UVX cache cleared successfully. The server will be re-downloaded on next launch.",
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Error",
                        "Failed to clear UVX cache. Check the console for details.",
                        "OK");
                }
            }
        }
    }
}
