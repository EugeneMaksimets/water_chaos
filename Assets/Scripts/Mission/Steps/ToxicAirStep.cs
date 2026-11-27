using UnityEngine;
using WaterChaos.Surfaces;

namespace WaterChaos.Mission
{
    /// <summary>Fills a building with toxic air, or clears it; done at once.</summary>
    public sealed class ToxicAirStep : MissionStep
    {
        [Header("Toxic Air")]
        [SerializeField] private ToxicAirZone _zone;

        [Tooltip("On: the building fills with toxic air. Off: the air clears.")]
        [SerializeField] private bool _active = true;

        protected override void OnTick()
        {
            if (_zone != null)
                _zone.SetActive(_active);
            else
                Debug.LogError($"{name}: ToxicAirStep needs a Zone.", this);

            Complete();
        }
    }
}
