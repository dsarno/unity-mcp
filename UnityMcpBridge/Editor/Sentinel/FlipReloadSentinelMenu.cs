using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Sentinel
{
    internal static class FlipReloadSentinelMenu
    {
        private const string PackageSentinelPath = "Packages/com.coplaydev.unity-mcp/Editor/Sentinel/__McpReloadSentinel.cs";

        [MenuItem("MCP/Flip Reload Sentinel")]
        private static void Flip()
        {
            try
            {
                Debug.Log("[FlipReloadSentinelMenu] Executing menu MCP/Flip Reload Sentinel");
                string path = PackageSentinelPath;
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[FlipReloadSentinelMenu] Sentinel not found at '{path}'.");
                    return;
                }

                string src = File.ReadAllText(path);
                var m = Regex.Match(src, @"(const\s+int\s+Tick\s*=\s*)(\d+)(\s*;)" );
                if (m.Success)
                {
                    string next = (m.Groups[2].Value == "1") ? "2" : "1";
                    string newSrc = src.Substring(0, m.Groups[2].Index) + next + src.Substring(m.Groups[2].Index + m.Groups[2].Length);
                    File.WriteAllText(path, newSrc);
                }
                else
                {
                    File.AppendAllText(path, "\n// MCP touch\n");
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
#if UNITY_EDITOR
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
#endif
                Debug.Log("[FlipReloadSentinelMenu] Sentinel flip menu executed successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FlipReloadSentinelMenu] Flip failed: {ex.Message}");
            }
        }
    }
}


