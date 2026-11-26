using System;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Pure model of something that takes a number of blows before it breaks (a door under an axe).
    /// Damage shows as crack stages that grow with every blow; it breaks exactly once, blows after that do nothing.
    /// </summary>
    public sealed class DurabilityModel
    {
        private readonly float _strength;

        /// <summary>Damage taken so far, never above the strength.</summary>
        public float Damage { get; private set; }

        public bool IsBroken { get; private set; }

        /// <summary>0 = intact, 1 = broken.</summary>
        public float Progress => _strength > 0f ? Damage / _strength : 1f;

        /// <param name="strength">Damage at which it breaks (e.g. 4 = four normal blows); negative and NaN become 0.</param>
        public DurabilityModel(float strength)
        {
            _strength = float.IsNaN(strength) ? 0f : Math.Max(0f, strength);
        }

        /// <summary>Applies one blow. Returns true only on the blow that breaks it.</summary>
        /// <param name="damage">Damage of the blow; zero, negative and NaN are ignored.</param>
        public bool ApplyHit(float damage)
        {
            if (IsBroken || !(damage > 0f))
                return false;

            Damage = Math.Min(_strength, Damage + damage);
            if (Damage < _strength)
                return false;

            IsBroken = true;
            return true;
        }

        /// <summary>
        /// How many of <paramref name="stages"/> crack stages show: none while intact, the first after any damage,
        /// all of them just before breaking; none once broken (the object is gone).
        /// </summary>
        public int VisibleCrackStages(int stages)
        {
            if (IsBroken || stages <= 0 || !(Damage > 0f))
                return 0;

            return Math.Min(stages, Math.Max(1, (int)Math.Ceiling(Progress * stages - 1e-4)));
        }
    }
}
