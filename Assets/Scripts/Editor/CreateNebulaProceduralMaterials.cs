using UnityEngine;
using UnityEditor;

public static class CreateNebulaProceduralMaterials
{
    private const string ShaderName   = "Custom/NebulaProcedural";
    private const string MaterialsPath = "Assets/Materials/";

    [MenuItem("Tools/Nebula Procedural/Create All Materials")]
    public static void CreateAll()
    {
        Shader sh = Shader.Find(ShaderName);
        if (sh == null)
        {
            Debug.LogError($"Shader '{ShaderName}' not found. Make sure NebulaProcedural.shader is in Assets/Shaders/.");
            return;
        }

        // Palette: Color A, Color B, Brightness, Density, Scale, AnimSpeed
        Make(sh, "Nebula_PurpleBlue",
            new Color(0.55f, 0.10f, 1.00f),
            new Color(0.10f, 0.65f, 1.00f),
            brightness: 4.5f, density: 1.6f, scale: 1.8f, animSpeed: 0.07f);

        Make(sh, "Nebula_PinkCyan",
            new Color(1.00f, 0.15f, 0.70f),
            new Color(0.05f, 0.85f, 1.00f),
            brightness: 5.0f, density: 1.5f, scale: 2.0f, animSpeed: 0.06f);

        Make(sh, "Nebula_OrangeBlue",
            new Color(1.00f, 0.45f, 0.05f),
            new Color(0.05f, 0.40f, 1.00f),
            brightness: 5.5f, density: 1.7f, scale: 1.6f, animSpeed: 0.09f);

        Make(sh, "Nebula_GreenPurple",
            new Color(0.10f, 1.00f, 0.45f),
            new Color(0.60f, 0.10f, 1.00f),
            brightness: 4.8f, density: 1.4f, scale: 2.2f, animSpeed: 0.05f);

        Make(sh, "Nebula_RedTeal",
            new Color(1.00f, 0.20f, 0.15f),
            new Color(0.00f, 0.85f, 0.75f),
            brightness: 5.2f, density: 1.8f, scale: 1.5f, animSpeed: 0.08f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ Created 5 procedural nebula materials in Assets/Materials/");
    }

    [MenuItem("Tools/Nebula Procedural/Apply to Selected Object")]
    public static void ApplyToSelected()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("Select a GameObject first.");
            return;
        }

        string matPath = MaterialsPath + "Nebula_PurpleBlue.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        if (mat == null)
        {
            Debug.LogWarning("Create materials first: Tools → Nebula Procedural → Create All Materials");
            return;
        }

        MeshRenderer mr = Selection.activeGameObject.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            Debug.LogWarning("Selected object has no MeshRenderer.");
            return;
        }

        mr.sharedMaterial = mat;
        Debug.Log($"✅ Applied {mat.name} to {Selection.activeGameObject.name}");
    }

    private static void Make(Shader sh, string matName,
        Color colorA, Color colorB,
        float brightness, float density, float scale, float animSpeed)
    {
        string path = MaterialsPath + matName + ".mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(sh) { name = matName };
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = sh;
        }

        mat.SetColor("_ColorA",      colorA);
        mat.SetColor("_ColorB",      colorB);
        mat.SetFloat("_Brightness",  brightness);
        mat.SetFloat("_Density",     density);
        mat.SetFloat("_Scale",       scale);
        mat.SetFloat("_AnimSpeed",   animSpeed);
        mat.SetFloat("_EdgeSoftness", 0.35f);
        mat.SetVector("_ScrollDir",  new Vector4(1f, 0.3f, 0.5f, 0f));
        mat.SetFloat("_Steps",       64f);
        mat.SetFloat("_StepSize",    0.08f);

        EditorUtility.SetDirty(mat);
        Debug.Log($"  → {matName}");
    }
}
