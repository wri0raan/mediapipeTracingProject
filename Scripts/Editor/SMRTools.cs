using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SMRTools
{
    static readonly string[] ToeNames = { "toe.L", "toe.R", "Toe.L", "Toe.R", "toePivot.L", "toePivot.R", "FootIK.L", "FootIK.R","heel.02.R", "heel.02.L" };

    [MenuItem("Tools/SkinnedMesh/Show Bones (Selected)")]
    static void ShowBonesSelected()
    {
        var smrs = Selection.GetFiltered<SkinnedMeshRenderer>(SelectionMode.Deep);
        if (smrs.Length == 0) { Debug.LogWarning("[Show Bones] 선택한 오브젝트에서 SkinnedMeshRenderer가 없어요."); return; }

        foreach (var smr in smrs)
        {
            if (!smr || smr.bones == null) continue;
            Debug.Log($"[Show Bones] {GetPath(smr.transform)} (count={smr.bones.Length})");
            for (int i = 0; i < smr.bones.Length; i++)
            {
                var t = smr.bones[i];
                var name = t ? t.name : "(null)";
                bool isToe = ToeNames.Contains(name);
                Debug.Log($"{i,3}: {name}" + (isToe ? "  <== toe related" : ""));
            }
        }
    }

    [MenuItem("Tools/SkinnedMesh/Strip Toe Influence (Selected)")]
    static void StripToeSelected()
    {
        var smrs = Selection.GetFiltered<SkinnedMeshRenderer>(SelectionMode.Deep);
        if (smrs.Length == 0) { Debug.LogWarning("[Strip Toe] 선택한 오브젝트에서 SkinnedMeshRenderer가 없어요."); return; }

        int total = 0, touched = 0;
        foreach (var smr in smrs)
        {
            if (!smr || smr.sharedMesh == null || smr.bones == null) continue;

            var stripIdx = smr.bones
                .Select((t, i) => (t, i))
                .Where(p => p.t && ToeNames.Contains(p.t.name))
                .Select(p => p.i)
                .ToArray();

            if (stripIdx.Length == 0) continue;

            Undo.RecordObject(smr, "Strip Toe Influence");

            var mesh = Object.Instantiate(smr.sharedMesh);
            mesh.name = smr.sharedMesh.name + "_noToe";
            var bw = mesh.boneWeights;
            total += bw.Length;

            int footL = IndexOfBone(smr, "foot.L", "Foot.L", "footL");
            int footR = IndexOfBone(smr, "foot.R", "Foot.R", "footR");

            for (int v = 0; v < bw.Length; v++)
            {
                var b = bw[v];
                bool changed = false;

                if (stripIdx.Contains(b.boneIndex0)) { b.weight0 = 0f; changed = true; }
                if (stripIdx.Contains(b.boneIndex1)) { b.weight1 = 0f; changed = true; }
                if (stripIdx.Contains(b.boneIndex2)) { b.weight2 = 0f; changed = true; }
                if (stripIdx.Contains(b.boneIndex3)) { b.weight3 = 0f; changed = true; }

                if (changed)
                {
                    float sum = b.weight0 + b.weight1 + b.weight2 + b.weight3;
                    if (sum > 1e-6f)
                    {
                        float inv = 1f / sum;
                        b.weight0 *= inv; b.weight1 *= inv; b.weight2 *= inv; b.weight3 *= inv;
                    }
                    else
                    {
                        int foot = (footL >= 0) ? footL : footR;
                        if (foot >= 0)
                        {
                            b.boneIndex0 = foot; b.weight0 = 1f;
                            b.boneIndex1 = b.boneIndex2 = b.boneIndex3 = 0;
                            b.weight1 = b.weight2 = b.weight3 = 0f;
                        }
                    }
                    bw[v] = b;
                    touched++;
                }
            }

            mesh.boneWeights = bw;
            mesh.RecalculateBounds();
            smr.sharedMesh = mesh;

            Debug.Log($"[Strip Toe] {GetPath(smr.transform)} → mesh '{mesh.name}', vertices={bw.Length}");
        }

        Debug.Log($"[Strip Toe] Done. totalVerts={total}, affectedVerts={touched}");
    }

    static int IndexOfBone(SkinnedMeshRenderer smr, params string[] names)
    {
        for (int i = 0; i < smr.bones.Length; i++)
        {
            var t = smr.bones[i];
            if (!t) continue;
            foreach (var n in names) if (t.name == n) return i;
        }
        return -1;
    }

    static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
