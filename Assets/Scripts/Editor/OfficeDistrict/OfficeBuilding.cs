using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WaterChaos.EditorTools.MiniVillage;

namespace WaterChaos.EditorTools.OfficeDistrict
{
    /// <summary>
    /// The main four-storey brick office. Building space: x 0..20, z 0..14, front (street) at z = 0.
    /// A switchback stairwell in the back-right corner joins all floors; on the ground floor its doorway is closed
    /// by a steel grate that only toxic liquid dissolves. Floor plans:
    /// 1 — exactly four rooms (reception, manager's office, open office, meeting room) and a hallway;
    /// 2 — open-plan workstations, a collaboration corner and a storage room;
    /// 3 — two manager offices, an archive, a meeting room and a lounge;
    /// 4 — boardroom, director's office, break room and storage.
    /// Some doorways have door leaves (axe-breakable), others are open.
    /// </summary>
    internal static class OfficeBuilding
    {
        public const string Name = "Office_Building";
        public const float Width = 20f;
        public const float Depth = 14f;
        public const float Wall = 0.3f;
        public const float Storey = 3.4f;
        public const float GroundTop = 0.15f;
        public const float Slab = 0.25f;
        public const int Floors = 4;
        private const float ParapetTop = Floors * Storey + GroundTop + 0.85f;

        // Stairwell (switchback): strip at floor level, flight A up to the half landing, flight B up to the next floor.
        private const float StairX0 = 16.2f;
        private const float InnerX1 = Width - Wall;
        private const float InnerZ1 = Depth - Wall;
        private const float FlightZ0 = 8.7f;
        private const float LandingZ = 11.2f;
        private const float FlightAX0 = 16.3f, FlightAX1 = 17.9f;
        private const float FlightBX0 = 18.1f, FlightBX1 = 19.6f;
        private const int Steps = 9;

        private const float DoorLeaf = 1f;
        private const float DoorHeight = 2.3f;
        private const float WindowWidth = 1.2f;
        private const float WindowHeight = 1.7f;
        private const float Sill = 0.9f;

        private static readonly string[] FloorMaterials = { "Linoleum", "OfficeCarpet", "OfficeCarpetGreen", "WoodFloor" };

        /// <summary>Doorway in a partition; <c>Leaf</c> puts a door in it.</summary>
        private struct Gap
        {
            public float A, B;
            public bool Leaf;
            public Gap(float a, float b, bool leaf) { A = a; B = b; Leaf = leaf; }
        }

        /// <summary>Partition: along X between z <c>Line0..Line1</c>, or along Z between x <c>Line0..Line1</c>.</summary>
        private struct Partition
        {
            public bool AlongX;
            public float Line0, Line1, From, To;
            public Gap[] Gaps;
        }

        public static float FloorY(int floor) => GroundTop + floor * Storey;

        public static float CeilingY(int floor) => FloorY(floor + 1) - Slab;

        /// <summary>Inside of a floor (building space): one temperature zone for the whole floor.</summary>
        public static Bounds FloorBounds(int floor)
        {
            float y0 = FloorY(floor);
            float y1 = CeilingY(floor);
            return new Bounds(new Vector3(Width * 0.5f, (y0 + y1) * 0.5f, Depth * 0.5f), new Vector3(Width - Wall * 2f, y1 - y0, Depth - Wall * 2f));
        }

        // ───────────── Prefab ─────────────

        public static GameObject Build()
        {
            var facade = new Facade(Width, Depth, Wall);
            var shell = new PropModel();
            var windows = new List<(Side Side, Opening Opening, int Floor)>();

            foreach (Side side in new[] { Side.Front, Side.Back, Side.Left, Side.Right })
            {
                var openings = new List<Opening>();
                for (int floor = 0; floor < Floors; floor++)
                {
                    foreach (float u in WindowCenters(side))
                    {
                        float y0 = FloorY(floor) + Sill;
                        var opening = new Opening(u - WindowWidth * 0.5f, u + WindowWidth * 0.5f, y0, y0 + WindowHeight);
                        openings.Add(opening);
                        windows.Add((side, opening, floor));
                        facade.Lintel(shell, side, opening);
                    }
                }

                if (side == Side.Front)
                {
                    var door = new Opening(3f, 4.6f, 0f, GroundTop + 2.4f);
                    openings.Add(door);
                    facade.Lintel(shell, side, door);
                }

                facade.Wall(shell, side, ParapetTop, openings, "BrickRed", "OfficeWall", 0.7f);
            }

            for (int floor = 1; floor < Floors; floor++)
                facade.Band(shell, FloorY(floor) - 0.1f, 0.2f, 0.08f);
            facade.Cornice(shell, FloorY(Floors) - 0.05f, ParapetTop);
            facade.Quoins(shell, 0.7f, FloorY(Floors) - 0.2f);

            // Roof: slab under the parapet, tar on top; entrance threshold, canopy and steps.
            shell.Part(new Vector3(Width * 0.5f, FloorY(Floors) - Slab * 0.5f, Depth * 0.5f), new Vector3(Width - Wall * 2f, Slab, Depth - Wall * 2f), "Ceiling", true);
            shell.Mesh.Box(new Vector3(Width * 0.5f, FloorY(Floors) + 0.01f, Depth * 0.5f), new Vector3(Width - Wall * 2f, 0.02f, Depth - Wall * 2f), "RoofDark");
            shell.Part(new Vector3(3.8f, GroundTop * 0.5f, Wall * 0.5f), new Vector3(1.6f, GroundTop, Wall), "Stone", true);
            shell.Part(new Vector3(3.8f, 2.75f, -0.7f), new Vector3(2.6f, 0.12f, 1.4f), "SteelDark");
            shell.Mesh.Box(new Vector3(3.8f, 2.65f, -1.38f), new Vector3(2.62f, 0.18f, 0.06f), "Stucco");

            GameObject root = VillageAssets.CreateMeshObject(Name, shell.Mesh, Name);
            VillageAssets.AddColliders(root, shell.Colliders);
            DistrictKit.MarkStatic(root);
            VillageAssets.Label(root.transform, "OFFICE", new Vector3(3.8f, 3.15f, -0.05f), 0.45f, new Color(1f, 0.92f, 0.7f), Quaternion.identity);

            for (int floor = 0; floor < Floors; floor++)
                BuildFloor(root.transform, floor, windows, facade);

            Transform entrance = DistrictKit.Group(root.transform, "Entrance");
            DistrictKit.Place(DistrictKit.Get("Door_OfficeGlass"), entrance, new Vector3(3.05f, GroundTop + 0.01f, Wall * 0.5f), 0f);

            Transform roof = DistrictKit.Group(root.transform, "Roof");
            DistrictKit.Place(DistrictKit.Get("WaterTower"), roof, new Vector3(5f, FloorY(Floors), 9.5f), 0f);
            DistrictKit.Place(DistrictKit.Get("RoofAcUnit"), roof, new Vector3(11f, FloorY(Floors), 4f), 0f);
            DistrictKit.Place(DistrictKit.Get("RoofAcUnit"), roof, new Vector3(13f, FloorY(Floors), 4f), 0f);
            var bulkhead = new PropModel();
            bulkhead.Part(new Vector3(17.95f, 1.2f, 11.2f), new Vector3(3.6f, 2.4f, 5f), "BrickRed", true);
            bulkhead.Part(new Vector3(17.95f, 2.45f, 11.2f), new Vector3(3.8f, 0.12f, 5.2f), "Stucco");
            bulkhead.Part(new Vector3(17.95f, 1f, 8.69f), new Vector3(0.9f, 2f, 0.04f), "Door");
            GameObject bulkheadObject = DistrictKit.Bake(bulkhead, "Office_Bulkhead", roof);
            bulkheadObject.transform.localPosition = new Vector3(0f, FloorY(Floors), 0f);

            return VillageAssets.SavePrefab(root, DistrictKit.BuildingFolder, Name);
        }

        private static IEnumerable<float> WindowCenters(Side side)
        {
            switch (side)
            {
                case Side.Front: return new[] { 1.9f, 5.9f, 11f, 14.2f, 18.2f };
                case Side.Back: return new[] { 1.9f, 5.5f, 11.6f, 14.4f };
                case Side.Left: return new[] { 3f, 10.5f };
                default: return new[] { 3f };
            }
        }

        // ───────────── Floors ─────────────

        private static void BuildFloor(Transform root, int floor, List<(Side Side, Opening Opening, int Floor)> windows, Facade facade)
        {
            float y = FloorY(floor);
            float ceiling = CeilingY(floor);
            var model = new PropModel();
            var doors = new List<(Vector3 Position, float Yaw)>();
            string floorMaterial = FloorMaterials[floor];

            // Slab: the ground floor sits on the ground; upper slabs have the stairwell hole and a ceiling below.
            if (floor == 0)
            {
                model.Part(new Vector3(Width * 0.5f, y * 0.5f, Depth * 0.5f), new Vector3(Width - Wall * 2f, y, Depth - Wall * 2f), floorMaterial, true);
            }
            else
            {
                SlabRect(model, Wall, InnerX1, Wall, FlightZ0, y, floorMaterial);
                SlabRect(model, Wall, StairX0, FlightZ0, InnerZ1, y, floorMaterial);
            }

            foreach (Partition partition in Plan(floor))
                BuildPartition(model, partition, y, ceiling, doors);

            GameObject floorObject = DistrictKit.Bake(model, $"{Name}_Floor{floor + 1}", root);
            floorObject.name = $"Floor {floor + 1}";

            if (floor < Floors - 1)
                BuildStairs(floorObject.transform, floor);
            else
                BuildTopRailing(floorObject.transform, y);

            Transform doorGroup = DistrictKit.Group(floorObject.transform, "Doors");
            foreach (var door in doors)
                DistrictKit.Place(DistrictKit.Get("Door_Office"), doorGroup, door.Position, door.Yaw);

            Transform windowGroup = DistrictKit.Group(floorObject.transform, "Windows");
            foreach (var window in windows)
            {
                if (window.Floor != floor)
                    continue;
                GameObject instance = DistrictKit.Place(DistrictKit.Get("Window_Office"), windowGroup, Vector3.zero, 0f);
                instance.transform.SetLocalPositionAndRotation(
                    facade.Place(window.Side, window.Opening.Center, window.Opening.Middle, Wall * 0.5f), Facade.Facing(window.Side));
            }

            if (floor == 0)
            {
                // The way up is barred: only toxic liquid dissolves the grate.
                GameObject grate = DistrictKit.Place(DistrictKit.Get("MetalGrate"), floorObject.transform, new Vector3(17.15f, y, 7.1f), 0f);
                grate.name = "Stairwell Grate";
            }

            Color warm = new Color(1f, 0.88f, 0.68f);
            DistrictKit.PointLight(floorObject.transform, "Light Front", new Vector3(7f, ceiling - 0.3f, 3.5f), 11f, 1.3f, warm);
            DistrictKit.PointLight(floorObject.transform, "Light Back", new Vector3(8f, ceiling - 0.3f, 10.5f), 11f, 1.3f, warm);
        }

        private static void SlabRect(PropModel model, float x0, float x1, float z0, float z1, float top, string floorMaterial)
        {
            var center = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
            var size = new Vector2(x1 - x0, z1 - z0);
            model.Mesh.Box(new Vector3(center.x, top - Slab + 0.02f, center.z), new Vector3(size.x, 0.04f, size.y), "Ceiling");
            model.Mesh.Box(new Vector3(center.x, top - (Slab - 0.04f) * 0.5f, center.z), new Vector3(size.x, Slab - 0.04f, size.y), floorMaterial);
            model.Collider(new Vector3(center.x, top - Slab * 0.5f, center.z), new Vector3(size.x, Slab, size.y));
        }

        /// <summary>Partitions of each floor; the stairwell walls are the same on every floor.</summary>
        private static List<Partition> Plan(int floor)
        {
            var walls = new List<Partition>
            {
                X(7f, 7.2f, 16f, InnerX1, new Gap(16.4f, 17.9f, false)),
                Z(16f, 16.2f, 7.2f, InnerZ1),
            };

            switch (floor)
            {
                case 0:
                    // Reception | manager + hallway; manager | hallway; front | back rooms; open office | meeting.
                    walls.Add(Z(8f, 8.2f, Wall, 7f, new Gap(5.8f, 6.9f, false)));
                    walls.Add(X(5.5f, 5.7f, 8.2f, InnerX1, new Gap(12f, 13.1f, true)));
                    walls.Add(X(7f, 7.2f, Wall, 16f, new Gap(2f, 3.5f, false), new Gap(11f, 12.1f, true)));
                    walls.Add(Z(9f, 9.2f, 7.2f, InnerZ1, new Gap(10f, 11.1f, true)));
                    break;
                case 1:
                    // Storage room in the back-left corner; the rest is open plan.
                    walls.Add(Z(4f, 4.2f, 9.8f, InnerZ1, new Gap(11f, 12.1f, true)));
                    walls.Add(X(9.6f, 9.8f, Wall, 4.2f));
                    break;
                case 2:
                    // Two offices and an archive along the street, a corridor, a meeting room and an open lounge.
                    walls.Add(X(5.5f, 5.7f, Wall, InnerX1, new Gap(3f, 4.1f, true), new Gap(10.5f, 11.6f, true), new Gap(17f, 18.2f, false)));
                    walls.Add(Z(7.5f, 7.7f, Wall, 5.5f));
                    walls.Add(Z(15.5f, 15.7f, Wall, 5.5f));
                    walls.Add(X(7f, 7.2f, Wall, 9f, new Gap(4f, 5.1f, true)));
                    walls.Add(Z(9f, 9.2f, 7.2f, InnerZ1));
                    break;
                default:
                    // Boardroom and director's office in front, break room and storage at the back.
                    walls.Add(X(5.5f, 5.7f, Wall, InnerX1, new Gap(5f, 6.1f, true), new Gap(15f, 16.1f, true)));
                    walls.Add(Z(12f, 12.2f, Wall, 5.5f));
                    walls.Add(X(7f, 7.2f, Wall, 8f, new Gap(3f, 4.5f, false)));
                    walls.Add(Z(8f, 8.2f, 7.2f, InnerZ1));
                    walls.Add(X(9.6f, 9.8f, 8.2f, 16f, new Gap(11f, 12.1f, true)));
                    break;
            }

            return walls;
        }

        private static Partition X(float z0, float z1, float x0, float x1, params Gap[] gaps)
        {
            return new Partition { AlongX = true, Line0 = z0, Line1 = z1, From = x0, To = x1, Gaps = gaps };
        }

        private static Partition Z(float x0, float x1, float z0, float z1, params Gap[] gaps)
        {
            return new Partition { AlongX = false, Line0 = x0, Line1 = x1, From = z0, To = z1, Gaps = gaps };
        }

        private static void BuildPartition(PropModel model, Partition wall, float y, float ceiling, List<(Vector3, float)> doors)
        {
            float line = (wall.Line0 + wall.Line1) * 0.5f;
            float thickness = wall.Line1 - wall.Line0;

            void Piece(float a, float b, float y0, float y1)
            {
                if (b - a < 0.01f || y1 - y0 < 0.01f)
                    return;
                Vector3 center = wall.AlongX
                    ? new Vector3((a + b) * 0.5f, (y0 + y1) * 0.5f, line)
                    : new Vector3(line, (y0 + y1) * 0.5f, (a + b) * 0.5f);
                Vector3 size = wall.AlongX
                    ? new Vector3(b - a, y1 - y0, thickness)
                    : new Vector3(thickness, y1 - y0, b - a);
                model.Part(center, size, "OfficeWall", true);
            }

            float from = wall.From;
            foreach (Gap gap in wall.Gaps)
            {
                Piece(from, gap.A, y, ceiling);
                Piece(gap.A, gap.B, y + DoorHeight, ceiling);
                from = gap.B;

                if (gap.Leaf)
                {
                    float hinge = gap.A + 0.05f;
                    doors.Add(wall.AlongX
                        ? (new Vector3(hinge, y + 0.01f, line), 0f)
                        : (new Vector3(line, y + 0.01f, hinge), -90f));
                }
            }

            Piece(from, wall.To, y, ceiling);
        }

        // ───────────── Stairs ─────────────

        /// <summary>
        /// Flight A climbs +Z to the half landing, flight B climbs back -Z to the next floor's strip.
        /// Steps are only looks; smooth ramps underneath carry the player. A spine wall stands between the flights.
        /// </summary>
        private static void BuildStairs(Transform floorObject, int floor)
        {
            float y = FloorY(floor);
            float half = Storey * 0.5f;
            float rise = half / Steps;
            float run = (LandingZ - FlightZ0) / Steps;
            var model = new PropModel();

            for (int k = 0; k < Steps; k++)
            {
                float top = rise * (k + 1);
                float za = FlightZ0 + run * k;
                model.Mesh.Box(new Vector3((FlightAX0 + FlightAX1) * 0.5f, y + top * 0.5f, za + run * 0.5f), new Vector3(FlightAX1 - FlightAX0, top, run), "Concrete");
                model.Mesh.Box(new Vector3((FlightAX0 + FlightAX1) * 0.5f, y + top - 0.015f, za + 0.03f), new Vector3(FlightAX1 - FlightAX0, 0.03f, 0.06f), "HazardYellow");

                float topB = half + rise * (k + 1);
                float zb = LandingZ - run * (k + 1);
                model.Mesh.Box(new Vector3((FlightBX0 + FlightBX1) * 0.5f, y + topB * 0.5f, zb + run * 0.5f), new Vector3(FlightBX1 - FlightBX0, topB, run), "Concrete");
                model.Mesh.Box(new Vector3((FlightBX0 + FlightBX1) * 0.5f, y + topB - 0.015f, zb + run - 0.03f), new Vector3(FlightBX1 - FlightBX0, 0.03f, 0.06f), "HazardYellow");
            }

            model.Part(new Vector3((StairX0 + InnerX1) * 0.5f, y + half * 0.5f, (LandingZ + InnerZ1) * 0.5f), new Vector3(InnerX1 - StairX0, half, InnerZ1 - LandingZ), "Concrete", true);
            float spineTop = floor == Floors - 2 ? Storey + 1f : Storey;
            model.Part(new Vector3((FlightAX1 + FlightBX0) * 0.5f, y + spineTop * 0.5f, (FlightZ0 + LandingZ) * 0.5f), new Vector3(FlightBX0 - FlightAX1, spineTop, LandingZ - FlightZ0), "OfficeWall", true);

            GameObject stairs = DistrictKit.Bake(model, $"{Name}_Stairs{floor + 1}", floorObject);
            stairs.name = "Stairs";

            float length = Mathf.Sqrt((LandingZ - FlightZ0) * (LandingZ - FlightZ0) + half * half);
            float angle = Mathf.Atan2(half, LandingZ - FlightZ0) * Mathf.Rad2Deg;
            const float thickness = 0.1f;

            Quaternion upA = Quaternion.Euler(-angle, 0f, 0f);
            Vector3 midA = new Vector3((FlightAX0 + FlightAX1) * 0.5f, y + half * 0.5f, (FlightZ0 + LandingZ) * 0.5f);
            DistrictKit.RotatedCollider(stairs.transform, "Ramp A", midA - upA * Vector3.up * thickness * 0.5f, upA,
                new Vector3(FlightAX1 - FlightAX0, thickness, length + 0.05f));

            Quaternion upB = Quaternion.Euler(angle, 0f, 0f);
            Vector3 midB = new Vector3((FlightBX0 + FlightBX1) * 0.5f, y + half * 1.5f, (FlightZ0 + LandingZ) * 0.5f);
            DistrictKit.RotatedCollider(stairs.transform, "Ramp B", midB - upB * Vector3.up * thickness * 0.5f, upB,
                new Vector3(FlightBX1 - FlightBX0, thickness, length + 0.05f));
        }

        /// <summary>Top floor: a rail keeps the player from dropping into the stairwell beside the last flight.</summary>
        private static void BuildTopRailing(Transform floorObject, float y)
        {
            var model = new PropModel();
            model.Part(new Vector3((StairX0 + FlightAX1) * 0.5f, y + 0.5f, FlightZ0 - 0.03f), new Vector3(FlightAX1 - StairX0, 1f, 0.06f), "SteelDark", true);
            model.Part(new Vector3((StairX0 + FlightAX1) * 0.5f, y + 1.02f, FlightZ0 - 0.03f), new Vector3(FlightAX1 - StairX0 + 0.05f, 0.06f, 0.1f), "WoodDark");
            DistrictKit.Bake(model, $"{Name}_TopRailing", floorObject).name = "Stair Railing";
        }

        // ───────────── Furniture ─────────────

        /// <summary>
        /// Furnishes an office instance. <paramref name="parent"/> has the office's pose, so positions are building space.
        /// Almost everything burns and water pushes the movable pieces (their prefabs carry the push setup).
        /// </summary>
        public static void Furnish(Transform parent)
        {
            FurnishGround(parent, FloorY(0));
            FurnishSecond(parent, FloorY(1));
            FurnishThird(parent, FloorY(2));
            FurnishFourth(parent, FloorY(3));
        }

        private static void FurnishGround(Transform p, float y)
        {
            Transform reception = DistrictKit.Group(p, "Floor 1 · Reception");
            F(reception, "OfficeDistrict/Furniture/ReceptionCounter", 4.5f, y, 4.4f, 0f);
            F(reception, "OfficeDistrict/Furniture/OfficeChair", 4.5f, y, 5.4f, 0f);
            F(reception, "OfficeDistrict/Furniture/Computer", 4f, y + 0.77f, 4.95f, 0f, false);
            F(reception, "Interior/Rug", 2.4f, y, 3.2f, 90f, false);
            F(reception, "Interior/Sofa", 1f, y, 3.2f, -90f);
            F(reception, "Interior/CoffeeTable", 2.4f, y, 3.2f, 90f);
            F(reception, "Interior/Armchair", 2.4f, y, 5.2f, 0f);
            F(reception, "OfficeDistrict/Furniture/WaterCooler", 7.5f, y, 2.4f, 90f, false);
            F(reception, "OfficeDistrict/Furniture/OfficePlant", 0.8f, y, 0.9f, 0f);
            F(reception, "OfficeDistrict/Furniture/OfficePlant", 7.4f, y, 0.9f, 0f);
            F(reception, "OfficeDistrict/Furniture/OfficeBin", 6.9f, y, 6.2f, 0f);

            Transform manager = DistrictKit.Group(p, "Floor 1 · Manager's Office");
            DeskSet(manager, 14f, y, 2.6f, 0f);
            F(manager, "Interior/Bookshelf", 19.3f, y, 2.4f, 90f);
            F(manager, "OfficeDistrict/Furniture/FilingCabinet", 9f, y, 0.75f, 180f, false);
            F(manager, "OfficeDistrict/Furniture/FilingCabinet", 9.55f, y, 0.75f, 180f, false);
            F(manager, "Interior/Armchair", 16.5f, y, 4.4f, 0f);
            F(manager, "Interior/Armchair", 17.8f, y, 4.4f, 0f);
            F(manager, "OfficeDistrict/Furniture/OfficePlant", 19.1f, y, 4.9f, 0f);
            F(manager, "OfficeDistrict/Furniture/OfficeBin", 15.3f, y, 2f, 0f);
            F(manager, "OfficeDistrict/Furniture/PaperBox", 10.5f, y, 4.8f, 15f);

            Transform hallway = DistrictKit.Group(p, "Floor 1 · Hallway");
            F(hallway, "OfficeDistrict/Furniture/PaperBox", 15.4f, y, 6.3f, 0f);
            F(hallway, "OfficeDistrict/Furniture/PaperBox", 14.9f, y, 6.4f, 30f);

            Transform open = DistrictKit.Group(p, "Floor 1 · Open Office");
            foreach (float x in new[] { 1.5f, 3.2f, 5.5f, 7.5f })
            {
                DeskSet(open, x, y, 9.5f, 0f);
                DeskSet(open, x, y, 12.2f, 180f);
            }
            F(open, "OfficeDistrict/Furniture/DeskDivider", 2.35f, y, 10.85f, 0f);
            F(open, "OfficeDistrict/Furniture/DeskDivider", 6.5f, y, 10.85f, 0f);
            F(open, "OfficeDistrict/Furniture/Printer", 0.7f, y, 11f, -90f, false);
            F(open, "OfficeDistrict/Furniture/FilingCabinet", 8.55f, y, 13.2f, 0f, false);
            F(open, "OfficeDistrict/Furniture/PaperBox", 8.4f, y, 8f, 10f);
            F(open, "OfficeDistrict/Furniture/OfficePlant", 0.8f, y, 13.2f, 0f);

            Transform meeting = DistrictKit.Group(p, "Floor 1 · Meeting Room");
            MeetingSet(meeting, 12.6f, y, 10.6f);
            F(meeting, "OfficeDistrict/Furniture/Whiteboard", 12.6f, y, 13.3f, 0f, false);
            F(meeting, "OfficeDistrict/Furniture/OfficePlant", 15.5f, y, 13.2f, 0f);
            F(meeting, "OfficeDistrict/Furniture/OfficePlant", 9.8f, y, 7.8f, 0f);
        }

        private static void FurnishSecond(Transform p, float y)
        {
            Transform open = DistrictKit.Group(p, "Floor 2 · Open Plan");
            foreach (float x in new[] { 6.5f, 8.4f, 10.3f, 12.2f })
            {
                DeskSet(open, x, y, 2f, 0f);
                DeskSet(open, x, y, 4.6f, 180f);
            }
            F(open, "OfficeDistrict/Furniture/DeskDivider", 7.45f, y, 3.3f, 0f);
            F(open, "OfficeDistrict/Furniture/DeskDivider", 11.25f, y, 3.3f, 0f);
            foreach (float z in new[] { 5f, 5.6f, 6.2f })
                F(open, "OfficeDistrict/Furniture/FilingCabinet", 0.7f, y, z, -90f, false);
            F(open, "OfficeDistrict/Furniture/Printer", 2.5f, y, 0.8f, 180f, false);
            F(open, "OfficeDistrict/Furniture/WaterCooler", 19.3f, y, 5.5f, 90f, false);
            F(open, "OfficeDistrict/Furniture/OfficePlant", 19.2f, y, 0.8f, 0f);

            Transform lounge = DistrictKit.Group(p, "Floor 2 · Collaboration Area");
            F(lounge, "Interior/Rug", 9.5f, y, 11.6f, 0f, false);
            F(lounge, "Interior/Sofa", 8f, y, 12.9f, 0f);
            F(lounge, "Interior/Sofa", 11f, y, 12.9f, 0f);
            F(lounge, "Interior/CoffeeTable", 9.5f, y, 11.5f, 0f);
            F(lounge, "Interior/Armchair", 7f, y, 10.6f, -90f);
            F(lounge, "OfficeDistrict/Furniture/Whiteboard", 13.5f, y, 8.4f, 180f, false);
            F(lounge, "OfficeDistrict/Furniture/OfficePlant", 15.4f, y, 9f, 0f);
            F(lounge, "OfficeDistrict/Furniture/OfficePlant", 4.8f, y, 13.2f, 0f);

            Transform storage = DistrictKit.Group(p, "Floor 2 · Storage Room");
            F(storage, "OfficeDistrict/Furniture/StorageShelf", 2f, y, 13.3f, 0f);
            F(storage, "OfficeDistrict/Furniture/StorageShelf", 0.7f, y, 11.8f, -90f);
            F(storage, "OfficeDistrict/Furniture/PaperBox", 2.5f, y, 11f, 0f);
            F(storage, "OfficeDistrict/Furniture/PaperBox", 3.1f, y, 11.3f, 25f);
        }

        private static void FurnishThird(Transform p, float y)
        {
            Transform officeA = DistrictKit.Group(p, "Floor 3 · Manager Office A");
            DeskSet(officeA, 3.9f, y, 2.4f, 0f);
            F(officeA, "Interior/Bookshelf", 0.7f, y, 4.6f, -90f);
            F(officeA, "Interior/Armchair", 6.5f, y, 4.5f, 0f);
            F(officeA, "OfficeDistrict/Furniture/OfficePlant", 6.9f, y, 0.8f, 0f);

            Transform officeB = DistrictKit.Group(p, "Floor 3 · Manager Office B");
            DeskSet(officeB, 11.6f, y, 2.4f, 0f);
            F(officeB, "Interior/Sofa", 9f, y, 4.9f, 0f);
            F(officeB, "OfficeDistrict/Furniture/FilingCabinet", 15f, y, 0.75f, 180f, false);
            F(officeB, "OfficeDistrict/Furniture/OfficePlant", 8.2f, y, 0.8f, 0f);

            Transform archive = DistrictKit.Group(p, "Floor 3 · Archive");
            F(archive, "OfficeDistrict/Furniture/StorageShelf", 17.7f, y, 0.8f, 180f);
            F(archive, "OfficeDistrict/Furniture/StorageShelf", 19.3f, y, 3f, 90f);
            F(archive, "OfficeDistrict/Furniture/PaperBox", 16.6f, y, 3.5f, 0f);
            F(archive, "OfficeDistrict/Furniture/PaperBox", 16.9f, y, 4.1f, 40f);

            Transform meeting = DistrictKit.Group(p, "Floor 3 · Meeting Room");
            MeetingSet(meeting, 4.6f, y, 10.6f);
            F(meeting, "OfficeDistrict/Furniture/Whiteboard", 4.6f, y, 13.3f, 0f, false);
            F(meeting, "OfficeDistrict/Furniture/OfficePlant", 8.5f, y, 13.2f, 0f);

            Transform lounge = DistrictKit.Group(p, "Floor 3 · Lounge");
            F(lounge, "Interior/Rug", 12.6f, y, 11.6f, 0f, false);
            F(lounge, "Interior/Sofa", 12.6f, y, 13.1f, 0f);
            F(lounge, "Interior/CoffeeTable", 12.6f, y, 11.6f, 0f);
            F(lounge, "Interior/Armchair", 10.3f, y, 11.2f, -90f);
            F(lounge, "Interior/Bookshelf", 15.6f, y, 10.4f, 90f);
        }

        private static void FurnishFourth(Transform p, float y)
        {
            Transform board = DistrictKit.Group(p, "Floor 4 · Boardroom");
            F(board, "OfficeDistrict/Furniture/MeetingTable", 4.4f, y, 2.9f, 0f);
            F(board, "OfficeDistrict/Furniture/MeetingTable", 7.4f, y, 2.9f, 0f);
            foreach (float x in new[] { 3.2f, 5f, 6.8f, 8.6f })
            {
                F(board, "OfficeDistrict/Furniture/OfficeChair", x, y, 2f, 180f);
                F(board, "OfficeDistrict/Furniture/OfficeChair", x, y, 3.8f, 0f);
            }
            F(board, "OfficeDistrict/Furniture/Whiteboard", 11.5f, y, 2.9f, 90f, false);
            F(board, "OfficeDistrict/Furniture/OfficePlant", 0.8f, y, 0.8f, 0f);
            F(board, "OfficeDistrict/Furniture/OfficePlant", 0.8f, y, 5f, 0f);

            Transform director = DistrictKit.Group(p, "Floor 4 · Director's Office");
            DeskSet(director, 16f, y, 2.2f, 0f);
            F(director, "Interior/Bookshelf", 19.3f, y, 3.6f, 90f);
            F(director, "Interior/Sofa", 13.5f, y, 4.9f, 0f);
            F(director, "Interior/Rug", 16f, y, 2.6f, 0f, false);

            Transform breakRoom = DistrictKit.Group(p, "Floor 4 · Break Room");
            F(breakRoom, "OfficeDistrict/Furniture/Kitchenette", 2.2f, y, 13.3f, 0f);
            F(breakRoom, "Interior/DiningTable", 4.5f, y, 10.3f, 0f);
            F(breakRoom, "Interior/DiningChair", 3.7f, y, 10.3f, -90f);
            F(breakRoom, "Interior/DiningChair", 5.3f, y, 10.3f, 90f);
            F(breakRoom, "Interior/DiningChair", 4.5f, y, 9.5f, 180f);
            F(breakRoom, "OfficeDistrict/Furniture/WaterCooler", 7.5f, y, 8f, 90f, false);
            F(breakRoom, "OfficeDistrict/Furniture/OfficePlant", 0.8f, y, 7.8f, 0f);

            Transform storage = DistrictKit.Group(p, "Floor 4 · Storage");
            F(storage, "OfficeDistrict/Furniture/StorageShelf", 10f, y, 13.3f, 0f);
            F(storage, "OfficeDistrict/Furniture/StorageShelf", 13.5f, y, 13.3f, 0f);
            F(storage, "OfficeDistrict/Furniture/StorageShelf", 15.6f, y, 11.6f, 90f);
            F(storage, "OfficeDistrict/Furniture/PaperBox", 9.2f, y, 10.5f, 0f);
            F(storage, "OfficeDistrict/Furniture/PaperBox", 9.7f, y, 10.6f, 20f);
            F(storage, "OfficeDistrict/Factory/CrateStack", 12.5f, y, 11.2f, 0f);
        }

        /// <summary>Desk with a computer and a chair; the user sits on the desk's -Z side (before the yaw).</summary>
        private static void DeskSet(Transform parent, float x, float y, float z, float yaw)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            var at = new Vector3(x, y, z);
            F(parent, "OfficeDistrict/Furniture/OfficeDesk", x, y, z, yaw);
            Vector3 computer = at + turn * new Vector3(-0.15f, 0.75f, 0.05f);
            F(parent, "OfficeDistrict/Furniture/Computer", computer.x, computer.y, computer.z, yaw, false);
            // The chair's backrest is on its +Z side: turned round so the user faces the desk.
            Vector3 chair = at + turn * new Vector3(0f, 0f, -0.7f);
            F(parent, "OfficeDistrict/Furniture/OfficeChair", chair.x, y, chair.z, yaw + 180f);
        }

        /// <summary>3 m meeting table with three chairs on each long side.</summary>
        private static void MeetingSet(Transform parent, float x, float y, float z)
        {
            F(parent, "OfficeDistrict/Furniture/MeetingTable", x, y, z, 0f);
            foreach (float dx in new[] { -1f, 0f, 1f })
            {
                F(parent, "OfficeDistrict/Furniture/OfficeChair", x + dx, y, z - 1f, 180f);
                F(parent, "OfficeDistrict/Furniture/OfficeChair", x + dx, y, z + 1f, 0f);
            }
        }

        private static void F(Transform parent, string prop, float x, float y, float z, float yaw, bool flammable = true)
        {
            DistrictKit.Furnish(prop, parent, new Vector3(x, y, z), yaw, flammable);
        }
    }
}
