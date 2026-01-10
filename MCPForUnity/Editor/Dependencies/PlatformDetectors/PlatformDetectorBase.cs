using System;
using System.Diagnostics;
using System.IO;
using MCPForUnity.Editor.Dependencies.Models;
using MCPForUnity.Editor.Services;

namespace MCPForUnity.Editor.Dependencies.PlatformDetectors
{
    /// <summary>
    /// Base class for platform-specific dependency detection
    /// </summary>
    public abstract class PlatformDetectorBase : IPlatformDetector
    {
        public abstract string PlatformName { get; }
        public abstract bool CanDetect { get; }

        public abstract DependencyStatus DetectPython();
        public abstract string GetPythonInstallUrl();
        public abstract string GetUvInstallUrl();
        public abstract string GetInstallationRecommendations();

        public virtual DependencyStatus DetectUv()
        {
            var status = new DependencyStatus("uv Package Manager", isRequired: true)
            {
                InstallationHint = GetUvInstallUrl()
            };

            try
            {
                // Use PathResolverService to get uvx path (respects user overrides)
                var pathResolver = new PathResolverService();
                string uvxPath = pathResolver.GetUvxPath();
                bool hasOverride = pathResolver.HasUvxPathOverride;

                // Try to get version from the resolved path
                if (TryGetUvVersion(uvxPath, out string version))
                {
                    status.IsAvailable = true;
                    status.Version = version;
                    status.Path = uvxPath;
                    status.Details = hasOverride
                        ? $"Found uv {version} (using override path: {uvxPath})"
                        : $"Found uv {version} at {uvxPath}";
                    return status;
                }

                // Fall back to PATH-based detection if resolved path didn't work
                if (TryFindUvInPath(out string pathUv, out string pathVersion))
                {
                    status.IsAvailable = true;
                    status.Version = pathVersion;
                    status.Path = pathUv;
                    status.Details = $"Found uv {pathVersion} in PATH";
                    return status;
                }

                status.ErrorMessage = "uv not found";
                status.Details = hasOverride
                    ? $"The override path '{uvxPath}' is not a valid uv executable. Install uv package manager or update the override path."
                    : "Install uv package manager and ensure it's added to PATH.";
            }
            catch (Exception ex)
            {
                status.ErrorMessage = $"Error detecting uv: {ex.Message}";
            }

            return status;
        }

        /// <summary>
        /// Attempts to get the version from a specific uv/uvx executable path.
        /// </summary>
        protected bool TryGetUvVersion(string uvPath, out string version)
        {
            version = null;

            if (string.IsNullOrEmpty(uvPath))
                return false;

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

                using var process = Process.Start(psi);
                if (process == null) return false;

                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);

                if (process.ExitCode == 0 && output.StartsWith("uv "))
                {
                    version = output.Substring(3).Trim();
                    return true;
                }
            }
            catch
            {
                // Path doesn't exist or isn't executable
            }

            return false;
        }

        protected bool TryFindUvInPath(out string uvPath, out string version)
        {
            uvPath = null;
            version = null;

            // Try common uv command names
            var commands = new[] { "uvx", "uv" };

            foreach (var cmd in commands)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = cmd,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process == null) continue;

                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(5000);

                    if (process.ExitCode == 0 && output.StartsWith("uv "))
                    {
                        version = output.Substring(3).Trim();
                        uvPath = cmd;
                        return true;
                    }
                }
                catch
                {
                    // Try next command
                }
            }

            return false;
        }

        protected bool TryParseVersion(string version, out int major, out int minor)
        {
            major = 0;
            minor = 0;

            try
            {
                var parts = version.Split('.');
                if (parts.Length >= 2)
                {
                    return int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor);
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return false;
        }
    }
}
