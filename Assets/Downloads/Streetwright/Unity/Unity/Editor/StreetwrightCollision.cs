// Streetwright for Unity (2021.3+, URP or HDRP): adds the kit's convex collision hulls to placed pieces.
// Put this file in any Editor folder. Copy glTF/collision.json into the project (for example
// Assets/Streetwright/glTF/collision.json), select piece instances in the scene or prefabs in the Project window, then
// Tools > Streetwright > Add Collision To Selection. Each piece gets a child "Collision" with one convex MeshCollider per
// hull (the hulls are the same ones the Unreal FBX carries as UCX_ meshes). Pieces are matched by name
// (SM_Kit_..., a trailing " (1)" or "(Clone)" is ignored).
// Hull points in collision.json are glTF space (metres, +Y up, right-handed). glTFast imports glTF by negating X, so
// the default flipX = true matches pieces imported from the .glb files.
// Untested draft; report problems to https://stonefletcher.com.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class StreetwrightCollision
{
    [Serializable] class Hull { public float[] points; public int[] tris; }
    [Serializable] class Piece { public string name; public string method; public Hull[] hulls; }
    [Serializable] class Kit { public string units; public Piece[] pieces; }

    static Dictionary<string, Piece> cache;

    static Dictionary<string, Piece> Load()
    {
        if (cache != null) return cache;
        var guids = AssetDatabase.FindAssets("collision t:TextAsset");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (!path.EndsWith("collision.json")) continue;
            var kit = JsonUtility.FromJson<Kit>(File.ReadAllText(path));
            if (kit == null || kit.pieces == null) continue;
            cache = new Dictionary<string, Piece>();
            foreach (var p in kit.pieces) cache[p.name] = p;
            return cache;
        }
        Debug.LogError("Streetwright: no collision.json in the project (copy glTF/collision.json into Assets).");
        return null;
    }

    [MenuItem("Tools/Streetwright/Add Collision To Selection")]
    static void AddToSelection() { Add(Selection.gameObjects, true); }

    [MenuItem("Tools/Streetwright/Reload collision.json")]
    static void Reload() { cache = null; Load(); }

    public static void Add(IEnumerable<GameObject> objects, bool flipX)
    {
        var kit = Load();
        if (kit == null) return;
        int n = 0;
        foreach (var go in objects)
        {
            var key = Regex.Replace(go.name, @"(\s*\(\d+\)|\(Clone\))$", "");
            if (!kit.TryGetValue(key, out var piece)) { Debug.LogWarning("Streetwright: no hulls for " + go.name); continue; }
            var old = go.transform.Find("Collision");
            if (old) Undo.DestroyObjectImmediate(old.gameObject);
            var holder = new GameObject("Collision");
            Undo.RegisterCreatedObjectUndo(holder, "Streetwright collision");
            holder.transform.SetParent(go.transform, false);
            holder.isStatic = go.isStatic;
            for (int h = 0; h < piece.hulls.Length; h++)
            {
                var pts = piece.hulls[h].points;
                var verts = new Vector3[pts.Length / 3];
                for (int i = 0; i < verts.Length; i++)
                    verts[i] = new Vector3(flipX ? -pts[3 * i] : pts[3 * i], pts[3 * i + 1], pts[3 * i + 2]);
                var tris = (int[])piece.hulls[h].tris.Clone();
                if (flipX)   // mirroring turns the winding inside out
                    for (int i = 0; i < tris.Length; i += 3) { var t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t; }
                var mesh = new Mesh { name = key + "_Hull" + h.ToString("00"), vertices = verts, triangles = tris };
                mesh.RecalculateBounds();
                var assetPath = AssetDatabase.GetAssetPath(go);
                if (!string.IsNullOrEmpty(assetPath)) AssetDatabase.AddObjectToAsset(mesh, assetPath);   // prefab: keep the mesh
                var col = holder.AddComponent<MeshCollider>();
                col.convex = true;          // Unity builds the hull from the points
                col.sharedMesh = mesh;
            }
            n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Streetwright: collision added to " + n + " object(s).");
    }
}
