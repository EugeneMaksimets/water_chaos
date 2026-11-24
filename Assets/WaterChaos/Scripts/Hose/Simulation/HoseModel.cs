using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Pure hose path and length: anchor (source connector) → wrap points → end (player nozzle).
    /// Positions are in world space; lengths are in meters (m).
    /// </summary>
    public sealed class HoseModel
    {
        /// <summary>Smallest allowed maximum length, in meters (m).</summary>
        public const float MinMaxLength = 0.1f;

        /// <summary>A length within this of the maximum counts as fully extended, in meters (m).</summary>
        public const float ExtendedTolerance = 0.01f;

        // Always at least two points: [0] = anchor, [Count - 1] = end; wrap points in between.
        private readonly List<Vector3> _points = new List<Vector3>();

        // One per wrap point: the side the hose bends to there, used to tell when it swings off.
        private readonly List<Vector3> _bendNormals = new List<Vector3>();

        // Anchor → last wrap point; only the last segment changes when the end moves.
        private float _fixedLength;

        /// <param name="maxLength">Physical hose length, in meters (m); invalid values become <see cref="MinMaxLength"/>.</param>
        public HoseModel(float maxLength)
        {
            MaxLength = IsFinite(maxLength) ? Mathf.Max(MinMaxLength, maxLength) : MinMaxLength;
            _points.Add(Vector3.zero);
            _points.Add(Vector3.zero);
        }

        /// <summary>Physical hose length, in meters (m).</summary>
        public float MaxLength { get; }

        /// <summary>Length along the whole path, in meters (m).</summary>
        public float Length { get; private set; }

        /// <summary>Hose still free to pull out, in meters (m); never negative.</summary>
        public float RemainingLength => Mathf.Max(0f, MaxLength - Length);

        public bool IsFullyExtended => Length >= MaxLength - ExtendedTolerance;

        /// <summary>Hose left for the last segment (last fixed point → end), in meters (m).</summary>
        public float FreeSegmentLength => Mathf.Max(0f, MaxLength - _fixedLength);

        public int PointCount => _points.Count;
        public int WrapCount => _points.Count - 2;

        public Vector3 Anchor => _points[0];
        public Vector3 End => _points[_points.Count - 1];

        /// <summary>Point the free last segment starts from: the last wrap point, or the anchor.</summary>
        public Vector3 LastFixedPoint => _points[_points.Count - 2];

        /// <summary>Bend side of the last wrap point; zero when there are no wrap points.</summary>
        public Vector3 LastBendNormal => _bendNormals.Count > 0 ? _bendNormals[_bendNormals.Count - 1] : Vector3.zero;

        public Vector3 GetPoint(int index) => _points[index];

        /// <summary>Straight hose from anchor to end, all wrap points removed. Returns false and changes nothing on invalid input.</summary>
        public bool Reset(Vector3 anchor, Vector3 end)
        {
            if (!IsFinite(anchor) || !IsFinite(end))
                return false;

            _points.Clear();
            _bendNormals.Clear();
            _points.Add(anchor);
            _points.Add(end);
            Recalculate();
            return true;
        }

        public bool SetAnchor(Vector3 anchor)
        {
            if (!IsFinite(anchor))
                return false;

            _points[0] = anchor;
            Recalculate();
            return true;
        }

        public bool SetEnd(Vector3 end)
        {
            if (!IsFinite(end))
                return false;

            _points[_points.Count - 1] = end;
            Length = _fixedLength + Vector3.Distance(LastFixedPoint, end);
            return true;
        }

        /// <summary>Adds a wrap point just before the end.</summary>
        /// <param name="bendNormal">Side the hose bends to at this point (any length, not zero).</param>
        public bool AddWrap(Vector3 point, Vector3 bendNormal)
        {
            if (!IsFinite(point) || !IsFinite(bendNormal) || bendNormal == Vector3.zero)
                return false;

            _points.Insert(_points.Count - 1, point);
            _bendNormals.Add(bendNormal.normalized);
            Recalculate();
            return true;
        }

        public bool RemoveLastWrap()
        {
            if (WrapCount == 0)
                return false;

            _points.RemoveAt(_points.Count - 2);
            _bendNormals.RemoveAt(_bendNormals.Count - 1);
            Recalculate();
            return true;
        }

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private void Recalculate()
        {
            _fixedLength = 0f;
            for (int i = 0; i < _points.Count - 2; i++)
                _fixedLength += Vector3.Distance(_points[i], _points[i + 1]);

            Length = _fixedLength + Vector3.Distance(LastFixedPoint, End);
        }
    }
}
