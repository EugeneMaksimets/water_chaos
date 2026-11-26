using UnityEngine;
using UnityEngine.Events;
using WaterChaos.UI;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Runs the mission: its steps are the child objects, started one after another in hierarchy order.
    /// The next step starts only when the current one is done, so no stage can be skipped;
    /// nothing advances while a story panel is open. Steps reach the UI through this runner,
    /// so game objects (doors, pumps, fires) never know about the mission or its UI.
    /// </summary>
    public sealed class MissionRunner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The player's root; zones check its position.")]
        [SerializeField] private Transform _player;

        [SerializeField] private ObjectiveListView _objectives;
        [SerializeField] private StoryPanelView _story;
        [SerializeField] private MissionHintView _hints;

        [Header("Flow")]
        [Tooltip("Starts the mission on Play; off = call StartMission() yourself (e.g. after the previous mission).")]
        [SerializeField] private bool _startOnPlay = true;

        [Tooltip("Writes every step change to the Console.")]
        [SerializeField] private bool _logSteps = true;

        [Tooltip("Called once after the last step; hook the next mission here.")]
        [SerializeField] private UnityEvent _missionCompleted = new UnityEvent();

        private MissionStep[] _steps;
        private int _index = -1;

        public Transform Player => _player;
        public ObjectiveListView Objectives => _objectives;
        public StoryPanelView Story => _story;
        public MissionHintView Hints => _hints;

        public bool IsStarted => _index >= 0;
        public bool IsCompleted { get; private set; }

        /// <summary>Step running now; null before the start and after the end.</summary>
        public MissionStep CurrentStep => IsStarted && !IsCompleted ? _steps[_index] : null;

        private void Awake()
        {
            if (_player == null || _objectives == null || _story == null || _hints == null)
            {
                Debug.LogError($"{name}: MissionRunner needs Player, Objectives, Story and Hints references.", this);
                enabled = false;
                return;
            }

            _steps = MissionStep.CollectChildren(transform);
        }

        private void Start()
        {
            if (_startOnPlay)
                StartMission();
        }

        public void StartMission()
        {
            if (!enabled || IsStarted)
                return;

            Advance();
        }

        private void Update()
        {
            if (!IsStarted || IsCompleted || _story.IsOpen)
                return;

            MissionStep step = _steps[_index];
            step.Tick();
            if (step.IsDone)
                Advance();
        }

        // Starts the next steps; instant ones (a drain, a met condition) finish in the same frame.
        private void Advance()
        {
            while (true)
            {
                _index++;
                if (_index >= _steps.Length)
                {
                    IsCompleted = true;
                    Log("mission completed");
                    _missionCompleted.Invoke();
                    return;
                }

                MissionStep step = _steps[_index];
                Log($"step {_index + 1}/{_steps.Length}: {step.name} ({step.GetType().Name})");
                step.Begin(this);

                if (!step.IsDone || _story.IsOpen)
                    return;
            }
        }

        [ContextMenu("Skip Current Step")]
        private void SkipCurrentStep()
        {
            MissionStep step = CurrentStep;
            if (step == null)
                return;

            Log($"skipped {step.name}");
            step.ForceComplete();
        }

        private void Log(string message)
        {
            if (_logSteps)
                Debug.Log($"[Mission] {message}", this);
        }
    }
}
