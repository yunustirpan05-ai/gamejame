using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Keske.Tests
{
    public class KeskeIntegrationTests
    {
        const string SaveKey = "KESKE_MERGED_PROGRESS_V2";
        bool hadSave;
        string previousSave;

        [SetUp]
        public void PreservePlayerProgress()
        {
            hadSave = PlayerPrefs.HasKey(SaveKey);
            previousSave = PlayerPrefs.GetString(SaveKey, "");
        }

        [TearDown]
        public void RestorePlayerProgress()
        {
            if (hadSave) PlayerPrefs.SetString(SaveKey, previousSave);
            else PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            Time.timeScale = 1;
        }

        [Test]
        public void PreparationChangesInvalidateTheLastOutcomeCheck()
        {
            var p = new KeskeProgress();
            foreach (string id in KeskeProgress.RiskIds) p.Fix(id);
            foreach (string id in KeskeProgress.SupplyIds) p.Collect(id);
            Assert.IsTrue(p.CanPack);
            p.bagPacked = true; p.familyPlan = true;
            Assert.IsTrue(p.Ready);
            Assert.IsFalse(p.CanFinish);
            p.testedCurrentPreparation = true;
            Assert.IsTrue(p.CanFinish);
            p.Changed();
            Assert.IsFalse(p.CanFinish);
        }

        [Test]
        public void PreviousFourRiskSaveStillRequiresTelevisionAndDryer()
        {
            var p = new KeskeProgress();
            foreach (string id in new[] { "bookcase", "wardrobe", "heavy", "route" }) p.Fix(id);
            p.bagPacked = true;
            p.familyPlan = true;
            p.testedCurrentPreparation = true;
            Assert.AreEqual(6, p.Completed);
            Assert.IsFalse(p.Ready);
            Assert.IsFalse(p.CanFinish);
            p.Fix("television");
            Assert.IsFalse(p.Ready);
            p.Fix("dryer");
            Assert.IsTrue(p.Ready);
            Assert.IsFalse(p.CanFinish);
        }

        [Test]
        public void SurfacePlacementUsesTheMeshInsteadOfAnOversizedBox()
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                table.transform.position = new Vector3(10, 1, 0);
                table.transform.localScale = new Vector3(2, 0.2f, 1);
                var ghost = table.GetComponent<BoxCollider>();
                ghost.center = new Vector3(0, 5, 0);
                ghost.size = new Vector3(1, 10, 1);
                var mesh = table.AddComponent<MeshCollider>();
                mesh.sharedMesh = table.GetComponent<MeshFilter>().sharedMesh;
                item.transform.position = new Vector3(5, 20, 4);
                item.transform.localScale = new Vector3(0.2f, 0.1f, 0.3f);
                KeskeSceneBridge.PlaceOnSurface(item.transform, table.transform, Vector2.zero);
                var bounds = KeskeSceneBridge.WorldBounds(item.transform);
                Assert.That(bounds.min.y, Is.EqualTo(1.102f).Within(0.001f),
                    "The item must sit on the visible tabletop, not the top of the ghost box.");
                Assert.That(bounds.center.x, Is.EqualTo(10).Within(0.001f));
                Assert.That(bounds.center.z, Is.EqualTo(0).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void OpeningANoteClearsTheTargetWithoutThrowing()
        {
            var root = new GameObject("NoteInteractionRegression");
            var noteObject = new GameObject("RegressionNote");
            try
            {
                var player = root.AddComponent<KeskePlayer>();
                var game = root.AddComponent<KeskeGame>();
                game.player = player;
                player.game = game;
                game.mode = ScreenMode.Playing;
                game.transition = 0;
                var note = noteObject.AddComponent<KeskeInteractable>();
                note.kind = InteractionKind.Note;
                note.recordMemory = false;
                note.titleTR = "Not";
                note.bodyTR = "Bilgi ekranı hedefi temizler.";
                player.target = note;
                Assert.DoesNotThrow(() => player.UseTarget());
                Assert.AreEqual(ScreenMode.Note, game.mode);
                Assert.IsNull(player.target);
                Assert.DoesNotThrow(() => player.UseTarget(), "A cleared target must also be safe.");
            }
            finally
            {
                Object.DestroyImmediate(noteObject);
                Object.DestroyImmediate(root);
                Time.timeScale = 1;
            }
        }

        [Test]
        public void AimFindsChildCollidersButCannotSelectThroughWallsOrBeyondReach()
        {
            var playerObject = new GameObject("AimTestPlayer");
            var itemObject = new GameObject("AimTestItem");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var player = playerObject.AddComponent<KeskePlayer>();
                player.enabled = false;
                player.view = playerObject.AddComponent<Camera>();
                player.view.enabled = false;
                player.view.pixelRect = new Rect(0, 0, 1280, 720);
                var item = itemObject.AddComponent<KeskeInteractable>();
                var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mesh.transform.SetParent(itemObject.transform, false);
                itemObject.transform.position = new Vector3(0, 0, 2);
                wall.transform.position = new Vector3(10, 0, 1);
                Physics.SyncTransforms();
                Assert.AreSame(item, player.FindTarget(), "Child collider must resolve its parent interaction; ignore the player collider.");
                // A tiny target just off the centre ray must still be selectable.
                mesh.transform.localScale = Vector3.one * 0.006f;
                itemObject.transform.position = new Vector3(0.016f, 0, 2);
                Physics.SyncTransforms();
                Assert.AreSame(item, player.FindTarget(), "Small aim tolerance should catch a nearby visible object.");
                wall.transform.position = new Vector3(0, 0, 1);
                Physics.SyncTransforms();
                player.target = item;
                Assert.IsNull(player.FindTarget(), "A previous target must disappear immediately behind a wall.");
                wall.transform.position = new Vector3(10, 0, 1);
                itemObject.transform.position = new Vector3(0, 0, 4);
                mesh.transform.localScale = Vector3.one;
                Physics.SyncTransforms();
                Assert.IsNull(player.FindTarget(), "Objects outside reach must not be selected.");
                itemObject.transform.position = new Vector3(0, 0, 2);
                item.enabled = false;
                Physics.SyncTransforms();
                Assert.IsNull(player.FindTarget());
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(itemObject);
                Object.DestroyImmediate(wall);
            }
        }

        [UnityTest]
        public IEnumerator AuthoredHouseSupportsTheFullPreparationLoop()
        {
            EditorSceneManager.OpenScene("Assets/Keske/Scenes/KeskeBirlesik.unity");
            var authoredWhistle = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(t => t.name == "whistle (1)");
            Vector3 whistlePosition = authoredWhistle.position;
            Quaternion whistleRotation = authoredWhistle.rotation;
            Vector3 whistleScale = authoredWhistle.lossyScale;
            yield return new EnterPlayMode();
            yield return null;
            var game = Object.FindFirstObjectByType<KeskeGame>();
            Assert.IsNotNull(game);
            Assert.IsNotNull(game.player);
            Assert.AreEqual("Player_Keske_Original", game.player.name);
            Assert.IsFalse(GameObject.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(t => t.name == "PlayerCapsule" && t.gameObject.activeInHierarchy));
            Assert.AreEqual(1, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.isActiveAndEnabled));
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(c => c.isActiveAndEnabled));
            var items = game.worldRoot.GetComponentsInChildren<KeskeInteractable>(true);
            var risks = game.worldRoot.GetComponentsInChildren<KeskeRisk>(true);
            Assert.AreEqual(6, risks.Length);
            Assert.AreEqual(8, KeskeProgress.TotalSteps);
            Assert.AreEqual(items.Length, items.Select(i => i.id).Distinct().Count(), "Interaction IDs must be unique.");
            foreach (string id in KeskeProgress.SupplyIds)
                Assert.AreEqual(1, items.Count(i => i.kind == InteractionKind.Pickup && i.id == id));
            Assert.IsTrue(items.Any(i => i.kind == InteractionKind.Finish));
            foreach (var item in items)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.titleTR), "Missing Turkish title: " + item.id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.titleEN), "Missing English title: " + item.id);
                if (item.kind == InteractionKind.Note || item.kind == InteractionKind.Plan || item.kind == InteractionKind.Bag)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(item.bodyTR), item.id);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(item.bodyEN), item.id);
                }
            }
            Assert.AreEqual(1, items.Count(i => i.id == "whistle"));
            var connectedWhistle = items.Single(i => i.id == "whistle").transform;
            Assert.That(Vector3.Distance(whistlePosition, connectedWhistle.position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(whistleRotation, connectedWhistle.rotation), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(whistleScale, connectedWhistle.lossyScale), Is.LessThan(0.001f));
            foreach (var mesh in game.worldRoot.GetComponentsInChildren<MeshCollider>(true))
                if (mesh.enabled && !mesh.isTrigger && mesh.sharedMesh != null)
                    foreach (var box in mesh.GetComponents<BoxCollider>())
                        Assert.IsFalse(box.enabled, "Duplicate furniture box can obstruct pickups: " + mesh.name);

            Assert.AreEqual("whistle (1)", items.Single(i => i.id == "whistle").name,
                "The supplied whistle model should be connected instead of creating a duplicate.");
            foreach (var risk in risks)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(risk.resultTR));
                Assert.IsFalse(string.IsNullOrWhiteSpace(risk.learningTR));
                Assert.IsNull(risk.GetComponent<Collider>(), "Interaction collider must follow the moving model.");
                Assert.IsTrue(risk.movingPart.GetComponentsInChildren<Collider>(true).Any(c => c.enabled && !c.isTrigger));
                if (risk.needsTool) Assert.IsNotNull(risk.strap);
            }
            Assert.AreEqual(3, items.Count(i => i.kind == InteractionKind.Note && i.recordMemory));
            Assert.IsTrue(Physics.Raycast(game.spawnPosition + Vector3.up * 0.1f,
                Vector3.down, 0.5f), "The spawn must have a floor.");
            Assert.IsTrue(Physics.Raycast(game.observationPosition + Vector3.up * 0.1f,
                Vector3.down, 0.5f), "The observation point must have a floor.");
            foreach (var item in items)
                Assert.IsTrue(item.GetComponentsInChildren<Collider>(true).Any(c => c.enabled && !c.isTrigger),
                    "Interaction has no usable collider: " + item.id);
            foreach (var door in items.Where(i => i.kind == InteractionKind.Door))
            {
                Assert.AreSame(door.transform, door.doorLeaf, "The door must rotate around the separate hinge.");
                var model = door.GetComponentInChildren<MeshFilter>().transform;
                var bounds = model.GetComponent<MeshFilter>().sharedMesh.bounds;
                Vector3 hinge = model.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.center.z));
                Assert.That(Vector3.Distance(hinge, door.doorLeaf.position), Is.LessThan(0.001f));
                Vector3 hingeBefore = door.doorLeaf.position;
                Vector3 centerBefore = model.TransformPoint(bounds.center);
                door.startOpen = true;
                door.ResetDoor();
                Assert.That(Vector3.Distance(hingeBefore, door.doorLeaf.position), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(centerBefore, model.TransformPoint(bounds.center)), Is.GreaterThan(0.5f));
                door.startOpen = false;
                door.ResetDoor();
                Assert.That(Vector3.Distance(centerBefore, model.TransformPoint(bounds.center)), Is.LessThan(0.001f));
            }
            game.english = false;
            game.Begin(false);
            Assert.AreEqual(ScreenMode.Info, game.mode);
            game.CloseInfo();
            Assert.AreEqual(ScreenMode.Playing, game.mode);
            foreach (var pair in new[] {
                new { Id = "torch", Position = new Vector3(-5.0f, 0.66f, 4.71f) },
                new { Id = "aid", Position = new Vector3(2.8f, 0.66f, 11.9f) }
            })
            {
                var pickup = items.Single(i => i.id == pair.Id);
                game.player.Teleport(pair.Position, 0);
                game.player.view.transform.LookAt(KeskeSceneBridge.WorldBounds(pickup.transform).center);
                Physics.SyncTransforms();
                Assert.AreSame(pickup, game.player.FindTarget(),
                    "A player standing next to the furniture must be able to target " + pair.Id);
            }
            game.SwitchWorld();
            foreach (var risk in risks)
                Assert.That(Vector3.Distance(risk.movingPart.localPosition, risk.aftermathPosition), Is.LessThan(0.001f));
            game.SwitchWorld();
            foreach (var risk in risks)
                Assert.That(Vector3.Distance(risk.movingPart.localPosition, risk.initialPosition), Is.LessThan(0.001f));
            foreach (var item in items.Where(i => i.kind == InteractionKind.Pickup)) item.Use(game);
            Assert.IsTrue(game.progress.CanPack);
            foreach (var risk in risks) { game.SetMode(ScreenMode.Playing); risk.Interact(game); }
            foreach (var risk in risks.Where(r => r.needsTool))
            {
                Assert.IsTrue(risk.strap.activeSelf, "Completed fastening must be visible.");
                Assert.That(risk.GetComponent<KeskeInteractable>().Prompt(game), Does.Contain("SABİTLENDİ"));
            }
            game.SetMode(ScreenMode.Playing);
            items.Single(i => i.kind == InteractionKind.Bag).Use(game);
            game.ChoosePlan(1);
            Assert.IsTrue(game.progress.Ready);
            game.TryFinish();
            Assert.AreNotEqual(ScreenMode.End, game.mode, "A fresh outcome check is required.");
            game.SwitchWorld();
            foreach (var risk in risks)
                Assert.That(Vector3.Distance(risk.movingPart.localPosition, risk.preparedPosition), Is.LessThan(0.001f));
            game.Save();
            game.Begin(true);
            game.CloseInfo();
            Assert.IsTrue(game.progress.Ready, "Continue should restore all eight objectives.");
            Assert.IsFalse(game.progress.testedCurrentPreparation, "Continue requires a fresh preview.");
            foreach (var item in items.Where(i => i.kind == InteractionKind.Pickup))
                Assert.IsFalse(item.gameObject.activeSelf, "Collected supply reappeared after Continue: " + item.id);
            game.SwitchWorld();
            game.TryFinish();
            Assert.AreEqual(ScreenMode.End, game.mode);
            game.Begin(false);
            game.CloseInfo();
            Assert.AreEqual(0, game.progress.Completed);
            foreach (var risk in risks.Where(r => r.needsTool)) Assert.IsFalse(risk.strap.activeSelf);
            foreach (var item in items.Where(i => i.kind == InteractionKind.Pickup)) Assert.IsTrue(item.gameObject.activeSelf);
            yield return new ExitPlayMode();
        }
    }
}
