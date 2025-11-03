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
        private const string UvPathOverrideKey = "MCPForUnity.UvPath";
        private const string ClaudeCliPathOverrideKey = "MCPForUnity.ClaudeCliPath";

        public bool HasUvPathOverride => !string.IsNullOrEmpty(EditorPrefs.GetString(UvPathOverrideKey, null));
        public bool HasClaudeCliPathOverride => !string.IsNullOrEmpty(EditorPrefs.GetString(ClaudeCliPathOverrideKey, null));

        public string GetUvPath(bool verifyPath = true)
        {
            // If the user overrided the path in EditorPrefs, use it
            try
            {
                string overridePath = EditorPrefs.GetString(UvPathOverrideKey, string.Empty);
                if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
                {
                    if (verifyPath && VerifyUvPath(overridePath)) return overridePath;
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
                        Arguments = "uv.exe",
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
                            if (File.Exists(path) && (verifyPath ? VerifyUvPath(path) : true)) return path;
                        }
                    }
                }
                catch { }

            }
            else
            {
                // For Linux and macOS, we need to use 'which' to resolve the path
                var whichPsi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/which",
                    Arguments = "uv",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                try
                {
                    // Prepend common user-local and package manager locations so 'which' can see them in Unity's GUI env
                    string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty;
                    string prepend = string.Join(":", new[]
                    {
                            Path.Combine(homeDir, ".local", "bin"),
                            "/opt/homebrew/bin",
                            "/usr/local/bin",
                            "/usr/bin",
                            "/bin"
                        });
                    string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                    whichPsi.EnvironmentVariables["PATH"] = string.IsNullOrEmpty(currentPath) ? prepend : (prepend + ":" + currentPath);
                }
                catch { }
                using var wp = Process.Start(whichPsi);
                string output = wp.StandardOutput.ReadToEnd().Trim();
                wp.WaitForExit(3000);
                if (wp.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
                {
                    if (verifyPath && VerifyUvPath(output)) return output;
                    return output;
                }
            }

            // When there's no override and it's not set up in the PATH, fallback to a manual scan of common locations
            string[] candidates; // Define candidates based on platform
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows Store (PythonSoftwareFoundation) install location probe
                // Example: %LOCALAPPDATA%\Packages\PythonSoftwareFoundation.Python.3.13_*\LocalCache\local-packages\Python313\Scripts\uv.exe
                try
                {

                    string pkgsRoot = Path.Combine(localAppData, "Packages");
                    if (Directory.Exists(pkgsRoot))
                    {
                        var pythonPkgs = Directory.GetDirectories(pkgsRoot, "PythonSoftwareFoundation.Python.*", SearchOption.TopDirectoryOnly)
                                                 .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase);
                        foreach (var pkg in pythonPkgs)
                        {
                            string localCache = Path.Combine(pkg, "LocalCache", "local-packages");
                            if (!Directory.Exists(localCache)) continue;
                            var pyRoots = Directory.GetDirectories(localCache, "Python*", SearchOption.TopDirectoryOnly)
                                                   .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase);
                            foreach (var pyRoot in pyRoots)
                            {
                                string uvExe = Path.Combine(pyRoot, "Scripts", "uv.exe");
                                if (File.Exists(uvExe) && (!verifyPath || VerifyUvPath(uvExe))) return uvExe;
                            }
                        }
                    }
                }
                catch { }

                candidates = new[]
                {
                    // Preferred: WinGet Links shims (stable entrypoints)
                    // Per-user shim (LOCALAPPDATA) → machine-wide shim (Program Files\WinGet\Links)
                    Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "uv.exe"),
                    Path.Combine(programFiles, "WinGet", "Links", "uv.exe"),

                    // Common per-user installs
                    Path.Combine(localAppData, @"Programs\Python\Python314\Scripts\uv.exe"),
                    Path.Combine(localAppData, @"Programs\Python\Python313\Scripts\uv.exe"),
                    Path.Combine(localAppData, @"Programs\Python\Python312\Scripts\uv.exe"),
                    Path.Combine(localAppData, @"Programs\Python\Python311\Scripts\uv.exe"),
                    Path.Combine(localAppData, @"Programs\Python\Python310\Scripts\uv.exe"),
                    Path.Combine(appData, @"Python\Python314\Scripts\uv.exe"),
                    Path.Combine(appData, @"Python\Python313\Scripts\uv.exe"),
                    Path.Combine(appData, @"Python\Python312\Scripts\uv.exe"),
                    Path.Combine(appData, @"Python\Python311\Scripts\uv.exe"),
                    Path.Combine(appData, @"Python\Python310\Scripts\uv.exe"),

                    // Program Files style installs (if a native installer was used)
                    Path.Combine(programFiles, @"uv\uv.exe"),

                    // Try simple name resolution later via PATH
                    "uv.exe",
                    "uv"
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty;
                candidates = new[]
                {
                    "/opt/homebrew/bin/uv",
                    "/usr/local/bin/uv",
                    "/usr/bin/uv",
                    "/opt/local/bin/uv",
                    Path.Combine(home, ".local", "bin", "uv"),
                    "/opt/homebrew/opt/uv/bin/uv",
                    // Framework Python installs
                    "/Library/Frameworks/Python.framework/Versions/3.14/bin/uv",
                    "/Library/Frameworks/Python.framework/Versions/3.13/bin/uv",
                    "/Library/Frameworks/Python.framework/Versions/3.12/bin/uv",
                    "/Library/Frameworks/Python.framework/Versions/3.11/bin/uv",
                    "/Library/Frameworks/Python.framework/Versions/3.10/bin/uv",
                    // Fallback to PATH resolution by name
                    "uv"
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                candidates = new[]
                {
                    "/usr/local/bin/uv",
                    "/usr/bin/uv",
                    "/opt/local/bin/uv",
                    "uv"
                };
            }
            else
            {
                // Unknown platform - just try basic name resolution
                candidates = new[] { "uv" };
            }

            // Try each candidate, if we can run a basic version check then return it
            foreach (string c in candidates)
            {
                try
                {
                    if (File.Exists(c) && (!verifyPath || VerifyUvPath(c))) return c;
                }
                catch { /* ignore */ }
            }

            return null;
        }

        private static bool VerifyUvPath(string uvPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = uvPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } return false; }
                if (p.ExitCode == 0)
                {
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    return output.StartsWith("uv ");
                }
            }
            catch { }
            return false;
        }

        public string GetClaudeCliPath()
        {
            // Check for override first
            string overridePath = EditorPrefs.GetString(ClaudeCliPathOverrideKey, null);
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            {
                return overridePath;
            }

            // Fall back to automatic detection
            return ExecPath.ResolveClaude();
        }

        public bool IsPythonDetected()
        {
            try
            {
                // Windows-specific Python detection
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // Common Windows Python installation paths
                    string[] windowsCandidates =
                    {
                        @"C:\Python314\python.exe",
                        @"C:\Python313\python.exe",
                        @"C:\Python312\python.exe",
                        @"C:\Python311\python.exe",
                        @"C:\Python310\python.exe",
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python314\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python313\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python312\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python311\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python310\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Python314\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Python313\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Python312\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Python311\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Python310\python.exe"),
                    };

                    foreach (string c in windowsCandidates)
                    {
                        if (File.Exists(c)) return true;
                    }

                    // Try 'where python' command (Windows equivalent of 'which')
                    var psi = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "python",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using (var p = Process.Start(psi))
                    {
                        string outp = p.StandardOutput.ReadToEnd().Trim();
                        p.WaitForExit(2000);
                        if (p.ExitCode == 0 && !string.IsNullOrEmpty(outp))
                        {
                            string[] lines = outp.Split('\n');
                            foreach (string line in lines)
                            {
                                string trimmed = line.Trim();
                                if (File.Exists(trimmed)) return true;
                            }
                        }
                    }
                }
                else
                {
                    // macOS/Linux detection
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty;
                    string[] candidates =
                    {
                        "/opt/homebrew/bin/python3",
                        "/usr/local/bin/python3",
                        "/usr/bin/python3",
                        "/opt/local/bin/python3",
                        Path.Combine(home, ".local", "bin", "python3"),
                        "/Library/Frameworks/Python.framework/Versions/3.14/bin/python3",
                        "/Library/Frameworks/Python.framework/Versions/3.13/bin/python3",
                        "/Library/Frameworks/Python.framework/Versions/3.12/bin/python3",
                        "/Library/Frameworks/Python.framework/Versions/3.11/bin/python3",
                        "/Library/Frameworks/Python.framework/Versions/3.10/bin/python3",
                    };
                    foreach (string c in candidates)
                    {
                        if (File.Exists(c)) return true;
                    }

                    // Try 'which python3'
                    var psi = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/which",
                        Arguments = "python3",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using (var p = Process.Start(psi))
                    {
                        string outp = p.StandardOutput.ReadToEnd().Trim();
                        p.WaitForExit(2000);
                        if (p.ExitCode == 0 && !string.IsNullOrEmpty(outp) && File.Exists(outp)) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public bool IsUvDetected()
        {
            return !string.IsNullOrEmpty(GetUvPath());
        }

        public bool IsClaudeCliDetected()
        {
            return !string.IsNullOrEmpty(GetClaudeCliPath());
        }

        public void SetUvPathOverride(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                ClearUvPathOverride();
                return;
            }

            if (!File.Exists(path))
            {
                throw new ArgumentException("The selected UV executable does not exist");
            }

            EditorPrefs.SetString(UvPathOverrideKey, path);
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
            // Also update the ExecPath helper for backwards compatibility
            ExecPath.SetClaudeCliPath(path);
        }

        public void ClearUvPathOverride()
        {
            EditorPrefs.DeleteKey(UvPathOverrideKey);
        }

        public void ClearClaudeCliPathOverride()
        {
            EditorPrefs.DeleteKey(ClaudeCliPathOverrideKey);
        }
    }
}
