using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Bakes per-vertex barycentric coordinates into UV channel 2 so that
    /// the NeonWireframe shader can render precise anti-aliased edges without
    /// a geometry shader (which is unsupported on most mobile XR platforms).
    ///
    /// Attach to a GameObject and call BakeIfNeeded() once after instantiation,
    /// or let Awake() handle it automatically.
    ///
    /// The baked mesh is unique per-instance so it won't affect other objects
    /// sharing the original mesh asset.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class NeonWireframeBaker : MonoBehaviour
    {
        // Cached so we can avoid re-baking on re-enable.
        private bool isBaked;

        private void Awake()
        {
            BakeIfNeeded();
        }

        /// <summary>
        /// Duplicates the shared mesh and writes barycentric coordinates
        /// into UV channel 2 (Vector2 XY; Z = 1 - X - Y is inferred in the shader).
        /// Safe to call multiple times — bakes only once.
        /// </summary>
        public void BakeIfNeeded()
        {
            if (isBaked) return;

            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            Mesh src  = mf.sharedMesh;
            int[] triangles = src.triangles;
            int triCount    = triangles.Length;

            // Build a new mesh with unshared vertices (one set per triangle face)
            // so each vertex in a triangle can get a unique barycentric coord.
            int vertCount       = triCount;                  // 3 verts per tri
            Vector3[] newVerts  = new Vector3[vertCount];
            Vector3[] newNorms  = new Vector3[vertCount];
            Vector4[] newTan    = new Vector4[vertCount];
            Vector2[] newUV     = new Vector2[vertCount];
            Vector2[] newBary   = new Vector2[vertCount];
            int[]     newTris   = new int[vertCount];

            Vector3[] srcVerts = src.vertices;
            Vector3[] srcNorms = src.normals;
            Vector4[] srcTan   = src.tangents;
            Vector2[] srcUV    = src.uv;
            bool hasNormals    = srcNorms != null && srcNorms.Length == srcVerts.Length;
            bool hasTangents   = srcTan   != null && srcTan.Length   == srcVerts.Length;
            bool hasUV         = srcUV    != null && srcUV.Length    == srcVerts.Length;

            // Barycentric triplet: vertex 0 = (1,0), 1 = (0,1), 2 = (0,0) → Z = 1-X-Y
            Vector2[] baryCoords =
            {
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0f)
            };

            for (int i = 0; i < triCount; i++)
            {
                int srcIdx      = triangles[i];
                newVerts[i]     = srcVerts[srcIdx];
                newNorms[i]     = hasNormals  ? srcNorms[srcIdx]  : Vector3.up;
                newTan[i]       = hasTangents ? srcTan[srcIdx]    : new Vector4(1, 0, 0, 1);
                newUV[i]        = hasUV       ? srcUV[srcIdx]     : Vector2.zero;
                newBary[i]      = baryCoords[i % 3];
                newTris[i]      = i;
            }

            Mesh baked          = new Mesh();
            baked.name          = $"{src.name}_WireBaked";
            baked.indexFormat   = vertCount > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            baked.vertices      = newVerts;
            baked.normals       = newNorms;
            baked.tangents      = newTan;
            baked.uv            = newUV;
            baked.uv2           = newBary;
            baked.triangles     = newTris;
            baked.RecalculateBounds();

            mf.mesh  = baked;   // Assign instance mesh, not sharedMesh.
            isBaked  = true;
        }
    }
}
