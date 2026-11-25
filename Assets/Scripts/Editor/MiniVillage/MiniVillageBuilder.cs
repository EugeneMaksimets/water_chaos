using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WaterChaos.Player;
using WaterChaos.Rooms;
using WaterChaos.UI;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.EditorTools.MiniVillage
{
    /// <summary>
    /// One click builds the stylized mini village: generated low-poly models, materials, push profiles,
    /// prefabs (Assets/WaterChaos/Environment) and the scene WaterChaos_MiniVillage.
    /// Gameplay objects (player, UI, pumps, hoses, nozzle, hydrant, fire, reservoirs) come from a copy of
    /// SampleScene, so their references stay intact; the two test tanks become the swimming pools and the
    /// lake becomes the pond. SampleScene and the other scenes are never modified.
    /// Re-running rebuilds the assets and the scene from scratch.
    /// </summary>
    public static class MiniVillageBuilder
    {
        public const string ScenePath = "Assets/WaterChaos/Scenes/WaterChaos_MiniVillage.unity";
        private const string SourceScenePath = "Assets/Scenes/SampleScene.unity";
        private const string FirePrefabPath = "Assets/WaterChaos/Prefabs/Cartoon Fire.prefab";
        private const string FridgePrefabPath = "Assets/WaterChaos/Prefabs/Fridge.prefab";
        private const string TransferHosePrefabPath = "Assets/WaterChaos/Prefabs/WaterTransfer/TransferHose_20m 1.prefab";
        private const string TreePath = "Assets/Downloads/FireFighter/FirefightersPack/Models/Prefabs/Enviroment/";

        // Surface heights in meters (m).
        private const float Road = 0.03f;
        private const float Walk = 0.14f;
        private const float Paving = 0.05f;
        private const float Forecourt = 0.04f;
        private const float Floor = VillageBuildings.FloorTop;

        private static Transform _interactive;

        [MenuItem("Tools/Water Chaos/Build Mini Village")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Random.InitState(2024);
            VillageAssets.SavedThisBuild.Clear();
            VillagePalette.Prepare();
            VillageAssets.Prepare();
            VillageProps.BuildAll();
            VillageBuildings.BuildWindowsAndDoors();
            Dictionary<string, HouseInfo> houses = BuildBuildingPrefabs();
            GameObject canopy = VillageBuildings.BuildCanopy();
            GameObject priceSign = VillageBuildings.BuildPriceSign();
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
            {
                Debug.LogError("Mini village: could not copy " + SourceScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (string name in new[] { "House", "Sphere", "ff", "Push" })
                Remove(scene, name);

            Transform village = new GameObject("MiniVillage").transform;
            Transform gameplay = new GameObject("Gameplay").transform;
            Transform lighting = new GameObject("Lighting").transform;
            Transform ui = new GameObject("UI").transform;

            Transform terrain = Group(village, "Terrain");
            Transform roads = Group(village, "Roads");
            Transform residential = Group(village, "ResidentialArea");
            Transform gasStation = Group(village, "GasStation");
            Transform poolArea = Group(village, "SwimmingPoolArea");
            Transform decoration = Group(village, "EnvironmentDecoration");
            Transform statics = Group(village, "StaticEnvironment");
            _interactive = Group(village, "InteractiveEnvironment");

            BuildTerrain(scene, terrain);
            BuildRoads(roads);
            Dictionary<string, Transform> placed = PlaceBuildings(houses, residential, gasStation, poolArea);
            BuildResidentialYards(statics, decoration);
            BuildGasStation(gasStation, canopy, priceSign);
            BuildStreetFurniture(statics);
            BuildTrees(decoration);
            BuildDecor(decoration);
            FurnishInteriors(houses, placed);
            SetupGameplay(scene, gameplay, poolArea, houses, placed);
            SetupLighting(scene, lighting);
            Move(FindRoot(scene, "Canvas"), ui, null);
            Move(FindRoot(scene, "EventSystem"), ui, null);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Mini village: scene saved to {ScenePath} ({VillageProps.Names.Count()} prop prefabs, {houses.Count} buildings).");
        }

        // ───────────── Buildings ─────────────

        private static Dictionary<string, HouseInfo> BuildBuildingPrefabs()
        {
            var specs = new[]
            {
                new HouseSpec
                {
                    Name = "House_TwoStorey_Beige", Width = 10f, Depth = 8f, Floors = 2, Exterior = "SidingBeige",
                    InteriorRight = "InteriorWallBlue", Roof = "Roof", Window = "Window_House", Door = "Door_Wood",
                    Porch = PorchKind.Full, Chimney = true, RoofHeight = 2.6f,
                },
                new HouseSpec
                {
                    Name = "House_TwoStorey_Blue", Width = 10f, Depth = 8f, Floors = 2, Exterior = "SidingBlue",
                    InteriorLeft = "InteriorWallBlue", Roof = "RoofDark", Window = "Window_HouseGreen", Door = "Door_Red",
                    Porch = PorchKind.Full, Chimney = true, RoofHeight = 2.6f,
                },
                new HouseSpec
                {
                    Name = "House_Bungalow", Width = 11f, Depth = 7.5f, Floors = 1, Exterior = "SidingCream",
                    Roof = "RoofDark", Window = "Window_House", Door = "Door_Wood", Porch = PorchKind.Small,
                    Chimney = true, RoofHeight = 2.2f, RightRoom = "Bedroom",
                },
                new HouseSpec
                {
                    Name = "House_Brick", Width = 9f, Depth = 8f, Floors = 2, Exterior = "Brick",
                    Roof = "RoofDark", Window = "Window_Plain", Door = "Door_Red", Porch = PorchKind.Canopy,
                    RoofHeight = 2.4f,
                },
                new HouseSpec
                {
                    Name = "GasStation_Shop", Width = 12f, Depth = 7f, Floors = 1, StoreyHeight = 3.6f,
                    Exterior = "GasWhite", Roof = "RoofDark", Window = "Window_Plain", Door = "Door_Shop",
                    FlatRoof = true, Wainscot = false, Partition = -1f, LeftRoom = "Quick Mart", ShopFront = true,
                    Sign = "QUICK MART",
                },
                new HouseSpec
                {
                    Name = "PoolHouse", Width = 4.5f, Depth = 3.5f, Floors = 1, StoreyHeight = 2.8f,
                    Exterior = "SidingMint", Roof = "RoofRed", Window = "Window_Plain", Door = "Door_Wood",
                    FlatRoof = true, Wainscot = false, Partition = -1f, LeftRoom = "Pool House",
                },
            };

            var result = new Dictionary<string, HouseInfo>();
            foreach (HouseSpec spec in specs)
                result[spec.Name] = VillageBuildings.BuildHouse(spec);
            return result;
        }

        private static Dictionary<string, Transform> PlaceBuildings(Dictionary<string, HouseInfo> houses,
            Transform residential, Transform gasStation, Transform poolArea)
        {
            var placed = new Dictionary<string, Transform>
            {
                ["Beige House"] = Instance(houses["House_TwoStorey_Beige"].Prefab, residential, new Vector3(-30f, 0f, 12f), 0f).transform,
                ["Bungalow"] = Instance(houses["House_Bungalow"].Prefab, residential, new Vector3(-14f, 0f, 12f), 0f).transform,
                ["Brick House"] = Instance(houses["House_Brick"].Prefab, residential, new Vector3(1f, 0f, 12f), 0f).transform,
                ["Blue House"] = Instance(houses["House_TwoStorey_Blue"].Prefab, residential, new Vector3(-30f, 0f, -12f), 180f).transform,
                ["Quick Mart"] = Instance(houses["GasStation_Shop"].Prefab, gasStation, new Vector3(34f, Forecourt, -24f), 180f).transform,
                ["Pool House"] = Instance(houses["PoolHouse"].Prefab, poolArea, new Vector3(45.5f, Paving, 14f), 90f).transform,
            };
            return placed;
        }

        private static string HouseKey(string placedName)
        {
            switch (placedName)
            {
                case "Beige House": return "House_TwoStorey_Beige";
                case "Bungalow": return "House_Bungalow";
                case "Brick House": return "House_Brick";
                case "Blue House": return "House_TwoStorey_Blue";
                case "Quick Mart": return "GasStation_Shop";
                default: return "PoolHouse";
            }
        }

        // ───────────── Terrain and roads ─────────────

        private static void BuildTerrain(Scene scene, Transform terrain)
        {
            GameObject ground = FindRoot(scene, "Ground");
            if (ground != null)
            {
                ground.GetComponent<Renderer>().sharedMaterial = VillagePalette.Get("Grass");
                Move(ground, terrain, null);
                MarkStatic(ground);
            }

            var hill = new LowPolyMesh();
            hill.Sphere(Vector3.zero, Vector3.one, 10, 6, "Hill");
            Mesh hillMesh = VillageAssets.SaveMesh(hill, "Hill", "Hill", out string[] hillMaterials);
            Material[] materials = VillagePalette.Get(hillMaterials);

            Transform hills = Group(terrain, "Hills");
            for (int i = 0; i < 15; i++)
            {
                float angle = i * 24f + Random.Range(-6f, 6f);
                float radius = Random.Range(82f, 96f);
                var position = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius, -2f, Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
                var scale = new Vector3(Random.Range(16f, 26f), Random.Range(6f, 11f), Random.Range(14f, 22f));
                Hill(hills, "Hill_" + i, hillMesh, materials, position, scale, Random.Range(0f, 360f));
            }

            // Gentle mounds inside the village, low enough to walk over.
            Hill(hills, "Mound_West", hillMesh, materials, new Vector3(-50f, -1.2f, 24f), new Vector3(9f, 3f, 8f), 20f);
            Hill(hills, "Mound_SouthWest", hillMesh, materials, new Vector3(-48f, -1.2f, -30f), new Vector3(8f, 2.8f, 9f), 70f);
            Hill(hills, "Mound_East", hillMesh, materials, new Vector3(58f, -1.2f, -30f), new Vector3(10f, 3.2f, 8f), 0f);
        }

        private static void Hill(Transform parent, string name, Mesh mesh, Material[] materials, Vector3 position, Vector3 scale, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            MarkStatic(go);
        }

        private static void BuildRoads(Transform roads)
        {
            var m = new PropModel();

            // Main road along X, Pool Lane north along Z.
            m.Part(new Vector3(0f, 0f, 0f), new Vector3(150f, Road * 2f, 7f), "Asphalt", true);
            m.Part(new Vector3(14f, 0f, 24.25f), new Vector3(6f, Road * 2f, 41.5f), "Asphalt", true);
            for (float x = -74f; x < 74f; x += 4f)
            {
                if (x > 9f && x < 18f)
                    continue;
                m.Part(new Vector3(x + 1f, Road + 0.003f, 0f), new Vector3(2f, 0.006f, 0.14f), "RoadYellow");
            }
            for (float z = 8f; z < 44f; z += 4f)
                m.Part(new Vector3(14f, Road + 0.003f, z + 1f), new Vector3(0.14f, 0.006f, 2f), "RoadYellow");
            m.Part(new Vector3(-32f, Road + 0.003f, 3.2f), new Vector3(86f, 0.006f, 0.1f), "RoadWhite");
            m.Part(new Vector3(46f, Road + 0.003f, 3.2f), new Vector3(58f, 0.006f, 0.1f), "RoadWhite");
            m.Part(new Vector3(0f, Road + 0.003f, -3.2f), new Vector3(150f, 0.006f, 0.1f), "RoadWhite");

            // Crosswalks and the stop line at the lane.
            for (float z = -2.7f; z <= 2.8f; z += 0.9f)
                m.Part(new Vector3(8.2f, Road + 0.003f, z), new Vector3(1.8f, 0.006f, 0.45f), "RoadWhite");
            for (float x = 11.6f; x <= 16.5f; x += 0.9f)
                m.Part(new Vector3(x, Road + 0.003f, 8.3f), new Vector3(0.45f, 0.006f, 1.8f), "RoadWhite");
            m.Part(new Vector3(15.5f, Road + 0.003f, 4.3f), new Vector3(2.8f, 0.006f, 0.3f), "RoadWhite");

            // Sidewalks with curbs; gaps where driveways and the forecourt meet the road.
            SidewalkAlongX(m, -75f, 11f, 3.5f, 6f, 3.5f, new[] { new Vector2(-24f, -21f), new Vector2(-7.4f, -4.6f) });
            SidewalkAlongX(m, 17f, 75f, 3.5f, 6f, 3.5f, new Vector2[0]);
            SidewalkAlongX(m, -75f, 75f, -6f, -3.5f, -3.5f, new[] { new Vector2(-24f, -21f), new Vector2(12f, 46f) });
            SidewalkAlongZ(m, 6f, 45f, 8.5f, 11f, 11f);
            SidewalkAlongZ(m, 6f, 45f, 17f, 19.5f, 17f);

            // Driveways and front walks.
            m.Part(new Vector3(-22.5f, Paving * 0.5f, 12.25f), new Vector3(3f, Paving, 17.5f), "Concrete", true);
            m.Part(new Vector3(-6f, Paving * 0.5f, 11.75f), new Vector3(2.8f, Paving, 16.5f), "Concrete", true);
            m.Part(new Vector3(-22.5f, Paving * 0.5f, -11.75f), new Vector3(3f, Paving, 16.5f), "Concrete", true);
            m.Part(new Vector3(-30f, Paving * 0.5f, 7.8f), new Vector3(1.4f, Paving, 3.6f), "Concrete", true);
            m.Part(new Vector3(-14f, Paving * 0.5f, 7.9f), new Vector3(1.4f, Paving, 3.8f), "Concrete", true);
            m.Part(new Vector3(1f, Paving * 0.5f, 8.35f), new Vector3(1.4f, Paving, 4.7f), "Concrete", true);
            m.Part(new Vector3(-30f, Paving * 0.5f, -7.8f), new Vector3(1.4f, Paving, 3.6f), "Concrete", true);

            // Pool deck and gas station forecourt with parking lines.
            m.Part(new Vector3(35f, Paving * 0.5f, 23.5f), new Vector3(30f, Paving, 29f), "Coping", true);
            m.Part(new Vector3(29f, Forecourt * 0.5f, -18.75f), new Vector3(34f, Forecourt, 30.5f), "Concrete", true);
            for (float x = 13f; x <= 26.5f; x += 3f)
                m.Part(new Vector3(x, Forecourt + 0.003f, -29.5f), new Vector3(0.1f, 0.006f, 5f), "RoadWhite");

            Bake(m, "Village_Roads", roads);
        }

        private static void SidewalkAlongX(PropModel m, float x0, float x1, float z0, float z1, float curbZ, Vector2[] gaps)
        {
            float x = x0;
            foreach (Vector2 gap in gaps.OrderBy(g => g.x).Concat(new[] { new Vector2(x1, x1) }))
            {
                if (gap.x > x)
                {
                    float center = (x + gap.x) * 0.5f;
                    m.Part(new Vector3(center, Walk * 0.5f, (z0 + z1) * 0.5f), new Vector3(gap.x - x, Walk, z1 - z0), "Sidewalk", true);
                    float curbCenter = curbZ > (z0 + z1) * 0.5f ? curbZ - 0.08f : curbZ + 0.08f;
                    m.Part(new Vector3(center, 0.08f, curbCenter), new Vector3(gap.x - x, 0.16f, 0.16f), "Curb");
                }
                x = Mathf.Max(x, gap.y);
            }
        }

        private static void SidewalkAlongZ(PropModel m, float z0, float z1, float x0, float x1, float curbX)
        {
            float center = (z0 + z1) * 0.5f;
            m.Part(new Vector3((x0 + x1) * 0.5f, Walk * 0.5f, center), new Vector3(x1 - x0, Walk, z1 - z0), "Sidewalk", true);
            float curbCenter = curbX > (x0 + x1) * 0.5f ? curbX - 0.08f : curbX + 0.08f;
            m.Part(new Vector3(curbCenter, 0.08f, center), new Vector3(0.16f, 0.16f, z1 - z0), "Curb");
        }

        // ───────────── Residential yards ─────────────

        private static void BuildResidentialYards(Transform statics, Transform decoration)
        {
            Transform fences = Group(statics, "Fences");
            var northGaps = new[] { new Vector2(-31f, -29f), new Vector2(-24.2f, -20.8f), new Vector2(-15f, -13f), new Vector2(-7.6f, -4.4f), new Vector2(0f, 2f) };
            FenceAlongX(fences, "PicketFence", 7.3f, -37f, 7f, northGaps);
            FenceAlongX(fences, "PicketFence", -7.3f, -37f, -16f, new[] { new Vector2(-31f, -29f), new Vector2(-24.2f, -20.8f) });

            Transform hedges = Group(statics, "Hedges");
            FenceAlongX(hedges, "Hedge", 34f, -37f, 7f, new Vector2[0]);
            FenceAlongX(hedges, "Hedge", -34f, -37f, -16f, new Vector2[0]);
            FenceAlongZ(hedges, "Hedge", -37.5f, 8f, 34f, new Vector2[0]);
            FenceAlongZ(hedges, "Hedge", -37.5f, -34f, -8f, new Vector2[0]);

            Transform yards = Group(_interactive, "ResidentialYards");

            // Front yards: mailboxes, bins, gnomes, pots.
            Spawn("Mailbox", yards, new Vector3(-32f, 0f, 6.6f), 0f);
            Spawn("Mailbox", yards, new Vector3(-16f, 0f, 6.6f), 0f);
            Spawn("Mailbox", yards, new Vector3(-1f, 0f, 6.6f), 0f);
            Spawn("Mailbox", yards, new Vector3(-32f, 0f, -6.6f), 180f);
            Spawn("TrashBin_Green", yards, new Vector3(-20.2f, 0f, 6.75f), 0f);
            Spawn("TrashBin_Blue", yards, new Vector3(-19.4f, 0f, 6.75f), 0f);
            Spawn("TrashBin_Green", yards, new Vector3(-3.7f, 0f, 6.75f), 0f);
            Spawn("TrashBin_Blue", yards, new Vector3(-11f, 0f, 6.75f), 0f);
            Spawn("TrashBin_Blue", yards, new Vector3(-20.2f, 0f, -6.75f), 180f);
            Spawn("GardenGnome", yards, new Vector3(-27f, 0f, 8.4f), 160f);
            Spawn("GardenGnome", yards, new Vector3(-16.6f, 0f, 8.7f), 200f);
            Spawn("GardenGnome", yards, new Vector3(-27f, 0f, -8.4f), 20f);
            Spawn("FlowerPot", yards, new Vector3(-34.3f, Floor, 10.3f), 0f);
            Spawn("FlowerPot", yards, new Vector3(-25.7f, Floor, 10.3f), 0f);
            Spawn("FlowerPot", yards, new Vector3(-15.4f, Floor, 11f), 0f);
            Spawn("FlowerPot", yards, new Vector3(-12.6f, Floor, 11f), 0f);
            Spawn("FlowerPot", yards, new Vector3(2.7f, 0f, 11.2f), 0f);
            Spawn("FlowerPot", yards, new Vector3(-25.7f, Floor, -10.3f), 0f);
            Spawn("SidewalkSign", yards, new Vector3(-12f, Walk, 5f), 30f);
            Spawn("Bench", yards, new Vector3(-33f, Floor, 11.2f), 0f);

            // Beige house backyard.
            Spawn("PicnicTable", yards, new Vector3(-31f, 0f, 25f), 0f);
            Spawn("Grill", yards, new Vector3(-27f, 0f, 23.5f), 0f);
            Spawn("DogHouse", yards, new Vector3(-34.2f, 0f, 27f), 90f);
            Spawn("Wheelbarrow", yards, new Vector3(-26.5f, 0f, 28f), 30f);
            Spawn("GardenRake", yards, new Vector3(-28.5f, 0f, 29.6f), 70f);
            Spawn("WateringCan", yards, new Vector3(-33f, 0f, 23f), 120f);
            Spawn("BeachBall", yards, new Vector3(-29f, 0f, 27.6f), 0f);

            // Bungalow backyard.
            Spawn("PatioTable", yards, new Vector3(-14f, 0f, 24f), 0f);
            Spawn("PlasticChair", yards, new Vector3(-14f, 0f, 22.9f), 180f);
            Spawn("PlasticChair", yards, new Vector3(-15.1f, 0f, 24.3f), 90f);
            Spawn("PlasticChair", yards, new Vector3(-12.9f, 0f, 24.3f), -90f);
            Spawn("Umbrella_Red", yards, new Vector3(-11.6f, 0f, 25.8f), 0f);
            Spawn("Bench", yards, new Vector3(-17.5f, 0f, 28f), 180f);
            Spawn("FlowerPot", yards, new Vector3(-9.6f, 0f, 21f), 0f);
            Spawn("Bucket_Red", yards, new Vector3(-18.6f, 0f, 21.2f), 0f);

            // Brick house backyard.
            Spawn("Bench", yards, new Vector3(1f, 0f, 26.5f), 180f);
            Spawn("Grill", yards, new Vector3(4f, 0f, 23.2f), 0f);
            Spawn("WoodCrate", yards, new Vector3(-2f, 0f, 24f), 10f);
            Spawn("WoodCrate", yards, new Vector3(-2f, 0.51f, 24f), 25f);
            Spawn("CardboardBox", yards, new Vector3(-1f, 0f, 25.3f), 0f);
            Spawn("CardboardBox", yards, new Vector3(-0.3f, 0f, 24.4f), 35f);
            Spawn("Shovel", yards, new Vector3(3.2f, 0f, 28.2f), 100f);
            Spawn("Bucket_Blue", yards, new Vector3(2.2f, 0f, 22.6f), 0f);

            // Blue house backyard (south).
            Spawn("PicnicTable", yards, new Vector3(-30f, 0f, -25f), 0f);
            Spawn("DogHouse", yards, new Vector3(-26f, 0f, -27f), 200f);
            Spawn("BeachBall", yards, new Vector3(-33f, 0f, -24f), 0f);
            Spawn("WateringCan", yards, new Vector3(-27.5f, 0f, -22.4f), 40f);
            Spawn("Grill", yards, new Vector3(-34f, 0f, -28f), 0f);
            Spawn("FlowerPot", yards, new Vector3(-35f, 0f, -21f), 0f);
            Spawn("GardenGnome", yards, new Vector3(-31f, 0f, -29.5f), 0f);

            // Park by the pond.
            Spawn("Bench", yards, new Vector3(-13f, 0f, -14.6f), 0f);
            Spawn("Bench", yards, new Vector3(-2.2f, 0f, -22f), 90f);
            Spawn("PicnicTable", yards, new Vector3(3f, 0f, -16f), 20f);
            Spawn("TrashBin_Green", yards, new Vector3(-6.5f, 0f, -14.8f), 0f);
            Spawn("FlowerPot", yards, new Vector3(-16.8f, 0f, -16.6f), 0f);
        }

        // ───────────── Gas station ─────────────

        private static void BuildGasStation(Transform station, GameObject canopy, GameObject priceSign)
        {
            Transform statics = Group(station, "Structures");
            Instance(canopy, statics, new Vector3(24f, Forecourt, -15f), 0f);
            Instance(priceSign, statics, new Vector3(44f, Forecourt, -7.5f), 0f);
            foreach (float x in new[] { 20f, 28f })
            {
                Spawn("FuelIsland", statics, new Vector3(x, Forecourt, -15f), 0f);
                Spawn("FuelDispenser", statics, new Vector3(x, Forecourt + 0.2f, -15f), 90f);
            }
            Spawn("PropaneCage", statics, new Vector3(41.3f, Forecourt, -29.6f), 90f);
            Spawn("AirPump", statics, new Vector3(44.8f, Forecourt, -13f), -90f);
            foreach (float x in new[] { 29.2f, 32.3f, 35.7f, 38.8f })
                Spawn("Bollard", statics, new Vector3(x, Forecourt, -22.9f), 0f);

            Transform props = Group(_interactive, "GasStationProps");
            Spawn("TrashBin_Green", props, new Vector3(21.3f, Forecourt, -18.3f), 0f);
            Spawn("TrashBin_Blue", props, new Vector3(29.3f, Forecourt, -11.8f), 0f);
            Spawn("WindshieldBucket", props, new Vector3(18.8f, Forecourt, -11.9f), 0f);
            Spawn("WindshieldBucket", props, new Vector3(26.8f, Forecourt, -18.2f), 0f);
            Spawn("GasCanister", props, new Vector3(25.4f, Forecourt, -13.8f), 20f);
            Spawn("GasCanister", props, new Vector3(25.9f, Forecourt, -14.6f), -15f);
            Spawn("GasCanister", props, new Vector3(30.6f, Forecourt, -23.2f), 90f);
            Spawn("OilBottleCrate", props, new Vector3(31.2f, Forecourt, -23.4f), 0f);
            Spawn("OilBottleCrate", props, new Vector3(36.9f, Forecourt, -23.4f), 0f);
            Spawn("IceChest", props, new Vector3(38.7f, Forecourt, -23.3f), 180f);
            Spawn("TireStack", props, new Vector3(41.6f, Forecourt, -25.8f), 0f);
            Spawn("TireStack", props, new Vector3(41.6f, Forecourt, -27.1f), 0f);
            Spawn("SidewalkSign", props, new Vector3(31f, Forecourt, -21f), 180f);
            Spawn("TrafficCone", props, new Vector3(13.6f, Forecourt, -26.2f), 0f);
            Spawn("TrafficCone", props, new Vector3(14.6f, Forecourt, -26.2f), 0f);
            Spawn("TrafficCone", props, new Vector3(25.6f, Forecourt, -33f), 0f);
            Spawn("TrafficCone", props, new Vector3(13.4f, Forecourt, -33f), 0f);
            Spawn("CardboardBox", props, new Vector3(36f, Forecourt, -32.4f), 10f);
            Spawn("CardboardBox", props, new Vector3(36.7f, Forecourt, -32.5f), -20f);
            Spawn("WoodCrate", props, new Vector3(37.6f, Forecourt, -32.3f), 0f);
            Spawn("Car_Blue", statics, new Vector3(14.5f, Forecourt, -29.5f), 0f);
            Spawn("Car_Red", statics, new Vector3(20.5f, Forecourt, -29.5f), 180f);
        }

        // ───────────── Street furniture, trees, decor ─────────────

        private static void BuildStreetFurniture(Transform statics)
        {
            Transform lamps = Group(statics, "StreetLamps");
            foreach (float x in new[] { -60f, -44f, -27f, -10f, 4f, 24f, 40f, 58f })
                Spawn("StreetLamp", lamps, new Vector3(x, Walk, 5.7f), 0f);
            foreach (float x in new[] { -55f, -38f, -15f, 2f, 50f, 62f })
                Spawn("StreetLamp", lamps, new Vector3(x, Walk, -5.7f), 180f);
            foreach (float z in new[] { 14f, 30f, 44f })
                Spawn("StreetLamp", lamps, new Vector3(19.2f, Walk, z), 90f);
            foreach (float z in new[] { 22f, 38f })
                Spawn("StreetLamp", lamps, new Vector3(8.8f, Walk, z), -90f);

            Spawn("Car_Red", statics, new Vector3(-22.5f, Paving, 15f), 0f);
            Spawn("Car_Yellow", statics, new Vector3(-6f, Paving, 15.5f), 0f);
            Spawn("Car_Yellow", statics, new Vector3(-22.5f, Paving, -15f), 180f);

            Transform signs = Group(_interactive, "RoadSigns");
            Spawn("RoadSign_Stop", signs, new Vector3(18.3f, Walk, 6.6f), 180f);
            Spawn("RoadSign_Speed", signs, new Vector3(-45f, Walk, -5.1f), 90f);
            Spawn("RoadSign_Speed", signs, new Vector3(55f, Walk, 5.1f), -90f);
            Spawn("RoadSign_Warning", signs, new Vector3(-4f, Walk, -5.1f), 90f);
            Spawn("TrafficCone", signs, new Vector3(-40f, Road, -2.4f), 0f);
            Spawn("TrafficCone", signs, new Vector3(-38.6f, Road, -2.4f), 0f);
        }

        private static void BuildTrees(Transform decoration)
        {
            Transform trees = Group(decoration, "Trees");
            Vector2[] spots =
            {
                new Vector2(-36f, 30f), new Vector2(-24.5f, 32f), new Vector2(-20f, 28f), new Vector2(-9.5f, 30f), new Vector2(-5.5f, 24f),
                new Vector2(6.5f, 30f), new Vector2(-36f, 9.5f), new Vector2(-2.4f, 8.8f), new Vector2(-36f, -30f), new Vector2(-24.5f, -32f),
                new Vector2(-19f, -14f), new Vector2(-18.5f, -29f), new Vector2(-1f, -31f), new Vector2(5f, -22f), new Vector2(8f, -34f),
                new Vector2(9.5f, -10f), new Vector2(7f, 36f), new Vector2(7f, 42f), new Vector2(52.5f, 12f), new Vector2(52.5f, 22f),
                new Vector2(52.5f, 32f), new Vector2(25f, 41f), new Vector2(35f, 41.5f), new Vector2(45f, 41f), new Vector2(-45f, 10f),
                new Vector2(-46f, -12f), new Vector2(-44f, 26f), new Vector2(-46f, 40f), new Vector2(-45f, -40f), new Vector2(60f, -20f),
                new Vector2(61f, 20f), new Vector2(60f, 40f), new Vector2(-30f, 48f), new Vector2(-10f, 50f), new Vector2(10f, 49f),
                new Vector2(-30f, -45f), new Vector2(-10f, -44f), new Vector2(12f, -44f), new Vector2(30f, -43f), new Vector2(50f, -42f),
                new Vector2(48.5f, -36f), new Vector2(10.5f, -37f), new Vector2(49f, -18f), new Vector2(-16f, -31f), new Vector2(-57f, 12f),
            };
            string[] kinds = { "Tree1", "Tree2", "Tree3" };
            for (int i = 0; i < spots.Length; i++)
                Tree(trees, kinds[i % kinds.Length], new Vector3(spots[i].x, 0f, spots[i].y), Random.Range(0.9f, 1.3f));

            Transform bushes = Group(decoration, "Bushes");
            Vector2[] bushSpots =
            {
                new Vector2(-33.5f, 8.6f), new Vector2(-26.5f, 8.8f), new Vector2(-18.6f, 10.6f), new Vector2(-9.4f, 10.6f),
                new Vector2(-2.7f, 11.3f), new Vector2(4.7f, 11.3f), new Vector2(-33.5f, -8.6f), new Vector2(-26.5f, -8.8f),
                new Vector2(-16.8f, -18f), new Vector2(-3.2f, -26f), new Vector2(-16.5f, -26.4f), new Vector2(11f, -20f),
                new Vector2(21f, 40.5f), new Vector2(21.5f, 7.6f), new Vector2(48.5f, 7.6f), new Vector2(47f, -36f),
                new Vector2(-8.4f, 21f), new Vector2(-19.8f, 21f), new Vector2(5.8f, 20.8f), new Vector2(-34.6f, -21f),
            };
            foreach (Vector2 spot in bushSpots)
            {
                GameObject bush = InstanceAsset(TreePath + "Bush1.prefab", bushes, new Vector3(spot.x, 0f, spot.y), Random.Range(0f, 360f));
                if (bush != null)
                    bush.transform.localScale *= Random.Range(0.9f, 1.3f);
            }
        }

        private static void BuildDecor(Transform decoration)
        {
            var m = new PropModel();

            // Garden beds with flowers.
            Vector3[] beds = { new Vector3(-30f, 0f, 31.5f), new Vector3(-14f, 0f, 30.5f), new Vector3(1f, 0f, 31f), new Vector3(-30f, 0f, -31.5f), new Vector3(-12f, 0f, -29f) };
            string[] flowers = { "FlowerPink", "FlowerYellow", "FlowerWhite", "PlasticRed" };
            foreach (Vector3 bed in beds)
            {
                m.Part(bed + new Vector3(0f, 0.12f, 0f), new Vector3(3f, 0.24f, 1f), "WoodLight", true);
                m.Part(bed + new Vector3(0f, 0.2f, 0f), new Vector3(2.8f, 0.1f, 0.8f), "Soil");
                for (int i = 0; i < 9; i++)
                {
                    var spot = bed + new Vector3(-1.2f + i * 0.3f, 0.36f, (i % 2 == 0 ? -0.2f : 0.2f));
                    m.Mesh.Sphere(spot, new Vector3(0.13f, 0.11f, 0.13f), 6, 3, "Leaves");
                    m.Mesh.Sphere(spot + Vector3.up * 0.1f, Vector3.one * 0.07f, 5, 3, flowers[i % flowers.Length]);
                }
            }

            // Reeds at the pond corners.
            foreach (Vector2 corner in new[] { new Vector2(-16.6f, -17f), new Vector2(-3.5f, -17f), new Vector2(-16.6f, -27f), new Vector2(-3.5f, -27f) })
            {
                for (int i = 0; i < 7; i++)
                {
                    var offset = new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
                    m.Mesh.Cylinder(new Vector3(corner.x, 0f, corner.y) + offset, 0.03f, 0.01f, Random.Range(0.8f, 1.3f), 4, "LeavesDark");
                }
            }

            // Stepping stones from the bungalow porch round to its backyard.
            for (int i = 0; i < 8; i++)
                m.Mesh.Cylinder(new Vector3(-8f, 0f, 11f + i * 1.3f), 0.32f, 0.32f, 0.04f, 7, "Stone", true, i * 0.4f);

            Bake(m, "Village_Decor", decoration);
        }

        // ───────────── Interiors ─────────────

        private static void FurnishInteriors(Dictionary<string, HouseInfo> houses, Dictionary<string, Transform> placed)
        {
            Transform interiors = Group(_interactive, "Interiors");
            foreach (KeyValuePair<string, Transform> house in placed)
            {
                HouseInfo info = houses[HouseKey(house.Key)];
                Transform parent = Group(interiors, house.Key);
                foreach (KeyValuePair<string, Bounds> room in info.Rooms)
                    Furnish(house.Value, room.Key, room.Value, parent);
            }
        }

        private static void Furnish(Transform house, string room, Bounds r, Transform parent)
        {
            float x0 = r.min.x, x1 = r.max.x, z0 = r.min.z, z1 = r.max.z;
            float cx = r.center.x, cz = r.center.z;

            void Put(string prop, float x, float z, float yaw, float y = Floor)
            {
                Spawn(prop, parent, house.TransformPoint(new Vector3(x, y, z)), house.eulerAngles.y + yaw);
            }

            switch (room)
            {
                case "Living Room":
                {
                    float sofaX = x0 + (x1 - x0) * 0.45f;
                    Put("Rug", sofaX, z1 - 2.2f, 0f);
                    Put("Sofa", sofaX, z1 - 0.55f, 0f);
                    Put("CoffeeTable", sofaX, z1 - 1.8f, 0f);
                    Put("BookStack", sofaX + 0.25f, z1 - 1.8f, 15f, Floor + 0.45f);
                    Put("TableLamp", sofaX - 0.3f, z1 - 1.8f, 0f, Floor + 0.45f);
                    Put("TVStand", x0 + 0.3f, cz + 1.6f, -90f);
                    Put("Television", x0 + 0.3f, cz + 1.6f, -90f, Floor + 0.5f);
                    Put("Armchair", x1 - 0.6f, cz - 0.9f, 90f);
                    Put("FloorLamp", sofaX + 1.45f, z1 - 0.35f, 0f);
                    Put("Bookshelf", x0 + 0.2f, z0 + 1.6f, -90f);
                    Put("Sideboard", x0 + 0.75f, z1 - 0.25f, 0f);
                    Put("Vase", x0 + 0.55f, z1 - 0.25f, 0f, Floor + 0.85f);
                    Put("FlowerPot", x1 - 0.35f, z1 - 0.35f, 0f);
                    break;
                }
                case "Kitchen":
                {
                    Put("KitchenCounter", x1 - 0.3f, cz + 1.0f, 90f);
                    Put("Bucket_Blue", x1 - 0.35f, cz - 0.6f, 0f);
                    float tableX = (x0 + x1) * 0.5f - 0.1f;
                    float tableZ = z0 + 1.7f;
                    Put("DiningTable", tableX, tableZ, 90f);
                    Put("Vase", tableX, tableZ, 0f, Floor + 0.77f);
                    foreach (float dz in new[] { -0.4f, 0.4f })
                    {
                        Put("DiningChair", tableX - 0.72f, tableZ + dz, -90f);
                        Put("DiningChair", tableX + 0.72f, tableZ + dz, 90f);
                    }
                    PutFridge(house, parent, new Vector3(x1 - 0.45f, Floor, z1 - 0.5f), 90f);
                    break;
                }
                case "Bedroom":
                    Put("Rug", cx, cz - 0.6f, 90f);
                    Put("Bed", x1 - 0.85f, z1 - 1.1f, 0f);
                    Put("Nightstand", x1 - 1.95f, z1 - 0.3f, 0f);
                    Put("TableLamp", x1 - 1.95f, z1 - 0.3f, 0f, Floor + 0.5f);
                    Put("Wardrobe", x0 + 0.35f, cz - 1.1f, -90f);
                    Put("FloorLamp", x0 + 0.4f, z0 + 0.5f, 0f);
                    Put("FlowerPot", x1 - 0.35f, z0 + 0.4f, 0f);
                    break;
                case "Quick Mart":
                {
                    Put("ShopShelf", -3.2f, 3.1f, 0f);
                    Put("ShopShelf", -3.2f, 4.9f, 180f);
                    Put("ShopShelf", 3.4f, 4.9f, 180f);
                    Put("ShopCounter", 3.4f, 2.0f, 0f);
                    Put("CashRegister", 3.9f, 2.0f, 180f, Floor + 1.045f);
                    foreach (float x in new[] { -3.9f, -3.3f, -2.7f })
                    {
                        Put("SnackBox_Red", x, 3.1f, 0f, Floor + 1.465f);
                        Put("SnackBox_Yellow", x + 0.25f, 4.9f, 180f, Floor + 1.465f);
                    }
                    foreach (float x in new[] { 2.8f, 3.4f, 4.0f })
                        Put("BottlePack", x, 4.9f, 0f, Floor + 1.465f);
                    Put("SnackBox_Green", 2.6f, 2.05f, 0f, Floor + 1.045f);
                    Put("CardboardBox", -5.1f, 6.2f, 10f);
                    Put("CardboardBox", -4.5f, 6.3f, -10f);
                    PutFridge(house, parent, new Vector3(0.6f, Floor, 6.3f), 0f);
                    break;
                }
                case "Pool House":
                    Put("WoodCrate", x0 + 0.4f, z1 - 0.4f, 0f);
                    Put("SwimRing_Pink", x1 - 0.5f, z1 - 0.5f, 0f);
                    Put("ChlorineCanister", x1 - 0.4f, z0 + 0.9f, 0f);
                    Put("Bucket_Blue", x0 + 0.4f, z0 + 1.0f, 0f);
                    break;
            }
        }

        private static void PutFridge(Transform house, Transform parent, Vector3 local, float yaw)
        {
            GameObject fridge = InstanceAsset(FridgePrefabPath, parent, house.TransformPoint(local), house.eulerAngles.y + yaw);
            if (fridge != null)
                PlaceOnSurface(fridge, house.TransformPoint(local).y);
        }

        // ───────────── Gameplay ─────────────

        private static void SetupGameplay(Scene scene, Transform gameplay, Transform poolArea,
            Dictionary<string, HouseInfo> houses, Dictionary<string, Transform> placed)
        {
            Transform waterSources = Group(gameplay, "WaterSources");
            Transform pumps = Group(gameplay, "Pump");
            Transform hoses = Group(gameplay, "Hose");
            Transform nozzles = Group(gameplay, "Nozzle");
            Transform hydrants = Group(gameplay, "Hydrant");
            Transform fires = Group(gameplay, "FireObjects");
            Transform rooms = Group(gameplay, "RoomTemperatureObjects");
            Transform effects = Group(gameplay, "Effects");

            BuildRectPool(FindRoot(scene, "Reservoir B"), waterSources);
            BuildOctagonPool(FindRoot(scene, "Reservoir B (1)"), waterSources);
            BuildPond(FindRoot(scene, "Lake"), waterSources);
            FurnishPoolArea(poolArea);

            PlaceExisting(FindRoot(scene, "Pump"), pumps, new Vector3(23.6f, 0f, 22.2f), Paving);
            PlaceExisting(FindRoot(scene, "Pump (1)"), pumps, new Vector3(-10f, 0f, -18.7f), 0.62f);
            GameObject[] transferHoses = ReplaceMissingTransferHoses(scene);
            if (transferHoses.Length > 0) PlaceExisting(transferHoses[0], hoses, new Vector3(23.4f, 0f, 24.4f), Paving);
            if (transferHoses.Length > 1) PlaceExisting(transferHoses[1], hoses, new Vector3(-12f, 0f, -15.4f), 0f);
            PlaceExisting(FindRoot(scene, "CoiledFireHoseYellow"), hoses, new Vector3(10f, 0f, -4.9f), Walk);
            PlaceExisting(FindRoot(scene, "FireHydrant_Red"), hydrants, new Vector3(8.6f, 0f, -5.4f), Walk);
            PlaceExisting(FindRoot(scene, "Nozzle"), nozzles, new Vector3(7.4f, 0f, -4.9f), Walk);

            // Fires: the scene's fire in the bungalow living room, a second one in the beige house kitchen.
            Transform bungalow = placed["Bungalow"];
            Bounds living = houses["House_Bungalow"].Rooms[0].Value;
            Move(FindRoot(scene, "Cartoon Fire"), fires, bungalow.TransformPoint(new Vector3(living.min.x + 1.2f, Floor + 0.5f, living.max.z - 2.9f)));
            var kitchenFire = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefabPath), fires);
            Bounds kitchen = houses["House_TwoStorey_Beige"].Rooms[1].Value;
            kitchenFire.name = "Cartoon Fire (Beige Kitchen)";
            kitchenFire.transform.position = placed["Beige House"].TransformPoint(new Vector3(kitchen.max.x - 1.0f, Floor + 0.5f, kitchen.center.z + 1.0f));

            Move(FindRoot(scene, "WindZone_Test"), gameplay, new Vector3(-60f, 2.7f, 42f));
            Move(FindRoot(scene, "Water"), effects, null);
            Move(FindRoot(scene, "WaterSplash"), effects, null);

            BuildRooms(rooms, houses, placed);

            GameObject player = FindRoot(scene, "Player");
            if (player != null)
            {
                player.transform.SetPositionAndRotation(new Vector3(6f, Walk + 1.02f, -4.7f), Quaternion.Euler(0f, -20f, 0f));
                Move(player, gameplay, null);
                SetupRoomVisitor(player);
            }
        }

        /// <summary>
        /// SampleScene's transfer hoses point at a prefab GUID that no longer exists (the prefab was renamed
        /// with a new GUID). In this scene they are replaced by instances of the current prefab.
        /// </summary>
        private static GameObject[] ReplaceMissingTransferHoses(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TransferHosePrefabPath);
            var result = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects().Where(g => g.name.StartsWith("TransferHose_20m")).ToArray())
            {
                if (!PrefabUtility.IsPrefabAssetMissing(root) || prefab == null)
                {
                    result.Add(root);
                    continue;
                }

                var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                replacement.name = root.name;
                replacement.transform.SetPositionAndRotation(root.transform.position, root.transform.rotation);
                Object.DestroyImmediate(root);
                result.Add(replacement);
            }

            return result.ToArray();
        }

        private static void BuildRectPool(GameObject tank, Transform waterSources)
        {
            if (tank == null)
                return;

            // Inner basin 9 × 5 m, raised tiled rim with a coping, 0.65 m of water.
            const float depth = 0.65f;
            const float rimHeight = 0.75f;
            var inner = new Vector2(9f, 5f);
            PrepareTank(tank, waterSources, "Pool_Rectangular", new Vector3(30f, Paving, 22f));

            var rim = new PropModel();
            float hx = inner.x * 0.5f, hz = inner.y * 0.5f;
            const float t = 0.4f;
            rim.Part(new Vector3(0f, rimHeight * 0.5f, -hz - t * 0.5f), new Vector3(inner.x + t * 2f, rimHeight, t), "PoolTile", true);
            rim.Part(new Vector3(0f, rimHeight * 0.5f, hz + t * 0.5f), new Vector3(inner.x + t * 2f, rimHeight, t), "PoolTile", true);
            rim.Part(new Vector3(-hx - t * 0.5f, rimHeight * 0.5f, 0f), new Vector3(t, rimHeight, inner.y), "PoolTile", true);
            rim.Part(new Vector3(hx + t * 0.5f, rimHeight * 0.5f, 0f), new Vector3(t, rimHeight, inner.y), "PoolTile", true);
            rim.Mesh.Box(new Vector3(0f, rimHeight + 0.04f, -hz - t * 0.5f), new Vector3(inner.x + t * 2f + 0.1f, 0.08f, t + 0.1f), "Coping");
            rim.Mesh.Box(new Vector3(0f, rimHeight + 0.04f, hz + t * 0.5f), new Vector3(inner.x + t * 2f + 0.1f, 0.08f, t + 0.1f), "Coping");
            rim.Mesh.Box(new Vector3(-hx - t * 0.5f, rimHeight + 0.04f, 0f), new Vector3(t + 0.1f, 0.08f, inner.y), "Coping");
            rim.Mesh.Box(new Vector3(hx + t * 0.5f, rimHeight + 0.04f, 0f), new Vector3(t + 0.1f, 0.08f, inner.y), "Coping");
            rim.Part(new Vector3(0f, 0.01f, 0f), new Vector3(inner.x, 0.02f, inner.y), "PoolTile");
            GameObject rimObject = Bake(rim, "Pool_Rectangular_Rim", tank.transform);
            rimObject.transform.localPosition = Vector3.zero;

            SetupWater(tank, new Vector3(inner.x, depth, inner.y), depth, inner.x * inner.y * depth * 1000f);
        }

        private static void BuildOctagonPool(GameObject tank, Transform waterSources)
        {
            if (tank == null)
                return;

            // Octagonal pool, 3 m from the centre to each inner wall, blue rim like a big paddling pool.
            const float apothem = 3f;
            const float depth = 0.6f;
            const float rimHeight = 0.72f;
            const float t = 0.4f;
            PrepareTank(tank, waterSources, "Pool_Octagonal", new Vector3(41f, Paving, 31f));

            float side = 2f * (apothem + t) * Mathf.Tan(22.5f * Mathf.Deg2Rad) + 0.02f;
            var rim = new PropModel();
            var colliders = new List<(Vector3, Quaternion, Vector3)>();
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                Quaternion rotation = Quaternion.Euler(0f, -angle, 0f);
                Vector3 center = rotation * new Vector3(apothem + t * 0.5f, rimHeight * 0.5f, 0f);
                rim.Mesh.SetTransform(center, rotation);
                rim.Mesh.Box(Vector3.zero, new Vector3(t, rimHeight, side), "PoolRim");
                rim.Mesh.Box(new Vector3(0f, rimHeight * 0.5f + 0.04f, 0f), new Vector3(t + 0.12f, 0.08f, side + 0.05f), "Coping");
                rim.Mesh.ResetTransform();
                colliders.Add((center, rotation, new Vector3(t, rimHeight, side)));
            }
            // Pool floor: the octagon outline is extruded in XY, so turn it flat.
            rim.Mesh.SetTransform(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            rim.Mesh.Extrude(VillageProps.Polygon(8, apothem / Mathf.Cos(22.5f * Mathf.Deg2Rad), 22.5f), -0.02f, 0f, "PoolTile");
            rim.Mesh.ResetTransform();
            GameObject rimObject = Bake(rim, "Pool_Octagonal_Rim", tank.transform, false);
            rimObject.transform.localPosition = Vector3.zero;
            foreach (var collider in colliders)
            {
                var child = new GameObject("RimCollider");
                child.transform.SetParent(rimObject.transform, false);
                child.transform.SetLocalPositionAndRotation(collider.Item1, collider.Item2);
                child.AddComponent<BoxCollider>().size = collider.Item3;
            }

            MarkStatic(rimObject);

            // Octagonal water body: a unit prism scaled to the pool.
            var prism = new LowPolyMesh();
            prism.SetTransform(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            prism.Extrude(VillageProps.Polygon(8, 0.5f, 22.5f), -0.5f, 0.5f, "Glass");
            prism.ResetTransform();
            Mesh mesh = VillageAssets.SaveMesh(prism, "Water_Octagon", "Water_Octagon", out _);
            Transform water = tank.transform.Find("Water");
            if (water != null)
                water.GetComponent<MeshFilter>().sharedMesh = mesh;

            float width = 2f * apothem / Mathf.Cos(22.5f * Mathf.Deg2Rad);
            float area = 8f * apothem * apothem * Mathf.Tan(22.5f * Mathf.Deg2Rad);
            SetupWater(tank, new Vector3(width, depth, width), depth, area * depth * 1000f, new Vector3(apothem * 2f, depth, apothem * 2f));
        }

        private static void PrepareTank(GameObject tank, Transform waterSources, string name, Vector3 position)
        {
            tank.name = name;
            tank.transform.SetParent(waterSources, false);
            tank.transform.SetPositionAndRotation(position, Quaternion.identity);
            foreach (Transform child in tank.transform.Cast<Transform>().ToArray())
            {
                if (child.name.StartsWith("Wall") || child.name == "Floor")
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        /// <param name="visual">Scale of the water visual (X, full height, Z).</param>
        /// <param name="volume">Size of the trigger volume; defaults to the visual size.</param>
        private static void SetupWater(GameObject tank, Vector3 visual, float depth, float liters, Vector3? volume = null)
        {
            Vector3 box = volume ?? visual;
            Transform volumeObject = tank.transform.Find("Water Volume");
            if (volumeObject != null)
            {
                volumeObject.localPosition = Vector3.zero;
                var collider = volumeObject.GetComponent<BoxCollider>();
                collider.center = new Vector3(0f, depth * 0.5f, 0f);
                collider.size = box;
                var reservoir = volumeObject.GetComponent<WaterReservoirComponent>();
                SetFloat(reservoir, "_capacity", Mathf.Round(liters));
                SetFloat(reservoir, "_initialVolume", Mathf.Round(liters));
            }

            Transform water = tank.transform.Find("Water");
            if (water != null)
            {
                water.localPosition = new Vector3(0f, depth * 0.5f, 0f);
                water.localScale = visual;
            }

            var level = tank.GetComponent<ReservoirLevelView>();
            if (level != null)
            {
                SetFloat(level, "_bottom", 0f);
                SetFloat(level, "_fullHeight", depth);
            }
        }

        private static void BuildPond(GameObject lake, Transform waterSources)
        {
            if (lake == null)
                return;

            // Basin 11 × 8 m, sunk a little so the water sits just under a low stone rim.
            lake.name = "Pond";
            lake.transform.SetParent(waterSources, false);
            lake.transform.SetPositionAndRotation(new Vector3(-10f, -0.35f, -22f), Quaternion.identity);
            var size = new Vector2(11f, 8f);

            Transform volume = lake.transform.Find("Water Volume");
            if (volume != null)
            {
                var box = volume.GetComponent<BoxCollider>();
                box.size = new Vector3(size.x, box.size.y, size.y);
                SetFloat(volume.GetComponent<WaterReservoirComponent>(), "_capacity", 60000f);
                SetFloat(volume.GetComponent<WaterReservoirComponent>(), "_initialVolume", 60000f);
            }

            Transform surface = lake.transform.Find("Surface");
            if (surface != null)
                surface.localScale = new Vector3(size.x, surface.localScale.y, size.y);

            // Stone rim (world top 0.6 m) and a dock with a ramp on the north side.
            var rim = new PropModel();
            const float top = 0.95f;
            const float t = 0.6f;
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            rim.Part(new Vector3(0f, top * 0.5f, -hz - t * 0.5f), new Vector3(size.x + t * 2f, top, t), "Stone", true);
            rim.Part(new Vector3(-3.5f, top * 0.5f, hz + t * 0.5f), new Vector3(size.x * 0.5f + t - 1f, top, t), "Stone", true);
            rim.Part(new Vector3(3.5f, top * 0.5f, hz + t * 0.5f), new Vector3(size.x * 0.5f + t - 1f, top, t), "Stone", true);
            rim.Part(new Vector3(-hx - t * 0.5f, top * 0.5f, 0f), new Vector3(t, top, size.y), "Stone", true);
            rim.Part(new Vector3(hx + t * 0.5f, top * 0.5f, 0f), new Vector3(t, top, size.y), "Stone", true);

            const float deck = 0.97f;
            rim.Part(new Vector3(0f, deck - 0.05f, hz - 0.6f), new Vector3(2.2f, 0.1f, 2.4f), "WoodLight", true);
            rim.Part(new Vector3(0f, deck - 0.05f, hz + 0.3f), new Vector3(2.2f, 0.1f, 1.2f), "WoodLight", true);
            foreach (float x in new[] { -1f, 1f })
                rim.Mesh.Cylinder(new Vector3(x, 0f, hz - 1.7f), 0.08f, 0.08f, deck, 6, "WoodDark");

            float rampLength = 3.2f;
            float rise = deck - 0.35f;
            float angle = Mathf.Atan2(rise, rampLength) * Mathf.Rad2Deg;
            var rampCenter = new Vector3(0f, 0.35f + rise * 0.5f - 0.05f, hz + 0.9f + rampLength * 0.5f);
            rim.Mesh.SetTransform(rampCenter, Quaternion.Euler(angle, 0f, 0f));
            rim.Mesh.Box(Vector3.zero, new Vector3(2.2f, 0.1f, Mathf.Sqrt(rampLength * rampLength + rise * rise)), "WoodLight");
            rim.Mesh.ResetTransform();

            GameObject rimObject = Bake(rim, "Pond_Rim", lake.transform);
            rimObject.transform.localPosition = Vector3.zero;
            var ramp = new GameObject("RampCollider");
            ramp.transform.SetParent(rimObject.transform, false);
            ramp.transform.SetLocalPositionAndRotation(rampCenter, Quaternion.Euler(angle, 0f, 0f));
            ramp.AddComponent<BoxCollider>().size = new Vector3(2.2f, 0.1f, Mathf.Sqrt(rampLength * rampLength + rise * rise));
            MarkStatic(rimObject);
        }

        private static void FurnishPoolArea(Transform poolArea)
        {
            Transform statics = Group(poolArea, "PoolStructures");
            Transform fences = Group(statics, "PoolFence");
            FenceAlongZ(fences, "PicketFence", 20.2f, 9f, 38f, new[] { new Vector2(11.5f, 16.5f) }, Paving);
            FenceAlongZ(fences, "PicketFence", 49.8f, 9f, 38f, new Vector2[0], Paving);
            FenceAlongX(fences, "PicketFence", 9.2f, 20f, 50f, new[] { new Vector2(22f, 26f) }, Paving);
            FenceAlongX(fences, "PicketFence", 37.8f, 20f, 50f, new Vector2[0], Paving);
            Spawn("PoolLadder", statics, new Vector3(28f, Paving, 19.3f), 0f);
            Spawn("PoolLadder", statics, new Vector3(41f, Paving, 27.8f), 0f);
            Spawn("PoolShower", statics, new Vector3(22.5f, Paving, 30f), 0f);

            Transform props = Group(_interactive, "PoolProps");
            foreach (float x in new[] { 26.5f, 28.6f, 31.4f, 33.5f })
                Spawn("SunLounger", props, new Vector3(x, Paving, 27.4f), 0f);
            Spawn("Umbrella_Red", props, new Vector3(30f, Paving, 28.3f), 0f);
            Spawn("Umbrella_Blue", props, new Vector3(30f, Paving, 15.4f), 0f);
            Spawn("Umbrella_Red", props, new Vector3(45.4f, Paving, 35.6f), 0f);

            Spawn("PatioTable", props, new Vector3(26.5f, Paving, 15.6f), 0f);
            Spawn("PlasticChair", props, new Vector3(26.5f, Paving, 14.5f), 0f);
            Spawn("PlasticChair", props, new Vector3(25.4f, Paving, 15.9f), 90f);
            Spawn("PlasticChair", props, new Vector3(27.6f, Paving, 15.9f), -90f);
            Spawn("PatioTable", props, new Vector3(33.5f, Paving, 15.6f), 0f);
            Spawn("PlasticChair", props, new Vector3(32.4f, Paving, 15.9f), 90f);
            Spawn("PlasticChair", props, new Vector3(34.6f, Paving, 15.9f), -90f);
            Spawn("PatioTable", props, new Vector3(37.4f, Paving, 35.8f), 0f);
            Spawn("PlasticChair", props, new Vector3(36.3f, Paving, 35.9f), 90f);
            Spawn("PlasticChair", props, new Vector3(38.5f, Paving, 35.9f), -90f);

            Spawn("SideTable", props, new Vector3(36.6f, Paving, 21f), 0f);
            Spawn("CoolerBox", props, new Vector3(36.8f, Paving, 23.6f), 90f);
            Spawn("Kickboard", props, new Vector3(36.5f, Paving, 19.4f), 20f);
            Spawn("Kickboard", props, new Vector3(37.1f, Paving, 18.7f), -15f);
            Spawn("ChlorineCanister", props, new Vector3(23.7f, Paving, 19.4f), 0f);
            Spawn("Bucket_Blue", props, new Vector3(23.6f, Paving, 25.6f), 0f);
            Spawn("PoolNet", props, new Vector3(22.4f, Paving, 27.2f), 90f);
            Spawn("BeachBall", props, new Vector3(35.6f, Paving, 26.5f), 0f);
            Spawn("BeachBall", props, new Vector3(45f, Paving, 26.6f), 0f);
            Spawn("BeachBall", props, new Vector3(38f, Paving, 34.2f), 0f);
            Spawn("SwimRing_Pink", props, new Vector3(36.6f, Paving, 29f), 0f);
            Spawn("SwimRing_Orange", props, new Vector3(45.6f, Paving, 32.8f), 0f);
            Spawn("PoolFilterPump", props, new Vector3(46.4f, Paving, 29f), 90f);
            Spawn("CoolerBox", props, new Vector3(36.7f, Paving, 32.2f), 0f);
            foreach (float z in new[] { 30f, 33.6f })
                Spawn("SunLounger", props, new Vector3(47.6f, Paving, z), 90f);
            Spawn("TrashBin_Blue", props, new Vector3(21f, Paving, 10.2f), 0f);
            Spawn("FlowerPot", props, new Vector3(21f, Paving, 11.1f), 0f);
            Spawn("FlowerPot", props, new Vector3(21f, Paving, 17.1f), 0f);
            Spawn("Bucket_Red", props, new Vector3(44.2f, Paving, 18f), 0f);
        }

        private static void BuildRooms(Transform parent, Dictionary<string, HouseInfo> houses, Dictionary<string, Transform> placed)
        {
            int layer = LayerMask.NameToLayer("Room");
            if (layer < 0)
            {
                Debug.LogWarning("Mini village: no \"Room\" layer; run Water Chaos/Setup Room Temperature once, then rebuild.");
                return;
            }

            foreach (KeyValuePair<string, Transform> house in placed)
            {
                if (house.Key == "Pool House")
                    continue;

                foreach (KeyValuePair<string, Bounds> room in houses[HouseKey(house.Key)].Rooms)
                {
                    string displayName = house.Key == room.Key ? room.Key : $"{house.Key} · {room.Key}";
                    var go = new GameObject(displayName) { layer = layer };
                    go.transform.SetParent(parent, false);
                    go.transform.SetPositionAndRotation(house.Value.TransformPoint(room.Value.center), house.Value.rotation);

                    // RequireComponent adds the trigger BoxCollider and the kinematic Rigidbody.
                    RoomTemperature temperature = go.AddComponent<RoomTemperature>();
                    var box = go.GetComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.center = Vector3.zero;
                    box.size = room.Value.size;
                    var body = go.GetComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;

                    var data = new SerializedObject(temperature);
                    data.FindProperty("_displayName").stringValue = displayName;
                    data.FindProperty("_startTemperature").floatValue = 25f;
                    data.FindProperty("_minTemperature").floatValue = -100f;
                    data.FindProperty("_coolingLimit").floatValue = 100f;
                    data.FindProperty("_initialFireHeat").floatValue = 5f;
                    data.FindProperty("_extinguishHeatLoss").floatValue = 5f;
                    data.FindProperty("_heatInterval").floatValue = 5f;
                    data.FindProperty("_heatPerInterval").floatValue = 1f;
                    data.FindProperty("_overheatCooling").floatValue = 2.5f;
                    data.FindProperty("_reigniteDelay").floatValue = 15f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void SetupRoomVisitor(GameObject player)
        {
            if (player.GetComponent<PlayerInventory>() == null)
                return;

            if (!player.TryGetComponent(out RoomVisitor visitor))
                visitor = player.AddComponent<RoomVisitor>();
            if (!player.TryGetComponent(out RoomTemperatureUI hud))
                hud = player.AddComponent<RoomTemperatureUI>();

            var data = new SerializedObject(hud);
            data.FindProperty("_visitor").objectReferenceValue = visitor;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // ───────────── Lighting ─────────────

        private static void SetupLighting(Scene scene, Transform lighting)
        {
            GameObject sunObject = FindRoot(scene, "Directional Light");
            if (sunObject != null)
            {
                Light sun = sunObject.GetComponent<Light>();
                sunObject.transform.rotation = Quaternion.Euler(34f, -38f, 0f);
                sun.color = new Color(1f, 0.88f, 0.72f);
                sun.intensity = 1.35f;
                sun.shadows = LightShadows.Soft;
                Move(sunObject, lighting, null);
            }

            Transform environment = Group(lighting, "EnvironmentLighting");
            Move(FindRoot(scene, "Global Volume"), environment, null);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.62f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.30f, 0.26f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.82f, 0.92f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 190f;

            Transform probes = Group(lighting, "ReflectionProbes");
            var probeObject = new GameObject("Village Reflection Probe");
            probeObject.transform.SetParent(probes, false);
            probeObject.transform.position = new Vector3(5f, 8f, 0f);
            var probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.size = new Vector3(140f, 40f, 110f);
            probe.resolution = 128;
        }

        // ───────────── Helpers ─────────────

        private static GameObject Spawn(string prop, Transform parent, Vector3 position, float yaw)
        {
            GameObject prefab = VillageProps.Get(prop);
            return prefab == null ? null : Instance(prefab, parent, position, yaw);
        }

        private static GameObject Instance(GameObject prefab, Transform parent, Vector3 position, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        private static GameObject InstanceAsset(string path, Transform parent, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("Mini village: prefab not found " + path);
                return null;
            }
            return Instance(prefab, parent, position, yaw);
        }

        /// <summary>Reused tree from FirefightersPack; adds a trunk collider (the pack has none) on the instance only.</summary>
        private static void Tree(Transform parent, string kind, Vector3 position, float scale)
        {
            GameObject tree = InstanceAsset(TreePath + kind + ".prefab", parent, position, Random.Range(0f, 360f));
            if (tree == null)
                return;

            tree.transform.localScale *= scale;
            Bounds bounds = WorldBounds(tree);
            float height = Mathf.Max(bounds.size.y, 1f);
            var capsule = tree.AddComponent<CapsuleCollider>();
            Vector3 lossy = tree.transform.lossyScale;
            capsule.radius = 0.3f / Mathf.Max(lossy.x, 0.01f);
            capsule.height = height * 0.6f / Mathf.Max(lossy.y, 0.01f);
            capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
            MarkStatic(tree);
        }

        /// <summary>Static fence or hedge pieces (2 m each) along X, skipping the gap ranges.</summary>
        private static void FenceAlongX(Transform parent, string piece, float z, float x0, float x1, Vector2[] gaps, float y = 0f)
        {
            for (float x = x0; x < x1 - 0.1f; x += 2f)
            {
                float end = Mathf.Min(x + 2f, x1);
                if (gaps.Any(g => end > g.x && x < g.y))
                    continue;
                GameObject go = Spawn(piece, parent, new Vector3((x + end) * 0.5f, y, z), 0f);
                go.transform.localScale = new Vector3((end - x) / 2f, 1f, 1f);
                MarkStatic(go);
            }
        }

        private static void FenceAlongZ(Transform parent, string piece, float x, float z0, float z1, Vector2[] gaps, float y = 0f)
        {
            for (float z = z0; z < z1 - 0.1f; z += 2f)
            {
                float end = Mathf.Min(z + 2f, z1);
                if (gaps.Any(g => end > g.x && z < g.y))
                    continue;
                GameObject go = Spawn(piece, parent, new Vector3(x, y, (z + end) * 0.5f), 90f);
                go.transform.localScale = new Vector3((end - z) / 2f, 1f, 1f);
                MarkStatic(go);
            }
        }

        private static GameObject Bake(PropModel model, string name, Transform parent, bool colliders = true)
        {
            GameObject go = VillageAssets.CreateMeshObject(name, model.Mesh, name);
            if (colliders)
                VillageAssets.AddColliders(go, model.Colliders);
            go.transform.SetParent(parent, false);
            MarkStatic(go);
            return go;
        }

        /// <summary>Moves an existing scene object and sets it on the surface at <paramref name="surface"/> by its render bounds.</summary>
        private static void PlaceExisting(GameObject go, Transform parent, Vector3 position, float surface)
        {
            if (go == null)
                return;
            Move(go, parent, position);
            PlaceOnSurface(go, surface);
        }

        private static void PlaceOnSurface(GameObject go, float surface)
        {
            Bounds bounds = WorldBounds(go);
            if (bounds.size == Vector3.zero)
                return;
            go.transform.position += Vector3.up * (surface - bounds.min.y + 0.01f);
        }

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>()
                .Where(r => !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void Move(GameObject go, Transform parent, Vector3? position)
        {
            if (go == null)
                return;
            go.transform.SetParent(parent, true);
            if (position.HasValue)
                go.transform.position = position.Value;
        }

        private static void Remove(Scene scene, string name)
        {
            GameObject go = FindRoot(scene, name);
            if (go != null)
                Object.DestroyImmediate(go);
        }

        private static void SetFloat(Object target, string field, float value)
        {
            if (target == null)
                return;
            var data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"Mini village: {target.GetType().Name} has no field {field}");
                return;
            }
            property.floatValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void MarkStatic(GameObject go)
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            foreach (Transform t in go.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (go == null)
                Debug.LogWarning("Mini village: root object not found: " + name);
            return go;
        }
    }
}
