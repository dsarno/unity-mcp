using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Models;
using MCPForUnity.Editor.Helpers;
using UnityEditor;

namespace MCPForUnity.Editor.Helpers
{
    public static class ConfigJsonBuilder
    {
        public static string BuildManualConfigJson(string uvPath, McpClient client)
        {
            var root = new JObject();
            bool isVSCode = client?.mcpType == McpTypes.VSCode;
            JObject container;
            if (isVSCode)
            {
                container = EnsureObject(root, "servers");
            }
            else
            {
                container = EnsureObject(root, "mcpServers");
            }

            var unity = new JObject();
            PopulateUnityNode(unity, uvPath, client, isVSCode);

            container["unityMCP"] = unity;

            return root.ToString(Formatting.Indented);
        }

        public static JObject ApplyUnityServerToExistingConfig(JObject root, string uvPath, McpClient client)
        {
            if (root == null) root = new JObject();
            bool isVSCode = client?.mcpType == McpTypes.VSCode;
            JObject container = isVSCode ? EnsureObject(root, "servers") : EnsureObject(root, "mcpServers");
            JObject unity = container["unityMCP"] as JObject ?? new JObject();
            PopulateUnityNode(unity, uvPath, client, isVSCode);

            container["unityMCP"] = unity;
            return root;
        }

        /// <summary>
        /// Centralized builder that applies all caveats consistently.
        /// - Sets command/args with uvx and package version
        /// - Ensures env exists
        /// - Adds transport configuration (HTTP or stdio)
        /// - Adds disabled:false for Windsurf/Kiro only when missing
        /// </summary>
        private static void PopulateUnityNode(JObject unity, string uvPath, McpClient client, bool isVSCode)
        {
            // Get transport preference (default to HTTP)
            bool useHttpTransport = EditorPrefs.GetBool("MCPForUnity.UseHttpTransport", true);
            
            if (useHttpTransport)
            {
                // HTTP mode: Use URL, no command
                string httpUrl = HttpEndpointUtility.GetMcpRpcUrl();
                unity["url"] = httpUrl;
                
                // Remove command/args if they exist from previous config
                if (unity["command"] != null) unity.Remove("command");
                if (unity["args"] != null) unity.Remove("args");
                
                if (isVSCode)
                {
                    unity["type"] = "http";
                }
            }
            else
            {
                // Stdio mode: Use uvx command
                var (uvxPath, fromUrl, packageName) = AssetPathUtility.GetUvxCommandParts();
                
                unity["command"] = uvxPath;
                
                var args = new List<string> { packageName };
                if (!string.IsNullOrEmpty(fromUrl))
                {
                    args.Insert(0, fromUrl);
                    args.Insert(0, "--from");
                }
                
                args.Add("--transport");
                args.Add("stdio");
                
                unity["args"] = JArray.FromObject(args.ToArray());
                
                // Remove url if it exists from previous config
                if (unity["url"] != null) unity.Remove("url");
                
                if (isVSCode)
                {
                    unity["type"] = "stdio";
                }
            }
            
            // Remove type for non-VSCode clients
            if (!isVSCode && unity["type"] != null)
            {
                unity.Remove("type");
            }

            if (client != null && (client.mcpType == McpTypes.Windsurf || client.mcpType == McpTypes.Kiro))
            {
                if (unity["env"] == null)
                {
                    unity["env"] = new JObject();
                }

                if (unity["disabled"] == null)
                {
                    unity["disabled"] = false;
                }
            }
        }

        private static JObject EnsureObject(JObject parent, string name)
        {
            if (parent[name] is JObject o) return o;
            var created = new JObject();
            parent[name] = created;
            return created;
        }
    }
}
