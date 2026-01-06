using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class BounceCubeSetupEditor
{
    static BounceCubeSetupEditor()
    {
        EditorApplication.delayCall += RunSetup;
    }

    [MenuItem("Tools/Setup Bounce Cubes")]
    public static void RunSetupMenu()
    {
        RunSetup();
    }

    static void RunSetup()
    {
        EditorApplication.delayCall -= RunSetup;
        
        // Create a temporary GameObject to run the setup
        GameObject tempGO = new GameObject("TempBounceSetup");
        BounceCubeSetup setup = tempGO.AddComponent<BounceCubeSetup>();
        setup.SetupBounceCubes();
        Object.DestroyImmediate(tempGO);
    }
}
