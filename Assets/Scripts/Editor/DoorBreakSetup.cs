using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WaterChaos.EditorTools.MiniVillage;
using WaterChaos.EditorTools.OfficeDistrict;
using WaterChaos.Interaction;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// Makes every hinged door prefab axe-breakable, in place (the house prefabs that nest them keep their links):
    /// two crack overlays on both faces of the leaf, eight prepared planks that fall with physics when it breaks,
    /// and <see cref="BreakableDoor"/> wired to them. Safe to run again: the previous cracks and planks are replaced.
    /// </summary>
    public static class DoorBreakSetup
    {
        private const string DoorFolder = VillageAssets.PrefabFolder + "/Doors";
        private const int Columns = 2;
        private const int Rows = 4;
        // Planks fly on the Ignore Raycast layer, like glass shards: the stream and the player's rays pass them.
        private const int PlankLayer = 2;

        /// <summary>Main material key of each door; also used for its planks.</summary>
        private static readonly Dictionary<string, string> Materials = new Dictionary<string, string>
        {
            { "Door_Wood", "Door" },
            { "Door_Red", "DoorRed" },
            { "Door_Shop", "MetalDark" },
            { "Door_Office", "WoodDark" },
            { "Door_OfficeGlass", "SteelDark" },
            { "Door_Brownstone", "DoorRed" },
            { "Door_Factory", "Steel" },
        };

        [MenuItem("Water Chaos/Setup Breakable Doors")]
        public static void SetupAll()
        {
            Directory.CreateDirectory(DistrictKit.EffectFolder);
            AssetDatabase.Refresh();
            GameObject splinters = DistrictKit.Burst("DoorSplinters", "WoodDark", 22, new Vector3(1f, 2f, 0.1f));

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { DoorFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                Apply(path, Materials.TryGetValue(name, out string material) ? material : "Door", splinters);
                count++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Breakable doors: {count} door prefabs can now be chopped down with the axe.");
        }

        /// <summary>Adds cracks, planks and <see cref="BreakableDoor"/> to the door prefab at <paramref name="path"/>.</summary>
        public static void Apply(string path, string material, GameObject splinters)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var leaf = root.GetComponent<BoxCollider>();
                if (leaf == null)
                {
                    Debug.LogWarning($"Breakable doors: {name} has no box collider; skipped.");
                    return;
                }

                foreach (string old in new[] { "Cracks 1", "Cracks 2", "Planks" })
                {
                    Transform child = root.transform.Find(old);
                    if (child != null)
                        Object.DestroyImmediate(child.gameObject);
                }

                Vector3 size = leaf.size;
                Vector3 center = leaf.center;
                bool metal = material.Contains("Steel") || material.Contains("Metal");
                string crackMaterial = metal ? "Black" : "WoodLight";

                GameObject first = CrackStage(root.transform, name, 1, size, center, crackMaterial);
                GameObject second = CrackStage(root.transform, name, 2, size, center, crackMaterial);
                Rigidbody[] planks = Planks(root.transform, name, size, center, material);

                if (!root.TryGetComponent(out BreakableDoor door))
                    door = root.AddComponent<BreakableDoor>();

                var data = new SerializedObject(door);
                SetArray(data.FindProperty("_crackStages"), new Object[] { first, second });
                SetArray(data.FindProperty("_fragments"), planks);
                data.FindProperty("_breakEffect").objectReferenceValue = splinters;
                data.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Crack lines on both faces; stage 2 adds more lines and a second break point.</summary>
        private static GameObject CrackStage(Transform leaf, string door, int stage, Vector3 size, Vector3 center, string material)
        {
            const float margin = 0.04f;
            var mesh = new LowPolyMesh();
            // In front of the raised panels on the leaf (they stand 2 cm out), or the lines hide behind them.
            float face = size.z * 0.5f + 0.022f;
            var min = new Vector2(center.x - size.x * 0.5f, center.y - size.y * 0.5f);
            var origins = new List<Vector2> { new Vector2(size.x * 0.62f, size.y * 0.55f) };
            int lines = 4;
            if (stage == 2)
            {
                origins.Add(new Vector2(size.x * 0.35f, size.y * 0.3f));
                lines = 6;
            }

            for (int o = 0; o < origins.Count; o++)
            {
                for (int i = 0; i < lines; i++)
                {
                    float angle = (i * 360f / lines + o * 37f + stage * 19f) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float length = size.y * (stage == 1 ? 0.16f : 0.24f) * (0.7f + 0.3f * ((i * 7 + o) % 3));
                    // Shorten the line so it ends inside the leaf instead of sticking out past its edge.
                    length = Mathf.Min(length, DistanceToEdge(origins[o], direction, size, margin));
                    Vector2 middle = min + origins[o] + direction * length * 0.5f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        mesh.SetTransform(new Vector3(middle.x, middle.y, center.z + side * face), Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg));
                        mesh.Box(Vector3.zero, new Vector3(length, 0.022f, 0.006f), material);
                        mesh.ResetTransform();
                    }
                }
            }

            GameObject cracks = VillageAssets.CreateMeshObject("Cracks " + stage, mesh, FreshAsset($"{door}_Cracks{stage}"));
            cracks.transform.SetParent(leaf, false);
            cracks.SetActive(false);
            return cracks;
        }

        /// <summary>
        /// Distance from <paramref name="origin"/> along <paramref name="direction"/> to the leaf's edge
        /// (a <paramref name="size"/>.x × <paramref name="size"/>.y rectangle inset by <paramref name="margin"/>), in meters (m).
        /// </summary>
        private static float DistanceToEdge(Vector2 origin, Vector2 direction, Vector3 size, float margin)
        {
            float distance = float.MaxValue;
            if (direction.x > 0.0001f)
                distance = Mathf.Min(distance, (size.x - margin - origin.x) / direction.x);
            else if (direction.x < -0.0001f)
                distance = Mathf.Min(distance, (margin - origin.x) / direction.x);
            if (direction.y > 0.0001f)
                distance = Mathf.Min(distance, (size.y - margin - origin.y) / direction.y);
            else if (direction.y < -0.0001f)
                distance = Mathf.Min(distance, (margin - origin.y) / direction.y);
            return Mathf.Max(0f, distance);
        }

        /// <summary>The leaf cut into a 2 × 4 grid of planks, each a kinematic, inactive body.</summary>
        private static Rigidbody[] Planks(Transform leaf, string door, Vector3 size, Vector3 center, string material)
        {
            var group = new GameObject("Planks");
            group.transform.SetParent(leaf, false);
            string asset = FreshAsset($"{door}_Planks");

            var planks = new List<Rigidbody>();
            float w = size.x / Columns;
            float h = size.y / Rows;
            Vector3 min = center - size * 0.5f;
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    var pieceSize = new Vector3(w - 0.015f, h - 0.015f, size.z);
                    var mesh = new LowPolyMesh();
                    mesh.Box(Vector3.zero, pieceSize, material);
                    GameObject piece = VillageAssets.CreateMeshObject($"Plank_{row}_{column}", mesh, asset);
                    piece.layer = PlankLayer;
                    piece.transform.SetParent(group.transform, false);
                    piece.transform.localPosition = new Vector3(min.x + w * (column + 0.5f), min.y + h * (row + 0.5f), center.z);
                    piece.AddComponent<BoxCollider>().size = pieceSize;

                    var body = piece.AddComponent<Rigidbody>();
                    body.mass = 2f;
                    body.isKinematic = true;
                    body.linearDamping = 0.1f;
                    body.angularDamping = 0.2f;
                    piece.SetActive(false);
                    planks.Add(body);
                }
            }

            return planks.ToArray();
        }

        // The mesh helper deletes and recreates an asset file the first time a build saves to it.
        private static string FreshAsset(string assetName)
        {
            VillageAssets.SavedThisBuild.Remove($"{VillageAssets.ModelFolder}/{assetName}.asset");
            return assetName;
        }

        private static void SetArray(SerializedProperty list, Object[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
