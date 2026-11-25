using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

/// <summary>
/// Builds the residential test scene from existing project assets: Streetwright kit (house shell, stairs,
/// fences, road), VNB Home Set (furniture, garden props), FirefightersPack (vehicles, trees).
/// Gameplay objects (player, UI, pumps, hoses, nozzle, hydrant, fire, lake, tank) are taken from
/// SampleScene by copying the scene, so all their references stay intact.
/// Re-running the menu item rebuilds the scene from scratch; SampleScene is never modified.
/// </summary>
public static class ResidentialEnvironmentBuilder
{
    public const string ScenePath = "Assets/WaterChaos/Scenes/WaterChaos_ResidentialEnvironment.unity";
    private const string SourceScenePath = "Assets/Scenes/SampleScene.unity";

    private const string KitPath = "Assets/Downloads/Streetwright/glTF/Meshes/SM_Kit_";
    private const string KitMaterialPath = "Assets/Downloads/Streetwright/Materials/MI_TS_";
    private const string HomePath = "Assets/Downloads/VNB - Home Set/Prefabs/";
    private const string FirePackPath = "Assets/Downloads/FireFighter/FirefightersPack/Models/Prefabs/";
    private const string FirePrefabPath = "Assets/WaterChaos/Prefabs/Cartoon Fire.prefab";
    private const string GrassMaterialPath = "Assets/Downloads/VNB - Home Set/Materials/Terrain.mat";

    private const string Style = "MediterraneanVilla";
    // Light plaster on both faces, so interior partitions do not show the facade colour.
    private const string InteriorWalls = "DesertModern_Walls";

    // Heights in meters (m): floor tops and wall top of the two-storey house.
    private const float Floor1 = 0.04f;
    private const float Floor2 = 3.04f;
    private const float WallTop = 6.04f;

    private static readonly Color WarmLight = new Color(1f, 0.85f, 0.65f);
    private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

    [MenuItem("Tools/Water Chaos/Build Residential Environment")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        MaterialCache.Clear();
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
        {
            Debug.LogError("Residential builder: could not copy " + SourceScenePath);
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemoveTestGeometry(scene);

        Transform environment = new GameObject("Environment").transform;
        Transform house = new GameObject("House").transform;
        Transform gameplay = new GameObject("Gameplay").transform;

        SetupGroundAndSun(scene);
        BuildHouse(house);
        BuildRoadAndFrontYard(Group(environment, "Road"), Group(environment, "FrontYard"));
        BuildGarden(Group(environment, "Garden"), Group(environment, "Trees"));
        SetupGameplay(scene, gameplay, Group(environment, "PoolArea"), Group(environment, "LakeShore"));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Residential builder: scene saved to " + ScenePath);
    }

    // ───────────────────────────── Scene clean-up ─────────────────────────────

    private static void RemoveTestGeometry(Scene scene)
    {
        // Debug primitives and the primitive tank from the test scene; the other tank becomes the pool.
        foreach (string name in new[] { "House", "Sphere", "ff", "Push", "Reservoir B (1)" })
        {
            GameObject go = FindRoot(scene, name);
            if (go != null)
                Object.DestroyImmediate(go);
        }
    }

    private static void SetupGroundAndSun(Scene scene)
    {
        GameObject ground = FindRoot(scene, "Ground");
        ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
        MarkStatic(ground);

        Light sun = FindRoot(scene, "Directional Light").GetComponent<Light>();
        sun.transform.rotation = Quaternion.Euler(38f, 25f, 0f);
        sun.color = new Color(1f, 0.93f, 0.82f);
    }

    // ───────────────────────────── House ─────────────────────────────

    private static void BuildHouse(Transform house)
    {
        Transform floor1 = Group(house, "FirstFloor");
        Transform floor2 = Group(house, "SecondFloor");
        Transform roof = Group(house, "Roof");
        Transform balcony = Group(house, "Balcony");
        Transform stairs = Group(house, "Stairs");

        BuildFirstFloorShell(Group(floor1, "Structure"));
        BuildSecondFloorShell(Group(floor2, "Structure"));
        BuildRoof(roof);
        BuildStairs(stairs);
        BuildBalconyAndPorch(balcony);
        FurnishFirstFloor(Group(floor1, "Furniture"), Group(floor1, "Lights"));
        FurnishSecondFloor(Group(floor2, "Furniture"), Group(floor2, "Lights"));
    }

    private static void BuildFirstFloorShell(Transform parent)
    {
        FloorGrid(parent, "Floor", -6, 8, 0, 10, Floor1, 1f, null, null);

        // Exterior walls (spec chars: W wall, N window, M wide window, D wide doorway).
        Run(parent, new Vector2(-6, 0), new Vector2(8, 0), Floor1, Vector2.down, "WMWNW" + "WDW" + "WMW");
        Run(parent, new Vector2(-6, 10), new Vector2(8, 10), Floor1, Vector2.up, "WMMW" + "DWW" + "WMW");
        Run(parent, new Vector2(-6, 0), new Vector2(-6, 10), Floor1, Vector2.left, "WNWNW" + "WMWW");
        Run(parent, new Vector2(8, 0), new Vector2(8, 10), Floor1, Vector2.right, "WMWW" + "WWMW");

        // Interior walls: hall | living+dining | office+kitchen, plus the wall beside the stairs.
        Run(parent, new Vector2(0, 0), new Vector2(0, 10), Floor1, Vector2.right, "WDWW" + "WDWW", InteriorWalls);
        Run(parent, new Vector2(4, 0), new Vector2(4, 10), Floor1, Vector2.right, "WDWW" + "WWWD", InteriorWalls);
        Run(parent, new Vector2(-6, 5), new Vector2(0, 5), Floor1, Vector2.up, "WWDWW", InteriorWalls);
        Run(parent, new Vector2(4, 5), new Vector2(8, 5), Floor1, Vector2.up, "WWWW", InteriorWalls);
        Run(parent, new Vector2(2, 4), new Vector2(2, 8), Floor1, Vector2.left, "WWWW", InteriorWalls);

        Corners(parent, Floor1);
        BandAround(parent, Floor2 - 0.12f);

        // Front entrance: stoop and canopy.
        Edge(parent, "Stoop_Single", new Vector2(1, 0), new Vector2(3, 0), Floor1 - 0.2f, Vector2.down, null, true, new Vector3(2f, 1f, 1f));
        Edge(parent, "Awning_Canopy_100", new Vector2(1, 0), new Vector2(3, 0), Floor1 + 2.35f, Vector2.down, null, false, new Vector3(2f, 1f, 1f));

        // Fireplace: chimney breast from the living room floor through the roof, with a stone hearth.
        Place(Kit("Chimney", parent, 90f), new Vector3(-5.53f, Floor1, 2.5f));
        Place(Kit("Chimney_Top", parent, 90f), new Vector3(-5.53f, Floor1 + 7.1f, 2.5f));
        Place(Kit("Stoop_Single", parent, 90f), new Vector3(-4.7f, Floor1 - 0.02f, 2.5f));
    }

    private static void BuildSecondFloorShell(Transform parent)
    {
        // Stairwell opening above the stairs (x 2..4, z 4..8).
        FloorGrid(parent, "Floor", -6, 8, 0, 10, Floor2, 1f, null, (x, z) => x >= 2 && x < 4 && z >= 4 && z < 8);

        Run(parent, new Vector2(-6, 0), new Vector2(8, 0), Floor2, Vector2.down, "WMWWW" + "WNNW" + "WMW");
        Run(parent, new Vector2(-6, 10), new Vector2(8, 10), Floor2, Vector2.up, "WMMW" + "DWW" + "WNNW");
        Run(parent, new Vector2(-6, 0), new Vector2(-6, 10), Floor2, Vector2.left, "WNWNW" + "WMWW");
        Run(parent, new Vector2(8, 0), new Vector2(8, 10), Floor2, Vector2.right, "WMWW" + "WNWNW");

        Run(parent, new Vector2(0, 0), new Vector2(0, 10), Floor2, Vector2.right, "WDWW" + "WDWW", InteriorWalls);
        Run(parent, new Vector2(4, 0), new Vector2(4, 10), Floor2, Vector2.right, "WDWW" + "WWWD", InteriorWalls);
        Run(parent, new Vector2(-6, 5), new Vector2(0, 5), Floor2, Vector2.up, "WWWWWW", InteriorWalls);
        Run(parent, new Vector2(4, 5), new Vector2(8, 5), Floor2, Vector2.up, "WWWW", InteriorWalls);

        // Railings around the stairwell.
        Run(parent, new Vector2(2, 4), new Vector2(2, 8), Floor2, Vector2.left, "RRRR");
        Run(parent, new Vector2(2, 4), new Vector2(4, 4), Floor2, Vector2.down, "RR");
        Place(Kit("Railing_Post", parent, 0f), new Vector3(2f, Floor2, 4f));

        Corners(parent, Floor2);
        BandAround(parent, WallTop - 0.12f);
    }

    private static void BuildRoof(Transform parent)
    {
        FloorGrid(parent, "Roof_Flat", -6, 8, 0, 10, WallTop + 0.15f, 1f, null, null);
        Run(parent, new Vector2(-6, 0), new Vector2(8, 0), WallTop, Vector2.down, new string('H', 14));
        Run(parent, new Vector2(-6, 10), new Vector2(8, 10), WallTop, Vector2.up, new string('H', 14));
        Run(parent, new Vector2(-6, 0), new Vector2(-6, 10), WallTop, Vector2.left, new string('H', 10));
        Run(parent, new Vector2(8, 0), new Vector2(8, 10), WallTop, Vector2.right, new string('H', 10));
    }

    private static void BuildStairs(Transform parent)
    {
        // The 1 m kit flight is stretched to 2 m wide (the player capsule is 1 m wide) and to the storey height.
        // The mesh rises towards its local -X; yaw 90 turns that to world +Z.
        float heightScale = (Floor2 - Floor1) / 3.117f;
        GameObject flight = Kit("Stair_Straight", parent, 90f, null, false, new Vector3(1f, heightScale, 2f));
        PlaceMin(flight, new Vector3(2f, Floor1, 4f));
        Ramp(parent, "StairRamp", new Vector3(3f, Floor1, 4f), new Vector3(3f, Floor2, 8f), 2f);
    }

    private static void BuildBalconyAndPorch(Transform parent)
    {
        // Balcony deck on the second floor (x -1..5, z 10..12) with railings.
        FloorGrid(parent, "Floor_Deck", -1, 5, 10, 12, Floor2, 1f, null, null);
        Run(parent, new Vector2(-1, 12), new Vector2(5, 12), Floor2, Vector2.up, "RRRRRR");
        Run(parent, new Vector2(-1, 10), new Vector2(-1, 12), Floor2, Vector2.left, "RR");
        Run(parent, new Vector2(5, 10), new Vector2(5, 12), Floor2, Vector2.right, "RR");
        foreach (float x in new[] { -1f, 2f, 5f })
            Place(Kit("Railing_Post", parent, 0f), new Vector3(x, Floor2, 12f));
        Run(parent, new Vector2(-1, 12), new Vector2(5, 12), Floor2 - 0.335f, Vector2.up, "bbbbbb");

        // Covered porch underneath, carried by posts.
        FloorGrid(parent, "Floor_Deck", -1, 5, 10, 13, Floor1, 1f, null, null);
        foreach (float x in new[] { -0.85f, 2f, 4.85f })
            Place(Kit("Post", parent, 0f, null, true, new Vector3(1f, (Floor2 - Floor1 - 0.1f) / 3f, 1f)), new Vector3(x, Floor1, 11.85f));
    }

    private static void FurnishFirstFloor(Transform parent, Transform lights)
    {
        float y = Floor1;

        // Living room (x -6..0, z 0..5): fireplace, sofa, coffee table, rug, armchairs, shelves.
        Home("Livingroom/Sofa", parent, -90f, new Vector3(-2.3f, y, 2.5f));
        Home("Livingroom/Coffee Table", parent, 90f, new Vector3(-3.4f, y, 2.5f));
        foreach (Vector2 rug in new[] { new Vector2(-3.9f, 2f), new Vector2(-2.9f, 2f), new Vector2(-3.9f, 3f), new Vector2(-2.9f, 3f) })
            Home("Decoration/Rug Tile", parent, 0f, new Vector3(rug.x, y + 0.002f, rug.y), false);
        Home("Livingroom/Chair 3", parent, 0f, new Vector3(-3.4f, y, 0.75f));
        Home("Livingroom/Chair 3", parent, 180f, new Vector3(-3.4f, y, 4.2f));
        Home("Livingroom/Modern Shelves", parent, 180f, new Vector3(-5.2f, y, 4.75f));
        Home("Decoration/Books I", parent, 180f, new Vector3(-5.4f, y + 0.95f, 4.75f), false);
        Home("Decoration/Lamp 1", parent, 0f, new Vector3(-0.6f, y, 4.4f));
        Home("Decoration/Plant Deco 1", parent, 0f, new Vector3(-0.6f, y, 0.6f));
        WallItem("Decoration/Painting 2", parent, 180f, new Vector3(-1.6f, y + 1.7f, 4.87f));
        CeilingLamp(parent, lights, new Vector3(-3f, Floor2, 2.5f), Floor1);
        PointLight(lights, "Hearth Glow", new Vector3(-4.8f, y + 0.45f, 2.5f), 3.5f, 1.2f, new Color(1f, 0.55f, 0.2f));

        // Dining room (x -6..0, z 5..10).
        Home("Livingroom/Dining Table", parent, 0f, new Vector3(-3f, y, 7.6f));
        foreach (float x in new[] { -4f, -3f, -2f })
        {
            Home("Livingroom/Chair 2", parent, 0f, new Vector3(x, y, 6.85f));
            Home("Livingroom/Chair 2", parent, 180f, new Vector3(x, y, 8.35f));
        }
        Home("Bedroom/Drawer A", parent, 0f, new Vector3(-5f, y, 5.48f));
        Home("Decoration/Vase 1", parent, 0f, new Vector3(-5.3f, y + 0.95f, 5.48f), false);
        Home("Decoration/Plant Deco 1", parent, 0f, new Vector3(-0.6f, y, 9.4f));
        WallItem("Decoration/Painting 1", parent, 0f, new Vector3(-5f, y + 1.65f, 5.13f));
        CeilingLamp(parent, lights, new Vector3(-3f, Floor2, 7.6f), Floor1);

        // Kitchen (x 4..8, z 5..10).
        Home("Kitchen/Oven", parent, -90f, new Vector3(7.5f, y, 5.6f));
        Home("Kitchen/Counter (Connected)", parent, -90f, new Vector3(7.55f, y, 6.55f));
        Home("Kitchen/Counter (Connected - Sink)", parent, -90f, new Vector3(7.55f, y, 7.55f));
        Home("Kitchen/Fridge", parent, -90f, new Vector3(7.48f, y, 8.55f));
        Home("Kitchen/Microwave", parent, -90f, new Vector3(7.6f, y + 0.95f, 6.55f), false);
        Home("Livingroom/Round Table", parent, 0f, new Vector3(5.6f, y, 7f));
        Home("Livingroom/Stool", parent, 0f, new Vector3(4.75f, y, 7f));
        Home("Livingroom/Stool", parent, 0f, new Vector3(6.45f, y, 7f));
        Home("Kitchen/Simple Coffee", parent, 0f, new Vector3(5.6f, y + 0.65f, 7f), false);
        Home("Kitchen/Trash Bin", parent, 0f, new Vector3(4.4f, y, 5.4f));
        CeilingLamp(parent, lights, new Vector3(6f, Floor2, 7f), Floor1);

        // Office (x 4..8, z 0..5).
        Home("Bedroom/Drawer B", parent, 180f, new Vector3(6f, y, 4.49f));
        Home("Gaming Room/Laptop", parent, 180f, new Vector3(6f, y + 0.95f, 4.45f), false);
        Home("Other/Printer", parent, 180f, new Vector3(5.4f, y + 0.95f, 4.5f), false);
        Home("Gaming Room/Gamer Chair", parent, 0f, new Vector3(6f, y, 3.5f));
        Home("Livingroom/Modern Shelves Variant", parent, -90f, new Vector3(7.76f, y, 4.2f));
        Home("Decoration/Plant Deco 1", parent, 0f, new Vector3(7.45f, y, 0.55f));
        Home("Decoration/Lamp 3", parent, 0f, new Vector3(4.5f, y, 4.5f));
        CeilingLamp(parent, lights, new Vector3(6f, Floor2, 2.5f), Floor1);

        // Entrance hall (x 0..4).
        Home("Decoration/Plant Deco 1", parent, 0f, new Vector3(0.45f, y, 0.45f));
        Home("Decoration/Plant Deco 1", parent, 0f, new Vector3(3.55f, y, 0.45f));
        WallItem("Decoration/Painting 3", parent, -90f, new Vector3(1.88f, y + 1.7f, 6f));
        CeilingLamp(parent, lights, new Vector3(1f, Floor2, 2f), Floor1);
        CeilingLamp(parent, lights, new Vector3(1f, Floor2, 9f), Floor1);
    }

    private static void FurnishSecondFloor(Transform parent, Transform lights)
    {
        float y = Floor2;

        // Master bedroom (x -6..0, z 0..5).
        Home("Bedroom/2P Bed", parent, 180f, new Vector3(-3.2f, y, 3.61f));
        foreach (float x in new[] { -4.85f, -1.55f })
        {
            Home("Bedroom/Bed Table", parent, 180f, new Vector3(x, y, 4.47f));
            Home("Decoration/Small Lamp 1", parent, 0f, new Vector3(x, y + 0.97f, 4.47f), false);
        }
        Home("Bedroom/Wardrobe", parent, 0f, new Vector3(-2.2f, y, 0.4f));
        Home("Bedroom/Drawer A Variant", parent, 0f, new Vector3(-4f, y, 0.49f));
        Home("Decoration/Rug Tile", parent, 0f, new Vector3(-3.7f, y + 0.002f, 1.8f), false);
        Home("Decoration/Rug Tile", parent, 0f, new Vector3(-2.7f, y + 0.002f, 1.8f), false);
        WallItem("Decoration/Painting 3", parent, 180f, new Vector3(-3.2f, y + 1.75f, 4.87f));
        CeilingLamp(parent, lights, new Vector3(-3f, WallTop, 2.5f), Floor2);

        // Second bedroom / kids room (x -6..0, z 5..10).
        Home("Bedroom/1P Bed", parent, 0f, new Vector3(-5.15f, y, 6.39f));
        Home("Bedroom/Bed Table", parent, 0f, new Vector3(-4.05f, y, 5.5f));
        Home("Decoration/Small Lamp 2", parent, 0f, new Vector3(-4.05f, y + 0.97f, 5.5f), false);
        Home("Bedroom/Drawer 2", parent, -90f, new Vector3(-0.45f, y, 9.2f));
        Home("Gaming Room/Puff Variant", parent, 0f, new Vector3(-2.6f, y, 8.6f));
        Home("Kid Room/Toy Car 1", parent, 30f, new Vector3(-2f, y, 7.2f), false);
        Home("Kid Room/Toy Plane", parent, -20f, new Vector3(-2.8f, y, 7.6f), false);
        Home("Kid Room/Toy House", parent, 0f, new Vector3(-1.6f, y, 8f), false);
        Home("Kid Room/Toy Figure 1", parent, 0f, new Vector3(-2.3f, y, 6.6f), false);
        WallItem("Gaming Room/Poster 2", parent, 90f, new Vector3(-5.88f, y + 1.6f, 9f));
        CeilingLamp(parent, lights, new Vector3(-3f, WallTop, 7.5f), Floor2);

        // Third bedroom / games room (x 4..8, z 0..5).
        Home("Bedroom/1P Bed 2", parent, 180f, new Vector3(7.2f, y, 3.63f));
        Home("Gaming Room/Table (Modern)", parent, 180f, new Vector3(5.2f, y, 4.6f));
        Home("Gaming Room/TV 1", parent, 180f, new Vector3(5.2f, y + 0.507f, 4.6f), false);
        Home("Gaming Room/Puff", parent, 180f, new Vector3(5.2f, y, 3f));
        Home("Bedroom/Drawer 3", parent, 90f, new Vector3(4.46f, y, 3.6f));
        WallItem("Gaming Room/Poster 1", parent, 0f, new Vector3(4.5f, y + 1.7f, 0.13f));
        CeilingLamp(parent, lights, new Vector3(6f, WallTop, 2.5f), Floor2);

        // Bathroom (x 4..8, z 5..10).
        Home("Bathroom/Bathtub-Shower", parent, 90f, new Vector3(6.8f, y, 5.58f));
        Home("Bathroom/Toilet", parent, -90f, new Vector3(7.54f, y, 7.5f));
        Home("Bathroom/Sink", parent, 180f, new Vector3(7.45f, y, 9.54f));
        WallItem("Bathroom/Mirror", parent, 180f, new Vector3(7.45f, y + 1.65f, 9.86f));
        Home("Garage/Washing Machine", parent, 90f, new Vector3(4.5f, y, 6f));
        Home("Bathroom/Towel Pile", parent, 0f, new Vector3(4.5f, y + 0.952f, 6f), false);
        CeilingLamp(parent, lights, new Vector3(6f, WallTop, 7.5f), Floor2);

        // Landing and balcony furniture.
        Home("Decoration/Plant Deco 1", parent, 0f, new Vector3(0.45f, y, 0.45f));
        CeilingLamp(parent, lights, new Vector3(1f, WallTop, 5f), Floor2);
        Home("Backyard/Chair (Outside)", parent, 90f, new Vector3(-0.4f, y, 11.2f));
        Home("Backyard/Chair (Outside)", parent, -90f, new Vector3(4.1f, y, 11.2f));
        Home("Decoration/Plant Deco 1", parent, 0f, new Vector3(4.4f, y, 10.45f));
    }

    // ───────────────────────────── Road and front yard ─────────────────────────────

    private static void BuildRoadAndFrontYard(Transform road, Transform yard)
    {
        // Asphalt road (z -17..-11), sidewalk (z -11..-9.2), driveway and front path.
        FloorGrid(road, "Floor", -34, 34, -17, -11, 0.02f, 2f, "Street", null);
        for (int row = 0; row < 3; row++)
            for (int x = -34; x < 34; x += 2)
                Kit("Path_Slab", road, 0f, "Street", true, new Vector3(2f, 1f, 1f)).transform.position = new Vector3(x + 2, 0.04f, -9.2f - row * 0.6f);
        for (float z = -9.2f; z < 6.8f; z += 2f)
            for (int x = 10; x < 14; x += 2)
                Kit("Floor", road, 0f, "Street", true, new Vector3(2f, 1f, 2f)).transform.position = new Vector3(x + 2, 0.03f, z + 2f);
        for (int i = 0; i < 14; i++)
            Kit("Path_Slab", yard, 0f, "Street", true, new Vector3(2f, 1f, 1f)).transform.position = new Vector3(3f, 0.04f, -0.9f - i * 0.6f);

        // Street lamps with warm lights.
        foreach (float x in new[] { -14f, -4f, 6f, 16f, 26f })
        {
            Place(Kit("Prop_LampPost", road, 0f), new Vector3(x, 0.04f, -10.7f));
            PointLight(road, "Street Light", new Vector3(x, 3.5f, -10.7f), 12f, 2f, WarmLight);
        }

        // Picket fence with openings for the front path (x 1..3) and the driveway (x 10..14).
        var fence = new System.Text.StringBuilder();
        for (int x = -17; x < 32; x++)
            fence.Append((x >= 1 && x < 3) || (x >= 10 && x < 14) ? '-' : 'F');
        Run(yard, new Vector2(-17, -8.9f), new Vector2(32, -8.9f), 0f, Vector2.down, fence.ToString());
        for (int x = -17; x <= 32; x++)
        {
            bool left = x > -17 && fence[x - 1 + 17] == 'F';
            bool right = x < 32 && fence[x + 17] == 'F';
            if (left || right)
                Place(Kit("Fence_Picket_Post", yard, 0f), new Vector3(x, 0f, -8.9f));
        }

        // Front garden.
        for (float x = -5.4f; x <= -0.5f; x += 1.2f)
            Home("Backyard/Bush", yard, x * 40f, new Vector3(x, 0f, -0.75f), false);
        for (float x = 4.6f; x <= 7.5f; x += 1.2f)
            Home("Backyard/Bush Variant", yard, x * 40f, new Vector3(x, 0f, -0.75f), false);
        Home("Backyard/Mailbox", yard, 180f, new Vector3(3.8f, 0f, -8.3f));
        Place(Kit("Prop_WheelieBin", yard, 180f), new Vector3(15f, 0f, -8f));
        Place(Kit("Prop_WheelieBin", yard, 180f), new Vector3(15.8f, 0f, -8f));
        Place(Kit("Garden_Bench", yard, 180f), new Vector3(-8f, 0f, -7.6f));

        // Vehicles: fire truck at the kerb, fire service car in the driveway.
        Prop(FirePackPath + "Vehicles/FireTruck1.prefab", yard, 90f, new Vector3(-7f, 0.02f, -14f), true);
        Prop(FirePackPath + "Vehicles/FireCar1.prefab", yard, 180f, new Vector3(12f, 0.03f, 2.5f), true);
        Prop(FirePackPath + "Equipment/TrafficCone.prefab", yard, 0f, new Vector3(-12f, 0.02f, -12.4f), false);
        Prop(FirePackPath + "Equipment/TrafficCone.prefab", yard, 0f, new Vector3(-2f, 0.02f, -12.4f), false);
    }

    // ───────────────────────────── Garden, hedges, trees ─────────────────────────────

    private static void BuildGarden(Transform garden, Transform trees)
    {
        // Boundary hedges (west, north, east).
        Run(garden, new Vector2(-17, -9), new Vector2(-17, 30), 0f, Vector2.left, new string('E', 39));
        Run(garden, new Vector2(-17, 30), new Vector2(32, 30), 0f, Vector2.up, new string('E', 49));
        Run(garden, new Vector2(32, -9), new Vector2(32, 30), 0f, Vector2.right, new string('E', 39));

        // Garden shed in the west corner.
        Place(Kit("Prop_GardenShed", garden, 90f), new Vector3(-13f, 0f, 22f));
        Place(Kit("Garden_Bench", garden, -90f), new Vector3(-15.8f, 0f, 14f));

        // Stepping-stone paths: patio -> shed, patio -> lake dock.
        StonePath(garden, new Vector2(-5.3f, 18f), new Vector2(-10.5f, 21.5f));
        StonePath(garden, new Vector2(7.3f, 17f), new Vector2(11.7f, 21f));

        // Trees and bushes (FirefightersPack + VNB).
        var treeSpots = new (string, Vector2)[]
        {
            (HomePath + "Backyard/Big Tree 1.prefab", new Vector2(-12f, -5f)),
            (HomePath + "Backyard/Big Tree 2.prefab", new Vector2(-14.5f, 2f)),
            (FirePackPath + "Enviroment/Tree2.prefab", new Vector2(20f, -4f)),
            (HomePath + "Backyard/Big Tree 1 Variant.prefab", new Vector2(27f, -5f)),
            (FirePackPath + "Enviroment/Tree1.prefab", new Vector2(-14f, 10f)),
            (FirePackPath + "Enviroment/Tree3.prefab", new Vector2(-9f, 27.5f)),
            (HomePath + "Backyard/Big Tree 2 Variant.prefab", new Vector2(9f, 27.5f)),
            (FirePackPath + "Enviroment/Tree2.prefab", new Vector2(30f, 16f)),
            (FirePackPath + "Enviroment/Tree1.prefab", new Vector2(29.5f, 27.5f)),
            (FirePackPath + "Enviroment/Tree3.prefab", new Vector2(22f, 28.2f)),
            (HomePath + "Backyard/Big Tree 1 Variant.prefab", new Vector2(14f, 27f)),
            (HomePath + "Backyard/Big Tree 2.prefab", new Vector2(20f, 8f)),
            (FirePackPath + "Enviroment/Tree3burned.prefab", new Vector2(-13.5f, 18.5f)),
        };
        foreach (var (path, spot) in treeSpots)
            Tree(path, trees, spot);

        var bushSpots = new[]
        {
            new Vector2(15f, 16.8f), new Vector2(15.1f, 25.2f), new Vector2(28.9f, 22f), new Vector2(21f, 26.4f),
            new Vector2(26.5f, 15.6f), new Vector2(-15.5f, 25f), new Vector2(9f, 12f), new Vector2(-7.5f, 12.5f),
        };
        for (int i = 0; i < bushSpots.Length; i++)
            Prop(FirePackPath + "Enviroment/Bush1.prefab", trees, i * 50f, new Vector3(bushSpots[i].x, 0f, bushSpots[i].y), false);
        for (int i = 0; i < 6; i++)
            Place(Kit("Shrub_Round", trees, i * 60f), new Vector3(-7f, 0f, 1f + i * 1.6f));
    }

    // ───────────────────────────── Gameplay: water sources, tools, fire ─────────────────────────────

    private static void SetupGameplay(Scene scene, Transform gameplay, Transform poolArea, Transform lakeShore)
    {
        Transform waterSources = Group(gameplay, "WaterSources");
        Transform pumps = Group(gameplay, "Pumps");
        Transform hoses = Group(gameplay, "Hoses");
        Transform nozzles = Group(gameplay, "Nozzles");
        Transform fires = Group(gameplay, "FireObjects");
        Transform hydrants = Group(gameplay, "Hydrants");

        BuildPool(FindRoot(scene, "Reservoir B"), waterSources, poolArea);
        BuildLake(FindRoot(scene, "Lake"), waterSources, lakeShore);

        // Existing pickables: pumps near each water source, transfer hoses, nozzle by the hydrant.
        Move(FindRoot(scene, "Pump"), pumps, new Vector3(-4.3f, 0.03f, 14.2f));
        Move(FindRoot(scene, "Pump (1)"), pumps, new Vector3(11.2f, 0f, 18.6f));
        GameObject[] transferHoses = scene.GetRootGameObjects().Where(g => g.name.StartsWith("TransferHose_20m")).ToArray();
        if (transferHoses.Length > 0) Move(transferHoses[0], hoses, new Vector3(-2.5f, 0.23f, 13.8f));
        if (transferHoses.Length > 1) Move(transferHoses[1], hoses, new Vector3(11.5f, 0.2f, 23f));
        Move(FindRoot(scene, "CoiledFireHoseYellow"), hoses, null);
        Move(FindRoot(scene, "FireHydrant_Red"), hydrants, new Vector3(-2.5f, -0.11f, -10f));
        Move(FindRoot(scene, "Nozzle"), nozzles, new Vector3(-1.2f, 0.34f, -9.9f));

        // Fires: the existing one at the garden shed, one more in the kitchen.
        Move(FindRoot(scene, "Cartoon Fire"), fires, new Vector3(-10.4f, 0.5f, 19.6f));
        var kitchenFire = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefabPath), fires);
        kitchenFire.name = "Cartoon Fire (Kitchen)";
        kitchenFire.transform.position = new Vector3(5.8f, Floor1 + 0.5f, 9.35f);

        Move(FindRoot(scene, "WindZone_Test"), gameplay, new Vector3(22f, 2.7f, 13f));

        GameObject player = FindRoot(scene, "Player");
        player.transform.SetPositionAndRotation(new Vector3(2f, 1f, -6.5f), Quaternion.identity);
    }

    private static void BuildPool(GameObject tank, Transform waterSources, Transform poolArea)
    {
        // Pool interior x -3..5, z 15..19; raised masonry rim 0.75 m; water depth 0.66 m.
        tank.name = "Pool";
        tank.transform.SetParent(waterSources, false);
        tank.transform.SetPositionAndRotation(new Vector3(1f, Floor1, 17f), Quaternion.identity);

        foreach (Transform child in tank.transform.Cast<Transform>().ToArray())
            if (child.name.StartsWith("Wall") || child.name == "Floor")
                Object.DestroyImmediate(child.gameObject);

        const float depth = 0.66f;
        var poolSize = new Vector2(7.8f, 3.8f);
        Transform volume = tank.transform.Find("Water Volume");
        var box = volume.GetComponent<BoxCollider>();
        volume.localPosition = Vector3.zero;
        box.center = new Vector3(0f, depth * 0.5f, 0f);
        box.size = new Vector3(poolSize.x, depth, poolSize.y);
        SetFloat(volume.GetComponent<WaterReservoirComponent>(), "_capacity", 20000f);
        SetFloat(volume.GetComponent<WaterReservoirComponent>(), "_initialVolume", 20000f);

        Transform water = tank.transform.Find("Water");
        water.localPosition = new Vector3(0f, depth * 0.5f, 0f);
        water.localScale = new Vector3(poolSize.x, depth, poolSize.y);
        var level = tank.GetComponent<ReservoirLevelView>();
        SetFloat(level, "_bottom", 0f);
        SetFloat(level, "_fullHeight", depth);

        // Rim and pool floor are children so looking at them shows the pool (WaterReservoirTarget).
        Transform rim = Group(tank.transform, "Rim");
        Run(rim, new Vector2(-3, 15), new Vector2(5, 15), 0f, Vector2.down, new string('P', 8));
        Run(rim, new Vector2(-3, 19), new Vector2(5, 19), 0f, Vector2.up, new string('P', 8));
        Run(rim, new Vector2(-3, 15), new Vector2(-3, 19), 0f, Vector2.left, new string('P', 4));
        Run(rim, new Vector2(5, 15), new Vector2(5, 19), 0f, Vector2.right, new string('P', 4));
        foreach (Vector2 c in new[] { new Vector2(-3, 15), new Vector2(5, 15), new Vector2(-3, 19), new Vector2(5, 19) })
            Place(Kit("Column_Square", rim, 0f, "Masonry", true, new Vector3(1f, 0.25f, 1f)), new Vector3(c.x, 0f, c.y));
        FloorGrid(Group(tank.transform, "PoolFloor"), "Floor", -3, 5, 15, 19, Floor1, 1f, "Street", null);

        // Paved patio around the pool joining the porch deck.
        for (int x = -5; x < 7; x++)
            for (int z = 13; z < 21; z++)
                if (!(x >= -3 && x < 5 && z >= 15 && z < 19))
                    Kit("Path_Slab", poolArea, 0f, "Street", true, new Vector3(1f, 1f, 1f / 0.6f)).transform.position = new Vector3(x + 1, 0.03f, z + 1);
        foreach (float x in new[] { -2f, 0f, 2f })
            Home("Backyard/Chair (Outside)", poolArea, 180f, new Vector3(x, 0.03f, 20.3f));
        Home("Backyard/Outside Umbrella", poolArea, 0f, new Vector3(4f, 0.03f, 20.3f));
        Home("Backyard/Simple Table (Outside)", poolArea, 90f, new Vector3(6.3f, 0.03f, 17f));
        Home("Backyard/Shower (Outside)", poolArea, 180f, new Vector3(6.4f, 0.03f, 13.9f));
        Home("Backyard/Outside Sofa (Alt)", poolArea, 90f, new Vector3(-4.4f, 0.03f, 17.2f));
        Home("Backyard/Outside Sofa", poolArea, 0f, new Vector3(3.6f, Floor1, 10.55f));
        Home("Garage/Tire", poolArea, 0f, new Vector3(6.4f, 0.03f, 20.4f), false);
    }

    private static void BuildLake(GameObject lake, Transform waterSources, Transform shore)
    {
        // Lake basin x 16..28, z 16.5..25.5 with a stone rim and a wooden dock on the west side.
        lake.transform.SetParent(waterSources, false);
        lake.transform.SetPositionAndRotation(new Vector3(22f, 0f, 21f), Quaternion.identity);
        var size = new Vector2(11.8f, 8.8f);

        Transform volume = lake.transform.Find("Water Volume");
        var box = volume.GetComponent<BoxCollider>();
        box.size = new Vector3(size.x, box.size.y, size.y);
        SetFloat(volume.GetComponent<WaterReservoirComponent>(), "_capacity", 80000f);
        SetFloat(volume.GetComponent<WaterReservoirComponent>(), "_initialVolume", 80000f);

        Transform surface = lake.transform.Find("Surface");
        surface.localScale = new Vector3(size.x, surface.localScale.y, size.y);

        Transform rim = Group(lake.transform, "Shore");
        float top = 0.85f;
        Run(rim, new Vector2(16, 16.5f), new Vector2(28, 16.5f), top, Vector2.down, new string('S', 12));
        Run(rim, new Vector2(16, 25.5f), new Vector2(28, 25.5f), top, Vector2.up, new string('S', 12));
        Run(rim, new Vector2(16, 16.5f), new Vector2(16, 25.5f), top, Vector2.left, new string('S', 9));
        Run(rim, new Vector2(28, 16.5f), new Vector2(28, 25.5f), top, Vector2.right, new string('S', 9));
        foreach (Vector2 c in new[] { new Vector2(16, 16.5f), new Vector2(28, 16.5f), new Vector2(16, 25.5f), new Vector2(28, 25.5f) })
            Place(Kit("Column_Square", rim, 0f, "Masonry", true, new Vector3(1.2f, top / 3f, 1.2f)), new Vector3(c.x, 0f, c.y));

        // Dock: ramp up from the lawn, deck over the rim into the water.
        Transform dock = Group(shore, "Dock");
        float deck = top + 0.01f;
        FloorGrid(dock, "Floor_Deck", 14, 19, 20, 22, deck, 1f, null, null);
        for (int i = 0; i < 2; i++)
        {
            GameObject flight = Kit("Stair_HalfFlight", dock, 180f, "Outdoor", false, new Vector3(1f, deck / 1.618f, 1f));
            PlaceMin(flight, new Vector3(12f, 0f, 20f + i));
        }
        Ramp(dock, "DockRamp", new Vector3(12f, 0f, 21f), new Vector3(14f, deck, 21f), 2f);
        Run(dock, new Vector2(16, 20), new Vector2(19, 20), deck, Vector2.down, "RRR");
        Run(dock, new Vector2(16, 22), new Vector2(19, 22), deck, Vector2.up, "RRR");
        Run(dock, new Vector2(19, 20), new Vector2(19, 22), deck, Vector2.right, "RR");

        string[] rocks = { "Backyard/Rock 1", "Backyard/Rock 2", "Backyard/Rock 3" };
        var rockSpots = new[] { new Vector3(18f, top, 16.5f), new Vector3(24.5f, top, 16.5f), new Vector3(28f, top, 20f), new Vector3(26f, top, 25.5f), new Vector3(19.5f, top, 25.5f), new Vector3(16f, top, 24f) };
        for (int i = 0; i < rockSpots.Length; i++)
            Home(rocks[i % rocks.Length], shore, i * 70f, rockSpots[i], false);
    }

    // ───────────────────────────── Placement helpers ─────────────────────────────

    /// <summary>Instantiates a Streetwright kit piece with the style material and optional mesh collision.</summary>
    private static GameObject Kit(string piece, Transform parent, float yaw, string material = null, bool collide = true, Vector3? scale = null)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(KitPath + piece + ".glb");
        if (asset == null)
        {
            Debug.LogError("Residential builder: missing kit piece " + piece);
            return new GameObject("Missing " + piece);
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (scale.HasValue)
            go.transform.localScale = scale.Value;

        Material kitMaterial = LoadKitMaterial(material ?? MaterialFor(piece));
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name == "M_Kit")
                    mats[i] = kitMaterial;
            r.sharedMaterials = mats;
        }

        if (collide)
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;

        MarkStatic(go);
        return go;
    }

    private static string MaterialFor(string piece)
    {
        if (piece.StartsWith("Wall")) return Style + "_Walls";
        if (piece.StartsWith("Window")) return "Windows";
        if (piece.StartsWith("Door")) return Style + "_Doors";
        if (piece.StartsWith("Roof") || piece.StartsWith("Trim") || piece.StartsWith("Awning")) return Style + "_Roofs";
        if (piece.StartsWith("Foundation") || piece.StartsWith("Chimney") || piece.StartsWith("Column") || piece.StartsWith("Stoop")) return Style + "_Masonry";
        if (piece == "Floor" || piece.StartsWith("Stair")) return "Floors";
        return "Outdoor";
    }

    private static Material LoadKitMaterial(string name)
    {
        if (name == "Masonry") name = Style + "_Masonry";
        if (!MaterialCache.TryGetValue(name, out Material material))
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(KitMaterialPath + name + ".mat");
            if (material == null)
                Debug.LogError("Residential builder: missing material MI_TS_" + name);
            MaterialCache[name] = material;
        }
        return material;
    }

    /// <summary>
    /// Places a kit piece that spans local X [-w, 0] along the grid edge a→b, its outside face (+Z) towards
    /// <paramref name="outward"/>.
    /// </summary>
    private static GameObject Edge(Transform parent, string piece, Vector2 a, Vector2 b, float y, Vector2 outward, string material = null, bool collide = true, Vector3? scale = null)
    {
        Quaternion rotation = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.y), Vector3.up);
        Vector3 start = new Vector3(a.x, y, a.y);
        Vector3 end = new Vector3(b.x, y, b.y);
        Vector3 pivot = Vector3.Dot(end - start, rotation * Vector3.right) > 0f ? end : start;

        GameObject go = Kit(piece, parent, rotation.eulerAngles.y, material, collide, scale);
        go.transform.position = pivot;
        return go;
    }

    /// <summary>
    /// Lays kit pieces along a straight line, one char per metre: W wall, N window, M 2 m window, D 2 m doorway,
    /// H half wall, R railing, F picket fence, E hedge, S stone rim, P masonry rim, B storey band, b porch beam, '-' gap.
    /// </summary>
    private static void Run(Transform parent, Vector2 start, Vector2 end, float y, Vector2 outward, string spec, string wallMaterial = null)
    {
        Vector2 direction = (end - start).normalized;
        float length = Vector2.Distance(start, end);
        float t = 0f;
        foreach (char c in spec)
        {
            int width = c == 'M' || c == 'D' ? 2 : 1;
            Vector2 a = start + direction * t;
            Vector2 b = start + direction * (t + width);
            t += width;

            switch (c)
            {
                case 'W': Edge(parent, "Wall", a, b, y, outward, wallMaterial); break;
                case 'N': Edge(parent, "Wall_Window", a, b, y, outward); Edge(parent, "Window_DoubleHung", a, b, y, outward); break;
                case 'M': Edge(parent, "Wall_Window2", a, b, y, outward); Edge(parent, "Window2_DoubleHung", a, b, y, outward); break;
                case 'D': Edge(parent, "Wall_Door2", a, b, y, outward, wallMaterial); break;
                case 'H': Edge(parent, "Wall_Half", a, b, y, outward); break;
                case 'R': Edge(parent, "Railing", a, b, y, outward); break;
                case 'F': Edge(parent, "Fence_Picket", a, b, y, outward); break;
                case 'E': Edge(parent, "Hedge_100", a, b, y, outward); break;
                case 'S': Edge(parent, "Foundation_Stone_100", a, b, y, outward, "Masonry"); break;
                case 'P': Edge(parent, "Foundation_Masonry_75", a, b, y, outward); break;
                case 'B': Edge(parent, "Trim_StoreyBand", a, b, y, outward, null, false); break;
                case 'b': Edge(parent, "Porch_Beam", a, b, y, outward); break;
                case '-': break;
                default: Debug.LogError("Residential builder: unknown wall spec char " + c); break;
            }
        }
        if (!Mathf.Approximately(t, length))
            Debug.LogError($"Residential builder: spec '{spec}' is {t} m but the run is {length} m");
    }

    /// <summary>Fills [xMin,xMax)×[zMin,zMax) with tiles whose top is at y; skip(x, z) leaves holes.</summary>
    private static void FloorGrid(Transform parent, string piece, float xMin, float xMax, float zMin, float zMax, float y, float step, string material, System.Func<float, float, bool> skip)
    {
        for (float x = xMin; x < xMax - 0.001f; x += step)
            for (float z = zMin; z < zMax - 0.001f; z += step)
            {
                if (skip != null && skip(x, z))
                    continue;
                GameObject tile = Kit(piece, parent, 0f, material, true, step == 1f ? (Vector3?)null : new Vector3(step, 1f, step));
                tile.transform.position = new Vector3(x + step, y, z + step);
            }
    }

    private static void Corners(Transform parent, float y)
    {
        foreach (Vector2 c in new[] { new Vector2(-6, 0), new Vector2(8, 0), new Vector2(-6, 10), new Vector2(8, 10) })
            Place(Kit("Column_Square", parent, 0f), new Vector3(c.x, y, c.y));
    }

    private static void BandAround(Transform parent, float y)
    {
        Run(parent, new Vector2(-6, 0), new Vector2(8, 0), y, Vector2.down, new string('B', 14));
        Run(parent, new Vector2(-6, 10), new Vector2(8, 10), y, Vector2.up, new string('B', 14));
        Run(parent, new Vector2(-6, 0), new Vector2(-6, 10), y, Vector2.left, new string('B', 10));
        Run(parent, new Vector2(8, 0), new Vector2(8, 10), y, Vector2.right, new string('B', 10));
    }

    /// <summary>Invisible slope collider whose top surface runs from low to high (stairs, dock ramp).</summary>
    private static void Ramp(Transform parent, string name, Vector3 low, Vector3 high, float width)
    {
        const float thickness = 0.2f;
        Vector3 along = high - low;
        Quaternion rotation = Quaternion.LookRotation(along.normalized, Vector3.up);
        var ramp = new GameObject(name);
        ramp.transform.SetParent(parent, false);
        ramp.transform.SetPositionAndRotation((low + high) * 0.5f - rotation * Vector3.up * (thickness * 0.5f), rotation);
        ramp.AddComponent<BoxCollider>().size = new Vector3(width, thickness, along.magnitude);
        MarkStatic(ramp);
    }

    private static void StonePath(Transform parent, Vector2 from, Vector2 to)
    {
        string[] stones = { "Backyard/Rock Path 1", "Backyard/Rock Path 2", "Backyard/Rock Path 3", "Backyard/Rock Path 4" };
        int count = Mathf.CeilToInt(Vector2.Distance(from, to) / 0.45f);
        for (int i = 0; i <= count; i++)
        {
            Vector2 p = Vector2.Lerp(from, to, i / (float)count);
            float side = (i % 2 == 0 ? 0.12f : -0.12f);
            Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized * side;
            Home(stones[i % stones.Length], parent, i * 47f, new Vector3(p.x + normal.x, 0.005f, p.y + normal.y), false);
        }
    }

    /// <summary>VNB Home Set prefab; position is the footprint centre and floor height.</summary>
    private static GameObject Home(string item, Transform parent, float yaw, Vector3 position, bool collide = true)
    {
        return Prop(HomePath + item + ".prefab", parent, yaw, position, collide);
    }

    private static GameObject Prop(string path, Transform parent, float yaw, Vector3 position, bool collide)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            Debug.LogError("Residential builder: missing prefab " + path);
            return new GameObject("Missing " + Path.GetFileNameWithoutExtension(path));
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Place(go, position);
        if (collide)
            AddBoxCollider(go);
        MarkStatic(go);
        return go;
    }

    /// <summary>Wall-mounted item; position is the centre of its bounds.</summary>
    private static void WallItem(string item, Transform parent, float yaw, Vector3 center)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HomePath + item + ".prefab"), parent);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.position += center - WorldBounds(go).center;
        MarkStatic(go);
    }

    private static void Tree(string path, Transform parent, Vector2 spot)
    {
        GameObject tree = Prop(path, parent, spot.x * 37f, new Vector3(spot.x, 0f, spot.y), false);
        var trunk = tree.AddComponent<CapsuleCollider>();
        trunk.radius = 0.3f;
        trunk.height = 3f;
        trunk.center = tree.transform.InverseTransformPoint(new Vector3(spot.x, 1.5f, spot.y));
    }

    private static void CeilingLamp(Transform parent, Transform lights, Vector3 ceiling, float floor)
    {
        // The ceiling is the underside of the slab above (slab is 0.1 m thick).
        Home("Decoration/Ceiling Lamp 2", parent, 0f, new Vector3(ceiling.x, ceiling.y - 0.1f - 0.16f, ceiling.z), false);
        PointLight(lights, "Room Light", new Vector3(ceiling.x, floor + 2.4f, ceiling.z), 7f, 1.6f, WarmLight);
    }

    private static void PointLight(Transform parent, string name, Vector3 position, float range, float intensity, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
    }

    /// <summary>Moves an object so its bounds are centred on position.xz with the bottom at position.y.</summary>
    private static GameObject Place(GameObject go, Vector3 position)
    {
        Bounds b = WorldBounds(go);
        go.transform.position += new Vector3(position.x - b.center.x, position.y - b.min.y, position.z - b.center.z);
        return go;
    }

    private static void PlaceMin(GameObject go, Vector3 min)
    {
        go.transform.position += min - WorldBounds(go).min;
    }

    private static Bounds WorldBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
            b.Encapsulate(r.bounds);
        return b;
    }

    private static void AddBoxCollider(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        var local = new Bounds(go.transform.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
        foreach (Renderer r in renderers)
        {
            Bounds wb = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? wb.min.x : wb.max.x,
                    (i & 2) == 0 ? wb.min.y : wb.max.y,
                    (i & 4) == 0 ? wb.min.z : wb.max.z);
                local.Encapsulate(go.transform.InverseTransformPoint(corner));
            }
        }
        var box = go.AddComponent<BoxCollider>();
        box.center = local.center;
        box.size = local.size;
    }

    private static void Move(GameObject go, Transform parent, Vector3? position)
    {
        if (go == null)
            return;
        go.transform.SetParent(parent, true);
        if (position.HasValue)
            go.transform.position = position.Value;
    }

    private static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"Residential builder: {target.GetType().Name} has no field {field}");
            return;
        }
        property.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
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
            Debug.LogWarning("Residential builder: root object not found: " + name);
        return go;
    }
}
