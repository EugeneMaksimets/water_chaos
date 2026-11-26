using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WaterChaos.EditorTools.MiniVillage;

namespace WaterChaos.EditorTools.OfficeDistrict
{
    internal enum Side
    {
        Front,
        Back,
        Left,
        Right,
    }

    /// <summary>Hole in a wall: <c>U</c> along the wall, <c>Y</c> up, in building space (m).</summary>
    internal struct Opening
    {
        public float U0, U1, Y0, Y1;

        public Opening(float u0, float u1, float y0, float y1)
        {
            U0 = u0;
            U1 = u1;
            Y0 = y0;
            Y1 = y1;
        }

        public float Center => (U0 + U1) * 0.5f;
        public float Middle => (Y0 + Y1) * 0.5f;
    }

    /// <summary>
    /// Brick city facades like the reference: thick walls cut around their openings, a stone plinth,
    /// cornice bands between storeys, a heavy top cornice with a parapet, corner quoins and window lintels.
    /// Building space: x 0..Width, z 0..Depth, front face at z = 0 facing -Z, ground at y = 0.
    /// </summary>
    internal sealed class Facade
    {
        public const float Skin = 0.12f;

        public readonly float Width;
        public readonly float Depth;
        public readonly float Thickness;

        public Facade(float width, float depth, float thickness)
        {
            Width = width;
            Depth = depth;
            Thickness = thickness;
        }

        /// <summary>Point on a wall: <paramref name="u"/> along it, <paramref name="depth"/> inwards from its outer face.</summary>
        public Vector3 Place(Side side, float u, float y, float depth)
        {
            switch (side)
            {
                case Side.Front: return new Vector3(u, y, depth);
                case Side.Back: return new Vector3(u, y, Depth - depth);
                case Side.Left: return new Vector3(depth, y, u);
                default: return new Vector3(Width - depth, y, u);
            }
        }

        public Vector3 Size(Side side, float length, float height, float thickness)
        {
            return side == Side.Front || side == Side.Back
                ? new Vector3(length, height, thickness)
                : new Vector3(thickness, height, length);
        }

        /// <summary>Rotation of a window or door prefab (glass faces -Z) set into the wall.</summary>
        public static Quaternion Facing(Side side)
        {
            switch (side)
            {
                case Side.Front: return Quaternion.identity;
                case Side.Back: return Quaternion.Euler(0f, 180f, 0f);
                case Side.Left: return Quaternion.Euler(0f, 90f, 0f);
                default: return Quaternion.Euler(0f, -90f, 0f);
            }
        }

        /// <summary>
        /// Builds one outer wall from the ground to <paramref name="height"/>: cut into vertical slices at every
        /// opening edge, each slice filled above and below its openings with brick outside, plaster inside,
        /// a stone plinth up to <paramref name="plinth"/>, and one collider per piece.
        /// </summary>
        public void Wall(PropModel model, Side side, float height, List<Opening> openings,
            string brick, string interior, float plinth = 0.6f)
        {
            bool alongX = side == Side.Front || side == Side.Back;
            float uMin = alongX ? 0f : Thickness;
            float uMax = alongX ? Width : Depth - Thickness;

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
                        Piece(model, side, a, b, y, gap.Y0, brick, interior, plinth);
                    y = Mathf.Max(y, gap.Y1);
                }

                if (y < height)
                    Piece(model, side, a, b, y, height, brick, interior, plinth);
            }
        }

        private void Piece(PropModel model, Side side, float a, float b, float y0, float y1, string brick, string interior, float plinth)
        {
            float u = (a + b) * 0.5f;
            float length = b - a;

            if (y0 < plinth)
            {
                float top = Mathf.Min(y1, plinth);
                model.Mesh.Box(Place(side, u, (y0 + top) * 0.5f, Skin * 0.5f - 0.03f), Size(side, length, top - y0, Skin + 0.06f), "Foundation");
            }
            if (y1 > plinth)
            {
                float bottom = Mathf.Max(y0, plinth);
                model.Mesh.Box(Place(side, u, (bottom + y1) * 0.5f, Skin * 0.5f), Size(side, length, y1 - bottom, Skin), brick);
            }

            model.Mesh.Box(Place(side, u, (y0 + y1) * 0.5f, Skin + (Thickness - Skin) * 0.5f), Size(side, length, y1 - y0, Thickness - Skin), interior);
            model.Collider(Place(side, u, (y0 + y1) * 0.5f, Thickness * 0.5f), Size(side, length, y1 - y0, Thickness));
        }

        /// <summary>Stucco band around the whole building at <paramref name="y"/> (between storeys, or the top cornice).</summary>
        public void Band(PropModel model, float y, float height, float protrude, string material = "Stucco")
        {
            float w = Width + protrude * 2f;
            float d = Depth + protrude * 2f;
            model.Mesh.Box(new Vector3(Width * 0.5f, y, -protrude * 0.5f + 0.001f), new Vector3(w, height, protrude), material);
            model.Mesh.Box(new Vector3(Width * 0.5f, y, Depth + protrude * 0.5f - 0.001f), new Vector3(w, height, protrude), material);
            model.Mesh.Box(new Vector3(-protrude * 0.5f + 0.001f, y, Depth * 0.5f), new Vector3(protrude, height, Depth), material);
            model.Mesh.Box(new Vector3(Width + protrude * 0.5f - 0.001f, y, Depth * 0.5f), new Vector3(protrude, height, Depth), material);
        }

        /// <summary>Heavy top cornice (two stepped bands) and a parapet cap.</summary>
        public void Cornice(PropModel model, float y, float parapetTop)
        {
            Band(model, y, 0.22f, 0.12f);
            Band(model, y + 0.2f, 0.18f, 0.26f);
            Band(model, parapetTop + 0.04f, 0.08f, 0.06f);
        }

        /// <summary>Alternating long and short stone blocks up every corner (from the plinth to <paramref name="top"/>).</summary>
        public void Quoins(PropModel model, float bottom, float top, string material = "Stucco")
        {
            const float course = 0.42f;
            const float protrude = 0.04f;
            int index = 0;
            foreach (Vector2 corner in new[] { new Vector2(0f, 0f), new Vector2(Width, 0f), new Vector2(0f, Depth), new Vector2(Width, Depth) })
            {
                float sx = corner.x > 0f ? -1f : 1f;
                float sz = corner.y > 0f ? -1f : 1f;
                for (float y = bottom; y + course * 0.9f < top; y += course)
                {
                    bool longFront = (index++ & 1) == 0;
                    float alongX = longFront ? 0.55f : 0.32f;
                    float alongZ = longFront ? 0.32f : 0.55f;
                    float cy = y + course * 0.45f;
                    float frontZ = corner.y > 0f ? Depth + protrude * 0.5f : -protrude * 0.5f;
                    float sideX = corner.x > 0f ? Width + protrude * 0.5f : -protrude * 0.5f;
                    model.Mesh.Box(new Vector3(corner.x + sx * alongX * 0.5f - sx * protrude, cy, frontZ), new Vector3(alongX, course * 0.86f, protrude), material);
                    model.Mesh.Box(new Vector3(sideX, cy, corner.y + sz * alongZ * 0.5f - sz * protrude), new Vector3(protrude, course * 0.86f, alongZ), material);
                }
            }
        }

        /// <summary>Stone lintel over an opening, standing out of the brick.</summary>
        public void Lintel(PropModel model, Side side, Opening opening, string material = "Stucco")
        {
            float length = opening.U1 - opening.U0 + 0.34f;
            model.Mesh.Box(Place(side, opening.Center, opening.Y1 + 0.2f, -0.03f), Size(side, length, 0.2f, 0.08f), material);
            model.Mesh.Box(Place(side, opening.Center, opening.Y1 + 0.32f, -0.05f), Size(side, length * 0.45f, 0.06f, 0.1f), material);
        }
    }
}
