using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace WaterChaos.Mission
{
    /// <summary>
    /// One step of a mission: a story panel, an objective, a scripted event.
    /// Started once by the <see cref="MissionRunner"/> (or a <see cref="StepGroup"/>), ticked every frame
    /// while running and completed once; a done step can never run again.
    /// Steps are child objects of the runner, in hierarchy order: reorder them by dragging in the Hierarchy,
    /// switch one off by deactivating its object.
    /// </summary>
    public abstract class MissionStep : MonoBehaviour
    {
        [Header("Step")]
        [Tooltip("Only inside a StepGroup: the group does not wait for this step and drops it when the others are done.")]
        [SerializeField] private bool _optional;

        [Tooltip("Called once when the step starts (e.g. change a teammate's animation, show an object).")]
        [SerializeField] private UnityEvent _started = new UnityEvent();

        [Tooltip("Called once when the step is completed.")]
        [SerializeField] private UnityEvent _completed = new UnityEvent();

        public bool IsOptional => _optional;
        public bool IsRunning { get; private set; }
        public bool IsDone { get; private set; }

        protected MissionRunner Runner { get; private set; }

        /// <summary>Starts the step; a condition already met completes it at once.</summary>
        public void Begin(MissionRunner runner)
        {
            if (IsRunning || IsDone)
                return;

            Runner = runner;
            IsRunning = true;
            OnBegin();
            _started.Invoke();
            Tick();
        }

        public void Tick()
        {
            if (IsRunning)
                OnTick();
        }

        /// <summary>Stops a running step without completing it (an optional step left behind by its group).</summary>
        public void Abort()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            OnEnd(false);
        }

        /// <summary>Completes the step as if its condition was met; for testing the mission in the Editor.</summary>
        public void ForceComplete()
        {
            Complete();
        }

        /// <summary>Steps on the active direct children of <paramref name="parent"/>, in hierarchy order.</summary>
        public static MissionStep[] CollectChildren(Transform parent)
        {
            List<MissionStep> steps = new List<MissionStep>();
            foreach (Transform child in parent)
            {
                if (child.gameObject.activeSelf)
                    steps.AddRange(child.GetComponents<MissionStep>());
            }

            return steps.ToArray();
        }

        protected void Complete()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            IsDone = true;
            OnEnd(true);
            _completed.Invoke();
        }

        protected virtual void OnBegin()
        {
        }

        /// <summary>Checks the condition; calls <see cref="Complete"/> when it is met.</summary>
        protected abstract void OnTick();

        /// <summary>Clean up (markers, hints, list line); <paramref name="completed"/> is false when aborted.</summary>
        protected virtual void OnEnd(bool completed)
        {
        }
    }
}
