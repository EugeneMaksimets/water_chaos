using System;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Pure gameplay model of a glass pane: strong enough water adds damage (impulse, N·s)
    /// until the pane cracks and then breaks. Weak water never harms it, however long it hits.
    /// </summary>
    public sealed class GlassPaneModel
    {
        private readonly float _minImpactForce;
        private readonly float _crackImpulse;
        private readonly float _breakImpulse;

        public GlassPaneState State { get; private set; }

        /// <summary>Damage taken so far, as impulse in newton-seconds (N·s).</summary>
        public float Damage { get; private set; }

        /// <param name="minImpactForce">Weakest water force that harms the glass, in newtons (N).</param>
        /// <param name="crackImpulse">Damage (N·s) at which the glass cracks.</param>
        /// <param name="breakImpulse">Damage (N·s) at which the glass breaks; never below the crack damage.</param>
        public GlassPaneModel(float minImpactForce, float crackImpulse, float breakImpulse)
        {
            _minImpactForce = Sanitize(minImpactForce);
            _crackImpulse = Sanitize(crackImpulse);
            _breakImpulse = Math.Max(Sanitize(breakImpulse), _crackImpulse);
            State = GlassPaneState.Intact;
        }

        /// <summary>
        /// Applies water hitting the pane for <paramref name="deltaTime"/>.
        /// Returns true if the pane changed state (cracked or broke).
        /// </summary>
        /// <param name="force">Water impact force in newtons (N); weaker than the minimum, zero or NaN is ignored.</param>
        /// <param name="deltaTime">Hit duration in seconds (s); negative and NaN values are ignored.</param>
        public bool ApplyImpact(float force, float deltaTime)
        {
            if (State == GlassPaneState.Broken || !(force > 0f) || force < _minImpactForce || !(deltaTime > 0f))
                return false;

            Damage += force * deltaTime;

            GlassPaneState next = Damage >= _breakImpulse ? GlassPaneState.Broken
                : Damage >= _crackImpulse ? GlassPaneState.Cracked
                : GlassPaneState.Intact;

            if (next == State)
                return false;

            State = next;
            return true;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}
