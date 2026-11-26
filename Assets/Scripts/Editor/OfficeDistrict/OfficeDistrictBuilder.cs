using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WaterChaos.EditorTools.MiniVillage;
using WaterChaos.Interaction;
using WaterChaos.Rooms;
using WaterChaos.Water;

namespace WaterChaos.EditorTools.OfficeDistrict
{
    /// <summary>
    /// Builds the Office District scene (Tools → Water Chaos → Build Office District): an evening brick city block
    /// after the reference — the four-storey office, brick walk-ups with water towers, a street with sidewalks,
    /// lamps and parked cars, a small square, and the fenced toxic liquid factory with its open toxic tank.
    /// The gameplay rig (player, pumps, hoses, nozzle, splitter, fire truck, hydrant, HUD) is taken over from the
    /// Mini Village scene, so every system is wired exactly as there. Rebuilding overwrites the scene and the
    /// district prefabs; the Mini Village is not touched.
    /// </summary>
    public static class OfficeDistrictBuilder
    {
        public const string ScenePath = "Assets/WaterChaos/Scenes/WaterChaos_OfficeDistrict.unity";
        private const string FirePrefabPath = "Assets/WaterChaos/Prefabs/Cartoon Fire.prefab";
        private const string FridgePrefabPath = "Assets/WaterChaos/Prefabs/Fridge.prefab";
        private const string AirConditionerPath = "Assets/WaterChaos/Prefabs/AirConditioner.prefab";
        private const string HydrantPath = "Assets/Downloads/FireHydrant/Prefab/FireHydrant_Red.prefab";
        private const string TreePath = "Assets/Downloads/FireFighter/FirefightersPack/Models/Prefabs/Enviroment/";
        private const string SkyPath = "Assets/WaterChaos/Environment/Materials/Sky_Dusk.mat";
        private const string ToxicAssetsCheck = "Assets/WaterChaos/Prefabs/Toxic/CorrosionFumes.prefab";

        private const float Road = DistrictKit.Road;
        private const float Walk = DistrictKit.Walk;
        private const float Paving = DistrictKit.Paving;

        // Layout (world, m): street along X, buildings north of it, factory and square south of it.
        private static readonly Vector3 OfficePosition = new Vector3(-4f, 0f, 7f);
        /// <summary>Centre of the factory compound's yard (gate towards the street).</summary>
        private static readonly Vector3 CompoundPosition = new Vector3(12f, 0f, -22f);

        [MenuItem("Tools/Water Chaos/Build Office District")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ToxicAssetsCheck) == null)
                Debug.LogWarning("Office district: toxic liquid assets are missing; run Water Chaos/Setup Toxic Liquid first.");

            Random.InitState(67);
            VillageAssets.SavedThisBuild.Clear();
            VillagePalette.Prepare();
            VillageAssets.Prepare();
            DistrictKit.Prepare();

            DistrictProps.BuildAll();
            GameObject office = OfficeBuilding.Build();
            List<(BrickHouseInfo House, Vector3 Position)> houses = BuildHouses();
            GameObject hall = ToxicFactory.BuildHall();
            AssetDatabase.SaveAssets();

            Scene scene = CreateScene();
            if (!scene.IsValid())
                return;

            Transform district = new GameObject("OfficeDistrict").transform;
            Transform buildings = DistrictKit.Group(district, "Buildings");
            Transform interiors = DistrictKit.Group(district, "Interiors");
            Transform factory = DistrictKit.Group(district, "Factory");
            Transform street = DistrictKit.Group(district, "StreetProps");
            Transform decoration = DistrictKit.Group(district, "Decoration");
            Transform gameplay = FindRoot(scene, "Gameplay").transform;

            BuildGround(district);
            Transform officeInstance = PlaceBuilding(office, buildings, OfficePosition, 0f);
            OfficeBuilding.Furnish(Posed(interiors, "Office Furniture", officeInstance));
            foreach (var entry in houses)
            {
                Transform house = PlaceBuilding(entry.House.Prefab, buildings, entry.Position, 0f);
                if (entry.House.Spec.Accessible)
                    DistrictHouses.Furnish(entry.House, Posed(interiors, entry.House.Spec.Name + " Furniture", house));
            }

            BuildStreetProps(street);
            BuildDecoration(decoration);
            SetupGameplay(scene, gameplay, officeInstance, houses);

            factory.position = CompoundPosition;
            ToxicFactory.BuildCompound(factory, hall, new ToxicFactory.GameplayParents
            {
                Interiors = interiors,
                Rooms = gameplay.Find("RoomTemperatureObjects"),
                Fires = gameplay.Find("FireObjects"),
                Items = gameplay.Find("Items"),
            });
            SetupLighting(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Report(scene);
        }

        // ───────────── Prefabs ─────────────

        private static List<(BrickHouseInfo, Vector3)> BuildHouses()
        {
            var specs = new List<(BrickHouseSpec, Vector3)>
            {
                (new BrickHouseSpec
                {
                    Name = "Brownstone_Red", Width = 9f, Depth = 10f, Storeys = 4, Brick = "BrickRed", Accessible = true,
                    DoorU = 1.2f, PartitionX = 5f, LeftRoom = "Living Room", RightRoom = "Kitchen", WaterTower = true,
                }, new Vector3(-16f, 0f, 7f)),
                (new BrickHouseSpec
                {
                    Name = "Corner_Brown", Width = 10f, Depth = 10f, Storeys = 5, Brick = "BrickBrown", DoorU = 4.4f,
                    WaterTower = true, Stoop = true,
                }, new Vector3(-28.5f, 0f, 7f)),
                (new BrickHouseSpec
                {
                    Name = "Rowhouse_Gray", Width = 12f, Depth = 9f, Storeys = 3, Brick = "BrickGray", Accessible = true,
                    DoorU = 2.5f, PartitionX = 7f, PartitionDoor = true, LeftRoom = "Living Room", RightRoom = "Bedroom",
                }, new Vector3(19f, 0f, 7f)),
                (new BrickHouseSpec
                {
                    Name = "Brownstone_Small", Width = 7f, Depth = 9f, Storeys = 3, Brick = "Brick", DoorU = 2.9f, Stoop = true,
                }, new Vector3(33f, 0f, 7f)),
                (new BrickHouseSpec
                {
                    Name = "Tenement_Gray", Width = 14f, Depth = 10f, Storeys = 5, Brick = "BrickGray", DoorU = 6.4f,
                    WaterTower = true,
                }, new Vector3(-12f, 0f, 27f)),
                (new BrickHouseSpec
                {
                    Name = "Tenement_Brown", Width = 12f, Depth = 10f, Storeys = 4, Brick = "BrickBrown", DoorU = 5.4f,
                    WaterTower = true,
                }, new Vector3(19f, 0f, 26f)),
            };

            return specs.Select(s => (DistrictHouses.Build(s.Item1), s.Item2)).ToList();
        }

        // ───────────── Scene ─────────────

        /// <summary>Copy of the Mini Village with its environment removed: only the gameplay rig, UI and lighting stay.</summary>
        private static Scene CreateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(MiniVillageBuilder.ScenePath, ScenePath))
            {
                Debug.LogError("Office district: could not copy " + MiniVillageBuilder.ScenePath);
                return default;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            DestroyRoot(scene, "MiniVillage");

            Transform gameplay = FindRoot(scene, "Gameplay").transform;
            foreach (string path in new[] { "WindZone_Test", "WaterSources/Pond", "WaterSources/UndergroundTank", "WaterSources/Pool_Octagonal" })
            {
                Transform child = gameplay.Find(path);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }

            Transform rooms = gameplay.Find("RoomTemperatureObjects");
            if (rooms != null)
            {
                foreach (Transform room in rooms.Cast<Transform>().ToArray())
                    Object.DestroyImmediate(room.gameObject);
            }

            return scene;
        }

        private static Transform PlaceBuilding(GameObject prefab, Transform parent, Vector3 position, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return go.transform;
        }

        /// <summary>Empty group with the pose of <paramref name="pose"/>, so children use building space.</summary>
        private static Transform Posed(Transform parent, string name, Transform pose)
        {
            Transform group = DistrictKit.Group(parent, name);
            group.SetPositionAndRotation(pose.position, pose.rotation);
            return group;
        }

        // ───────────── Ground ─────────────

        private static void BuildGround(Transform district)
        {
            var ground = new PropModel();
            ground.Part(new Vector3(6f, -0.25f, -4f), new Vector3(140f, 0.5f, 96f), "Grass", true);
            DistrictKit.Bake(ground, "District_Ground", district).name = "Ground";

            var roads = new PropModel();
            const float x0 = -64f, x1 = 76f;
            float cx = (x0 + x1) * 0.5f, len = x1 - x0;
            roads.Part(new Vector3(cx, Road * 0.5f - 0.05f, -0.5f), new Vector3(len, Road + 0.1f, 8f), "Asphalt", true);
            for (float x = x0 + 2f; x < x1; x += 4f)
                roads.Mesh.Box(new Vector3(x, Road + 0.004f, -0.5f), new Vector3(2f, 0.01f, 0.15f), "RoadYellow");
            for (int i = 0; i < 7; i++)
                roads.Mesh.Box(new Vector3(-6.2f, Road + 0.004f, -4f + i * 1.15f), new Vector3(2.6f, 0.01f, 0.55f), "RoadWhite");

            Sidewalk(roads, cx, len, 3.5f, 7f, true);
            Sidewalk(roads, cx, len, -8f, -4.5f, false);

            // Square in the south-west, parking lot in the north-east (the factory brings its own yard).
            roads.Part(new Vector3(-24f, Paving * 0.5f, -21f), new Vector3(30f, Paving, 2.5f), "Sidewalk", true);
            roads.Part(new Vector3(-24f, Paving * 0.5f, -21f), new Vector3(2.5f, Paving, 22f), "Sidewalk", true);
            roads.Part(new Vector3(49f, Road * 0.5f, 15f), new Vector3(16f, Road, 16f), "Asphalt", true);
            for (int i = 0; i < 6; i++)
                roads.Mesh.Box(new Vector3(42.5f + i * 2.6f, Road + 0.004f, 18f), new Vector3(0.12f, 0.01f, 5f), "RoadWhite");

            DistrictKit.Bake(roads, "District_Roads", district).name = "Roads";
        }

        private static void Sidewalk(PropModel model, float cx, float length, float z0, float z1, bool curbAtMin)
        {
            model.Part(new Vector3(cx, Walk * 0.5f, (z0 + z1) * 0.5f), new Vector3(length, Walk, z1 - z0), "Sidewalk", true);
            float curb = curbAtMin ? z0 + 0.1f : z1 - 0.1f;
            model.Mesh.Box(new Vector3(cx, Walk * 0.5f + 0.005f, curb), new Vector3(length, Walk + 0.01f, 0.22f), "Curb");
        }

        // ───────────── Street ─────────────

        private static void BuildStreetProps(Transform street)
        {
            Transform lamps = DistrictKit.Group(street, "Street Lamps");
            foreach (float x in new[] { -48f, -34f, -21f, -8f, 9f, 24f, 38f, 52f })
                Lamp(lamps, new Vector3(x, Walk, 4.1f), 0f);
            foreach (float x in new[] { -40f, -26f, -12f, 2f, 16f, 30f, 44f })
                Lamp(lamps, new Vector3(x, Walk, -7.4f), 180f);

            Transform cars = DistrictKit.Group(street, "Cars");
            DistrictKit.Place(DistrictKit.Get("Street/Car_Red"), cars, new Vector3(-37f, Road, 2.2f), 90f);
            DistrictKit.Place(DistrictKit.Get("Street/Car_Blue"), cars, new Vector3(29f, Road, 2.2f), 90f);
            DistrictKit.Place(DistrictKit.Get("Street/Car_Yellow"), cars, new Vector3(-22f, Road, -3.2f), -90f);
            DistrictKit.Place(DistrictKit.Get("Street/Car_Blue"), cars, new Vector3(43.8f, Road, 18f), 0f);
            DistrictKit.Place(DistrictKit.Get("Street/Car_Red"), cars, new Vector3(49f, Road, 18f), 0f);
            DistrictKit.Place(DistrictKit.Get("Street/Car_Yellow"), cars, new Vector3(51.6f, Road, 12f), 180f);

            Transform furniture = DistrictKit.Group(street, "Street Furniture");
            DistrictKit.Furnish("Garden/Bench", furniture, new Vector3(13f, Walk, 6.4f), 180f);
            DistrictKit.Furnish("Garden/Bench", furniture, new Vector3(-30f, Walk, -7.6f), 0f);
            DistrictKit.Furnish("Garden/TrashBin_Green", furniture, new Vector3(-6.5f, Walk, 4.3f), 0f);
            DistrictKit.Furnish("Garden/TrashBin_Blue", furniture, new Vector3(17.2f, Walk, 4.3f), 0f);
            DistrictKit.Furnish("Garden/TrashBin_Green", furniture, new Vector3(-2f, Walk, -7.6f), 0f);
            DistrictKit.Furnish("OfficeDistrict/Props/StreetMailbox", furniture, new Vector3(6.5f, Walk, 4.4f), 180f, false);
            DistrictKit.Furnish("Pool/PatioTable", furniture, new Vector3(27f, Walk, 5.4f), 0f);
            DistrictKit.Furnish("Pool/PlasticChair", furniture, new Vector3(26.2f, Walk, 5.4f), 90f);
            DistrictKit.Furnish("Pool/PlasticChair", furniture, new Vector3(27.8f, Walk, 5.4f), -90f);
            DistrictKit.Furnish("Garden/FlowerPot", furniture, new Vector3(-14.2f, Walk, 6.5f), 0f);
            DistrictKit.Furnish("Garden/FlowerPot", furniture, new Vector3(22.8f, Walk, 6.5f), 0f);
            DistrictKit.Furnish("Garden/SidewalkSign", furniture, new Vector3(1.6f, Walk, 6f), 160f);

            Transform signs = DistrictKit.Group(street, "Road Signs");
            DistrictKit.Place(DistrictKit.Get("Street/RoadSign_Stop"), signs, new Vector3(40f, Walk, 4.3f), 180f);
            DistrictKit.Place(DistrictKit.Get("Street/RoadSign_Speed"), signs, new Vector3(-30f, Walk, -7.5f), 0f);
            DistrictKit.Place(DistrictKit.Get("Street/RoadSign_Warning"), signs, new Vector3(4.6f, Walk, -7.6f), 0f);
            DistrictKit.Place(DistrictKit.Get("Street/TrafficCone"), signs, new Vector3(7f, Road, -4f), 0f);
            DistrictKit.Place(DistrictKit.Get("Street/TrafficCone"), signs, new Vector3(11f, Road, -4f), 0f);
        }

        /// <summary>Street lamp with a warm point light under its head (the evening light of the reference).</summary>
        private static void Lamp(Transform parent, Vector3 position, float yaw)
        {
            GameObject lamp = DistrictKit.Place(DistrictKit.Get("Street/StreetLamp"), parent, position, yaw);
            if (lamp != null)
                DistrictKit.PointLight(lamp.transform, "Lamp Light", new Vector3(0f, 4.6f, -1.1f), 11f, 2.2f, new Color(1f, 0.78f, 0.5f));
        }

        private static void BuildDecoration(Transform decoration)
        {
            Transform trees = DistrictKit.Group(decoration, "Trees");
            var spots = new[]
            {
                new Vector2(-42f, 4.6f), new Vector2(-27f, 4.6f), new Vector2(31f, 4.6f), new Vector2(46f, 4.6f),
                new Vector2(-33f, -14f), new Vector2(-17f, -15f), new Vector2(-40f, -31f), new Vector2(-14f, -28f),
                new Vector2(-28f, -31f), new Vector2(-38f, -19f), new Vector2(-40f, 18f), new Vector2(38f, 24f),
                new Vector2(-2f, 30f), new Vector2(8f, 33f), new Vector2(35f, -18f), new Vector2(40f, -30f),
            };
            string[] kinds = { "Tree1", "Tree2", "Tree3" };
            for (int i = 0; i < spots.Length; i++)
                Tree(trees, kinds[i % kinds.Length], new Vector3(spots[i].x, 0f, spots[i].y), Random.Range(0.9f, 1.2f));

            Transform square = DistrictKit.Group(decoration, "Square");
            DistrictKit.Furnish("Garden/PicnicTable", square, new Vector3(-30f, Paving, -15.5f), 0f);
            DistrictKit.Furnish("Garden/PicnicTable", square, new Vector3(-18f, Paving, -26f), 30f);
            DistrictKit.Furnish("Garden/Bench", square, new Vector3(-24f, Paving, -18.6f), 180f);
            DistrictKit.Furnish("Garden/Bench", square, new Vector3(-21.5f, Paving, -23.5f), 90f);
            DistrictKit.Furnish("Garden/TrashBin_Blue", square, new Vector3(-26f, Paving, -19.6f), 0f);
            DistrictKit.Furnish("Garden/GardenGnome", square, new Vector3(-31f, 0f, -16f), 30f);
            foreach (Vector3 at in new[] { new Vector3(-36f, 0f, -12f), new Vector3(-12f, 0f, -12f), new Vector3(-36f, 0f, -31f) })
                DistrictKit.Place(AssetDatabase.LoadAssetAtPath<GameObject>(TreePath + "Bush1.prefab"), square, at, Random.Range(0f, 360f));
            for (float x = -38f; x < -9f; x += 2f)
                DistrictKit.Place(DistrictKit.Get("Street/Hedge"), square, new Vector3(x, 0f, -10.5f), 0f);
            DistrictKit.MarkStatic(trees.gameObject);
        }

        private static void Tree(Transform parent, string kind, Vector3 position, float scale)
        {
            GameObject tree = DistrictKit.Place(AssetDatabase.LoadAssetAtPath<GameObject>(TreePath + kind + ".prefab"), parent, position, Random.Range(0f, 360f));
            if (tree == null)
                return;

            tree.transform.localScale *= scale;
            Bounds bounds = DistrictKit.WorldBounds(tree);
            float height = Mathf.Max(bounds.size.y, 1f);
            var capsule = tree.AddComponent<CapsuleCollider>();
            Vector3 lossy = tree.transform.lossyScale;
            capsule.radius = 0.3f / Mathf.Max(lossy.x, 0.01f);
            capsule.height = height * 0.6f / Mathf.Max(lossy.y, 0.01f);
            capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
        }

        // ───────────── Gameplay ─────────────

        private static void SetupGameplay(Scene scene, Transform gameplay, Transform office,
            List<(BrickHouseInfo House, Vector3 Position)> houses)
        {
            Transform waterSources = gameplay.Find("WaterSources");
            SetupCistern(waterSources);

            // The truck goes back to where its compartments hold the stowed pump, nozzle and hose (by the spawn point).
            Transform truck = waterSources != null ? waterSources.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("FireTruck")) : null;
            if (truck != null)
                truck.SetPositionAndRotation(new Vector3(3f, Road, -1.75f), Quaternion.Euler(0f, -90f, 0f));

            GameObject player = gameplay.Cast<Transform>().Select(t => t.gameObject).FirstOrDefault(g => g.name == "Player");
            if (player != null)
                player.transform.SetPositionAndRotation(new Vector3(6f, Walk + 1.02f, -4.7f), Quaternion.Euler(0f, -20f, 0f));

            // Pump, the placed transfer hose and the splitter by the spawn point.
            Transform pumps = gameplay.Find("Pump");
            if (pumps != null)
            {
                Transform pump = pumps.Find("Pump");
                if (pump != null)
                    PlaceOnSurface(pump, new Vector3(10.5f, 0f, -6.4f), Walk);
            }

            Transform hoses = gameplay.Find("Hose");
            if (hoses != null && truck != null)
            {
                Transform loose = hoses.Cast<Transform>()
                    .OrderByDescending(h => Vector3.Distance(h.position, truck.position)).FirstOrDefault();
                if (loose != null)
                    PlaceOnSurface(loose, new Vector3(12f, 0f, -6.6f), Walk);
            }

            GameObject splitter = scene.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("HoseSplitter"));
            if (splitter != null)
            {
                splitter.transform.SetParent(gameplay, true);
                PlaceOnSurface(splitter.transform, new Vector3(13.4f, 0f, -6.4f), Walk);
            }

            Transform hydrants = gameplay.Find("Hydrant");
            var hydrant = AssetDatabase.LoadAssetAtPath<GameObject>(HydrantPath);
            if (hydrants != null && hydrant != null)
            {
                Transform main = hydrants.Cast<Transform>().FirstOrDefault();
                if (main != null)
                    main.SetPositionAndRotation(new Vector3(8.6f, Walk, -5.4f), main.rotation);
                PlaceAsset(hydrant, hydrants, new Vector3(-10f, Walk, 4.4f), 0f);
                PlaceAsset(hydrant, hydrants, new Vector3(36f, Walk, -5.6f), 180f);
            }

            Transform items = DistrictKit.Group(gameplay, "Items");
            DistrictKit.PlaceAxe(items, new Vector3(7.6f, Walk + 0.08f, -6.9f), 20f);
            DistrictKit.PlaceAxe(items, office.TransformPoint(new Vector3(6.6f, OfficeBuilding.GroundTop + 0.08f, 6.3f)), 70f);

            SetupFires(gameplay, office);
            SetupCoolers(scene, gameplay, office);
            SetupTemperature(gameplay, office, houses);
        }

        /// <summary>The rectangular pool reservoir stays a water source: a municipal cistern on the square.</summary>
        private static void SetupCistern(Transform waterSources)
        {
            Transform cistern = waterSources != null ? waterSources.Find("Pool_Rectangular") : null;
            if (cistern == null)
                return;

            cistern.name = "Water Cistern";
            cistern.SetPositionAndRotation(new Vector3(-31f, Paving, -27f), Quaternion.identity);
        }

        private static void SetupFires(Transform gameplay, Transform office)
        {
            Transform fires = gameplay.Find("FireObjects");
            if (fires == null)
                fires = DistrictKit.Group(gameplay, "FireObjects");

            Transform[] existing = fires.Cast<Transform>().ToArray();
            var spots = new List<(string Name, Vector3 Position)>
            {
                ("Cartoon Fire (Office Floor 1 · Meeting Room)", office.TransformPoint(new Vector3(14.6f, OfficeBuilding.FloorY(0) + 0.5f, 8.1f))),
                ("Cartoon Fire (Office Floor 3 · Lounge)", office.TransformPoint(new Vector3(14.3f, OfficeBuilding.FloorY(2) + 0.5f, 8.2f))),
            };

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefabPath);
            for (int i = 0; i < spots.Count; i++)
            {
                GameObject fire = i < existing.Length ? existing[i].gameObject
                    : prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, fires) : null;
                if (fire == null)
                    continue;
                fire.name = spots[i].Name;
                fire.transform.SetParent(fires, true);
                fire.transform.position = spots[i].Position;
            }
        }

        /// <summary>The scene's wall air conditioners go to the office floors (one cooler per floor), a fridge to the break room.</summary>
        private static void SetupCoolers(Scene scene, Transform gameplay, Transform office)
        {
            Transform coolers = DistrictKit.Group(gameplay, "Coolers");
            List<GameObject> units = scene.GetRootGameObjects().Where(g => g.name.StartsWith("AirConditioner")).ToList();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AirConditionerPath);
            float[] alongWall = { 5f, 8f, 6.3f, 6.3f };
            for (int floor = 0; floor < OfficeBuilding.Floors; floor++)
            {
                GameObject unit = floor < units.Count ? units[floor]
                    : prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, coolers) : null;
                if (unit == null)
                    continue;

                unit.name = $"AirConditioner (Office Floor {floor + 1})";
                unit.transform.SetParent(coolers, true);
                Vector3 local = new Vector3(OfficeBuilding.Wall + 0.01f, OfficeBuilding.FloorY(floor) + 2.4f, alongWall[floor]);
                unit.transform.SetPositionAndRotation(office.TransformPoint(local), office.rotation * Quaternion.Euler(0f, 90f, 0f));
            }

            for (int i = OfficeBuilding.Floors; i < units.Count; i++)
                Object.DestroyImmediate(units[i]);

            var fridge = AssetDatabase.LoadAssetAtPath<GameObject>(FridgePrefabPath);
            if (fridge != null)
            {
                Transform instance = PlaceAsset(fridge, coolers, Vector3.zero, 0f).transform;
                instance.name = "Fridge (Office Floor 4 · Break Room)";
                instance.rotation = office.rotation;
                PlaceOnSurface(instance, office.TransformPoint(new Vector3(4.2f, 0f, 13.2f)), OfficeBuilding.FloorY(3));
            }
        }

        /// <summary>
        /// One temperature zone per floor, never per room: each office floor, each enterable house's ground floor
        /// get one <see cref="RoomTemperature"/> box covering every room of that floor (the factory adds its hall).
        /// </summary>
        private static void SetupTemperature(Transform gameplay, Transform office,
            List<(BrickHouseInfo House, Vector3 Position)> houses)
        {
            Transform rooms = gameplay.Find("RoomTemperatureObjects");
            if (rooms == null)
                rooms = DistrictKit.Group(gameplay, "RoomTemperatureObjects");

            for (int floor = 0; floor < OfficeBuilding.Floors; floor++)
                DistrictKit.Zone(rooms, $"Office · Floor {floor + 1}", office, OfficeBuilding.FloorBounds(floor));

            foreach (var entry in houses.Where(h => h.House.Spec.Accessible))
            {
                Transform house = new GameObject().transform;
                house.SetPositionAndRotation(entry.Position, Quaternion.identity);
                DistrictKit.Zone(rooms, $"{entry.House.Spec.Name.Replace('_', ' ')} · Ground Floor", house, entry.House.GroundFloor);
                Object.DestroyImmediate(house.gameObject);
            }
        }

        // ───────────── Lighting ─────────────

        /// <summary>Evening like the reference: low orange sun, deep blue ambient, purple haze, warm lamps.</summary>
        private static void SetupLighting(Scene scene)
        {
            Light sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(l => l.type == LightType.Directional && l.gameObject.scene == scene);
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(14f, -55f, 0f);
                sun.color = new Color(1f, 0.58f, 0.34f);
                sun.intensity = 0.75f;
                sun.shadows = LightShadows.Soft;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.24f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.30f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.11f, 0.13f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.34f, 0.25f, 0.32f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 160f;

            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            Shader procedural = Shader.Find("Skybox/Procedural");
            if (sky == null && procedural != null)
            {
                sky = new Material(procedural);
                sky.SetFloat("_SunSize", 0.05f);
                sky.SetFloat("_AtmosphereThickness", 1.7f);
                sky.SetColor("_SkyTint", new Color(0.36f, 0.26f, 0.48f));
                sky.SetColor("_GroundColor", new Color(0.13f, 0.1f, 0.13f));
                sky.SetFloat("_Exposure", 0.6f);
                AssetDatabase.CreateAsset(sky, SkyPath);
            }
            if (sky != null)
                RenderSettings.skybox = sky;
        }

        // ───────────── Helpers ─────────────

        private static GameObject PlaceAsset(GameObject prefab, Transform parent, Vector3 position, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        /// <summary>Moves an object so its render bounds sit centred on <paramref name="at"/> (x, z) on the surface height.</summary>
        private static void PlaceOnSurface(Transform item, Vector3 at, float surface)
        {
            item.position = new Vector3(at.x, item.position.y, at.z);
            Bounds bounds = DistrictKit.WorldBounds(item.gameObject);
            if (bounds.size == Vector3.zero)
            {
                item.position = new Vector3(at.x, surface, at.z);
                return;
            }

            item.position += new Vector3(at.x - bounds.center.x, surface - bounds.min.y + 0.01f, at.z - bounds.center.z);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (go == null)
                Debug.LogWarning("Office district: root object not found: " + name);
            return go;
        }

        private static void DestroyRoot(Scene scene, string name)
        {
            GameObject go = FindRoot(scene, name);
            if (go != null)
                Object.DestroyImmediate(go);
        }

        private static void Report(Scene scene)
        {
            int Count<T>() where T : Component =>
                Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(c => c.gameObject.scene == scene);

            string zones = string.Join(", ", Object.FindObjectsByType<RoomTemperature>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r.gameObject.scene == scene).Select(r => r.name));
            WaterReservoirComponent toxic = Object.FindObjectsByType<WaterReservoirComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(r => r.transform.parent != null && r.transform.parent.name == "ToxicTank");
            string toxicLiquid = toxic != null ? ((LiquidType)new SerializedObject(toxic).FindProperty("_liquid").intValue).ToString() : "missing";

            Debug.Log($"Office district: scene saved to {ScenePath}. Temperature zones: {zones}. " +
                      $"Flammable: {Count<FlammableObject>()}, water-pushable: {Count<WaterPushHandler>()}, " +
                      $"breakable windows: {Count<WaterBreakableGlass>()}, breakable doors: {Count<BreakableDoor>()}, " +
                      $"corrodible: {Count<CorrodibleObject>()}, rigid bodies: {Count<Rigidbody>()}, " +
                      $"lights: {Count<Light>()}, toxic tank liquid: {toxicLiquid}.");
        }
    }
}
