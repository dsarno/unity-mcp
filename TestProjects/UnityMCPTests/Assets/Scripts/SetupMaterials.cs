using UnityEngine;

public class SetupMaterials : MonoBehaviour
{
    [ContextMenu("Setup Scene")]
    public void SetupScene()
    {
        // Create materials
        Material cubeMat = CreateBlueMetallicMaterial();
        Material sphereMat = CreateRedGlowingMaterial();
        Material cylinderMat = CreateGreenMetallicMaterial();
        Material planeMat = CreateYellowGlowingMaterial();

        // Create primitives
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Cube";
        cube.transform.position = new Vector3(-3f, 0.5f, 0f);
        cube.GetComponent<Renderer>().material = cubeMat;

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Sphere";
        sphere.transform.position = new Vector3(-1f, 0.5f, 0f);
        sphere.GetComponent<Renderer>().material = sphereMat;

        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = "Cylinder";
        cylinder.transform.position = new Vector3(1f, 1f, 0f);
        cylinder.GetComponent<Renderer>().material = cylinderMat;

        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Plane";
        plane.transform.position = new Vector3(3f, 0f, 0f);
        plane.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
        plane.GetComponent<Renderer>().material = planeMat;

        // Create directional light
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        lightObj.transform.position = new Vector3(0f, 5f, 0f);
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        light.intensity = 1f;

        Debug.Log("Scene setup complete!");
    }

    private Material CreateBlueMetallicMaterial()
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.name = "CubeMaterial";
        mat.color = Color.blue;
        mat.SetFloat("_Metallic", 0.8f);
        mat.SetFloat("_Glossiness", 0.6f);
        return mat;
    }

    private Material CreateRedGlowingMaterial()
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.name = "SphereMaterial";
        mat.color = Color.red;
        mat.SetColor("_EmissionColor", new Color(0.8f, 0f, 0f, 1f));
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return mat;
    }

    private Material CreateGreenMetallicMaterial()
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.name = "CylinderMaterial";
        mat.color = Color.green;
        mat.SetFloat("_Metallic", 0.9f);
        mat.SetFloat("_Glossiness", 0.7f);
        return mat;
    }

    private Material CreateYellowGlowingMaterial()
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.name = "PlaneMaterial";
        mat.color = Color.yellow;
        mat.SetColor("_EmissionColor", new Color(0.8f, 0.8f, 0f, 1f));
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return mat;
    }
}
