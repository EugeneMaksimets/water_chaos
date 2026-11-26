using System.Collections.Generic;
using UnityEngine;
using WaterChaos.EditorTools.MiniVillage;

namespace WaterChaos.EditorTools.OfficeDistrict
{
    /// <summary>
    /// The small toxic liquid factory: a corrugated steel hall (roller door, personnel door, high windows,
    /// a steel mezzanine with stairs, process tanks and pipes) and the steel shell of the open toxic tank
    /// in the yard. The tank's liquid is the scene's existing reservoir (WaterReservoirComponent, Liquid = Toxic).
    /// Hall space: x 0..18, z 0..12, front at z = 0.
    /// </summary>
    internal static class ToxicFactory
    {
        public const string Name = "Factory_Hall";
        public const float Width = 18f;
        public const float Depth = 12f;
        private const float Wall = 0.3f;
        private const float WallTop = 6.2f;
        private const float FloorTop = 0.1f;
        private const float DeckTop = 2.9f;

        private static readonly float[] Sides = { -1f, 1f };

        /// <summary>Inside of the hall: one temperature zone.</summary>
        public static Bounds HallBounds => new Bounds(
            new Vector3(Width * 0.5f, (FloorTop + WallTop) * 0.5f, Depth * 0.5f),
            new Vector3(Width - Wall * 2f, WallTop - FloorTop, Depth - Wall * 2f));

        public static GameObject BuildHall()
        {
            var facade = new Facade(Width, Depth, Wall);
            var model = new PropModel();
            var windows = new List<(Side, Opening)>();

            foreach (Side side in new[] { Side.Front, Side.Back, Side.Left, Side.Right })
            {
                var openings = new List<Opening>();
                float[] centers = side == Side.Front ? new[] { 11.5f, 15f }
                    : side == Side.Back ? new[] { 3f, 7f, 11f, 15f }
                    : new[] { 3f, 9f };
                foreach (float u in centers)
                {
                    var opening = new Opening(u - 0.9f, u + 0.9f, 3.6f, 4.6f);
                    openings.Add(opening);
                    windows.Add((side, opening));
                }

                if (side == Side.Front)
                {
                    openings.Add(new Opening(2f, 6.2f, 0f, 4.4f));
                    openings.Add(new Opening(8f, 9.2f, 0f, 2.35f));
                }

                facade.Wall(model, side, WallTop, openings, "Corrugated", "Concrete", 0.5f);
            }

            facade.Band(model, WallTop - 0.1f, 0.2f, 0.1f, "SteelDark");
            model.Mesh.Gable(new Vector3(Width * 0.5f, WallTop, Depth * 0.5f), Width + 0.5f, Depth + 0.5f, 1.4f, "RoofDark");
            model.Part(new Vector3(Width * 0.5f, WallTop - 0.1f, Depth * 0.5f), new Vector3(Width - Wall * 2f, 0.2f, Depth - Wall * 2f), "SteelDark", true);
            model.Part(new Vector3(Width * 0.5f, FloorTop * 0.5f, Depth * 0.5f), new Vector3(Width - Wall * 2f, FloorTop, Depth - Wall * 2f), "Concrete", true);
            model.Part(new Vector3(4.1f, FloorTop * 0.5f, Wall * 0.5f), new Vector3(4.2f, FloorTop, Wall), "Concrete", true);
            model.Part(new Vector3(8.6f, FloorTop * 0.5f, Wall * 0.5f), new Vector3(1.2f, FloorTop, Wall), "Concrete", true);

            // Roller door: shutter box over the opening and hazard stripes on the jambs.
            model.Mesh.Box(new Vector3(4.1f, 4.65f, -0.18f), new Vector3(4.5f, 0.5f, 0.36f), "SteelDark");
            model.Mesh.Box(new Vector3(4.1f, 4.25f, 0.05f), new Vector3(4.2f, 0.3f, 0.06f), "Corrugated");
            foreach (float x in new[] { 1.95f, 6.25f })
            {
                for (int i = 0; i < 6; i++)
                    model.Mesh.Box(new Vector3(x, 0.35f + i * 0.7f, -0.02f), new Vector3(0.1f, 0.35f, 0.04f), "HazardYellow");
            }

            BuildMezzanine(model);
            BuildProcessTanks(model);

            GameObject root = VillageAssets.CreateMeshObject(Name, model.Mesh, Name);
            VillageAssets.AddColliders(root, model.Colliders);
            DistrictKit.MarkStatic(root);
            VillageAssets.Label(root.transform, "TOXIC WORKS", new Vector3(12.8f, 5.4f, -0.05f), 0.55f, new Color(0.6f, 1f, 0.3f), Quaternion.identity);

            float length = Mathf.Sqrt(4f * 4f + (DeckTop - FloorTop) * (DeckTop - FloorTop));
            float angle = Mathf.Atan2(DeckTop - FloorTop, 4f) * Mathf.Rad2Deg;
            Quaternion up = Quaternion.Euler(-angle, 0f, 0f);
            Vector3 mid = new Vector3(11.6f, (FloorTop + DeckTop) * 0.5f, 6.5f);
            DistrictKit.RotatedCollider(root.transform, "Stair Ramp", mid - up * Vector3.up * 0.05f, up, new Vector3(1.2f, 0.1f, length + 0.05f));

            Transform windowGroup = DistrictKit.Group(root.transform, "Windows");
            foreach (var window in windows)
            {
                GameObject instance = DistrictKit.Place(DistrictKit.Get("Window_Factory"), windowGroup, Vector3.zero, 0f);
                instance.transform.SetLocalPositionAndRotation(facade.Place(window.Item1, window.Item2.Center, window.Item2.Middle, Wall * 0.5f), Facade.Facing(window.Item1));
            }

            Transform doors = DistrictKit.Group(root.transform, "Doors");
            DistrictKit.Place(DistrictKit.Get("Door_Factory"), doors, new Vector3(8.05f, FloorTop + 0.01f, Wall * 0.5f), 0f);

            Color cold = new Color(0.85f, 1f, 0.85f);
            DistrictKit.PointLight(root.transform, "Hall Light A", new Vector3(5f, 5.4f, 6f), 12f, 1.6f, cold);
            DistrictKit.PointLight(root.transform, "Hall Light B", new Vector3(13f, 5.4f, 6f), 12f, 1.6f, cold);

            return VillageAssets.SavePrefab(root, DistrictKit.BuildingFolder, Name);
        }

        /// <summary>Steel deck along the back wall (x 1..12.2, z 8.5..11.7) with posts, railings and stairs up from z 4.5.</summary>
        private static void BuildMezzanine(PropModel model)
        {
            const float x0 = 1f, x1 = 12.2f, z0 = 8.5f, z1 = Depth - Wall;
            model.Part(new Vector3((x0 + x1) * 0.5f, DeckTop - 0.075f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.15f, z1 - z0), "SteelDark", true);
            for (float x = x0 + 0.2f; x < x1; x += 2.8f)
                model.Part(new Vector3(x, DeckTop * 0.5f, z0 + 0.15f), new Vector3(0.15f, DeckTop, 0.15f), "HazardYellow", true);

            // Railing along the front edge, open at the stair head (x 11..12.2).
            model.Part(new Vector3((x0 + 11f) * 0.5f, DeckTop + 0.5f, z0 + 0.03f), new Vector3(11f - x0, 1f, 0.05f), "Steel", true);
            model.Mesh.Box(new Vector3((x0 + 11f) * 0.5f, DeckTop + 1.02f, z0 + 0.03f), new Vector3(11f - x0, 0.06f, 0.08f), "HazardYellow");
            model.Part(new Vector3(x0 + 0.03f, DeckTop + 0.5f, (z0 + z1) * 0.5f), new Vector3(0.05f, 1f, z1 - z0), "Steel", true);

            const float stairX0 = 11f, stairX1 = 12.2f, stairZ0 = 4.5f;
            const int steps = 14;
            float rise = (DeckTop - FloorTop) / steps;
            float run = (z0 - stairZ0) / steps;
            for (int k = 0; k < steps; k++)
            {
                float top = FloorTop + rise * (k + 1);
                model.Mesh.Box(new Vector3((stairX0 + stairX1) * 0.5f, top - 0.03f, stairZ0 + run * (k + 0.5f)), new Vector3(stairX1 - stairX0, 0.06f, run), "Steel");
            }
            foreach (float x in new[] { stairX0 - 0.04f, stairX1 + 0.04f })
            {
                model.Mesh.SetTransform(new Vector3(x, (FloorTop + DeckTop) * 0.5f, (stairZ0 + z0) * 0.5f), Quaternion.Euler(-Mathf.Atan2(DeckTop - FloorTop, z0 - stairZ0) * Mathf.Rad2Deg, 0f, 0f));
                model.Mesh.Box(Vector3.zero, new Vector3(0.06f, 0.25f, Mathf.Sqrt(16f + 7.84f)), "SteelDark");
                model.Mesh.Box(new Vector3(0f, 0.95f, 0f), new Vector3(0.05f, 0.05f, Mathf.Sqrt(16f + 7.84f)), "HazardYellow");
                model.Mesh.ResetTransform();
            }
        }

        /// <summary>Two tall process tanks with glowing sight glasses and the pipes along the back wall.</summary>
        private static void BuildProcessTanks(PropModel model)
        {
            foreach (float z in new[] { 5.2f, 8.8f })
            {
                var center = new Vector3(15.6f, FloorTop, z);
                model.Mesh.Cylinder(center, 1f, 1f, 4.2f, 12, "Steel");
                model.Mesh.Cylinder(center + Vector3.up * 4.2f, 1f, 0.3f, 0.6f, 12, "Steel");
                model.Mesh.Cylinder(center + Vector3.up * 0.9f, 1.02f, 1.02f, 0.12f, 12, "HazardYellow", false);
                model.Mesh.Box(center + new Vector3(-1.01f, 2.4f, 0f), new Vector3(0.04f, 2.2f, 0.18f), "ToxicGlow");
                model.Collider(center + Vector3.up * 2.4f, new Vector3(1.9f, 4.8f, 1.9f));
                foreach (float s in Sides)
                    model.Mesh.Box(center + new Vector3(s * 0.7f, 0.25f, 0f), new Vector3(0.15f, 0.5f, 0.15f), "SteelDark");
            }

            model.Mesh.SetTransform(new Vector3(9f, 4.9f, Depth - 0.6f), Quaternion.Euler(0f, 0f, 90f));
            model.Mesh.Cylinder(new Vector3(0f, -8.4f, 0f), 0.14f, 0.14f, 16.8f, 8, "Steel");
            model.Mesh.ResetTransform();
            model.Mesh.SetTransform(new Vector3(9f, 5.25f, Depth - 0.6f), Quaternion.Euler(0f, 0f, 90f));
            model.Mesh.Cylinder(new Vector3(0f, -8.4f, 0f), 0.1f, 0.1f, 16.8f, 8, "ToxicDrum");
            model.Mesh.ResetTransform();
            model.Mesh.Cylinder(new Vector3(15.6f, 4.8f, 8.8f), 0.12f, 0.12f, 0.5f, 8, "Steel");
            model.Mesh.SetTransform(new Vector3(15.6f, 5.25f, 10.3f), Quaternion.Euler(90f, 0f, 0f));
            model.Mesh.Cylinder(new Vector3(0f, -1.1f, 0f), 0.1f, 0.1f, 2.2f, 8, "ToxicDrum");
            model.Mesh.ResetTransform();
        }

        /// <summary>Loose stock and tools in the hall; <paramref name="parent"/> has the hall's pose.</summary>
        public static void Furnish(Transform parent)
        {
            const float y = FloorTop;
            Transform stock = DistrictKit.Group(parent, "Hall · Stock");
            for (int i = 0; i < 6; i++)
                F(stock, "OfficeDistrict/Factory/ToxicBarrel", 2f + (i % 3) * 0.7f, y, 9.6f + (i / 3) * 0.7f, i * 25f, false);
            for (int i = 0; i < 4; i++)
                F(stock, "OfficeDistrict/Factory/MetalBarrel", 6.4f + (i % 2) * 0.7f, y, 10.6f + (i / 2) * 0.7f, i * 40f, false);
            F(stock, "OfficeDistrict/Factory/OilBarrel", 4.8f, y, 6.2f, 0f, true);
            F(stock, "OfficeDistrict/Factory/OilBarrel", 5.5f, y, 6.5f, 30f, true);
            F(stock, "OfficeDistrict/Factory/ChemicalTote", 9f, y, 10.6f, 0f, false);
            F(stock, "OfficeDistrict/Factory/ChemicalTote", 9f, y, 9.3f, 0f, false);
            F(stock, "OfficeDistrict/Factory/Pallet", 7.8f, y, 2.4f, 0f, true);
            F(stock, "OfficeDistrict/Factory/CrateStack", 7.8f, y + 0.15f, 2.4f, 0f, true);
            F(stock, "OfficeDistrict/Factory/Pallet", 9.4f, y, 2.4f, 90f, true);
            F(stock, "OfficeDistrict/Furniture/PaperBox", 9.4f, y + 0.15f, 2.3f, 0f, true);
            F(stock, "OfficeDistrict/Furniture/PaperBox", 9.6f, y + 0.15f, 2.7f, 20f, true);

            Transform tools = DistrictKit.Group(parent, "Hall · Tools");
            F(tools, "OfficeDistrict/Factory/Workbench", 17.1f, y, 2f, 90f, true);
            F(tools, "OfficeDistrict/Factory/Toolbox", 17.1f, y + 0.94f, 1.6f, 90f, false);
            F(tools, "OfficeDistrict/Factory/ControlPanel", 13.4f, y, 11.4f, 0f, false);
            F(tools, "OfficeDistrict/Factory/ControlPanel", 13.4f, y, 3.6f, 180f, false);
            for (int i = 0; i < 3; i++)
                F(tools, "OfficeDistrict/Factory/GasCylinder", 0.6f, y, 5.8f + i * 0.35f, 0f, false);
            F(tools, "OfficeDistrict/Factory/HazardBarrier", 13.4f, y, 7f, 90f, false);

            Transform deck = DistrictKit.Group(parent, "Hall · Mezzanine");
            F(deck, "OfficeDistrict/Factory/CrateStack", 3f, DeckTop, 10.6f, 0f, true);
            F(deck, "OfficeDistrict/Factory/ToxicBarrel", 6f, DeckTop, 10.8f, 0f, false);
            F(deck, "OfficeDistrict/Furniture/FilingCabinet", 8.5f, DeckTop, 11.2f, 0f, false);
        }

        /// <summary>
        /// Steel shell of the open octagonal toxic tank around an existing reservoir (centre at its origin):
        /// riveted walls up to <paramref name="rimHeight"/>, a hazard band, a floor, an outside ladder and labels.
        /// </summary>
        public static GameObject BuildTankShell(Transform tank, float apothem, float rimHeight)
        {
            const float t = 0.25f;
            float side = 2f * (apothem + t) * Mathf.Tan(22.5f * Mathf.Deg2Rad) + 0.02f;
            var shell = new PropModel();
            var colliders = new List<(Vector3, Quaternion, Vector3)>();
            for (int i = 0; i < 8; i++)
            {
                Quaternion rotation = Quaternion.Euler(0f, -i * 45f, 0f);
                Vector3 center = rotation * new Vector3(apothem + t * 0.5f, rimHeight * 0.5f, 0f);
                shell.Mesh.SetTransform(center, rotation);
                shell.Mesh.Box(Vector3.zero, new Vector3(t, rimHeight, side), "Steel");
                // Hazard band 1 cm proud of both wall faces (a shared inner face would flicker).
                shell.Mesh.Box(new Vector3(0f, rimHeight * 0.5f - 0.18f, 0f), new Vector3(t + 0.02f, 0.2f, side + 0.01f), "HazardYellow");
                shell.Mesh.Box(new Vector3(0f, rimHeight * 0.5f + 0.04f, 0f), new Vector3(t + 0.14f, 0.08f, side + 0.06f), "SteelDark");
                shell.Mesh.Box(new Vector3(0.01f, -rimHeight * 0.5f + 0.1f, 0f), new Vector3(t + 0.03f, 0.2f, side + 0.01f), "SteelDark");
                shell.Mesh.ResetTransform();
                colliders.Add((center, rotation, new Vector3(t, rimHeight, side)));
            }

            shell.Mesh.SetTransform(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            shell.Mesh.Extrude(VillageProps.Polygon(8, apothem / Mathf.Cos(22.5f * Mathf.Deg2Rad), 22.5f), -0.02f, 0f, "SteelDark");
            shell.Mesh.ResetTransform();

            // Ladder up the outside of the -X wall.
            float lx = -(apothem + t + 0.08f);
            foreach (float s in Sides)
                shell.Mesh.Box(new Vector3(lx, (rimHeight + 0.5f) * 0.5f, s * 0.25f), new Vector3(0.05f, rimHeight + 0.5f, 0.05f), "HazardYellow");
            for (float y = 0.3f; y < rimHeight + 0.4f; y += 0.3f)
                shell.Mesh.Box(new Vector3(lx, y, 0f), new Vector3(0.04f, 0.04f, 0.5f), "SteelDark");

            GameObject go = DistrictKit.Bake(shell, "ToxicTank_Shell", tank, false);
            go.transform.localPosition = Vector3.zero;
            foreach (var c in colliders)
                DistrictKit.RotatedCollider(go.transform, "WallCollider", c.Item1, c.Item2, c.Item3);

            for (int i = 0; i < 4; i++)
            {
                Quaternion facing = Quaternion.Euler(0f, 45f + i * 90f, 0f);
                Vector3 position = facing * new Vector3(0f, rimHeight * 0.55f, -(apothem + t + 0.02f));
                VillageAssets.Label(go.transform, "TOXIC", position, 0.32f, new Color(0.1f, 0.1f, 0.1f), facing);
            }

            return go;
        }

        // ───────────── Compound ─────────────

        private const string WaterMeshPath = "Assets/WaterChaos/Environment/Models/Water_Octagon.asset";
        private const string WaterMaterialPath = "Assets/WaterChaos/Materials/Water/Materials/M_WaterPond.mat";
        private const string FirePrefabPath = "Assets/WaterChaos/Prefabs/Cartoon Fire.prefab";
        private const float TankApothem = 3f;
        private const float TankDepth = 0.6f;
        private const float TankRim = 1.15f;

        /// <summary>Fenced yard pad of the compound: 36 × 24 m around its origin, gate on the +Z side.</summary>
        public static readonly Vector2 CompoundSize = new Vector2(36f, 24f);

        /// <summary>Scene parents the compound registers its gameplay objects with.</summary>
        public struct GameplayParents
        {
            public Transform Interiors;
            public Transform Rooms;
            public Transform Fires;
            public Transform Items;
        }

        /// <summary>
        /// The whole factory compound under <paramref name="root"/> (compound space: origin at the yard centre,
        /// gate in the +Z fence): concrete yard, the hall (furnished, one temperature zone, a fire), the open toxic
        /// tank — a new reservoir holding toxic liquid — with silo and pipes, the corrodible security fence,
        /// yard props and an axe by the gate. Returns the hall.
        /// </summary>
        public static Transform BuildCompound(Transform root, GameObject hallPrefab, GameplayParents parents)
        {
            const float paving = DistrictKit.Paving;
            var pad = new PropModel();
            pad.Part(new Vector3(0f, paving * 0.5f, 0f), new Vector3(CompoundSize.x, paving, CompoundSize.y), "Concrete", true);
            pad.Part(new Vector3(-3f, paving * 0.5f, 13f), new Vector3(6f, paving, 2f), "Concrete", true);
            DistrictKit.Bake(pad, "Factory_Yard_Pad", root).name = "Yard Pad";

            var hall = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(hallPrefab, root);
            hall.transform.SetLocalPositionAndRotation(new Vector3(8f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f));

            Transform furniture = DistrictKit.Group(parents.Interiors != null ? parents.Interiors : root, "Factory Furniture");
            furniture.SetPositionAndRotation(hall.transform.position, hall.transform.rotation);
            Furnish(furniture);

            CreateToxicTank(root, new Vector3(13.5f, paving, 6.5f));
            BuildYard(root);

            if (parents.Rooms != null)
                DistrictKit.Zone(parents.Rooms, "Toxic Works · Hall", hall.transform, HallBounds);

            var fire = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefabPath);
            if (fire != null && parents.Fires != null)
            {
                var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(fire, parents.Fires);
                instance.name = "Cartoon Fire (Factory Hall)";
                instance.transform.position = hall.transform.TransformPoint(new Vector3(8.6f, 0.6f, 3.6f));
            }

            if (parents.Items != null)
                DistrictKit.PlaceAxe(parents.Items, root.TransformPoint(new Vector3(-3.4f, paving + 0.08f, 10f)), root.eulerAngles.y - 30f);

            return hall.transform;
        }

        /// <summary>
        /// A new open toxic tank: water body and reservoir (Liquid = Toxic, full), level view, reservoir link for the
        /// detector, green tint and the steel shell. The pump is put into it like into a pool.
        /// </summary>
        public static Transform CreateToxicTank(Transform parent, Vector3 localPosition)
        {
            var tank = new GameObject("ToxicTank").transform;
            tank.SetParent(parent, false);
            tank.localPosition = localPosition;

            float width = 2f * TankApothem / Mathf.Cos(22.5f * Mathf.Deg2Rad);
            float area = 8f * TankApothem * TankApothem * Mathf.Tan(22.5f * Mathf.Deg2Rad);
            float liters = Mathf.Round(area * TankDepth * 1000f);

            var volume = new GameObject("Water Volume") { layer = 4 };
            volume.transform.SetParent(tank, false);
            var box = volume.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, TankDepth * 0.5f, 0f);
            box.size = new Vector3(TankApothem * 2f, TankDepth, TankApothem * 2f);
            var reservoir = volume.AddComponent<WaterChaos.Water.WaterReservoirComponent>();
            var data = new UnityEditor.SerializedObject(reservoir);
            data.FindProperty("_liquid").intValue = (int)WaterChaos.Water.LiquidType.Toxic;
            data.FindProperty("_capacity").floatValue = liters;
            data.FindProperty("_initialVolume").floatValue = liters;
            data.FindProperty("_lowLevelRatio").floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
            volume.AddComponent<WaterChaos.Water.WaterBody>();

            var water = new GameObject("Water");
            water.transform.SetParent(tank, false);
            water.transform.localPosition = new Vector3(0f, TankDepth * 0.5f, 0f);
            water.transform.localScale = new Vector3(width, TankDepth, width);
            water.AddComponent<MeshFilter>().sharedMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(WaterMeshPath);
            var renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var level = tank.gameObject.AddComponent<WaterChaos.WaterTransfer.ReservoirLevelView>();
            var levelData = new UnityEditor.SerializedObject(level);
            levelData.FindProperty("_reservoir").objectReferenceValue = reservoir;
            levelData.FindProperty("_water").objectReferenceValue = water.transform;
            levelData.FindProperty("_bottom").floatValue = 0f;
            levelData.FindProperty("_fullHeight").floatValue = TankDepth;
            levelData.ApplyModifiedPropertiesWithoutUndo();

            DistrictKit.SetReference(tank.gameObject.AddComponent<WaterChaos.Water.WaterReservoirTarget>(), "_reservoir", reservoir);

            var tint = tank.gameObject.AddComponent<WaterChaos.Water.ReservoirLiquidTint>();
            var tintData = new UnityEditor.SerializedObject(tint);
            tintData.FindProperty("_reservoir").objectReferenceValue = reservoir;
            SerializedArray(tintData.FindProperty("_renderers"), renderer);
            tintData.ApplyModifiedPropertiesWithoutUndo();

            BuildTankShell(tank, TankApothem, TankRim);
            return tank;
        }

        /// <summary>Fence, silo, pipes to the tank and into the hall, warning signs, stock and the loading area.</summary>
        private static void BuildYard(Transform root)
        {
            const float paving = DistrictKit.Paving;
            Transform yard = DistrictKit.Group(root, "Yard");

            // Security fence around the yard (toxic liquid eats it); gate gap at x -6.5..0.5 in the +Z side.
            Transform fence = DistrictKit.Group(yard, "Security Fence");
            for (float x = -17f; x < 18f; x += 2f)
            {
                if (x > -6.5f && x < 0.5f)
                    continue;
                DistrictKit.Place(DistrictKit.Get("SecurityFence"), fence, new Vector3(x, paving, 12f), 0f);
                DistrictKit.Place(DistrictKit.Get("SecurityFence"), fence, new Vector3(x, paving, -12f), 180f);
            }
            for (float z = 11f; z > -12f; z -= 2f)
            {
                DistrictKit.Place(DistrictKit.Get("SecurityFence"), fence, new Vector3(-18f, paving, z), 90f);
                DistrictKit.Place(DistrictKit.Get("SecurityFence"), fence, new Vector3(18f, paving, z), -90f);
            }

            var structures = new PropModel();
            var silo = new Vector3(4f, paving, 8.5f);
            structures.Mesh.Cylinder(silo + Vector3.up * 1.2f, 1.5f, 1.5f, 6f, 14, "Steel");
            structures.Mesh.Cylinder(silo + Vector3.up * 7.2f, 1.5f, 0.2f, 1.2f, 14, "Steel");
            structures.Mesh.Cylinder(silo + Vector3.up * 1.2f, 0.4f, 0.4f, 0.3f, 8, "SteelDark");
            foreach (float y in new[] { 2f, 5.5f })
                structures.Mesh.Cylinder(silo + Vector3.up * y, 1.53f, 1.53f, 0.3f, 14, "HazardYellow", false);
            for (int i = 0; i < 4; i++)
            {
                Vector3 leg = silo + Quaternion.Euler(0f, 45f + i * 90f, 0f) * new Vector3(1.2f, 0f, 0f);
                structures.Part(leg + Vector3.up * 0.65f, new Vector3(0.2f, 1.3f, 0.2f), "SteelDark");
            }
            structures.Collider(silo + Vector3.up * 4f, new Vector3(3f, 8f, 3f));
            for (float y = 0.4f; y < 7f; y += 0.35f)
                structures.Mesh.Box(silo + new Vector3(0f, y, 1.6f), new Vector3(0.5f, 0.04f, 0.04f), "HazardYellow");

            Pipe(structures, new Vector3(5.5f, 3f, 8.5f), new Vector3(11.3f, 3f, 8.5f), 0.14f, "Steel");
            Pipe(structures, new Vector3(11.3f, 1.4f, 8.5f), new Vector3(11.3f, 3.1f, 8.5f), 0.14f, "Steel");
            Pipe(structures, new Vector3(13.5f, 3.2f, 3.7f), new Vector3(13.5f, 3.2f, -2f), 0.16f, "ToxicDrum");
            Pipe(structures, new Vector3(13.5f, 1.4f, 3.7f), new Vector3(13.5f, 3.3f, 3.7f), 0.16f, "ToxicDrum");
            Pipe(structures, new Vector3(13.6f, 3.2f, -2f), new Vector3(7.9f, 3.2f, -2f), 0.16f, "ToxicDrum");
            foreach (Vector3 post in new[] { new Vector3(13.5f, 0f, 1f), new Vector3(13.5f, 0f, -2f), new Vector3(10.5f, 0f, -2f), new Vector3(8f, 0f, 8.5f) })
            {
                float height = post.z > 8f ? 3f : 3.2f;
                structures.Part(new Vector3(post.x, height * 0.5f, post.z), new Vector3(0.18f, height, 0.18f), "SteelDark", true);
            }
            DistrictKit.Bake(structures, "Factory_Yard_Structures", yard).name = "Silo And Pipes";

            Transform props = DistrictKit.Group(yard, "Yard Props");
            foreach (Vector3 at in new[] { new Vector3(9f, 0f, 3.2f), new Vector3(17f, 0f, 10f), new Vector3(17f, 0f, 2.5f) })
                DistrictKit.Place(DistrictKit.Get("ToxicWarningSign"), props, at + Vector3.up * paving, 0f);
            for (int i = 0; i < 5; i++)
                DistrictKit.Furnish("OfficeDistrict/Factory/ToxicBarrel", props, new Vector3(15.6f + (i % 2) * 0.7f, paving, -1f - (i / 2) * 0.7f), i * 35f, false);
            DistrictKit.Furnish("OfficeDistrict/Factory/ChemicalTote", props, new Vector3(8.5f, paving, 10.6f), 0f, false);
            DistrictKit.Furnish("OfficeDistrict/Factory/HazardBarrier", props, new Vector3(9.2f, paving, 10.7f), 90f, false);
            DistrictKit.Furnish("OfficeDistrict/Factory/HazardBarrier", props, new Vector3(1f, paving, 11f), 0f, false);

            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3(-15.5f + (i % 2) * 1.6f, paving, -2f - (i / 2) * 2.4f);
                DistrictKit.Furnish("OfficeDistrict/Factory/Pallet", props, at, 0f);
                DistrictKit.Furnish(i % 2 == 0 ? "OfficeDistrict/Factory/CrateStack" : "OfficeDistrict/Furniture/PaperBox", props, at + Vector3.up * 0.15f, i * 20f);
            }
            DistrictKit.Furnish("OfficeDistrict/Factory/MetalBarrel", props, new Vector3(-16.5f, paving, 4f), 0f, false);
            DistrictKit.Furnish("OfficeDistrict/Factory/MetalBarrel", props, new Vector3(-15.8f, paving, 3.6f), 0f, false);
            DistrictKit.Furnish("OfficeDistrict/Factory/OilBarrel", props, new Vector3(-16.2f, paving, 4.7f), 0f);
            DistrictKit.Furnish("Street/TrafficCone", props, new Vector3(-5.5f, paving, 11f), 0f, false);
            DistrictKit.Furnish("Street/TrafficCone", props, new Vector3(-0.5f, paving, 11f), 0f, false);
        }

        private static void Pipe(PropModel model, Vector3 from, Vector3 to, float radius, string material)
        {
            Vector3 direction = to - from;
            model.Mesh.SetTransform(from, Quaternion.FromToRotation(Vector3.up, direction.normalized));
            model.Mesh.Cylinder(Vector3.zero, radius, radius, direction.magnitude, 8, material);
            model.Mesh.ResetTransform();
            model.Collider((from + to) * 0.5f, new Vector3(Mathf.Abs(direction.x) + radius * 2f, Mathf.Abs(direction.y) + radius * 2f, Mathf.Abs(direction.z) + radius * 2f));
        }

        private static void SerializedArray(UnityEditor.SerializedProperty list, params Object[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void F(Transform parent, string prop, float x, float y, float z, float yaw, bool flammable)
        {
            DistrictKit.Furnish(prop, parent, new Vector3(x, y, z), yaw, flammable);
        }
    }
}
