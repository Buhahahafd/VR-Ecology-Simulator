using UnityEngine;
using UnityEditor;

public static class CreateNMSMaterials
{
    [MenuItem("Tools/Create No Man's Sky Cloud Materials")]
    public static void CreateMaterials()
    {
        Shader cloudShader = Shader.Find("Custom/NoMansSkyNebula");
        if (cloudShader == null)
        {
            Debug.LogError("Shader Custom/NoMansSkyNebula not found!");
            return;
        }
        
        CreateNMSMaterial("NMS_Blue", cloudShader, 
            new Color(0.3f, 0.7f, 1.0f), 
            new Color(0.6f, 0.9f, 1.0f),
            5.0f, 1.8f, 2.2f);
            
        CreateNMSMaterial("NMS_Purple", cloudShader, 
            new Color(0.7f, 0.3f, 1.0f), 
            new Color(1.0f, 0.5f, 0.9f),
            5.5f, 1.6f, 2.0f);
            
        CreateNMSMaterial("NMS_Pink", cloudShader, 
            new Color(1.0f, 0.3f, 0.6f), 
            new Color(1.0f, 0.6f, 0.9f),
            6.0f, 1.9f, 1.8f);
            
        CreateNMSMaterial("NMS_Cyan", cloudShader, 
            new Color(0.2f, 0.9f, 1.0f), 
            new Color(0.5f, 1.0f, 1.0f),
            5.2f, 2.0f, 2.1f);
            
        CreateNMSMaterial("NMS_Green", cloudShader, 
            new Color(0.3f, 1.0f, 0.6f), 
            new Color(0.6f, 1.0f, 0.8f),
            5.3f, 1.7f, 1.9f);
            
        CreateNMSMaterial("NMS_Orange", cloudShader, 
            new Color(1.0f, 0.6f, 0.2f), 
            new Color(1.0f, 0.8f, 0.4f),
            6.2f, 2.1f, 1.7f);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("✅ Created 6 No Man's Sky style cloud materials!");
    }
    
    private static void CreateNMSMaterial(string name, Shader shader, Color color1, Color color2, float brightness, float density, float scale)
    {
        Material mat = new Material(shader);
        mat.name = name;
        
        mat.SetColor("_CloudColor1", color1);
        mat.SetColor("_CloudColor2", color2);
        mat.SetFloat("_Brightness", brightness);
        mat.SetFloat("_CloudScale", scale);
        mat.SetFloat("_CloudDensity", density);
        mat.SetFloat("_DetailScale", Random.Range(2.3f, 2.8f));
        mat.SetFloat("_DetailAmount", Random.Range(0.35f, 0.45f));
        mat.SetVector("_ScrollSpeed", new Vector4(
            Random.Range(0.008f, 0.015f), 
            Random.Range(0.004f, 0.01f), 
            Random.Range(0.006f, 0.012f), 
            0));
        mat.SetFloat("_Steps", 64f);
        mat.SetFloat("_StepSize", 0.08f);
        mat.SetFloat("_EdgeFade", 0.3f);
        mat.SetFloat("_AlphaMultiplier", 1.5f);
        
        string path = $"Assets/Materials/{name}.mat";
        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"✅ Created: {name}");
    }
}
