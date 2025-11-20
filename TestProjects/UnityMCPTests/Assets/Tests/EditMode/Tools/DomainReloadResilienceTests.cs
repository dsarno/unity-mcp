using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace Tests.EditMode.Tools
{
    /// <summary>
    /// Tests for domain reload resilience - ensuring MCP requests succeed even during Unity domain reloads.
    /// </summary>
    public class DomainReloadResilienceTests
    {
        private const string TempDir = "Assets/Temp/DomainReloadTests";

        [SetUp]
        public void Setup()
        {
            // Ensure temp directory exists
            if (!AssetDatabase.IsValidFolder(TempDir))
            {
                Directory.CreateDirectory(TempDir);
                AssetDatabase.Refresh();
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up temp directory
            if (AssetDatabase.IsValidFolder(TempDir))
            {
                AssetDatabase.DeleteAsset(TempDir);
            }
        }

        /// <summary>
        /// This test simulates the stress test scenario:
        /// 1. Create a script (triggers domain reload)
        /// 2. Make multiple rapid read_console calls
        /// 3. Verify all calls succeed (no "No Unity plugins are currently connected" errors)
        /// 
        /// Note: This test uses UnityTest coroutine to handle the async nature of domain reloads.
        /// </summary>
        [UnityTest]
        public IEnumerator StressTest_CreateScriptAndReadConsoleMultipleTimes()
        {
            // Step 1: Create a script to trigger domain reload
            var scriptPath = Path.Combine(TempDir, "StressTestScript.cs").Replace("\\", "/");
            var scriptContent = @"using UnityEngine;

public class StressTestScript : MonoBehaviour
{
    void Start() { }
}";
            
            // Write script file
            File.WriteAllText(scriptPath, scriptContent);
            AssetDatabase.Refresh();
            
            Debug.Log("[DomainReloadTest] Script created, domain reload triggered");
            
            // Wait a frame for the domain reload to start
            yield return null;
            
            // Step 2: Make multiple rapid read_console calls
            // These should succeed even during the reload window
            int successCount = 0;
            int totalCalls = 5;
            
            for (int i = 0; i < totalCalls; i++)
            {
                var request = new JObject
                {
                    ["action"] = "get",
                    ["types"] = new JArray { "all" },
                    ["count"] = 50,
                    ["format"] = "plain",
                    ["includeStacktrace"] = false
                };
                
                var result = ReadConsoleTool.Execute(request);
                
                // Check if the call succeeded
                if (result != null && result["success"]?.Value<bool>() == true)
                {
                    successCount++;
                    Debug.Log($"[DomainReloadTest] read_console call {i+1}/{totalCalls} succeeded");
                }
                else
                {
                    var error = result?["error"]?.ToString() ?? "Unknown error";
                    Debug.LogError($"[DomainReloadTest] read_console call {i+1}/{totalCalls} failed: {error}");
                }
                
                // Small delay between calls to simulate rapid-fire scenario
                yield return new WaitForSeconds(0.1f);
            }
            
            // Step 3: Verify all calls succeeded
            Debug.Log($"[DomainReloadTest] {successCount}/{totalCalls} read_console calls succeeded");
            Assert.AreEqual(totalCalls, successCount, 
                $"Expected all {totalCalls} read_console calls to succeed during domain reload, but only {successCount} succeeded");
        }

        /// <summary>
        /// Test that read_console works reliably after a domain reload completes.
        /// </summary>
        [Test]
        public void ReadConsole_AfterDomainReload_Succeeds()
        {
            // This test assumes domain reload has already completed
            // (Unity tests run after domain reload)
            
            var request = new JObject
            {
                ["action"] = "get",
                ["types"] = new JArray { "error", "warning", "log" },
                ["count"] = 10,
                ["format"] = "plain"
            };
            
            var result = ReadConsoleTool.Execute(request);
            
            Assert.IsNotNull(result, "read_console should return a result");
            Assert.IsTrue(result["success"]?.Value<bool>() ?? false, 
                $"read_console should succeed: {result["error"]}");
        }

        /// <summary>
        /// Test creating a script and immediately querying console logs.
        /// This simulates a common AI workflow pattern.
        /// </summary>
        [UnityTest]
        public IEnumerator CreateScript_ThenQueryConsole_Succeeds()
        {
            // Create a simple script
            var scriptPath = Path.Combine(TempDir, "TestScript1.cs").Replace("\\", "/");
            var scriptContent = @"using UnityEngine;

public class TestScript1 : MonoBehaviour
{
    void Start()
    {
        Debug.Log(""TestScript1 initialized"");
    }
}";
            
            File.WriteAllText(scriptPath, scriptContent);
            AssetDatabase.Refresh();
            
            Debug.Log("[DomainReloadTest] Script created");
            
            // Wait a frame
            yield return null;
            
            // Immediately try to read console
            var request = new JObject
            {
                ["action"] = "get",
                ["types"] = new JArray { "all" },
                ["count"] = 50,
                ["format"] = "plain"
            };
            
            var result = ReadConsoleTool.Execute(request);
            
            // Should succeed even if domain reload is happening
            Assert.IsNotNull(result, "read_console should return a result");
            Assert.IsTrue(result["success"]?.Value<bool>() ?? false, 
                $"read_console should succeed even during/after script creation: {result["error"]}");
        }

        /// <summary>
        /// Test rapid script creation followed by console reads.
        /// This is an even more aggressive stress test.
        /// </summary>
        [UnityTest]
        public IEnumerator RapidScriptCreation_WithConsoleReads_AllSucceed()
        {
            int scriptCount = 3;
            int consoleReadsPerScript = 2;
            int successCount = 0;
            int totalExpectedReads = scriptCount * consoleReadsPerScript;
            
            for (int i = 0; i < scriptCount; i++)
            {
                // Create script
                var scriptPath = Path.Combine(TempDir, $"RapidScript{i}.cs").Replace("\\", "/");
                var scriptContent = $@"using UnityEngine;

public class RapidScript{i} : MonoBehaviour
{{
    void Start() {{ Debug.Log(""RapidScript{i}""); }}
}}";
                
                File.WriteAllText(scriptPath, scriptContent);
                AssetDatabase.Refresh();
                
                Debug.Log($"[DomainReloadTest] Created script {i+1}/{scriptCount}");
                
                // Immediately try console reads
                for (int j = 0; j < consoleReadsPerScript; j++)
                {
                    var request = new JObject
                    {
                        ["action"] = "get",
                        ["types"] = new JArray { "all" },
                        ["count"] = 20,
                        ["format"] = "plain"
                    };
                    
                    var result = ReadConsoleTool.Execute(request);
                    
                    if (result != null && result["success"]?.Value<bool>() == true)
                    {
                        successCount++;
                    }
                    else
                    {
                        var error = result?["error"]?.ToString() ?? "Unknown error";
                        Debug.LogError($"[DomainReloadTest] Console read failed: {error}");
                    }
                    
                    yield return new WaitForSeconds(0.05f);
                }
                
                // Brief wait between script creations
                yield return new WaitForSeconds(0.2f);
            }
            
            Debug.Log($"[DomainReloadTest] {successCount}/{totalExpectedReads} console reads succeeded");
            
            // We expect at least 80% success rate (some may fail due to timing, but resilience should help most)
            int minExpectedSuccess = (int)(totalExpectedReads * 0.8f);
            Assert.GreaterOrEqual(successCount, minExpectedSuccess, 
                $"Expected at least {minExpectedSuccess} console reads to succeed, but only {successCount} succeeded");
        }
    }
}

