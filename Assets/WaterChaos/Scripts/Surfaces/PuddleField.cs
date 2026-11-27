using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Environment;
using WaterChaos.Water;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// All puddles of a scene. Liquid landing near an existing puddle (within its radius plus the merge distance,
    /// on the same floor) joins it instead of creating another one on top; only liquid landing farther away starts
    /// a new puddle. Lookups use a spatial hash, never a scan of every puddle. At the puddle limit the oldest puddle
    /// dries up to make room, so liquid leaves a puddle wherever it lands.
    /// </summary>
    public sealed class PuddleField
    {
        /// <summary>Puddles on floors this far apart vertically are separate, in meters (m).</summary>
        public const float FloorTolerance = 0.5f;

        private readonly PuddleConfig _config;
        private readonly int _maxPuddles;
        private readonly SurfacePatchRegistry<PuddleModel> _registry;
        private readonly List<PuddleModel> _dried = new List<PuddleModel>();

        /// <param name="maxPuddles">Most puddles at once; past it the oldest one is removed.</param>
        public PuddleField(PuddleConfig config, int maxPuddles)
        {
            _config = config;
            _maxPuddles = Math.Max(1, maxPuddles);
            float reach = config.MaxRadius + config.MergeDistance;
            _registry = new SurfacePatchRegistry<PuddleModel>(reach, reach);
        }

        public IReadOnlyList<PuddleModel> Puddles => _registry.All;
        public int Count => _registry.Count;

        /// <summary>Raised when a new puddle appears.</summary>
        public event Action<PuddleModel> Created;

        /// <summary>Raised when a puddle is removed: it dried up, or it was the oldest and made room for a new one.</summary>
        public event Action<PuddleModel> Removed;

        /// <summary>The puddle liquid landing at <paramref name="point"/> would join, if any.</summary>
        public PuddleModel FindNearby(Vector3 point)
        {
            return _registry.FindNearest(point, _config.MergeDistance, FloorTolerance);
        }

        /// <summary>
        /// Liquid landed on a floor at <paramref name="point"/>: joins the puddle there or starts a new one.
        /// Returns that puddle, or null when no liquid was given or a new puddle has no room.
        /// </summary>
        /// <param name="space">
        /// Free floor around the point for a new puddle, in meters (m) (to the edge of the surface or a wall);
        /// less than the smallest puddle leaves none.
        /// </param>
        public PuddleModel AddLiquid(Vector3 point, LiquidType liquid, float liters, float space = float.PositiveInfinity)
        {
            if (!(liters > 0f))
                return null;

            PuddleModel puddle = FindNearby(point);
            if (puddle == null)
            {
                if (!(space >= _config.MinRadius))
                    return null;

                while (_registry.Count >= _maxPuddles)
                    RemoveOldest();

                puddle = new PuddleModel(point, liquid, _config, space);
                _registry.Add(puddle);
                puddle.AddLiquid(liquid, liters);
                Created?.Invoke(puddle);
                return puddle;
            }

            puddle.AddLiquid(liquid, liters);
            return puddle;
        }

        /// <summary>
        /// Freezes and melts every puddle by the temperature where it lies (no zone = no freezing) and removes the ones
        /// that dried up (no new liquid for their lifetime).
        /// </summary>
        public void Tick(float deltaTime, ITemperatureProvider temperatures)
        {
            IReadOnlyList<PuddleModel> all = _registry.All;
            _dried.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                PuddleModel puddle = all[i];
                float temperature = 0f;
                bool hasZone = temperatures != null && temperatures.TryGetTemperature(puddle.Center, out temperature);
                puddle.UpdateTemperature(hasZone, temperature, deltaTime);
                puddle.Advance(deltaTime);
                if (puddle.IsDriedUp)
                    _dried.Add(puddle);
            }

            foreach (PuddleModel puddle in _dried)
            {
                _registry.Remove(puddle);
                Removed?.Invoke(puddle);
            }
        }

        /// <summary>The puddle under a point (e.g. the player's feet), if any.</summary>
        public bool TryGetAt(Vector3 point, out PuddleModel puddle)
        {
            puddle = _registry.FindContaining(point, FloorTolerance);
            return puddle != null;
        }

        /// <summary>Ice under the point and its grip (0..1).</summary>
        public bool TryGetIceAt(Vector3 point, out float friction)
        {
            PuddleModel ice = _registry.FindContaining(point, FloorTolerance, p => p.IsFrozen);
            friction = ice != null ? ice.Friction : 1f;
            return ice != null;
        }

        /// <summary>True if the point stands in a liquid puddle of a liquid <paramref name="match"/> accepts.</summary>
        public bool IsInLiquid(Vector3 point, Predicate<LiquidType> match)
        {
            return _registry.FindContaining(point, FloorTolerance, p => !p.IsFrozen && match(p.Liquid)) != null;
        }

        private void RemoveOldest()
        {
            // The registry keeps puddles in the order they appeared.
            PuddleModel oldest = _registry.All[0];
            _registry.Remove(oldest);
            Removed?.Invoke(oldest);
        }

        public void Clear()
        {
            _registry.Clear();
        }
    }
}
