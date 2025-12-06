using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Tools;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnityTests.Editor.Tools
{
    public class ReadConsoleTests
    {
        [Test]
        public void HandleCommand_Clear_Works()
        {
            // Arrange
            var paramsObj = new JObject
            {
                ["action"] = "clear"
            };

            // Act
            var result = ToJObject(ReadConsole.HandleCommand(paramsObj));

            // Assert
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
        }

        [Test]
        public void HandleCommand_Get_Works()
        {
            // Arrange
            Debug.Log("Test Log Message"); // Ensure there is at least one log
            var paramsObj = new JObject
            {
                ["action"] = "get",
                ["count"] = 5
            };

            // Act
            var result = ToJObject(ReadConsole.HandleCommand(paramsObj));

            // Assert
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
            Assert.IsInstanceOf<JArray>(result["data"]);
        }

        private static JObject ToJObject(object result)
        {
            return result as JObject ?? JObject.FromObject(result);
        }
    }
}

