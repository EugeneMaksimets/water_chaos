using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterChaos.EditorTools.MiniVillage;

namespace WaterChaos.EditorTools.OfficeDistrict
{
    /// <summary>
    /// Puts the toxic liquid factory compound into the Mini Village (Water Chaos → Add Toxic Factory To Mini Village):
    /// south of the gas station, gate towards the village. Everything else in the scene is kept as it is; only
    /// decoration (trees, rocks) standing on the new yard is removed. Adds the hall's temperature zone, its fire
    /// and axes (by the player and at the gate). Safe to run again: the previous compound is replaced.
    /// Needs the district prefabs (Tools → Water Chaos → Build Office District) and the breakable doors.
    /// </summary>
    public static class VillageFactorySetup
    {
        private const string RootName = "ToxicFactory";
        private const string HallPath = VillageAssets.PrefabFolder + "/" + DistrictKit.BuildingFolder + "/" + ToxicFactory.Name + ".prefab";
        private static readonly Vector3 Position = new Vector3(2f, 0f, -50f);

        [MenuItem("Water Chaos/Add Toxic Factory To Mini Village")]
        public static void Setup()
        {
            var hall = AssetDatabase.LoadAssetAtPath<GameObject>(HallPath);
            if (hall == null)
            {
                Debug.LogError("Toxic factory: " + HallPath + " is missing; run Tools → Water Chaos → Build Office District first.");
                return;
            }

            Scene scene = OpenVillage();
            if (!scene.IsValid())
                return;

            VillagePalette.Prepare();
            DistrictKit.Prepare();

            GameObject old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (old != null)
                Object.DestroyImmediate(old);
            foreach (string stale in new[] { "Gameplay/RoomTemperatureObjects/Toxic Works · Hall", "Gameplay/FireObjects/Cartoon Fire (Factory Hall)", "Gameplay/Items" })
            {
                GameObject found = Find(scene, stale);
                if (found != null)
                    Object.DestroyImmediate(found);
            }

            var bounds = new Bounds(Position, new Vector3(ToxicFactory.CompoundSize.x + 4f, 100f, ToxicFactory.CompoundSize.y + 4f));
            List<string> removed = ClearDecoration(scene, bounds);

            var root = new GameObject(RootName).transform;
            root.SetPositionAndRotation(Position, Quaternion.identity);
            Transform gameplay = scene.GetRootGameObjects().First(g => g.name == "Gameplay").transform;
            Transform items = DistrictKit.Group(gameplay, "Items");

            ToxicFactory.BuildCompound(root, hall, new ToxicFactory.GameplayParents
            {
                Interiors = root,
                Rooms = gameplay.Find("RoomTemperatureObjects"),
                Fires = gameplay.Find("FireObjects"),
                Items = items,
            });

            PlaceAxeByPlayer(gameplay, items);

            EditorSceneManager.MarkSceneDirty(scene);
            if (Application.isBatchMode)
                EditorSceneManager.SaveScene(scene);
            Debug.Log($"Toxic factory: added to {scene.name} at {Position}; removed decoration on the yard: {string.Join(", ", removed)}." +
                      (Application.isBatchMode ? "" : " Save the scene."));
        }

        private static Scene OpenVillage()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path == MiniVillageBuilder.ScenePath)
                return active;

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return default;
            return EditorSceneManager.OpenScene(MiniVillageBuilder.ScenePath, OpenSceneMode.Single);
        }

        /// <summary>Removes decoration pieces (trees, rocks, flowers) whose position is on the new yard.</summary>
        private static List<string> ClearDecoration(Scene scene, Bounds area)
        {
            var removed = new List<string>();
            GameObject village = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MiniVillage");
            Transform decoration = village != null ? village.transform.Find("EnvironmentDecoration") : null;
            if (decoration == null)
                return removed;

            foreach (Transform group in decoration.Cast<Transform>().ToArray())
            {
                foreach (Transform piece in group.Cast<Transform>().ToArray())
                {
                    Vector3 p = piece.position;
                    if (area.Contains(new Vector3(p.x, area.center.y, p.z)))
                    {
                        removed.Add(piece.name);
                        Object.DestroyImmediate(piece.gameObject);
                    }
                }
            }

            return removed;
        }

        /// <summary>An axe on the ground in front of the player's start, so it can be tried at once.</summary>
        private static void PlaceAxeByPlayer(Transform gameplay, Transform items)
        {
            Transform player = gameplay.Find("Player");
            if (player == null)
                return;

            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            Vector3 spot = player.position + forward * 1.6f + Vector3.Cross(Vector3.up, forward) * 0.8f;
            float ground = Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : 0.14f;
            DistrictKit.PlaceAxe(items, new Vector3(spot.x, ground + 0.08f, spot.z), player.eulerAngles.y + 30f);
        }

        private static GameObject Find(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == parts[0]);
            if (root == null)
                return null;
            Transform child = root.transform.Find(string.Join("/", parts.Skip(1)));
            return child != null ? child.gameObject : null;
        }
    }
}
