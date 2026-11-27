using System;
using UnityEngine;
using WaterChaos.Environment;
using WaterChaos.Water;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// One puddle: which liquid lies there, how much (L), how wide it is, and whether water froze into ice.
    /// Grows with the liquid poured into it up to the maximum size; a different liquid mixes in by the liquid rules
    /// (<see cref="LiquidTypeExtensions.MixWith"/>). Only a freezing liquid (water) ever turns to ice, and only in a
    /// real temperature zone: without a zone (outdoors) it stays liquid and ice melts.
    /// </summary>
    public sealed class PuddleModel : ISurfacePatch
    {
        private readonly PuddleConfig _config;
        // Seconds (s) the puddle has been cold (liquid) or warm (frozen) enough to change state.
        private float _stateTimer;

        public PuddleModel(Vector3 center, LiquidType liquid, PuddleConfig config)
        {
            Center = center;
            Liquid = liquid;
            _config = config;
            Radius = config.MinRadius;
        }

        public Vector3 Center { get; }
        public float Radius { get; private set; }
        public LiquidType Liquid { get; private set; }
        /// <summary>Liquid in the puddle, in liters (L); never above <see cref="PuddleConfig.MaxVolume"/>.</summary>
        public float Volume { get; private set; }
        /// <summary>Water in the puddle froze: the surface is slippery ice.</summary>
        public bool IsFrozen { get; private set; }
        /// <summary>Grip on this puddle compared to normal ground (0..1): low on ice, normal otherwise.</summary>
        public float Friction => IsFrozen ? _config.IceFriction : 1f;
        public bool IsFull => Volume >= _config.MaxVolume;

        /// <summary>Raised when size, liquid or ice state changes.</summary>
        public event Action<PuddleModel> Changed;

        /// <summary>
        /// Pours liquid in. Returns the liters taken; a full puddle takes nothing more (it never grows past its maximum).
        /// </summary>
        public float AddLiquid(LiquidType liquid, float liters)
        {
            if (!(liters > 0f))
                return 0f;

            bool changed = false;
            LiquidType mixed = Liquid.MixWith(liquid);
            if (mixed != Liquid)
            {
                Liquid = mixed;
                changed = true;
                // Gasoline or toxic liquid never stays frozen.
                if (IsFrozen && !Liquid.CanFreeze())
                    SetFrozen(false);
            }

            float taken = Math.Min(liters, _config.MaxVolume - Volume);
            if (taken > 0f)
            {
                Volume += taken;
                float radius = Mathf.Clamp(Mathf.Sqrt(Volume * _config.AreaPerLiter / Mathf.PI), _config.MinRadius, _config.MaxRadius);
                if (!Mathf.Approximately(radius, Radius))
                {
                    Radius = radius;
                    changed = true;
                }
            }

            if (changed)
                Changed?.Invoke(this);

            return Math.Max(0f, taken);
        }

        /// <summary>
        /// Freezes or melts the puddle by the temperature where it lies.
        /// </summary>
        /// <param name="hasZone">A temperature zone covers the puddle; false outdoors.</param>
        /// <param name="temperature">Temperature of that zone (°C); ignored without a zone.</param>
        /// <param name="deltaTime">Elapsed time in seconds (s).</param>
        public void UpdateTemperature(bool hasZone, float temperature, float deltaTime)
        {
            if (!(deltaTime > 0f))
                return;

            bool wantsChange = IsFrozen
                ? !Liquid.CanFreeze() || !hasZone || temperature > _config.MeltAbove
                : Liquid.CanFreeze() && FreezingRules.IsFreezing(hasZone, temperature, _config.FreezeBelow);

            if (!wantsChange)
            {
                _stateTimer = 0f;
                return;
            }

            _stateTimer += deltaTime;
            float delay = IsFrozen ? _config.MeltDelay : _config.FreezeDelay;
            if (_stateTimer >= delay)
                SetFrozen(!IsFrozen);
        }

        private void SetFrozen(bool frozen)
        {
            _stateTimer = 0f;
            if (IsFrozen == frozen)
                return;

            IsFrozen = frozen;
            Changed?.Invoke(this);
        }
    }
}
