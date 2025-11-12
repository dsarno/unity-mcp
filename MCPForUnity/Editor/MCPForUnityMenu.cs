using MCPForUnity.Editor.Setup;
using MCPForUnity.Editor.Windows;
using UnityEditor;

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
        
    }
}
