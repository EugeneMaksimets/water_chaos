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
        private const string Teammate = "Напарник";
        private const int Idle = 0;
        private const int Extinguish = 2;

        private static readonly List<string> s_report = new List<string>();

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

            Zones zones = BuildZones(scene, root);
            SetUpTeammates(scene);
            BuildSteps(scene, zones, root);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Selection.activeGameObject = root.gameObject;

            Debug.Log("Mission setup done. Check the choices below, then save the scene (Cmd+S).\n- "
                + string.Join("\n- ", s_report), root);
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
            public RoomTemperature HouseRoom;
            public CoolingAppliance[] Coolers;

            public WaterBody ToxicBody;
            public WaterBody UndergroundBody;
            public WaterBody[] PoolBodies;
            public PumpController ToxicPump;
            public PumpController PoolPump;
            public PumpController TankPump;
            public TransferHose[] Hoses;
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
                Door = Nearest(All<BreakableDoor>(), origin);
                HouseRoom = Nearest(All<RoomTemperature>(), origin);
                Coolers = HouseRoom != null
                    ? All<CoolingAppliance>().Where(c => RoomBounds(HouseRoom).Contains(c.transform.position)).ToArray()
                    : new CoolingAppliance[0];

                WaterBody[] bodies = All<WaterBody>();
                ToxicBody = bodies.FirstOrDefault(b => HasAncestorNamed(b.transform, "toxic"));
                UndergroundBody = bodies.FirstOrDefault(b => HasAncestorNamed(b.transform, "underground"));
                PoolBodies = bodies.Where(b => HasAncestorNamed(b.transform, "pool")).ToArray();

                List<PumpController> pumps = All<PumpController>().Where(p => p.GetComponent<PickableItem>() != null).ToList();
                ToxicPump = TakeNearest(pumps, ToxicBody);
                PoolPump = TakeNearest(pumps, PoolBodies.FirstOrDefault());
                TankPump = TakeNearest(pumps, UndergroundBody);
                if (TankPump == null)
                {
                    // The toxic pump is free again after the grate, so the player takes it from the toxic tank.
                    TankPump = ToxicPump;
                    s_report.Add("ONLY TWO PUMPS: the underground-tank stage reuses the toxic-tank pump. "
                        + "Add a third Pump prefab and set it in steps \"Pick Up · Pump and Hose (Tank)\" … \"Pump · Start Tank Pump\" if you want a separate one.");
                }

                Hoses = All<TransferHose>();
                if (PoolPump != null)
                    Hoses = Hoses.OrderBy(h => Vector3.Distance(h.transform.position, PoolPump.transform.position)).ToArray();

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

                Report("House room (nearest to the truck)", HouseRoom);
                Report("Breakable door (nearest to the truck)", Door);
                s_report.Add($"Coolers in the house: {Coolers.Length}");
                Report("Toxic tank water", ToxicBody);
                Report("Underground tank water", UndergroundBody);
                s_report.Add($"Pools: {PoolBodies.Length}");
                Report("Pump for the toxic tank", ToxicPump);
                Report("Pump for the pool", PoolPump);
                Report("Pump for the underground tank", TankPump);
                s_report.Add($"Transfer hoses: {Hoses.Length}");
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
            public MissionZone TeammateFires;
        }

        private static Zones BuildZones(SceneObjects scene, Transform root)
        {
            Transform parent = new GameObject("Zones").transform;
            parent.SetParent(root, false);

            Zones zones = new Zones();
            if (scene.HouseRoom != null)
            {
                Bounds room = RoomBounds(scene.HouseRoom);
                zones.House = CreateZone("Zone · House (entered)", parent, room);
                room.Expand(new Vector3(4f, 2f, 4f));
                zones.HouseFires = CreateZone("Zone · House Fires", parent, room);
            }

            zones.ToxicTank = BodyZone("Zone · Toxic Tank Hint", parent, scene.ToxicBody, 4f);
            zones.Pool = BodyZone("Zone · Pool Hint", parent, scene.PoolBodies.FirstOrDefault(), 4f);
            zones.UndergroundTank = BodyZone("Zone · Underground Tank Hint", parent, scene.UndergroundBody, 4f);

            List<Vector3> crew = scene.Sprayers.Select(s => s.transform.position).ToList();
            if (scene.Talker != null)
                crew.Add(scene.Talker.position);
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
            {
                GameObject feed = new GameObject("Hydrant → Splitter Feed");
                Undo.RegisterCreatedObjectUndo(feed, "Setup Mission");
                feed.transform.SetParent(scene.Hydrant.transform, false);
                FixedHoseFeed component = feed.AddComponent<FixedHoseFeed>();
                Set(component, "_source", scene.Hydrant);
                Set(component, "_socket", scene.SplitterInlet);
                s_report.Add("Hydrant → splitter feed added (no hose look: set Visual on it if you add one).");
            }
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

                AddTeammateNozzle(sprayer.transform, scene.Outlets[i], nozzlePrefab);
            }
        }

        /// <summary>
        /// A copy of the nozzle prefab in front of the teammate, without its pickup parts (the player must not take it),
        /// fed from a splitter outlet. Its position and rotation need fixing by hand to fit the hands.
        /// </summary>
        private static void AddTeammateNozzle(Transform teammate, SplitterOutlet outlet, GameObject prefab)
        {
            GameObject nozzle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, teammate.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(nozzle, "Setup Mission");
            PrefabUtility.UnpackPrefabInstance(nozzle, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            nozzle.name = "Teammate Nozzle";
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

            TeammateNozzle component = nozzle.AddComponent<TeammateNozzle>();
            Set(component, "_stream", nozzle.GetComponentInChildren<WaterStream>(true));
            Set(component, "_outlet", outlet);
            Set(component, "_hose", nozzle.GetComponentInChildren<HoseController>(true));
            s_report.Add($"Nozzle for {teammate.name} on outlet {outlet.name}: MOVE \"Teammate Nozzle\" into the hands and aim it at the fire.");
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
            Marker(s.Axe, 0.8f);
            Marker(s.Nozzle, 0.8f);
            foreach (TransferHose hose in s.Hoses)
                Marker(hose, 0.8f);
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
            NozzleConnectObjective connectTruck = Add<NozzleConnectObjective>(root, Name("Connect · Nozzle to Truck"),
                "Підключи пожежний ствол до машини", truckOutlet);
            Set(connectTruck, "_connector", s.Connector);
            SetArray(connectTruck, "_points", s.TruckOutlet);
            Hint(connectTruck, "Підключи ствол до машини", PlayerControl.Interact, null);

            // Stage 3: the door.
            BreakDoorObjective breakDoor = Add<BreakDoorObjective>(root, Name("Break · Door"),
                "Розбий двері сокирою, щоб потрапити всередину будинку.", door);
            Set(breakDoor, "_door", s.Door);

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

            PickUp(root, Name("Pick Up · Pump and Hose (Pool)"), "Знайди насос та шланг.", s.Inventory, Item(s.PoolPump), Item(s.Hoses.ElementAtOrDefault(0)));
            PumpInWaterObjective poolIn = Add<PumpInWaterObjective>(root, Name("Pump · Into Pool"), "Встанови насос у басейн", pools);
            SetArray(poolIn, "_pumps", Deployments(s.PoolPump, s.ToxicPump, s.TankPump));
            SetArray(poolIn, "_bodies", s.PoolBodies);
            Hint(poolIn, "Встанови насос у басейн.", PlayerControl.Drop, z.Pool);
            TransferHoseObjective poolHose = Add<TransferHoseObjective>(root, Name("Hose · Pool to Underground Tank"),
                "Протягни шланг від насоса до підземного резервуара", underground);
            SetArray(poolHose, "_hoses", s.Hoses);
            SetArray(poolHose, "_endBodies", s.UndergroundBody);
            SetArray(poolHose, "_endSockets", s.SplitterInlet);
            Hint(poolHose, "Підключи шланг до насоса та протягни його до підземного резервуара або перехідника.", null, null);
            PumpRunningObjective poolOn = Add<PumpRunningObjective>(root, Name("Pump · Start Pool Pump"), "Увімкни насос біля басейну", poolPump);
            SetArray(poolOn, "_pumps", s.PoolPump, s.ToxicPump, s.TankPump);
            Hint(poolOn, "Шланг на місці! Тепер увімкни насос біля басейну.", null, null);

            // Stage 8: the second pump and the tee.
            PickUp(root, Name("Pick Up · Pump and Hose (Tank)"), "Знайди ще один насос та шланг", s.Inventory,
                Item(s.TankPump), Item(s.Hoses.ElementAtOrDefault(1)));
            PumpInWaterObjective tankIn = Add<PumpInWaterObjective>(root, Name("Pump · Into Underground Tank"),
                "Помісти насос у підземний резервуар", underground);
            SetArray(tankIn, "_pumps", Deployments(s.TankPump, s.ToxicPump, s.PoolPump));
            SetArray(tankIn, "_bodies", s.UndergroundBody);
            Hint(tankIn, "Помісти насос у підземний резервуар.", PlayerControl.Drop, z.UndergroundTank);
            TransferHoseObjective tankHose = Add<TransferHoseObjective>(root, Name("Hose · Onto Tank Pump"),
                "Підключи шланг до насоса", tankPump);
            SetArray(tankHose, "_hoses", s.Hoses);
            SetArray(tankHose, "_startPoints", TransferPoint(s.TankPump));
            TransferHoseObjective tee = Add<TransferHoseObjective>(root, Name("Hose · Into Tee"),
                "Знайди трійник та підключи до нього шланг.", splitter);
            SetArray(tee, "_hoses", s.Hoses);
            SetArray(tee, "_startPoints", TransferPoint(s.TankPump));
            SetArray(tee, "_endSockets", s.SplitterInlet);
            PumpRunningObjective tankOn = Add<PumpRunningObjective>(root, Name("Pump · Start Tank Pump"),
                "Увімкни насос у підземному резервуарі", tankPump);
            SetArray(tankOn, "_pumps", s.TankPump);

            // Stage 9: gasoline instead of water (the tank holds gasoline; the flame streams light by themselves).
            WaitStep wait = Add<WaitStep>(root, Name("Wait · Gasoline Reaches Teammates"), null);
            Set(wait, "_seconds", 2.5f);
            Story(root, Name("Story · Gasoline"), Teammate, "Ти що наробив?! Це ж не вода! У нас у шлангах бензин!");
            NeutralizeObjective neutralize = Add<NeutralizeObjective>(root, Name("Neutralize · Gasoline"),
                "Знайди спосіб нейтралізувати бензин.", underground);
            Set(neutralize, "_tank", s.UndergroundBody != null ? s.UndergroundBody.GetComponent<WaterReservoirComponent>() : null);
            Set(neutralize, "_toxinMarker", toxicTank);
            Set(neutralize, "_soapMarker", soap);
            Hint(neutralize, "Накачай токсичну воду в резервуар із бензином і кинь туди мило.", null, null);

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

        private static Bounds RoomBounds(RoomTemperature room) => room.GetComponent<BoxCollider>().bounds;

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
                case float number: property.floatValue = number; break;
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
