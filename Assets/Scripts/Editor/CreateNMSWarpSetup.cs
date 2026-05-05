using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

/// <summary>
/// Editor tool that builds the full NMS Warp Effect hierarchy with Particle Systems
/// and assigns the NebulaParticle material automatically.
/// </summary>
public static class CreateNMSWarpSetup
{
    private const string PrefabSavePath = "Assets/Prefabs/NMSWarpEffect.prefab";
    private const string MaterialBasePath = "Assets/Materials/";

    [MenuItem("Tools/Create NMS Warp Effect Scene")]
    public static void CreateWarpScene()
    {
        // 1. Find / create materials
        Material bgMat    = GetOrCreateNebulaMaterial("NMS_BG_PurpleBlue",
            new Color(0.4f, 0.2f, 1.0f), new Color(0.2f, 0.6f, 1.0f), 4.0f, 0.5f);
        Material midMat   = GetOrCreateNebulaMaterial("NMS_MID_PinkCyan",
            new Color(1.0f, 0.2f, 0.7f), new Color(0.1f, 0.9f, 1.0f), 5.0f, 0.6f);
        Material fgMat    = GetOrCreateNebulaMaterial("NMS_FG_OrangeGreen",
            new Color(1.0f, 0.5f, 0.1f), new Color(0.2f, 1.0f, 0.5f), 6.0f, 0.45f);

        // 2. Build hierarchy
        GameObject root = new GameObject("NMS Warp Effect");

        GameObject bgGO  = CreateLayer(root.transform, "BG Layer",  bgMat,
            maxParticles: 40, sizeMin: 40f, sizeMax: 100f,
            speedMin: 4f, speedMax: 8f, depthMin: 80f, depthMax: 180f, radius: 70f);

        GameObject midGO = CreateLayer(root.transform, "MID Layer", midMat,
            maxParticles: 60, sizeMin: 20f, sizeMax: 55f,
            speedMin: 10f, speedMax: 18f, depthMin: 40f, depthMax: 100f, radius: 50f);

        GameObject fgGO  = CreateLayer(root.transform, "FG Layer",  fgMat,
            maxParticles: 30, sizeMin: 8f,  sizeMax: 25f,
            speedMin: 20f, speedMax: 32f, depthMin: 10f, depthMax: 50f,  radius: 30f);

        // 3. Attach controller
        NMSWarpEffect controller = root.AddComponent<NMSWarpEffect>();

        SerializedObject so = new SerializedObject(controller);
        AssignLayer(so, "backgroundLayer",  bgGO.GetComponent<ParticleSystem>(),  40, new Vector2(40, 100), 8f, 4f, 8f, 70f, 80f, 180f);
        AssignLayer(so, "midLayer",         midGO.GetComponent<ParticleSystem>(), 60, new Vector2(20, 55),  6f, 10f, 18f, 50f, 40f, 100f);
        AssignLayer(so, "foregroundLayer",  fgGO.GetComponent<ParticleSystem>(),  30, new Vector2(8, 25),   4f, 20f, 32f, 30f, 10f, 50f);
        so.ApplyModifiedPropertiesWithoutUndo();

        // 4. Save as prefab
        bool success;
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabSavePath, InteractionMode.AutomatedAction, out success);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = root;

        Debug.Log(success
            ? "✅ NMS Warp Effect created! Press Play to see it in action."
            : "⚠️ Hierarchy created but prefab save failed. Check the path.");
    }

    // ──────────────────────────────────────────────────────────────────

    private static Material GetOrCreateNebulaMaterial(string name,
        Color color1, Color color2, float brightness, float density)
    {
        string path = MaterialBasePath + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        // Prefer NMS simple shader; fall back to URP/Unlit
        Shader sh = Shader.Find("Custom/NoMansSkyNebula")
                 ?? Shader.Find("Custom/SpaceNebulaSimple")
                 ?? Shader.Find("Universal Render Pipeline/Unlit");

        mat = new Material(sh) { name = name };

        if (sh.name.StartsWith("Custom/"))
        {
            mat.SetColor("_CloudColor1", color1);
            mat.SetColor("_CloudColor2", color2);
            mat.SetFloat("_Brightness",    brightness);
            mat.SetFloat("_CloudDensity",  density * 2f);
            mat.SetFloat("_CloudScale",    2.0f);
            mat.SetFloat("_DetailScale",   2.5f);
            mat.SetFloat("_DetailAmount",  0.4f);
            mat.SetFloat("_Steps",         64f);
            mat.SetFloat("_StepSize",      0.08f);
            mat.SetFloat("_EdgeFade",      0.3f);
            mat.SetFloat("_AlphaMultiplier", 1.8f);
        }
        else
        {
            mat.SetColor("_BaseColor", color1);
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static GameObject CreateLayer(Transform parent, string layerName, Material mat,
        int maxParticles, float sizeMin, float sizeMax,
        float speedMin, float speedMax,
        float depthMin, float depthMax, float radius)
    {
        GameObject go = new GameObject(layerName);
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();

        // Renderer setup
        psr.material        = mat;
        psr.renderMode      = ParticleSystemRenderMode.Billboard;
        psr.sortingOrder    = 0;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows  = false;
        psr.allowRoll       = false;

        // Main module
        var main             = ps.main;
        main.loop            = true;
        main.prewarm         = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = maxParticles;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize       = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation   = new ParticleSystem.MinMaxCurve(0, 360f * Mathf.Deg2Rad);
        main.gravityModifier = 0f;

        // Emission
        var emission         = ps.emission;
        emission.rateOverTime = maxParticles / 7f;

        // Shape: box in front of camera
        var shape            = ps.shape;
        shape.shapeType      = ParticleSystemShapeType.Box;
        shape.scale          = new Vector3(radius * 2f, radius * 2f, depthMax - depthMin);

        // Color over lifetime – fade in and out
        var col              = ps.colorOverLifetime;
        col.enabled          = true;
        col.color            = new ParticleSystem.MinMaxGradient(BuildFadeGradient(Color.white));

        // Size over lifetime – slight breathe
        var sol              = ps.sizeOverLifetime;
        sol.enabled          = true;
        sol.size             = new ParticleSystem.MinMaxCurve(1f, BuildBreatheCurve());

        // Rotation over lifetime
        var rol              = ps.rotationOverLifetime;
        rol.enabled          = true;
        rol.z                = new ParticleSystem.MinMaxCurve(-8f * Mathf.Deg2Rad, 8f * Mathf.Deg2Rad);

        // Noise – "breathing" / turbulence
        var noise            = ps.noise;
        noise.enabled        = true;
        noise.strength       = new ParticleSystem.MinMaxCurve(1.5f);
        noise.frequency      = 0.3f;
        noise.scrollSpeed    = 0.3f;
        noise.damping        = true;
        noise.quality        = ParticleSystemNoiseQuality.Medium;

        return go;
    }

    private static Gradient BuildFadeGradient(Color baseColor)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(baseColor, 0f), new GradientColorKey(baseColor, 1f) },
            new[]
            {
                new GradientAlphaKey(0f,   0f),
                new GradientAlphaKey(1f,   0.15f),
                new GradientAlphaKey(1f,   0.85f),
                new GradientAlphaKey(0f,   1f)
            });
        return g;
    }

    private static AnimationCurve BuildBreatheCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f,    0.0f, 0f, 3f),
            new Keyframe(0.12f, 1.0f),
            new Keyframe(0.5f,  0.9f),
            new Keyframe(0.88f, 1.0f),
            new Keyframe(1f,    0.0f, -3f, 0f));
    }

    private static void AssignLayer(SerializedObject so, string fieldName,
        ParticleSystem ps, int count, Vector2 size, float lifetime,
        float speedMin, float speedMax, float radius, float depthMin, float depthMax)
    {
        var field = so.FindProperty(fieldName);
        field.FindPropertyRelative("particleSystem").objectReferenceValue = ps;
        field.FindPropertyRelative("maxParticles").intValue               = count;
        field.FindPropertyRelative("sizeRange").vector2Value              = size;
        field.FindPropertyRelative("lifetime").floatValue                 = lifetime;
        field.FindPropertyRelative("speedMin").floatValue                 = speedMin;
        field.FindPropertyRelative("speedMax").floatValue                 = speedMax;
        field.FindPropertyRelative("spawnRadius").floatValue              = radius;
        field.FindPropertyRelative("spawnDepthMin").floatValue            = depthMin;
        field.FindPropertyRelative("spawnDepthMax").floatValue            = depthMax;
        field.FindPropertyRelative("fadeInTime").floatValue               = 1.5f;
        field.FindPropertyRelative("fadeOutTime").floatValue              = 1.5f;
    }
}
