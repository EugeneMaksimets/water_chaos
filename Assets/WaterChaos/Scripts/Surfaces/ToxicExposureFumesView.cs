using UnityEngine;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// Green fumes rising from a character (an NPC) while its <see cref="ToxicExposure"/> says it is exposed,
    /// so others can see who is in toxic air or liquid. Display only.
    /// </summary>
    public sealed class ToxicExposureFumesView : MonoBehaviour
    {
        [SerializeField] private ToxicExposure _exposure;

        [Tooltip("Looping fumes above the character.")]
        [SerializeField] private ParticleSystem _fumes;

        private void Awake()
        {
            if (_exposure == null || _fumes == null)
            {
                Debug.LogError($"{name}: ToxicExposureFumesView needs Exposure and Fumes references.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            bool exposed = _exposure.IsExposed;
            if (exposed && !_fumes.isEmitting)
                _fumes.Play();
            else if (!exposed && _fumes.isEmitting)
                _fumes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
