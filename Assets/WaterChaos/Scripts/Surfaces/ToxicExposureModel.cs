using System;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// Whether a character is exposed to toxic liquid, from any number of sources at once:
    /// standing in a toxic puddle or in toxic air (a cloud) as long as it lasts, and direct hits of a toxic stream
    /// (each hit keeps the exposure on for the hold time). One model per character, so overlapping sources never flicker or double up.
    /// </summary>
    public sealed class ToxicExposureModel
    {
        private float _lastHit = float.NegativeInfinity;

        /// <param name="holdTime">Seconds (s) direct exposure lasts after the last hit.</param>
        public ToxicExposureModel(float holdTime)
        {
            HoldTime = float.IsNaN(holdTime) ? 0f : Math.Max(0f, holdTime);
        }

        public float HoldTime { get; }
        public bool InPuddle { get; private set; }
        public bool InCloud { get; private set; }

        /// <summary>A toxic stream hit the character at <paramref name="time"/> (s); refreshes the hold timer.</summary>
        public void Hit(float time)
        {
            if (time > _lastHit)
                _lastHit = time;
        }

        public void SetInPuddle(bool inPuddle)
        {
            InPuddle = inPuddle;
        }

        public void SetInCloud(bool inCloud)
        {
            InCloud = inCloud;
        }

        public bool IsHitRecently(float time) => time - _lastHit <= HoldTime;

        /// <summary>Exposed by any source at <paramref name="time"/> (s).</summary>
        public bool IsExposed(float time) => InPuddle || InCloud || IsHitRecently(time);
    }
}
