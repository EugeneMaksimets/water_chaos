using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Items;
using WaterChaos.Player;
using WaterChaos.Sounds;

namespace WaterChaos.Tools
{
    /// <summary>
    /// A gas mask: in the hands, the primary button puts it on or takes it off. It stays in its inventory slot, so it can
    /// always be taken off again; while it is on it protects whatever is in the hands and is not seen in them.
    /// Thrown away, it comes off.
    /// </summary>
    [RequireComponent(typeof(PickableItem))]
    public sealed class GasMaskItem : MonoBehaviour, IEquippable
    {
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private PickableItem _item;
        private PlayerEquipment _owner;
        private PlayerGasMask _wearer;

        private void Awake()
        {
            _item = GetComponent<PickableItem>();
            GetComponentsInChildren(true, _renderers);
        }

        public void Equip(PlayerEquipment owner)
        {
            _owner = owner;
            _wearer = FindWearer(owner);
            _owner.Input.PrimaryPressed += Toggle;
            ShowInHands(_wearer == null || !_wearer.IsWorn);
        }

        public void Unequip()
        {
            if (_owner == null)
                return;

            _owner.Input.PrimaryPressed -= Toggle;

            // No longer in the inventory: it was thrown away, so it is not on the face any more.
            if (_wearer != null && _wearer.IsWorn && !_owner.Inventory.Model.Contains(_item))
            {
                _wearer.SetWorn(false);
                ShowInHands(true);
            }

            _owner = null;
        }

        private void Toggle()
        {
            if (_wearer == null)
            {
                Debug.LogError($"{name}: the player has no PlayerGasMask.", this);
                return;
            }

            bool wear = !_wearer.IsWorn;
            _wearer.SetWorn(wear);
            ShowInHands(!wear);
            Sound.Play(SoundId.PickUp, transform.position);
        }

        /// <summary>On the face it is not seen in the hands (the screen shows the mask instead).</summary>
        private void ShowInHands(bool visible)
        {
            foreach (Renderer part in _renderers)
            {
                if (part != null)
                    part.enabled = visible;
            }
        }

        private static PlayerGasMask FindWearer(PlayerEquipment owner)
        {
            PlayerGasMask wearer = owner.GetComponent<PlayerGasMask>();
            return wearer != null ? wearer : owner.GetComponentInParent<PlayerGasMask>();
        }
    }
}
