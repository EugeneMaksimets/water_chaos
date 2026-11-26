using System;

namespace WaterChaos.Water
{
    /// <summary>
    /// Pure model of a liquid tank: finite volume of one liquid (<see cref="Liquid"/>) that can be drawn until empty.
    /// Added liquid mixes in (<see cref="LiquidTypeExtensions.MixWith"/>): gasoline or toxic liquid spoils water,
    /// and gasoline meeting toxic liquid turns the whole tank into water.
    /// The tank also owns whether its water is soapy (<see cref="IsSoapy"/>): every pump, hose and nozzle downstream
    /// only passes that on.
    /// </summary>
    public sealed class WaterReservoir
    {
        /// <summary>Maximum volume in liters (L).</summary>
        public float Capacity { get; }

        /// <summary>Current volume in liters (L); never negative.</summary>
        public float Volume { get; private set; }

        /// <summary>Liquid in the tank; an empty tank keeps its last liquid until another one is added.</summary>
        public LiquidType Liquid { get; private set; }

        /// <summary>
        /// Soap is dissolved in the water (<see cref="AddSoap"/>). Only water can be soapy: anything that turns the tank
        /// into another liquid washes the soap out, and the tank stays plain even if it turns back into water.
        /// </summary>
        public bool IsSoapy { get; private set; }

        public bool IsEmpty => Volume <= 0f;

        /// <summary>No more water fits in.</summary>
        public bool IsFull => Volume >= Capacity;

        /// <summary>Volume that still fits in, in liters (L); never negative.</summary>
        public float FreeCapacity => Math.Max(0f, Capacity - Volume);

        /// <summary>Fill level (0..1) below which outflow weakens; 0 = never weakens.</summary>
        public float LowLevelRatio { get; }
        /// <summary>
        /// Share of a request the tank can give right now: 1 above the low level,
        /// then sqrt(fill / lowLevel) down to 0 (like a draining tank, empties in finite time).
        /// </summary>
        public float OutflowRatio =>
            FillRatio >= LowLevelRatio ? 1f : (float)Math.Sqrt(FillRatio / LowLevelRatio);
        
        /// <summary>Fill level from 0 (empty) to 1 (full); 0 when capacity is 0.</summary>
        public float FillRatio => Capacity > 0f ? Volume / Capacity : 0f;

        /// <param name="capacity">Maximum volume in liters (L); negative and NaN become 0.</param>
        /// <param name="initialVolume">Starting volume in liters (L); clamped to 0..capacity.</param>
        /// <param name="liquid">Liquid the tank holds.</param>
        public WaterReservoir(float capacity, float initialVolume, float lowLevelRatio = 0f, LiquidType liquid = LiquidType.Water)
        {
            Liquid = liquid;
            Capacity = Sanitize(capacity);
            Volume = Math.Min(Sanitize(initialVolume), Capacity);
            LowLevelRatio = Math.Min(Sanitize(lowLevelRatio), 1f);
        }

        /// <summary>
        /// Takes up to the requested volume from the reservoir.
        /// </summary>
        /// <param name="requested">Wanted volume in liters (L); negative and NaN are treated as 0.</param>
        /// <returns>Volume actually drawn in liters (L), never more than was stored.</returns>
        public float Draw(float requested)
        {
            float drawn = Math.Min(Sanitize(requested) * OutflowRatio, Volume);
            Volume -= drawn;
            return drawn;
        }

        /// <summary>
        /// Dissolves a bar of soap in the tank. Works only on water that is there (<see cref="LiquidTypeExtensions.CanBeSoapy"/>):
        /// an empty tank, gasoline or toxic liquid stay as they are.
        /// </summary>
        /// <returns>True if the water is soapy now.</returns>
        public bool AddSoap()
        {
            if (IsEmpty || !Liquid.CanBeSoapy())
                return false;

            IsSoapy = true;
            return true;
        }

        /// <summary>
        /// Puts up to the given volume of <paramref name="liquid"/> into the reservoir (e.g. pumped in from another one).
        /// An empty tank takes the new liquid; otherwise the two mix (<see cref="LiquidTypeExtensions.MixWith"/>).
        /// Soap survives only while the result is water: soapy water added to water makes it soapy,
        /// any other liquid that takes over washes the soap out.
        /// </summary>
        /// <param name="amount">Offered volume in liters (L); negative and NaN are treated as 0.</param>
        /// <param name="isSoapy">The added liquid is soapy water.</param>
        /// <returns>Volume actually accepted in liters (L), never more than <see cref="FreeCapacity"/>.</returns>
        public float Add(float amount, LiquidType liquid, bool isSoapy = false)
        {
            bool wasEmpty = IsEmpty;
            LiquidType mixed = wasEmpty ? liquid : Liquid.MixWith(liquid);
            bool keepsSoap = !wasEmpty && IsSoapy && Liquid == mixed;
            bool addsSoap = isSoapy && liquid.CanBeSoapy();

            float accepted = Add(amount);
            if (accepted > 0f)
            {
                Liquid = mixed;
                IsSoapy = mixed.CanBeSoapy() && (keepsSoap || addsSoap);
            }

            return accepted;
        }

        /// <summary>
        /// Puts up to the given volume of the stored liquid into the reservoir (e.g. what the destination did not take).
        /// </summary>
        /// <param name="amount">Offered volume in liters (L); negative and NaN are treated as 0.</param>
        /// <returns>Volume actually accepted in liters (L), never more than <see cref="FreeCapacity"/>.</returns>
        public float Add(float amount)
        {
            float accepted = Math.Min(Sanitize(amount), FreeCapacity);
            Volume = Math.Min(Volume + accepted, Capacity);
            return accepted;
        }

        /// <summary>Removes all liquid at once (e.g. a story event drains the tank); the liquid type is kept, soap is gone.</summary>
        public void Empty()
        {
            Volume = 0f;
            IsSoapy = false;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}