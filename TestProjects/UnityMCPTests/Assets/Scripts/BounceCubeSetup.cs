using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class BounceCubeSetup : MonoBehaviour
{
    void Start()
    {
        SetupBounceCubes();
    }

    [ContextMenu("Setup Bounce Cubes")]
    public void SetupBounceCubes()
    {
        // Find all bounce cubes
        GameObject[] cubes = {
            GameObject.Find("BounceCube_1"),
            GameObject.Find("BounceCube_2"),
            GameObject.Find("BounceCube_3"),
            GameObject.Find("BounceCube_4"),
            GameObject.Find("BounceCube_5"),
            GameObject.Find("BounceCube_6"),
            GameObject.Find("BounceCube_7"),
            GameObject.Find("BounceCube_8"),
            GameObject.Find("BounceCube_9"),
            GameObject.Find("BounceCube_10")
        };

        // Positions (side by side)
        Vector3[] positions = {
            new Vector3(-4.5f, 0, 0),
            new Vector3(-3.5f, 0, 0),
            new Vector3(-2.5f, 0, 0),
            new Vector3(-1.5f, 0, 0),
            new Vector3(-0.5f, 0, 0),
            new Vector3(0.5f, 0, 0),
            new Vector3(1.5f, 0, 0),
            new Vector3(2.5f, 0, 0),
            new Vector3(3.5f, 0, 0),
            new Vector3(4.5f, 0, 0)
        };

        // Different scales
        Vector3[] scales = {
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.7f, 0.7f, 0.7f),
            new Vector3(0.9f, 0.9f, 0.9f),
            new Vector3(1.1f, 1.1f, 1.1f),
            new Vector3(1.3f, 1.3f, 1.3f),
            new Vector3(1.2f, 1.2f, 1.2f),
            new Vector3(1f, 1f, 1f),
            new Vector3(0.8f, 0.8f, 0.8f),
            new Vector3(0.6f, 0.6f, 0.6f),
            new Vector3(1.4f, 1.4f, 1.4f)
        };

        // Material paths
        string[] materialPaths = {
            "Assets/Materials/BounceCube_Red.mat",
            "Assets/Materials/BounceCube_Blue.mat",
            "Assets/Materials/BounceCube_Green.mat",
            "Assets/Materials/BounceCube_Yellow.mat",
            "Assets/Materials/BounceCube_Purple.mat",
            "Assets/Materials/BounceCube_Orange.mat",
            "Assets/Materials/BounceCube_Cyan.mat",
            "Assets/Materials/BounceCube_Pink.mat",
            "Assets/Materials/BounceCube_Teal.mat",
            "Assets/Materials/BounceCube_Magenta.mat"
        };

        // Material colors (RGBA)
        Color[] materialColors = {
            new Color(1f, 0.2f, 0.2f, 1f),      // Red
            new Color(0.2f, 0.4f, 1f, 1f),       // Blue
            new Color(0.2f, 1f, 0.3f, 1f),       // Green
            new Color(1f, 0.9f, 0.2f, 1f),      // Yellow
            new Color(0.8f, 0.2f, 1f, 1f),      // Purple
            new Color(1f, 0.5f, 0.1f, 1f),      // Orange
            new Color(0.1f, 0.9f, 1f, 1f),      // Cyan
            new Color(1f, 0.4f, 0.8f, 1f),      // Pink
            new Color(0.2f, 0.8f, 0.7f, 1f),    // Teal
            new Color(1f, 0.1f, 0.5f, 1f)       // Magenta
        };

        // Bounce component values (height, speed)
        float[] heights = { 0.5f, 0.8f, 1.2f, 1.5f, 2f, 1.8f, 1f, 0.7f, 0.6f, 2.2f };
        float[] speeds = { 1f, 1.5f, 2f, 2.5f, 3f, 2.2f, 1.8f, 1.2f, 0.8f, 3.5f };

        for (int i = 0; i < cubes.Length; i++)
        {
            if (cubes[i] == null) continue;

            // Set position and scale
            cubes[i].transform.localPosition = positions[i];
            cubes[i].transform.localScale = scales[i];

            // Assign material and set color
#if UNITY_EDITOR
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPaths[i]);
            if (mat != null)
            {
                // Set material color
                mat.color = materialColors[i];
                
                var renderer = cubes[i].GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = mat;
                }
            }
#endif

            // Add or get Bounce component and set values
            Bounce bounce = cubes[i].GetComponent<Bounce>();
            if (bounce == null)
            {
                bounce = cubes[i].AddComponent<Bounce>();
            }
            
#if UNITY_EDITOR
            // Use SerializedObject to properly set serialized fields
            SerializedObject serializedBounce = new SerializedObject(bounce);
            SerializedProperty heightProp = serializedBounce.FindProperty("height");
            SerializedProperty speedProp = serializedBounce.FindProperty("speed");
            
            if (heightProp != null) heightProp.floatValue = heights[i];
            if (speedProp != null) speedProp.floatValue = speeds[i];
            serializedBounce.ApplyModifiedProperties();
#else
            // Runtime fallback - make fields public temporarily or use reflection
            var heightField = typeof(Bounce).GetField("height", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var speedField = typeof(Bounce).GetField("speed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (heightField != null) heightField.SetValue(bounce, heights[i]);
            if (speedField != null) speedField.SetValue(bounce, speeds[i]);
#endif
        }

        Debug.Log("Bounce cubes setup complete!");
    }
}
