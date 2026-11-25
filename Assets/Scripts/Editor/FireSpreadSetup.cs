using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Rooms;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One-click setup of fire spreading: settings asset, FireSpreader on the fire prefab and on every fire
    /// in the open scene, and the scene ground plus the floors inside rooms marked as flammable surfaces.
    /// Run "Setup Fire Layer" first so fires have their own layer. Safe to run again.
    /// Scene changes are undo-able; save the scene afterwards.
    /// </summary>
    public static class FireSpreadSetup
    {
        private const string FirePrefabPath = "Assets/WaterChaos/Prefabs/Cartoon Fire.prefab";
        private const string SettingsFolder = "Assets/WaterChaos/ScriptableObjects/Fire";
        private const string SettingsPath = SettingsFolder + "/FireSpreadSettings.asset";
        private const string GroundName = "Ground";
        // Floor tiles of the house kit are prefab instances named "Floor".
        private const string FloorTileName = "Floor";
        // How far above a floor's top surface the room check is made, in meters (m).
        private const float FloorProbeHeight = 0.05f;
        private const string OldTestCratesName = "Flammable Test Crates";

        [MenuItem("Water Chaos/Setup Fire Spread")]
        private static void Setup()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefabPath);
            if (prefab == null || prefab.GetComponent<FireExtinguishHandler>() == null)
            {
                Debug.LogError($"Fire spread setup: no fire prefab with FireExtinguishHandler at {FirePrefabPath}.");
                return;
            }

            FireSpreadSettings settings = GetOrCreateSettings(prefab.GetComponent<FireExtinguishHandler>());
            AddSpreaderToPrefab(settings);

            FireExtinguishHandler[] fires = Object.FindObjectsByType<FireExtinguishHandler>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (FireExtinguishHandler fire in fires)
                AddSpreader(fire.gameObject, settings, true);

            RemoveOldTestCrates();
            MarkGroundFlammable();
            int floors = MarkRoomFloorsFlammable();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeObject = settings;
            Debug.Log($"Fire spread setup: done, {fires.Length} scene fire(s) can spread over \"{GroundName}\" " +
                      $"and {floors} newly marked floor tile(s) inside rooms. Save the scene (Cmd+S).");
        }

        private static FireSpreadSettings GetOrCreateSettings(FireExtinguishHandler firePrefab)
        {
            FireSpreadSettings settings = AssetDatabase.LoadAssetAtPath<FireSpreadSettings>(SettingsPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder(SettingsFolder))
                    AssetDatabase.CreateFolder("Assets/WaterChaos/ScriptableObjects", "Fire");

                settings = ScriptableObject.CreateInstance<FireSpreadSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            SerializedObject data = new SerializedObject(settings);
            data.FindProperty("_firePrefab").objectReferenceValue = firePrefab;
            data.FindProperty("_targetMask").intValue = TargetMask();
            data.FindProperty("_fireMask").intValue = LayerBit("Fire", ~0);
            data.FindProperty("_waterMask").intValue = LayerBit("Water", 0);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>Everything except fires, water and the player: they are never burning targets or ground.</summary>
        private static int TargetMask()
        {
            return ~(LayerBit("Fire", 0) | LayerBit("Water", 0) | LayerBit("Player", 0) | LayerBit("Ignore Raycast", 0));
        }

        private static int LayerBit(string layerName, int fallback)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? 1 << layer : fallback;
        }

        private static void AddSpreaderToPrefab(FireSpreadSettings settings)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(FirePrefabPath);
            try
            {
                AddSpreader(root, settings, false);
                PrefabUtility.SaveAsPrefabAsset(root, FirePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddSpreader(GameObject fire, FireSpreadSettings settings, bool undo)
        {
            FireSpreader spreader = fire.GetComponent<FireSpreader>();
            if (spreader == null)
                spreader = undo ? Undo.AddComponent<FireSpreader>(fire) : fire.AddComponent<FireSpreader>();

            SerializedObject data = new SerializedObject(spreader);
            data.FindProperty("_settings").objectReferenceValue = settings;
            if (undo)
                data.ApplyModifiedProperties();
            else
                data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveOldTestCrates()
        {
            GameObject crates = GameObject.Find(OldTestCratesName);
            if (crates != null)
                Undo.DestroyObjectImmediate(crates);
        }

        private static void MarkGroundFlammable()
        {
            GameObject ground = GameObject.Find(GroundName);
            if (ground == null)
            {
                Debug.LogWarning($"Fire spread setup: no \"{GroundName}\" object; add FlammableSurface to the ground by hand.");
                return;
            }

            if (ground.GetComponent<FlammableSurface>() == null)
                Undo.AddComponent<FlammableSurface>(ground);
        }

        /// <summary>
        /// Fires inside a room look for flammable ground inside the same room; without it they can never spread.
        /// Marks every floor tile whose top lies inside a room. Returns how many tiles were newly marked.
        /// </summary>
        private static int MarkRoomFloorsFlammable()
        {
            RoomTemperature[] rooms = Object.FindObjectsByType<RoomTemperature>(FindObjectsSortMode.None);
            if (rooms.Length == 0)
                return 0;

            int marked = 0;
            Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            foreach (Collider part in colliders)
            {
                if (part.isTrigger)
                    continue;

                GameObject tile = PrefabUtility.GetOutermostPrefabInstanceRoot(part.gameObject);
                if (tile == null)
                    tile = part.gameObject;

                if (!tile.name.StartsWith(FloorTileName) || tile.GetComponentInParent<FlammableSurface>() != null)
                    continue;

                Bounds bounds = part.bounds;
                Vector3 top = new Vector3(bounds.center.x, bounds.max.y + FloorProbeHeight, bounds.center.z);
                if (!IsInsideAnyRoom(rooms, top))
                    continue;

                Undo.AddComponent<FlammableSurface>(tile);
                marked++;
            }

            return marked;
        }

        // RoomTemperature.Contains works only in Play Mode (it caches its box in Awake), so the box is read here.
        private static bool IsInsideAnyRoom(RoomTemperature[] rooms, Vector3 point)
        {
            foreach (RoomTemperature room in rooms)
            {
                if (!room.TryGetComponent(out BoxCollider box))
                    continue;

                Vector3 offset = room.transform.InverseTransformPoint(point) - box.center;
                if (RoomTemperatureModel.IsInsideBox(offset, box.size))
                    return true;
            }

            return false;
        }
    }
}
