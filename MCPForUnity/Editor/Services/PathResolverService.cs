using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MCPForUnity.Editor.Helpers;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Implementation of path resolver service with override support
    /// </summary>
    public class PathResolverService : IPathResolverService
    {
        private const string UvxPathOverrideKey = "MCPForUnity.UvxPath";
        private const string ClaudeCliPathOverrideKey = "MCPForUnity.ClaudeCliPath";

        public bool HasUvxPathOverride => !string.IsNullOrEmpty(EditorPrefs.GetString(UvxPathOverrideKey, null));
        public bool HasClaudeCliPathOverride => !string.IsNullOrEmpty(EditorPrefs.GetString(ClaudeCliPathOverrideKey, null));

        public string GetUvxPath(bool verifyPath = true)
        {
            // If the user overrided the path in EditorPrefs, use it
            try
            {
                string overridePath = EditorPrefs.GetString(UvxPathOverrideKey, string.Empty);
                if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
                {
                    if (verifyPath && VerifyUvxPath(overridePath)) return overridePath;
                    return overridePath;
                }
            }
            catch { /* ignore */ }

            // Get environment variables
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) ?? string.Empty;
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) ?? string.Empty;
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ?? string.Empty;

            // Then let's check if it's available via PATH
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var wherePsi = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "uvx.exe",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using var wp = Process.Start(wherePsi);
                    string output = wp.StandardOutput.ReadToEnd().Trim();
                    wp.WaitForExit(1500);
                    if (wp.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string path = line.Trim();
                            if (File.Exists(path) && (verifyPath ? VerifyUvxPath(path) : true)) return path;
                        }
                    }
                }
                catch { }

                // Try common installation paths
                string[] candidates = new[]
                {
                    Path.Combine(programFiles, "uvx", "bin", "uvx.exe"),
                    Path.Combine(localAppData, "uvx", "bin", "uvx.exe"),
                    Path.Combine(localAppData, "Python", "Python310", "Scripts", "uvx.exe"),
                    Path.Combine(localAppData, "Programs", "uvx", "uvx.exe"),
                    Path.Combine(localAppData, "Microsoft", "WindowsApps", "uvx.exe")
                };

                foreach (var c in candidates)
                {
                    if (File.Exists(c) && (verifyPath ? VerifyUvxPath(c) : true)) return c;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Check for uvx in common macOS locations
                string[] candidates = new[]
                {
                    "/opt/homebrew/bin/uvx",
                    "/usr/local/bin/uvx",
                    Path.Combine(appData, "uvx", "bin", "uvx"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local", "bin", "uvx")
                };

                foreach (var c in candidates)
                {
                    if (File.Exists(c) && (verifyPath ? VerifyUvxPath(c) : true)) return c;
                }

                // Try 'which uvx' command
                try
                {
                    var whichPsi = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = "-c \"which uvx\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using var wp = Process.Start(whichPsi);
                    string output = wp.StandardOutput.ReadToEnd().Trim();
                    wp.WaitForExit(1500);
                    if (wp.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output) &&
                        (verifyPath ? VerifyUvxPath(output) : true))
                    {
                        return output;
                    }
                }
                catch { }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Check for uvx in common Linux locations
                string[] candidates = new[]
                {
                    "/usr/bin/uvx",
                    "/usr/local/bin/uvx",
                    Path.Combine(appData, "uvx", "bin", "uvx"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local", "bin", "uvx")
                };

                foreach (var c in candidates)
                {
                    if (File.Exists(c) && (verifyPath ? VerifyUvxPath(c) : true)) return c;
                }

                // Try 'which uvx' command
                try
                {
                    var whichPsi = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = "-c \"which uvx\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using var wp = Process.Start(whichPsi);
                    string output = wp.StandardOutput.ReadToEnd().Trim();
                    wp.WaitForExit(1500);
                    if (wp.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output) &&
                        (verifyPath ? VerifyUvxPath(output) : true))
                    {
                        return output;
                    }
                }
                catch { }
            }

            // Fallback: try just "uvx" (may work if in PATH)
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // On Windows, try uvx.exe directly
                    if (verifyPath ? VerifyUvxPath("uvx.exe") : true) return "uvx.exe";
                }
                else
                {
                    // On Unix-like systems, try uvx
                    if (verifyPath ? VerifyUvxPath("uvx") : true) return "uvx";
                }
            }
            catch { }

            return null;
        }

        public string GetClaudeCliPath()
        {
            try
            {
                string overridePath = EditorPrefs.GetString(ClaudeCliPathOverrideKey, string.Empty);
                if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
                {
                    return overridePath;
                }
            }
            catch { /* ignore */ }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string[] candidates = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "claude", "claude.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "claude", "claude.exe"),
                    "claude.exe"
                };

                foreach (var c in candidates)
                {
                    if (File.Exists(c)) return c;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string[] candidates = new[]
                {
                    "/opt/homebrew/bin/claude",
                    "/usr/local/bin/claude",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local", "bin", "claude")
                };

                foreach (var c in candidates)
                {
                    if (File.Exists(c)) return c;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string[] candidates = new[]
                {
                    "/usr/bin/claude",
                    "/usr/local/bin/claude",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local", "bin", "claude")
                };

                foreach (var c in candidates)
                {
                    if (File.Exists(c)) return c;
                }
            }

            return null;
        }

        public bool IsPythonDetected()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python.exe" : "python3",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                p.WaitForExit(2000);
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public bool IsUvxDetected()
        {
            return !string.IsNullOrEmpty(GetUvxPath());
        }

        public bool IsClaudeCliDetected()
        {
            return !string.IsNullOrEmpty(GetClaudeCliPath());
        }

        public void SetUvxPathOverride(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                ClearUvxPathOverride();
                return;
            }

            if (!File.Exists(path))
            {
                throw new ArgumentException("The selected UVX executable does not exist");
            }

            EditorPrefs.SetString(UvxPathOverrideKey, path);
        }

        public void SetClaudeCliPathOverride(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                ClearClaudeCliPathOverride();
                return;
            }

            if (!File.Exists(path))
            {
                throw new ArgumentException("The selected Claude CLI executable does not exist");
            }

            EditorPrefs.SetString(ClaudeCliPathOverrideKey, path);
        }

        public void ClearUvxPathOverride()
        {
            EditorPrefs.DeleteKey(UvxPathOverrideKey);
        }

        public void ClearClaudeCliPathOverride()
        {
            EditorPrefs.DeleteKey(ClaudeCliPathOverrideKey);
        }

        private static bool VerifyUvxPath(string uvxPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = uvxPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(2000);
                return p.ExitCode == 0 && output.Contains("uvx");
            }
            catch
            {
                return false;
            }
        }
    }
}
