using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MCPForUnity.External.Tommy;
using Newtonsoft.Json;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// Codex CLI specific configuration helpers. Handles TOML snippet
    /// generation and lightweight parsing so Codex can join the auto-setup
    /// flow alongside JSON-based clients.
    /// </summary>
    public static class CodexConfigHelper
    {
        public static bool IsCodexConfigured(string pythonDir)
        {
            try
            {
                string basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(basePath)) return false;

                string configPath = Path.Combine(basePath, ".codex", "config.toml");
                if (!File.Exists(configPath)) return false;

                string toml = File.ReadAllText(configPath);
                if (!TryParseCodexServer(toml, out _, out var args)) return false;

                string dir = McpConfigFileHelper.ExtractDirectoryArg(args);
                if (string.IsNullOrEmpty(dir)) return false;

                return McpConfigFileHelper.PathsEqual(dir, pythonDir);
            }
            catch
            {
                return false;
            }
        }

        public static string BuildCodexServerBlock(string uvPath, string serverSrc)
        {
            string argsArray = FormatTomlStringArray(new[] { "run", "--directory", serverSrc, "server.py" });

            var sb = new StringBuilder();
            sb.AppendLine("[mcp_servers.unityMCP]");
            sb.AppendLine($"command = \"{EscapeTomlString(uvPath)}\"");
            sb.AppendLine($"args = {argsArray}");
            sb.AppendLine($"startup_timeout_sec = 30");

            // Windows-specific environment block to help Codex locate needed paths
            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty;
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ?? string.Empty; // Roaming
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) ?? string.Empty;
                    string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) ?? string.Empty;
                    string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) ?? string.Empty;
                    string systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? (Path.GetPathRoot(userProfile)?.TrimEnd('\\', '/') ?? "C:");
                    string systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? Path.Combine(systemDrive + "\\", "Windows");
                    string comspec = Environment.GetEnvironmentVariable("COMSPEC") ?? Path.Combine(Environment.SystemDirectory ?? (systemRoot + "\\System32"), "cmd.exe");
                    string homeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
                    string homePath = Environment.GetEnvironmentVariable("HOMEPATH");
                    if (string.IsNullOrEmpty(homeDrive))
                    {
                        homeDrive = systemDrive;
                    }
                    if (string.IsNullOrEmpty(homePath) && !string.IsNullOrEmpty(userProfile))
                    {
                        // Derive HOMEPATH from USERPROFILE (e.g., C:\\Users\\name -> \\Users\\name)
                        if (userProfile.StartsWith(homeDrive + "\\", StringComparison.OrdinalIgnoreCase))
                        {
                            homePath = userProfile.Substring(homeDrive.Length);
                        }
                        else
                        {
                            try
                            {
                                var root = Path.GetPathRoot(userProfile) ?? string.Empty; // e.g., C:\\
                                homePath = userProfile.Substring(root.Length - 1); // keep leading backslash
                            }
                            catch { homePath = "\\"; }
                        }
                    }

                    string powershell = Path.Combine(Environment.SystemDirectory ?? (systemRoot + "\\System32"), "WindowsPowerShell\\v1.0\\powershell.exe");
                    string pwsh = Path.Combine(programFiles, "PowerShell\\7\\pwsh.exe");

                    string tempDir = Path.Combine(localAppData, "Temp");

                    sb.AppendLine();
                    sb.AppendLine("[mcp_servers.unityMCP.env]");
                    sb.AppendLine($"SystemRoot = \"{EscapeTomlString(systemRoot)}\"");
                    sb.AppendLine($"APPDATA = \"{EscapeTomlString(appData)}\"");
                    sb.AppendLine($"COMSPEC = \"{EscapeTomlString(comspec)}\"");
                    sb.AppendLine($"HOMEDRIVE = \"{EscapeTomlString(homeDrive?.TrimEnd('\\') ?? string.Empty)}\"");
                    sb.AppendLine($"HOMEPATH = \"{EscapeTomlString(homePath ?? string.Empty)}\"");
                    sb.AppendLine($"LOCALAPPDATA = \"{EscapeTomlString(localAppData)}\"");
                    sb.AppendLine($"POWERSHELL = \"{EscapeTomlString(powershell)}\"");
                    sb.AppendLine($"PROGRAMDATA = \"{EscapeTomlString(programData)}\"");
                    sb.AppendLine($"PROGRAMFILES = \"{EscapeTomlString(programFiles)}\"");
                    sb.AppendLine($"PWSH = \"{EscapeTomlString(pwsh)}\"");
                    sb.AppendLine($"SYSTEMDRIVE = \"{EscapeTomlString(systemDrive)}\"");
                    sb.AppendLine($"SYSTEMROOT = \"{EscapeTomlString(systemRoot)}\"");
                    sb.AppendLine($"TEMP = \"{EscapeTomlString(tempDir)}\"");
                    sb.AppendLine($"TMP = \"{EscapeTomlString(tempDir)}\"");
                    sb.AppendLine($"USERPROFILE = \"{EscapeTomlString(userProfile)}\"");
                }
            }
            catch { /* best effort */ }

            return sb.ToString();
        }

        public static string UpsertCodexServerBlock(string existingToml, string newBlock)
        {
            if (string.IsNullOrWhiteSpace(existingToml))
            {
                // Default to snake_case section when creating new files
                return newBlock.TrimEnd() + Environment.NewLine;
            }

            StringBuilder sb = new StringBuilder();
            using StringReader reader = new StringReader(existingToml);
            string line;
            bool inTarget = false;
            bool replaced = false;

            // Support both TOML section casings and nested subtables (e.g., env)
            // Prefer the casing already present in the user's file; fall back to snake_case
            bool hasCamelSection = existingToml.IndexOf("[mcpServers.unityMCP]", StringComparison.OrdinalIgnoreCase) >= 0
                                   || existingToml.IndexOf("[mcpServers.unityMCP.", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasSnakeSection = existingToml.IndexOf("[mcp_servers.unityMCP]", StringComparison.OrdinalIgnoreCase) >= 0
                                   || existingToml.IndexOf("[mcp_servers.unityMCP.", StringComparison.OrdinalIgnoreCase) >= 0;
            bool preferCamel = hasCamelSection || (!hasSnakeSection && existingToml.IndexOf("[mcpServers]", StringComparison.OrdinalIgnoreCase) >= 0);

            // Prepare block variants matching the chosen casing, including nested tables
            string newBlockCamel = newBlock
                .Replace("[mcp_servers.unityMCP.env]", "[mcpServers.unityMCP.env]")
                .Replace("[mcp_servers.unityMCP]", "[mcpServers.unityMCP]");
            string newBlockEffective = preferCamel ? newBlockCamel : newBlock;

            static bool IsSection(string s)
            {
                string t = s.Trim();
                return t.StartsWith("[") && t.EndsWith("]") && !t.StartsWith("[[");
            }

            static string SectionName(string header)
            {
                string t = header.Trim();
                if (t.StartsWith("[") && t.EndsWith("]")) t = t.Substring(1, t.Length - 2);
                return t;
            }

            bool TargetOrChild(string section)
            {
                // Compare case-insensitively; accept both snake and camel as the same logical table
                string name = SectionName(section);
                return name.StartsWith("mcp_servers.unityMCP", StringComparison.OrdinalIgnoreCase)
                       || name.StartsWith("mcpServers.unityMCP", StringComparison.OrdinalIgnoreCase);
            }

            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                bool isSection = IsSection(trimmed);
                if (isSection)
                {
                    // If we encounter the target section or any of its nested tables, mark/keep in-target
                    if (TargetOrChild(trimmed))
                    {
                        if (!replaced)
                        {
                            if (sb.Length > 0 && sb[^1] != '\n') sb.AppendLine();
                            sb.AppendLine(newBlockEffective.TrimEnd());
                            replaced = true;
                        }
                        inTarget = true;
                        continue;
                    }

                    // A new unrelated section ends the target region
                    if (inTarget)
                    {
                        inTarget = false;
                    }
                }

                if (inTarget)
                {
                    continue;
                }

                sb.AppendLine(line);
            }

            if (!replaced)
            {
                if (sb.Length > 0 && sb[^1] != '\n') sb.AppendLine();
                sb.AppendLine(newBlockEffective.TrimEnd());
            }

            return sb.ToString().TrimEnd() + Environment.NewLine;
        }

        public static bool TryParseCodexServer(string toml, out string command, out string[] args)
        {
            command = null;
            args = null;
            if (string.IsNullOrWhiteSpace(toml)) return false;

            try
            {
                using var reader = new StringReader(toml);
                TomlTable root = TOML.Parse(reader);
                if (root == null) return false;

                if (!TryGetTable(root, "mcp_servers", out var servers)
                    && !TryGetTable(root, "mcpServers", out servers))
                {
                    return false;
                }

                if (!TryGetTable(servers, "unityMCP", out var unity))
                {
                    return false;
                }

                command = GetTomlString(unity, "command");
                args = GetTomlStringArray(unity, "args");

                return !string.IsNullOrEmpty(command) && args != null;
            }
            catch (TomlParseException)
            {
                return false;
            }
            catch (TomlSyntaxException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool TryGetTable(TomlTable parent, string key, out TomlTable table)
        {
            table = null;
            if (parent == null) return false;

            if (parent.TryGetNode(key, out var node))
            {
                if (node is TomlTable tbl)
                {
                    table = tbl;
                    return true;
                }

                if (node is TomlArray array)
                {
                    var firstTable = array.Children.OfType<TomlTable>().FirstOrDefault();
                    if (firstTable != null)
                    {
                        table = firstTable;
                        return true;
                    }
                }
            }

            return false;
        }

        private static string GetTomlString(TomlTable table, string key)
        {
            if (table != null && table.TryGetNode(key, out var node))
            {
                if (node is TomlString str) return str.Value;
                if (node.HasValue) return node.ToString();
            }
            return null;
        }

        private static string[] GetTomlStringArray(TomlTable table, string key)
        {
            if (table == null) return null;
            if (!table.TryGetNode(key, out var node)) return null;

            if (node is TomlArray array)
            {
                List<string> values = new List<string>();
                foreach (TomlNode element in array.Children)
                {
                    if (element is TomlString str)
                    {
                        values.Add(str.Value);
                    }
                    else if (element.HasValue)
                    {
                        values.Add(element.ToString());
                    }
                }

                return values.Count > 0 ? values.ToArray() : Array.Empty<string>();
            }

            if (node is TomlString single)
            {
                return new[] { single.Value };
            }

            return null;
        }

        private static string FormatTomlStringArray(IEnumerable<string> values)
        {
            if (values == null) return "[]";
            StringBuilder sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (string value in values)
            {
                if (!first)
                {
                    sb.Append(", ");
                }
                sb.Append('"').Append(EscapeTomlString(value ?? string.Empty)).Append('"');
                first = false;
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string EscapeTomlString(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

    }
}
