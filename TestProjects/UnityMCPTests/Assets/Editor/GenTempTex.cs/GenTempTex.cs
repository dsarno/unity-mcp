using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GenTempTex
{
    static GenTempTex()
    {
        try
        {
            var folder = "Assets/Temp/LiveTests";
            if (!AssetDatabase.IsValidFolder("Assets/Temp")) AssetDatabase.CreateFolder("Assets", "Temp");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Temp", "LiveTests");
            CreateSolidTextureAsset($"{folder}/TempBaseTex.asset", Color.white);
        }
        catch {}
    }

    private static void CreateSolidTextureAsset(string path, Color color)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) return;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        AssetDatabase.CreateAsset(tex, path);
        AssetDatabase.SaveAssets();
    }
}