using UnityEngine;
using UnityEditor;

public static class CloudMaterialCreator
{
    [MenuItem("Tools/Create Space Cloud Materials")]
    public static void CreateMaterials()
    {
        Shader cloudShader = Shader.Find("Custom/SpaceNebulaSimple");
        if (cloudShader == null)
        {
            Debug.LogError("Shader Custom/SpaceNebulaSimple not found!");
            return;
        }
        
        CreateCloudMaterial("SpaceCloud_Blue", cloudShader, 
            new Color(0.2f, 0.4f, 1.0f), 
            new Color(0.1f, 0.8f, 1.0f), 
            new Color(0.5f, 0.5f, 1.0f),
            3.5f, 1.3f, 1.5f, 1.2f);
            
        CreateCloudMaterial("SpaceCloud_Purple", cloudShader, 
            new Color(0.8f, 0.2f, 1.0f), 
            new Color(1.0f, 0.3f, 0.7f), 
            new Color(0.6f, 0.1f, 1.0f),
            3.8f, 1.4f, 1.8f, 1.3f);
            
        CreateCloudMaterial("SpaceCloud_Pink", cloudShader, 
            new Color(1.0f, 0.2f, 0.5f), 
            new Color(1.0f, 0.5f, 0.8f), 
            new Color(0.9f, 0.1f, 0.6f),
            4.0f, 1.5f, 1.6f, 1.4f);
            
        CreateCloudMaterial("SpaceCloud_Cyan", cloudShader, 
            new Color(0.1f, 0.9f, 1.0f), 
            new Color(0.2f, 1.0f, 0.8f), 
            new Color(0.0f, 0.7f, 1.0f),
            3.2f, 1.2f, 2.0f, 1.1f);
            
        CreateCloudMaterial("SpaceCloud_Green", cloudShader, 
            new Color(0.2f, 1.0f, 0.5f), 
            new Color(0.5f, 1.0f, 0.2f), 
            new Color(0.1f, 0.8f, 0.4f),
            3.6f, 1.3f, 1.7f, 1.2f);
            
        CreateCloudMaterial("SpaceCloud_Orange", cloudShader, 
            new Color(1.0f, 0.5f, 0.1f), 
            new Color(1.0f, 0.3f, 0.2f), 
            new Color(1.0f, 0.7f, 0.3f),
            4.2f, 1.6f, 1.4f, 1.5f);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("✅ Created 6 cloud materials in /Assets/Materials/");
    }
    
    private static void CreateCloudMaterial(string name, Shader shader, Color color1, Color color2, Color color3, float brightness, float contrast, float scale, float density)
    {
        Material mat = new Material(shader);
        mat.name = name;
        
        mat.SetColor("_CloudColor1", color1);
        mat.SetColor("_CloudColor2", color2);
        mat.SetColor("_CloudColor3", color3);
        mat.SetFloat("_Brightness", brightness);
        mat.SetFloat("_Contrast", contrast);
        mat.SetFloat("_CloudScale", scale);
        mat.SetFloat("_CloudDensity", density);
        mat.SetFloat("_DetailScale", Random.Range(2.2f, 3.0f));
        mat.SetFloat("_DetailAmount", Random.Range(0.3f, 0.4f));
        mat.SetVector("_ScrollSpeed", new Vector4(
            Random.Range(0.015f, 0.025f), 
            Random.Range(0.008f, 0.015f), 
            Random.Range(0.012f, 0.02f), 
            0));
        mat.SetFloat("_ParallaxAmount", Random.Range(0.15f, 0.22f));
        mat.SetFloat("_Steps", 80f);
        mat.SetFloat("_StepSize", 0.1f);
        mat.SetFloat("_AbsorptionFactor", Random.Range(1.0f, 1.6f));
        mat.SetFloat("_EdgeFade", Random.Range(0.35f, 0.45f));
        mat.SetFloat("_DistanceFade", 150f);
        
        string path = $"Assets/Materials/{name}.mat";
        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"Created material: {name}");
    }
}
