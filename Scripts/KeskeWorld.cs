using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Keske
{
    // Also called by the Editor scene builder. All objects remain editable in the Hierarchy.
    public static class KeskeWorld
    {
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Color Wood = new Color(0.25f, 0.17f, 0.12f);
        static readonly Color PaleWood = new Color(0.47f, 0.34f, 0.22f);
        static readonly Color Wall = new Color(0.38f, 0.42f, 0.38f);
        static readonly Color Dark = new Color(0.075f, 0.11f, 0.12f);
        static readonly Color Teal = new Color(0.15f, 0.7f, 0.66f);
        static readonly Color Amber = new Color(0.92f, 0.52f, 0.2f);
        static readonly Color Linen = new Color(0.65f, 0.61f, 0.48f);

        static Transform Group(string name, Transform parent, Vector3 position)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            return obj.transform;
        }

        static Material Mat(Color color, bool glow = false)
        {
            string key = ColorUtility.ToHtmlStringRGB(color) + (glow ? "_Glow" : "");
            Material mat;
            if (materials.TryGetValue(key, out mat) && mat != null) return mat;
            Shader shader = GraphicsSettings.currentRenderPipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader) { name = "Keske_" + key, color = color };
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.15f);
            if (glow)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 1.5f);
            }
            materials[key] = mat;
            return mat;
        }

        static GameObject Shape(string name, Transform parent, Vector3 position, Vector3 scale, Color color, PrimitiveType shape = PrimitiveType.Cube, bool solid = true, bool glow = false)
        {
            var obj = GameObject.CreatePrimitive(shape);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = Mat(color, glow);
            if (!solid)
            {
                var collider = obj.GetComponent<Collider>();
                collider.enabled = false;
                if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
            }
            return obj;
        }

        static GameObject Box(string name, Transform p, float x, float y, float z, float sx, float sy, float sz, Color color, bool solid = true)
        {
            return Shape(name, p, new Vector3(x, y, z), new Vector3(sx, sy, sz), color, PrimitiveType.Cube, solid);
        }

        static KeskeInteractable Interactive(Transform root, InteractionKind kind, string id, string tr, string en)
        {
            var it = root.gameObject.AddComponent<KeskeInteractable>();
            it.kind = kind; it.id = id; it.titleTR = tr; it.titleEN = en;
            return it;
        }

        public static void Build(KeskeGame game)
        {
            materials.Clear();
            var world = Group("World", game.transform, Vector3.zero);
            game.worldRoot = world;
            var garden = Group("Garden", world, Vector3.zero);
            var house = Group("House", world, Vector3.zero);
            var ground = Group("GroundFloor", house, Vector3.zero);
            var upper = Group("UpperFloor", house, new Vector3(0, 3.2f, 0));
            BuildGarden(garden);
            BuildShell(house, ground, upper);
            BuildGround(ground);
            BuildUpper(upper);
            BuildGardenGameplay(garden);
            BuildLights(game, world);
            BuildPlayer(game);
            var shadow = Group("MemorySilhouette", world, Vector3.zero);
            Shape("Body", shadow, new Vector3(0, 1.05f, 0), new Vector3(0.5f, 0.8f, 0.28f), Dark * 0.25f, PrimitiveType.Capsule, false);
            Shape("Head", shadow, new Vector3(0, 1.87f, 0), Vector3.one * 0.34f, Dark * 0.25f, PrimitiveType.Sphere, false);
            Box("Coat", shadow, 0, 0.5f, 0, 0.7f, 0.8f, 0.35f, Dark * 0.25f, false);
            game.shadowFigure = shadow.gameObject;
            shadow.gameObject.SetActive(false);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.025f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.37f, 0.34f, 0.29f);
            RenderSettings.fogColor = new Color(0.09f, 0.12f, 0.12f);
            RenderSettings.skybox = null;
        }

        static void BuildGarden(Transform garden)
        {
            Box("Ground", garden, 0, -0.2f, 0, 70, 0.4f, 70, new Color(0.13f, 0.19f, 0.14f));
            Box("GardenPath", garden, 0, 0.014f, -13, 2.3f, 0.025f, 16, new Color(0.3f, 0.31f, 0.27f), false);
            var random = new System.Random(36);
            for (int i = 0; i < 60; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float radius = 15 + (float)random.NextDouble() * 15;
                Vector3 treePosition = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                if (Vector3.Distance(treePosition, new Vector3(-10, 0, -14)) < 3.2f) continue;
                var tree = Group("Pine_" + i, garden, treePosition);
                float height = 5 + (float)random.NextDouble() * 4;
                Shape("Trunk", tree, new Vector3(0, height * 0.45f, 0), new Vector3(0.4f, height * 0.45f, 0.4f), Wood, PrimitiveType.Cylinder);
                for (int k = 0; k < 3; k++)
                {
                    var crown = Shape("Foliage", tree, new Vector3(0, height * 0.52f + k * 1.25f, 0), new Vector3(4 - k * 0.85f, 3 - k * 0.35f, 4 - k * 0.85f), new Color(0.09f + k * 0.015f, 0.2f + k * 0.012f, 0.16f), PrimitiveType.Sphere, false);
                    crown.transform.localRotation = Quaternion.Euler(0, i * 41, 0);
                }
            }
            for (int i = 0; i < 8; i++)
                Box("PathStone", garden, i % 2 == 0 ? -1.3f : 1.3f, 0.08f, -6 - i * 1.2f, 0.3f, 0.15f, 0.5f, Wall);
            Box("BoundaryNorth", garden, 0, 1, 33, 68, 2, 0.4f, Dark);
            Box("BoundarySouth", garden, 0, 1, -33, 68, 2, 0.4f, Dark);
            Box("BoundaryEast", garden, 33, 1, 0, 0.4f, 2, 68, Dark);
            Box("BoundaryWest", garden, -33, 1, 0, 0.4f, 2, 68, Dark);
        }

        static void BuildShell(Transform house, Transform ground, Transform upper)
        {
            Box("Floor", ground, 0, -0.12f, 0, 12, 0.24f, 10, PaleWood);
            Box("UpperMainFloor", house, -1.5f, 3.1f, 0, 9, 0.2f, 10, PaleWood);
            Box("UpperSouthLanding", house, 4.5f, 3.1f, -4.25f, 3, 0.2f, 1.5f, PaleWood);
            Box("UpperNorthLanding", house, 4.5f, 3.1f, 4.2f, 3, 0.2f, 1.6f, PaleWood);
            for (int f = 0; f < 2; f++)
            {
                Transform parent = f == 0 ? ground : upper;
                Box("WestWall", parent, -6, 1.5f, 0, 0.2f, 3, 10, Wall);
                Box("EastWall", parent, 6, 1.5f, 0, 0.2f, 3, 10, Wall);
                Box("NorthWall", parent, 0, 1.5f, 5, 12, 3, 0.2f, Wall);
                WindowWall(parent, -3.5f, -5, 5);
                WindowWall(parent, 3.5f, -5, 5);
                if (f == 0)
                    Box("EntranceHeader", parent, 0, 2.65f, -5, 2, 0.7f, 0.2f, Wall);
                else Box("FrontCenterWall", parent, 0, 1.5f, -5, 2, 3, 0.2f, Wall);
                Partition(parent);
                Box("RoomDividerWest", parent, -5.3f, 1.5f, 0, 1.4f, 3, 0.15f, Wall);
                Box("RoomDividerEast", parent, -2.3f, 1.5f, 0, 1.6f, 3, 0.15f, Wall);
                Box("RoomDividerHeader", parent, -3.85f, 2.65f, 0, 1.5f, 0.7f, 0.15f, Wall);
                if (f == 1) // No bypass around the locked study door.
                    Box("StudyDividerClosure", parent, -3.85f, 1.15f, 0, 1.5f, 2.3f, 0.15f, Wall);
                for (int k = 0; k < 7; k++)
                    Box("CeilingBeam", parent, -5.7f + k * 1.9f, 2.98f, 0, 0.13f, 0.16f, 10, Wood, false);
                for (int k = 0; k < 12; k++)
                    Box("FloorJoin", parent, -5.5f + k, 0.006f, 0, 0.015f, 0.008f, 10, Wood, false);
            }
            Door(ground, "front_door", "Giriş kapısı", "Front door", new Vector3(-0.95f, 0, -4.92f), 1.9f, 0, null, false);
            Door(ground, "living_door", "Salon kapısı", "Living-room door", new Vector3(-1.5f, 0, -2.7f), 1.4f, -90, null, true);
            Door(upper, "bedroom_door", "Yatak odası", "Bedroom", new Vector3(-1.5f, 0, -2.7f), 1.4f, -90, null, true);
            Door(upper, "study_door", "Çalışma odası", "Study", new Vector3(-1.5f, 0, 1.3f), 1.4f, -90, "key", false);
            for (int i = 1; i <= 16; i++)
            {
                float h = i * 0.2f;
                Box("Stair_" + i, ground, 4.5f, h / 2, -3.4f + (i - 0.5f) * 0.425f, 2.2f, h, 0.425f, PaleWood);
                Box("StairEdge", ground, 4.5f, h + 0.005f, -3.4f + (i - 1) * 0.425f, 2.2f, 0.01f, 0.04f, Linen, false);
            }
            Box("UpperGuardRail", upper, 2.98f, 0.65f, -0.05f, 0.12f, 1.3f, 6.7f, Wood);
            // A collider rail prevents stepping from the upper floor into the stair opening.
            for (int i = 0; i < 5; i++) Box("StairPost", ground, 5.65f, 0.65f + i * 0.7f, -3 + i * 1.5f, 0.1f, 1.1f, 0.1f, Wood);
            Box("RoofCeiling", house, 0, 6.35f, 0, 12.5f, 0.2f, 10.5f, Dark);
            var leftRoof = Box("RoofLeft", house, -3.2f, 6.9f, 0, 6.8f, 0.18f, 11.4f, new Color(0.14f, 0.21f, 0.21f), false);
            leftRoof.transform.localEulerAngles = new Vector3(0, 0, 15);
            var rightRoof = Box("RoofRight", house, 3.2f, 6.9f, 0, 6.8f, 0.18f, 11.4f, new Color(0.14f, 0.21f, 0.21f), false);
            rightRoof.transform.localEulerAngles = new Vector3(0, 0, -15);
            foreach (float x in new[] { -5.9f, 5.9f })
                foreach (float z in new[] { -5.12f, 5.12f }) Box("HousePost", house, x, 3.2f, z, 0.18f, 6.4f, 0.18f, Wood);
        }

        static void WindowWall(Transform p, float x, float z, float width)
        {
            Box("WindowLower", p, x, 0.5f, z, width, 1, 0.2f, Wall);
            Box("WindowUpper", p, x, 2.7f, z, width, 0.6f, 0.2f, Wall);
            Box("WindowSide", p, x - 1.85f, 1.7f, z, 1.3f, 1.4f, 0.2f, Wall);
            Box("WindowSide", p, x + 1.85f, 1.7f, z, 1.3f, 1.4f, 0.2f, Wall);
            Box("WindowCenter", p, x, 1.7f, z, 0.08f, 1.4f, 0.18f, Wood);
            Box("WindowCrossbar", p, x, 1.7f, z, 2.4f, 0.08f, 0.18f, Wood);
            Box("WindowSill", p, x, 1.02f, z, 2.6f, 0.08f, 0.35f, PaleWood);
            // Transparent collision plane: prevents stepping through window openings.
            var glass = Group("WindowBarrier", p, new Vector3(x, 1.7f, z));
            var barrier = glass.gameObject.AddComponent<BoxCollider>();
            barrier.size = new Vector3(2.4f, 1.4f, 0.1f);
        }

        static void Partition(Transform p)
        {
            Box("HallWallSouth", p, -1.5f, 1.5f, -3.85f, 0.15f, 3, 2.3f, Wall);
            Box("HallWallMiddle", p, -1.5f, 1.5f, 0, 0.15f, 3, 2.6f, Wall);
            Box("HallWallNorth", p, -1.5f, 1.5f, 3.85f, 0.15f, 3, 2.3f, Wall);
            Box("DoorHeader", p, -1.5f, 2.65f, -2, 0.15f, 0.7f, 1.4f, Wood);
            Box("DoorHeader", p, -1.5f, 2.65f, 2, 0.15f, 0.7f, 1.4f, Wood);
        }

        static KeskeInteractable Door(Transform parent, string id, string tr, string en, Vector3 position, float width, float yaw, string key, bool startOpen)
        {
            var root = Group(id, parent, position);
            root.localEulerAngles = new Vector3(0, yaw, 0);
            var it = Interactive(root, InteractionKind.Door, id, tr, en);
            var pivot = Group("Hinge", root, Vector3.zero);
            it.doorLeaf = pivot;
            it.requiredItem = key;
            it.startOpen = startOpen;
            it.opened = startOpen;
            Box("DoorLeaf", pivot, width / 2, 1.13f, 0, width, 2.26f, 0.12f, Wood);
            Box("DoorInset", pivot, width / 2, 1.42f, -0.075f, width * 0.72f, 1, 0.03f, PaleWood, false);
            Shape("Handle", pivot, new Vector3(width - 0.15f, 1, -0.12f), Vector3.one * 0.13f, Amber, PrimitiveType.Sphere, false);
            return it;
        }

        static Transform Table(Transform parent, string name, Vector3 pos, float width, float depth, float height = 0.8f)
        {
            var t = Group(name, parent, pos);
            Box("Top", t, 0, height, 0, width, 0.1f, depth, PaleWood);
            foreach (float x in new[] { -width * 0.4f, width * 0.4f })
                foreach (float z in new[] { -depth * 0.38f, depth * 0.38f }) Box("Leg", t, x, height / 2, z, 0.09f, height, 0.09f, Wood);
            return t;
        }

        static KeskeInteractable Pickup(Transform parent, string id, string tr, string en, Vector3 pos, Color color)
        {
            var root = Group("Pickup_" + id, parent, pos);
            var it = Interactive(root, InteractionKind.Pickup, id, tr, en);
            var hit = root.gameObject.AddComponent<BoxCollider>();
            hit.center = new Vector3(0, 0.2f, 0);
            hit.size = new Vector3(0.55f, 0.55f, 0.45f);
            if (id == "water" || id == "torch" || id == "batteries")
            {
                Shape("Body", root, new Vector3(0, 0.2f, 0), new Vector3(0.17f, 0.2f, 0.17f), color, PrimitiveType.Cylinder, false);
                Shape("Cap", root, new Vector3(0, 0.42f, 0), new Vector3(0.11f, 0.025f, 0.11f), Linen, PrimitiveType.Cylinder, false);
            }
            else
            {
                Box("Body", root, 0, 0.14f, 0, 0.4f, id == "documents" ? 0.04f : 0.26f, 0.28f, color, false);
                Box("Label", root, 0, 0.15f, -0.145f, 0.18f, 0.1f, 0.015f, Linen, false);
                if (id == "aid")
                {
                    Box("CrossV", root, 0, 0.16f, -0.159f, 0.04f, 0.13f, 0.01f, new Color(0.7f, 0.12f, 0.1f), false);
                    Box("CrossH", root, 0, 0.16f, -0.16f, 0.12f, 0.04f, 0.01f, new Color(0.7f, 0.12f, 0.1f), false);
                }
            }
            it.marker = Shape("InteractMarker", root, new Vector3(0, 0.66f, 0), Vector3.one * 0.065f, Teal, PrimitiveType.Sphere, false, true);
            return it;
        }

        static KeskeRisk Risk(Transform parent, string id, string tr, string en, Vector3 pos, bool tool)
        {
            var root = Group("Risk_" + id, parent, pos);
            Interactive(root, InteractionKind.Risk, id, tr, en);
            var risk = root.gameObject.AddComponent<KeskeRisk>();
            risk.movingPart = Group("MovableVisual", root, Vector3.zero);
            risk.needsTool = tool;
            risk.initialPosition = risk.preparedPosition = Vector3.zero;
            return risk;
        }

        static void BuildGround(Transform ground)
        {
            Box("LivingRug", ground, -3.4f, 0.018f, -2.5f, 3.3f, 0.025f, 3.5f, new Color(0.23f, 0.31f, 0.3f), false);
            Box("SofaBase", ground, -3.6f, 0.35f, -4.3f, 2.2f, 0.7f, 0.7f, new Color(0.3f, 0.33f, 0.29f));
            Box("SofaBack", ground, -3.6f, 0.8f, -4.6f, 2.2f, 0.8f, 0.2f, new Color(0.25f, 0.28f, 0.24f));
            Table(ground, "CoffeeTable", new Vector3(-3.5f, 0, -2.8f), 1.2f, 0.65f, 0.5f);
            Pickup(ground, "torch", "El feneri", "Flashlight", new Vector3(-3.5f, 0.56f, -2.8f), Dark);
            var bookcase = Risk(ground, "bookcase", "Kitaplık", "Bookcase", new Vector3(-5.5f, 0, -1.9f), true);
            Box("Back", bookcase.movingPart, -0.25f, 1.22f, 0, 0.1f, 2.44f, 1.5f, Wood);
            for (int i = 0; i < 5; i++)
            {
                Box("Shelf", bookcase.movingPart, 0, i * 0.52f + 0.1f, 0, 0.6f, 0.07f, 1.5f, PaleWood);
                for (int j = 0; j < 5; j++) Box("Book", bookcase.movingPart, 0.02f, i * 0.52f + 0.29f, -0.55f + j * 0.23f, 0.34f, 0.28f, 0.13f, j % 2 == 0 ? Dark : Linen, false);
            }
            Box("End", bookcase.movingPart, 0, 1.2f, -0.75f, 0.6f, 2.4f, 0.1f, Wood);
            Box("End", bookcase.movingPart, 0, 1.2f, 0.75f, 0.6f, 2.4f, 0.1f, Wood);
            bookcase.aftermathPosition = new Vector3(0.3f, 0.3f, 0);
            bookcase.aftermathEuler = new Vector3(0, 0, -78);
            bookcase.strap = Box("FasteningIndicator", bookcase.movingPart, 0.32f, 1.9f, 0, 0.025f, 0.12f, 1.55f, Teal, false);
            bookcase.strap.SetActive(false);
            bookcase.resultTR = "Kitaplık devrilmiş ve odanın geçişini daraltmış. Görünüşte düzenli bir oda, sabitlenmeyen mobilyalar yüzünden risk taşıyabilir.";
            bookcase.resultEN = "The bookcase fell and narrowed the room's passage. Even a tidy room can contain unsecured furniture hazards.";
            bookcase.learningTR = "Kitaplık için sabitleme önlemi seçtin. Gerçekte uygun bağlantı elemanları ve duvar türüne uygun uygulama gerekir; gerektiğinde uzman desteği alınmalıdır. Bu oyun bağlantının teknik doğruluğunu simüle etmez.";
            bookcase.learningEN = "You selected a fastening measure. Real furniture needs suitable hardware and installation for the wall type, with qualified help when needed. This game does not simulate installation engineering.";
            var route = Risk(ground, "route", "Geçişteki kutular", "Crates in the passage", new Vector3(0.05f, 0, -3.8f), false);
            Box("CrateA", route.movingPart, 0, 0.38f, 0, 0.85f, 0.76f, 0.7f, PaleWood);
            Box("CrateB", route.movingPart, 0.55f, 0.28f, 0.25f, 0.65f, 0.56f, 0.6f, Wood);
            route.preparedPosition = new Vector3(2.6f, 0, -0.65f);
            route.aftermathPosition = new Vector3(0.2f, 0, 0.1f);
            route.aftermathEuler = new Vector3(0, 35, 0);
            route.resultTR = "Girişteki kutular geçişi daraltıyor. Günlük hayatta bırakılan eşyalar, çıkış yolunu kullanmayı zorlaştırabilir.";
            route.resultEN = "The entrance crates narrow the passage. Everyday clutter can make an exit route harder to use.";
            route.learningTR = "Kutuları geçiş yolundan kaldırdın. Ev içindeki çıkış güzergâhlarını önceden belirlemek ve açık tutmak hazırlığın bir parçasıdır.";
            route.learningEN = "You cleared the passage. Identifying routes in advance and keeping them clear is part of preparedness.";
            Table(ground, "KitchenCounter", new Vector3(-5.25f, 0, 2.4f), 0.9f, 3.6f, 0.88f);
            Pickup(ground, "water", "İçme suyu", "Drinking water", new Vector3(-5.15f, 0.94f, 1.3f), new Color(0.2f, 0.53f, 0.68f));
            Box("Sink", ground, -5.25f, 0.95f, 3.1f, 0.65f, 0.08f, 0.8f, Dark, false);
            Table(ground, "DiningTable", new Vector3(-3.2f, 0, 3.7f), 1.5f, 1.1f);
            Pickup(ground, "food", "Dayanıklı gıda", "Shelf-stable food", new Vector3(-3.3f, 0.86f, 3.7f), Amber);
            Table(ground, "Workbench", new Vector3(1.4f, 0, 3.9f), 2.3f, 0.9f);
            Pickup(ground, "tool", "Sabitleme seti", "Fastening kit", new Vector3(0.7f, 0.86f, 3.9f), Amber);
            Pickup(ground, "key", "Çalışma odası anahtarı", "Study key", new Vector3(2.1f, 0.86f, 3.9f), new Color(0.65f, 0.55f, 0.21f));
            Table(ground, "ToolShelf", new Vector3(2, 0, 1.8f), 0.9f, 0.6f, 1.05f);
            Pickup(ground, "batteries", "Yedek piller", "Spare batteries", new Vector3(2, 1.11f, 1.8f), Amber);
            var bag = Group("EmergencyBag", ground, new Vector3(1.6f, 0, -4.25f));
            var bagIt = Interactive(bag, InteractionKind.Bag, "bag", "Afet çantası", "Emergency bag");
            Box("BagBody", bag, 0, 0.4f, 0, 0.65f, 0.8f, 0.4f, Teal);
            Box("BagPocket", bag, 0, 0.35f, -0.25f, 0.45f, 0.32f, 0.1f, Dark);
            Box("BagHandle", bag, 0, 0.84f, 0, 0.28f, 0.08f, 0.1f, Dark, false);
            bagIt.bodyTR = "Çanta, erişilebilir bir yerde hazır. Oyundaki dokuz eşya örnek bir seçkidir; gerçek ihtiyaçlar kişi sayısı, iklim, sağlık durumu, bebekler ve evcil hayvanlara göre değişir. Yiyecek, içecek ve diğer malzemeler düzenli kontrol edilmelidir.";
            bagIt.bodyEN = "The bag is ready in an accessible place. These nine game items are an example selection; actual needs vary with household size, climate, health, babies and pets. Check food, water and other supplies regularly.";
            Note(ground, "memory_1", new Vector3(-2.55f, 0.86f, 3.7f), "Yarım kalan liste", "The unfinished list",
                "\"Sabitleme setini aldık. Kitaplığı yarın hallederiz.\"\n\nNotun tarihi silinmiş. Masanın üzerindeki liste ise hâlâ yarım. Bu evin hikâyesi, ertelenen küçük işlerle değişmiş.",
                "\"We bought the fastening kit. We'll do the bookcase tomorrow.\"\n\nThe date has faded. The list on the table is still unfinished. Small postponed jobs changed this house's story.");
        }

        static void BuildUpper(Transform upper)
        {
            Box("BedBase", upper, -3.5f, 0.3f, -3.4f, 1.7f, 0.6f, 2.4f, Wood);
            Box("Mattress", upper, -3.5f, 0.67f, -3.4f, 1.65f, 0.2f, 2.3f, Linen);
            Box("Pillow", upper, -3.5f, 0.82f, -4.15f, 1.1f, 0.15f, 0.45f, new Color(0.8f, 0.77f, 0.64f), false);
            Pickup(upper, "blanket", "Battaniye", "Blanket", new Vector3(-3.5f, 0.8f, -2.6f), new Color(0.4f, 0.51f, 0.5f));
            var wardrobe = Risk(upper, "wardrobe", "Gardırop", "Wardrobe", new Vector3(-5.15f, 0, -3.75f), true);
            Box("Cabinet", wardrobe.movingPart, 0, 1.15f, 0, 1.4f, 2.3f, 0.7f, Wood);
            Box("DoorPanelA", wardrobe.movingPart, -0.34f, 1.18f, 0.365f, 0.64f, 2.12f, 0.03f, PaleWood, false);
            Box("DoorPanelB", wardrobe.movingPart, 0.34f, 1.18f, 0.365f, 0.64f, 2.12f, 0.03f, PaleWood, false);
            wardrobe.aftermathPosition = new Vector3(0, 0.37f, 0.3f);
            wardrobe.aftermathEuler = new Vector3(78, 0, 0);
            wardrobe.strap = Box("FasteningIndicator", wardrobe.movingPart, 0, 1.9f, 0.39f, 1.4f, 0.1f, 0.04f, Teal, false);
            wardrobe.strap.SetActive(false);
            wardrobe.resultTR = "Gardırop devrilmiş. Yatak odasındaki ağır mobilyalar, dinlenme alanının çevresinde de tehlike oluşturabilir.";
            wardrobe.resultEN = "The wardrobe has fallen. Heavy furniture can create hazards around a sleeping area too.";
            wardrobe.learningTR = "Gardırop için sabitleme önlemi seçtin. Eşyaların sabitlenmesi, ev içindeki bazı tehlikeleri azaltır; binanın yapısal güvenliği hakkında bilgi vermez.";
            wardrobe.learningEN = "You selected a fastening measure for the wardrobe. Securing furniture reduces some indoor hazards; it does not assess a building's structural safety.";
            Table(upper, "LandingTable", new Vector3(1.75f, 0, -3.95f), 1.4f, 0.6f, 0.8f);
            Pickup(upper, "aid", "İlk yardım çantası", "First-aid kit", new Vector3(1.7f, 0.86f, -3.95f), Linen);
            var heavy = Risk(upper, "heavy", "Ağır seramik eşya", "Heavy ceramic object", new Vector3(1.75f, 0, 2.2f), false);
            Box("ShelfBack", heavy.transform, 0, 1.05f, 0.28f, 1, 2.1f, 0.1f, Wood);
            Box("HighShelf", heavy.transform, 0, 1.85f, 0, 1, 0.1f, 0.65f, PaleWood);
            Box("LowShelf", heavy.transform, 0, 0.2f, 0, 1, 0.1f, 0.65f, PaleWood);
            heavy.initialPosition = new Vector3(0, 2.1f, 0);
            heavy.preparedPosition = new Vector3(0, 0.46f, 0);
            heavy.aftermathPosition = new Vector3(-0.65f, 0.22f, -0.7f);
            heavy.aftermathEuler = new Vector3(80, 20, 0);
            heavy.movingPart.localPosition = heavy.initialPosition;
            Shape("CeramicBody", heavy.movingPart, Vector3.zero, new Vector3(0.38f, 0.48f, 0.38f), Amber, PrimitiveType.Sphere);
            Table(upper, "StudyDesk", new Vector3(-4.9f, 0, 3.7f), 1.5f, 1.1f, 0.82f);
            Pickup(upper, "radio", "Pilli radyo", "Battery radio", new Vector3(-4.9f, 0.88f, 3.7f), Dark);
            Table(upper, "DocumentTable", new Vector3(-2.5f, 0, 4.2f), 1.1f, 0.65f);
            Pickup(upper, "documents", "Belge kopyaları", "Document copies", new Vector3(-2.5f, 0.86f, 4.2f), Linen);
            heavy.resultTR = "Yüksekteki ağır seramik eşya düşmüş. Küçük görünen bir nesnenin konumu, taşıdığı riski değiştirebilir.";
            heavy.resultEN = "The heavy ceramic object fell from a high shelf. An object's location can change the risk it presents.";
            heavy.learningTR = "Ağır eşyayı alt rafa indirdin. Ağır ve büyük eşyaları alt raflarda tutmak, düşme kaynaklı bazı tehlikeleri azaltmaya yardımcı olur.";
            heavy.learningEN = "You moved the heavy object to the lower shelf. Keeping heavy and large objects low helps reduce some falling-object hazards.";
            var board = Group("FamilyPlan", upper, new Vector3(-5.8f, 1.5f, 1.5f));
            var plan = Interactive(board, InteractionKind.Plan, "plan", "Aile buluşma planı", "Family meeting plan");
            Box("Board", board, 0, 0, 0, 0.12f, 1.2f, 1.4f, Dark);
            Box("Paper", board, 0.07f, 0, 0, 0.025f, 0.9f, 1.1f, Linen, false);
            Box("Marker", board, 0.09f, 0.28f, 0, 0.03f, 0.08f, 0.9f, Teal, false);
            plan.bodyTR = "Aile ayrı yerlerde olabilir. Sarsıntı sona erdikten ve çevre güvenliği değerlendirildikten sonraki iletişim için hangi planı önceden kararlaştırırsın?";
            plan.bodyEN = "Family members may be in different places. Which communication plan would you agree on beforehand for after the shaking has stopped and conditions have been assessed?";
            Note(upper, "memory_2", new Vector3(-4.2f, 0.88f, 3.7f), "Cevapsız not", "An unanswered note",
                "\"Ayrı düşersek nerede buluşacağız?\"\n\nCevap için ayrılan satır boş. Bir hazırlık listesi, birlikte konuşulmadığında eksik kalır.",
                "\"Where do we meet if we are separated?\"\n\nThe answer line is empty. A preparation list is incomplete until you talk about it together.");
            Note(upper, "memory_3", new Vector3(2.1f, 0.86f, -3.95f), "Prova kaydı", "Training log",
                "Bu ev, ev içi hazırlık kararlarını görünür kılmak için kurulmuş bir simülasyondur. Gördüğün sonuçlar önceden yazılmış örneklerdir; gerçek bir depremin, binanın veya eşyanın davranışını hesaplamaz.\n\nBuradaki gölge, geçmişte bırakılmış bir hatıradır.",
                "This house is a simulation that makes indoor preparation choices visible. Its outcomes are scripted examples, not calculations of an actual earthquake, building or object.\n\nThe shadow here is a memory left behind.");
        }

        static void Note(Transform parent, string id, Vector3 pos, string titleTR, string titleEN, string tr, string en)
        {
            var root = Group("Note_" + id, parent, pos);
            var note = Interactive(root, InteractionKind.Note, id, titleTR, titleEN);
            note.bodyTR = tr; note.bodyEN = en;
            Box("Paper", root, 0, 0.015f, 0, 0.34f, 0.03f, 0.26f, Linen);
            Box("Ink", root, 0, 0.034f, 0, 0.23f, 0.01f, 0.1f, Wood, false);
        }

        static void BuildGardenGameplay(Transform garden)
        {
            var bench = Table(garden, "GardenBench", new Vector3(-3.8f, 0, -8.5f), 2.2f, 0.65f, 0.48f);
            Box("Back", bench, 0, 0.85f, 0.3f, 2.2f, 0.6f, 0.1f, Wood);
            Pickup(garden, "whistle", "Düdük", "Whistle", new Vector3(-3.8f, 0.54f, -8.5f), Amber);
            var terminal = Group("FinishTerminal", garden, new Vector3(4, 0, -11));
            Interactive(terminal, InteractionKind.Finish, "finish", "Prova terminali", "Training terminal");
            Box("Base", terminal, 0, 0.7f, 0, 0.9f, 1.4f, 0.65f, Dark);
            Shape("Screen", terminal, new Vector3(0, 1.3f, -0.34f), new Vector3(0.7f, 0.45f, 0.035f), Teal, PrimitiveType.Cube, false, true);
            Box("Top", terminal, 0, 1.65f, 0, 1, 0.12f, 0.8f, Teal);
        }

        static void BuildLights(KeskeGame game, Transform root)
        {
            var sunObject = Group("EveningLight", root, Vector3.zero);
            var sun = sunObject.gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.9f, 0.77f, 0.57f);
            sun.intensity = 0.65f;
            sun.shadows = LightShadows.Soft;
            sunObject.rotation = Quaternion.Euler(32, -35, 0);
            game.sun = sun;
            RenderSettings.sun = sun;
            var lights = new List<Light>();
            foreach (Vector3 pos in new[] {
                new Vector3(-3.6f, 2.65f, -2.3f), new Vector3(-3.6f, 2.65f, 2.7f),
                new Vector3(1.4f, 2.65f, -1), new Vector3(2.7f, 2.65f, 3.7f),
                new Vector3(-3.6f, 5.85f, -2.3f), new Vector3(-3.6f, 5.85f, 2.7f),
                new Vector3(0.7f, 5.85f, 0), new Vector3(0, 2.5f, -6.1f) })
            {
                var holder = Group("HouseLamp", root, pos);
                Shape("Bulb", holder, Vector3.zero, Vector3.one * 0.14f, Amber, PrimitiveType.Sphere, false, true);
                var lamp = holder.gameObject.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.range = 7;
                lamp.intensity = 1.15f;
                lamp.color = new Color(1, 0.75f, 0.48f);
                lamp.shadows = LightShadows.None;
                lights.Add(lamp);
            }
            game.houseLights = lights.ToArray();
        }

        static void BuildPlayer(KeskeGame game)
        {
            var root = Group("Player", game.transform, new Vector3(0, 0.08f, -8));
            var cc = root.gameObject.AddComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.27f;
            cc.center = new Vector3(0, 0.88f, 0);
            cc.stepOffset = 0.28f;
            cc.slopeLimit = 50;
            cc.skinWidth = 0.035f;
            cc.minMoveDistance = 0;
            var player = root.gameObject.AddComponent<KeskePlayer>();
            game.player = player;
            player.game = game;
            var head = Group("View", root, new Vector3(0, 1.58f, 0));
            var camera = head.gameObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 72;
            camera.nearClipPlane = 0.06f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.12f, 0.12f);
            head.gameObject.AddComponent<AudioListener>();
            player.view = camera;
            var lightObj = Group("TrainingTorch", head, new Vector3(0.16f, -0.12f, 0.15f));
            var light = lightObj.gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 17;
            light.spotAngle = 62;
            light.intensity = 3;
            light.color = new Color(0.82f, 0.91f, 1);
            light.shadows = LightShadows.Soft;
            player.torch = light;
        }
    }
}
