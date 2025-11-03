using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Models;
using MCPForUnity.Editor.Helpers;

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
        /// - Adds type:"stdio" for VSCode
        /// - Adds disabled:false for Windsurf/Kiro only when missing
        /// </summary>
        private static void PopulateUnityNode(JObject unity, string uvPath, McpClient client, bool isVSCode)
        {
            // Use uvx command with the package version
            string uvxCommand = AssetPathUtility.GetUvxCommand();
            unity["command"] = uvxCommand;

            unity["args"] = JArray.FromObject(new[] { "mcp-for-unity" });

            if (isVSCode)
            {
                unity["type"] = "stdio";
            }
            else
            {
                // Remove type if it somehow exists from previous clients
                if (unity["type"] != null) unity.Remove("type");
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
