#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using WaterChaos.Interaction;
using WaterChaos.Surfaces;
using WaterChaos.ToxicReaction;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>
    /// Toxic liquid on the real Cartoon Fire prefab: the fire keeps burning and reacts (green, cloud, splashes),
    /// a nearby fire is set off by the cloud, a character in the cloud is exposed, and the reaction ends.
    /// Needs "Water Chaos/Setup Toxic Fire Reaction" to have run.
    /// </summary>
    public class ToxicReactionPlayTests
    {
        private const string FirePath = "Assets/WaterChaos/Prefabs/Cartoon Fire.prefab";
        private const string TuningPath = "Assets/WaterChaos/ScriptableObjects/Tuning/WaterFlowTuning.asset";

        private sealed class FakeSource : IWaterFlowSource
        {
            public WaterFlowData Output { get; set; }
        }

        private GameObject _ground;
        private GameObject _fireA;
        private GameObject _fireB;
        private GameObject _nozzle;
        private GameObject _player;
        private FakeSource _source;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.localScale = new Vector3(60f, 1f, 60f);
            _ground.transform.position = new Vector3(0f, -0.5f, 0f);

            var firePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirePath);
            Assert.IsNotNull(firePrefab.GetComponentInChildren<FireToxicReaction>(), "run Setup Toxic Fire Reaction first");
            // The fire's collider is a 1 m box around it: fires stand that high above the floor, like spawned ones.
            _fireA = Object.Instantiate(firePrefab, new Vector3(0f, 0.5f, 0f), Quaternion.identity);
            _fireB = Object.Instantiate(firePrefab, new Vector3(2f, 0.5f, 0f), Quaternion.identity);

            _nozzle = new GameObject("Nozzle");
            _nozzle.SetActive(false);
            var stream = _nozzle.AddComponent<WaterStream>();
            var so = new SerializedObject(stream);
            so.FindProperty("_tuning").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WaterFlowTuning>(TuningPath);
            so.FindProperty("_nozzle").objectReferenceValue = _nozzle.transform;
            so.FindProperty("_hitMask").intValue = ~(1 << 2);
            so.ApplyModifiedPropertiesWithoutUndo();
            _nozzle.SetActive(true);
            _source = new FakeSource();
            stream.SetSource(_source);

            _player = new GameObject("Player");
            _player.transform.position = new Vector3(-1f, 1f, -1.2f);
            var body = _player.AddComponent<CharacterController>();
            body.center = Vector3.zero;
            body.height = 2f;
            _player.AddComponent<ToxicExposure>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject go in new[] { _ground, _fireA, _fireB, _nozzle, _player })
            {
                if (go != null)
                    Object.Destroy(go);
            }

            foreach (ToxicCloud cloud in Object.FindObjectsByType<ToxicCloud>(FindObjectsSortMode.None))
                Object.Destroy(cloud.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ToxicOnFire_FireReacts_CloudSetsOffNeighbour_ExposesPlayer_ThenEnds()
        {
            FireExtinguishHandler fireA = _fireA.GetComponentInChildren<FireExtinguishHandler>();
            FireExtinguishHandler fireB = _fireB.GetComponentInChildren<FireExtinguishHandler>();
            FireToxicReaction reactionA = _fireA.GetComponentInChildren<FireToxicReaction>();
            FireToxicReaction reactionB = _fireB.GetComponentInChildren<FireToxicReaction>();
            int chainGeneration = -1;
            reactionB.ReactionStarted += g => chainGeneration = g;

            // Aim at fire A from the side.
            Vector3 target = fireA.transform.position + Vector3.up * 0.2f;
            _nozzle.transform.position = target + new Vector3(-4f, 0.3f, 0f);
            _nozzle.transform.rotation = Quaternion.LookRotation(target - _nozzle.transform.position);
            _source.Output = new WaterFlowData(4f, 300000f, LiquidType.Toxic);

            yield return Wait(0.5f);
            Assert.IsTrue(reactionA.IsReacting, "toxic liquid makes the fire react");
            Assert.IsTrue(fireA.IsBurning, "it does not put the fire out");
            Assert.Greater(ToxicHazards.Count, 0, "a toxic cloud appeared");

            yield return Wait(1.5f);
            Assert.AreEqual(1, chainGeneration, "the cloud set off the fire next to it");
            Assert.IsTrue(_player.GetComponent<ToxicExposure>().IsExposed, "a character in the cloud is exposed");

            yield return Wait(2f);
            Assert.IsFalse(reactionA.IsReacting, "the reaction ends after its time, even under constant spraying");
            Assert.IsTrue(fireA.IsBurning);
        }

        [UnityTest]
        public IEnumerator AfterReaction_FlamesGetTheirOriginalColorsBack()
        {
            FireToxicReaction reaction = _fireA.GetComponentInChildren<FireToxicReaction>();
            ParticleSystem[] flames = _fireA.GetComponentsInChildren<ParticleSystem>();
            var before = new Color[flames.Length][];
            for (int i = 0; i < flames.Length; i++)
                before[i] = Keys(flames[i]);

            Assert.IsTrue(reaction.TryReact(0));
            yield return null;
            bool changed = false;
            for (int i = 0; i < flames.Length; i++)
                changed |= !Same(before[i], Keys(flames[i]));
            Assert.IsTrue(changed, "the flames turn toxic");

            yield return Wait(3f);
            for (int i = 0; i < flames.Length; i++)
                Assert.IsTrue(Same(before[i], Keys(flames[i])), $"{flames[i].name}: colors back to the original, not white");
        }

        private static Color[] Keys(ParticleSystem particles)
        {
            ParticleSystem.MinMaxGradient lifetime = particles.colorOverLifetime.color;
            Gradient gradient = lifetime.gradient ?? lifetime.gradientMax;
            var colors = new System.Collections.Generic.List<Color> { particles.main.startColor.color, particles.main.startColor.colorMax };
            if (gradient != null)
                foreach (GradientColorKey key in gradient.colorKeys)
                    colors.Add(key.color);
            return colors.ToArray();
        }

        private static bool Same(Color[] a, Color[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (((Vector4)(a[i] - b[i])).sqrMagnitude > 1e-6f)
                    return false;
            }

            return true;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
                yield return new WaitForFixedUpdate();
        }
    }
}
#endif
