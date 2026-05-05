using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Assigns NMSSkyboxMaterial as the scene skybox and configures ambient lighting
/// to match a dark NMS space environment.
/// </summary>
public static class NMSSkyboxSetup
{
    private const string MaterialPath = "Assets/Materials/NMSSkyboxMaterial.mat";

    [MenuItem("Tools/NMS Setup/Apply NMS Skybox to Scene")]
    public static void Apply()
    {
        Material skybox = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (skybox == null)
        {
            Debug.LogError($"[NMSSkybox] Material not found: {MaterialPath}");
            return;
        }

        // Assign skybox
        RenderSettings.skybox = skybox;

        // Ambient: solid dark colour — space doesn't scatter light
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.02f, 0.01f, 0.04f);

        // No sun in a loading screen
        RenderSettings.sun = null;

        // Fog off — nebula volume handles depth
        RenderSettings.fog = false;

        // Bake the skybox so it's applied immediately in editor
        DynamicGI.UpdateEnvironment();

        EditorUtility.SetDirty(RenderSettings.skybox);
        Debug.Log("[NMSSkybox] Skybox applied.");
    }
}
