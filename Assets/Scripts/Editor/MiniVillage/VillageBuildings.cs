using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WaterChaos.EditorTools.MiniVillage
{
    internal enum PorchKind
    {
        None,
        Full,
        Small,
        Canopy
    }

    /// <summary>Parameters of one building made by <see cref="VillageBuildings"/>.</summary>
    internal sealed class HouseSpec
    {
        public string Name;
        public float Width = 10f;
        public float Depth = 8f;
        public int Floors = 1;
        public float StoreyHeight = 3f;
        public string Exterior = "SidingBeige";
        public string InteriorLeft = "InteriorWall";
        public string InteriorRight = "InteriorWall";
        public string Roof = "Roof";
        public string Window = "Window_House";
        public string Door = "Door_Wood";
        public PorchKind Porch;
        public bool Chimney;
        public bool FlatRoof;
        public bool Wainscot = true;
        public float RoofHeight = 2.4f;
        /// <summary>Interior wall position as a share of the width right of the centre; negative = one room.</summary>
        public float Partition = 0.18f;
        public string LeftRoom = "Living Room";
        public string RightRoom = "Kitchen";
        public bool ShopFront;
        public string Sign;
    }

    /// <summary>Built house prefab plus its ground-floor rooms (local space) for furniture and temperature.</summary>
    internal sealed class HouseInfo
    {
        public GameObject Prefab;
        public HouseSpec Spec;
        public readonly List<KeyValuePair<string, Bounds>> Rooms = new List<KeyValuePair<string, Bounds>>();
    }

    /// <summary>
    /// Buildings of the village: houses, shop, pool house, gas station canopy and price sign, plus the
    /// breakable windows and hinged doors they use. House local space: front wall outer face at z = 0
    /// facing -Z, the house extends to z = Depth, x from -Width/2 to Width/2, ground floor top at 0.15 m.
    /// Walls are cut around their openings, so windows and doors sit in real holes.
    /// </summary>
    internal static class VillageBuildings
    {
        public const float FloorTop = 0.15f;
        private const float Wall = 0.25f;
        private const float Skin = 0.12f;
        private const float FoundationTop = 0.45f;
        private const float DoorWidth = 1.6f;
        private const float DoorTop = 2.45f;

        private static readonly Dictionary<string, GameObject> Openings = new Dictionary<string, GameObject>();

        private enum Side
        {
            Front,
            Back,
            Left,
            Right
        }

        private struct Opening
        {
            public float U0, U1, Y0, Y1;
            public string Prefab;
        }

        public static void BuildWindowsAndDoors()
        {
            Openings.Clear();
            Openings["Window_House"] = VillageAssets.SaveWindow("Window_House", 1.1f, 1.3f, "Trim", "ShutterBrown", true, 2, 2, 60f, 120f, 360f);
            Openings["Window_HouseGreen"] = VillageAssets.SaveWindow("Window_HouseGreen", 1.1f, 1.3f, "Trim", "ShutterGreen", true, 2, 2, 60f, 120f, 360f);
            Openings["Window_Plain"] = VillageAssets.SaveWindow("Window_Plain", 1.1f, 1.3f, "Trim", null, true, 2, 2, 60f, 120f, 360f);
            Openings["Window_Shop"] = VillageAssets.SaveWindow("Window_Shop", 2.4f, 2f, "MetalDark", null, false, 3, 2, 80f, 200f, 600f);
            Openings["Door_Wood"] = VillageAssets.SaveDoor("Door_Wood", DoorWidth - 0.1f, 2.25f, "Door", false);
            Openings["Door_Red"] = VillageAssets.SaveDoor("Door_Red", DoorWidth - 0.1f, 2.25f, "DoorRed", false);
            Openings["Door_Shop"] = VillageAssets.SaveDoor("Door_Shop", DoorWidth - 0.1f, 2.25f, "MetalDark", true);
        }

        // ───────────── Houses ─────────────

        public static HouseInfo BuildHouse(HouseSpec spec)
        {
            var info = new HouseInfo { Spec = spec };
            var model = new PropModel();
            var rotatedColliders = new List<(Vector3 Center, Vector3 Size, Quaternion Rotation)>();
            var placements = new List<(string Prefab, Vector3 Position, Quaternion Rotation)>();

            float w = spec.Width;
            float d = spec.Depth;
            float hw = w * 0.5f;
            float wallTop = spec.Floors * spec.StoreyHeight + FloorTop;
            float groundCeiling = spec.Floors > 1 ? spec.StoreyHeight : wallTop - FloorTop;

            Dictionary<Side, List<Opening>> openings = PlanOpenings(spec, wallTop);
            foreach (KeyValuePair<Side, List<Opening>> side in openings)
            {
                BuildWall(model, spec, side.Key, side.Value, wallTop);
                foreach (Opening opening in side.Value)
                {
                    if (opening.Prefab.StartsWith("Door"))
                        continue;
                    Vector3 center = Place(spec, side.Key, (opening.U0 + opening.U1) * 0.5f, (opening.Y0 + opening.Y1) * 0.5f, Wall * 0.5f);
                    placements.Add((opening.Prefab, center, Facing(side.Key)));
                }
            }

            BuildCorners(model, spec, wallTop);
            BuildFloors(model, spec, wallTop);
            BuildDoorFrame(model, spec, placements);

            float partition = spec.Partition >= 0f ? w * spec.Partition : float.NaN;
            if (!float.IsNaN(partition))
                BuildPartition(model, spec, partition, groundCeiling);

            if (spec.FlatRoof)
                BuildFlatRoof(model, spec, wallTop);
            else
                BuildGableRoof(model, spec, wallTop, rotatedColliders);

            BuildPorch(model, spec);

            // Rooms of the ground floor, split by the partition.
            float inner = hw - Wall;
            if (float.IsNaN(partition))
            {
                AddRoom(info, spec.LeftRoom, -inner, inner, d, groundCeiling);
            }
            else
            {
                AddRoom(info, spec.LeftRoom, -inner, partition - 0.075f, d, groundCeiling);
                AddRoom(info, spec.RightRoom, partition + 0.075f, inner, d, groundCeiling);
            }

            GameObject root = VillageAssets.CreateMeshObject(spec.Name, model.Mesh, spec.Name);
            VillageAssets.AddColliders(root, model.Colliders);
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);

            for (int i = 0; i < rotatedColliders.Count; i++)
            {
                var child = new GameObject("RoofCollider_" + i);
                child.transform.SetParent(root.transform, false);
                child.transform.SetLocalPositionAndRotation(rotatedColliders[i].Center, rotatedColliders[i].Rotation);
                child.AddComponent<BoxCollider>().size = rotatedColliders[i].Size;
            }

            foreach (var placement in placements)
                Instance(Openings[placement.Prefab], root.transform, placement.Position, placement.Rotation);

            foreach (KeyValuePair<string, Bounds> room in info.Rooms)
                AddRoomLight(root.transform, room.Value, groundCeiling);

            if (!string.IsNullOrEmpty(spec.Sign))
                VillageAssets.Label(root.transform, spec.Sign, new Vector3(0f, wallTop + 0.15f, -0.29f), 0.6f, Color.white, Quaternion.identity);

            info.Prefab = VillageAssets.SavePrefab(root, "Buildings", spec.Name);
            return info;
        }

        private static Dictionary<Side, List<Opening>> PlanOpenings(HouseSpec spec, float wallTop)
        {
            float w = spec.Width;
            float d = spec.Depth;
            var result = new Dictionary<Side, List<Opening>>
            {
                { Side.Front, new List<Opening>() },
                { Side.Back, new List<Opening>() },
                { Side.Left, new List<Opening>() },
                { Side.Right, new List<Opening>() },
            };

            result[Side.Front].Add(new Opening { U0 = -DoorWidth * 0.5f, U1 = DoorWidth * 0.5f, Y0 = FloorTop, Y1 = DoorTop, Prefab = spec.Door });

            for (int floor = 0; floor < spec.Floors; floor++)
            {
                float bottom = floor * spec.StoreyHeight + FloorTop + (floor == 0 ? 0.85f : 0.75f);
                float top = bottom + 1.3f;
                if (top > wallTop - 0.3f)
                    continue;

                if (floor == 0 && spec.ShopFront)
                {
                    foreach (float side in new[] { -1f, 1f })
                        result[Side.Front].Add(Window(side * w * 0.28f, 2.4f, FloorTop + 0.45f, FloorTop + 2.45f, "Window_Shop"));
                }
                else if (floor == 0)
                {
                    // Only where the window clears both the door and the wall corner.
                    float x = w * 0.28f;
                    if (x - 0.55f > DoorWidth * 0.5f + 0.1f && x + 0.55f < w * 0.5f - Wall - 0.05f)
                    {
                        foreach (float side in new[] { -1f, 1f })
                            result[Side.Front].Add(Window(side * x, 1.1f, bottom, top, spec.Window));
                    }
                }
                else
                {
                    foreach (float x in new[] { -0.3f, 0f, 0.3f })
                        result[Side.Front].Add(Window(x * w, 1.1f, bottom, top, spec.Window));
                }

                foreach (float side in new[] { -1f, 1f })
                    result[Side.Back].Add(Window(side * w * 0.28f, 1.1f, bottom, top, spec.Window));

                result[Side.Left].Add(Window(d * 0.5f, 1.1f, bottom, top, spec.Window));
                result[Side.Right].Add(Window(d * 0.5f, 1.1f, bottom, top, spec.Window));
            }

            return result;
        }

        private static Opening Window(float center, float width, float bottom, float top, string prefab)
        {
            return new Opening { U0 = center - width * 0.5f, U1 = center + width * 0.5f, Y0 = bottom, Y1 = top, Prefab = prefab };
        }

        /// <summary>
        /// Cuts the wall into vertical slices at every opening edge, then fills each slice above and below
        /// its openings: exterior skin (foundation band at the bottom), interior skin and one collider per piece.
        /// </summary>
        private static void BuildWall(PropModel model, HouseSpec spec, Side side, List<Opening> openings, float wallTop)
        {
            bool alongX = side == Side.Front || side == Side.Back;
            float uMin = alongX ? -spec.Width * 0.5f : Wall;
            float uMax = alongX ? spec.Width * 0.5f : spec.Depth - Wall;
            string interior = side == Side.Right ? spec.InteriorRight : side == Side.Left ? spec.InteriorLeft
                : spec.InteriorLeft;

            var cuts = new SortedSet<float> { uMin, uMax };
            foreach (Opening opening in openings)
            {
                cuts.Add(Mathf.Clamp(opening.U0, uMin, uMax));
                cuts.Add(Mathf.Clamp(opening.U1, uMin, uMax));
            }

            float[] edges = cuts.ToArray();
            for (int i = 0; i < edges.Length - 1; i++)
            {
                float a = edges[i];
                float b = edges[i + 1];
                if (b - a < 0.001f)
                    continue;

                float y = 0f;
                foreach (Opening gap in openings.Where(o => o.U0 <= a + 0.0001f && o.U1 >= b - 0.0001f).OrderBy(o => o.Y0))
                {
                    if (gap.Y0 > y)
                        WallPiece(model, spec, side, a, b, y, gap.Y0, interior);
                    y = Mathf.Max(y, gap.Y1);
                }

                if (y < wallTop)
                    WallPiece(model, spec, side, a, b, y, wallTop, interior);
            }

            // Trim band between storeys, and a taller frieze under the eaves that also covers the joint
            // between the wall top and the roof slabs (they sit a little above the wall).
            for (int floor = 1; floor <= spec.Floors; floor++)
            {
                bool eaves = floor == spec.Floors;
                float bandY = eaves ? wallTop - 0.05f : floor * spec.StoreyHeight + 0.1f;
                float bandHeight = eaves ? 0.3f : 0.14f;
                Vector3 center = Place(spec, side, (uMin + uMax) * 0.5f, bandY, -0.02f);
                model.Mesh.Box(center, Size(side, uMax - uMin + (alongX ? 0.1f : 0f), bandHeight, 0.05f), "Trim");
            }
        }

        private static void WallPiece(PropModel model, HouseSpec spec, Side side, float a, float b, float y0, float y1, string interior)
        {
            float u = (a + b) * 0.5f;
            float length = b - a;

            if (y0 < FoundationTop)
            {
                float top = Mathf.Min(y1, FoundationTop);
                model.Mesh.Box(Place(spec, side, u, (y0 + top) * 0.5f, Skin * 0.5f), Size(side, length, top - y0, Skin), "Foundation");
            }
            if (y1 > FoundationTop)
            {
                float bottom = Mathf.Max(y0, FoundationTop);
                model.Mesh.Box(Place(spec, side, u, (bottom + y1) * 0.5f, Skin * 0.5f), Size(side, length, y1 - bottom, Skin), spec.Exterior);
            }

            float innerDepth = Skin + (Wall - Skin) * 0.5f;
            const float wainscotTop = 1.0f;
            if (spec.Wainscot && y0 < wainscotTop)
            {
                float top = Mathf.Min(y1, wainscotTop);
                model.Mesh.Box(Place(spec, side, u, (y0 + top) * 0.5f, innerDepth), Size(side, length, top - y0, Wall - Skin), "Wainscot");
                if (y1 > wainscotTop)
                    model.Mesh.Box(Place(spec, side, u, (wainscotTop + y1) * 0.5f, innerDepth), Size(side, length, y1 - wainscotTop, Wall - Skin), interior);
            }
            else
            {
                model.Mesh.Box(Place(spec, side, u, (y0 + y1) * 0.5f, innerDepth), Size(side, length, y1 - y0, Wall - Skin), interior);
            }

            model.Collider(Place(spec, side, u, (y0 + y1) * 0.5f, Wall * 0.5f), Size(side, length, y1 - y0, Wall));
        }

        /// <summary>Local point on a wall: <paramref name="u"/> along it, <paramref name="depth"/> inwards from its outer face.</summary>
        private static Vector3 Place(HouseSpec spec, Side side, float u, float y, float depth)
        {
            float hw = spec.Width * 0.5f;
            switch (side)
            {
                case Side.Front: return new Vector3(u, y, depth);
                case Side.Back: return new Vector3(u, y, spec.Depth - depth);
                case Side.Left: return new Vector3(-hw + depth, y, u);
                default: return new Vector3(hw - depth, y, u);
            }
        }

        private static Vector3 Size(Side side, float length, float height, float thickness)
        {
            return side == Side.Front || side == Side.Back
                ? new Vector3(length, height, thickness)
                : new Vector3(thickness, height, length);
        }

        private static Quaternion Facing(Side side)
        {
            switch (side)
            {
                case Side.Front: return Quaternion.identity;
                case Side.Back: return Quaternion.Euler(0f, 180f, 0f);
                case Side.Left: return Quaternion.Euler(0f, 90f, 0f);
                default: return Quaternion.Euler(0f, -90f, 0f);
            }
        }

        private static void BuildCorners(PropModel model, HouseSpec spec, float wallTop)
        {
            float hw = spec.Width * 0.5f;
            foreach (float x in new[] { -hw, hw })
            {
                foreach (float z in new[] { 0f, spec.Depth })
                    model.Mesh.Box(new Vector3(x, wallTop * 0.5f, z), new Vector3(0.16f, wallTop, 0.16f), "Trim");
            }
        }

        private static void BuildFloors(PropModel model, HouseSpec spec, float wallTop)
        {
            float innerW = spec.Width - Wall * 2f;
            float innerD = spec.Depth - Wall * 2f;
            float cz = spec.Depth * 0.5f;

            model.Part(new Vector3(0f, FloorTop * 0.5f, cz), new Vector3(innerW, FloorTop, innerD), "WoodFloor", true);

            for (int floor = 1; floor < spec.Floors; floor++)
            {
                float y = floor * spec.StoreyHeight;
                model.Part(new Vector3(0f, y + 0.02f, cz), new Vector3(innerW, 0.04f, innerD), "Ceiling");
                model.Part(new Vector3(0f, y + 0.12f, cz), new Vector3(innerW, 0.16f, innerD), "WoodFloor");
                model.Collider(new Vector3(0f, y + 0.1f, cz), new Vector3(innerW, 0.2f, innerD));
            }

            model.Part(new Vector3(0f, wallTop - 0.075f, cz), new Vector3(innerW, 0.15f, innerD), "Ceiling", true);
        }

        /// <summary>
        /// Casing around the front door, stop strips on its outer side (they stop the hinged leaf when it
        /// closes), and the door leaf itself, placed wide open into the house.
        /// </summary>
        private static void BuildDoorFrame(PropModel model, HouseSpec spec, List<(string, Vector3, Quaternion)> placements)
        {
            float u0 = -DoorWidth * 0.5f;
            float u1 = DoorWidth * 0.5f;
            float height = DoorTop - FloorTop;

            foreach (float x in new[] { u0 - 0.07f, u1 + 0.07f })
                model.Mesh.Box(new Vector3(x, FloorTop + height * 0.5f + 0.03f, -0.02f), new Vector3(0.14f, height + 0.06f, 0.05f), "Trim");
            model.Mesh.Box(new Vector3(0f, DoorTop + 0.07f, -0.02f), new Vector3(DoorWidth + 0.28f, 0.14f, 0.05f), "Trim");

            const float stopDepth = 0.0625f;
            var stopSize = new Vector3(0.07f, height, 0.055f);
            model.Part(new Vector3(u0 + 0.035f, FloorTop + height * 0.5f, stopDepth), stopSize, "Trim", true);
            model.Part(new Vector3(u1 - 0.035f, FloorTop + height * 0.5f, stopDepth), stopSize, "Trim", true);
            model.Part(new Vector3(0f, DoorTop - 0.035f, stopDepth), new Vector3(DoorWidth, 0.07f, 0.055f), "Trim", true);

            placements.Add((spec.Door, new Vector3(u0 + 0.05f, FloorTop + 0.01f, Wall * 0.5f), Quaternion.Euler(0f, -90f, 0f)));
        }

        private static void BuildPartition(PropModel model, HouseSpec spec, float x, float ceiling)
        {
            const float half = 0.075f;
            float z0 = Wall;
            float z1 = spec.Depth - Wall;
            float doorCenter = spec.Depth * 0.6f;
            float d0 = doorCenter - DoorWidth * 0.5f;
            float d1 = doorCenter + DoorWidth * 0.5f;

            void Piece(float a, float b, float y0, float y1)
            {
                var center = new Vector3(x, (y0 + y1) * 0.5f, (a + b) * 0.5f);
                var size = new Vector3(half, y1 - y0, b - a);
                model.Mesh.Box(center - Vector3.right * half * 0.5f, size, spec.InteriorLeft);
                model.Mesh.Box(center + Vector3.right * half * 0.5f, size, spec.InteriorRight);
                model.Collider(center, new Vector3(half * 2f, y1 - y0, b - a));
            }

            Piece(z0, d0, FloorTop, ceiling);
            Piece(d1, z1, FloorTop, ceiling);
            Piece(d0, d1, DoorTop, ceiling);
        }

        private static void BuildGableRoof(PropModel model, HouseSpec spec, float wallTop, List<(Vector3, Vector3, Quaternion)> colliders)
        {
            const float overhang = 0.45f;
            float w = spec.Width;
            float half = spec.Depth * 0.5f;
            float height = spec.RoofHeight;
            var ridge = new Vector3(0f, wallTop + height, half);

            model.Mesh.Gable(new Vector3(0f, wallTop, half), w, spec.Depth, height, spec.Exterior);

            // Roof slabs sit 3 cm above the gable slopes (coplanar faces would flicker) and run a little past
            // the ridge so the two slopes overlap there; white barge boards close the gap at the gable ends.
            const float lift = 0.03f;
            const float thickness = 0.16f;
            const float pastRidge = 0.25f;
            float slope = Mathf.Atan2(height, half);
            float length = (half + overhang) / Mathf.Cos(slope) + pastRidge;
            foreach (float sign in new[] { -1f, 1f })
            {
                var direction = new Vector3(0f, -Mathf.Sin(slope), sign * Mathf.Cos(slope));
                var normal = new Vector3(0f, Mathf.Cos(slope), sign * Mathf.Sin(slope));
                Vector3 center = ridge + direction * (length * 0.5f - pastRidge) + normal * (thickness * 0.5f + lift);
                Quaternion rotation = Quaternion.LookRotation(direction, normal);
                var size = new Vector3(w + 0.7f, thickness, length);

                model.Mesh.SetTransform(center, rotation);
                model.Mesh.Box(Vector3.zero, size, spec.Roof);
                // Fascia board: its face stands 1.5 cm in front of the slab end (a shared plane would flicker).
                model.Mesh.Box(new Vector3(0f, -0.05f, length * 0.5f - 0.025f), new Vector3(w + 0.74f, 0.18f, 0.08f), "Trim");
                foreach (float end in new[] { -1f, 1f })
                {
                    float x = end * (w * 0.5f + 0.02f);
                    model.Mesh.Box(new Vector3(x, -thickness * 0.5f - 0.06f, pastRidge * 0.5f), new Vector3(0.06f, 0.24f, length - pastRidge), "Trim");
                }
                model.Mesh.ResetTransform();
                colliders.Add((center, size, rotation));
            }

            model.Mesh.Box(ridge + Vector3.up * (thickness + lift + 0.1f), new Vector3(w + 0.75f, 0.14f, 0.42f), spec.Roof);

            if (spec.Chimney)
            {
                float chimneyHeight = height + 1.1f;
                var chimney = new Vector3(w * 0.3f, wallTop + chimneyHeight * 0.5f, spec.Depth * 0.7f);
                model.Part(chimney, new Vector3(0.75f, chimneyHeight, 0.75f), "BrickDark", true);
                model.Mesh.Box(chimney + Vector3.up * (chimneyHeight * 0.5f + 0.05f), new Vector3(0.9f, 0.1f, 0.9f), "Concrete");
            }
        }

        private static void BuildFlatRoof(PropModel model, HouseSpec spec, float wallTop)
        {
            float w = spec.Width;
            float d = spec.Depth;
            model.Part(new Vector3(0f, wallTop + 0.1f, d * 0.5f), new Vector3(w + 0.3f, 0.2f, d + 0.3f), spec.Roof, true);
            foreach (float side in new[] { -1f, 1f })
            {
                model.Part(new Vector3(side * (w * 0.5f + 0.1f), wallTop + 0.4f, d * 0.5f), new Vector3(0.2f, 0.4f, d + 0.3f), "Trim");
                // Slightly shorter than the side parapets, so their end faces do not share a plane.
                model.Part(new Vector3(0f, wallTop + 0.4f, d * 0.5f + side * (d * 0.5f + 0.1f)), new Vector3(w + 0.36f, 0.4f, 0.2f), "Trim");
            }

            if (!string.IsNullOrEmpty(spec.Sign))
                model.Part(new Vector3(0f, wallTop + 0.15f, -0.15f), new Vector3(w + 0.44f, 1.0f, 0.25f), "GasRed");
        }

        private static void BuildPorch(PropModel model, HouseSpec spec)
        {
            float hw = spec.Width * 0.5f;
            switch (spec.Porch)
            {
                case PorchKind.Full:
                {
                    model.Part(new Vector3(0f, FloorTop * 0.5f, -1.2f), new Vector3(spec.Width, FloorTop, 2.4f), "WoodLight", true);
                    float[] columns = { -hw + 0.25f, -1.4f, 1.4f, hw - 0.25f };
                    foreach (float x in columns)
                        model.Part(new Vector3(x, FloorTop + 1.3f, -2.15f), new Vector3(0.22f, 2.6f, 0.22f), "Trim", true);
                    model.Part(new Vector3(0f, 2.82f, -1.25f), new Vector3(spec.Width + 0.3f, 0.14f, 2.75f), spec.Roof, true);
                    model.Mesh.Box(new Vector3(0f, 2.74f, -2.64f), new Vector3(spec.Width + 0.34f, 0.22f, 0.06f), "Trim");
                    Railing(model, columns[0], columns[1], -2.15f);
                    Railing(model, columns[2], columns[3], -2.15f);
                    break;
                }
                case PorchKind.Small:
                {
                    // Used on single-storey houses: the main roof's eave hangs low, so the porch roof sits
                    // below it and stops in front of it instead of running into it.
                    const float roofTop = 2.62f;
                    const float roofFront = -2.4f;
                    const float roofBack = -0.55f;
                    float columnHeight = roofTop - 0.14f - FloorTop;
                    model.Part(new Vector3(0f, FloorTop * 0.5f, -1.1f), new Vector3(4.2f, FloorTop, 2.2f), "WoodLight", true);
                    foreach (float x in new[] { -1.9f, 1.9f })
                        model.Part(new Vector3(x, FloorTop + columnHeight * 0.5f, -2f), new Vector3(0.2f, columnHeight, 0.2f), "Trim", true);
                    model.Part(new Vector3(0f, roofTop - 0.07f, (roofFront + roofBack) * 0.5f), new Vector3(4.6f, 0.14f, roofBack - roofFront), spec.Roof, true);
                    model.Mesh.Box(new Vector3(0f, roofTop - 0.15f, roofFront - 0.02f), new Vector3(4.64f, 0.22f, 0.06f), "Trim");
                    break;
                }
                case PorchKind.Canopy:
                    model.Part(new Vector3(0f, FloorTop * 0.5f, -0.65f), new Vector3(2.4f, FloorTop, 1.3f), "Concrete", true);
                    model.Part(new Vector3(0f, 2.75f, -0.6f), new Vector3(2.4f, 0.12f, 1.2f), spec.Roof, true);
                    foreach (float x in new[] { -1.05f, 1.05f })
                    {
                        model.Mesh.SetTransform(new Vector3(x, 2.45f, -0.3f), Quaternion.Euler(45f, 0f, 0f));
                        model.Mesh.Box(Vector3.zero, new Vector3(0.08f, 0.08f, 0.8f), "Trim");
                        model.Mesh.ResetTransform();
                    }
                    break;
            }
        }

        private static void Railing(PropModel model, float x0, float x1, float z)
        {
            float length = x1 - x0 - 0.22f;
            float center = (x0 + x1) * 0.5f;
            foreach (float y in new[] { 0.35f, 1.0f })
                model.Mesh.Box(new Vector3(center, y, z), new Vector3(length, 0.07f, 0.07f), "Trim");
            for (float x = x0 + 0.3f; x < x1 - 0.2f; x += 0.22f)
                model.Mesh.Box(new Vector3(x, 0.67f, z), new Vector3(0.04f, 0.65f, 0.04f), "Trim");
            model.Collider(new Vector3(center, 0.6f, z), new Vector3(length, 0.9f, 0.1f));
        }

        private static void AddRoom(HouseInfo info, string name, float x0, float x1, float depth, float ceiling)
        {
            var center = new Vector3((x0 + x1) * 0.5f, ceiling * 0.5f, depth * 0.5f);
            var size = new Vector3(x1 - x0, ceiling, depth - Wall * 2f);
            info.Rooms.Add(new KeyValuePair<string, Bounds>(name, new Bounds(center, size)));
        }

        private static void AddRoomLight(Transform house, Bounds room, float ceiling)
        {
            GameObject lamp = Instance(VillageProps.Get("CeilingLamp"), house, new Vector3(room.center.x, ceiling, room.center.z), Quaternion.identity);
            var lightObject = new GameObject("Room Light");
            lightObject.transform.SetParent(lamp.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, -0.85f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = Mathf.Max(room.size.x, room.size.z) + 1.5f;
            light.intensity = 2.2f;
            light.color = new Color(1f, 0.84f, 0.62f);
            light.shadows = LightShadows.None;
        }

        // ───────────── Gas station pieces ─────────────

        public static GameObject BuildCanopy()
        {
            var model = new PropModel();
            const float height = 4.6f;
            foreach (float x in new[] { -6f, 6f })
            {
                foreach (float z in new[] { -3.2f, 3.2f })
                {
                    model.Part(new Vector3(x, height * 0.5f, z), new Vector3(0.45f, height, 0.45f), "GasWhite", true);
                    model.Part(new Vector3(x, 0.4f, z), new Vector3(0.55f, 0.8f, 0.55f), "GasRed");
                }
            }

            model.Part(new Vector3(0f, height + 0.25f, 0f), new Vector3(16f, 0.5f, 10f), "GasWhite", true);
            foreach (float side in new[] { -1f, 1f })
            {
                model.Mesh.Box(new Vector3(0f, height + 0.3f, side * 5.05f), new Vector3(16.2f, 0.65f, 0.12f), "GasRed");
                model.Mesh.Box(new Vector3(side * 8.05f, height + 0.3f, 0f), new Vector3(0.12f, 0.65f, 10.2f), "GasRed");
                model.Mesh.Box(new Vector3(0f, height + 0.62f, side * 5.05f), new Vector3(16.2f, 0.06f, 0.14f), "GasYellow");
            }

            for (int x = -1; x <= 1; x++)
            {
                foreach (float z in new[] { -2f, 2f })
                    model.Mesh.Box(new Vector3(x * 4.5f, height - 0.02f, z), new Vector3(1.4f, 0.04f, 0.7f), "LampCool");
            }

            GameObject root = VillageAssets.CreateMeshObject("GasStation_Canopy", model.Mesh, "GasStation_Canopy");
            VillageAssets.AddColliders(root, model.Colliders);
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
            VillageAssets.Label(root.transform, "FUEL", new Vector3(0f, height + 0.3f, -5.13f), 0.5f, Color.white, Quaternion.identity);
            VillageAssets.Label(root.transform, "FUEL", new Vector3(0f, height + 0.3f, 5.13f), 0.5f, Color.white, Quaternion.Euler(0f, 180f, 0f));

            for (int x = -1; x <= 1; x += 2)
            {
                var lightObject = new GameObject("Canopy Light");
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.localPosition = new Vector3(x * 4f, height - 0.4f, 0f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 9f;
                light.intensity = 2f;
                light.color = new Color(0.9f, 0.95f, 1f);
                light.shadows = LightShadows.None;
            }

            return VillageAssets.SavePrefab(root, "GasStation", "GasStation_Canopy");
        }

        public static GameObject BuildPriceSign()
        {
            var model = new PropModel();
            foreach (float x in new[] { -0.8f, 0.8f })
                model.Part(new Vector3(x, 2.25f, 0f), new Vector3(0.25f, 4.5f, 0.25f), "GasWhite", true);
            model.Part(new Vector3(0f, 5.2f, 0f), new Vector3(2.3f, 1.4f, 0.35f), "GasRed", true);
            model.Part(new Vector3(0f, 3.6f, 0f), new Vector3(2.1f, 1.6f, 0.3f), "Black", true);
            model.Part(new Vector3(0f, 0.2f, 0f), new Vector3(2.4f, 0.4f, 0.6f), "Concrete", true);

            GameObject root = VillageAssets.CreateMeshObject("GasStation_PriceSign", model.Mesh, "GasStation_PriceSign");
            VillageAssets.AddColliders(root, model.Colliders);
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
            foreach (float side in new[] { -1f, 1f })
            {
                Quaternion facing = side < 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                float z = side * 0.19f;
                VillageAssets.Label(root.transform, "GAS", new Vector3(0f, 5.2f, z), 0.8f, Color.white, facing);
                VillageAssets.Label(root.transform, "REG  3.49", new Vector3(0f, 4.0f, side * 0.16f), 0.38f, new Color(1f, 0.8f, 0.2f), facing);
                VillageAssets.Label(root.transform, "PLUS 3.79", new Vector3(0f, 3.5f, side * 0.16f), 0.38f, new Color(1f, 0.8f, 0.2f), facing);
                VillageAssets.Label(root.transform, "DSL  3.99", new Vector3(0f, 3.0f, side * 0.16f), 0.38f, new Color(1f, 0.8f, 0.2f), facing);
            }

            return VillageAssets.SavePrefab(root, "GasStation", "GasStation_PriceSign");
        }

        private static GameObject Instance(GameObject prefab, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            return go;
        }
    }
}
