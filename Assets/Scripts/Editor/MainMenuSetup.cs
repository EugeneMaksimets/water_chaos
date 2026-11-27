using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WaterChaos.FireTruck;
using WaterChaos.Interaction;
using WaterChaos.Mission;
using WaterChaos.Player;
using WaterChaos.UI;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// Builds the main menu scene (a burning gas station, a firefighter calmly refuelling the fire truck, the Start button)
    /// and sets up the arrival intro in the Mini Village scene (flashing beacons, getting out of the truck, then the mission).
    /// Both commands can be run again: they replace what they built before.
    /// </summary>
    internal static class MainMenuSetup
    {
        private const string MenuScenePath = "Assets/WaterChaos/Scenes/WaterChaos_MainMenu.unity";
        private const string GameScenePath = "Assets/WaterChaos/Scenes/WaterChaos_MiniVillage.unity";
        private const string IntroName = "Mission Intro";
        private const string IntroExitName = "Intro Exit Point";
        private const string BeaconsName = "Beacons";

        private const string EnvPrefabs = "Assets/WaterChaos/Environment/Prefabs/";
        private const string EnvMaterials = "Assets/WaterChaos/Environment/Materials/";
        private const string BeaconMaterials = "Assets/WaterChaos/Materials/";
        private const string FirePrefab = "Assets/WaterChaos/Prefabs/Cartoon Fire.prefab";
        private const string FirefighterPrefab = "Assets/WaterChaos/Prefabs/Firefighter.prefab";

        [MenuItem("Water Chaos/Build Main Menu Scene")]
        public static void BuildMenuScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Transform station = new GameObject("Gas Station").transform;
            BuildGround(station);
            Transform canopy = Place(EnvPrefabs + "GasStation/GasStation_Canopy.prefab", station, new Vector3(0f, 0f, 0f), 0f);
            foreach (float x in new[] { -4f, 4f })
            {
                Place(EnvPrefabs + "GasStation/FuelIsland.prefab", station, new Vector3(x, 0f, 0f), 0f);
                Place(EnvPrefabs + "GasStation/FuelDispenser.prefab", station, new Vector3(x, 0.2f, 0f), 90f);
            }
            Transform shop = Place(EnvPrefabs + "Buildings/GasStation_Shop.prefab", station, new Vector3(-4f, 0f, -12f), 180f);
            Place(EnvPrefabs + "GasStation/GasStation_PriceSign.prefab", station, new Vector3(9f, 0f, 5f), 0f);
            Place(EnvPrefabs + "GasStation/GasCanister.prefab", station, new Vector3(1.4f, 0f, -1.6f), 20f);
            Place(EnvPrefabs + "GasStation/TireStack.prefab", station, new Vector3(-11f, 0f, -5f), 0f);

            // The truck stands at the left island; the firefighter fills it up and ignores the fire behind him.
            Transform truck = Place(EnvPrefabs + "Vehicles/FireTruck.prefab", station, new Vector3(-7.3f, 0f, 0.6f), 180f);
            AddBeacons(truck);
            Transform firefighter = Place(FirefighterPrefab, station, new Vector3(-5.35f, 0f, -0.9f), -90f);
            BuildFuelHose(station, new Vector3(-4.35f, 1.25f, 0.25f), firefighter, new Vector3(-6.03f, 1.05f, -0.9f));

            BuildFires(station, shop, canopy);
            Transform lookAt = new GameObject("Camera Target").transform;
            lookAt.position = new Vector3(-5f, 2f, -2.5f);
            BuildMenuCamera(lookAt);
            BuildMenuLighting();
            BuildEventSystem();
            new GameObject("Main Menu").AddComponent<MainMenuView>();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            SetBuildScenes();
            Debug.Log($"Main menu: scene saved to {MenuScenePath}; it is the first scene in the build.");
        }

        [MenuItem("Water Chaos/Setup Mission Intro (Mini Village)")]
        public static void SetupIntro()
        {
            MissionRunner runner = Object.FindAnyObjectByType<MissionRunner>(FindObjectsInactive.Include);
            FireTruckController truck = Object.FindAnyObjectByType<FireTruckController>(FindObjectsInactive.Include);
            PlayerInputHandler input = Object.FindAnyObjectByType<PlayerInputHandler>(FindObjectsInactive.Include);
            if (runner == null || truck == null || input == null)
            {
                Debug.LogError("Mission intro: open the Mini Village scene with the mission set up (MissionRunner, fire truck, player).");
                return;
            }

            foreach (GameObject old in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                         .Where(t => t.parent == null && (t.name == IntroName || t.name == IntroExitName))
                         .Select(t => t.gameObject).ToList())
                Undo.DestroyObjectImmediate(old);

            Transform player = input.transform;
            Camera camera = player.GetComponentInChildren<Camera>(true);
            FireTruckBeacons beacons = AddBeacons(truck.transform);

            // The player gets out by the driver door (truck's left side), keeping the spawn facing.
            Transform exit = new GameObject(IntroExitName).transform;
            Undo.RegisterCreatedObjectUndo(exit.gameObject, "Setup Mission Intro");
            Vector3 door = truck.transform.TransformPoint(new Vector3(-2.3f, 0f, -2.8f));
            float playerHeight = player.position.y - GroundHeight(player.position, player, truck.transform);
            exit.SetPositionAndRotation(new Vector3(door.x, GroundHeight(door, player, truck.transform) + playerHeight, door.z),
                Quaternion.Euler(0f, player.eulerAngles.y, 0f));

            GameObject intro = new GameObject(IntroName);
            Undo.RegisterCreatedObjectUndo(intro, "Setup Mission Intro");
            MissionIntro component = intro.AddComponent<MissionIntro>();
            var data = new SerializedObject(component);
            data.FindProperty("_runner").objectReferenceValue = runner;
            data.FindProperty("_truck").objectReferenceValue = truck.transform;
            data.FindProperty("_beacons").objectReferenceValue = beacons;
            data.FindProperty("_camera").objectReferenceValue = camera != null ? camera.transform : null;
            data.FindProperty("_exitPoint").objectReferenceValue = exit;
            Behaviour[] controls = new Behaviour[] { input, player.GetComponent<PlayerLook>(), player.GetComponent<PlayerMovement>() }
                .Where(b => b != null).ToArray();
            SerializedProperty list = data.FindProperty("_playerControls");
            list.arraySize = controls.Length;
            for (int i = 0; i < controls.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = controls[i];
            data.ApplyModifiedProperties();

            var runnerData = new SerializedObject(runner);
            runnerData.FindProperty("_startOnPlay").boolValue = false;
            runnerData.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(intro.scene);
            Selection.activeGameObject = intro;
            Debug.Log($"Mission intro set up (camera: {(camera != null ? camera.name : "NOT FOUND")}); "
                + "the mission now starts after the intro. Save the scene (Cmd+S).", intro);
        }

        /// <summary>Batch mode entry: builds the menu scene, then sets up the intro in Mini Village and saves it.</summary>
        public static void BuildAllBatch()
        {
            BuildMenuScene();
            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            SetupIntro();
            EditorSceneManager.SaveScene(game);
            AssetDatabase.SaveAssets();
        }

        // ───────────── Beacons ─────────────

        /// <summary>Red and blue light bar on the cab roof (truck local space: front -Z, roof top at 3.32 m).</summary>
        private static FireTruckBeacons AddBeacons(Transform truck)
        {
            Transform old = truck.Find(BeaconsName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            FireTruckBeacons oldComponent = truck.GetComponent<FireTruckBeacons>();
            if (oldComponent != null)
                Undo.DestroyObjectImmediate(oldComponent);

            Transform bar = new GameObject(BeaconsName).transform;
            Undo.RegisterCreatedObjectUndo(bar.gameObject, "Add Beacons");
            bar.SetParent(truck, false);
            bar.localPosition = new Vector3(0f, 3.32f, -3.4f);

            Color red = new Color(1f, 0.08f, 0.05f);
            Color blue = new Color(0.1f, 0.35f, 1f);
            CreateLens(bar, "Bar Base", new Vector3(0f, 0.06f, 0f), new Vector3(2f, 0.12f, 0.32f), Material("M_BeaconBase", new Color(0.12f, 0.12f, 0.14f)));
            var reds = new List<GameObject>();
            var blues = new List<GameObject>();
            foreach (float side in new[] { -1f, 1f })
            {
                bool isRed = side < 0f;
                Color color = isRed ? red : blue;
                string id = isRed ? "Red" : "Blue";
                CreateLens(bar, $"{id} Lens", new Vector3(side * 0.55f, 0.18f, 0f), new Vector3(0.8f, 0.14f, 0.28f), Material($"M_Beacon{id}Off", color * 0.35f));

                GameObject lit = new GameObject($"{id} Lit");
                lit.transform.SetParent(bar, false);
                lit.transform.localPosition = new Vector3(side * 0.55f, 0.18f, 0f);
                CreateLens(lit.transform, "Glow", Vector3.zero, new Vector3(0.82f, 0.16f, 0.3f), Material($"M_Beacon{id}On", color * 1.6f));
                Light light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(lit.transform, false);
                light.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                light.type = LightType.Point;
                light.color = color;
                light.range = 10f;
                light.intensity = 4f;
                light.shadows = LightShadows.None;
                lit.SetActive(false);
                (isRed ? reds : blues).Add(lit);
            }

            FireTruckBeacons beacons = Undo.AddComponent<FireTruckBeacons>(truck.gameObject);
            var data = new SerializedObject(beacons);
            SetArray(data.FindProperty("_red"), reds);
            SetArray(data.FindProperty("_blue"), blues);
            data.ApplyModifiedProperties();
            return beacons;
        }

        private static void CreateLens(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lens.name = name;
            Object.DestroyImmediate(lens.GetComponent<Collider>());
            lens.transform.SetParent(parent, false);
            lens.transform.localPosition = position;
            lens.transform.localScale = size;
            MeshRenderer renderer = lens.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Unlit colour material in Assets/WaterChaos/Materials, created once.</summary>
        private static Material Material(string name, Color color)
        {
            string path = BeaconMaterials + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetArray(SerializedProperty property, List<GameObject> values)
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ───────────── Menu scene ─────────────

        private static void BuildGround(Transform parent)
        {
            GameObject forecourt = GameObject.CreatePrimitive(PrimitiveType.Plane);
            forecourt.name = "Forecourt";
            forecourt.transform.SetParent(parent, false);
            forecourt.transform.position = new Vector3(4f, 0f, -2f);
            forecourt.transform.localScale = new Vector3(5f, 1f, 4f);
            forecourt.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(EnvMaterials + "M_Concrete.mat");

            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Plane);
            road.name = "Road";
            road.transform.SetParent(parent, false);
            road.transform.position = new Vector3(4f, -0.01f, 0f);
            road.transform.localScale = new Vector3(30f, 1f, 30f);
            road.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(EnvMaterials + "M_Asphalt.mat");
        }

        private static Transform Place(string path, Transform parent, Vector3 position, float yaw)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"Main menu: prefab not found: {path}");
                return new GameObject(System.IO.Path.GetFileNameWithoutExtension(path) + " (missing)").transform;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return go.transform;
        }

        /// <summary>Black fuel hose from the dispenser, sagging to the firefighter's hands, into the truck's filler.</summary>
        private static void BuildFuelHose(Transform parent, Vector3 dispenser, Transform firefighter, Vector3 filler)
        {
            Vector3 hands = firefighter.position + firefighter.forward * 0.45f + Vector3.up * 1.05f;
            var points = new List<Vector3>();
            const int segments = 12;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 point = Vector3.Lerp(dispenser, hands, t);
                point.y -= Mathf.Sin(t * Mathf.PI) * 0.7f;
                points.Add(point);
            }
            points.Add(filler);

            LineRenderer line = new GameObject("Fuel Hose").AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            line.widthMultiplier = 0.05f;
            line.numCornerVertices = 2;
            line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(EnvMaterials + "M_Black.mat");
            line.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Fire on the shop roof and front, on the canopy and at the far island; spreading off (it is a backdrop).</summary>
        private static void BuildFires(Transform parent, Transform shop, Transform canopy)
        {
            Transform fires = new GameObject("Fires").transform;
            fires.SetParent(parent, false);

            Bounds shopBounds = Bounds(shop);
            Bounds canopyBounds = Bounds(canopy);
            var spots = new List<(Vector3 position, float scale)>
            {
                (new Vector3(shopBounds.min.x + 2f, shopBounds.max.y, shopBounds.center.z), 2.2f),
                (new Vector3(shopBounds.center.x, shopBounds.max.y, shopBounds.center.z + 1f), 2.6f),
                (new Vector3(shopBounds.max.x - 2f, shopBounds.max.y, shopBounds.center.z - 1f), 2f),
                (new Vector3(shopBounds.min.x + 1.5f, 0f, shopBounds.max.z + 0.6f), 1.6f),
                (new Vector3(canopyBounds.max.x - 1.5f, canopyBounds.max.y, canopyBounds.center.z), 1.8f),
                (new Vector3(4f, 0.2f, 1.2f), 1.4f),
            };

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefab);
            foreach ((Vector3 position, float scale) in spots)
            {
                var fire = (GameObject)PrefabUtility.InstantiatePrefab(prefab, fires);
                fire.transform.position = position;
                fire.transform.localScale = Vector3.one * scale;
                foreach (FireSpreader spreader in fire.GetComponentsInChildren<FireSpreader>(true))
                    spreader.enabled = false;
            }
        }

        private static Bounds Bounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(root.position, Vector3.one * 4f);

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer part in renderers)
                bounds.Encapsulate(part.bounds);
            return bounds;
        }

        private static void BuildMenuCamera(Transform lookAt)
        {
            GameObject go = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = go.AddComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.farClipPlane = 400f;
            go.AddComponent<AudioListener>();
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            go.transform.position = new Vector3(5f, 3f, 17f);
            go.transform.rotation = Quaternion.LookRotation(lookAt.position - go.transform.position);

            MenuCameraDrift drift = go.AddComponent<MenuCameraDrift>();
            var data = new SerializedObject(drift);
            data.FindProperty("_lookAt").objectReferenceValue = lookAt;
            data.ApplyModifiedProperties();
        }

        private static void BuildMenuLighting()
        {
            Light sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(14f, 200f, 0f);
            sun.color = new Color(1f, 0.8f, 0.62f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;

            Light glow = new GameObject("Fire Glow").AddComponent<Light>();
            glow.type = LightType.Point;
            glow.transform.position = new Vector3(-4f, 4f, -8f);
            glow.color = new Color(1f, 0.5f, 0.15f);
            glow.range = 20f;
            glow.intensity = 5f;

            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(EnvMaterials + "Sky_Dusk.mat");
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.55f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.48f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.35f, 0.25f, 0.25f);
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 160f;

            var volumeObject = new GameObject("Global Volume");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
        }

        private static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // Menu first (index 0 is loaded on start), then the game scene; other scenes keep their state.
        private static void SetBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != MenuScenePath && s.path != GameScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(GameScenePath, true));
            scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ───────────── Helpers ─────────────

        /// <summary>Height of the ground under a point, ignoring the player and the truck.</summary>
        private static float GroundHeight(Vector3 point, Transform player, Transform truck)
        {
            Physics.SyncTransforms();
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 3f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
            float best = point.y;
            float bestDistance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(player) || hit.transform.IsChildOf(truck) || hit.distance >= bestDistance)
                    continue;

                best = hit.point.y;
                bestDistance = hit.distance;
            }

            return best;
        }
    }
}
