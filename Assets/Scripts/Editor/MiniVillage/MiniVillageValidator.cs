using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterChaos.FireTruck;
using WaterChaos.Interaction;
using WaterChaos.Rooms;
using WaterChaos.Water;

namespace WaterChaos.EditorTools.MiniVillage
{
    /// <summary>
    /// Checks the mini village scene: references, components, and a physics run in edit mode —
    /// props must rest quietly without water, react to strong water by weight class, and windows must
    /// crack and shatter only under strong water. The scene is opened, simulated and never saved.
    /// </summary>
    public static class MiniVillageValidator
    {
        private const float Step = 0.02f;

        private static readonly StringBuilder Report = new StringBuilder();
        private static int _failures;

        [MenuItem("Tools/Water Chaos/Validate Mini Village")]
        public static void Validate()
        {
            Report.Clear();
            _failures = 0;
            Scene scene = EditorSceneManager.OpenScene(MiniVillageBuilder.ScenePath, OpenSceneMode.Single);
            GameObject[] all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();

            CheckReferences(all);
            CheckComponents(all);
            SimulatePhysics(all);

            Report.Insert(0, _failures == 0 ? "MINI VILLAGE VALIDATION: PASSED\n" : $"MINI VILLAGE VALIDATION: {_failures} FAILURE(S)\n");
            Debug.Log(Report.ToString());

            // Leave the scene untouched on disk.
            EditorSceneManager.OpenScene(MiniVillageBuilder.ScenePath, OpenSceneMode.Single);
        }

        private static void CheckReferences(GameObject[] all)
        {
            int missingScripts = all.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
            int missingPrefabs = all.Count(PrefabUtility.IsPrefabAssetMissing);
            // Only the village: the nozzle's stream and hose meshes are generated at runtime.
            GameObject[] village = all.Where(g => g.transform.root.name == "MiniVillage").ToArray();
            int missingMeshes = village.Select(g => g.GetComponent<MeshFilter>()).Count(f => f != null && f.sharedMesh == null);
            int missingMaterials = all.Select(g => g.GetComponent<MeshRenderer>())
                .Count(r => r != null && !(r.GetComponent<TextMesh>() != null) && r.sharedMaterials.Any(m => m == null));
            Expect(missingScripts == 0, $"Missing scripts: {missingScripts}");
            Expect(missingPrefabs == 0, $"Missing prefabs: {missingPrefabs}");
            Expect(missingMeshes == 0, $"Village mesh filters without mesh: {missingMeshes}");
            Expect(missingMaterials == 0, $"Renderers with a missing material: {missingMaterials}");

            foreach (string group in new[] { "MiniVillage", "Gameplay", "Lighting", "UI" })
                Expect(all.Any(g => g.name == group && g.transform.parent == null), $"Root group {group}");
            foreach (string name in new[] { "Player", "Pool_Rectangular", "Pool_Octagonal", "Pond", "Pump", "Pump (1)", "Nozzle", "FireHydrant_Red", "Canvas", "EventSystem", "FireTruck" })
                Expect(all.Any(g => g.name == name), $"Gameplay object {name}");
        }

        private static void CheckComponents(GameObject[] all)
        {
            WaterPushHandler[] pushables = all.SelectMany(g => g.GetComponents<WaterPushHandler>()).ToArray();
            WaterBreakableGlass[] glass = all.SelectMany(g => g.GetComponents<WaterBreakableGlass>()).ToArray();
            RoomTemperature[] rooms = all.SelectMany(g => g.GetComponents<RoomTemperature>()).ToArray();
            WaterReservoirComponent[] reservoirs = all.SelectMany(g => g.GetComponents<WaterReservoirComponent>()).ToArray();
            FireExtinguishHandler[] fires = all.SelectMany(g => g.GetComponents<FireExtinguishHandler>()).ToArray();

            Info($"Water-pushable objects: {pushables.Length}");
            Info($"Breakable glass panes: {glass.Length}");
            Info($"Rooms with temperature: {rooms.Length}");
            Info($"Water sources (reservoirs): {reservoirs.Length}");
            Info($"Fires: {fires.Length}");

            Expect(pushables.Length >= 150, "At least 150 water-pushable objects");
            Expect(glass.Length >= 40, "At least 40 breakable windows");
            Expect(rooms.Length >= 8, "At least 8 rooms");
            Expect(reservoirs.Length >= 3, "Two pools and the pond are water sources");
            Expect(fires.Length >= 2, "Fires placed");

            var profileCounts = new Dictionary<string, int>();
            int noCollider = 0, noProfile = 0, kinematic = 0;
            foreach (WaterPushHandler pushable in pushables)
            {
                if (pushable.GetComponent<Collider>() == null) noCollider++;
                var profile = (WaterPushProfile)new SerializedObject(pushable).FindProperty("_profile").objectReferenceValue;
                if (profile == null) noProfile++;
                else profileCounts[profile.name] = profileCounts.TryGetValue(profile.name, out int n) ? n + 1 : 1;
                if (pushable.GetComponent<Rigidbody>().isKinematic) kinematic++;
            }
            Expect(noCollider == 0, $"Pushables without collider on the same object: {noCollider}");
            Expect(noProfile == 0, $"Pushables without profile: {noProfile}");
            Expect(kinematic == 0, $"Kinematic pushables: {kinematic}");
            Info("Pushables per profile: " + string.Join(", ", profileCounts.OrderBy(p => p.Key).Select(p => $"{p.Key} {p.Value}")));
            Expect(profileCounts.Count >= 6, "Several different physics profiles in use");

            int glassWithoutShards = glass.Count(g => ((Rigidbody[])Field(g, "_fragments")).Length == 0);
            Expect(glassWithoutShards == 0, $"Windows without shards: {glassWithoutShards}");

            // Static scenery must never move: no rigid bodies on buildings, trees, hills, roads, fences.
            string[] staticRoots = { "ResidentialArea", "Trees", "Hills", "Roads", "Fences", "Hedges", "StreetLamps", "Structures", "PoolStructures" };
            int movableStatic = 0;
            foreach (GameObject root in all.Where(g => staticRoots.Contains(g.name)))
            {
                movableStatic += root.GetComponentsInChildren<Rigidbody>(true)
                    .Count(b => b.GetComponent<HingeJoint>() == null && b.gameObject.layer != 2);
            }
            Expect(movableStatic == 0, $"Rigidbodies on static scenery (doors and glass shards excluded): {movableStatic}");

            int treesWithCollider = all.Where(g => g.name.StartsWith("Tree")).Count(g => g.GetComponent<CapsuleCollider>() != null);
            Info($"Trees with trunk collider: {treesWithCollider}");
        }

        // ───────────── Physics ─────────────

        private static void SimulatePhysics(GameObject[] all)
        {
            SimulationMode previous = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                foreach (WaterPushHandler handler in all.SelectMany(g => g.GetComponents<WaterPushHandler>()))
                    CallAwake(handler);
                foreach (WaterBreakableGlass pane in all.SelectMany(g => g.GetComponents<WaterBreakableGlass>()))
                    CallAwake(pane);

                Rigidbody[] bodies = all.SelectMany(g => g.GetComponents<Rigidbody>())
                    .Where(b => !b.isKinematic && b.gameObject.activeInHierarchy && b.GetComponent<WaterPushHandler>() != null).ToArray();
                Dictionary<Rigidbody, Vector3> start = bodies.ToDictionary(b => b, b => b.position);

                Simulate(4f);
                var drifted = bodies.Where(b => b.GetComponent<HingeJoint>() == null && (b.position - start[b]).magnitude > 0.35f).ToArray();
                var fell = bodies.Where(b => b.position.y < -1f).ToArray();
                Info($"Settle 4 s without water: {bodies.Length} bodies, {drifted.Length} moved > 0.35 m, {fell.Length} fell through");
                foreach (Rigidbody body in drifted.Take(15))
                    Info($"   moved: {Path(body.transform)} by {(body.position - start[body]).magnitude:0.00} m");
                Expect(drifted.Length <= bodies.Length / 20, "Props rest quietly without water (≤5% settle more than 0.35 m)");
                Expect(fell.Length == 0, "No prop falls through the ground");

                TestPushes(all);
                TestGlass(all);
                TestFireTruck(all);
            }
            finally
            {
                Physics.simulationMode = previous;
            }
        }

        private static void TestPushes(GameObject[] all)
        {
            // Strong water: full force from the tuning (200 N) for 1.5 s; weak water: 30 N.
            string[] light = { "PlasticChair", "BeachBall", "Bucket_Red", "TrashBin_Green", "PatioTable", "SunLounger", "Umbrella_Red", "RoadSign_Stop", "Mailbox", "TrafficCone", "Armchair", "DiningChair" };
            foreach (string name in light)
            {
                float moved = Push(all, name, 200f, 1.5f, out float tilt);
                Expect(moved > 0.25f || tilt > 20f, $"Strong water moves {name}: moved {moved:0.00} m, tilted {tilt:0}°");
            }

            float heavyWeak = Push(all, "Sofa", 80f, 1.5f, out float heavyWeakTilt);
            Expect(heavyWeak < 0.1f && heavyWeakTilt < 5f, $"Weak water (80 N) does not move the heavy sofa: {heavyWeak:0.00} m");
            float heavyStrong = Push(all, "Bookshelf", 200f, 2f, out float shelfTilt);
            Expect(heavyStrong > 0.15f || shelfTilt > 15f, $"Strong water moves the heavy bookshelf: {heavyStrong:0.00} m, tilted {shelfTilt:0}°");
            float chairWeak = Push(all, "DiningChair", 15f, 1.5f, out _);
            Expect(chairWeak < 0.05f, $"Water below the chair threshold (15 N) does not move it: {chairWeak:0.00} m");

            float door = PushDoor(all);
            Expect(door > 10f, $"Strong water swings a hinged door: {door:0}°");

            // Several objects at once: every pushable in the pool area for 1 s.
            GameObject poolProps = all.FirstOrDefault(g => g.name == "PoolProps");
            if (poolProps != null)
            {
                WaterPushHandler[] handlers = poolProps.GetComponentsInChildren<WaterPushHandler>();
                Dictionary<WaterPushHandler, Vector3> before = handlers.ToDictionary(h => h, h => h.transform.position);
                Dictionary<WaterPushHandler, Vector3> directions = handlers.ToDictionary(h => h, FreeDirection);
                for (float t = 0f; t < 1f; t += Step)
                {
                    foreach (WaterPushHandler handler in handlers)
                        Hit(handler, directions[handler], 200f);
                    Physics.Simulate(Step);
                }
                int movedCount = handlers.Count(h => (h.transform.position - before[h]).magnitude > 0.05f || Quaternion.Angle(Quaternion.identity, h.transform.rotation) > 5f);
                Info($"Simultaneous hits: {movedCount} of {handlers.Length} pool props reacted");
                Expect(movedCount >= handlers.Length * 0.8f, "Most pool props react when hit together");
            }
        }

        private static float Push(GameObject[] all, string prefabName, float force, float seconds, out float tilt)
        {
            WaterPushHandler handler = FindInstance(all, prefabName)?.GetComponent<WaterPushHandler>();
            tilt = 0f;
            if (handler == null)
            {
                Expect(false, "Instance of " + prefabName + " found");
                return 0f;
            }

            Vector3 start = handler.transform.position;
            Quaternion rotation = handler.transform.rotation;
            Vector3 direction = FreeDirection(handler);
            for (float t = 0f; t < seconds; t += Step)
            {
                Hit(handler, direction, force);
                Physics.Simulate(Step);
            }
            Simulate(1f);
            tilt = Quaternion.Angle(rotation, handler.transform.rotation);
            return Vector3.ProjectOnPlane(handler.transform.position - start, Vector3.up).magnitude;
        }

        private static float PushDoor(GameObject[] all)
        {
            HingeJoint hinge = all.Select(g => g.GetComponent<HingeJoint>()).FirstOrDefault(h => h != null);
            if (hinge == null)
                return 0f;

            WaterPushHandler handler = hinge.GetComponent<WaterPushHandler>();
            Quaternion before = hinge.transform.rotation;
            // Hit the leaf face near its free edge, along its normal.
            Vector3 normal = hinge.transform.forward;
            for (float t = 0f; t < 1.5f; t += Step)
            {
                Vector3 point = hinge.transform.TransformPoint(new Vector3(1.2f, 1.2f, 0f));
                handler.ReceiveImpact(new WaterImpact(point, -normal, normal, 0.2f, 200f));
                Physics.Simulate(Step);
            }
            return Quaternion.Angle(before, hinge.transform.rotation);
        }

        private static void TestGlass(GameObject[] all)
        {
            WaterBreakableGlass[] panes = all.SelectMany(g => g.GetComponents<WaterBreakableGlass>()).ToArray();
            if (panes.Length < 2)
                return;

            WaterBreakableGlass weak = panes[0];
            for (float t = 0f; t < 5f; t += Step)
                weak.ReceiveImpact(GlassImpact(weak, 50f));
            Expect(weak.State == GlassPaneState.Intact, $"Weak water (50 N for 5 s) leaves glass intact: {weak.State}");

            WaterBreakableGlass strong = panes[1];
            GlassPaneState afterCrack = GlassPaneState.Intact;
            for (float t = 0f; t < 3f && strong.State != GlassPaneState.Broken; t += Step)
            {
                strong.ReceiveImpact(GlassImpact(strong, 200f));
                if (strong.State == GlassPaneState.Cracked)
                    afterCrack = GlassPaneState.Cracked;
                Physics.Simulate(Step);
            }
            Expect(afterCrack == GlassPaneState.Cracked, "Strong water cracks the glass first");
            Expect(strong.State == GlassPaneState.Broken, $"Strong water (200 N) breaks the glass within 3 s: {strong.State}");

            var renderer = (Renderer)Field(strong, "_pane");
            Expect(!renderer.enabled && !strong.GetComponent<Collider>().enabled, "Broken pane hides its glass and stops blocking water");
            var breakOff = (GameObject[])Field(strong, "_breakOff");
            Expect(breakOff.Length > 0 && breakOff.All(p => !p.activeSelf), $"Window bars break with the glass ({breakOff.Length} intact part(s) hidden)");

            var shards = (Rigidbody[])Field(strong, "_fragments");
            Vector3[] start = shards.Select(s => s.position).ToArray();
            Simulate(1.5f);
            int active = shards.Count(s => s.gameObject.activeSelf && !s.isKinematic);
            int fallen = shards.Where((s, i) => (s.position - start[i]).magnitude > 0.2f).Count();
            Expect(active == shards.Length, $"All {shards.Length} shards released with physics: {active}");
            Expect(fallen >= shards.Length / 2, $"Shards fly/fall away: {fallen} of {shards.Length} moved");
        }

        private static void TestFireTruck(GameObject[] all)
        {
            FireTruckController truck = all.Select(g => g.GetComponent<FireTruckController>()).FirstOrDefault(t => t != null);
            if (truck == null)
            {
                Expect(false, "Fire truck found");
                return;
            }

            WaterReservoirComponent tank = truck.GetComponent<WaterReservoirComponent>();
            CallAwake(tank);
            CallAwake(truck);
            WaterConnectionPoint outlet = truck.GetComponentInChildren<WaterConnectionPoint>();
            CallAwake(outlet);
            Expect(outlet != null && ReferenceEquals(outlet.Source, truck), "Truck outlet feeds the nozzle hose from the truck");

            float before = truck.Reservoir.Volume;
            truck.Toggle();
            MethodInfo fixedUpdate = typeof(FireTruckController).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 100; i++)
                fixedUpdate.Invoke(truck, null);
            float seconds = 100 * Time.fixedDeltaTime;
            Info($"Truck: tank {before:0} L, after {seconds:0.0} s open {truck.Reservoir.Volume:0.0} L, output {truck.Output.Pressure:0} Pa / {truck.Output.FlowRate:0.0} L/s");
            Expect(truck.Output.Pressure >= 499_000f, "Truck gives full pressure with the valve open");
            Expect(truck.Reservoir.Volume < before, "Truck water comes out of its tank");

            CompartmentDoor[] doors = truck.GetComponentsInChildren<CompartmentDoor>();
            Expect(doors.Length == 4, $"Truck has 4 compartment doors: {doors.Length}");
            if (doors.Length > 0)
            {
                CallAwake(doors[0]);
                var actions = new List<InteractionAction>();
                doors[0].CollectActions(null, actions);
                actions[0].Execute();
                actions.Clear();
                doors[0].CollectActions(null, actions);
                Expect(actions.Count == 1 && actions[0].Label == "Close", "Compartment door opens from the interaction menu");
            }
        }

        // ───────────── Helpers ─────────────

        private static void Hit(WaterPushHandler handler, Vector3 direction, float force)
        {
            Bounds bounds = handler.GetComponent<Collider>().bounds;
            foreach (Collider collider in handler.GetComponents<Collider>())
                bounds.Encapsulate(collider.bounds);
            Vector3 point = bounds.center + Vector3.up * bounds.extents.y * 0.4f - direction * bounds.extents.x;
            handler.ReceiveImpact(new WaterImpact(point, -direction, direction, 0.2f, force));
        }

        /// <summary>Horizontal direction with the most free space around the object (so a wall does not stop the test).</summary>
        private static Vector3 FreeDirection(WaterPushHandler handler)
        {
            Physics.SyncTransforms();
            Collider[] own = handler.GetComponents<Collider>();
            Vector3 center = handler.GetComponent<Collider>().bounds.center;
            Vector3 best = Vector3.right;
            float bestDistance = -1f;
            foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                float distance = 10f;
                foreach (RaycastHit hit in Physics.RaycastAll(center, direction, 10f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!own.Contains(hit.collider))
                        distance = Mathf.Min(distance, hit.distance);
                }
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = direction;
                }
            }
            return best;
        }

        private static WaterImpact GlassImpact(WaterBreakableGlass pane, float force)
        {
            Vector3 inward = pane.transform.forward;
            return new WaterImpact(pane.transform.position, -inward, inward, 0.2f, force);
        }

        private static GameObject FindInstance(GameObject[] all, string prefabName)
        {
            return all.FirstOrDefault(g =>
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(g);
                return source != null && source.name == prefabName && PrefabUtility.IsOutermostPrefabInstanceRoot(g);
            });
        }

        private static void Simulate(float seconds)
        {
            for (float t = 0f; t < seconds; t += Step)
                Physics.Simulate(Step);
        }

        private static void CallAwake(Object component)
        {
            MethodInfo awake = component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            awake?.Invoke(component, null);
        }

        private static object Field(Object component, string name)
        {
            return component.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component);
        }

        private static string Path(Transform t)
        {
            return t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition)
                _failures++;
            Report.AppendLine((condition ? "  PASS  " : "  FAIL  ") + message);
        }

        private static void Info(string message)
        {
            Report.AppendLine("  INFO  " + message);
        }
    }
}
