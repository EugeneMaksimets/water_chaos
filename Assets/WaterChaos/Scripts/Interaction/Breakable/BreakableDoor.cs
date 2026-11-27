using System;
using System.Collections;
using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// A door leaf an axe can chop down (rules in <see cref="DurabilityModel"/>), like a window under water:
    /// blows do not swing it open, they crack it, stage by stage; the last blow breaks it once — the leaf leaves
    /// its hinge and disappears, its prepared planks fall with physics and later shrink away.
    /// Lives on the leaf, next to its colliders. Water still only pushes the leaf (WaterPushHandler).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BreakableDoor : MonoBehaviour, IToolHitReceiver
    {
        [Header("Strength")]
        [Tooltip("Normal axe blows it takes to break the door.")]
        [SerializeField, Min(0.1f)] private float _strength = 4f;

        [Header("Cracks")]
        [Tooltip("Crack overlays shown one after another as the door takes damage (inactive children).")]
        [SerializeField] private GameObject[] _crackStages = new GameObject[0];

        [Header("Fragments")]
        [Tooltip("Inactive child planks released when the door breaks.")]
        [SerializeField] private Rigidbody[] _fragments = new Rigidbody[0];

        [Tooltip("Impulse given to each plank along the blow, in newton-seconds (N·s).")]
        [SerializeField, Min(0f)] private float _fragmentImpulse = 6f;

        [Tooltip("Seconds (s) the planks stay before they shrink away.")]
        [SerializeField, Min(0f)] private float _fragmentLifetime = 8f;

        [Tooltip("Seconds (s) the planks take to shrink away.")]
        [SerializeField, Min(0.01f)] private float _shrinkTime = 0.8f;

        [Tooltip("Optional one-shot effect (splinters) spawned where the door was; it removes itself.")]
        [SerializeField] private GameObject _breakEffect;

        private DurabilityModel _model;

        /// <summary>Raised once, when the door breaks.</summary>
        public event Action<BreakableDoor> Broken;

        public bool IsBroken => Model.IsBroken;

        /// <summary>0 = intact, 1 = broken.</summary>
        public float Progress => Model.Progress;

        /// <summary>Crack overlays shown now.</summary>
        public int VisibleCracks => Model.VisibleCrackStages(_crackStages.Length);

        // Built on first use, so it also works before Awake (edit mode tests).
        private DurabilityModel Model => _model ??= new DurabilityModel(_strength);

        private void Awake()
        {
            ShowCracks(0);
            foreach (Rigidbody fragment in _fragments)
            {
                if (fragment != null)
                    fragment.gameObject.SetActive(false);
            }
        }

        public void ReceiveToolHit(in ToolHit hit)
        {
            if (this == null || !enabled || !gameObject.activeInHierarchy)
                return;

            // A blow never swings the door: it only damages it.
            bool broke = Model.ApplyHit(hit.Damage);
            ShowCracks(VisibleCracks);
            if (broke)
                Break(hit.Direction);
        }

        private void ShowCracks(int count)
        {
            for (int i = 0; i < _crackStages.Length; i++)
            {
                if (_crackStages[i] != null)
                    _crackStages[i].SetActive(i < count);
            }
        }

        private void Break(Vector3 direction)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            Vector3 center = renderers.Length > 0 ? renderers[0].bounds.center : transform.position;

            if (TryGetComponent(out Joint hinge))
                DestroyHinge(hinge);

            if (TryGetComponent(out Rigidbody body))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            // Active parts only: the planks are still inactive here.
            foreach (Collider part in GetComponentsInChildren<Collider>())
                part.enabled = false;
            foreach (Renderer part in renderers)
                part.enabled = false;

            if (_breakEffect != null)
                Instantiate(_breakEffect, center, transform.rotation);

            ReleaseFragments(direction.sqrMagnitude > 0f ? direction.normalized : transform.forward);
            WaterChaos.Sounds.Sound.Play(WaterChaos.Sounds.SoundId.DoorBreak, center);
            Broken?.Invoke(this);
        }

        private void ReleaseFragments(Vector3 push)
        {
            bool any = false;
            foreach (Rigidbody fragment in _fragments)
            {
                if (fragment == null)
                    continue;

                any = true;
                fragment.gameObject.SetActive(true);
                fragment.isKinematic = false;
                Vector3 spread = UnityEngine.Random.insideUnitSphere * 0.4f;
                fragment.AddForce((push + spread) * _fragmentImpulse, ForceMode.Impulse);
                fragment.AddTorque(UnityEngine.Random.insideUnitSphere * _fragmentImpulse * 0.3f, ForceMode.Impulse);
            }

            if (any && Application.isPlaying && isActiveAndEnabled)
                StartCoroutine(ClearFragments());
        }

        private static void DestroyHinge(Joint hinge)
        {
            if (Application.isPlaying)
                Destroy(hinge);
            else
                DestroyImmediate(hinge);
        }

        private IEnumerator ClearFragments()
        {
            yield return new WaitForSeconds(_fragmentLifetime);

            Vector3[] sizes = new Vector3[_fragments.Length];
            for (int i = 0; i < _fragments.Length; i++)
            {
                if (_fragments[i] != null)
                    sizes[i] = _fragments[i].transform.localScale;
            }

            for (float t = 0f; t < _shrinkTime; t += Time.deltaTime)
            {
                float k = 1f - t / _shrinkTime;
                for (int i = 0; i < _fragments.Length; i++)
                {
                    if (_fragments[i] != null)
                        _fragments[i].transform.localScale = sizes[i] * k;
                }

                yield return null;
            }

            foreach (Rigidbody fragment in _fragments)
            {
                if (fragment != null)
                    fragment.gameObject.SetActive(false);
            }
        }
    }
}
