using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.EditorTools.MiniVillage;
using WaterChaos.Hydrant;
using WaterChaos.Items;
using WaterChaos.Pump;
using WaterChaos.Splitter;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One click builds the three-way hose splitter: a low-poly model (mesh asset), its materials and the prefab
    /// with all gameplay parts wired (inlet socket for the transfer hose, three nozzle outlets, pickup), places one next
    /// to the pump in the open scene, and gives every hydrant a transfer outlet so the transfer hose can start there too.
    /// Safe to run again: assets are rebuilt, an existing splitter in the scene is left alone. Save the scene afterwards.
    /// </summary>
    public static class HoseSplitterSetup
    {
        private const string ModelFolder = "Assets/WaterChaos/Models/Splitter";
        private const string MaterialFolder = "Assets/WaterChaos/Materials/Splitter";
        private const string PrefabPath = "Assets/WaterChaos/Prefabs/HoseSplitter.prefab";
        private const string Name = "HoseSplitter";

        // Material keys of the model.
        private const string Body = "Body";
        private const string Inlet = "Inlet";
        private const string Outlet = "Outlet";
        private const string Dark = "Dark";

        private static readonly float[] OutletAngles = { -35f, 0f, 35f };
        private static readonly float[] OutletOffsets = { -0.1f, 0f, 0.1f };

        // Layout in model space (meters, +Z = outlets, -Z = inlet).
        private const float ManifoldZ = 0.2f;
        private const float OutletPipeLength = 0.1f;
        private const float OutletCouplingLength = 0.05f;
        private const float InletEndZ = -0.28f;

        [MenuItem("Water Chaos/Build Hose Splitter")]
        private static void Build()
        {
            Directory.CreateDirectory(ModelFolder);
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();

            Mesh mesh = SaveMesh(out string[] materialKeys);
            GameObject prefab = SavePrefab(mesh, materialKeys);
            PlaceInScene(prefab);
            AddHydrantTransferOutlets();

            Debug.Log($"Hose splitter: built {PrefabPath}. Save the scene (Cmd+S).");
        }

        // ───────────── Model ─────────────

        private static Mesh SaveMesh(out string[] materialKeys)
        {
            LowPolyMesh model = new LowPolyMesh();

            // Main body: a short horizontal barrel with two dark bands and a flared front.
            Along(model, new Vector3(0f, 0f, -0.12f), Vector3.forward);
            model.Cylinder(Vector3.zero, 0.075f, 0.075f, 0.22f, 10, Body);
            model.Cylinder(new Vector3(0f, 0.05f, 0f), 0.082f, 0.082f, 0.015f, 10, Dark);
            model.Cylinder(new Vector3(0f, 0.15f, 0f), 0.082f, 0.082f, 0.015f, 10, Dark);
            model.Cylinder(new Vector3(0f, 0.22f, 0f), 0.075f, 0.09f, 0.04f, 10, Body);

            // Manifold the three outlets come out of.
            model.ResetTransform();
            model.Box(new Vector3(0f, 0f, 0.16f), new Vector3(0.3f, 0.1f, 0.08f), Body);

            // Inlet: pipe and a red coupling with two lugs, clearly different from the outlets.
            Along(model, new Vector3(0f, 0f, -0.12f), Vector3.back);
            model.Cylinder(Vector3.zero, 0.045f, 0.045f, 0.1f, 10, Body);
            model.Cylinder(new Vector3(0f, 0.1f, 0f), 0.062f, 0.062f, 0.06f, 10, Inlet);
            model.ResetTransform();
            model.Box(new Vector3(0.075f, 0f, -0.25f), new Vector3(0.03f, 0.03f, 0.04f), Inlet);
            model.Box(new Vector3(-0.075f, 0f, -0.25f), new Vector3(0.03f, 0.03f, 0.04f), Inlet);

            // Outlets: pipe, valve with a lever, brass coupling.
            for (int i = 0; i < OutletAngles.Length; i++)
            {
                GetOutlet(i, out Vector3 start, out Vector3 direction);
                Along(model, start, direction);
                model.Cylinder(Vector3.zero, 0.032f, 0.032f, OutletPipeLength, 8, Body);
                model.Sphere(new Vector3(0f, 0.045f, 0f), Vector3.one * 0.042f, 8, 5, Body);
                model.Cylinder(new Vector3(0f, OutletPipeLength, 0f), 0.048f, 0.048f, OutletCouplingLength, 8, Outlet);

                model.SetTransform(start + direction * 0.045f, Quaternion.LookRotation(direction));
                model.Box(new Vector3(0f, 0.055f, 0f), new Vector3(0.016f, 0.03f, 0.016f), Dark);
                model.Box(new Vector3(0f, 0.07f, 0.03f), new Vector3(0.018f, 0.014f, 0.08f), Dark);
            }

            // Feet, so it rests level on the ground.
            model.ResetTransform();
            model.Box(new Vector3(0f, -0.09f, -0.06f), new Vector3(0.16f, 0.03f, 0.04f), Dark);
            model.Box(new Vector3(0f, -0.075f, 0.16f), new Vector3(0.26f, 0.06f, 0.04f), Dark);

            Mesh mesh = model.ToMesh(Name, out materialKeys);
            string path = $"{ModelFolder}/{Name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>The following solids stand on <paramref name="start"/> and grow along <paramref name="direction"/>.</summary>
        private static void Along(LowPolyMesh model, Vector3 start, Vector3 direction)
        {
            model.SetTransform(start, Quaternion.FromToRotation(Vector3.up, direction));
        }

        private static void GetOutlet(int index, out Vector3 start, out Vector3 direction)
        {
            direction = Quaternion.Euler(0f, OutletAngles[index], 0f) * Vector3.forward;
            start = new Vector3(OutletOffsets[index], 0f, ManifoldZ);
        }

        private static Material GetMaterial(string key)
        {
            string path = $"{MaterialFolder}/M_Splitter{key}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            switch (key)
            {
                case Inlet: Setup(material, new Color(0.78f, 0.16f, 0.12f), 0.35f, 0.2f); break;
                case Outlet: Setup(material, new Color(0.95f, 0.72f, 0.22f), 0.55f, 0.6f); break;
                case Dark: Setup(material, new Color(0.13f, 0.13f, 0.14f), 0.25f, 0.1f); break;
                default: Setup(material, new Color(0.42f, 0.46f, 0.5f), 0.45f, 0.5f); break;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Setup(Material material, Color color, float smoothness, float metallic)
        {
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
        }

        // ───────────── Prefab ─────────────

        private static GameObject SavePrefab(Mesh mesh, string[] materialKeys)
        {
            GameObject root = new GameObject(Name);

            Material[] materials = new Material[materialKeys.Length];
            for (int i = 0; i < materialKeys.Length; i++)
                materials[i] = GetMaterial(materialKeys[i]);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = materials;

            AddBox(root, new Vector3(0f, -0.015f, -0.03f), new Vector3(0.17f, 0.18f, 0.38f));
            AddBox(root, new Vector3(0f, 0f, 0.16f), new Vector3(0.3f, 0.1f, 0.08f));

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 3f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // Inlet: the socket the transfer hose end plugs into.
            GameObject inlet = new GameObject("Inlet");
            inlet.transform.SetParent(root.transform, false);
            inlet.transform.localPosition = new Vector3(0f, 0f, InletEndZ);
            inlet.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            AddBox(inlet, new Vector3(0f, 0f, -0.04f), new Vector3(0.14f, 0.14f, 0.1f));
            HoseEndSocket socket = inlet.AddComponent<HoseEndSocket>();

            // Outlets: one connection point each, with the source that gives it its share.
            WaterConnectionPoint[] points = new WaterConnectionPoint[OutletAngles.Length];
            for (int i = 0; i < OutletAngles.Length; i++)
            {
                GetOutlet(i, out Vector3 start, out Vector3 direction);
                GameObject outlet = new GameObject("Outlet " + (i + 1));
                outlet.transform.SetParent(root.transform, false);
                outlet.transform.localPosition = start + direction * (OutletPipeLength + OutletCouplingLength);
                outlet.transform.localRotation = Quaternion.LookRotation(direction);
                AddBox(outlet, new Vector3(0f, 0f, -0.03f), new Vector3(0.11f, 0.11f, 0.08f));

                // Source first: the point looks for it when it wakes up.
                outlet.AddComponent<SplitterOutlet>();
                points[i] = outlet.AddComponent<WaterConnectionPoint>();
            }

            root.AddComponent<HoseSplitter>();

            // The item holder is turned 90° (like for the nozzle): -90° puts the outlets forward, the inlet toward the player.
            PickableItem item = root.AddComponent<PickableItem>();
            SerializedObject itemData = new SerializedObject(item);
            itemData.FindProperty("_heldPosition").vector3Value = Vector3.zero;
            itemData.FindProperty("_heldRotation").vector3Value = new Vector3(0f, -90f, 0f);
            itemData.FindProperty("_heldScale").floatValue = 1f;
            SerializedProperty heldParts = itemData.FindProperty("_disableWhileHeld");
            // In the hands nothing is connected: the nozzles and the transfer hose come off.
            heldParts.arraySize = points.Length + 1;
            for (int i = 0; i < points.Length; i++)
                heldParts.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            heldParts.GetArrayElementAtIndex(points.Length).objectReferenceValue = socket;
            itemData.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static void AddBox(GameObject target, Vector3 center, Vector3 size)
        {
            BoxCollider box = target.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        // ───────────── Scene ─────────────

        /// <summary>Puts one splitter on the ground next to the pump (once).</summary>
        private static void PlaceInScene(GameObject prefab)
        {
            if (Object.FindAnyObjectByType<HoseSplitter>(FindObjectsInactive.Include) != null)
            {
                Debug.Log("Hose splitter: the scene already has one; only the prefab was rebuilt.");
                return;
            }

            PumpController pump = Object.FindAnyObjectByType<PumpController>(FindObjectsInactive.Include);
            Vector3 position = pump != null ? pump.transform.position + pump.transform.right * 1.5f : Vector3.zero;
            Physics.SyncTransforms();
            if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 10f, ~0, QueryTriggerInteraction.Ignore))
                position = ground.point + Vector3.up * 0.15f;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Place Hose Splitter");
            instance.transform.position = position;

            EditorSceneManager.MarkSceneDirty(instance.scene);
            Selection.activeGameObject = instance;
        }

        /// <summary>
        /// Every hydrant in the open scene gets a transfer outlet (once), so the transfer hose can start at it like at a pump.
        /// </summary>
        private static void AddHydrantTransferOutlets()
        {
            int added = 0;
            foreach (HydrantController hydrant in Object.FindObjectsByType<HydrantController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hydrant.GetComponentInChildren<TransferConnectionPoint>(true) != null)
                    continue;

                // Next to the nozzle's connection point: the player aims at the same collider for both hoses.
                WaterConnectionPoint outlet = hydrant.GetComponentInChildren<WaterConnectionPoint>(true);
                Undo.AddComponent<TransferConnectionPoint>(outlet != null ? outlet.gameObject : hydrant.gameObject);
                EditorSceneManager.MarkSceneDirty(hydrant.gameObject.scene);
                added++;
            }

            Debug.Log($"Hose splitter: transfer outlets added to {added} hydrant(s).");
        }
    }
}
