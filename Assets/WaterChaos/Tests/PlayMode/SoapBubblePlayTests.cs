#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using WaterChaos.Soap;
using WaterChaos.Teammate;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>
    /// The whole bubble effect on the real teammate prefab (the original Firefighter inside):
    /// forms around the character, lifts both together, stops under ceilings, pops, the character falls and lands
    /// and gets its own movement back. Needs "Water Chaos/Setup Soap" to have created the prefab.
    /// </summary>
    public class SoapBubblePlayTests
    {
        private const string TeammatePath = "Assets/WaterChaos/Prefabs/Soap/Teammate_Firefighter.prefab";
        private const float TimeScale = 3f;
        private const float Timeout = 30f; // game seconds

        private GameObject _ground;
        private GameObject _ceiling;
        private GameObject _teammate;
        private SoapBubbleController _controller;
        private SoapBubbleReceiver _receiver;
        private float _savedTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = TimeScale;

            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.localScale = new Vector3(30f, 1f, 30f);
            _ground.transform.position = new Vector3(0f, -0.5f, 0f);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TeammatePath);
            Assert.IsNotNull(prefab, "run Water Chaos/Setup Soap first");
            _teammate = Object.Instantiate(prefab, new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            _controller = _teammate.GetComponent<SoapBubbleController>();
            _receiver = _teammate.GetComponent<SoapBubbleReceiver>();

            // Let the teammate settle on the ground.
            yield return WaitFixed(0.5f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = _savedTimeScale;
            foreach (GameObject go in new[] { _teammate, _ground, _ceiling })
            {
                if (go != null)
                    Object.Destroy(go);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Bubble_FormsAroundTheFirefighter_WhoStaysVisible()
        {
            Renderer[] model = ModelRenderers();
            Assert.Greater(model.Length, 0, "the Firefighter model is there");

            Spray();
            yield return new WaitForFixedUpdate();

            Assert.IsNotNull(_controller.ActiveBubble);
            Vector3 modelScale = model[0].transform.lossyScale;

            float end = Time.time + Timeout;
            while (_controller.Phase != SoapBubblePhase.Falling)
            {
                SoapBubbleView bubble = _controller.ActiveBubble;
                foreach (Renderer part in model)
                {
                    Assert.IsTrue(part.enabled && part.gameObject.activeInHierarchy, "firefighter visible all the time");
                    if (bubble != null)
                        Assert.IsFalse(part.transform.IsChildOf(bubble.transform), "the character is not part of the bubble");
                }

                Assert.AreEqual(modelScale, model[0].transform.lossyScale, "the character is never scaled");
                Assert.Less(Time.time, end, "the effect never finished");
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator CharacterAndBubble_RiseTogetherToTheThirdFloor()
        {
            float startY = _teammate.transform.position.y;
            Spray();
            yield return WaitForPhase(SoapBubblePhase.Rising);

            SoapBubbleView bubble = _controller.ActiveBubble;
            Vector3 offset = bubble.Shell.position - _teammate.transform.position;
            float top = startY;
            while (_controller.Phase == SoapBubblePhase.Rising)
            {
                Assert.AreEqual(offset.y, (bubble.Shell.position - _teammate.transform.position).y, 0.01f, "moving together");
                top = Mathf.Max(top, _teammate.transform.position.y);
                yield return new WaitForFixedUpdate();
            }

            Assert.AreEqual(startY + _controller.RiseHeight, top, 0.5f, "about the third floor");
        }

        [UnityTest]
        public IEnumerator Bubble_Bursts_ThenFirefighterFallsAndLandsWithMovementBack()
        {
            Spray();
            yield return WaitForPhase(SoapBubblePhase.Falling);

            Assert.IsNull(_controller.ActiveBubble, "bubble popped");
            Assert.IsTrue(_teammate.GetComponent<TeammateMotor>().enabled, "gravity back right after the pop");
            float releaseY = _teammate.transform.position.y;
            Assert.Greater(releaseY, 3f);

            yield return WaitForPhase(SoapBubblePhase.Normal);

            Assert.Less(_teammate.transform.position.y, 0.3f, "landed on the ground");
            Assert.IsTrue(_teammate.GetComponent<CharacterController>().enabled);
            Assert.IsTrue(_teammate.GetComponent<TeammateMotor>().enabled);
        }

        [UnityTest]
        public IEnumerator Bubble_UnderACeiling_NeverGoesThroughIt()
        {
            _ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ceiling.name = "Ceiling";
            _ceiling.transform.localScale = new Vector3(10f, 0.3f, 10f);
            _ceiling.transform.position = new Vector3(0f, 4f, 0f);
            float ceilingBottom = 4f - 0.15f;
            yield return new WaitForFixedUpdate();

            var character = _teammate.GetComponent<CharacterController>();
            Spray();
            while (_controller.Phase != SoapBubblePhase.Falling)
            {
                Assert.LessOrEqual(character.bounds.max.y, ceilingBottom + 0.01f, "character stays below the ceiling");
                yield return new WaitForFixedUpdate();
            }

            yield return WaitForPhase(SoapBubblePhase.Normal);
            Assert.Less(_teammate.transform.position.y, 0.3f);
        }

        [UnityTest]
        public IEnumerator RepeatedSpraying_MakesOneBubbleAtATime()
        {
            for (int i = 0; i < 30; i++)
            {
                Spray();
                Assert.LessOrEqual(_teammate.GetComponentsInChildren<SoapBubbleView>().Length, 1);
                yield return new WaitForFixedUpdate();
            }
        }

        // ───────────── Helpers ─────────────

        private void Spray()
        {
            _receiver.ReceiveImpact(new WaterImpact(_teammate.transform.position + Vector3.up, Vector3.back, Vector3.forward,
                0.2f, 50f, LiquidType.Water, true));
        }

        private Renderer[] ModelRenderers()
        {
            return _teammate.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0
                ? (Renderer[])_teammate.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                : _teammate.GetComponentsInChildren<MeshRenderer>(true);
        }

        private IEnumerator WaitForPhase(SoapBubblePhase phase)
        {
            float end = Time.time + Timeout;
            while (_controller.Phase != phase)
            {
                Assert.Less(Time.time, end, $"never reached {phase}");
                yield return new WaitForFixedUpdate();
            }
        }

        private static IEnumerator WaitFixed(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
                yield return new WaitForFixedUpdate();
        }
    }
}
#endif
