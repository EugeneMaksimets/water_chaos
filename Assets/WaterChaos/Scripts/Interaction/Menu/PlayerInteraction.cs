using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Single list of what the player can do right now: gathers actions from the player's own
    /// <see cref="IInteractionProvider"/> components and from the world object under the crosshair.
    /// It only collects; the menu decides how the actions are shown and picked.
    /// Runs after the providers have found their targets this frame.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        private const int MaxHits = 16;
        private const string DefaultTitle = "Interact";

        [Header("References")]
        [Tooltip("The look ray for world objects starts here and goes along its forward (the player camera).")]
        [SerializeField] private Transform _camera;

        [Header("Settings")]
        [Tooltip("How far the player can reach a world object that offers its own actions, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxDistance = 3f;

        [Tooltip("Layers the look ray can hit. Exclude the Player layer.")]
        [SerializeField] private LayerMask _interactMask = ~0;

        private readonly List<IInteractionProvider> _playerProviders = new List<IInteractionProvider>();
        private readonly List<InteractionAction> _collected = new List<InteractionAction>();
        private readonly List<InteractionAction> _actions = new List<InteractionAction>();
        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];

        /// <summary>Valid actions this frame, without duplicate labels. Do not modify.</summary>
        public IReadOnlyList<InteractionAction> Actions => _actions;

        /// <summary>Root of the object the actions are about; null when there is none (e.g. only hands actions).</summary>
        public Transform Focus { get; private set; }

        /// <summary>Menu title: the focus object's name, the only action's label, or "Interact".</summary>
        public string Title { get; private set; } = DefaultTitle;

        private void Awake()
        {
            if (_camera == null)
            {
                Debug.LogError($"{name}: PlayerInteraction needs a Camera reference.", this);
                enabled = false;
                return;
            }

            GetComponents(_playerProviders);
        }

        private void OnDisable()
        {
            _actions.Clear();
            Focus = null;
            Title = DefaultTitle;
        }

        private void Update()
        {
            _actions.Clear();

            foreach (IInteractionProvider provider in _playerProviders)
            {
                if (provider is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                    continue;

                Collect(provider);
            }

            IInteractionProvider world = FindWorldProvider(new Ray(_camera.position, _camera.forward), _maxDistance, _interactMask, _hits);
            if (world != null && !_playerProviders.Contains(world))
                Collect(world);

            Focus = FindFocus(_actions);
            Title = Focus != null ? CleanName(Focus.name) : _actions.Count == 1 ? _actions[0].Label : DefaultTitle;
        }

        private void Collect(IInteractionProvider provider)
        {
            _collected.Clear();
            provider.CollectActions(this, _collected);
            foreach (InteractionAction action in _collected)
                AddUnique(_actions, action);
        }

        /// <summary>Adds the action unless one with the same label is already listed. True if added.</summary>
        public static bool AddUnique(List<InteractionAction> actions, InteractionAction action)
        {
            foreach (InteractionAction listed in actions)
            {
                if (listed.Label == action.Label)
                    return false;
            }

            actions.Add(action);
            return true;
        }

        /// <summary>Scene name without Unity's duplicate suffix: "Pump (1)" becomes "Pump".</summary>
        public static string CleanName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return DefaultTitle;

            int open = objectName.LastIndexOf(" (");
            if (open > 0 && objectName.EndsWith(")"))
            {
                string number = objectName.Substring(open + 2, objectName.Length - open - 3);
                if (int.TryParse(number, out _))
                    return objectName.Substring(0, open);
            }

            return objectName;
        }

        /// <summary>
        /// Nearest world provider along the ray. Triggers without a provider (water volumes, wind zones)
        /// are looked through; the first other solid collider blocks the view.
        /// </summary>
        public static IInteractionProvider FindWorldProvider(Ray ray, float maxDistance, int mask, RaycastHit[] buffer)
        {
            int count = Physics.RaycastNonAlloc(ray, buffer, maxDistance, mask, QueryTriggerInteraction.Collide);

            IInteractionProvider best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = buffer[i];
                if (hit.distance >= bestDistance)
                    continue;

                IInteractionProvider provider = hit.collider.GetComponentInParent<IInteractionProvider>();
                if (provider == null && hit.collider.isTrigger)
                    continue;

                best = provider;
                bestDistance = hit.distance;
            }

            return best;
        }

        private static Transform FindFocus(List<InteractionAction> actions)
        {
            foreach (InteractionAction action in actions)
            {
                if (action.Target != null)
                    return action.Target.transform.root;
            }

            return null;
        }
    }
}
