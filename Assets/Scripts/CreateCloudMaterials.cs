using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

public class CreateCloudMaterials : MonoBehaviour
{
    [ContextMenu("Create Cloud Materials")]
    private void CreateMaterials()
    {
        Shader cloudShader = Shader.Find("Custom/SpaceNebulaRaymarching");
        if (cloudShader == null)
        {
            Debug.LogError("Shader Custom/SpaceNebulaRaymarching not found!");
            return;
        }
        
        CreateCloudMaterial("SpaceCloud_Blue", cloudShader, 
            new Color(0.2f, 0.4f, 1.0f), 
            new Color(0.1f, 0.8f, 1.0f), 
            new Color(0.5f, 0.5f, 1.0f));
            
        CreateCloudMaterial("SpaceCloud_Purple", cloudShader, 
            new Color(0.8f, 0.2f, 1.0f), 
            new Color(1.0f, 0.3f, 0.7f), 
            new Color(0.6f, 0.1f, 1.0f));
            
        CreateCloudMaterial("SpaceCloud_Pink", cloudShader, 
            new Color(1.0f, 0.2f, 0.5f), 
            new Color(1.0f, 0.5f, 0.8f), 
            new Color(0.9f, 0.1f, 0.6f));
            
        CreateCloudMaterial("SpaceCloud_Cyan", cloudShader, 
            new Color(0.1f, 0.9f, 1.0f), 
            new Color(0.2f, 1.0f, 0.8f), 
            new Color(0.0f, 0.7f, 1.0f));
            
        CreateCloudMaterial("SpaceCloud_Green", cloudShader, 
            new Color(0.2f, 1.0f, 0.5f), 
            new Color(0.5f, 1.0f, 0.2f), 
            new Color(0.1f, 0.8f, 0.4f));
            
        CreateCloudMaterial("SpaceCloud_Orange", cloudShader, 
            new Color(1.0f, 0.5f, 0.1f), 
            new Color(1.0f, 0.3f, 0.2f), 
            new Color(1.0f, 0.7f, 0.3f));
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("Cloud materials created in /Assets/Materials/");
    }
    
    private void CreateCloudMaterial(string name, Shader shader, Color color1, Color color2, Color color3)
    {
        Material mat = new Material(shader);
        mat.name = name;
        
        mat.SetColor("_CloudColor1", color1);
        mat.SetColor("_CloudColor2", color2);
        mat.SetColor("_CloudColor3", color3);
        mat.SetFloat("_Brightness", Random.Range(2.5f, 4.0f));
        mat.SetFloat("_Contrast", Random.Range(1.2f, 1.6f));
        mat.SetFloat("_CloudScale", Random.Range(1.2f, 2.5f));
        mat.SetFloat("_CloudDensity", Random.Range(0.8f, 1.5f));
        mat.SetFloat("_DetailScale", Random.Range(2.0f, 3.5f));
        mat.SetFloat("_DetailAmount", Random.Range(0.25f, 0.45f));
        mat.SetVector("_ScrollSpeed", new Vector4(
            Random.Range(0.01f, 0.03f), 
            Random.Range(0.005f, 0.02f), 
            Random.Range(0.01f, 0.025f), 
            0));
        mat.SetFloat("_ParallaxAmount", Random.Range(0.1f, 0.25f));
        mat.SetFloat("_Steps", 80f);
        mat.SetFloat("_StepSize", 0.1f);
        mat.SetFloat("_AbsorptionFactor", Random.Range(1.0f, 1.8f));
        mat.SetFloat("_EdgeFade", Random.Range(0.3f, 0.5f));
        mat.SetFloat("_DistanceFade", 150f);
        
        string path = $"Assets/Materials/{name}.mat";
        AssetDatabase.CreateAsset(mat, path);
    }
}
#endif
