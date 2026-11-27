#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using WaterChaos.Interaction;
using WaterChaos.Rooms;
using WaterChaos.Surfaces;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>
    /// Puddles, snow, ice and toxic exposure with the real stream, room and surface controller:
    /// a cold room (-5 °C) on the left, the outdoors (no temperature zone) on the right.
    /// Needs "Water Chaos/Setup Puddles, Snow, Ice and Toxic Exposure" to have created the assets.
    /// </summary>
    public class LiquidSurfacePlayTests
    {
        private const string SettingsPath = "Assets/WaterChaos/ScriptableObjects/Surfaces/LiquidSurfaceSettings.asset";
        private const string PuddlePath = "Assets/WaterChaos/Prefabs/Surfaces/Puddle.prefab";
        private const string SnowdriftPath = "Assets/WaterChaos/Prefabs/Surfaces/Snowdrift.prefab";
        private const string TuningPath = "Assets/WaterChaos/ScriptableObjects/Tuning/WaterFlowTuning.asset";

        private static readonly Vector3 RoomCenter = new Vector3(-20f, 1f, 0f);
        private static readonly Vector3 Outdoors = new Vector3(20f, 0f, 0f);

        private sealed class FakeSource : IWaterFlowSource
        {
            public WaterFlowData Output { get; set; }
        }

        private GameObject _ground;
        private GameObject _room;
        private GameObject _surfaces;
        private GameObject _nozzle;
        private GameObject _player;
        private WaterStream _stream;
        private FakeSource _source;
        private LiquidSurfaceController _controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.localScale = new Vector3(100f, 1f, 100f);
            _ground.transform.position = new Vector3(0f, -0.5f, 0f);

            _room = new GameObject("Cold Room");
            _room.SetActive(false);
            _room.transform.position = RoomCenter;
            var box = _room.AddComponent<BoxCollider>();
            // Reaches below the floor, so a puddle exactly on the floor is inside.
            box.size = new Vector3(12f, 4f, 12f);
            var room = _room.AddComponent<RoomTemperature>();
            Set(room, "_startTemperature", -5f);
            Set(room, "_minTemperature", -100f);
            _room.layer = 2; // Ignore Raycast: the stream must not hit the room volume.
            _room.SetActive(true);

            _surfaces = new GameObject("Liquid Surfaces");
            _surfaces.SetActive(false);
            _controller = _surfaces.AddComponent<LiquidSurfaceController>();
            var so = new SerializedObject(_controller);
            so.FindProperty("_settings").objectReferenceValue = Load<LiquidSurfaceSettings>(SettingsPath);
            so.FindProperty("_puddlePrefab").objectReferenceValue = Load<GameObject>(PuddlePath).GetComponent<PuddleView>();
            so.FindProperty("_snowdriftPrefab").objectReferenceValue = Load<GameObject>(SnowdriftPath).GetComponent<SnowdriftView>();
            so.ApplyModifiedPropertiesWithoutUndo();
            _surfaces.SetActive(true);

            _nozzle = new GameObject("Nozzle");
            _nozzle.SetActive(false);
            _stream = _nozzle.AddComponent<WaterStream>();
            var streamSo = new SerializedObject(_stream);
            streamSo.FindProperty("_tuning").objectReferenceValue = Load<WaterFlowTuning>(TuningPath);
            streamSo.FindProperty("_nozzle").objectReferenceValue = _nozzle.transform;
            streamSo.FindProperty("_hitMask").intValue = ~(1 << 2);
            streamSo.ApplyModifiedPropertiesWithoutUndo();
            _nozzle.SetActive(true);

            _source = new FakeSource();
            _stream.SetSource(_source);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject go in new[] { _ground, _room, _surfaces, _nozzle, _player })
            {
                if (go != null)
                    Object.Destroy(go);
            }

            yield return null;
        }

        // ───────────── Scenario A: normal water outdoors ─────────────

        [UnityTest]
        public IEnumerator A_Water_MakesOnePuddle_GrowsInPlace_SecondSpotMakesSecond()
        {
            Spray(LiquidType.Water, Outdoors);
            yield return Wait(0.6f);

            Assert.AreEqual(1, _controller.Puddles.Count);
            PuddleModel first = _controller.Puddles.Puddles[0];
            float radius = first.Radius;

            yield return Wait(1.5f);
            Assert.AreEqual(1, _controller.Puddles.Count, "the same spot grows the same puddle");
            Assert.Greater(first.Radius, radius);
            Assert.IsFalse(_stream.IsSnow);

            Spray(LiquidType.Water, Outdoors + new Vector3(0f, 0f, 8f));
            yield return Wait(0.6f);
            Assert.AreEqual(2, _controller.Puddles.Count);
            Assert.AreEqual(2, _surfaces.GetComponentsInChildren<PuddleView>().Length, "one view per puddle");
        }

        // ───────────── Scenario B/C: cold room vs outdoors ─────────────

        [UnityTest]
        public IEnumerator B_InColdRoom_WaterTurnsToSnow_PilesUp_AndBlocksFire()
        {
            Vector3 target = RoomCenter + new Vector3(2f, -1f, 0f);
            Spray(LiquidType.Water, target, RoomCenter + new Vector3(-2f, 0.5f, 0f));
            yield return Wait(2f);

            Assert.IsTrue(_stream.IsSnow);
            Assert.AreEqual(0, _controller.Puddles.Count, "snow makes drifts, not puddles");
            Assert.AreEqual(1, _controller.Snowdrifts.Count);
            SnowdriftModel drift = _controller.Snowdrifts.Drifts[0];
            Assert.Greater(drift.Height, 0f);
            Assert.IsTrue(FireIgnitionRules.IsBlocked(drift.Center + Vector3.up * 0.3f), "no new fire on the snowdrift");
            Assert.IsFalse(FireIgnitionRules.IsBlocked(drift.Center + new Vector3(4f, 0.3f, 0f)), "fire elsewhere is unchanged");
        }

        [UnityTest]
        public IEnumerator B_WaterSprayedIntoColdRoomFromOutside_FreezesThere_OutdoorPuddleStaysLiquid()
        {
            // Nozzle outdoors (no zone): water, not snow. It lands inside the cold room.
            Vector3 inside = RoomCenter + new Vector3(0f, -1f, 0f);
            Spray(LiquidType.Water, inside, new Vector3(-10f, 1.5f, 0f));
            yield return Wait(1f);
            Assert.IsFalse(_stream.IsSnow, "outdoors the nozzle sprays water");

            Spray(LiquidType.Water, Outdoors);
            yield return Wait(1f);
            Stop();
            yield return Wait(2.5f);

            Assert.AreEqual(2, _controller.Puddles.Count);
            PuddleModel indoor = null;
            PuddleModel outdoor = null;
            foreach (PuddleModel puddle in _controller.Puddles.Puddles)
            {
                if (puddle.Center.x < -14f)
                    indoor = puddle;
                else if (puddle.Center.x > 0f)
                    outdoor = puddle;
            }

            Assert.IsNotNull(indoor, "the water landed inside the room");
            Assert.IsNotNull(outdoor);
            Assert.IsTrue(indoor.IsFrozen, "the indoor puddle froze");
            Assert.IsTrue(LiquidSurfaceController.TryGetIceAt(indoor.Center, out float friction));
            Assert.Less(friction, 0.5f);
            Assert.IsFalse(outdoor.IsFrozen, "the outdoor puddle never freezes");
            Assert.IsFalse(LiquidSurfaceController.TryGetIceAt(outdoor.Center, out _));
        }

        [UnityTest]
        public IEnumerator C_LeavingTheColdRoom_WaterComesBack()
        {
            Spray(LiquidType.Water, RoomCenter + new Vector3(2f, -1f, 0f), RoomCenter);
            yield return Wait(0.3f);
            Assert.IsTrue(_stream.IsSnow);

            Spray(LiquidType.Water, Outdoors);
            yield return Wait(0.3f);
            Assert.IsFalse(_stream.IsSnow);
        }

        // ───────────── Scenario D/E: gasoline and toxic liquid never freeze ─────────────

        [UnityTest]
        public IEnumerator D_Gasoline_InColdRoom_StaysLiquid()
        {
            Spray(LiquidType.Gasoline, RoomCenter + new Vector3(2f, -1f, 0f), RoomCenter);
            yield return Wait(3f);

            Assert.IsFalse(_stream.IsSnow);
            Assert.AreEqual(0, _controller.Snowdrifts.Count);
            Assert.AreEqual(1, _controller.Puddles.Count);
            Assert.AreEqual(LiquidType.Gasoline, _controller.Puddles.Puddles[0].Liquid);
            Assert.IsFalse(_controller.Puddles.Puddles[0].IsFrozen);
        }

        [UnityTest]
        public IEnumerator E_Toxic_InColdRoom_StaysLiquid_AndExposesPlayerStandingInIt()
        {
            Vector3 spot = RoomCenter + new Vector3(2f, -1f, 0f);
            Spray(LiquidType.Toxic, spot, RoomCenter);
            yield return Wait(3f);
            Stop();

            Assert.IsFalse(_stream.IsSnow);
            Assert.AreEqual(1, _controller.Puddles.Count);
            Assert.IsFalse(_controller.Puddles.Puddles[0].IsFrozen);

            ToxicExposure exposure = SpawnPlayer(_controller.Puddles.Puddles[0].Center + Vector3.up * 1f);
            yield return Wait(0.3f);
            Assert.IsTrue(exposure.IsExposed, "standing in the toxic puddle");

            _player.transform.position = Outdoors + new Vector3(0f, 1f, 30f);
            yield return Wait(0.3f);
            Assert.IsFalse(exposure.IsExposed, "left the puddle");
        }

        [UnityTest]
        public IEnumerator E_DirectToxicSpray_ExposesPlayer_ThenFades()
        {
            ToxicExposure exposure = SpawnPlayer(Outdoors + new Vector3(0f, 1f, 0f));
            yield return Wait(0.2f);
            Assert.IsFalse(exposure.IsExposed);

            Spray(LiquidType.Toxic, _player.transform.position, Outdoors + new Vector3(-6f, 1f, 0f));
            yield return Wait(0.5f);
            Assert.IsTrue(exposure.IsExposed, "hit directly by a toxic stream");
            Assert.IsFalse(exposure.InToxicPuddle);

            Stop();
            yield return Wait(2f);
            Assert.IsFalse(exposure.IsExposed, "exposure ends after the hold time");
            Assert.AreEqual(0, _controller.Puddles.Count, "liquid hitting a character leaves no puddle");
            Assert.AreEqual(1, _player.GetComponents<ToxicExposure>().Length);
        }

        // ───────────── Helpers ─────────────

        private ToxicExposure SpawnPlayer(Vector3 position)
        {
            _player = new GameObject("Player");
            _player.transform.position = position;
            var body = _player.AddComponent<CharacterController>();
            body.height = 2f;
            body.center = Vector3.zero;
            return _player.AddComponent<ToxicExposure>();
        }

        /// <summary>Points the nozzle at the target from a spot 6 m away and 2 m up (or from <paramref name="from"/>).</summary>
        private void Spray(LiquidType liquid, Vector3 target, Vector3? from = null)
        {
            Vector3 origin = from ?? target + new Vector3(-6f, 2f, 0f);
            _nozzle.transform.position = origin;
            _nozzle.transform.rotation = Quaternion.LookRotation(target - origin);
            _source.Output = new WaterFlowData(4f, 300000f, liquid);
        }

        private void Stop()
        {
            _source.Output = default;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
                yield return new WaitForFixedUpdate();
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"missing {path}: run the liquid surface setup first");
            return asset;
        }

        private static void Set(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
