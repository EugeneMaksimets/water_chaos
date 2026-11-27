using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// Spatial hash of surface patches on a horizontal grid (x, z), so a lookup checks only the patches in the
    /// cells around a point instead of every patch in the scene. Patches never move, so each lives in the cell of
    /// its center; a patch reaches at most <c>maxReach</c> meters from it, which sets how many cells are searched.
    /// Patches on different floors are told apart by the vertical tolerance.
    /// </summary>
    public sealed class SurfacePatchRegistry<T> where T : class, ISurfacePatch
    {
        private readonly Dictionary<long, List<T>> _cells = new Dictionary<long, List<T>>();
        private readonly List<T> _all = new List<T>();
        private readonly float _cellSize;
        private readonly int _cellReach;

        /// <param name="cellSize">Grid cell size, in meters (m).</param>
        /// <param name="maxReach">Farthest a lookup may match from a patch center (largest radius plus margin), in meters (m).</param>
        public SurfacePatchRegistry(float cellSize, float maxReach)
        {
            if (!(cellSize > 0f))
                throw new ArgumentException("Cell size must be positive.", nameof(cellSize));
            if (float.IsNaN(maxReach) || maxReach < 0f)
                throw new ArgumentException("Reach must not be negative.", nameof(maxReach));

            _cellSize = cellSize;
            _cellReach = Mathf.Max(1, Mathf.CeilToInt(maxReach / cellSize));
        }

        public int Count => _all.Count;
        /// <summary>Every patch, oldest first.</summary>
        public IReadOnlyList<T> All => _all;

        public void Add(T patch)
        {
            long key = KeyOf(patch.Center);
            if (!_cells.TryGetValue(key, out List<T> cell))
            {
                cell = new List<T>();
                _cells.Add(key, cell);
            }

            cell.Add(patch);
            _all.Add(patch);
        }

        public bool Remove(T patch)
        {
            if (!_all.Remove(patch))
                return false;

            long key = KeyOf(patch.Center);
            if (_cells.TryGetValue(key, out List<T> cell))
            {
                cell.Remove(patch);
                if (cell.Count == 0)
                    _cells.Remove(key);
            }

            return true;
        }

        public void Clear()
        {
            _cells.Clear();
            _all.Clear();
        }

        /// <summary>
        /// The patch whose edge is nearest to the point, if the point lies within its radius plus
        /// <paramref name="margin"/> (horizontally) and within <paramref name="verticalTolerance"/> of its height.
        /// </summary>
        /// <param name="filter">Optional: only patches it accepts are considered.</param>
        public T FindNearest(Vector3 point, float margin, float verticalTolerance, Predicate<T> filter = null)
        {
            T best = null;
            float bestGap = float.MaxValue;
            int cx = CellOf(point.x);
            int cz = CellOf(point.z);

            for (int x = cx - _cellReach; x <= cx + _cellReach; x++)
            {
                for (int z = cz - _cellReach; z <= cz + _cellReach; z++)
                {
                    if (!_cells.TryGetValue(Key(x, z), out List<T> cell))
                        continue;

                    for (int i = 0; i < cell.Count; i++)
                    {
                        T patch = cell[i];
                        if (Mathf.Abs(point.y - patch.Center.y) > verticalTolerance)
                            continue;
                        if (filter != null && !filter(patch))
                            continue;

                        float gap = HorizontalDistance(point, patch.Center) - patch.Radius;
                        if (gap <= margin && gap < bestGap)
                        {
                            bestGap = gap;
                            best = patch;
                        }
                    }
                }
            }

            return best;
        }

        /// <summary>A patch that covers the point (inside its radius, within the vertical tolerance).</summary>
        public T FindContaining(Vector3 point, float verticalTolerance, Predicate<T> filter = null)
        {
            return FindNearest(point, 0f, verticalTolerance, filter);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private int CellOf(float coordinate) => Mathf.FloorToInt(coordinate / _cellSize);

        private long KeyOf(Vector3 point) => Key(CellOf(point.x), CellOf(point.z));

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
    }
}
