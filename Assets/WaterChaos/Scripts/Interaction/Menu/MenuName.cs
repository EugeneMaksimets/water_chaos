using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Name of an object in the interaction menu (Ukrainian), instead of its scene name ("Pump", "Fridge").
    /// Put it on the root of the object; aiming at any of its parts shows this name.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuName : MonoBehaviour, IDisplayName
    {
        [Tooltip("Name shown in the interaction menu.")]
        [SerializeField] private string _name;

        public string DisplayName => _name;
    }
}
