using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SMRTools_HardStrip
{

    static readonly HashSet<string> ToeNames = new(StringComparer.Ordinal)
    {
        "toe.L","toe.R","Toe.L","Toe.R",
        "toePivot.L","toePivot.R",
        "heel.02.L","heel.02.R","heel.L","heel.R","Heel.L","Heel.R",
        "FootIK.L","FootIK.R"
    };

    [MenuItem("Tools/SkinnedMesh/Hard Strip Toe (Selected)")]
    static void HardStripToeSelected()
    {
        var smrs = Selection.GetFiltered<SkinnedMeshRenderer>(SelectionMode.Deep | SelectionMode.Editable);
        if (smrs == null || smrs.Length == 0)
        {
            Debug.LogWarning("[HardStripToe] 선택한 오브젝트에서 SkinnedMeshRenderer를 찾지 못했어요.");
            return;
        }

        foreach (var smr in smrs)
        {
            if (!smr || smr.sharedMesh == null || smr.bones == null || smr.bones.Length == 0)
                continue;

            var removeIdx = new HashSet<int>();
            for (int i = 0; i < smr.bones.Length; i++)
            {
                var t = smr.bones[i];
                if (t && ToeNames.Contains(t.name))
                    removeIdx.Add(i);
            }

            if (removeIdx.Count == 0)
                continue;

            Undo.RecordObject(smr, "Hard Strip Toe Influence");

            int idxFootL = IndexOfBone(smr, "foot.L", "Foot.L", "footL");
            int idxFootR = IndexOfBone(smr, "foot.R", "Foot.R", "footR");

            var oldMesh = smr.sharedMesh;
            var mesh = UnityEngine.Object.Instantiate(oldMesh);
            mesh.name = oldMesh.name + "_noToeHard";

            var oldBones = smr.bones;
            var oldBindposes = mesh.bindposes;
            var oldBW = mesh.boneWeights;

            var newBones = new List<Transform>(oldBones.Length);
            var newBindposes = new List<Matrix4x4>(oldBindposes.Length);

            var mapOldToNew = new Dictionary<int, int>(oldBones.Length);
            for (int i = 0; i < oldBones.Length; i++)
            {
                if (removeIdx.Contains(i)) continue;
                int newIdx = newBones.Count;
                newBones.Add(oldBones[i]);
                newBindposes.Add(oldBindposes[i]);
                mapOldToNew[i] = newIdx;
            }

            int fallbackL = (idxFootL >= 0 && mapOldToNew.TryGetValue(idxFootL, out var nfL)) ? nfL : -1;
            int fallbackR = (idxFootR >= 0 && mapOldToNew.TryGetValue(idxFootR, out var nfR)) ? nfR : -1;

            BoneWeight Remap(BoneWeight b)
            {

                void Fix(ref int bi, ref float w)
                {
                    if (w <= 0f) return;

                    if (removeIdx.Contains(bi))
                    {

                        bool isRight = NameOf(oldBones, bi).Contains(".R");
                        int fb = isRight ? fallbackR : fallbackL;
                        if (fb < 0) fb = fallbackL >= 0 ? fallbackL : fallbackR;

                        if (fb >= 0) { bi = fb; } else { w = 0f; }
                    }
                    else
                    {

                        if (mapOldToNew.TryGetValue(bi, out int nb)) bi = nb;
                        else { w = 0f; }
                    }
                }

                int bi0 = b.boneIndex0, bi1 = b.boneIndex1, bi2 = b.boneIndex2, bi3 = b.boneIndex3;
                float w0 = b.weight0, w1 = b.weight1, w2 = b.weight2, w3 = b.weight3;

                Fix(ref bi0, ref w0);
                Fix(ref bi1, ref w1);
                Fix(ref bi2, ref w2);
                Fix(ref bi3, ref w3);

                float sum = w0 + w1 + w2 + w3;
                if (sum > 1e-6f)
                {
                    float inv = 1f / sum;
                    w0 *= inv; w1 *= inv; w2 *= inv; w3 *= inv;
                }
                else
                {

                    int fb = fallbackL >= 0 ? fallbackL : fallbackR;
                    if (fb >= 0) { bi0 = fb; w0 = 1f; bi1 = bi2 = bi3 = 0; w1 = w2 = w3 = 0f; }
                }

                return new BoneWeight { boneIndex0 = bi0, weight0 = w0, boneIndex1 = bi1, weight1 = w1, boneIndex2 = bi2, weight2 = w2, boneIndex3 = bi3, weight3 = w3 };
            }

            var newBW = new BoneWeight[oldBW.Length];
            for (int i = 0; i < oldBW.Length; i++) newBW[i] = Remap(oldBW[i]);

            mesh.boneWeights = newBW;
            mesh.bindposes = newBindposes.ToArray();
            mesh.RecalculateBounds();

            smr.sharedMesh = mesh;
            smr.bones = newBones.ToArray();

            Debug.Log($"[HardStripToe] {GetPath(smr.transform)}  bones:{oldBones.Length}->{newBones.Count}, vertices:{newBW.Length}, removed:{removeIdx.Count}");
        }

        Debug.Log("[HardStripToe] 완료.");
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

    static string NameOf(Transform[] arr, int idx)
    {
        if (idx < 0 || idx >= arr.Length) return "";
        var t = arr[idx];
        return t ? t.name : "";
    }

    static string GetPath(Transform t)
    {
        if (!t) return "(null)";
        string p = t.name;
        while (t.parent) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
