using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.Sounds
{
    /// <summary>
    /// Sound of one fire: it crackles while it burns, louder the stronger it is, and is silent when out;
    /// a puff when it goes out; a hiss while water hits it (steam). Far fires are quiet or not heard at all
    /// (the range is in <see cref="SoundSettings"/>). Without a fire model (flames on a burning teammate) it simply burns.
    /// </summary>
    public sealed class FireSound : MonoBehaviour
    {
        private FireExtinguishHandler _fire;
        private FireSteamEffect _steam;
        private SoundLoop _burning;
        private SoundLoop _hiss;
        private bool _hadFire;
        private bool _wasBurning;

        private void Awake()
        {
            _fire = GetComponent<FireExtinguishHandler>();
            _steam = GetComponent<FireSteamEffect>();
            _hadFire = _fire != null;
            _burning = new SoundLoop(gameObject, SoundId.FireBurning, 0.4f);
            _hiss = new SoundLoop(gameObject, SoundId.ExtinguishHiss, 0.15f);
        }

        private void OnDisable()
        {
            _burning.Stop();
            _hiss.Stop();
        }

        private void Update()
        {
            // A look-only copy of a fire (its fire model removed) always burns.
            bool hasFire = _hadFire && _fire != null;
            bool burning = !hasFire || _fire.IsBurning;
            float strength = hasFire ? Mathf.Clamp(_fire.Intensity, 0.35f, 1.5f) : 1f;

            _burning.Update(burning, strength);
            _hiss.Update(_steam != null && _steam.IsSteaming);

            if (hasFire && _wasBurning && !burning)
                Sound.Play(SoundId.FireOut, transform.position);
            _wasBurning = burning;
        }
    }
}
