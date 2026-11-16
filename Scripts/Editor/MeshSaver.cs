using UnityEditor;
using UnityEngine;
using System.IO;

public static class MeshSaver
{
    [MenuItem("Tools/SkinnedMesh/Save Selected Meshes as Assets")]
    static void SaveSelectedMeshes()
    {
        foreach (var smr in Selection.GetFiltered<SkinnedMeshRenderer>(SelectionMode.Deep))
        {
            if (!smr || smr.sharedMesh == null)
            {
                Debug.LogWarning($"[MeshSaver] {smr?.name ?? "??"} → 저장할 Mesh가 없음");
                continue;
            }

            var mesh = smr.sharedMesh;
            string folder = "Assets/Model/Astronaut/source";
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string path = $"{folder}/{mesh.name}.asset";
            AssetDatabase.CreateAsset(Object.Instantiate(mesh), path);
            Debug.Log($"[MeshSaver] {mesh.name} 저장 완료 → {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
