using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralCircle : MonoBehaviour
{
    [Header("Shape")]
    public float radius = 0.12f;
    [Range(3, 256)] public int segments = 64;
    public bool doubleSided = true;

    void Reset() { Generate(); }
    void OnValidate() { Generate(); }

    void Generate()
    {
        int seg = Mathf.Max(3, segments);
        int v0 = seg + 1;
        int vCount = doubleSided ? v0 * 2 : v0;

        var verts = new Vector3[vCount];
        var uvs = new Vector2[vCount];
        var tris = new int[seg * 3 * (doubleSided ? 2 : 1)];

        verts[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < seg; i++)
        {
            float a = (float)i / seg * Mathf.PI * 2f;
            float x = Mathf.Cos(a) * radius;
            float y = Mathf.Sin(a) * radius;
            verts[i + 1] = new Vector3(x, y, 0f);
            uvs[i + 1] = new Vector2(x / (2f * radius) + 0.5f, y / (2f * radius) + 0.5f);
        }

        int t = 0;
        for (int i = 0; i < seg; i++)
        {
            int n = (i + 1) % seg;
            tris[t++] = 0;
            tris[t++] = i + 1;
            tris[t++] = n + 1;
        }

        if (doubleSided)
        {
            int o = v0;
            for (int i = 0; i < v0; i++) { verts[o + i] = verts[i]; uvs[o + i] = uvs[i]; }
            for (int i = 0; i < seg; i++)
            {
                int n = (i + 1) % seg;
                tris[t++] = o + 0;
                tris[t++] = o + n + 1;
                tris[t++] = o + i + 1;
            }
        }

        var m = new Mesh { name = "FaceCircle" };
        m.vertices = verts; m.uv = uvs; m.triangles = tris;
        m.RecalculateNormals(); m.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = m;
    }
}
