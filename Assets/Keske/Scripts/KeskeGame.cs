using System;
using UnityEngine;

namespace Keske
{
    public enum ScreenMode { Menu, Playing, Pause, Journal, Info, Note, Plan, End }

    public class KeskeGame : MonoBehaviour
    {
        public Transform worldRoot;
        public KeskePlayer player;
        public Light sun;
        public Light[] houseLights;
        public Vector3 spawnPosition = new Vector3(3.7f, 0.66f, 0);
        public float spawnYaw = -90;
        public Vector3 observationPosition = new Vector3(1.5f, 0.66f, 2.1f);
        public float observationYaw = -90;
        [Header("Optional custom audio / İsteğe bağlı sesler")]
        public AudioClip ambienceClip;
        public AudioClip stepClip;
        public AudioClip pickupClip;
        public AudioClip doorClip;
        public AudioClip successClip;
        public AudioClip switchClip;
        [Header("Optional UI font / İsteğe bağlı arayüz yazı tipi")]
        public Font interfaceFont;
        [HideInInspector] public KeskeProgress progress = new KeskeProgress();
        [HideInInspector] public KeskeAudio sound;
        [HideInInspector] public ScreenMode mode = ScreenMode.Menu;
        [HideInInspector] public bool preview;
        [HideInInspector] public bool english;
        [HideInInspector] public float volume = 0.7f;
        [HideInInspector] public float transition;
        [HideInInspector] public string notice;
        [HideInInspector] public float noticeRemaining;
        [HideInInspector] public string noteTitle;
        [HideInInspector] public string noteBody;
        const string SaveKey = "KESKE_MERGED_PROGRESS_V2";
        KeskeInteractable[] objects;
        KeskeRisk[] risks;
        ScreenMode infoReturn = ScreenMode.Menu;
        float autosave;
        bool started;

        public bool CanMove { get { return mode == ScreenMode.Playing && transition < 0.12f; } }
        public bool HasSave { get { return PlayerPrefs.HasKey(SaveKey); } }
        public string T(string tr, string en) { return english ? en : tr; }

        void Start()
        {
            if (worldRoot == null || player == null)
            { Debug.LogError("[KESKE] Open Assets/Keske/Scenes/KeskeBirlesik.unity."); enabled = false; return; }
            player.game = this;
            objects = worldRoot.GetComponentsInChildren<KeskeInteractable>(true);
            risks = worldRoot.GetComponentsInChildren<KeskeRisk>(true);
            foreach (var item in objects) item.Initialize();
            sound = gameObject.AddComponent<KeskeAudio>();
            sound.Initialize(this);
            var hud = GetComponent<KeskeHUD>();
            if (hud == null) hud = gameObject.AddComponent<KeskeHUD>();
            hud.game = this;
            english = PlayerPrefs.GetInt("KESKE_EN", 0) == 1;
            volume = PlayerPrefs.GetFloat("KESKE_VOLUME", 0.7f);
            player.sensitivity = PlayerPrefs.GetFloat("KESKE_SENS", 1.6f);
            AudioListener.volume = volume;
            ApplyState();
            MenuView();
            started = true;
        }

        void Update()
        {
            if (!started) return;
            transition = Mathf.Max(0, transition - Time.unscaledDeltaTime * 1.5f);
            noticeRemaining = Mathf.Max(0, noticeRemaining - Time.unscaledDeltaTime);
            if (mode == ScreenMode.Playing)
            {
                progress.seconds += Time.deltaTime;
                autosave += Time.deltaTime;
                if (autosave > 30) { Save(); autosave = 0; }
                if (KeskeInput.Pressed(KeyCode.Q) && transition <= 0) SwitchWorld();
                if (KeskeInput.Pressed(KeyCode.H)) Notify(Hint(), 9);
                if (KeskeInput.Pressed(KeyCode.Tab)) SetMode(ScreenMode.Journal);
                if (KeskeInput.Pressed(KeyCode.Escape)) { Save(); SetMode(ScreenMode.Pause); }
            }
            else if (KeskeInput.Pressed(KeyCode.Escape))
            {
                if (mode == ScreenMode.Info) SetMode(infoReturn);
                else if (mode != ScreenMode.Menu && mode != ScreenMode.End) SetMode(ScreenMode.Playing);
            }
            else if (mode == ScreenMode.Journal && KeskeInput.Pressed(KeyCode.Tab)) SetMode(ScreenMode.Playing);
        }

        public void SetMode(ScreenMode value)
        {
            player.target = null;
            mode = value;
            bool playing = value == ScreenMode.Playing;
            Time.timeScale = playing ? 1 : 0;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
        }

        public void Begin(bool resume)
        {
            progress = new KeskeProgress();
            if (resume && HasSave)
            {
                try { progress = JsonUtility.FromJson<KeskeProgress>(PlayerPrefs.GetString(SaveKey)) ?? new KeskeProgress(); }
                catch (Exception) { progress = new KeskeProgress(); }
            }
            progress.Normalize();
            noticeRemaining = 0;
            autosave = 0;
            progress.testedCurrentPreparation = false; // Continue always requires a fresh outcome check.
            if (player.torch != null) player.torch.enabled = false;
            preview = false;
            player.Teleport(spawnPosition, spawnYaw);
            foreach (var item in objects) item.ResetDoor();
            ApplyState();
            SetMode(ScreenMode.Playing);
            transition = 0.65f;
            Save();
            // Instructions are shown after Play/Continue, not as a menu or HUD panel.
            infoReturn = ScreenMode.Playing;
            SetMode(ScreenMode.Info);
        }

        public void MenuView()
        {
            player.Teleport(spawnPosition, spawnYaw);
            SetMode(ScreenMode.Menu);
        }

        public void ReturnToMenu() { Save(); MenuView(); }

        public void SwitchWorld()
        {
            preview = !preview;
            if (preview)
            {
                progress.previewSeen = true;
                progress.testedCurrentPreparation = true;
            }
            // Clear authored observation spot prevents moved furniture enclosing the player.
            player.Teleport(observationPosition, observationYaw);
            ApplyState();
            sound.Play("switch", 0.5f);
            transition = 0.8f;
            Notify(preview
                ? T("SONUÇ · Sarsıntı sonrası önizleme. Değişiklik yapmak için Q.", "OUTCOME · After-shaking preview. Q to make preparations.")
                : T("HAZIRLIK · Sonucu değiştirebilirsin.", "PREPARATION · You can change the outcome."), 5);
            Save();
        }

        public void ApplyState()
        {
            foreach (var risk in risks) risk.Apply(this);
            foreach (var item in objects)
                if (item.kind == InteractionKind.Pickup) item.gameObject.SetActive(!progress.Has(item.id));
            if (sun != null)
            {
                sun.color = preview ? new Color(0.8f, 0.88f, 1f) : new Color(1f, 0.96f, 0.88f);
                sun.intensity = preview ? 0.8f : 1.1f;
            }
            if (houseLights != null)
                foreach (var light in houseLights) if (light != null)
                {
                    light.intensity = preview ? 1.05f : 1.3f;
                    light.color = preview ? new Color(0.8f, 0.88f, 1f) : new Color(1, 0.94f, 0.84f);
                }
            RenderSettings.ambientLight = preview ? new Color(0.48f, 0.52f, 0.57f) : new Color(0.6f, 0.58f, 0.53f);
            RenderSettings.fogColor = preview ? new Color(0.035f, 0.065f, 0.09f) : new Color(0.09f, 0.12f, 0.12f);
            if (player.view != null) player.view.backgroundColor = RenderSettings.fogColor;
            if (sound != null) sound.SetPreview(preview);
            Physics.SyncTransforms();
        }

        public void Prepared(string message)
        {
            sound.Play("done");
            Notify(message, 6);
            Save();
        }
        public void Notify(string message, float seconds = 4) { notice = message; noticeRemaining = seconds; }
        public void ShowNote(string title, string body) { noteTitle = title; noteBody = body; SetMode(ScreenMode.Note); }
        public void ShowPlan(KeskeInteractable item) { noteTitle = item.Title(this); noteBody = item.Body(this); SetMode(ScreenMode.Plan); }
        public void OpenInfo() { infoReturn = mode; SetMode(ScreenMode.Info); }
        public void CloseInfo() { SetMode(infoReturn); }

        public void ChoosePlan(int choice)
        {
            if (choice != 1)
            {
                Notify(T("Planı herkes önceden bilmeli. Birbirini ev içinde aramak yerine belirlenmiş buluşma yeri ve bölge dışı iletişim kişisi seç.", "Agree on the plan beforehand. Choose a meeting place and an out-of-area contact instead of searching for one another indoors."), 10);
                return;
            }
            progress.familyPlan = true;
            progress.Changed();
            SetMode(ScreenMode.Playing);
            Prepared(T("Aile planı tamamlandı: bilinen buluşma yeri ve bölge dışı iletişim kişisi.", "Family plan agreed: a known meeting place and an out-of-area contact."));
        }

        public void TryFinish()
        {
            if (!progress.Ready)
            {
                Notify(T("Hazırlık adımları: ", "Preparation steps: ") + progress.Completed + "/" + KeskeProgress.TotalSteps + ". " + T("TAB ile eksikleri gör.", "TAB shows what remains."), 6);
                return;
            }
            if (!preview || !progress.testedCurrentPreparation)
            {
                Notify(T("Son düzenini Q ile SONUÇ modunda kontrol et, sonra terminale dön.", "Check your latest setup in OUTCOME with Q, then return to the terminal."), 6);
                return;
            }
            sound.Play("done");
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            SetMode(ScreenMode.End);
        }

        public string Hint()
        {
            if (!progress.previewSeen) return T("Q ile sonuç önizlemesine bak. İncelenebilir eşyalarda el simgesi belirir.", "Press Q to see the outcome. A hand icon marks objects you can inspect.");
            if (preview && !progress.Ready) return T("Önlem almak ve eşya toplamak için Q ile hazırlığa dön.", "Return to preparation with Q to collect items and reduce risks.");
            if (!progress.Has("tool")) return T("Yatak odasındaki çalışma masasında sabitleme setini bul.", "Find the fastening kit on the bedroom study desk.");
            if (!progress.Fixed("bookcase")) return T("Yatak odasının girişindeki çalışma rafını sabitle.", "Secure the study shelving near the bedroom entrance.");
            if (!progress.Fixed("route")) return T("Girişte geçişi daraltan kutuları düzenle.", "Move the crates narrowing the passage near the entrance.");
            if (!progress.Fixed("wardrobe")) return T("Yatak odasındaki dolabı sabitle.", "Secure the bedroom wardrobe.");
            if (!progress.Fixed("television")) return T("Salondaki televizyonu sabitle.", "Secure the living-room television.");
            if (!progress.Fixed("dryer")) return T("Banyoda çamaşır makinesinin üstündeki kurutma makinesini sabitle.", "Secure the dryer above the bathroom washing machine.");
            if (!progress.Fixed("heavy")) return T("Mutfaktaki yüksek rafta duran kavanozu alt konuma al.", "Move the jar on the high kitchen shelf to the lower stand.");
            foreach (var id in KeskeProgress.SupplyIds) if (!progress.Has(id)) return SupplyHint(id);
            if (!progress.bagPacked) return T("Topladığın malzemeleri giriş yanındaki çantaya koy.", "Pack your supplies into the bag beside the entrance.");
            if (!progress.familyPlan) return T("Yatak odasının girişindeki aile planı panosunu oku.", "Read the family-plan board near the bedroom entrance.");
            if (!preview || !progress.testedCurrentPreparation) return T("Hazırlık tamam. Q ile son düzeninin sonuçlarını kontrol et.", "Preparation complete. Press Q to test your latest setup.");
            return T("Giriş koridorundaki turkuaz terminale git.", "Reach the teal terminal in the entrance hall.");
        }

        public string SupplyName(string id)
        {
            switch (id)
            {
                case "water": return T("İçme suyu", "Drinking water");
                case "food": return T("Dayanıklı gıda", "Shelf-stable food");
                case "aid": return T("İlk yardım çantası", "First-aid kit");
                case "blanket": return T("Battaniye", "Blanket");
                case "radio": return T("Pilli radyo", "Battery radio");
                case "torch": return T("El feneri", "Flashlight");
                case "batteries": return T("Yedek piller", "Spare batteries");
                case "whistle": return T("Düdük", "Whistle");
                case "documents": return T("Belge kopyaları", "Document copies");
                default: return id;
            }
        }
        public string SupplyHint(string id)
        {
            switch (id)
            {
                case "water": return T("İçme suyu: salonda televizyon yanındaki yüzey.", "Drinking water: surface beside the living-room television.");
                case "food": return T("Dayanıklı gıda: mutfak tezgâhı.", "Shelf-stable food: kitchen counter.");
                case "aid": return T("İlk yardım çantası: banyodaki tezgâh.", "First-aid kit: bathroom counter.");
                case "blanket": return T("Battaniye: yatak üzeri.", "Blanket: on the bed.");
                case "radio": return T("Pilli radyo: yatak odasındaki çalışma masası.", "Battery radio: bedroom study desk.");
                case "torch": return T("Çantaya konacak el feneri: salondaki orta sehpa.", "Flashlight for the bag: living-room coffee table.");
                case "batteries": return T("Yedek piller: yatak odasındaki konsol.", "Spare batteries: bedroom sideboard.");
                case "whistle": return T("Düdük: mutfaktaki yemek masasının üzerinde.", "Whistle: on the kitchen dining table.");
                default: return T("Belge kopyaları: yatak odasındaki çalışma masası.", "Document copies: bedroom study desk.");
            }
        }

        public void Preferences()
        {
            PlayerPrefs.SetInt("KESKE_EN", english ? 1 : 0);
            PlayerPrefs.SetFloat("KESKE_VOLUME", volume);
            PlayerPrefs.SetFloat("KESKE_SENS", player.sensitivity);
            AudioListener.volume = volume;
            PlayerPrefs.Save();
        }
        public void Save()
        {
            if (mode == ScreenMode.End || mode == ScreenMode.Menu || !started) return;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(progress));
            PlayerPrefs.Save();
        }
        void OnApplicationFocus(bool focus)
        {
            if (!focus && started && mode == ScreenMode.Playing) { Save(); SetMode(ScreenMode.Pause); }
        }
        void OnApplicationQuit() { if (started && mode != ScreenMode.Menu) Save(); Time.timeScale = 1; }
        void OnDestroy() { Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
