using System;
using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// What a target receives from the water flow during one physics step.
    /// </summary>
    public readonly struct WaterImpact
    {
        /// <summary>
        /// Contact point in world
        /// </summary>
        public Vector3 ContactPoint { get; }
        /// <summary>
        /// Surface normal at the conttact point
        /// </summary>
        public Vector3 Normal { get; }
        /// <summary>
        /// Direction the water go at contact
        /// </summary>
        public Vector3 Direction { get; }
        /// <summary>
        /// Water delivered this step in liters
        /// </summary>
        public float Volume { get; }
        /// <summary>
        /// Impact force newtons (N)
        /// </summary>
        public float Force { get; }
        /// <summary>
        /// Liquid that hit; receivers decide how they react to it
        /// </summary>
        public LiquidType Liquid { get; }
        /// <summary>
        /// Soapy water hit (see <see cref="WaterFlowData.IsSoapy"/>); always false for other liquids
        /// </summary>
        public bool IsSoapy { get; }

        public WaterImpact(Vector3 point, 
            Vector3 normal, 
            Vector3 direction,
            float volume,
            float force,
            LiquidType liquid = LiquidType.Water,
            bool isSoapy = false)
        {
            Liquid = liquid;
            IsSoapy = isSoapy && liquid.CanBeSoapy();
            ContactPoint = point;
            Normal = normal;
            Direction = direction;
            Volume = Sanitize(volume);
            Force = Sanitize(force);
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}