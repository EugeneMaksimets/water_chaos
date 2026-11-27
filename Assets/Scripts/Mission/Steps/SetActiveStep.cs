using UnityEngine;

namespace WaterChaos.Mission
{
    /// <summary>Shows or hides objects (e.g. lights the fires on a roof); done at once.</summary>
    public sealed class SetActiveStep : MissionStep
    {
        [Header("Objects")]
        [SerializeField] private GameObject[] _objects = new GameObject[0];

        [SerializeField] private bool _active = true;

        protected override void OnTick()
        {
            foreach (GameObject target in _objects)
            {
                if (target != null)
                    target.SetActive(_active);
            }

            Complete();
        }
    }
}
