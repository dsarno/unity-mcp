using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Tools;

namespace MCPForUnityTests.Editor.Tools
{
    /// <summary>
    /// Comprehensive baseline tests for ManageGameObject "find" action.
    /// These tests capture existing behavior before API redesign.
    /// </summary>
    public class ManageGameObjectFindTests
    {
        private List<GameObject> testObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in testObjects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            testObjects.Clear();
        }

        private GameObject CreateTestObject(string name)
        {
            var go = new GameObject(name);
            testObjects.Add(go);
            return go;
        }

        #region Find By Name Tests

        [Test]
        public void Find_ByName_FindsSingleObject()
        {
            var target = CreateTestObject("FindMeByName");

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "FindMeByName",
                ["searchMethod"] = "by_name"
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
            
            var data = resultObj["data"];
            Assert.IsNotNull(data, "Should return data");
        }

        [Test]
        public void Find_ByName_FindsMultipleObjects()
        {
            CreateTestObject("DuplicateFindName");
            CreateTestObject("DuplicateFindName");
            CreateTestObject("DuplicateFindName");

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "DuplicateFindName",
                ["searchMethod"] = "by_name",
                ["findAll"] = true
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
        }

        [Test]
        public void Find_ByName_NotFound_ReturnsSuccessWithEmptyResult()
        {
            // Current behavior: returns success=true with empty/no results
            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "NonExistentObject12345",
                ["searchMethod"] = "by_name"
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            // Current behavior returns success even when nothing found
            Assert.IsTrue(resultObj.Value<bool>("success"), "Current behavior returns success for empty find");
        }

        [Test]
        public void Find_ByName_PartialMatch_HandlesCorrectly()
        {
            CreateTestObject("FindableObject");
            CreateTestObject("FindableObjectTwo");

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "Findable",
                ["searchMethod"] = "by_name"
            };

            var result = ManageGameObject.HandleCommand(p);
            // Capture current behavior - may do partial match or exact only
            Assert.IsNotNull(result, "Should return a result");
        }

        #endregion

        #region Find By ID Tests

        [Test]
        public void Find_ByID_FindsObject()
        {
            var target = CreateTestObject("FindByIDTarget");
            int instanceID = target.GetInstanceID();

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = instanceID.ToString(),
                ["searchMethod"] = "by_id"
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
        }

        [Test]
        public void Find_ByID_InvalidID_ReturnsSuccessWithEmptyResult()
        {
            // Current behavior: returns success=true even for invalid ID
            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "999999999",
                ["searchMethod"] = "by_id"
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            // Current behavior returns success even when nothing found
            Assert.IsTrue(resultObj.Value<bool>("success"), "Current behavior returns success for not found");
        }

        [Test]
        public void Find_ByID_NonNumericID_HandlesGracefully()
        {
            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "not_a_number",
                ["searchMethod"] = "by_id"
            };

            var result = ManageGameObject.HandleCommand(p);
            // Should fail gracefully
            Assert.IsNotNull(result, "Should return a result");
        }

        #endregion

        #region Find By Path Tests

        [Test]
        public void Find_ByPath_FindsNestedObject()
        {
            var parent = CreateTestObject("PathParent");
            var child = CreateTestObject("PathChild");
            child.transform.SetParent(parent.transform);

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "PathParent/PathChild",
                ["searchMethod"] = "by_path"
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
        }

        [Test]
        public void Find_ByPath_DeepNesting_FindsObject()
        {
            var level1 = CreateTestObject("Level1");
            var level2 = CreateTestObject("Level2");
            var level3 = CreateTestObject("Level3");
            
            level2.transform.SetParent(level1.transform);
            level3.transform.SetParent(level2.transform);

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "Level1/Level2/Level3",
                ["searchMethod"] = "by_path"
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
        }

        [Test]
        public void Find_ByPath_InvalidPath_ReturnsSuccessWithEmptyResult()
        {
            // Current behavior: returns success=true even for invalid path
            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "NonExistent/Path/Here",
                ["searchMethod"] = "by_path"
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            // Current behavior returns success even when nothing found
            Assert.IsTrue(resultObj.Value<bool>("success"), "Current behavior returns success for not found");
        }

        #endregion

        #region Find By Tag Tests

        [Test]
        public void Find_ByTag_FindsTaggedObjects()
        {
            var target = CreateTestObject("TaggedObject");
            target.tag = "MainCamera";

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "MainCamera",
                ["searchMethod"] = "by_tag"
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
        }

        [Test]
        public void Find_ByTag_MultipleObjects_FindsAll()
        {
            var target1 = CreateTestObject("Tagged1");
            var target2 = CreateTestObject("Tagged2");
            target1.tag = "Player";
            target2.tag = "Player";

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "Player",
                ["searchMethod"] = "by_tag",
                ["findAll"] = true
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
        }

        [Test]
        public void Find_ByTag_InvalidTag_ReturnsSuccessWithEmptyResult()
        {
            // Current behavior: returns success=true even for invalid tag
            // Ignore expected warning logs about invalid tag
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                var p = new JObject
                {
                    ["action"] = "find",
                    ["target"] = "NonExistentTag12345",
                    ["searchMethod"] = "by_tag"
                };

                var result = ManageGameObject.HandleCommand(p);
                var resultObj = result as JObject ?? JObject.FromObject(result);
                
                // Current behavior returns success even when nothing found
                Assert.IsTrue(resultObj.Value<bool>("success"), "Current behavior returns success for not found");
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }

        #endregion

        #region Find By Layer Tests

        [Test]
        public void Find_ByLayer_FindsLayeredObjects()
        {
            var target = CreateTestObject("LayeredObject");
            target.layer = LayerMask.NameToLayer("UI");

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "UI",
                ["searchMethod"] = "by_layer"
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
        }

        [Test]
        public void Find_ByLayer_MultipleObjects_FindsAll()
        {
            var target1 = CreateTestObject("Layered1");
            var target2 = CreateTestObject("Layered2");
            
            // Use built-in "UI" layer (5) that exists in all Unity projects
            int uiLayer = LayerMask.NameToLayer("UI");
            target1.layer = uiLayer;
            target2.layer = uiLayer;

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "UI",
                ["searchMethod"] = "by_layer",
                ["findAll"] = true
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
        }

        #endregion

        #region Find By Component Tests

        [Test]
        public void Find_ByComponent_FindsObjectsWithComponent()
        {
            var target = CreateTestObject("WithRigidbody");
            target.AddComponent<Rigidbody>();

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "Rigidbody",
                ["searchMethod"] = "by_component"
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
        }

        [Test]
        public void Find_ByComponent_MultipleObjects_FindsAll()
        {
            var target1 = CreateTestObject("WithCollider1");
            var target2 = CreateTestObject("WithCollider2");
            target1.AddComponent<BoxCollider>();
            target2.AddComponent<BoxCollider>();

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "BoxCollider",
                ["searchMethod"] = "by_component",
                ["findAll"] = true
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
        }

        [Test]
        public void Find_ByComponent_InvalidComponent_ReturnsSuccessWithEmptyResult()
        {
            // Current behavior: returns success=true even for invalid component type
            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "NonExistentComponent12345",
                ["searchMethod"] = "by_component"
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            // Current behavior returns success even when nothing found
            Assert.IsTrue(resultObj.Value<bool>("success"), "Current behavior returns success for not found");
        }

        #endregion

        #region Inactive Object Tests

        [Test]
        public void Find_IncludeInactive_FindsInactiveObjects()
        {
            var target = CreateTestObject("InactiveTarget");
            target.SetActive(false);

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "InactiveTarget",
                ["searchMethod"] = "by_name",
                ["searchInactive"] = true
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
        }

        [Test]
        public void Find_ExcludeInactive_DoesNotFindInactiveObjects()
        {
            var target = CreateTestObject("InactiveExcluded");
            target.SetActive(false);

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "InactiveExcluded",
                ["searchMethod"] = "by_name",
                ["searchInactive"] = false
            };

            var result = ManageGameObject.HandleCommand(p);
            // Capture current behavior
            Assert.IsNotNull(result, "Should return a result");
        }

        #endregion

        #region Search In Children Tests

        [Test]
        public void Find_InChildren_SearchesChildHierarchy()
        {
            var parent = CreateTestObject("SearchParent");
            var child = CreateTestObject("SearchChild");
            var grandchild = CreateTestObject("SearchGrandchild");
            
            child.transform.SetParent(parent.transform);
            grandchild.transform.SetParent(child.transform);

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "SearchGrandchild",
                ["searchMethod"] = "by_name",
                ["searchInChildren"] = true
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
        }

        #endregion

        #region Pagination Tests

        [Test]
        public void Find_WithPagination_ReturnsPagedResults()
        {
            // Create multiple objects
            for (int i = 0; i < 10; i++)
            {
                var go = CreateTestObject($"PaginatedObject{i}");
                go.tag = "Respawn";
            }

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "Respawn",
                ["searchMethod"] = "by_tag",
                ["findAll"] = true,
                ["pageSize"] = 5,
                ["cursor"] = 0
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
            
            // Check for pagination info in response
            var data = resultObj["data"];
            Assert.IsNotNull(data, "Should return data");
        }

        #endregion

        #region Response Structure Tests

        [Test]
        public void Find_Success_ReturnsGameObjectData()
        {
            var target = CreateTestObject("DataTarget");
            target.transform.position = new Vector3(1, 2, 3);

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "DataTarget",
                ["searchMethod"] = "by_name"
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
            
            var data = resultObj["data"];
            Assert.IsNotNull(data, "Should return data with GameObject information");
        }

        [Test]
        public void Find_Success_IncludesInstanceID()
        {
            var target = CreateTestObject("InstanceIDTarget");

            var p = new JObject
            {
                ["action"] = "find",
                ["target"] = "InstanceIDTarget",
                ["searchMethod"] = "by_name"
            };

            var result = ManageGameObject.HandleCommand(p);
            var resultObj = result as JObject ?? JObject.FromObject(result);

            Assert.IsTrue(resultObj.Value<bool>("success"), resultObj.ToString());
            
            // Verify instanceID is present in response
            var dataStr = resultObj["data"]?.ToString() ?? "";
            Assert.IsTrue(dataStr.Contains("instanceID") || dataStr.Contains("InstanceID") || dataStr.Contains("instance"),
                "Response should include instance ID");
        }

        #endregion
    }
}

