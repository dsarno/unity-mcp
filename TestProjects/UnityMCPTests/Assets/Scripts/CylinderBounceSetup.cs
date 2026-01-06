using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class CylinderBounceSetup : MonoBehaviour
{
    void Start()
    {
        SetupBounceCylinders();
    }

    [ContextMenu("Setup Bounce Cylinders")]
    public void SetupBounceCylinders()
    {
        // Find all bounce cylinders
        GameObject[] cylinders = {
            GameObject.Find("BounceCylinder_1"),
            GameObject.Find("BounceCylinder_2"),
            GameObject.Find("BounceCylinder_3"),
            GameObject.Find("BounceCylinder_4"),
            GameObject.Find("BounceCylinder_5"),
            GameObject.Find("BounceCylinder_6"),
            GameObject.Find("BounceCylinder_7"),
            GameObject.Find("BounceCylinder_8"),
            GameObject.Find("BounceCylinder_9"),
            GameObject.Find("BounceCylinder_10")
        };

        // Different bounce heights
        float[] heights = { 1.5f, 2.0f, 1.2f, 1.8f, 2.5f, 1.0f, 2.2f, 1.6f, 1.4f, 2.8f };
        
        // Different bounce speeds
        float[] speeds = { 1.0f, 1.5f, 2.0f, 1.2f, 0.8f, 2.5f, 1.2f, 1.8f, 2.2f, 0.9f };
        
        // Different direction vectors (normalized)
        Vector3[] directions = {
            new Vector3(0, 1, 0),           // Straight up
            new Vector3(0, 1, 0.3f),        // Up and forward
            new Vector3(0.2f, 1, 0),       // Up and right
            new Vector3(0, 1, -0.2f),      // Up and back
            new Vector3(-0.3f, 1, 0.2f),   // Up, left, and forward
            new Vector3(0.1f, 1, 0.1f),    // Up, slight diagonal
            new Vector3(0, 1, 0.4f),       // Up and more forward
            new Vector3(-0.2f, 1, -0.1f),  // Up, left, and back
            new Vector3(0.15f, 1, -0.15f), // Up, right, and back
            new Vector3(0, 1, 0)           // Straight up
        };

        // Material paths
        string[] materialPaths = {
            "Assets/Materials/CylinderBounce_1_Red.mat",
            "Assets/Materials/CylinderBounce_2_Blue.mat",
            "Assets/Materials/CylinderBounce_3_Green.mat",
            "Assets/Materials/CylinderBounce_4_Yellow.mat",
            "Assets/Materials/CylinderBounce_5_Purple.mat",
            "Assets/Materials/CylinderBounce_6_Orange.mat",
            "Assets/Materials/CylinderBounce_7_Cyan.mat",
            "Assets/Materials/CylinderBounce_8_Pink.mat",
            "Assets/Materials/CylinderBounce_9_Emerald.mat",
            "Assets/Materials/CylinderBounce_10_Gold.mat"
        };

        // Material colors
        Color[] materialColors = {
            new Color(1f, 0.2f, 0.2f, 1f),      // Red
            new Color(0.2f, 0.4f, 1f, 1f),      // Blue
            new Color(0.2f, 1f, 0.3f, 1f),      // Green
            new Color(1f, 0.9f, 0.1f, 1f),      // Yellow
            new Color(0.7f, 0.2f, 1f, 1f),      // Purple
            new Color(1f, 0.5f, 0.1f, 1f),      // Orange
            new Color(0.1f, 0.9f, 1f, 1f),      // Cyan
            new Color(1f, 0.4f, 0.8f, 1f),      // Pink
            new Color(0.1f, 0.8f, 0.5f, 1f),    // Emerald
            new Color(1f, 0.84f, 0f, 1f)        // Gold
        };

        for (int i = 0; i < cylinders.Length; i++)
        {
            if (cylinders[i] == null) continue;

            // Assign material and set color
#if UNITY_EDITOR
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPaths[i]);
            if (mat != null)
            {
                // Set material color
                mat.color = materialColors[i];
                
                var renderer = cylinders[i].GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = mat;
                }
            }
#endif

            // Get or add CylinderBounce component and set values
            CylinderBounce bounce = cylinders[i].GetComponent<CylinderBounce>();
            if (bounce == null)
            {
                bounce = cylinders[i].AddComponent<CylinderBounce>();
            }
            
#if UNITY_EDITOR
            // Use SerializedObject to properly set serialized fields
            SerializedObject serializedBounce = new SerializedObject(bounce);
            SerializedProperty heightProp = serializedBounce.FindProperty("height");
            SerializedProperty speedProp = serializedBounce.FindProperty("speed");
            SerializedProperty directionProp = serializedBounce.FindProperty("direction");
            
            if (heightProp != null) heightProp.floatValue = heights[i];
            if (speedProp != null) speedProp.floatValue = speeds[i];
            if (directionProp != null) directionProp.vector3Value = directions[i];
            serializedBounce.ApplyModifiedProperties();
#else
            // Runtime fallback
            bounce.height = heights[i];
            bounce.speed = speeds[i];
            bounce.direction = directions[i];
#endif
        }

        Debug.Log("Bounce cylinders setup complete!");
    }
}
