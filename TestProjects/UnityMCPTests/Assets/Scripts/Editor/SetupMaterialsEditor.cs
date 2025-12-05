using UnityEngine;
using UnityEditor;

public class SetupMaterialsEditor
{
    [MenuItem("Tools/Setup Scene Materials")]
    public static void SetupMaterials()
    {
        // Cube - Red, Metallic
        var cubeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CubeMaterial.mat");
        if (cubeMat != null)
        {
            cubeMat.color = Color.red;
            if (cubeMat.HasProperty("_Metallic"))
                cubeMat.SetFloat("_Metallic", 0.8f);
            if (cubeMat.HasProperty("_Glossiness"))
                cubeMat.SetFloat("_Glossiness", 0.6f);
            EditorUtility.SetDirty(cubeMat);
        }

        // Sphere - Blue, Emission
        var sphereMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SphereMaterial.mat");
        if (sphereMat != null)
        {
            sphereMat.color = new Color(0f, 0.5f, 1f, 1f);
            if (sphereMat.HasProperty("_EmissionColor"))
                sphereMat.SetColor("_EmissionColor", new Color(0f, 0.3f, 0.6f, 1f));
            if (sphereMat.HasProperty("_EmissionEnabled"))
                sphereMat.SetFloat("_EmissionEnabled", 1f);
            sphereMat.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(sphereMat);
        }

        // Cylinder - Green, Metallic
        var cylinderMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CylinderMaterial.mat");
        if (cylinderMat != null)
        {
            cylinderMat.color = Color.green;
            if (cylinderMat.HasProperty("_Metallic"))
                cylinderMat.SetFloat("_Metallic", 0.9f);
            if (cylinderMat.HasProperty("_Glossiness"))
                cylinderMat.SetFloat("_Glossiness", 0.7f);
            EditorUtility.SetDirty(cylinderMat);
        }

        // Plane - Yellow, Emission
        var planeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PlaneMaterial.mat");
        if (planeMat != null)
        {
            planeMat.color = Color.yellow;
            if (planeMat.HasProperty("_EmissionColor"))
                planeMat.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0f, 1f));
            if (planeMat.HasProperty("_EmissionEnabled"))
                planeMat.SetFloat("_EmissionEnabled", 1f);
            planeMat.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(planeMat);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Materials setup complete!");
    }
}


