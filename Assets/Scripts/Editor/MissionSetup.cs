using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.FireTruck;
using WaterChaos.Hose;
using WaterChaos.Hydrant;
using WaterChaos.Interaction;
using WaterChaos.Items;
using WaterChaos.Mission;
using WaterChaos.Player;
using WaterChaos.Pump;
using WaterChaos.Rooms;
using WaterChaos.Soap;
using WaterChaos.Splitter;
using WaterChaos.Teammate;
using WaterChaos.Tools;
using WaterChaos.UI;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One click builds the first mission in the open scene (Mini Village): the "Mission" object with a step per child
    /// (Ukrainian texts, conditions, markers), the mission UI and pause, zones, the hydrant → splitter feed and
    /// a sprayed nozzle for each teammate. Scene objects are found by type and by distance to the fire truck;
    /// every choice is written to the Console, so check it and fix references in the Inspector if the scene differs.
    /// Not run twice: delete the "Mission" and "Mission UI" objects to build again. Save the scene afterwards.
    /// </summary>
    public static class MissionSetup
    {
        private const string NozzlePrefabPath = "Assets/WaterChaos/Prefabs/Nozzle.prefab";

        /// <summary>The burning house in front of the truck (stages 3–5). Change it if the mission moves to another house.</summary>
        private const string HouseName = "House_TwoStorey_Beige";
        private const string HouseRoomName = "Beige House · Room";
        private const float HydrantHoseLength = 80f;

        // Beige house geometry from MiniVillageBuilder (house space: facing -Z, x ±Width/2, z 0..Depth), in meters (m).
        private const float HouseWidth = 10f;
        private const float HouseDepth = 8f;
        private const float HouseFloorTop = 0.15f;
        private const float HouseStoreyHeight = 3f;
        // Room box margin past the outer walls, so wall-mounted air conditioners count as inside, in meters (m).
        private const float HouseRoomMargin = 0.3f;
        // The room starts this far below the ground, so floor-level pivots (fires, fridges) are inside, in meters (m).
        private const float HouseRoomBelowGround = 0.2f;

        // First house fire: hot from the start, a handful of fires and never more.
        private const float HouseStartTemperature = 70f;
        private const int HouseMinFires = 4;
        private const int HouseMaxFires = 8;
        // Fires added to reach the minimum stand at these spots (house space, on the ground floor), in meters (m).
        private static readonly Vector3[] HouseFireSpots =
        {
            new Vector3(-3f, 0f, 2.5f), new Vector3(3f, 0f, 2.5f), new Vector3(-3f, 0f, 6f), new Vector3(3f, 0f, 6f),
            new Vector3(0f, 0f, 4.5f),
        };
        // No fire outdoors around the first house, the truck and the player's start (kept on Rebuild).
        private const string NoOutdoorFireName = "No Outdoor Fire · First Location";

        // A teammate's gasoline flame reaches the house from the street, in meters (m).
        private const float TeammateFlameLength = 10f;

        private const string PumpPrefabPath = "Assets/WaterChaos/Prefabs/Pump.prefab";
        // The same transfer hose as the ones already in the village (real coil mesh and material).
        private const string HosePrefabPath = "Assets/WaterChaos/Prefabs/WaterTransfer/TransferHose_20m 1.prefab";
        // Extra pumps and hoses so no stage has to take equipment back from an earlier one; kept on Rebuild.
        private const string TankPumpName = "Pump · Underground Tank";
        private const string TankHoseName = "Hose · Underground Tank";
        private const string NeutralPumpName = "Pump · Neutralize";
        private const string NeutralHoseName = "Hose · Neutralize";

        // Objects the setup creates (besides "Mission" and "Mission UI"); removed by Rebuild.
        private const string TeammateNozzleName = "Teammate Nozzle";
        private const string FeedName = "Hydrant → Splitter Feed";
        private const string Teammate = "Напарник";
        private const int Idle = 0;
        private const int Extinguish = 2;

        private static readonly List<string> s_report = new List<string>();

        /// <summary>
        /// Deletes what a previous setup created and builds again; keeps markers, the house room,
        /// the extra pumps and hoses and the teammate nozzles (wherever you moved them).
        /// </summary>
        [MenuItem("Water Chaos/Rebuild Mission (Mini Village)")]
        private static void Rebuild()
        {
            Undo.SetCurrentGroupName("Rebuild Mission");
            foreach (Transform t in All<Transform>())
            {
                if (t == null)
                    continue;

                string name = t.name;
                // Teammate nozzles are kept: they were placed in the hands by hand.
                bool created = (t.parent == null && (name == "Mission" || name == "Mission UI")) || name == FeedName;
                if (created)
                    Undo.DestroyObjectImmediate(t.gameObject);
            }

            Build();
        }

        [MenuItem("Water Chaos/Setup Mission (Mini Village)")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<MissionRunner>(FindObjectsInactive.Include) != null)
            {
                Debug.LogError("Mission setup: the scene already has a MissionRunner. Delete \"Mission\" and \"Mission UI\" to build again.");
                return;
            }

            s_report.Clear();
            Undo.SetCurrentGroupName("Setup Mission");
            int undoGroup = Undo.GetCurrentGroup();

            SceneObjects scene = new SceneObjects();
            if (!scene.Find())
                return;

            BuildUi(scene, out ObjectiveListView objectives, out StoryPanelView story, out MissionHintView hints);
            Transform root = new GameObject("Mission").transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Setup Mission");

            MissionRunner runner = Undo.AddComponent<MissionRunner>(root.gameObject);
            Set(runner, "_player", scene.Player);
            Set(runner, "_objectives", objectives);
            Set(runner, "_story", story);
            Set(runner, "_hints", hints);

            EnsureHouseRoom(scene);
            EnsureEquipment(scene);
            Zones zones = BuildZones(scene, root);
            SetUpTeammates(scene);
            BuildSteps(scene, zones, root);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            LinkIntro(runner);
            Selection.activeGameObject = root.gameObject;

            Debug.Log("Mission setup done. Check the choices below, then save the scene (Cmd+S).\n- "
                + string.Join("\n- ", s_report), root);
        }

        /// <summary>
        /// An arrival intro starts the mission itself: it gets the new runner, and the runner waits for it.
        /// </summary>
        private static void LinkIntro(MissionRunner runner)
        {
            MissionIntro intro = Any<MissionIntro>();
            if (intro == null)
                return;

            Set(intro, "_runner", runner);
            Set(runner, "_startOnPlay", false);
            s_report.Add($"Intro {intro.name} starts the mission (runner's Start On Play is off).");
        }

        // ───────────── Scene objects ─────────────

        /// <summary>Everything the mission needs, found in the open scene.</summary>
        private sealed class SceneObjects
        {
            public Transform Player;
            public PlayerInventory Inventory;
            public PlayerHoseConnector Connector;
            public Behaviour[] PlayerInput;

            public FireTruckController Truck;
            public WaterConnectionPoint TruckOutlet;
            public PickableItem Axe;
            public PickableItem Nozzle;
            public BreakableDoor Door;
            public Transform House;
            public Bounds HouseBounds;
            public RoomTemperature HouseRoom;
            public CoolingAppliance[] Coolers;

            public WaterBody ToxicBody;
            public WaterBody UndergroundBody;
            public WaterBody[] PoolBodies;
            public PumpController ToxicPump;
            public PumpController PoolPump;
            public PumpController TankPump;
            public PumpController NeutralPump;
            public TransferHose PoolHose;
            public TransferHose TankHose;
            public TransferHose NeutralHose;
            public CorrodibleObject Grate;
            public SoapItem Soap;

            public HydrantController Hydrant;
            public HoseSplitter Splitter;
            public HoseEndSocket SplitterInlet;
            public SplitterOutlet[] Outlets;
            public Transform Talker;
            public FirefighterAnimationSwitcher[] Sprayers;

            public bool Find()
            {
                PlayerInputHandler input = Any<PlayerInputHandler>();
                Truck = Any<FireTruckController>();
                if (input == null || Truck == null)
                {
                    Debug.LogError("Mission setup: the scene needs the player (PlayerInputHandler) and the fire truck.");
                    return false;
                }

                Player = input.transform;
                Inventory = Any<PlayerInventory>();
                Connector = Any<PlayerHoseConnector>();
                PlayerInput = new Behaviour[] { input, Any<PlayerLook>(), Any<PlayerMovement>() }.Where(b => b != null).ToArray();

                Vector3 origin = Truck.transform.position;
                TruckOutlet = Truck.GetComponentInChildren<WaterConnectionPoint>(true);
                Axe = Nearest(All<AxeTool>().Select(a => a.GetComponentInParent<PickableItem>()), origin);
                Nozzle = Nearest(All<NozzleController>().Select(n => n.GetComponentInParent<PickableItem>()), origin);
                House = All<Transform>().FirstOrDefault(t => t.name == HouseName);
                if (House != null)
                {
                    HouseBounds = RendererBounds(House);
                    Bounds near = HouseBounds;
                    near.Expand(1f);
                    Door = Nearest(All<BreakableDoor>().Where(d => near.Contains(d.transform.position)), origin);
                    HouseRoom = All<RoomTemperature>().FirstOrDefault(r => HouseBounds.Contains(r.transform.position));
                    Coolers = All<CoolingAppliance>().Where(c => near.Contains(c.transform.position)).ToArray();
                }
                else
                {
                    Coolers = new CoolingAppliance[0];
                    s_report.Add($"HOUSE \"{HouseName}\" NOT FOUND: set the door, coolers, zones and room by hand (or change HouseName).");
                }

                WaterBody[] bodies = All<WaterBody>();
                ToxicBody = bodies.FirstOrDefault(b => HasAncestorNamed(b.transform, "toxic"));
                UndergroundBody = bodies.FirstOrDefault(b => HasAncestorNamed(b.transform, "underground"));
                // The pool nearest to the underground tank comes first: the transfer hose must reach from it.
                Vector3 tank = UndergroundBody != null ? UndergroundBody.transform.position : origin;
                PoolBodies = bodies.Where(b => HasAncestorNamed(b.transform, "pool"))
                    .OrderBy(b => Vector3.Distance(b.transform.position, tank)).ToArray();

                // The two pumps already in the scene: one for the toxic tank, one for the pool.
                // The underground-tank and neutralizing pumps (and their hoses) are created by the setup.
                List<PumpController> pumps = All<PumpController>()
                    .Where(p => p.GetComponent<PickableItem>() != null && p.name != TankPumpName && p.name != NeutralPumpName)
                    .ToList();
                ToxicPump = TakeNearest(pumps, ToxicBody);
                PoolPump = TakeNearest(pumps, PoolBodies.FirstOrDefault());
                TankPump = All<PumpController>().FirstOrDefault(p => p.name == TankPumpName);
                NeutralPump = All<PumpController>().FirstOrDefault(p => p.name == NeutralPumpName);

                List<TransferHose> hoses = All<TransferHose>().Where(h => h.name != TankHoseName && h.name != NeutralHoseName).ToList();
                PoolHose = TakeNearest(hoses, PoolPump);
                TankHose = All<TransferHose>().FirstOrDefault(h => h.name == TankHoseName);
                NeutralHose = All<TransferHose>().FirstOrDefault(h => h.name == NeutralHoseName);

                CorrodibleObject[] corrodibles = All<CorrodibleObject>();
                Grate = corrodibles.FirstOrDefault(c => HasAncestorNamed(c.transform, "grate")) ?? corrodibles.FirstOrDefault();
                Soap = Any<SoapItem>();

                Hydrant = Any<HydrantController>();
                Splitter = Any<HoseSplitter>();
                SplitterInlet = Splitter != null ? Splitter.GetComponentInChildren<HoseEndSocket>(true) : null;
                Outlets = Splitter != null ? Splitter.GetComponentsInChildren<SplitterOutlet>(true) : new SplitterOutlet[0];

                TeammateMotor talker = Any<TeammateMotor>();
                Talker = talker != null ? talker.transform : null;
                Sprayers = All<FirefighterAnimationSwitcher>()
                    .Where(s => !s.transform.IsChildOf(Player) && (talker == null || !s.transform.IsChildOf(talker.transform)))
                    .ToArray();

                Report("House", House);
                Report("Breakable door of the house", Door);
                s_report.Add($"Coolers in the house: {Coolers.Length}");
                Report("Toxic tank water", ToxicBody);
                Report("Underground tank water", UndergroundBody);
                Report("Pool for the transfer (nearest to the underground tank)", PoolBodies.FirstOrDefault());
                Report("Pump for the toxic tank", ToxicPump);
                Report("Pump for the pool", PoolPump);
                Report("Pump for the underground tank", TankPump);
                Report("Hose for the pool", PoolHose);
                Report("Grate", Grate);
                Report("Splitter", Splitter);
                Report("Talking teammate", Talker);
                s_report.Add($"Spraying teammates: {Sprayers.Length}");
                return true;
            }
        }

        // ───────────── UI ─────────────

        private static void BuildUi(SceneObjects scene, out ObjectiveListView objectives, out StoryPanelView story, out MissionHintView hints)
        {
            GameObject go = new GameObject("Mission UI");
            Undo.RegisterCreatedObjectUndo(go, "Setup Mission");

            GamePause pause = go.AddComponent<GamePause>();
            SetArray(pause, "_disableWhilePaused", scene.PlayerInput);

            objectives = go.AddComponent<ObjectiveListView>();
            story = go.AddComponent<StoryPanelView>();
            Set(story, "_pause", pause);
            hints = go.AddComponent<MissionHintView>();
            go.AddComponent<MissionMarkerView>();
        }

        // ───────────── Zones ─────────────

        private sealed class Zones
        {
            public MissionZone House;
            public MissionZone HouseFires;
            public MissionZone ToxicTank;
            public MissionZone Pool;
            public MissionZone UndergroundTank;
            public MissionZone Teammates;
            public MissionZone TeammatesView;
            public MissionZone TeammateFires;
        }

        private static Zones BuildZones(SceneObjects scene, Transform root)
        {
            Transform parent = new GameObject("Zones").transform;
            parent.SetParent(root, false);

            Zones zones = new Zones();
            if (scene.HouseRoom != null)
            {
                Bounds room = scene.HouseRoom.GetComponent<BoxCollider>().bounds;
                zones.House = CreateZone("Zone · House (entered)", parent, room);
                room.Expand(new Vector3(2f, 1f, 2f));
                zones.HouseFires = CreateZone("Zone · House Fires", parent, room);
            }

            zones.ToxicTank = BodyZone("Zone · Toxic Tank Hint", parent, scene.ToxicBody, 4f);
            zones.Pool = BodyZone("Zone · Pool Hint", parent, scene.PoolBodies.FirstOrDefault(), 4f);
            zones.UndergroundTank = BodyZone("Zone · Underground Tank Hint", parent, scene.UndergroundBody, 4f);

            List<Vector3> crew = scene.Sprayers.Select(s => s.transform.position).ToList();
            if (scene.Talker != null)
                crew.Add(scene.Talker.position);

            // Close enough to see the flamethrowers; the splitter and the underground tank stay outside it.
            if (crew.Count > 0)
            {
                Bounds view = new Bounds(crew[0], Vector3.zero);
                foreach (Vector3 point in crew)
                    view.Encapsulate(point);
                view.Expand(new Vector3(10f, 6f, 10f));
                zones.TeammatesView = CreateZone("Zone · Teammates View (flamethrowers)", parent, view);
            }

            if (scene.Splitter != null)
                crew.Add(scene.Splitter.transform.position);

            if (crew.Count > 0)
            {
                Bounds area = new Bounds(crew[0], Vector3.zero);
                foreach (Vector3 point in crew)
                    area.Encapsulate(point);

                area.Expand(new Vector3(12f, 6f, 12f));
                zones.Teammates = CreateZone("Zone · Teammates (reached)", parent, area);
                area.Expand(new Vector3(16f, 4f, 16f));
                zones.TeammateFires = CreateZone("Zone · Teammate Fires", parent, area);
            }

            s_report.Add("ZONES are rough boxes: check every \"Zone ·\" object under Mission/Zones and resize it to the real area.");
            return zones;
        }

        private static MissionZone BodyZone(string name, Transform parent, WaterBody body, float margin)
        {
            if (body == null)
                return null;

            Bounds bounds = body.GetComponent<BoxCollider>().bounds;
            bounds.Expand(new Vector3(margin * 2f, 3f, margin * 2f));
            return CreateZone(name, parent, bounds);
        }

        private static MissionZone CreateZone(string name, Transform parent, Bounds bounds)
        {
            GameObject go = new GameObject(name, typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            go.transform.position = bounds.center;
            go.layer = 2;

            BoxCollider box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = bounds.size;
            return go.AddComponent<MissionZone>();
        }

        // ───────────── Teammates ─────────────

        private static void SetUpTeammates(SceneObjects scene)
        {
            if (scene.Hydrant != null)
                Set(scene.Hydrant, "_openOnStart", true);

            if (scene.Hydrant != null && scene.SplitterInlet != null)
                AddHydrantFeed(scene);
            else
            {
                s_report.Add("MISSING hydrant or splitter inlet: the teammates get no water.");
            }

            if (scene.Talker != null)
            {
                FirefighterAnimationSwitcher talkerAnimation = scene.Talker.GetComponentInChildren<FirefighterAnimationSwitcher>(true);
                if (talkerAnimation != null)
                    SetEnum(talkerAnimation, "_animation", Idle);
            }

            GameObject nozzlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NozzlePrefabPath);
            for (int i = 0; i < scene.Sprayers.Length; i++)
            {
                FirefighterAnimationSwitcher sprayer = scene.Sprayers[i];
                SetEnum(sprayer, "_animation", Extinguish);

                if (i >= scene.Outlets.Length)
                {
                    s_report.Add($"NO FREE SPLITTER OUTLET for {sprayer.name}.");
                    continue;
                }

                if (nozzlePrefab == null)
                {
                    s_report.Add($"MISSING {NozzlePrefabPath}: no nozzle for {sprayer.name}.");
                    continue;
                }

                TeammateNozzle existing = sprayer.GetComponentInChildren<TeammateNozzle>(true);
                GameObject nozzle = existing != null ? existing.gameObject : CreateTeammateNozzle(sprayer.transform, nozzlePrefab);
                ConfigureTeammateNozzle(nozzle, sprayer, scene.Outlets[i], existing == null);
            }
        }

        /// <summary>
        /// The tee is fed by the hydrant from the start: a fixed hose from the hydrant outlet to the splitter inlet,
        /// drawn with the nozzle hose look (the hose part of the nozzle prefab, its end on the inlet).
        /// </summary>
        private static void AddHydrantFeed(SceneObjects scene)
        {
            WaterConnectionPoint outlet = scene.Hydrant.GetComponentInChildren<WaterConnectionPoint>(true);
            if (outlet == null)
            {
                GameObject point = new GameObject("Hydrant Outlet");
                Undo.RegisterCreatedObjectUndo(point, "Setup Mission");
                point.transform.SetParent(scene.Hydrant.transform, false);
                point.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                outlet = point.AddComponent<WaterConnectionPoint>();
                s_report.Add("Hydrant had no WaterConnectionPoint: added \"Hydrant Outlet\"; move it onto the hydrant nozzle.");
            }

            GameObject feed = new GameObject(FeedName);
            Undo.RegisterCreatedObjectUndo(feed, "Setup Mission");
            feed.transform.SetParent(scene.Hydrant.transform, false);
            FixedHoseFeed component = feed.AddComponent<FixedHoseFeed>();
            Set(component, "_from", outlet);
            Set(component, "_socket", scene.SplitterInlet);

            HoseController hose = CreateHoseFromNozzlePrefab(feed.transform, scene.SplitterInlet.Anchor);
            Set(component, "_hose", hose);
            s_report.Add(hose != null
                ? $"Tee fed by the hydrant: hose {outlet.name} → {scene.SplitterInlet.name}. Put the splitter near the teammates."
                : "Tee fed by the hydrant, but the hose look could not be made from the nozzle prefab.");
        }

        /// <summary>The hose part of the nozzle prefab on its own, its end at <paramref name="end"/>.</summary>
        private static HoseController CreateHoseFromNozzlePrefab(Transform parent, Transform end)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NozzlePrefabPath);
            if (prefab == null)
                return null;

            GameObject nozzle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            PrefabUtility.UnpackPrefabInstance(nozzle, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            HoseController hose = nozzle.GetComponentInChildren<HoseController>(true);
            if (hose == null || hose.gameObject == nozzle)
            {
                Object.DestroyImmediate(nozzle);
                return null;
            }

            hose.transform.SetParent(parent, true);
            hose.gameObject.name = "Hydrant Hose";
            Object.DestroyImmediate(nozzle);

            Set(hose, "_endPoint", end);
            Set(hose, "_maxLength", HydrantHoseLength);
            return hose;
        }

        /// <summary>
        /// The house needs its own temperature room, so coolers there lower the heat and slow the fire:
        /// a copy of an existing room (same layer and settings) fitted to the house.
        /// </summary>
        private static void EnsureHouseRoom(SceneObjects scene)
        {
            if (scene.House == null)
                return;

            if (scene.HouseRoom == null)
            {
                RoomTemperature template = All<RoomTemperature>().FirstOrDefault();
                if (template == null)
                {
                    s_report.Add("NO RoomTemperature in the scene to copy: the house has no temperature (run Water Chaos/Setup Room Temperature).");
                    return;
                }

                GameObject copy = Object.Instantiate(template.gameObject, template.transform.parent);
                Undo.RegisterCreatedObjectUndo(copy, "Setup Mission");
                copy.name = HouseRoomName;
                scene.HouseRoom = copy.GetComponent<RoomTemperature>();
                Set(scene.HouseRoom, "_displayName", "Будинок");
            }

            if (scene.HouseRoom.name != HouseRoomName)
            {
                Report("House temperature room (kept as it is)", scene.HouseRoom);
                return;
            }

            ConfigureFirstHouse(scene.House, scene.HouseRoom, scene.Truck.transform, scene.Player);
        }

        /// <summary>Updates the first house (room, temperature, fires, no outdoor fire) without rebuilding the mission.</summary>
        [MenuItem("Water Chaos/Update First House (Mini Village)")]
        private static void UpdateFirstHouse()
        {
            s_report.Clear();
            Transform house = All<Transform>().FirstOrDefault(t => t.name == HouseName);
            RoomTemperature room = All<RoomTemperature>().FirstOrDefault(r => r.name == HouseRoomName);
            FireTruckController truck = Any<FireTruckController>();
            PlayerInputHandler player = Any<PlayerInputHandler>();
            if (house == null || room == null || truck == null || player == null)
            {
                Debug.LogError($"Update first house: \"{HouseName}\", \"{HouseRoomName}\", the truck or the player not found; run Setup Mission first.");
                return;
            }

            Undo.SetCurrentGroupName("Update First House");
            ConfigureFirstHouse(house, room, truck.transform, player.transform);
            EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            Debug.Log("Update first house done. Save the scene (Cmd+S).\n- " + string.Join("\n- ", s_report), room);
        }

        private static void ConfigureFirstHouse(Transform house, RoomTemperature room, Transform truck, Transform player)
        {
            FitHouseRoom(house, room);
            Set(room, "_startTemperature", HouseStartTemperature);
            Set(room, "_maxFires", (float)HouseMaxFires);
            EnsureHouseFires(house, room);
            EnsureNoOutdoorFire(house, truck, player);
            s_report.Add($"{HouseRoomName}: ground floor only, starts at {HouseStartTemperature} °C, at most {HouseMaxFires} fires.");
        }

        /// <summary>At least <see cref="HouseMinFires"/> fires burn in the house at the start: copies of a house fire are added.</summary>
        private static void EnsureHouseFires(Transform house, RoomTemperature room)
        {
            BoxCollider box = room.GetComponent<BoxCollider>();
            List<FireExtinguishHandler> fires = All<FireExtinguishHandler>()
                .Where(f => f.gameObject.scene.IsValid() && box.bounds.Contains(f.transform.position)).ToList();

            FireExtinguishHandler template = fires.FirstOrDefault();
            if (template == null)
            {
                s_report.Add("NO FIRE in the first house to copy: put at least one Cartoon Fire inside it.");
                return;
            }

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(template.gameObject);
            foreach (Vector3 spot in HouseFireSpots)
            {
                if (fires.Count >= HouseMinFires)
                    break;

                Vector3 position = house.TransformPoint(spot + Vector3.up * HouseFloorTop);
                if (fires.Any(f => Vector3.Distance(f.transform.position, position) < 2f))
                    continue;

                GameObject copy = source != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(source, template.transform.parent)
                    : Object.Instantiate(template.gameObject, template.transform.parent);
                Undo.RegisterCreatedObjectUndo(copy, "Update First House");
                copy.name = $"Cartoon Fire (House {fires.Count + 1})";
                copy.transform.SetPositionAndRotation(position, template.transform.rotation);
                fires.Add(copy.GetComponent<FireExtinguishHandler>());
                s_report.Add($"Added {copy.name} in the house; move it if it stands inside furniture.");
            }

            s_report.Add($"Fires in the first house at the start: {fires.Count}.");
        }

        private static void EnsureNoOutdoorFire(Transform house, Transform truck, Transform player)
        {
            if (All<NoOutdoorFireZone>().Any(z => z.name == NoOutdoorFireName))
            {
                s_report.Add($"\"{NoOutdoorFireName}\" kept as it is.");
                return;
            }

            Bounds area = RendererBounds(house);
            area.Encapsulate(truck.position);
            area.Encapsulate(player.position);
            area.Expand(new Vector3(12f, 20f, 12f));

            GameObject go = new GameObject(NoOutdoorFireName, typeof(BoxCollider));
            Undo.RegisterCreatedObjectUndo(go, "Update First House");
            go.layer = 2;
            go.transform.position = area.center;
            BoxCollider box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = area.size;
            go.AddComponent<NoOutdoorFireZone>();
            s_report.Add($"\"{NoOutdoorFireName}\" created around the first house, the truck and the start: no fire outdoors there. "
                + "Resize it if it reaches the teammates' house.");
        }

        /// <summary>
        /// Ground floor only, from just below the ground (fires and fridges stand on the floor, their pivots at floor level)
        /// up to below the upper floor: a fire spreads only inside its own room, so it never climbs upstairs or into the yard.
        /// </summary>
        private static void FitHouseRoom(Transform house, RoomTemperature room)
        {
            const float bottom = -HouseRoomBelowGround;
            float top = HouseFloorTop + HouseStoreyHeight - 0.15f;

            Transform transform = room.transform;
            Undo.RecordObject(transform, "Fit House Room");
            transform.SetPositionAndRotation(
                house.TransformPoint(new Vector3(0f, (bottom + top) * 0.5f, HouseDepth * 0.5f)), house.rotation);
            transform.localScale = Vector3.one;

            BoxCollider box = room.GetComponent<BoxCollider>();
            Undo.RecordObject(box, "Fit House Room");
            box.center = Vector3.zero;
            box.size = new Vector3(HouseWidth + 2f * HouseRoomMargin, top - bottom, HouseDepth + 2f * HouseRoomMargin);
        }

        /// <summary>
        /// A pump and a hose for the underground tank (stage 8) and for neutralizing (stage 9),
        /// placed next to where they are used; existing ones (by name) are kept where you put them.
        /// Every hose gets a length that reaches its target.
        /// </summary>
        private static void EnsureEquipment(SceneObjects scene)
        {
            Transform parent = scene.PoolPump != null ? scene.PoolPump.transform.parent : null;
            Vector3 tank = scene.UndergroundBody != null ? scene.UndergroundBody.transform.position : scene.Truck.transform.position;
            Vector3 toxic = scene.ToxicBody != null ? scene.ToxicBody.transform.position : tank;

            if (scene.TankPump == null)
                scene.TankPump = Spawn<PumpController>(PumpPrefabPath, TankPumpName, parent, tank + new Vector3(-4f, 0f, 3f));
            scene.TankHose = ReplaceIfOtherPrefab(scene.TankHose, HosePrefabPath);
            scene.NeutralHose = ReplaceIfOtherPrefab(scene.NeutralHose, HosePrefabPath);
            if (scene.TankHose == null)
                scene.TankHose = Spawn<TransferHose>(HosePrefabPath, TankHoseName, parent, tank + new Vector3(-5f, 0f, 4.5f));
            if (scene.NeutralPump == null)
                scene.NeutralPump = Spawn<PumpController>(PumpPrefabPath, NeutralPumpName, parent, toxic + new Vector3(6f, 0f, 3f));
            if (scene.NeutralHose == null)
                scene.NeutralHose = Spawn<TransferHose>(HosePrefabPath, NeutralHoseName, parent, toxic + new Vector3(7f, 0f, 4.5f));

            Vector3 pool = scene.PoolBodies.Length > 0 ? scene.PoolBodies[0].transform.position : tank;
            Vector3 splitter = scene.Splitter != null ? scene.Splitter.transform.position : tank;
            FitHoseLength(scene.PoolHose, pool, tank);
            FitHoseLength(scene.TankHose, tank, splitter);
            FitHoseLength(scene.NeutralHose, toxic, tank);

            Report("Pump for the underground tank", scene.TankPump);
            Report("Pump for neutralizing (toxic tank → gasoline tank)", scene.NeutralPump);
            s_report.Add($"NEW EQUIPMENT: \"{TankPumpName}\", \"{TankHoseName}\" next to the underground tank and "
                + $"\"{NeutralPumpName}\", \"{NeutralHoseName}\" next to the toxic tank — move them where you want; Rebuild keeps them.");
        }

        /// <summary>An object made from another prefab is swapped for one of <paramref name="prefabPath"/> at the same place.</summary>
        private static T ReplaceIfOtherPrefab<T>(T current, string prefabPath) where T : Component
        {
            if (current == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(current.gameObject) == prefabPath)
                return current;

            Transform old = current.transform;
            T replacement = Spawn<T>(prefabPath, old.name, old.parent, old.position);
            if (replacement != null)
            {
                replacement.transform.SetPositionAndRotation(old.position, old.rotation);
                s_report.Add($"{old.name}: replaced with {System.IO.Path.GetFileNameWithoutExtension(prefabPath)} (same look as the other hoses).");
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            return replacement;
        }

        private static T Spawn<T>(string prefabPath, string name, Transform parent, Vector3 near) where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                s_report.Add($"MISSING {prefabPath}: {name} not created.");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Setup Mission");
            instance.name = name;
            instance.transform.position = OnGround(near);
            return instance.GetComponent<T>();
        }

        /// <summary>The first solid surface under the point (triggers such as water are skipped), slightly above it.</summary>
        private static Vector3 OnGround(Vector3 point)
        {
            Physics.SyncTransforms();
            Vector3 top = new Vector3(point.x, point.y + 30f, point.z);
            return Physics.Raycast(top, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point + Vector3.up * 0.3f
                : new Vector3(point.x, 0.3f, point.z);
        }

        /// <summary>Long enough for the straight distance plus some slack for going around things.</summary>
        private static void FitHoseLength(TransferHose hose, Vector3 from, Vector3 to)
        {
            if (hose == null)
                return;

            float needed = Mathf.Ceil((Vector3.Distance(from, to) * 1.4f + 5f) / 5f) * 5f;
            SerializedObject data = new SerializedObject(hose);
            SerializedProperty length = data.FindProperty("_maxLength");
            if (length != null && length.floatValue < needed)
            {
                length.floatValue = needed;
                data.ApplyModifiedProperties();
                s_report.Add($"{hose.name}: length set to {needed} m to reach its target.");
            }
        }

        /// <summary>
        /// A copy of the nozzle prefab in front of the teammate, without its pickup parts (the player must not take it),
        /// fed from a splitter outlet. Its position and rotation need fixing by hand to fit the hands.
        /// </summary>
        private static GameObject CreateTeammateNozzle(Transform teammate, GameObject prefab)
        {
            GameObject nozzle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, teammate.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(nozzle, "Setup Mission");
            PrefabUtility.UnpackPrefabInstance(nozzle, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            nozzle.name = TeammateNozzleName;
            nozzle.transform.SetParent(teammate, false);
            nozzle.transform.localPosition = new Vector3(0.25f, 1.2f, 0.55f);
            nozzle.transform.localRotation = Quaternion.identity;

            foreach (NozzleController controller in nozzle.GetComponentsInChildren<NozzleController>(true))
                Object.DestroyImmediate(controller);
            foreach (PickableItem item in nozzle.GetComponentsInChildren<PickableItem>(true))
                Object.DestroyImmediate(item);
            foreach (Rigidbody body in nozzle.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(body);
            foreach (Collider part in nozzle.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(part);

            nozzle.AddComponent<TeammateNozzle>();
            return nozzle;
        }

        /// <summary>
        /// Wires the nozzle to its splitter outlet, makes its gasoline flame light by itself and reach the house,
        /// and parents it to the teammate's right hand (keeping where it is), so it follows the animation.
        /// </summary>
        private static void ConfigureTeammateNozzle(GameObject nozzle, FirefighterAnimationSwitcher teammate, SplitterOutlet outlet, bool created)
        {
            TeammateNozzle component = nozzle.GetComponent<TeammateNozzle>();
            Set(component, "_stream", nozzle.GetComponentInChildren<WaterStream>(true));
            Set(component, "_outlet", outlet);
            Set(component, "_hose", nozzle.GetComponentInChildren<HoseController>(true));

            foreach (FuelStreamIgnition ignition in nozzle.GetComponentsInChildren<FuelStreamIgnition>(true))
            {
                Set(ignition, "_selfIgniting", true);
                Set(ignition, "_flameLength", TeammateFlameLength);
            }

            Transform hand = FindRightHand(teammate.GetComponent<Animator>());
            if (hand != null && nozzle.transform.parent != hand)
            {
                Undo.SetTransformParent(nozzle.transform, hand, "Setup Mission");
                s_report.Add($"{teammate.name}: nozzle attached to the hand bone \"{hand.name}\" — it moves with the animation. "
                    + "Check its offset in Play Mode (the pose differs from the Editor pose).");
            }
            else if (hand == null)
            {
                s_report.Add($"{teammate.name}: NO HAND BONE found; drag \"{TeammateNozzleName}\" onto the right-hand bone yourself.");
            }

            if (created)
                s_report.Add($"Nozzle for {teammate.name} on outlet {outlet.name}: move it into the hands and aim it at the house.");
        }

        /// <summary>The right-hand bone: from the humanoid avatar, otherwise the child named like a right hand.</summary>
        private static Transform FindRightHand(Animator animator)
        {
            if (animator == null)
                return null;

            if (animator.isHuman)
            {
                Transform bone = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (bone != null)
                    return bone;
            }

            foreach (Transform child in animator.GetComponentsInChildren<Transform>(true))
            {
                string name = child.name.ToLowerInvariant().Replace(" ", string.Empty);
                bool isHand = name.Contains("hand") && !name.Contains("finger") && !name.Contains("thumb") && !name.Contains("index");
                bool isRight = name.Contains("right") || name.EndsWith("_r") || name.EndsWith(".r") || name.EndsWith("r_") || name.StartsWith("r_");
                if (isHand && isRight)
                    return child;
            }

            return null;
        }

        // ───────────── Steps ─────────────

        private static void BuildSteps(SceneObjects s, Zones z, Transform root)
        {
            MissionMarker truckOutlet = Marker(s.TruckOutlet, 0.6f);
            MissionMarker door = Marker(s.Door, 2.3f);
            MissionMarker toxicTank = Marker(s.ToxicBody, 2f);
            MissionMarker underground = Marker(s.UndergroundBody, 1.5f);
            MissionMarker[] pools = s.PoolBodies.Take(1).Select(b => Marker(b, 1.5f)).ToArray();
            MissionMarker grate = Marker(s.Grate, 1f);
            MissionMarker splitter = Marker(s.Splitter, 0.8f);
            MissionMarker talker = Marker(s.Talker, 2.3f);
            MissionMarker soap = Marker(s.Soap, 0.6f);
            MissionMarker poolPump = Marker(s.PoolPump, 1f);
            MissionMarker toxicPump = Marker(s.ToxicPump, 1f);
            MissionMarker tankPump = Marker(s.TankPump, 1f);
            MissionMarker neutralPump = Marker(s.NeutralPump, 1f);
            Marker(s.NeutralHose, 0.8f);
            Marker(s.TankHose, 0.8f);
            Marker(s.Axe, 0.8f);
            Marker(s.Nozzle, 0.8f);
            Marker(s.PoolHose, 0.8f);
            foreach (CoolingAppliance cooler in s.Coolers)
                Marker(cooler, 1.2f);

            int n = 0;
            string Name(string title) => $"{++n:00} · {title}";

            // Stage 1: arrival.
            Story(root, Name("Story · Arrival"), null,
                "Ти приїхав на виклик, щоб допомогти своїм напарникам. Але, схоже, ти заїхав не з того боку — проїзду далі немає.\n\n"
                + "Поки що давай загасимо будинок перед тобою, а потім подумаємо, як дістатися до твоїх друзів.");

            // Stage 2: equipment.
            PickUp(root, Name("Pick Up · Axe and Nozzle"), "Знайди сокиру та пожежний ствол", s.Inventory, s.Axe, s.Nozzle);
            // Stage 3: the door.
            BreakDoorObjective breakDoor = Add<BreakDoorObjective>(root, Name("Break · Door"),
                "Розбий двері сокирою, щоб потрапити всередину будинку.", door);
            Set(breakDoor, "_door", s.Door);

            // Then the nozzle on the truck.
            NozzleConnectObjective connectTruck = Add<NozzleConnectObjective>(root, Name("Connect · Nozzle to Truck"),
                "Підключи пожежний ствол до машини", truckOutlet);
            Set(connectTruck, "_connector", s.Connector);
            SetArray(connectTruck, "_points", s.TruckOutlet);
            Hint(connectTruck, "Підключи ствол до машини", PlayerControl.Interact, null);

            // Stage 4: inside the house.
            EnterZoneObjective enterHouse = Add<EnterZoneObjective>(root, Name("Zone · Entered House"), null);
            Set(enterHouse, "_zone", z.House);
            Story(root, Name("Story · Heat"), null,
                "Температура всередині будинку критично висока! Через це вогонь поширюється набагато швидше.\n\n"
                + "Знайди всі кондиціонери та холодильники й увімкни їх. Це допоможе знизити температуру та сповільнити поширення вогню.");

            // Stages 4–5: cooling while fighting the fire; the fire decides when the stage ends (90 s at most).
            Transform group = Add<StepGroup>(root, Name("Group · Cool and Extinguish House"), null).transform;
            CoolingObjective cooling = Add<CoolingObjective>(group, "Cooling · House", "Увімкни всі кондиціонери та холодильники");
            Set(cooling, "_optional", true);
            SetArray(cooling, "_appliances", s.Coolers);
            ExtinguishFiresObjective houseFires = Add<ExtinguishFiresObjective>(group, "Extinguish · House",
                "Загаси всі осередки пожежі в будинку.");
            SetArray(houseFires, "_zones", z.HouseFires);
            Set(houseFires, "_timeLimit", 90f);
            SetArray(houseFires, "_settleRooms", s.HouseRoom);

            DrainReservoirStep drain = Add<DrainReservoirStep>(root, Name("Event · Truck Tank Empty"), null);
            Set(drain, "_tank", s.Truck.GetComponent<WaterReservoirComponent>());

            // Stage 6: toxic water.
            Story(root, Name("Story · Fence"), null,
                "Чудово, з будинком розібралися. Але як тепер дістатися до напарників?\n\n"
                + "Потрібно якось подолати цю металеву огорожу. Поки я їхав сюди, помітив неподалік завод. "
                + "Можливо, там знайдеться щось, що допоможе розчинити метал.");
            PickUp(root, Name("Pick Up · Pump (Toxic)"), "Знайди насос", s.Inventory, Item(s.ToxicPump));
            PumpInWaterObjective toxicIn = Add<PumpInWaterObjective>(root, Name("Pump · Into Toxic Tank"),
                "Закинь насос у резервуар із токсичною водою", toxicTank);
            SetArray(toxicIn, "_pumps", Deployments(s.ToxicPump, s.PoolPump, s.TankPump));
            SetArray(toxicIn, "_bodies", s.ToxicBody);
            Hint(toxicIn, "Закинь насос у резервуар із токсичною водою.", PlayerControl.Drop, z.ToxicTank);
            NozzleConnectObjective connectToxic = Add<NozzleConnectObjective>(root, Name("Connect · Nozzle to Toxic Pump"),
                "Підключи пожежний ствол до насоса", toxicPump);
            Set(connectToxic, "_connector", s.Connector);
            SetArray(connectToxic, "_points", Points(s.ToxicPump, s.PoolPump, s.TankPump));
            Hint(connectToxic, "Підключи пожежний ствол до насоса.", PlayerControl.Interact, z.ToxicTank);
            CorrodeObjective corrode = Add<CorrodeObjective>(root, Name("Corrode · Grate"),
                "Розчини металеву решітку токсичною водою", grate);
            Set(corrode, "_target", s.Grate);

            // Stage 7: the teammates run dry.
            EnterZoneObjective reach = Add<EnterZoneObjective>(root, Name("Zone · Reached Teammates"), "Дістанься до напарників", talker);
            Set(reach, "_zone", z.Teammates);
            HydrantFailureStep failure = Add<HydrantFailureStep>(root, Name("Event · Hydrant Runs Dry"), null);
            Set(failure, "_hydrant", s.Hydrant);
            Set(failure, "_releaseFeed", s.Hydrant != null ? s.Hydrant.GetComponentInChildren<FixedHoseFeed>(true) : null);
            Story(root, Name("Story · Out of Water"), Teammate, "Ти вчасно! У нас майже закінчилася вода! Треба терміново знайти нове джерело!");

            PickUp(root, Name("Pick Up · Pump and Hose (Pool)"), "Знайди насос та шланг.", s.Inventory, Item(s.PoolPump), Item(s.PoolHose));
            PumpInWaterObjective poolIn = Add<PumpInWaterObjective>(root, Name("Pump · Into Pool"), "Встанови насос у басейн", pools);
            SetArray(poolIn, "_pumps", Deployments(s.PoolPump, s.ToxicPump, s.TankPump));
            SetArray(poolIn, "_bodies", s.PoolBodies.Take(1).ToArray());
            Hint(poolIn, "Встанови насос у басейн.", PlayerControl.Drop, z.Pool);
            TransferHoseObjective poolHose = Add<TransferHoseObjective>(root, Name("Hose · Pool to Underground Tank"),
                "Протягни шланг від насоса до підземного резервуара", underground);
            SetArray(poolHose, "_hoses", s.PoolHose, s.TankHose);
            SetArray(poolHose, "_endBodies", s.UndergroundBody);
            SetArray(poolHose, "_endSockets", s.SplitterInlet);
            Hint(poolHose, "Підключи шланг до насоса та протягни його до підземного резервуара або перехідника.", null, null);
            PumpRunningObjective poolOn = Add<PumpRunningObjective>(root, Name("Pump · Start Pool Pump"), "Увімкни насос біля басейну", poolPump);
            SetArray(poolOn, "_pumps", s.PoolPump, s.ToxicPump, s.TankPump);
            Hint(poolOn, "Шланг на місці! Тепер увімкни насос біля басейну.", null, null);

            // Stage 8: the second pump and the tee.
            PickUp(root, Name("Pick Up · Pump and Hose (Tank)"), "Знайди ще один насос та шланг", s.Inventory,
                Item(s.TankPump), Item(s.TankHose));
            PumpInWaterObjective tankIn = Add<PumpInWaterObjective>(root, Name("Pump · Into Underground Tank"),
                "Помісти насос у підземний резервуар", underground);
            SetArray(tankIn, "_pumps", Deployments(s.TankPump, s.ToxicPump, s.PoolPump));
            SetArray(tankIn, "_bodies", s.UndergroundBody);
            Hint(tankIn, "Помісти насос у підземний резервуар.", PlayerControl.Drop, z.UndergroundTank);
            TransferHoseObjective tankHose = Add<TransferHoseObjective>(root, Name("Hose · Onto Tank Pump"),
                "Підключи шланг до насоса", tankPump);
            SetArray(tankHose, "_hoses", s.TankHose, s.PoolHose);
            SetArray(tankHose, "_startPoints", TransferPoint(s.TankPump));
            TransferHoseObjective tee = Add<TransferHoseObjective>(root, Name("Hose · Into Tee"),
                "Знайди трійник та підключи до нього шланг.", splitter);
            SetArray(tee, "_hoses", s.TankHose, s.PoolHose);
            SetArray(tee, "_startPoints", TransferPoint(s.TankPump));
            SetArray(tee, "_endSockets", s.SplitterInlet);
            PumpRunningObjective tankOn = Add<PumpRunningObjective>(root, Name("Pump · Start Tank Pump"),
                "Увімкни насос у підземному резервуарі", tankPump);
            SetArray(tankOn, "_pumps", s.TankPump);

            // Stage 9: gasoline instead of water (the tank holds gasoline; the flame streams light by themselves).
            // The player sees the flamethrowers first: the story waits until they are back with the teammates.
            EnterZoneObjective back = Add<EnterZoneObjective>(root, Name("Zone · Back to Teammates"), "Повернись до напарників", talker);
            Set(back, "_zone", z.TeammatesView);
            Story(root, Name("Story · Gasoline"), Teammate, "Ти що наробив?! Це ж не вода! У нас у шлангах бензин!");

            // Neutralize with its own pump and long hose: toxic tank → gasoline tank, plus soap.
            PickUp(root, Name("Pick Up · Pump and Hose (Neutralize)"), "Знайди спосіб нейтралізувати бензин: візьми насос і довгий шланг",
                s.Inventory, Item(s.NeutralPump), Item(s.NeutralHose));
            PumpInWaterObjective neutralIn = Add<PumpInWaterObjective>(root, Name("Pump · Into Toxic Tank (Neutralize)"),
                "Встанови насос у резервуар із токсичною водою", toxicTank);
            SetArray(neutralIn, "_pumps", Deployments(s.NeutralPump, s.ToxicPump));
            SetArray(neutralIn, "_bodies", s.ToxicBody);
            Hint(neutralIn, "Встанови насос у резервуар із токсичною водою.", PlayerControl.Drop, z.ToxicTank);
            TransferHoseObjective neutralHose = Add<TransferHoseObjective>(root, Name("Hose · Toxic to Gasoline Tank"),
                "Протягни шланг до резервуара з бензином", underground);
            SetArray(neutralHose, "_hoses", s.NeutralHose, s.PoolHose);
            SetArray(neutralHose, "_startPoints", TransferPoint(s.NeutralPump), TransferPoint(s.ToxicPump));
            SetArray(neutralHose, "_endBodies", s.UndergroundBody);
            NeutralizeObjective neutralize = Add<NeutralizeObjective>(root, Name("Neutralize · Gasoline"),
                "Нейтралізуй бензин", underground);
            Set(neutralize, "_tank", s.UndergroundBody != null ? s.UndergroundBody.GetComponent<WaterReservoirComponent>() : null);
            Set(neutralize, "_soap", s.Soap);
            SetArray(neutralize, "_stopPumps", s.NeutralPump, s.ToxicPump);
            Set(neutralize, "_toxinMarker", neutralPump);
            Set(neutralize, "_soapMarker", soap);
            Hint(neutralize, "Увімкни насос, щоб накачати токсичну воду в резервуар із бензином, і кинь туди мило.", null, null);

            // Stage 10: the final fire.
            ExtinguishFiresObjective finalFires = Add<ExtinguishFiresObjective>(root, Name("Extinguish · Teammates"),
                "Загаси пожежу та допоможи напарникам!");
            SetArray(finalFires, "_zones", z.TeammateFires);
            Story(root, Name("Story · Finale"), Teammate, "Молодець! Ти допоміг напарникам, але, здається, тепер у вас є нові проблеми…");

            // Stage 9 needs gasoline in the underground tank: whatever is pumped from it reaches the teammates.
            if (s.UndergroundBody != null)
            {
                WaterReservoirComponent tank = s.UndergroundBody.GetComponent<WaterReservoirComponent>();
                SetEnum(tank, "_liquid", (int)LiquidType.Gasoline);
                s_report.Add($"Underground tank ({tank.name}) now starts with GASOLINE.");
            }
        }

        // ───────────── Step helpers ─────────────

        private static T Add<T>(Transform parent, string name, string title, params MissionMarker[] markers) where T : MissionStep
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            T step = go.AddComponent<T>();

            if (step is ObjectiveStep)
            {
                if (title != null)
                    Set(step, "_title", title);
                SetArray(step, "_markers", markers.Where(m => m != null).ToArray());
            }

            return step;
        }

        private static void Story(Transform parent, string name, string speaker, string text)
        {
            StoryStep step = Add<StoryStep>(parent, name, null);
            Set(step, "_speaker", speaker ?? string.Empty);
            Set(step, "_text", text);
        }

        private static void PickUp(Transform parent, string name, string title, PlayerInventory inventory, params PickableItem[] items)
        {
            PickUpObjective step = Add<PickUpObjective>(parent, name, title);
            Set(step, "_inventory", inventory);
            SetArray(step, "_items", items.Where(i => i != null).ToArray());
        }

        private static void Hint(ObjectiveStep step, string text, PlayerControl? key, MissionZone zone)
        {
            Set(step, "_hint", text);
            Set(step, "_showHintKey", key.HasValue);
            if (key.HasValue)
                SetEnum(step, "_hintKey", (int)key.Value);
            Set(step, "_hintZone", zone);
        }

        private static MissionMarker Marker(Component target, float height)
        {
            if (target == null)
                return null;

            MissionMarker marker = target.GetComponent<MissionMarker>();
            if (marker == null)
            {
                marker = Undo.AddComponent<MissionMarker>(target.gameObject);
                Set(marker, "_height", height);
            }

            return marker;
        }

        private static PickableItem Item(Component component) => component != null ? component.GetComponent<PickableItem>() : null;

        private static PumpDeployment[] Deployments(params PumpController[] pumps) =>
            pumps.Where(p => p != null).Distinct().Select(p => p.GetComponent<PumpDeployment>()).Where(d => d != null).ToArray();

        private static WaterConnectionPoint[] Points(params PumpController[] pumps) =>
            pumps.Where(p => p != null).Distinct().Select(p => p.GetComponentInChildren<WaterConnectionPoint>(true)).Where(c => c != null).ToArray();

        private static TransferConnectionPoint TransferPoint(PumpController pump) =>
            pump != null ? pump.GetComponentInChildren<TransferConnectionPoint>(true) : null;

        // ───────────── Search helpers ─────────────

        private static T Any<T>() where T : Object => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);

        private static T[] All<T>() where T : Object =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include);

        private static T Nearest<T>(IEnumerable<T> candidates, Vector3 point) where T : Component =>
            candidates.Where(c => c != null).OrderBy(c => Vector3.Distance(c.transform.position, point)).FirstOrDefault();

        private static T TakeNearest<T>(List<T> candidates, Component target) where T : Component
        {
            if (candidates.Count == 0)
                return null;

            T nearest = target != null ? Nearest(candidates, target.transform.position) : candidates[0];
            candidates.Remove(nearest);
            return nearest;
        }

        private static bool HasAncestorNamed(Transform transform, string part)
        {
            for (Transform t = transform; t != null; t = t.parent)
            {
                if (t.name.ToLowerInvariant().Contains(part))
                    return true;
            }

            return false;
        }

        private static Bounds RendererBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.position, Vector3.one * 10f);

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer part in renderers)
                bounds.Encapsulate(part.bounds);
            return bounds;
        }

        private static void Report(string label, Object found)
        {
            s_report.Add(found != null ? $"{label}: {found.name}" : $"{label}: NOT FOUND — set it by hand");
        }

        // ───────────── Serialized fields ─────────────

        private static void Set(Object target, string field, object value)
        {
            SerializedObject data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"Mission setup: {target.GetType().Name} has no field {field}.", target);
                return;
            }

            switch (value)
            {
                case string text: property.stringValue = text; break;
                case float number:
                    if (property.propertyType == SerializedPropertyType.Integer)
                        property.intValue = Mathf.RoundToInt(number);
                    else
                        property.floatValue = number;
                    break;
                case bool flag: property.boolValue = flag; break;
                case Object reference: property.objectReferenceValue = reference; break;
                case null: property.objectReferenceValue = null; break;
            }

            data.ApplyModifiedProperties();
        }

        private static void SetEnum(Object target, string field, int index)
        {
            SerializedObject data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(field);
            if (property == null)
                return;

            Undo.RecordObject(target, "Setup Mission");
            property.enumValueIndex = index;
            data.ApplyModifiedProperties();
        }

        private static void SetArray(Object target, string field, params Object[] values)
        {
            SerializedObject data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(field);
            if (property == null || !property.isArray)
            {
                Debug.LogError($"Mission setup: {target.GetType().Name} has no array {field}.", target);
                return;
            }

            Object[] present = values.Where(v => v != null).ToArray();
            property.arraySize = present.Length;
            for (int i = 0; i < present.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = present[i];

            data.ApplyModifiedProperties();
        }
    }
}
