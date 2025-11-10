using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Data;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Service for managing MCP server lifecycle
    /// </summary>
    public class ServerManagementService : IServerManagementService
    {
        /// <summary>
        /// Start the local HTTP server in a new terminal window
        /// </summary>
        public bool StartLocalHttpServer()
        {
            // Check if HTTP transport is enabled
            bool useHttpTransport = EditorPrefs.GetBool("MCPForUnity.UseHttpTransport", true);
            if (!useHttpTransport)
            {
                EditorUtility.DisplayDialog(
                    "HTTP Transport Disabled",
                    "HTTP transport is not enabled. Please enable it in the MCP For Unity window first.",
                    "OK");
                return false;
            }
            
            // Get the HTTP URL
            string httpUrl = EditorPrefs.GetString("MCPForUnity.HttpUrl", "http://localhost:8080");
            
            // Check if it's a local URL
            if (!IsLocalUrl())
            {
                EditorUtility.DisplayDialog(
                    "Remote Server",
                    $"The configured URL ({httpUrl}) is not a local address.\n\n" +
                    "This operation is only for starting a local server. " +
                    "For remote servers, please start the server manually on the remote machine.",
                    "OK");
                return false;
            }
            
            // Get uvx command parts
            var (uvxPath, fromUrl, packageName) = AssetPathUtility.GetUvxCommandParts();
            
            if (string.IsNullOrEmpty(uvxPath))
            {
                EditorUtility.DisplayDialog(
                    "UVX Not Found",
                    "UVX is not installed or not found in PATH. Please install UV/UVX first.",
                    "OK");
                return false;
            }
            
            // Build the command
            string args = string.IsNullOrEmpty(fromUrl) 
                ? $"{packageName} --transport http --http-url {httpUrl}"
                : $"--from {fromUrl} {packageName} --transport http --http-url {httpUrl}";
            
            // Start the server in a terminal
            string command = $"{uvxPath} {args}";
            
            if (EditorUtility.DisplayDialog(
                "Start Local HTTP Server",
                $"This will start the MCP server in HTTP mode:\n\n{command}\n\n" +
                "The server will run in a separate terminal window. " +
                "Close the terminal to stop the server.\n\n" +
                "Continue?",
                "Start Server",
                "Cancel"))
            {
                try
                {
                    // Start the server in a new terminal window (cross-platform)
                    var startInfo = CreateTerminalProcessStartInfo(command);
                    
                    System.Diagnostics.Process.Start(startInfo);
                    
                    McpLog.Info($"Started local HTTP server: {command}");
                    EditorUtility.DisplayDialog(
                        "Server Started",
                        "The MCP server has been started in a new terminal window.\n\n" +
                        "Close the terminal window to stop the server.",
                        "OK");
                    return true;
                }
                catch (Exception ex)
                {
                    McpLog.Error($"Failed to start server: {ex.Message}");
                    EditorUtility.DisplayDialog(
                        "Error",
                        $"Failed to start server: {ex.Message}",
                        "OK");
                    return false;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if the configured HTTP URL is a local address
        /// </summary>
        public bool IsLocalUrl()
        {
            string httpUrl = EditorPrefs.GetString("MCPForUnity.HttpUrl", "http://localhost:8080");
            return IsLocalUrl(httpUrl);
        }
        
        /// <summary>
        /// Check if a URL is local (localhost, 127.0.0.1, 0.0.0.0)
        /// </summary>
        private static bool IsLocalUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            
            try
            {
                var uri = new Uri(url);
                string host = uri.Host.ToLower();
                return host == "localhost" || host == "127.0.0.1" || host == "0.0.0.0" || host == "::1";
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Check if the local HTTP server can be started
        /// </summary>
        public bool CanStartLocalServer()
        {
            bool useHttpTransport = EditorPrefs.GetBool("MCPForUnity.UseHttpTransport", true);
            return useHttpTransport && IsLocalUrl();
        }
        
        /// <summary>
        /// Creates a ProcessStartInfo for opening a terminal window with the given command
        /// Works cross-platform: macOS, Windows, and Linux
        /// </summary>
        private System.Diagnostics.ProcessStartInfo CreateTerminalProcessStartInfo(string command)
        {
#if UNITY_EDITOR_OSX
            // macOS: Use osascript to open Terminal.app
            return new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"osascript -e 'tell app \\\"Terminal\\\" to do script \\\"{command}\\\"'\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
#elif UNITY_EDITOR_WIN
            // Windows: Use cmd.exe with start command to open new window
            return new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start cmd.exe /k \"{command}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
#else
            // Linux: Try common terminal emulators
            // Priority: gnome-terminal, xterm, konsole, xfce4-terminal
            string[] terminals = { "gnome-terminal", "xterm", "konsole", "xfce4-terminal" };
            string terminalCmd = null;
            
            foreach (var term in terminals)
            {
                try
                {
                    var which = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "which",
                        Arguments = term,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });
                    which.WaitForExit();
                    if (which.ExitCode == 0)
                    {
                        terminalCmd = term;
                        break;
                    }
                }
                catch { }
            }
            
            if (terminalCmd == null)
            {
                terminalCmd = "xterm"; // Fallback
            }
            
            // Different terminals have different argument formats
            string args;
            if (terminalCmd == "gnome-terminal")
            {
                args = $"-- bash -c \"{command}; exec bash\"";
            }
            else if (terminalCmd == "konsole")
            {
                args = $"-e bash -c \"{command}; exec bash\"";
            }
            else if (terminalCmd == "xfce4-terminal")
            {
                args = $"--hold -e \"bash -c '{command}'\"";
            }
            else // xterm and others
            {
                args = $"-hold -e bash -c \"{command}\"";
            }
            
            return new System.Diagnostics.ProcessStartInfo
            {
                FileName = terminalCmd,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            };
#endif
        }
    }
}
