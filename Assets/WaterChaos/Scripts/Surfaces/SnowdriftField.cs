using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Environment;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// All snowdrifts of a scene. Snow landing near a drift (within its radius plus the merge distance) builds it up
    /// instead of starting another; melted drifts are removed, and at the drift limit the oldest one goes to make room.
    /// Spatial hash lookups, never a scan of the scene.
    /// </summary>
    public sealed class SnowdriftField
    {
        /// <summary>A fire this far above a drift (it starts a little above the floor) is still covered, in meters (m).</summary>
        public const float IgnitionHeightTolerance = 1f;

        private readonly SnowdriftConfig _config;
        private readonly int _maxDrifts;
        private readonly SurfacePatchRegistry<SnowdriftModel> _registry;
        private readonly List<SnowdriftModel> _melted = new List<SnowdriftModel>();

        public SnowdriftField(SnowdriftConfig config, int maxDrifts)
        {
            _config = config;
            _maxDrifts = Math.Max(1, maxDrifts);
            float reach = config.MaxRadius + config.MergeDistance;
            _registry = new SurfacePatchRegistry<SnowdriftModel>(reach, reach);
        }

        public IReadOnlyList<SnowdriftModel> Drifts => _registry.All;
        public int Count => _registry.Count;

        public event Action<SnowdriftModel> Created;
        /// <summary>Raised when a drift melted away or was removed to make room, and is gone.</summary>
        public event Action<SnowdriftModel> Removed;

        /// <summary>Snow landed at the point: builds up the drift there or starts a new one (null if nothing added).</summary>
        public SnowdriftModel AddSnow(Vector3 point, float liters)
        {
            if (!(liters > 0f))
                return null;

            SnowdriftModel drift = _registry.FindNearest(point, _config.MergeDistance, PuddleField.FloorTolerance);
            if (drift == null)
            {
                while (_registry.Count >= _maxDrifts)
                {
                    SnowdriftModel oldest = _registry.All[0];
                    _registry.Remove(oldest);
                    Removed?.Invoke(oldest);
                }

                drift = new SnowdriftModel(point, _config);
                _registry.Add(drift);
                drift.AddSnow(liters);
                Created?.Invoke(drift);
                return drift;
            }

            drift.AddSnow(liters);
            return drift;
        }

        /// <summary>Melts drifts that lie where it is warm (or outside every zone) and removes melted ones.</summary>
        public void Tick(float deltaTime, ITemperatureProvider temperatures)
        {
            IReadOnlyList<SnowdriftModel> all = _registry.All;
            _melted.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                SnowdriftModel drift = all[i];
                float temperature = 0f;
                bool hasZone = temperatures != null && temperatures.TryGetTemperature(drift.Center, out temperature);
                drift.UpdateTemperature(hasZone, temperature, deltaTime);
                if (drift.IsMelted)
                    _melted.Add(drift);
            }

            foreach (SnowdriftModel drift in _melted)
            {
                _registry.Remove(drift);
                Removed?.Invoke(drift);
            }
        }

        /// <summary>True if a drift deep enough covers the point (a fire position just above the floor).</summary>
        public bool BlocksIgnitionAt(Vector3 firePosition)
        {
            // The fire sits above the floor: compare against a point at the drift's height range.
            SnowdriftModel drift = _registry.FindContaining(firePosition, IgnitionHeightTolerance, d => d.BlocksIgnition);
            return drift != null;
        }

        public void Clear()
        {
            _registry.Clear();
        }
    }
}
