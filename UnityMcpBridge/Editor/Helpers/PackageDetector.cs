using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// Auto-runs legacy/older install detection on package load/update (log-only).
    /// Runs once per embedded server version using an EditorPrefs version-scoped key.
    /// </summary>
    [InitializeOnLoad]
    public static class PackageDetector
    {
        private const string DetectOnceFlagKeyPrefix = "MCPForUnity.LegacyDetectLogged:";

        static PackageDetector()
        {
            try
            {
                string ver = ReadEmbeddedVersionOrFallback();
                string key = DetectOnceFlagKeyPrefix + ver;
                if (!EditorPrefs.GetBool(key, false))
                {
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            // Runs detection + logs only; EnsureServerInstalled currently logs then returns if already installed
                            ServerInstaller.EnsureServerInstalled();
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning("MCP for Unity: Auto-detect on load failed: " + ex.Message);
                        }
                        finally
                        {
                            EditorPrefs.SetBool(key, true);
                        }
                    };
                }
            }
            catch { /* ignore */ }
        }

        private static string ReadEmbeddedVersionOrFallback()
        {
            try
            {
                if (ServerPathResolver.TryFindEmbeddedServerSource(out var embeddedSrc))
                {
                    var p = System.IO.Path.Combine(embeddedSrc, "server_version.txt");
                    if (System.IO.File.Exists(p))
                        return (System.IO.File.ReadAllText(p)?.Trim() ?? "unknown");
                }
            }
            catch { }
            return "unknown";
        }
    }
}


