using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MCPForUnity.Editor.Tools.Prefabs;
using static MCPForUnityTests.Editor.TestUtilities;

namespace MCPForUnityTests.Editor.Tools
{
    public class ManagePrefabsTests
    {
        private const string TempDirectory = "Assets/Temp/ManagePrefabsTests";

        [SetUp]
        public void SetUp()
        {
            StageUtility.GoToMainStage();
            EnsureTempDirectoryExists();
        }

        [TearDown]
        public void TearDown()
        {
            StageUtility.GoToMainStage();
            
            // Clean up temp directory after each test
            if (AssetDatabase.IsValidFolder(TempDirectory))
            {
                AssetDatabase.DeleteAsset(TempDirectory);
            }
            
            // Clean up empty parent folders to avoid debris
            CleanupEmptyParentFolders(TempDirectory);
        }

        [Test]
        public void OpenStage_OpensPrefabInIsolation()
        {
            string prefabPath = CreateTestPrefab("OpenStageCube");

            try
            {
                var openParams = new JObject
                {
                    ["action"] = "open_stage",
                    ["prefabPath"] = prefabPath
                };

                var openResult = ToJObject(ManagePrefabs.HandleCommand(openParams));

                Assert.IsTrue(openResult.Value<bool>("success"), "open_stage should succeed for a valid prefab.");

                UnityEditor.SceneManagement.PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                Assert.IsNotNull(stage, "Prefab stage should be open after open_stage.");
                Assert.AreEqual(prefabPath, stage.assetPath, "Opened stage should match prefab path.");

                var stageInfo = ToJObject(MCPForUnity.Editor.Resources.Editor.PrefabStage.HandleCommand(new JObject()));
                Assert.IsTrue(stageInfo.Value<bool>("success"), "get_prefab_stage should succeed when stage is open.");

                var data = stageInfo["data"] as JObject;
                Assert.IsNotNull(data, "Stage info should include data payload.");
                Assert.IsTrue(data.Value<bool>("isOpen"));
                Assert.AreEqual(prefabPath, data.Value<string>("assetPath"));
            }
            finally
            {
                StageUtility.GoToMainStage();
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }

        [Test]
        public void CloseStage_ReturnsSuccess_WhenNoStageOpen()
        {
            StageUtility.GoToMainStage();
            var closeResult = ToJObject(ManagePrefabs.HandleCommand(new JObject
            {
                ["action"] = "close_stage"
            }));

            Assert.IsTrue(closeResult.Value<bool>("success"), "close_stage should succeed even if no stage is open.");
        }

        [Test]
        public void CloseStage_ClosesOpenPrefabStage()
        {
            string prefabPath = CreateTestPrefab("CloseStageCube");

            try
            {
                ManagePrefabs.HandleCommand(new JObject
                {
                    ["action"] = "open_stage",
                    ["prefabPath"] = prefabPath
                });

                var closeResult = ToJObject(ManagePrefabs.HandleCommand(new JObject
                {
                    ["action"] = "close_stage"
                }));

                Assert.IsTrue(closeResult.Value<bool>("success"), "close_stage should succeed when stage is open.");
                Assert.IsNull(PrefabStageUtility.GetCurrentPrefabStage(), "Prefab stage should be closed after close_stage.");
            }
            finally
            {
                StageUtility.GoToMainStage();
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }

        [Test]
        public void SaveOpenStage_SavesDirtyChanges()
        {
            string prefabPath = CreateTestPrefab("SaveStageCube");

            try
            {
                ManagePrefabs.HandleCommand(new JObject
                {
                    ["action"] = "open_stage",
                    ["prefabPath"] = prefabPath
                });

                UnityEditor.SceneManagement.PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                Assert.IsNotNull(stage, "Stage should be open before modifying.");

                stage.prefabContentsRoot.transform.localScale = new Vector3(2f, 2f, 2f);

                var saveResult = ToJObject(ManagePrefabs.HandleCommand(new JObject
                {
                    ["action"] = "save_open_stage"
                }));

                Assert.IsTrue(saveResult.Value<bool>("success"), "save_open_stage should succeed when stage is open.");
                Assert.IsFalse(stage.scene.isDirty, "Stage scene should not be dirty after saving.");

                GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.AreEqual(new Vector3(2f, 2f, 2f), reloaded.transform.localScale, "Saved prefab asset should include changes from open stage.");
            }
            finally
            {
                StageUtility.GoToMainStage();
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }

        [Test]
        public void SaveOpenStage_ReturnsError_WhenNoStageOpen()
        {
            StageUtility.GoToMainStage();

            var saveResult = ToJObject(ManagePrefabs.HandleCommand(new JObject
            {
                ["action"] = "save_open_stage"
            }));

            Assert.IsFalse(saveResult.Value<bool>("success"), "save_open_stage should fail when no stage is open.");
        }

        [Test]
        public void CreateFromGameObject_CreatesPrefabAndLinksInstance()
        {
            EnsureTempDirectoryExists();
            StageUtility.GoToMainStage();

            string prefabPath = Path.Combine(TempDirectory, "SceneObjectSaved.prefab").Replace('\\', '/');
            GameObject sceneObject = new GameObject("ScenePrefabSource");

            try
            {
                var result = ToJObject(ManagePrefabs.HandleCommand(new JObject
                {
                    ["action"] = "create_from_gameobject",
                    ["target"] = sceneObject.name,
                    ["prefabPath"] = prefabPath
                }));

                Assert.IsTrue(result.Value<bool>("success"), "create_from_gameobject should succeed for a valid scene object.");

                var data = result["data"] as JObject;
                Assert.IsNotNull(data, "Response data should include prefab information.");

                string savedPath = data.Value<string>("prefabPath");
                Assert.AreEqual(prefabPath, savedPath, "Returned prefab path should match the requested path.");

                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(savedPath);
                Assert.IsNotNull(prefabAsset, "Prefab asset should exist at the saved path.");

                int instanceId = data.Value<int>("instanceId");
                var linkedInstance = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                Assert.IsNotNull(linkedInstance, "Linked instance should resolve from instanceId.");
                Assert.AreEqual(savedPath, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(linkedInstance), "Instance should be connected to the new prefab.");

                sceneObject = linkedInstance;
            }
            finally
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabPath) != null)
                {
                    AssetDatabase.DeleteAsset(prefabPath);
                }

                if (sceneObject != null)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(sceneObject))
                    {
                        PrefabUtility.UnpackPrefabInstance(
                            sceneObject,
                            PrefabUnpackMode.Completely,
                            InteractionMode.AutomatedAction
                        );
                    }
                    UnityEngine.Object.DestroyImmediate(sceneObject, true);
                }
            }
        }

        [Test]
        public void CreateFromGameObject_FindsInactiveObject_WhenSearchInactiveTrue()
        {
            EnsureTempDirectoryExists();
            StageUtility.GoToMainStage();

            string prefabPath = Path.Combine(TempDirectory, "InactiveObjectSaved.prefab").Replace('\\', '/');
            GameObject sceneObject = new GameObject("InactivePrefabSource");
            sceneObject.SetActive(false);

            try
            {
                // First, verify it fails without searchInactive
                var failResult = ToJObject(ManagePrefabs.HandleCommand(new JObject
                {
                    ["action"] = "create_from_gameobject",
                    ["target"] = sceneObject.name,
                    ["prefabPath"] = prefabPath
                }));

                Assert.IsFalse(failResult.Value<bool>("success"), "create_from_gameobject should fail for inactive object without searchInactive.");
                Assert.IsTrue(failResult.Value<string>("error").Contains("searchInactive"), "Error message should hint about searchInactive option.");

                // Now try with searchInactive: true
                var result = ToJObject(ManagePrefabs.HandleCommand(new JObject
                {
                    ["action"] = "create_from_gameobject",
                    ["target"] = sceneObject.name,
                    ["prefabPath"] = prefabPath,
                    ["searchInactive"] = true
                }));

                Assert.IsTrue(result.Value<bool>("success"), "create_from_gameobject should succeed for inactive object with searchInactive: true.");

                var data = result["data"] as JObject;
                Assert.IsNotNull(data, "Response data should include prefab information.");

                string savedPath = data.Value<string>("prefabPath");
                Assert.AreEqual(prefabPath, savedPath, "Returned prefab path should match the requested path.");

                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(savedPath);
                Assert.IsNotNull(prefabAsset, "Prefab asset should exist at the saved path.");

                int instanceId = data.Value<int>("instanceId");
                var linkedInstance = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                Assert.IsNotNull(linkedInstance, "Linked instance should resolve from instanceId.");

                sceneObject = linkedInstance;
            }
            finally
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabPath) != null)
                {
                    AssetDatabase.DeleteAsset(prefabPath);
                }

                if (sceneObject != null)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(sceneObject))
                    {
                        PrefabUtility.UnpackPrefabInstance(
                            sceneObject,
                            PrefabUnpackMode.Completely,
                            InteractionMode.AutomatedAction
                        );
                    }
                    UnityEngine.Object.DestroyImmediate(sceneObject, true);
                }
            }
        }

        [Test]
        public void CreateFromGameObject_FindsObjectByInstanceId_WithTargetInstanceId()
        {
            EnsureTempDirectoryExists();
            StageUtility.GoToMainStage();

            string prefabPath = Path.Combine(TempDirectory, "InstanceIdLookup.prefab").Replace('\\', '/');
            GameObject sceneObject = new GameObject("InstanceIdPrefabSource");
            int instanceId = sceneObject.GetInstanceID();

            try
            {
                var result = ToJObject(ManagePrefabs.HandleCommand(new JObject
                {
                    ["action"] = "create_from_gameobject",
                    ["target_instance_id"] = instanceId,
                    ["prefabPath"] = prefabPath
                }));

                Assert.IsTrue(result.Value<bool>("success"), "create_from_gameobject should succeed with target_instance_id.");

                var data = result["data"] as JObject;
                Assert.IsNotNull(data, "Response data should include prefab information.");

                string savedPath = data.Value<string>("prefabPath");
                Assert.AreEqual(prefabPath, savedPath, "Returned prefab path should match the requested path.");

                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(savedPath);
                Assert.IsNotNull(prefabAsset, "Prefab asset should exist at the saved path.");

                int returnedInstanceId = data.Value<int>("instanceId");
                var linkedInstance = EditorUtility.InstanceIDToObject(returnedInstanceId) as GameObject;
                Assert.IsNotNull(linkedInstance, "Linked instance should resolve from instanceId.");

                sceneObject = linkedInstance;
            }
            finally
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabPath) != null)
                {
                    AssetDatabase.DeleteAsset(prefabPath);
                }

                if (sceneObject != null)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(sceneObject))
                    {
                        PrefabUtility.UnpackPrefabInstance(
                            sceneObject,
                            PrefabUnpackMode.Completely,
                            InteractionMode.AutomatedAction
                        );
                    }
                    UnityEngine.Object.DestroyImmediate(sceneObject, true);
                }
            }
        }

        [Test]
        public void CreateFromGameObject_FindsInactiveObjectByInstanceId()
        {
            EnsureTempDirectoryExists();
            StageUtility.GoToMainStage();

            string prefabPath = Path.Combine(TempDirectory, "InactiveInstanceId.prefab").Replace('\\', '/');
            GameObject sceneObject = new GameObject("InactiveInstanceIdSource");
            sceneObject.SetActive(false);
            int instanceId = sceneObject.GetInstanceID();

            try
            {
                // Instance ID lookup should find inactive objects without searchInactive flag
                // because by_id search always uses searchInactive: true internally
                var result = ToJObject(ManagePrefabs.HandleCommand(new JObject
                {
                    ["action"] = "create_from_gameobject",
                    ["target_instance_id"] = instanceId,
                    ["prefabPath"] = prefabPath
                }));

                Assert.IsTrue(result.Value<bool>("success"), "create_from_gameobject should succeed for inactive object with target_instance_id.");

                var data = result["data"] as JObject;
                Assert.IsNotNull(data, "Response data should include prefab information.");

                int returnedInstanceId = data.Value<int>("instanceId");
                var linkedInstance = EditorUtility.InstanceIDToObject(returnedInstanceId) as GameObject;
                Assert.IsNotNull(linkedInstance, "Linked instance should resolve from instanceId.");

                sceneObject = linkedInstance;
            }
            finally
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabPath) != null)
                {
                    AssetDatabase.DeleteAsset(prefabPath);
                }

                if (sceneObject != null)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(sceneObject))
                    {
                        PrefabUtility.UnpackPrefabInstance(
                            sceneObject,
                            PrefabUnpackMode.Completely,
                            InteractionMode.AutomatedAction
                        );
                    }
                    UnityEngine.Object.DestroyImmediate(sceneObject, true);
                }
            }
        }

        [Test]
        public void CreateFromGameObject_TargetAcceptsIntegerInstanceId()
        {
            EnsureTempDirectoryExists();
            StageUtility.GoToMainStage();

            string prefabPath = Path.Combine(TempDirectory, "IntegerTargetLookup.prefab").Replace('\\', '/');
            GameObject sceneObject = new GameObject("IntegerTargetSource");
            int instanceId = sceneObject.GetInstanceID();

            try
            {
                // Pass instance ID as integer in target parameter (not target_instance_id)
                var result = ToJObject(ManagePrefabs.HandleCommand(new JObject
                {
                    ["action"] = "create_from_gameobject",
                    ["target"] = instanceId, // Integer, not string
                    ["prefabPath"] = prefabPath
                }));

                Assert.IsTrue(result.Value<bool>("success"), "create_from_gameobject should succeed with integer target (instance ID).");

                var data = result["data"] as JObject;
                Assert.IsNotNull(data, "Response data should include prefab information.");

                string savedPath = data.Value<string>("prefabPath");
                Assert.AreEqual(prefabPath, savedPath, "Returned prefab path should match the requested path.");

                int returnedInstanceId = data.Value<int>("instanceId");
                var linkedInstance = EditorUtility.InstanceIDToObject(returnedInstanceId) as GameObject;
                Assert.IsNotNull(linkedInstance, "Linked instance should resolve from instanceId.");

                sceneObject = linkedInstance;
            }
            finally
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabPath) != null)
                {
                    AssetDatabase.DeleteAsset(prefabPath);
                }

                if (sceneObject != null)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(sceneObject))
                    {
                        PrefabUtility.UnpackPrefabInstance(
                            sceneObject,
                            PrefabUnpackMode.Completely,
                            InteractionMode.AutomatedAction
                        );
                    }
                    UnityEngine.Object.DestroyImmediate(sceneObject, true);
                }
            }
        }

        [Test]
        public void CreateFromGameObject_TargetInstanceIdTakesPrecedence()
        {
            EnsureTempDirectoryExists();
            StageUtility.GoToMainStage();

            string prefabPath = Path.Combine(TempDirectory, "PrecedenceTest.prefab").Replace('\\', '/');
            GameObject correctObject = new GameObject("CorrectObject");
            GameObject wrongObject = new GameObject("WrongObject");
            int correctInstanceId = correctObject.GetInstanceID();

            try
            {
                // Pass both target (pointing to wrong object) and target_instance_id (pointing to correct object)
                // target_instance_id should take precedence
                var result = ToJObject(ManagePrefabs.HandleCommand(new JObject
                {
                    ["action"] = "create_from_gameobject",
                    ["target"] = wrongObject.name,
                    ["target_instance_id"] = correctInstanceId,
                    ["prefabPath"] = prefabPath
                }));

                Assert.IsTrue(result.Value<bool>("success"), "create_from_gameobject should succeed.");

                var data = result["data"] as JObject;
                Assert.IsNotNull(data, "Response data should include prefab information.");

                int returnedInstanceId = data.Value<int>("instanceId");
                var linkedInstance = EditorUtility.InstanceIDToObject(returnedInstanceId) as GameObject;
                Assert.IsNotNull(linkedInstance, "Linked instance should resolve from instanceId.");

                // The prefab should have been created from the correctObject (via target_instance_id)
                // not from wrongObject (via target)
                Assert.AreEqual("CorrectObject", linkedInstance.name, "Prefab should be created from object specified by target_instance_id.");

                correctObject = linkedInstance;
            }
            finally
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabPath) != null)
                {
                    AssetDatabase.DeleteAsset(prefabPath);
                }

                if (correctObject != null)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(correctObject))
                    {
                        PrefabUtility.UnpackPrefabInstance(
                            correctObject,
                            PrefabUnpackMode.Completely,
                            InteractionMode.AutomatedAction
                        );
                    }
                    UnityEngine.Object.DestroyImmediate(correctObject, true);
                }

                if (wrongObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(wrongObject, true);
                }
            }
        }

        private static string CreateTestPrefab(string name)
        {
            EnsureTempDirectoryExists();

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temp.name = name;

            string path = Path.Combine(TempDirectory, name + ".prefab").Replace('\\', '/');
            PrefabUtility.SaveAsPrefabAsset(temp, path, out bool success);
            UnityEngine.Object.DestroyImmediate(temp);

            Assert.IsTrue(success, "PrefabUtility.SaveAsPrefabAsset should succeed for test prefab.");
            return path;
        }

        private static void EnsureTempDirectoryExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Temp"))
            {
                AssetDatabase.CreateFolder("Assets", "Temp");
            }

            if (!AssetDatabase.IsValidFolder(TempDirectory))
            {
                AssetDatabase.CreateFolder("Assets/Temp", "ManagePrefabsTests");
            }
        }
    }
}
