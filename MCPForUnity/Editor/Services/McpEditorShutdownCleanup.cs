using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Best-effort cleanup when the Unity Editor is quitting.
    /// - Stops active transports so clients don't see a "hung" session longer than necessary.
    /// - If HTTP Local is selected, attempts to stop the local HTTP server (guarded by PID heuristics).
    /// </summary>
    [InitializeOnLoad]
    internal static class McpEditorShutdownCleanup
    {
        static McpEditorShutdownCleanup()
        {
            // Guard against duplicate subscriptions across domain reloads.
            try { EditorApplication.quitting -= OnEditorQuitting; } catch { }
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnEditorQuitting()
        {
            // 1) Stop transports (best-effort, bounded wait).
            try
            {
                var transport = MCPServiceLocator.TransportManager;

                Task stopHttp = transport.StopAsync(TransportMode.Http);
                Task stopStdio = transport.StopAsync(TransportMode.Stdio);

                try { Task.WaitAll(new[] { stopHttp, stopStdio }, 750); } catch { }
            }
            catch (Exception ex)
            {
                // Avoid hard failures on quit.
                McpLog.Warn($"Shutdown cleanup: failed to stop transports: {ex.Message}");
            }

            // 2) Stop local HTTP server if the user selected HTTP Local (best-effort).
            try
            {
                bool useHttp = EditorPrefs.GetBool(EditorPrefKeys.UseHttpTransport, true);
                if (!useHttp)
                {
                    return;
                }

                // Prefer explicit scope if present; fall back to URL heuristics for backward compatibility.
                string scope = string.Empty;
                try { scope = EditorPrefs.GetString(EditorPrefKeys.HttpTransportScope, string.Empty); } catch { }

                bool httpLocalSelected = string.Equals(scope, "local", StringComparison.OrdinalIgnoreCase)
                                         || (string.IsNullOrEmpty(scope) && MCPServiceLocator.Server.IsLocalUrl());

                if (!httpLocalSelected)
                {
                    return;
                }

                // StopLocalHttpServer is already guarded to only terminate processes that look like mcp-for-unity.
                MCPServiceLocator.Server.StopLocalHttpServer();
            }
            catch (Exception ex)
            {
                McpLog.Warn($"Shutdown cleanup: failed to stop local HTTP server: {ex.Message}");
            }
        }
    }
}


