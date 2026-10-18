using System;
using System.Collections.Generic;
using UnityEngine;

namespace Keske
{
    // Builds gameplay around the authored furniture without changing its asset files.
    [DefaultExecutionOrder(-200)]
    public sealed class KeskeSceneBridge : MonoBehaviour
    {
        public GameObject blanketPrefab;
        public GameObject toolboxPrefab;
        public GameObject cratePrefab;
        public Material bagMaterial;
        readonly Dictionary<string, Transform> authored = new Dictionary<string, Transform>();
        readonly List<Material> generatedMaterials = new List<Material>();
        Transform world;
        KeskeGame game;
        Material teal, dark, amber, paper;

        void Awake()
        {
            world = new GameObject("Keske_AuthoredHouse").transform;
            var roots = gameObject.scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root == gameObject || root == world.gameObject) continue;
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    if (!authored.ContainsKey(tr.name)) authored.Add(tr.name, tr);
                root.transform.SetParent(world, true);
            }
            foreach (var camera in world.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var listener in world.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (string name in new[] { "PlayerCapsule", "Canvas", "EventSystem" })
            {
                Transform tr;
                if (authored.TryGetValue(name, out tr)) tr.gameObject.SetActive(false);
            }
            teal = Material("Keske_Teal", new Color(0.12f, 0.58f, 0.49f));
            dark = Material("Keske_Charcoal", new Color(0.13f, 0.17f, 0.19f));
            amber = Material("Keske_Amber", new Color(0.95f, 0.58f, 0.16f));
            paper = Material("Keske_Paper", new Color(0.95f, 0.93f, 0.81f));
            game = gameObject.AddComponent<KeskeGame>();
            game.worldRoot = world;
            game.spawnPosition = new Vector3(3.7f, 0.66f, 0);
            game.spawnYaw = -90;
            game.observationPosition = new Vector3(1.5f, 0.66f, 2.1f);
            game.observationYaw = -90;
            foreach (var light in world.GetComponentsInChildren<Light>(true))
                if (light.type == LightType.Directional) { game.sun = light; break; }
            var lamps = new List<Light>();
            foreach (var light in world.GetComponentsInChildren<Light>(true))
                if (light.type != LightType.Directional) lamps.Add(light);
            game.houseLights = lamps.ToArray();
            RemoveDuplicateFurnitureBoxes();
            CreatePlayer();
            ConnectRisks();
            ConnectSupplies();
            ConnectDoors();
            ConnectTraining();
            RenderSettings.fog = false;
            Debug.Log("[KESKE] Authored house connected. Original KeskePlayer active; Starter Assets player disabled.");
        }

        // Some authored prefab instances have oversized extra boxes above their mesh.
        // Keep their accurate mesh collision and disable only duplicate boxes on the same object.
        void RemoveDuplicateFurnitureBoxes()
        {
            foreach (var mesh in world.GetComponentsInChildren<MeshCollider>(true))
            {
                if (!mesh.enabled || mesh.isTrigger || mesh.sharedMesh == null) continue;
                foreach (var box in mesh.GetComponents<BoxCollider>()) box.enabled = false;
            }
        }

        Transform Find(string name)
        {
            Transform tr;
            if (!authored.TryGetValue(name, out tr))
                throw new InvalidOperationException("[KESKE] Required house object missing: " + name);
            return tr;
        }

        Material Material(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name, color = color };
            generatedMaterials.Add(mat);
            return mat;
        }

        Transform Group(string name, Vector3 position, Transform parent = null)
        {
            var tr = new GameObject(name).transform;
            tr.SetParent(parent != null ? parent : world, false);
            tr.position = position;
            return tr;
        }

        GameObject Shape(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, PrimitiveType primitive = PrimitiveType.Cube, bool solid = true)
        {
            var obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { var collider = obj.GetComponent<Collider>(); collider.enabled = false; Destroy(collider); }
            return obj;
        }

        GameObject Prefab(GameObject prefab, string name, Vector3 position)
        {
            if (prefab == null) throw new InvalidOperationException("[KESKE] Missing prefab: " + name);
            var obj = Instantiate(prefab, position, Quaternion.identity, world);
            obj.name = name;
            return obj;
        }

        // Root-space bounds work for rotated/scaled FBX files as well as simple prefabs.
        static Bounds LocalBounds(Transform root)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = renderer.localBounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    Vector3 local = root.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (first) { bounds = new Bounds(local, Vector3.zero); first = false; }
                    else bounds.Encapsulate(local);
                }
            }
            return bounds;
        }

        static void EnsureCollider(Transform root, bool fitWholeObject = false)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                if (fitWholeObject) c.enabled = false;
                else if (c.enabled && !c.isTrigger) return;
            }
            Bounds b = LocalBounds(root);
            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = b.center;
            // Minimum dimensions are in world units even for FBXs with large import scales.
            Vector3 s = root.lossyScale;
            box.size = new Vector3(Mathf.Max(b.size.x, 0.18f / Mathf.Max(0.001f, Mathf.Abs(s.x))),
                Mathf.Max(b.size.y, 0.18f / Mathf.Max(0.001f, Mathf.Abs(s.y))),
                Mathf.Max(b.size.z, 0.18f / Mathf.Max(0.001f, Mathf.Abs(s.z))));
        }

        KeskeInteractable Interact(Transform root, InteractionKind kind, string id, string tr, string en)
        {
            if (kind != InteractionKind.Risk) EnsureCollider(root, kind == InteractionKind.Pickup);
            var item = root.gameObject.AddComponent<KeskeInteractable>();
            item.kind = kind; item.id = id; item.titleTR = tr; item.titleEN = en;
            return item;
        }

        KeskeRisk Risk(Transform model, string id, string tr, string en, bool tool,
            Vector3 outcomePosition, Vector3 outcomeEuler, Vector3 preparedPosition)
        {
            // A unit-scale parent isolates gameplay poses from imported mesh scale and rotation.
            var root = Group("Risk_" + id, model.position);
            var moving = Group("Moving_" + id, model.position, root);
            model.SetParent(moving, true);
            foreach (var trf in model.GetComponentsInChildren<Transform>(true)) trf.gameObject.isStatic = false;
            EnsureCollider(moving);
            Interact(root, InteractionKind.Risk, id, tr, en);
            var risk = root.gameObject.AddComponent<KeskeRisk>();
            risk.movingPart = moving;
            risk.initialPosition = Vector3.zero; risk.initialEuler = Vector3.zero;
            risk.preparedPosition = preparedPosition; risk.preparedEuler = Vector3.zero;
            risk.aftermathPosition = outcomePosition; risk.aftermathEuler = outcomeEuler;
            risk.needsTool = tool;
            if (tool)
            {
                Bounds b = LocalBounds(moving);
                risk.strap = new GameObject("FasteningIndicators");
                risk.strap.transform.SetParent(moving, false);
                // Bands on both visible axes remain apparent from different approaches.
                float bandY = b.center.y;
                Shape("FrontBand", risk.strap.transform, new Vector3(b.center.x, bandY, b.max.z + 0.025f),
                    new Vector3(Mathf.Max(0.2f, b.size.x), 0.09f, 0.04f), teal, PrimitiveType.Cube, false);
                Shape("BackBand", risk.strap.transform, new Vector3(b.center.x, bandY, b.min.z - 0.025f),
                    new Vector3(Mathf.Max(0.2f, b.size.x), 0.09f, 0.04f), teal, PrimitiveType.Cube, false);
                foreach (float x in new[] { b.min.x - 0.025f, b.max.x + 0.025f })
                    Shape("SideBand", risk.strap.transform, new Vector3(x, bandY, b.center.z),
                        new Vector3(0.04f, 0.09f, Mathf.Max(0.2f, b.size.z)), teal, PrimitiveType.Cube, false);
                risk.strap.SetActive(false);
            }
            return risk;
        }

        void ConnectRisks()
        {
            var shelf = Find("Storage");
            var bookcase = Risk(shelf, "bookcase", "Çalışma alanındaki raf", "Study shelving", true,
                new Vector3(-0.55f, 0.14f, 0), new Vector3(0, 0, 78), Vector3.zero);
            bookcase.resultTR = "Sabitlenmeyen raf devrilmiş ve çalışma alanını daraltmış.";
            bookcase.resultEN = "The unsecured shelving has fallen into the study area.";
            bookcase.learningTR = "Raf için sabitleme önlemi aldın. Gerçek uygulamada mobilyaya ve duvara uygun bağlantı elemanları gerekir.";
            bookcase.learningEN = "You secured the shelving. Real installation requires hardware suitable for the furniture and wall.";
            // Loose shelf decorations belong to the moving furniture too.
            foreach (string name in new[] { "SM_LPI_Decor_Magazine_01", "SM_LPI_Plant_Herb_01",
                "SM_LPI_Office_PaperStack_01", "SM_LPI_Decor_Book_01", "SM_LPI_Office_DocumentTray_01",
                "SM_LPI_Decor_BookStack_01", "SM_LPI_Office_BinderRow_01" })
                Find(name).SetParent(bookcase.movingPart, true);

            var wardrobe = Risk(Find("Dolap"), "wardrobe", "Yatak odasındaki dolap", "Bedroom wardrobe", true,
                new Vector3(0, 0.16f, 0.5f), new Vector3(78, 0, 0), Vector3.zero);
            wardrobe.resultTR = "Sabitlenmeyen dolap yatağın yakınındaki alana devrilmiş.";
            wardrobe.resultEN = "The unsecured wardrobe has fallen near the sleeping area.";
            wardrobe.learningTR = "Dolap için sabitleme önlemi aldın. Bu önlem bazı eşya risklerini azaltır; binanın yapısal güvenliğini değerlendirmez.";
            wardrobe.learningEN = "You secured the wardrobe. This reduces some furniture risks; it does not assess structural safety.";

            var television = Risk(Find("Televizyon"), "television", "Salondaki televizyon", "Living-room television", true,
                new Vector3(0.65f, -0.45f, 0), new Vector3(0, 0, -65), Vector3.zero);
            television.resultTR = "Sabitlenmeyen televizyon üniteden kaymış ve devrilmiş.";
            television.resultEN = "The unsecured television slid off its stand and toppled.";
            television.learningTR = "Televizyon sabitlendi. Turkuaz bantlar tamamlanan önlemi gösterir. Gerçek kurulumda televizyona ve bağlantı yüzeyine uygun devrilme önleyici bağlantı kullanılır.";
            television.learningEN = "Television secured. Teal bands mark the completed preparation. Real installation needs anti-tip hardware suitable for the television and mounting surface.";
            var dryer = Risk(Find("Kurutma"), "dryer", "Banyodaki üst kurutma makinesi", "Upper bathroom dryer", true,
                new Vector3(-0.7f, -0.95f, -0.45f), new Vector3(0, 0, 65), Vector3.zero);
            dryer.resultTR = "Üstteki sabitlenmeyen kurutma makinesi yerinden kaymış ve düşmüş.";
            dryer.resultEN = "The unsecured upper dryer shifted and fell.";
            dryer.learningTR = "Kurutma makinesi sabitlendi. Turkuaz bantlar tamamlanan önlemi gösterir. Gerçekte üst üste kurulum için üreticinin uyumlu bağlantı kiti ve yetkili servis uygulaması gerekir.";
            dryer.learningEN = "Dryer secured. Teal bands mark the completed preparation. Real stacked installation requires the manufacturer's compatible stacking kit and qualified installation.";

            var jar = Find("Şekerlik");
            var heavy = Risk(jar, "heavy", "Yüksek raftaki kavanoz", "Jar on a high shelf", false,
                new Vector3(0, -2.48f, 0.7f), new Vector3(80, 20, 0), new Vector3(0.485f, -2.1f, 1.736f));
            heavy.resultTR = "Yüksekte bırakılan kavanoz düşmüş. Eşyanın konumu düşme riskini etkiler.";
            heavy.resultEN = "The jar fell from the high shelf. Placement affects falling-object hazards.";
            heavy.learningTR = "Kavanozu alt konuma aldın. Ağır ve kırılabilir eşyaları yüksek raflarda bırakmamak hazırlığın bir parçasıdır.";
            heavy.learningEN = "You moved the jar lower. Keeping heavy and breakable items off high shelves is part of preparation.";
            // A visible low stand supports the prepared pose instead of leaving the jar floating.
            Shape("LowSafetyStand", world, new Vector3(-5.8f, 0.84f, -1.6f),
                new Vector3(0.58f, 0.48f, 0.55f), dark);

            var crates = Group("PassageCrates", new Vector3(3.45f, 0.61f, -2.55f));
            var a = Prefab(cratePrefab, "PassageBox_A", crates.position);
            a.transform.SetParent(crates, true);
            var b = Prefab(cratePrefab, "PassageBox_B", crates.position + new Vector3(-0.6f, 0, 0.25f));
            b.transform.SetParent(crates, true);
            var route = Risk(crates, "route", "Geçişteki kutular", "Boxes in the passage", false,
                new Vector3(-0.15f, 0, 0.1f), new Vector3(0, 25, 0), new Vector3(-1.9f, 0, 0.6f));
            route.resultTR = "Kutular geçişi daraltıyor. Çıkış güzergâhını önceden açık tutmak gerekir.";
            route.resultEN = "Boxes narrow the passage. Keep exit routes clear in advance.";
            route.learningTR = "Kutuları geçişten kaldırdın. Hazırlık sırasında çıkış yollarını belirle ve açık tut.";
            route.learningEN = "You cleared the boxes. Identify exit routes and keep them clear during preparation.";
        }

        void Pickup(Transform root, string id, string tr, string en)
        {
            Interact(root, InteractionKind.Pickup, id, tr, en);
        }

        public static Bounds WorldBounds(Transform root)
        {
            bool first = true;
            Bounds result = new Bounds(root.position, Vector3.zero);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds local = renderer.localBounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    Vector3 point = renderer.transform.TransformPoint(corner);
                    if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                    else result.Encapsulate(point);
                }
            }
            return result;
        }

        public static void PlaceOnSurface(Transform item, Transform surface, Vector2 offset)
        {
            Bounds top = WorldBounds(surface);
            Bounds body = WorldBounds(item);
            float x = top.center.x + offset.x, z = top.center.z + offset.y;
            float supportY = float.NegativeInfinity;
            // Sample the surface under the item, excluding headboards and other tall edges.
            Physics.SyncTransforms();
            var ray = new Ray(new Vector3(x, top.max.y + 1, z), Vector3.down);
            foreach (var collider in surface.GetComponentsInChildren<MeshCollider>())
            {
                RaycastHit hit;
                if (collider.enabled && !collider.isTrigger && collider.Raycast(ray, out hit, top.size.y + 2))
                    supportY = Mathf.Max(supportY, hit.point.y);
            }
            if (float.IsNegativeInfinity(supportY)) supportY = top.max.y;
            item.position += new Vector3(x - body.center.x, supportY + 0.002f - body.min.y, z - body.center.z);
        }

        void ConnectSupplies()
        {
            PlaceOnSurface(Find("bottle"), Find("SM_LPI_Storage_TVUnit_01"), new Vector2(0, 0.65f));
            Pickup(Find("bottle"), "water", "İçme suyu", "Drinking water");
            Pickup(Find("Konserve"), "food", "Dayanıklı gıda", "Shelf-stable food");
            PlaceOnSurface(Find("FirstAidKit"), Find("SM_LPI_Kitchen_Base_01"), new Vector2(-0.12f, 0));
            Pickup(Find("FirstAidKit"), "aid", "İlk yardım çantası", "First-aid kit");
            // Both battery meshes are collected together.
            var batteries = Group("SpareBatteries", Find("Battery").position);
            Find("Battery").SetParent(batteries, true);
            Find("Battery (1)").SetParent(batteries, true);
            Pickup(batteries, "batteries", "Yedek piller", "Spare batteries");
            Pickup(Find("SM_LPI_Office_PaperStack_01 (1)"), "documents", "Belge kopyaları", "Document copies");
            var blanket = Prefab(blanketPrefab, "EmergencyBlanket", new Vector3(3.56f, 1.3f, -12.2f));
            PlaceOnSurface(blanket.transform, Find("SM_LPI_Bed_Double_01"), new Vector2(0.2f, 0));
            Pickup(blanket.transform, "blanket", "Battaniye", "Blanket");
            var toolbox = Prefab(toolboxPrefab, "FasteningKit", new Vector3(1.15f, 1.73f, -7.9f));
            Pickup(toolbox.transform, "tool", "Sabitleme seti", "Fastening kit");

            var radio = Group("EmergencyRadio", new Vector3(2.05f, 1.75f, -8.0f));
            Shape("RadioBody", radio, new Vector3(0, 0.12f, 0), new Vector3(0.34f, 0.24f, 0.12f), teal);
            Shape("Speaker", radio, new Vector3(-0.075f, 0.12f, -0.067f), new Vector3(0.14f, 0.008f, 0.14f), dark, PrimitiveType.Cylinder, false).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Shape("Tuner", radio, new Vector3(0.09f, 0.18f, -0.068f), new Vector3(0.095f, 0.035f, 0.01f), paper, PrimitiveType.Cube, false);
            Shape("Antenna", radio, new Vector3(0.12f, 0.35f, 0), new Vector3(0.009f, 0.14f, 0.009f), dark, PrimitiveType.Cylinder, false);
            Pickup(radio, "radio", "Pilli radyo", "Battery radio");

            Transform torch;
            if (!authored.TryGetValue("EmergencyFlashlight", out torch))
            {
                torch = Group("EmergencyFlashlight", new Vector3(-6.4f, 1.25f, 4.7f));
                Shape("Grip", torch, new Vector3(0, 0.065f, 0), new Vector3(0.07f, 0.11f, 0.07f), dark, PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(90, 0, 0);
                Shape("Lens", torch, new Vector3(0, 0.065f, 0.12f), new Vector3(0.105f, 0.025f, 0.105f), amber, PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            foreach (var renderer in torch.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = renderer.name == "Lens" ? amber : dark;
            PlaceOnSurface(torch, Find("SM_LPI_Table_Coffee_03"), Vector2.zero);
            Pickup(torch, "torch", "El feneri", "Flashlight");

            // Preserve the designer's position, rotation, size and material on the dining table.
            Pickup(Find("whistle (1)"), "whistle", "Düdük", "Whistle");
        }

        void ConnectDoors()
        {
            foreach (string name in new[] { "SM_LPI_Open_DoorLeaf_01 (2)", "SM_LPI_Open_DoorLeaf_01 (3)", "SM_LPI_Open_DoorLeaf_01 (4)" })
            {
                var model = Find(name);
                // This asset has a bottom-centre origin; its handle is on imported -X.
                // Rotate a unit-scale vertical hinge at +X, preserving the authored pose.
                Bounds bounds = model.GetComponent<MeshFilter>().sharedMesh.bounds;
                Vector3 hingePosition = model.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.center.z));
                var root = Group("Door_" + name, hingePosition);
                root.rotation = Quaternion.Euler(0, model.eulerAngles.y, 0);
                model.SetParent(root, true);
                foreach (var tr in model.GetComponentsInChildren<Transform>(true)) tr.gameObject.isStatic = false;
                var door = Interact(root, InteractionKind.Door, name, "Kapı", "Door");
                door.doorLeaf = root;
                door.openAngle = 95;
                door.openAwayFromPlayer = true;
                door.doorCenter = root.InverseTransformPoint(model.TransformPoint(bounds.center));
            }
        }

        void Inspect(Transform root, string id, string tr, string en, string bodyTR, string bodyEN, bool memory = false)
        {
            var item = Interact(root, InteractionKind.Note, id, tr, en);
            item.bodyTR = bodyTR; item.bodyEN = bodyEN; item.recordMemory = memory;
        }

        void ConnectTraining()
        {
            var bag = Find("Backpack");
            
            var bagItem = Interact(bag, InteractionKind.Bag, "bag", "Afet çantası", "Emergency bag");
            bagItem.bodyTR = "Dokuz örnek malzemeyi topla ve çantaya yerleştir. Gerçek çanta içeriği hane halkının özel ihtiyaçlarına göre değişir.";
            bagItem.bodyEN = "Collect the nine example supplies and pack the bag. Actual contents depend on household needs.";
            var plan = Interact(Find("Pano"), InteractionKind.Plan, "plan", "Aile buluşma planı", "Family meeting plan");
            plan.bodyTR = "Aile bireyleri ayrı yerlerde olabilir. Sarsıntı sonrasındaki iletişim için hangi planı önceden kararlaştırırsın?";
            plan.bodyEN = "Family members may be apart. Which communication plan would you agree on in advance?";
            Inspect(Find("SM_LPI_Appl_Hob_01"), "inspect_stove", "Ocak ve mutfak", "Hob and kitchen",
                "Mutfaktaki yüksek rafları incele. Bu ocak bilgi noktasıdır; oyunda gaz vanası işlemi simüle edilmez.",
                "Inspect the high kitchen shelves. This hob is an information point; gas-valve procedures are not simulated.");
            Inspect(Find("SM_LPI_Util_Extinguisher_01"), "inspect_extinguisher", "Yangın söndürücü", "Fire extinguisher",
                "Yangın söndürücünün yerini önceden bilmek hazırlığın bir parçasıdır. Bu nesne inceleme içindir; çanta malzemesi değildir.",
                "Knowing the extinguisher location is part of preparation. This is an inspection point, not a bag supply.");
            int index = 0;
            // Name duplicates exist in the authored scene, so enumerate every window transform.
            var windows = new List<Transform>();
            foreach (var tr in world.GetComponentsInChildren<Transform>(true))
                if (tr.name.StartsWith("SM_LPI_Open_Window_01", StringComparison.Ordinal)) windows.Add(tr);
            foreach (var window in windows)
                Inspect(window, "inspect_window_" + index++, "Pencere ve cam", "Window and glass",
                    "Pencere çevresindeki düşebilecek eşyaları fark et. Bu pencere inceleme noktasıdır; cam kırılması bu sürümde simüle edilmez.",
                    "Notice objects that could fall near windows. This is an inspection point; glass breakage is not simulated in this version.");

            CreateNote("memory_1", new Vector3(-6.0f, 1.25f, 4.9f), "Ertelenen iş", "A postponed job",
                "Sabitleme setini aldık. Rafı yarın hallederiz...\n\nÇalışma alanındaki rafı kontrol et. Küçük bir hazırlığı ertelemek sonucu değiştirebilir.",
                "We bought the kit. We will secure the shelving tomorrow...\n\nCheck the study shelving. A postponed preparation can change the outcome.");
            CreateNote("memory_2", new Vector3(2.85f, 1.74f, -7.95f), "Eksik plan", "An unfinished plan",
                "Ayrı yerlerde olursak nerede buluşacağız?\n\nPano üzerinden aile planını tamamla.",
                "Where will we meet if we are apart?\n\nComplete the family plan at the board.");
            CreateNote("memory_3", new Vector3(4.1f, 1.77f, -1.12f), "Tatbikat notu", "Training note",
                "Q ile hazırlık ve sonuç arasında geçebilirsin. Sonuçlar önceden yazılmış örneklerdir; gerçek deprem fiziğini hesaplamaz.",
                "Q switches preparation and outcome. Outcomes are scripted examples, not earthquake physics.");
            var terminal = Group("TrainingFinishTerminal", new Vector3(3.9f, 0.61f, 2.5f));
            Shape("Pedestal", terminal, new Vector3(0, 0.5f, 0), new Vector3(0.34f, 1, 0.32f), dark);
            Shape("Screen", terminal, new Vector3(0, 1.15f, 0), new Vector3(0.55f, 0.38f, 0.12f), teal);
            Interact(terminal, InteractionKind.Finish, "finish", "Tatbikatı tamamla", "Complete training");
        }

        void CreateNote(string id, Vector3 position, string tr, string en, string bodyTR, string bodyEN)
        {
            var note = Group("Note_" + id, position);
            Shape("Paper", note, Vector3.zero, new Vector3(0.22f, 0.015f, 0.3f), paper);
            if (id == "memory_1") PlaceOnSurface(note, Find("SM_LPI_Table_Coffee_03"), new Vector2(0.28f, -0.18f));
            else if (id == "memory_2") PlaceOnSurface(note, Find("SM_LPI_Office_Desk_01"), new Vector2(0.6f, -0.1f));
            else PlaceOnSurface(note, Find("Backpack"), Vector2.zero);
            Inspect(note, id, tr, en, bodyTR, bodyEN, true);
        }

        void CreatePlayer()
        {
            var root = Group("Player_Keske_Original", game.spawnPosition, transform);
            var cc = root.gameObject.AddComponent<CharacterController>();
            cc.height = 1.75f; cc.radius = 0.27f; cc.center = new Vector3(0, 0.88f, 0);
            cc.stepOffset = 0.28f; cc.slopeLimit = 50; cc.skinWidth = 0.035f; cc.minMoveDistance = 0;
            var player = root.gameObject.AddComponent<KeskePlayer>();
            game.player = player; player.game = game;
            var head = new GameObject("View").transform;
            head.SetParent(root, false); head.localPosition = new Vector3(0, 1.58f, 0);
            var camera = head.gameObject.AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.fieldOfView = 72;
            camera.nearClipPlane = 0.06f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.Skybox;
            head.gameObject.AddComponent<AudioListener>();
            player.view = camera;
            var torchObject = new GameObject("TrainingTorch");
            torchObject.transform.SetParent(head, false);
            torchObject.transform.localPosition = new Vector3(0.16f, -0.12f, 0.15f);
            var light = torchObject.AddComponent<Light>();
            light.type = LightType.Spot; light.range = 17; light.spotAngle = 62;
            light.intensity = 3; light.color = new Color(0.82f, 0.91f, 1); light.shadows = LightShadows.Soft;
            light.enabled = false; player.torch = light;
        }

        void OnDestroy()
        {
            foreach (var material in generatedMaterials) if (material != null) Destroy(material);
        }
    }
}
