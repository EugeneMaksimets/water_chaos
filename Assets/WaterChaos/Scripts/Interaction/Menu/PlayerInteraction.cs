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

        [Header("Highlight")]
        [Tooltip("How much the object under the crosshair is tinted towards white (0 = off).")]
        [SerializeField, Range(0f, 1f)] private float _highlight = 0.3f;

        private readonly List<IInteractionProvider> _playerProviders = new List<IInteractionProvider>();
        private readonly List<InteractionAction> _collected = new List<InteractionAction>();
        private readonly List<InteractionAction> _actions = new List<InteractionAction>();
        private readonly List<InteractionAction> _menuActions = new List<InteractionAction>();
        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private InteractionHighlight _highlighter;

        /// <summary>Valid actions this frame, without duplicate labels. Do not modify.</summary>
        public IReadOnlyList<InteractionAction> Actions => _actions;

        /// <summary>Actions listed in the radial menu this frame (<see cref="InteractionAction.InMenu"/>). Do not modify.</summary>
        public IReadOnlyList<InteractionAction> MenuActions => _menuActions;

        /// <summary>Object the actions are about (see <see cref="FocusOf"/>); null when there is none (e.g. only hands actions).</summary>
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
            _highlighter = new InteractionHighlight(_highlight);
        }

        private void OnDisable()
        {
            _actions.Clear();
            _menuActions.Clear();
            Focus = null;
            _highlighter?.Clear();
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

            SelectMenuActions(_actions, _menuActions);
            Focus = FindFocus(_actions);
            Title = Focus != null ? GetName(Focus) : _actions.Count == 1 ? _actions[0].Label : DefaultTitle;
            _highlighter.Show(FirstTarget(_actions), Focus);
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

        /// <summary>
        /// Index of the action the primary button performs: the first primary action listed in the menu
        /// (e.g. "Open" on the fridge under the crosshair), else the first button-only one ("Water On/Off"); -1 if none.
        /// </summary>
        public static int PrimaryIndex(IReadOnlyList<InteractionAction> actions)
        {
            int buttonOnly = -1;
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].Button != InteractionButton.Primary)
                    continue;

                if (actions[i].InMenu)
                    return i;

                if (buttonOnly < 0)
                    buttonOnly = i;
            }

            return buttonOnly;
        }

        /// <summary>Fills <paramref name="menu"/> with the actions of <paramref name="all"/> that are listed in the radial menu.</summary>
        public static void SelectMenuActions(IReadOnlyList<InteractionAction> all, List<InteractionAction> menu)
        {
            menu.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].InMenu)
                    menu.Add(all[i]);
            }
        }

        /// <summary>Index of the first action performed by <paramref name="button"/>; -1 if there is none.</summary>
        public static int IndexOf(IReadOnlyList<InteractionAction> actions, InteractionButton button)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].Button == button)
                    return i;
            }

            return -1;
        }

        /// <summary>The object's <see cref="IDisplayName"/> (also on a parent, e.g. a hose end), else its cleaned scene name.</summary>
        private static string GetName(Transform focus)
        {
            IDisplayName named = focus.GetComponentInParent<IDisplayName>();
            return named != null ? named.DisplayName : CleanName(focus.name);
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

        private static Transform FirstTarget(List<InteractionAction> actions)
        {
            foreach (InteractionAction action in actions)
            {
                if (action.Target != null)
                    return action.Target.transform;
            }

            return null;
        }

        private static Transform FindFocus(List<InteractionAction> actions)
        {
            foreach (InteractionAction action in actions)
            {
                if (action.Target != null)
                    return FocusOf(action.Target.transform);
            }

            return null;
        }

        /// <summary>
        /// The whole object a target belongs to: its highest ancestor below any empty group objects
        /// (scene folders like "Gameplay/Pumps" that hold only a Transform), e.g. HoseAttachPoint → Pump.
        /// </summary>
        public static Transform FocusOf(Transform target)
        {
            Transform focus = target;
            while (focus.parent != null && !IsGroup(focus.parent))
                focus = focus.parent;

            return focus;
        }

        private static bool IsGroup(Transform transform)
        {
            return transform.gameObject.GetComponentCount() == 1;
        }
    }
}
