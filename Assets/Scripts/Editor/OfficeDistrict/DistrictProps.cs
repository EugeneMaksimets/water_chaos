using System.IO;
using UnityEditor;
using UnityEngine;
using WaterChaos.EditorTools.MiniVillage;
using WaterChaos.Interaction;
using WaterChaos.Items;
using WaterChaos.Tools;

namespace WaterChaos.EditorTools.OfficeDistrict
{
    /// <summary>
    /// Low-poly props of the office district. Pivot at the bottom centre, front towards -Z.
    /// Weight class (water push profile) and flammability follow the material:
    /// wood, fabric, paper and plastic burn; metal, glass and electronics do not.
    /// </summary>
    internal static class DistrictProps
    {
        private const string IconPath = "Assets/WaterChaos/Materials/Icon/axe_icon.png";
        private const string GrateProfilePath = "Assets/WaterChaos/ScriptableObjects/Corrosion/Corrosion_MetalGrate.asset";
        private const string FenceProfilePath = "Assets/WaterChaos/ScriptableObjects/Corrosion/Corrosion_Fence.asset";

        private static readonly float[] Sides = { -1f, 1f };

        /// <summary>Pose of the axe at the player's item holder (see <see cref="BuildAxe"/>).</summary>
        public static readonly Vector3 HeldPosition = new Vector3(0.05f, -0.6f, 0f);
        public static readonly Vector3 HeldRotation = new Vector3(0f, 180f, -35f);

        public static void BuildAll()
        {
            BuildOfficeFurniture();
            BuildFactoryProps();
            BuildStreetProps();
            BuildAxe();
            BuildGrate();
            BuildDoorsAndWindows();
        }

        // ───────────── Office furniture ─────────────

        private static void BuildOfficeFurniture()
        {
            var desk = new PropModel();
            desk.Part(new Vector3(0f, 0.73f, 0f), new Vector3(1.4f, 0.04f, 0.7f), "WoodLight", true);
            foreach (float x in Sides)
                desk.Part(new Vector3(x * 0.66f, 0.36f, 0f), new Vector3(0.05f, 0.72f, 0.62f), "SteelDark");
            desk.Part(new Vector3(0.42f, 0.36f, -0.02f), new Vector3(0.42f, 0.66f, 0.6f), "WoodLight");
            for (int i = 0; i < 3; i++)
                desk.Part(new Vector3(0.42f, 0.16f + i * 0.21f, -0.325f), new Vector3(0.34f, 0.16f, 0.02f), "WoodDark");
            desk.Part(new Vector3(0f, 0.45f, 0.33f), new Vector3(1.3f, 0.45f, 0.02f), "SteelDark");
            desk.Collider(new Vector3(0f, 0.37f, 0f), new Vector3(1.4f, 0.74f, 0.7f));
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "OfficeDesk", desk, VillageAssets.Heavy, true);

            var chair = new PropModel();
            chair.Part(new Vector3(0f, 0.46f, 0f), new Vector3(0.5f, 0.08f, 0.48f), "FabricTeal", true);
            chair.Part(new Vector3(0f, 0.82f, 0.22f), new Vector3(0.46f, 0.6f, 0.07f), "FabricTeal", true);
            chair.Mesh.Cylinder(new Vector3(0f, 0.08f, 0f), 0.03f, 0.03f, 0.36f, 6, "SteelDark");
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f;
                chair.Mesh.SetTransform(new Vector3(0f, 0.06f, 0f), Quaternion.Euler(0f, angle, 0f));
                chair.Mesh.Box(new Vector3(0f, 0f, 0.15f), new Vector3(0.05f, 0.04f, 0.3f), "SteelDark");
                chair.Mesh.Sphere(new Vector3(0f, -0.03f, 0.28f), Vector3.one * 0.035f, 6, 3, "Black");
                chair.Mesh.ResetTransform();
            }
            foreach (float x in Sides)
                chair.Part(new Vector3(x * 0.27f, 0.6f, 0.02f), new Vector3(0.04f, 0.05f, 0.34f), "Black");
            chair.Collider(new Vector3(0f, 0.2f, 0f), new Vector3(0.6f, 0.4f, 0.6f));
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "OfficeChair", chair, VillageAssets.LightFurniture, true);

            var computer = new PropModel();
            computer.Part(new Vector3(0f, 0.01f, 0.05f), new Vector3(0.24f, 0.02f, 0.16f), "Black");
            computer.Part(new Vector3(0f, 0.12f, 0.08f), new Vector3(0.04f, 0.22f, 0.04f), "Black");
            computer.Part(new Vector3(0f, 0.3f, 0.05f), new Vector3(0.56f, 0.34f, 0.04f), "Black", true);
            computer.Part(new Vector3(0f, 0.3f, 0.028f), new Vector3(0.52f, 0.3f, 0.01f), "Screen");
            computer.Part(new Vector3(0f, 0.015f, -0.2f), new Vector3(0.44f, 0.03f, 0.15f), "PlasticWhite", true);
            computer.Part(new Vector3(0.32f, 0.012f, -0.2f), new Vector3(0.06f, 0.025f, 0.1f), "PlasticWhite");
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "Computer", computer, VillageAssets.SmallDecor, false);

            var cabinet = new PropModel();
            cabinet.Part(new Vector3(0f, 0.66f, 0f), new Vector3(0.46f, 1.32f, 0.6f), "Steel", true);
            for (int i = 0; i < 4; i++)
            {
                cabinet.Part(new Vector3(0f, 0.18f + i * 0.32f, -0.305f), new Vector3(0.4f, 0.28f, 0.01f), "Metal");
                cabinet.Part(new Vector3(0f, 0.26f + i * 0.32f, -0.32f), new Vector3(0.14f, 0.025f, 0.03f), "SteelDark");
            }
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "FilingCabinet", cabinet, VillageAssets.Heavy, false);

            var counter = new PropModel();
            counter.Part(new Vector3(0f, 0.55f, 0f), new Vector3(2.6f, 1.1f, 0.6f), "WoodDark", true);
            counter.Part(new Vector3(0f, 1.12f, -0.05f), new Vector3(2.7f, 0.05f, 0.75f), "Whiteboard");
            counter.Part(new Vector3(0f, 0.75f, 0.45f), new Vector3(2.6f, 0.04f, 0.5f), "WoodLight", true);
            counter.Part(new Vector3(0f, 0.55f, -0.31f), new Vector3(1.6f, 0.4f, 0.02f), "Wainscot");
            foreach (float x in Sides)
                counter.Part(new Vector3(x * 1.32f, 0.55f, 0.5f), new Vector3(0.06f, 1.1f, 0.4f), "WoodDark", true);
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "ReceptionCounter", counter, null, true);

            var meeting = new PropModel();
            meeting.Part(new Vector3(0f, 0.74f, 0f), new Vector3(3f, 0.06f, 1.2f), "WoodDark", true);
            foreach (float x in new[] { -1.1f, 1.1f })
                meeting.Part(new Vector3(x, 0.36f, 0f), new Vector3(0.12f, 0.72f, 0.8f), "WoodDark");
            meeting.Collider(new Vector3(0f, 0.36f, 0f), new Vector3(2.4f, 0.72f, 0.4f));
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "MeetingTable", meeting, VillageAssets.Heavy, true);

            var board = new PropModel();
            board.Part(new Vector3(0f, 1.35f, 0f), new Vector3(1.6f, 1f, 0.04f), "Whiteboard", true);
            board.Part(new Vector3(0f, 1.87f, 0f), new Vector3(1.66f, 0.05f, 0.06f), "Metal");
            board.Part(new Vector3(0f, 0.83f, -0.04f), new Vector3(1.6f, 0.04f, 0.08f), "Metal");
            board.Part(new Vector3(-0.3f, 1.5f, -0.025f), new Vector3(0.5f, 0.02f, 0.005f), "PlasticBlue");
            board.Part(new Vector3(0.2f, 1.3f, -0.025f), new Vector3(0.7f, 0.02f, 0.005f), "PlasticRed");
            foreach (float x in Sides)
            {
                board.Part(new Vector3(x * 0.75f, 0.9f, 0f), new Vector3(0.04f, 1.8f, 0.04f), "Metal");
                board.Part(new Vector3(x * 0.75f, 0.02f, 0f), new Vector3(0.06f, 0.04f, 0.55f), "Metal");
            }
            board.Collider(new Vector3(0f, 0.2f, 0f), new Vector3(1.6f, 0.4f, 0.55f));
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "Whiteboard", board, VillageAssets.Tall, false);

            var plant = new PropModel();
            plant.Mesh.Cylinder(Vector3.zero, 0.17f, 0.22f, 0.42f, 8, "PlasticWhite");
            plant.Mesh.Cylinder(new Vector3(0f, 0.38f, 0f), 0.2f, 0.2f, 0.03f, 8, "Soil");
            plant.Mesh.Sphere(new Vector3(0f, 0.78f, 0f), new Vector3(0.32f, 0.42f, 0.32f), 7, 4, "Leaves");
            plant.Mesh.Sphere(new Vector3(0.12f, 1.05f, 0.05f), new Vector3(0.2f, 0.26f, 0.2f), 6, 3, "LeavesDark");
            plant.Collider(new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 1.1f, 0.5f));
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "OfficePlant", plant, VillageAssets.SmallDecor, true);

            var paper = new PropModel();
            paper.Part(new Vector3(0f, 0.15f, 0f), new Vector3(0.45f, 0.3f, 0.32f), "Cardboard", true);
            paper.Part(new Vector3(0f, 0.305f, 0f), new Vector3(0.47f, 0.02f, 0.34f), "Cardboard");
            paper.Part(new Vector3(0.05f, 0.33f, 0.02f), new Vector3(0.3f, 0.03f, 0.21f), "PlasticWhite");
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "PaperBox", paper, VillageAssets.LightPlastic, true);

            var printer = new PropModel();
            printer.Part(new Vector3(0f, 0.18f, 0f), new Vector3(0.55f, 0.36f, 0.45f), "PlasticWhite", true);
            printer.Part(new Vector3(0f, 0.38f, 0.05f), new Vector3(0.5f, 0.04f, 0.35f), "MetalDark");
            printer.Part(new Vector3(0.18f, 0.37f, -0.18f), new Vector3(0.12f, 0.02f, 0.08f), "Screen");
            printer.Part(new Vector3(0f, 0.12f, -0.23f), new Vector3(0.4f, 0.02f, 0.03f), "MetalDark");
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "Printer", printer, VillageAssets.Furniture, false);

            var cooler = new PropModel();
            cooler.Part(new Vector3(0f, 0.5f, 0f), new Vector3(0.34f, 1f, 0.34f), "PlasticWhite", true);
            cooler.Mesh.Cylinder(new Vector3(0f, 1f, 0f), 0.15f, 0.15f, 0.38f, 8, "Glass");
            cooler.Part(new Vector3(-0.07f, 0.75f, -0.18f), new Vector3(0.04f, 0.05f, 0.04f), "PlasticBlue");
            cooler.Part(new Vector3(0.07f, 0.75f, -0.18f), new Vector3(0.04f, 0.05f, 0.04f), "PlasticRed");
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "WaterCooler", cooler, VillageAssets.Furniture, false);

            var bin = new PropModel();
            bin.Mesh.Cylinder(Vector3.zero, 0.15f, 0.18f, 0.38f, 8, "MetalDark", false);
            bin.Mesh.Cylinder(Vector3.zero, 0.15f, 0.15f, 0.01f, 8, "MetalDark");
            bin.Mesh.Sphere(new Vector3(0f, 0.34f, 0f), new Vector3(0.12f, 0.06f, 0.12f), 6, 3, "PlasticWhite");
            bin.Collider(new Vector3(0f, 0.19f, 0f), new Vector3(0.34f, 0.38f, 0.34f));
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "OfficeBin", bin, VillageAssets.LightPlastic, true);

            var divider = new PropModel();
            divider.Part(new Vector3(0f, 0.6f, 0f), new Vector3(1.4f, 1.2f, 0.06f), "FabricTeal", true);
            divider.Part(new Vector3(0f, 1.21f, 0f), new Vector3(1.42f, 0.03f, 0.08f), "Metal");
            foreach (float x in Sides)
                divider.Part(new Vector3(x * 0.55f, 0.02f, 0f), new Vector3(0.06f, 0.04f, 0.45f), "Metal", true);
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "DeskDivider", divider, VillageAssets.LightFurniture, true);

            var shelf = new PropModel();
            foreach (float x in Sides)
            {
                foreach (float z in new[] { -0.2f, 0.2f })
                    shelf.Part(new Vector3(x * 0.58f, 0.95f, z), new Vector3(0.04f, 1.9f, 0.04f), "Steel");
            }
            for (int i = 0; i < 4; i++)
            {
                float y = 0.1f + i * 0.58f;
                shelf.Part(new Vector3(0f, y, 0f), new Vector3(1.2f, 0.03f, 0.44f), "Steel");
                if (i < 3)
                {
                    shelf.Part(new Vector3(-0.3f, y + 0.15f, 0f), new Vector3(0.45f, 0.28f, 0.34f), "Cardboard");
                    shelf.Part(new Vector3(0.28f, y + 0.12f, 0.02f), new Vector3(0.4f, 0.22f, 0.3f), i == 1 ? "PlasticBlue" : "Cardboard");
                }
            }
            shelf.Collider(new Vector3(0f, 0.95f, 0f), new Vector3(1.2f, 1.9f, 0.46f));
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "StorageShelf", shelf, VillageAssets.Heavy, true);

            var kitchen = new PropModel();
            kitchen.Part(new Vector3(0f, 0.45f, 0f), new Vector3(2.4f, 0.9f, 0.6f), "PlasticWhite", true);
            kitchen.Part(new Vector3(0f, 0.92f, 0f), new Vector3(2.44f, 0.04f, 0.64f), "WoodDark");
            kitchen.Part(new Vector3(-0.6f, 0.95f, 0f), new Vector3(0.5f, 0.03f, 0.4f), "Metal");
            kitchen.Part(new Vector3(0.6f, 1.07f, 0.05f), new Vector3(0.35f, 0.26f, 0.3f), "MetalDark");
            for (int i = 0; i < 4; i++)
                kitchen.Part(new Vector3(-0.9f + i * 0.6f, 0.45f, -0.305f), new Vector3(0.55f, 0.8f, 0.01f), "Trim");
            DistrictKit.SaveProp(DistrictKit.FurnitureFolder, "Kitchenette", kitchen, null, true);
        }

        // ───────────── Factory ─────────────

        private static void BuildFactoryProps()
        {
            Barrel("MetalBarrel", "Steel", "SteelDark", false);
            Barrel("ToxicBarrel", "ToxicDrum", "HazardYellow", false);
            Barrel("OilBarrel", "GasRed", "SteelDark", true);

            var tote = new PropModel();
            tote.Part(new Vector3(0f, 0.08f, 0f), new Vector3(1.2f, 0.16f, 1f), "WoodLight", true);
            tote.Part(new Vector3(0f, 0.65f, 0f), new Vector3(1.1f, 0.95f, 0.92f), "ToxicGlow", true);
            foreach (float x in new[] { -0.55f, 0f, 0.55f })
            {
                foreach (float z in Sides)
                    tote.Part(new Vector3(x, 0.66f, z * 0.47f), new Vector3(0.03f, 1f, 0.03f), "Steel");
            }
            foreach (float y in new[] { 0.4f, 0.8f, 1.15f })
            {
                foreach (float z in Sides)
                    tote.Part(new Vector3(0f, y, z * 0.47f), new Vector3(1.12f, 0.03f, 0.03f), "Steel");
            }
            tote.Mesh.Cylinder(new Vector3(0f, 1.12f, 0f), 0.12f, 0.12f, 0.06f, 8, "PlasticWhite");
            DistrictKit.SaveProp(DistrictKit.FactoryFolder, "ChemicalTote", tote, VillageAssets.Heavy, false);

            var pallet = new PropModel();
            foreach (float z in new[] { -0.45f, 0f, 0.45f })
                pallet.Part(new Vector3(0f, 0.05f, z), new Vector3(1.2f, 0.1f, 0.1f), "WoodLight", true);
            for (int i = 0; i < 6; i++)
                pallet.Part(new Vector3(-0.5f + i * 0.2f, 0.12f, 0f), new Vector3(0.14f, 0.03f, 1f), "WoodLight");
            pallet.Collider(new Vector3(0f, 0.07f, 0f), new Vector3(1.2f, 0.14f, 1f));
            DistrictKit.SaveProp(DistrictKit.FactoryFolder, "Pallet", pallet, VillageAssets.LightFurniture, true);

            var crates = new PropModel();
            crates.Part(new Vector3(-0.3f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), "WoodLight", true);
            crates.Part(new Vector3(0.32f, 0.25f, 0.05f), new Vector3(0.5f, 0.5f, 0.5f), "WoodDark", true);
            crates.Part(new Vector3(-0.25f, 0.85f, 0.02f), new Vector3(0.5f, 0.5f, 0.5f), "WoodDark", true);
            foreach (float y in new[] { 0.1f, 0.5f })
                crates.Part(new Vector3(-0.3f, y, -0.305f), new Vector3(0.6f, 0.06f, 0.02f), "WoodDark");
            DistrictKit.SaveProp(DistrictKit.FactoryFolder, "CrateStack", crates, VillageAssets.Furniture, true);

            var cylinder = new PropModel();
            cylinder.Mesh.Cylinder(Vector3.zero, 0.13f, 0.13f, 1.3f, 8, "GasYellow");
            cylinder.Mesh.Sphere(new Vector3(0f, 1.3f, 0f), new Vector3(0.13f, 0.08f, 0.13f), 8, 3, "GasYellow");
            cylinder.Mesh.Cylinder(new Vector3(0f, 1.36f, 0f), 0.04f, 0.04f, 0.12f, 6, "SteelDark");
            cylinder.Collider(new Vector3(0f, 0.7f, 0f), new Vector3(0.26f, 1.4f, 0.26f));
            DistrictKit.SaveProp(DistrictKit.FactoryFolder, "GasCylinder", cylinder, VillageAssets.Tall, false);

            var toolbox = new PropModel();
            toolbox.Part(new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.24f, 0.22f), "GasRed", true);
            toolbox.Part(new Vector3(0f, 0.28f, 0f), new Vector3(0.3f, 0.03f, 0.03f), "SteelDark");
            DistrictKit.SaveProp(DistrictKit.FactoryFolder, "Toolbox", toolbox, VillageAssets.SmallDecor, false);

            var bench = new PropModel();
            bench.Part(new Vector3(0f, 0.9f, 0f), new Vector3(2f, 0.07f, 0.8f), "WoodDark", true);
            foreach (float x in Sides)
            {
                foreach (float z in Sides)
                    bench.Part(new Vector3(x * 0.92f, 0.45f, z * 0.34f), new Vector3(0.07f, 0.9f, 0.07f), "SteelDark");
            }
            bench.Part(new Vector3(0f, 0.25f, 0f), new Vector3(1.9f, 0.04f, 0.7f), "Steel");
            bench.Part(new Vector3(0.5f, 0.97f, 0.1f), new Vector3(0.25f, 0.08f, 0.15f), "SteelDark");
            bench.Collider(new Vector3(0f, 0.45f, 0f), new Vector3(2f, 0.9f, 0.8f));
            DistrictKit.SaveProp(DistrictKit.FactoryFolder, "Workbench", bench, VillageAssets.Heavy, true);

            var panel = new PropModel();
            panel.Part(new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 1.8f, 0.4f), "Steel", true);
            panel.Part(new Vector3(0f, 1.3f, -0.205f), new Vector3(0.6f, 0.35f, 0.01f), "Screen");
            for (int i = 0; i < 4; i++)
                panel.Part(new Vector3(-0.25f + i * 0.17f, 0.95f, -0.21f), new Vector3(0.08f, 0.08f, 0.02f), i % 2 == 0 ? "ToxicGlow" : "GasRed");
            panel.Part(new Vector3(0f, 0.6f, -0.205f), new Vector3(0.7f, 0.04f, 0.01f), "HazardYellow");
            DistrictKit.SaveProp(DistrictKit.FactoryFolder, "ControlPanel", panel, null, false);

            var barrier = new PropModel();
            barrier.Part(new Vector3(0f, 0.25f, 0f), new Vector3(2f, 0.5f, 0.6f), "Concrete", true);
            barrier.Part(new Vector3(0f, 0.65f, 0f), new Vector3(2f, 0.3f, 0.3f), "Concrete", true);
            for (int i = 0; i < 4; i++)
            {
                barrier.Mesh.SetTransform(new Vector3(-0.75f + i * 0.5f, 0.6f, -0.155f), Quaternion.Euler(0f, 0f, 40f));
                barrier.Mesh.Box(Vector3.zero, new Vector3(0.16f, 0.5f, 0.01f), "HazardYellow");
                barrier.Mesh.ResetTransform();
            }
            DistrictKit.SaveProp(DistrictKit.FactoryFolder, "HazardBarrier", barrier, null, false);

            var sign = new PropModel();
            sign.Part(new Vector3(0f, 0.8f, 0.03f), new Vector3(0.06f, 1.6f, 0.06f), "SteelDark", true);
            sign.Mesh.SetTransform(new Vector3(0f, 1.55f, 0f), Quaternion.identity);
            sign.Mesh.Extrude(VillageProps.Polygon(3, 0.42f, 90f), -0.02f, 0.02f, "HazardYellow");
            sign.Mesh.Extrude(VillageProps.Polygon(3, 0.3f, 90f), -0.03f, -0.02f, "Black");
            sign.Mesh.Extrude(VillageProps.Polygon(3, 0.2f, 90f), -0.04f, -0.03f, "ToxicGlow");
            sign.Mesh.ResetTransform();
            DistrictKit.SaveProp(DistrictKit.FactoryFolder, "ToxicWarningSign", sign, VillageAssets.Sign, false);

            // Chain-link security fence panel (2 m): toxic liquid eats it away.
            var fence = new PropModel();
            foreach (float x in Sides)
                fence.Part(new Vector3(x * 0.97f, 1f, 0f), new Vector3(0.07f, 2f, 0.07f), "SteelDark");
            foreach (float y in new[] { 0.1f, 1.95f })
                fence.Part(new Vector3(0f, y, 0f), new Vector3(2f, 0.05f, 0.05f), "SteelDark");
            for (int i = 0; i < 9; i++)
            {
                float x = -0.8f + i * 0.2f;
                fence.Mesh.SetTransform(new Vector3(x, 1f, 0f), Quaternion.Euler(0f, 0f, 30f));
                fence.Mesh.Box(Vector3.zero, new Vector3(0.015f, 2.1f, 0.015f), "Steel");
                fence.Mesh.ResetTransform();
                fence.Mesh.SetTransform(new Vector3(x, 1f, 0f), Quaternion.Euler(0f, 0f, -30f));
                fence.Mesh.Box(Vector3.zero, new Vector3(0.015f, 2.1f, 0.015f), "Steel");
                fence.Mesh.ResetTransform();
            }
            fence.Part(new Vector3(0f, 1.6f, -0.04f), new Vector3(0.5f, 0.3f, 0.01f), "HazardYellow");
            fence.Collider(new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 0.1f));
            GameObject fencePrefab = DistrictKit.SaveProp(DistrictKit.FactoryFolder, "SecurityFence", fence, null, false);
            MakeCorrodible(fencePrefab, FenceProfilePath);

            var pipe = new PropModel();
            Horizontal(pipe.Mesh, new Vector3(0f, 0f, 0f), 0.12f, 2f, "Steel");
            foreach (float x in Sides)
                Horizontal(pipe.Mesh, new Vector3(x * 0.95f, 0f, 0f), 0.16f, 0.08f, "SteelDark");
            DistrictKit.SaveProp(DistrictKit.FactoryFolder, "PipeSegment", pipe, null, false);
        }

        private static void Barrel(string name, string body, string band, bool flammable)
        {
            var model = new PropModel();
            model.Mesh.Cylinder(Vector3.zero, 0.3f, 0.3f, 0.9f, 10, body);
            foreach (float y in new[] { 0.25f, 0.62f })
                model.Mesh.Cylinder(new Vector3(0f, y, 0f), 0.31f, 0.31f, 0.05f, 10, band, false);
            model.Mesh.Cylinder(new Vector3(0.12f, 0.9f, 0f), 0.04f, 0.04f, 0.02f, 6, "SteelDark");
            model.Collider(new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.9f, 0.6f));
            DistrictKit.SaveProp(DistrictKit.FactoryFolder, name, model, VillageAssets.Furniture, flammable);
        }

        // ───────────── Street ─────────────

        private static void BuildStreetProps()
        {
            var tower = new PropModel();
            foreach (float x in Sides)
            {
                foreach (float z in Sides)
                    tower.Part(new Vector3(x * 0.9f, 1f, z * 0.9f), new Vector3(0.12f, 2f, 0.12f), "SteelDark");
            }
            tower.Part(new Vector3(0f, 1.1f, 0f), new Vector3(2f, 0.08f, 2f), "SteelDark");
            tower.Mesh.Cylinder(new Vector3(0f, 2f, 0f), 1.1f, 1.1f, 2.2f, 12, "WoodDark");
            foreach (float y in new[] { 2.4f, 3.2f, 3.9f })
                tower.Mesh.Cylinder(new Vector3(0f, y, 0f), 1.12f, 1.12f, 0.06f, 12, "SteelDark", false);
            tower.Mesh.Cylinder(new Vector3(0f, 4.2f, 0f), 1.2f, 0.05f, 0.9f, 12, "RoofDark");
            tower.Collider(new Vector3(0f, 2.5f, 0f), new Vector3(2.2f, 5f, 2.2f));
            DistrictKit.SaveProp(DistrictKit.PropsFolder, "WaterTower", tower, null, false);

            var chimney = new PropModel();
            chimney.Part(new Vector3(0f, 0.6f, 0f), new Vector3(0.7f, 1.2f, 0.5f), "BrickBrown", true);
            chimney.Part(new Vector3(0f, 1.25f, 0f), new Vector3(0.8f, 0.1f, 0.6f), "Stucco");
            DistrictKit.SaveProp(DistrictKit.PropsFolder, "RoofChimney", chimney, null, false);

            var ac = new PropModel();
            ac.Part(new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.8f, 0.9f), "Metal", true);
            ac.Mesh.Cylinder(new Vector3(0f, 0.8f, 0f), 0.35f, 0.35f, 0.04f, 10, "SteelDark");
            ac.Part(new Vector3(0f, 0.4f, -0.455f), new Vector3(1f, 0.6f, 0.01f), "SteelDark");
            DistrictKit.SaveProp(DistrictKit.PropsFolder, "RoofAcUnit", ac, null, false);

            var mailbox = new PropModel();
            mailbox.Part(new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 0.9f, 0.45f), "PlasticBlue", true);
            mailbox.Mesh.Cylinder(new Vector3(0f, 1f, -0.225f), 0.25f, 0.25f, 0.45f, 10, "PlasticBlue");
            foreach (float x in Sides)
                mailbox.Part(new Vector3(x * 0.2f, 0.05f, 0f), new Vector3(0.06f, 0.1f, 0.4f), "SteelDark");
            DistrictKit.SaveProp(DistrictKit.PropsFolder, "StreetMailbox", mailbox, VillageAssets.Heavy, false);

            // Front steps up to a raised door: the top step meets the wall at z = 0, the stairs run out to -Z.
            var stoop = new PropModel();
            for (int i = 0; i < 4; i++)
            {
                float top = 0.17f * (i + 1);
                float depth = 0.32f * (4 - i);
                stoop.Part(new Vector3(0f, top * 0.5f, -depth * 0.5f), new Vector3(1.8f, top, depth), "Stucco", true);
            }
            foreach (float x in Sides)
                stoop.Part(new Vector3(x * 0.95f, 0.55f, -0.64f), new Vector3(0.1f, 1.1f, 1.28f), "BrickBrown");
            DistrictKit.SaveProp(DistrictKit.PropsFolder, "Stoop", stoop, null, false);
        }

        // ───────────── Axe ─────────────

        /// <summary>Fire axe: a pickable item; in the hands the primary button swings it (<see cref="AxeTool"/>).</summary>
        private static void BuildAxe()
        {
            var model = new PropModel();
            model.Part(new Vector3(0f, 0.45f, 0f), new Vector3(0.045f, 0.9f, 0.045f), "WoodLight");
            model.Part(new Vector3(0f, 0.05f, 0f), new Vector3(0.055f, 0.08f, 0.055f), "Black");
            model.Mesh.SetTransform(new Vector3(0.11f, 0.84f, 0f), Quaternion.identity);
            model.Mesh.Extrude(new[]
            {
                new Vector2(-0.07f, -0.05f), new Vector2(0.12f, -0.11f), new Vector2(0.16f, 0f),
                new Vector2(0.12f, 0.1f), new Vector2(-0.07f, 0.05f),
            }, -0.012f, 0.012f, "GasRed");
            model.Mesh.ResetTransform();
            model.Part(new Vector3(0.25f, 0.84f, 0f), new Vector3(0.03f, 0.2f, 0.026f), "Metal");
            model.Part(new Vector3(-0.07f, 0.84f, 0f), new Vector3(0.09f, 0.06f, 0.03f), "SteelDark");
            model.Collider(new Vector3(0.05f, 0.47f, 0f), new Vector3(0.36f, 0.94f, 0.08f));

            GameObject root = VillageAssets.CreateMeshObject("Axe", model.Mesh, "Axe");
            VillageAssets.AddColliders(root, model.Colliders);
            var body = root.AddComponent<Rigidbody>();
            body.mass = 2.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var item = root.AddComponent<PickableItem>();
            var itemData = new SerializedObject(item);
            itemData.FindProperty("_icon").objectReferenceValue = AxeIcon();
            // The item holder is turned 90° to the right of the view, so the view's forward is the holder's -X:
            // turned round (Y 180) the blade (+X) points forward; Z -35 leans the head forward. Gripped low on the handle.
            itemData.FindProperty("_heldPosition").vector3Value = HeldPosition;
            itemData.FindProperty("_heldRotation").vector3Value = HeldRotation;
            itemData.FindProperty("_heldScale").floatValue = 1f;
            itemData.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<AxeTool>();
            DistrictKit.Remember("Axe", VillageAssets.SavePrefab(root, DistrictKit.PropsFolder, "Axe"));
        }

        /// <summary>64 × 64 icon: red head on a wooden handle, imported as a sprite.</summary>
        private static Sprite AxeIcon()
        {
            if (!File.Exists(IconPath))
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var clear = new Color(0f, 0f, 0f, 0f);
                var wood = new Color(0.78f, 0.58f, 0.36f);
                var red = new Color(0.86f, 0.2f, 0.16f);
                var steel = new Color(0.85f, 0.87f, 0.9f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Color c = clear;
                        // Handle: diagonal from bottom-left to top-right.
                        if (Mathf.Abs((x - 10) - (y - 6)) <= 3 && x > 6 && x < 50)
                            c = wood;
                        // Head around (44, 46).
                        float hx = x - 44f, hy = y - 46f;
                        if (hx > -6f && hx < 14f && hy > -12f + hx * 0.2f && hy < 12f - hx * 0.2f && Mathf.Abs(hx + hy) < 14f)
                            c = hx > 9f ? steel : red;
                        texture.SetPixel(x, y, c);
                    }
                }

                File.WriteAllBytes(IconPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(IconPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        }

        // ───────────── Grate ─────────────

        /// <summary>
        /// Steel bar grate filling a 1.5 × 2.4 m doorway. Static; only toxic liquid destroys it
        /// (<see cref="CorrodibleObject"/> with the metal grate profile); water and the axe do nothing.
        /// </summary>
        private static void BuildGrate()
        {
            CorrosionProfile profile = LoadOrCreateGrateProfile();

            var model = new PropModel();
            const float width = 1.5f, height = 2.4f;
            for (int i = 0; i <= 7; i++)
                model.Part(new Vector3(-width * 0.5f + 0.06f + i * (width - 0.12f) / 7f, height * 0.5f, 0f), new Vector3(0.04f, height, 0.04f), "SteelDark");
            foreach (float y in new[] { 0.06f, 0.8f, 1.6f, height - 0.06f })
                model.Part(new Vector3(0f, y, 0f), new Vector3(width, 0.08f, 0.06f), "Steel");
            model.Part(new Vector3(0.3f, 1.2f, -0.05f), new Vector3(0.18f, 0.24f, 0.08f), "HazardYellow");
            model.Collider(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0.1f));
            GameObject prefab = DistrictKit.SaveProp(DistrictKit.DestructionFolder, "MetalGrate", model, null, false);
            MakeCorrodible(prefab, GrateProfilePath);
        }

        private static CorrosionProfile LoadOrCreateGrateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<CorrosionProfile>(GrateProfilePath);
            if (profile != null)
                return profile;

            Directory.CreateDirectory(Path.GetDirectoryName(GrateProfilePath));
            profile = ScriptableObject.CreateInstance<CorrosionProfile>();
            AssetDatabase.CreateAsset(profile, GrateProfilePath);
            var data = new SerializedObject(profile);
            // Steel: about 100 L of toxic liquid (a good 20 s of spraying).
            data.FindProperty("_resistance").floatValue = 0.6f;
            data.FindProperty("_threshold").floatValue = 40f;
            data.FindProperty("_corrosionPerLiter").floatValue = 1f;
            data.FindProperty("_soakRate").floatValue = 1f;
            data.FindProperty("_wetDuration").floatValue = 3f;
            data.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private static void MakeCorrodible(GameObject prefab, string profilePath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<CorrosionProfile>(profilePath);
            if (profile == null)
            {
                Debug.LogWarning($"Office district: corrosion profile {profilePath} missing; run Water Chaos/Setup Toxic Liquid first.");
                return;
            }

            string path = AssetDatabase.GetAssetPath(prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var corrodible = root.AddComponent<CorrodibleObject>();
                DistrictKit.SetReference(corrodible, "_profile", profile);
                var debris = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WaterChaos/Prefabs/Toxic/CorrosionBreakDebris.prefab");
                if (debris != null)
                    DistrictKit.SetReference(corrodible, "_destroyEffect", debris);

                var view = root.AddComponent<CorrosionView>();
                var fumes = AssetDatabase.LoadAssetAtPath<ParticleSystem>("Assets/WaterChaos/Prefabs/Toxic/CorrosionFumes.prefab");
                if (fumes != null)
                    DistrictKit.SetReference(view, "_fumesPrefab", fumes);

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ───────────── Doors and windows ─────────────

        private static void BuildDoorsAndWindows()
        {
            GameObject splinters = DistrictKit.Burst("DoorSplinters", "WoodDark", 22, new Vector3(1f, 2f, 0.1f));
            BreakableDoorPrefab("Door_Office", 1f, 2.2f, "WoodDark", false, splinters);
            BreakableDoorPrefab("Door_OfficeGlass", 1.5f, 2.25f, "SteelDark", true, splinters);
            BreakableDoorPrefab("Door_Brownstone", 1.1f, 2.25f, "DoorRed", false, splinters);
            BreakableDoorPrefab("Door_Factory", 1.1f, 2.2f, "Steel", false, splinters);

            // Breakable windows; the frame is cream like the reference's white trims.
            DistrictKit.Remember("Window_Office", VillageAssets.SaveWindow("Window_Office", 1.2f, 1.7f, "Trim", null, false, 2, 2, 60f, 120f, 360f));
            DistrictKit.Remember("Window_Brownstone", VillageAssets.SaveWindow("Window_Brownstone", 1f, 1.6f, "Trim", null, true, 2, 2, 60f, 120f, 360f));
            DistrictKit.Remember("Window_Factory", VillageAssets.SaveWindow("Window_Factory", 1.8f, 1f, "SteelDark", null, true, 2, 1, 70f, 140f, 400f));

            // Lit window seen from the street on floors that cannot be entered: frame, glowing pane, sill.
            var lit = new PropModel();
            const float w = 1f, h = 1.6f;
            lit.Part(new Vector3(0f, 0f, 0.05f), new Vector3(w, h, 0.02f), "WindowGlow");
            lit.Part(new Vector3(0f, h * 0.5f + 0.05f, 0f), new Vector3(w + 0.18f, 0.1f, 0.14f), "Trim");
            lit.Part(new Vector3(0f, -h * 0.5f - 0.05f, -0.05f), new Vector3(w + 0.3f, 0.08f, 0.24f), "Trim");
            foreach (float x in Sides)
                lit.Part(new Vector3(x * (w * 0.5f + 0.05f), 0f, 0f), new Vector3(0.1f, h, 0.14f), "Trim");
            lit.Part(new Vector3(0f, 0f, 0.03f), new Vector3(0.05f, h, 0.03f), "Trim");
            lit.Part(new Vector3(0f, 0f, 0.03f), new Vector3(w, 0.05f, 0.03f), "Trim");
            DistrictKit.SaveProp(DistrictKit.BuildingFolder, "Window_LitFake", lit, null, false);

            var dark = new PropModel();
            dark.Part(new Vector3(0f, 0f, 0.05f), new Vector3(w, h, 0.02f), "Screen");
            dark.Part(new Vector3(0f, h * 0.5f + 0.05f, 0f), new Vector3(w + 0.18f, 0.1f, 0.14f), "Trim");
            dark.Part(new Vector3(0f, -h * 0.5f - 0.05f, -0.05f), new Vector3(w + 0.3f, 0.08f, 0.24f), "Trim");
            foreach (float x in Sides)
                dark.Part(new Vector3(x * (w * 0.5f + 0.05f), 0f, 0f), new Vector3(0.1f, h, 0.14f), "Trim");
            dark.Part(new Vector3(0f, 0f, 0.03f), new Vector3(0.05f, h, 0.03f), "Trim");
            dark.Part(new Vector3(0f, 0f, 0.03f), new Vector3(w, 0.05f, 0.03f), "Trim");
            DistrictKit.SaveProp(DistrictKit.BuildingFolder, "Window_DarkFake", dark, null, false);
        }

        private static void BreakableDoorPrefab(string name, float width, float height, string material, bool glass, GameObject splinters)
        {
            GameObject prefab = VillageAssets.SaveDoor(name, width, height, material, glass);
            string path = AssetDatabase.GetAssetPath(prefab);
            DoorBreakSetup.Apply(path, material, splinters);
            DistrictKit.Remember(name, AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }

        private static void Horizontal(LowPolyMesh mesh, Vector3 center, float radius, float length, string material)
        {
            mesh.SetTransform(center, Quaternion.Euler(0f, 0f, 90f));
            mesh.Cylinder(new Vector3(0f, -length * 0.5f, 0f), radius, radius, length, 8, material);
            mesh.ResetTransform();
        }
    }
}
