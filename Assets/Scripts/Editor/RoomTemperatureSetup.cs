using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterChaos.Interaction;
using WaterChaos.Player;
using WaterChaos.Props;
using WaterChaos.Rooms;
using WaterChaos.UI;
using Object = UnityEngine.Object;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One-click setup of room temperature:
    /// a "Room" layer (rooms see fires, players and coolers, never each other),
    /// the room mask in FireSpreadSettings, a CoolingAppliance on the Fridge and AirConditioner prefabs,
    /// one trigger room per house room in the residential scene, and RoomVisitor + RoomTemperatureUI on the player.
    /// Runs once by itself when the residential scene is open without rooms; safe to run again
    /// (rooms are rebuilt, components reused). Save the scene afterwards.
    /// </summary>
    [InitializeOnLoad]
    public static class RoomTemperatureSetup
    {
        private const string RoomLayer = "Room";
        private const string FireLayer = "Fire";
        private const string RoomsRootName = "Rooms";
        private const string FridgePrefabPath = "Assets/WaterChaos/Prefabs/Fridge.prefab";
        private const string AirConditionerPrefabPath = "Assets/WaterChaos/Prefabs/AirConditioner.prefab";
        private const string SpreadSettingsPath = "Assets/WaterChaos/ScriptableObjects/Fire/FireSpreadSettings.asset";
        private const string AutoRunKey = "WaterChaos.RoomTemperatureSetup.AutoRan";
        private const int FirstUserLayer = 8;

        // Storey heights of the residential house, in meters (m): floor 1 from the ground to the second floor top.
        private const float Floor1Bottom = 0f;
        private const float Floor2Bottom = 3.04f;
        private const float RoofBottom = 6.04f;

        private struct RoomSpec
        {
            public string Name;
            public float MinX, MaxX, MinZ, MaxZ, MinY, MaxY;

            public RoomSpec(string name, float minX, float maxX, float minZ, float maxZ, float minY, float maxY)
            {
                Name = name;
                MinX = minX; MaxX = maxX;
                MinZ = minZ; MaxZ = maxZ;
                MinY = minY; MaxY = maxY;
            }
        }

        // Room layout from ResidentialEnvironmentBuilder (walls at x -6 / 0 / 4 / 8 and z 0 / 5 / 10).
        private static readonly RoomSpec[] Rooms =
        {
            new RoomSpec("Living Room", -6f, 0f, 0f, 5f, Floor1Bottom, Floor2Bottom),
            new RoomSpec("Dining Room", -6f, 0f, 5f, 10f, Floor1Bottom, Floor2Bottom),
            new RoomSpec("Entrance Hall", 0f, 4f, 0f, 10f, Floor1Bottom, Floor2Bottom),
            new RoomSpec("Office", 4f, 8f, 0f, 5f, Floor1Bottom, Floor2Bottom),
            new RoomSpec("Kitchen", 4f, 8f, 5f, 10f, Floor1Bottom, Floor2Bottom),
            new RoomSpec("Master Bedroom", -6f, 0f, 0f, 5f, Floor2Bottom, RoofBottom),
            new RoomSpec("Kids Room", -6f, 0f, 5f, 10f, Floor2Bottom, RoofBottom),
            new RoomSpec("Landing", 0f, 4f, 0f, 10f, Floor2Bottom, RoofBottom),
            new RoomSpec("Games Room", 4f, 8f, 0f, 5f, Floor2Bottom, RoofBottom),
            new RoomSpec("Bathroom", 4f, 8f, 5f, 10f, Floor2Bottom, RoofBottom),
        };

        static RoomTemperatureSetup()
        {
            EditorApplication.delayCall += AutoRun;
        }

        private static void AutoRun()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(AutoRunKey, false))
                return;

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ResidentialEnvironmentBuilder.ScenePath || FindRoot(scene, RoomsRootName) != null)
                return;

            SessionState.SetBool(AutoRunKey, true);
            Setup();
        }

        [MenuItem("Water Chaos/Setup Room Temperature")]
        public static void Setup()
        {
            int layer = GetOrCreateLayer(RoomLayer);
            if (layer < 0)
            {
                Debug.LogError("Room temperature setup: no free layer slot for \"Room\".");
                return;
            }

            SetupCollisions(layer);
            SetRoomMask(layer);

            EditPrefab(FridgePrefabPath, root => AddCooler(root, CoolingApplianceKind.Refrigerator, false));
            EditPrefab(AirConditionerPrefabPath, root => AddCooler(root, CoolingApplianceKind.AirConditioner, false));

            foreach (Fridge fridge in FindInScene<Fridge>())
                AddCooler(fridge.gameObject, CoolingApplianceKind.Refrigerator, true);
            foreach (AirConditioner airConditioner in FindInScene<AirConditioner>())
                AddCooler(airConditioner.gameObject, CoolingApplianceKind.AirConditioner, true);

            Scene scene = SceneManager.GetActiveScene();
            int rooms = scene.path == ResidentialEnvironmentBuilder.ScenePath ? BuildRooms(scene, layer) : 0;
            bool hud = SetupPlayer();

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            if (rooms == 0)
                Debug.LogWarning("Room temperature setup: rooms are only built in the residential scene. " +
                                 "Elsewhere add a RoomTemperature (with a BoxCollider) to a GameObject on the \"Room\" layer.");

            Debug.Log($"Room temperature setup: done. Layer {layer} \"{RoomLayer}\", {rooms} room(s), " +
                      $"HUD on player: {hud}. Save the scene (Cmd+S).");
        }

        // ───────────── Layer and physics ─────────────

        private static int GetOrCreateLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
                return existing;

            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = FirstUserLayer; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue))
                    continue;

                slot.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }

            return -1;
        }

        /// <summary>
        /// Rooms are triggers, so these pairs only produce trigger events, never physical contacts.
        /// Rooms ignore each other; the Fire layer (otherwise colliding with nothing) is seen by rooms.
        /// </summary>
        private static void SetupCollisions(int roomLayer)
        {
            for (int other = 0; other < 32; other++)
                Physics.IgnoreLayerCollision(roomLayer, other, other == roomLayer);

            int fireLayer = LayerMask.NameToLayer(FireLayer);
            if (fireLayer >= 0)
                Physics.IgnoreLayerCollision(roomLayer, fireLayer, false);

            // Also write the project setting, so the matrix survives an editor restart.
            SerializedObject physics = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);
            SerializedProperty matrix = physics.FindProperty("m_LayerCollisionMatrix");
            if (matrix == null || !matrix.isArray)
                return;

            uint roomBit = 1u << roomLayer;
            for (int i = 0; i < matrix.arraySize; i++)
            {
                SerializedProperty row = matrix.GetArrayElementAtIndex(i);
                uint value = (uint)row.longValue;
                row.longValue = i == roomLayer ? ~roomBit : value | roomBit;
            }

            physics.ApplyModifiedProperties();
        }

        private static void SetRoomMask(int layer)
        {
            FireSpreadSettings settings = AssetDatabase.LoadAssetAtPath<FireSpreadSettings>(SpreadSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning($"Room temperature setup: {SpreadSettingsPath} not found; fires keep the default room mask.");
                return;
            }

            SerializedObject data = new SerializedObject(settings);
            data.FindProperty("_roomMask").intValue = 1 << layer;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        // ───────────── Coolers ─────────────

        private static void AddCooler(GameObject root, CoolingApplianceKind kind, bool undo)
        {
            // Scene copies of the prefabs get the cooler from the prefab itself.
            if (undo && PrefabUtility.IsPartOfPrefabInstance(root))
                return;

            if (!root.TryGetComponent(out CoolingAppliance cooler))
                cooler = undo ? Undo.AddComponent<CoolingAppliance>(root) : root.AddComponent<CoolingAppliance>();

            SerializedObject data = new SerializedObject(cooler);
            data.FindProperty("_kind").enumValueIndex = (int)kind;
            data.FindProperty("_coolingPerSecond").floatValue = 1f;
            if (undo)
                data.ApplyModifiedProperties();
            else
                data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"Room temperature setup: prefab not found at {path}.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ───────────── Rooms ─────────────

        private static int BuildRooms(Scene scene, int layer)
        {
            GameObject old = FindRoot(scene, RoomsRootName);
            if (old != null)
                Undo.DestroyObjectImmediate(old);

            GameObject root = new GameObject(RoomsRootName);
            Undo.RegisterCreatedObjectUndo(root, "Setup Room Temperature");
            SceneManager.MoveGameObjectToScene(root, scene);

            foreach (RoomSpec spec in Rooms)
                CreateRoom(root.transform, spec, layer);

            return Rooms.Length;
        }

        private static void CreateRoom(Transform parent, RoomSpec spec, int layer)
        {
            GameObject room = new GameObject(spec.Name);
            room.layer = layer;
            room.transform.SetParent(parent, false);
            room.transform.position = new Vector3(
                (spec.MinX + spec.MaxX) * 0.5f, (spec.MinY + spec.MaxY) * 0.5f, (spec.MinZ + spec.MaxZ) * 0.5f);

            // RequireComponent adds the BoxCollider and Rigidbody.
            RoomTemperature temperature = room.AddComponent<RoomTemperature>();

            BoxCollider box = room.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = new Vector3(spec.MaxX - spec.MinX, spec.MaxY - spec.MinY, spec.MaxZ - spec.MinZ);

            Rigidbody body = room.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            SerializedObject data = new SerializedObject(temperature);
            data.FindProperty("_displayName").stringValue = spec.Name;
            data.FindProperty("_startTemperature").floatValue = 25f;
            data.FindProperty("_minTemperature").floatValue = -100f;
            data.FindProperty("_coolingLimit").floatValue = 100f;
            data.FindProperty("_initialFireHeat").floatValue = 5f;
            data.FindProperty("_heatInterval").floatValue = 5f;
            data.FindProperty("_heatPerInterval").floatValue = 1f;
            data.FindProperty("_overheatCooling").floatValue = 2.5f;
            data.FindProperty("_reigniteDelay").floatValue = 15f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // ───────────── Player ─────────────

        private static bool SetupPlayer()
        {
            PlayerInventory inventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (inventory == null)
            {
                Debug.LogWarning("Room temperature setup: no PlayerInventory in the scene; add RoomVisitor and RoomTemperatureUI to the player by hand.");
                return false;
            }

            GameObject player = inventory.gameObject;

            if (!player.TryGetComponent(out RoomVisitor visitor))
                visitor = Undo.AddComponent<RoomVisitor>(player);

            if (!player.TryGetComponent(out RoomTemperatureUI ui))
                ui = Undo.AddComponent<RoomTemperatureUI>(player);

            SerializedObject data = new SerializedObject(ui);
            data.FindProperty("_visitor").objectReferenceValue = visitor;
            data.ApplyModifiedProperties();
            return true;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == rootName)
                    return root;
            }

            return null;
        }

        private static T[] FindInScene<T>() where T : Component
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }
}
