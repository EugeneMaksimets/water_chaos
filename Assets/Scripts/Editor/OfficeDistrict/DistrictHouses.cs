using System.Collections.Generic;
using UnityEngine;
using WaterChaos.EditorTools.MiniVillage;

namespace WaterChaos.EditorTools.OfficeDistrict
{
    /// <summary>Parameters of one brick city house.</summary>
    internal sealed class BrickHouseSpec
    {
        public string Name;
        public float Width = 9f;
        public float Depth = 10f;
        public int Storeys = 3;
        public float StoreyHeight = 3.2f;
        public string Brick = "BrickRed";
        /// <summary>The ground floor can be entered: real door, breakable windows, furnished rooms.</summary>
        public bool Accessible;
        /// <summary>Front door position along the front, in meters (m) from the left corner.</summary>
        public float DoorU = 1.5f;
        /// <summary>Ground floor partition along Z at this X; negative = one room.</summary>
        public float PartitionX = -1f;
        /// <summary>The doorway in the partition gets a door leaf.</summary>
        public bool PartitionDoor;
        public string LeftRoom = "Living Room";
        public string RightRoom = "Kitchen";
        public bool WaterTower;
        public bool Stoop;
    }

    /// <summary>Built house: prefab, its ground floor zone and its rooms (building space).</summary>
    internal sealed class BrickHouseInfo
    {
        public GameObject Prefab;
        public BrickHouseSpec Spec;
        public Bounds GroundFloor;
        public readonly List<KeyValuePair<string, Bounds>> Rooms = new List<KeyValuePair<string, Bounds>>();
    }

    /// <summary>
    /// Brick walk-ups like the reference: brick in red, brown or grey, cream lintels, cornice bands, quoins,
    /// a heavy cornice and often a water tower on the roof. Upper floors cannot be entered: their windows are
    /// lit or dark panels. Accessible houses have a furnished ground floor (one temperature zone).
    /// </summary>
    internal static class DistrictHouses
    {
        private const float Wall = 0.3f;
        private const float GroundTop = 0.15f;
        private const float WindowWidth = 1f;
        private const float WindowHeight = 1.6f;
        private const float DoorWidth = 1.2f;

        public static BrickHouseInfo Build(BrickHouseSpec spec)
        {
            var info = new BrickHouseInfo { Spec = spec };
            var facade = new Facade(spec.Width, spec.Depth, Wall);
            var model = new PropModel();
            float top = spec.Storeys * spec.StoreyHeight + GroundTop;
            float parapet = top + 0.8f;
            float groundCeiling = spec.StoreyHeight - 0.05f;
            var real = new List<(Side, Opening)>();
            var fake = new List<(Side, float U, float Y, bool Lit)>();
            int litIndex = spec.Name.Length;

            foreach (Side side in new[] { Side.Front, Side.Back, Side.Left, Side.Right })
            {
                var openings = new List<Opening>();
                float length = side == Side.Front || side == Side.Back ? spec.Width : spec.Depth;
                List<float> centers = WindowCenters(length);
                for (int storey = 0; storey < spec.Storeys; storey++)
                {
                    float sill = storey * spec.StoreyHeight + GroundTop + 0.95f;
                    foreach (float u in centers)
                    {
                        var opening = new Opening(u - WindowWidth * 0.5f, u + WindowWidth * 0.5f, sill, sill + WindowHeight);
                        if (side == Side.Front && storey == 0 && Overlaps(opening, spec.DoorU - 0.3f, spec.DoorU + DoorWidth + 0.3f))
                            continue;

                        facade.Lintel(model, side, opening);
                        if (storey == 0 && spec.Accessible)
                        {
                            openings.Add(opening);
                            real.Add((side, opening));
                        }
                        else
                        {
                            // Roughly every third window glows, like the reference's evening street.
                            fake.Add((side, u, opening.Middle, (litIndex++ * 7) % 3 == 0));
                        }
                    }
                }

                if (side == Side.Front)
                {
                    float doorBottom = spec.Stoop ? 0.68f : 0f;
                    var door = new Opening(spec.DoorU, spec.DoorU + DoorWidth, doorBottom, GroundTop + doorBottom + 2.35f);
                    facade.Lintel(model, side, door);
                    if (spec.Accessible)
                        openings.Add(door);
                    else
                        model.Mesh.Box(facade.Place(side, door.Center, door.Middle, -0.01f), facade.Size(side, DoorWidth, door.Y1 - door.Y0, 0.04f), "Door");
                }

                facade.Wall(model, side, parapet, openings, spec.Brick, "InteriorWall", 0.6f);
            }

            for (int storey = 1; storey < spec.Storeys; storey++)
                facade.Band(model, storey * spec.StoreyHeight + GroundTop - 0.05f, 0.16f, 0.07f);
            facade.Cornice(model, top - 0.05f, parapet);
            facade.Quoins(model, 0.6f, top - 0.2f);

            float innerW = spec.Width - Wall * 2f;
            float innerD = spec.Depth - Wall * 2f;
            var center = new Vector3(spec.Width * 0.5f, 0f, spec.Depth * 0.5f);
            model.Part(new Vector3(center.x, GroundTop * 0.5f, center.z), new Vector3(innerW, GroundTop, innerD), "WoodFloor", true);
            // Closed upper floors: one slab over the ground floor, one roof slab.
            model.Part(new Vector3(center.x, groundCeiling + 0.12f, center.z), new Vector3(innerW, 0.24f, innerD), "Ceiling", true);
            model.Part(new Vector3(center.x, top - 0.12f, center.z), new Vector3(innerW, 0.24f, innerD), "RoofDark", true);

            if (spec.Accessible)
            {
                model.Part(new Vector3(spec.DoorU + DoorWidth * 0.5f, GroundTop * 0.5f, Wall * 0.5f), new Vector3(DoorWidth, GroundTop, Wall), "Stone", true);
                if (spec.PartitionX > 0f)
                    BuildPartition(model, spec, groundCeiling);
            }

            GameObject root = VillageAssets.CreateMeshObject(spec.Name, model.Mesh, spec.Name);
            VillageAssets.AddColliders(root, model.Colliders);
            DistrictKit.MarkStatic(root);

            Transform windows = DistrictKit.Group(root.transform, "Windows");
            foreach (var window in real)
            {
                GameObject instance = DistrictKit.Place(DistrictKit.Get("Window_Brownstone"), windows, Vector3.zero, 0f);
                instance.transform.SetLocalPositionAndRotation(facade.Place(window.Item1, window.Item2.Center, window.Item2.Middle, Wall * 0.5f), Facade.Facing(window.Item1));
            }

            Transform facadeWindows = DistrictKit.Group(root.transform, "Facade Windows");
            foreach (var window in fake)
            {
                GameObject instance = DistrictKit.Place(DistrictKit.Get(window.Lit ? "Window_LitFake" : "Window_DarkFake"), facadeWindows, Vector3.zero, 0f);
                instance.transform.SetLocalPositionAndRotation(facade.Place(window.Item1, window.U, window.Y, -0.075f), Facade.Facing(window.Item1));
            }
            DistrictKit.MarkStatic(facadeWindows.gameObject);

            if (spec.Accessible)
            {
                Transform doors = DistrictKit.Group(root.transform, "Doors");
                DistrictKit.Place(DistrictKit.Get("Door_Brownstone"), doors, new Vector3(spec.DoorU + 0.05f, GroundTop + 0.01f, Wall * 0.5f), 0f);
                if (spec.PartitionX > 0f && spec.PartitionDoor)
                    DistrictKit.Place(DistrictKit.Get("Door_Office"), doors, new Vector3(spec.PartitionX, GroundTop + 0.01f, PartitionDoorZ(spec) + 0.05f), -90f);
                DistrictKit.PointLight(root.transform, "Room Light", new Vector3(center.x, groundCeiling - 0.4f, center.z), Mathf.Max(spec.Width, spec.Depth), 1.4f, new Color(1f, 0.84f, 0.6f));
            }

            if (spec.Stoop)
                DistrictKit.Place(DistrictKit.Get("Stoop"), root.transform, new Vector3(spec.DoorU + DoorWidth * 0.5f, 0f, 0f), 0f);

            Transform roof = DistrictKit.Group(root.transform, "Roof");
            if (spec.WaterTower)
                DistrictKit.Place(DistrictKit.Get("WaterTower"), roof, new Vector3(spec.Width * 0.6f, top, spec.Depth * 0.6f), 0f);
            DistrictKit.Place(DistrictKit.Get("RoofChimney"), roof, new Vector3(spec.Width * 0.2f, top, spec.Depth * 0.8f), 0f);
            DistrictKit.MarkStatic(roof.gameObject);

            float y1 = groundCeiling;
            info.GroundFloor = new Bounds(new Vector3(center.x, (GroundTop + y1) * 0.5f, center.z), new Vector3(innerW, y1 - GroundTop, innerD));
            if (spec.PartitionX > 0f)
            {
                AddRoom(info, spec.LeftRoom, Wall, spec.PartitionX - 0.1f, spec.Depth, y1);
                AddRoom(info, spec.RightRoom, spec.PartitionX + 0.1f, spec.Width - Wall, spec.Depth, y1);
            }
            else
            {
                AddRoom(info, spec.LeftRoom, Wall, spec.Width - Wall, spec.Depth, y1);
            }

            info.Prefab = VillageAssets.SavePrefab(root, DistrictKit.BuildingFolder, spec.Name);
            return info;
        }

        private static List<float> WindowCenters(float length)
        {
            var centers = new List<float>();
            int count = Mathf.Max(1, Mathf.FloorToInt((length - 1.2f) / 2.1f));
            float step = (length - 1.2f) / count;
            for (int i = 0; i < count; i++)
                centers.Add(0.6f + step * (i + 0.5f));
            return centers;
        }

        private static bool Overlaps(Opening opening, float a, float b)
        {
            return opening.U1 > a && opening.U0 < b;
        }

        private static float PartitionDoorZ(BrickHouseSpec spec) => spec.Depth * 0.6f - 0.55f;

        private static void BuildPartition(PropModel model, BrickHouseSpec spec, float ceiling)
        {
            const float half = 0.1f;
            float x = spec.PartitionX;
            float z0 = Wall;
            float z1 = spec.Depth - Wall;
            float d0 = PartitionDoorZ(spec);
            float d1 = d0 + 1.1f;

            void Piece(float a, float b, float y0, float y1)
            {
                var size = new Vector3(half * 2f, y1 - y0, b - a);
                model.Part(new Vector3(x, (y0 + y1) * 0.5f, (a + b) * 0.5f), size, "InteriorWall", true);
            }

            Piece(z0, d0, GroundTop, ceiling);
            Piece(d1, z1, GroundTop, ceiling);
            Piece(d0, d1, GroundTop + 2.3f, ceiling);
        }

        private static void AddRoom(BrickHouseInfo info, string name, float x0, float x1, float depth, float ceiling)
        {
            var center = new Vector3((x0 + x1) * 0.5f, (GroundTop + ceiling) * 0.5f, depth * 0.5f);
            var size = new Vector3(x1 - x0, ceiling - GroundTop, depth - Wall * 2f);
            info.Rooms.Add(new KeyValuePair<string, Bounds>(name, new Bounds(center, size)));
        }

        // ───────────── Furniture ─────────────

        /// <summary>Furnishes the accessible ground floor; <paramref name="parent"/> has the house's pose.</summary>
        public static void Furnish(BrickHouseInfo house, Transform parent)
        {
            const float y = GroundTop;
            foreach (KeyValuePair<string, Bounds> room in house.Rooms)
            {
                Transform group = DistrictKit.Group(parent, room.Key);
                Bounds b = room.Value;
                switch (room.Key)
                {
                    case "Kitchen":
                        F(group, "Interior/KitchenCounter", b.center.x, y, b.max.z - 0.35f, 0f, true);
                        F(group, "Interior/DiningTable", b.center.x, y, b.center.z - 0.5f, 0f, true);
                        F(group, "Interior/DiningChair", b.center.x - 0.8f, y, b.center.z - 0.5f, -90f, true);
                        F(group, "Interior/DiningChair", b.center.x + 0.8f, y, b.center.z - 0.5f, 90f, true);
                        F(group, "OfficeDistrict/Furniture/OfficeBin", b.max.x - 0.4f, y, b.min.z + 0.5f, 0f, true);
                        F(group, "Interior/Vase", b.center.x, y + 0.76f, b.center.z - 0.5f, 0f, true);
                        break;
                    case "Bedroom":
                        F(group, "Interior/Bed", b.center.x, y, b.max.z - 1.2f, 180f, true);
                        F(group, "Interior/Nightstand", b.center.x + 1.2f, y, b.max.z - 0.35f, 0f, true);
                        F(group, "Interior/Wardrobe", b.max.x - 0.4f, y, b.min.z + 1.2f, 90f, true);
                        F(group, "Interior/Rug", b.center.x, y, b.center.z, 0f, true);
                        F(group, "Interior/TableLamp", b.center.x + 1.2f, y + 0.55f, b.max.z - 0.35f, 0f, false);
                        break;
                    default:
                        F(group, "Interior/Rug", b.center.x, y, b.center.z, 0f, true);
                        F(group, "Interior/Sofa", b.center.x, y, b.max.z - 0.6f, 0f, true);
                        F(group, "Interior/CoffeeTable", b.center.x, y, b.center.z, 0f, true);
                        F(group, "Interior/TVStand", b.center.x, y, b.min.z + 1.6f, 180f, true);
                        F(group, "Interior/Television", b.center.x, y + 0.5f, b.min.z + 1.6f, 180f, false);
                        F(group, "Interior/Armchair", b.min.x + 0.6f, y, b.center.z, -90f, true);
                        F(group, "Interior/Bookshelf", b.min.x + 0.3f, y, b.max.z - 1.6f, -90f, true);
                        F(group, "Interior/FloorLamp", b.max.x - 0.4f, y, b.max.z - 0.4f, 0f, false);
                        F(group, "OfficeDistrict/Furniture/OfficePlant", b.max.x - 0.4f, y, b.min.z + 2.4f, 0f, true);
                        F(group, "OfficeDistrict/Furniture/PaperBox", b.min.x + 0.6f, y, b.min.z + 2.4f, 15f, true);
                        break;
                }
            }
        }

        private static void F(Transform parent, string prop, float x, float y, float z, float yaw, bool flammable)
        {
            DistrictKit.Furnish(prop, parent, new Vector3(x, y, z), yaw, flammable);
        }
    }
}
