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
        public GameObject shadowFigure;
        [Header("Optional custom audio / İsteğe bağlı sesler")]
        public AudioClip ambienceClip;
        public AudioClip stepClip;
        public AudioClip pickupClip;
        public AudioClip doorClip;
        public AudioClip successClip;
        public AudioClip switchClip;
        public AudioClip scareClip;
        [Header("Optional UI font / İsteğe bağlı arayüz yazı tipi")]
        public Font interfaceFont;
        [HideInInspector] public KeskeProgress progress = new KeskeProgress();
        [HideInInspector] public KeskeAudio sound;
        [HideInInspector] public ScreenMode mode = ScreenMode.Menu;
        [HideInInspector] public bool preview;
        [HideInInspector] public bool english;
        [HideInInspector] public bool scares = true;
        [HideInInspector] public float volume = 0.7f;
        [HideInInspector] public float transition;
        [HideInInspector] public string notice;
        [HideInInspector] public float noticeRemaining;
        [HideInInspector] public string noteTitle;
        [HideInInspector] public string noteBody;
        [HideInInspector] public float scareRemaining;
        const string SaveKey = "KESKE_PROGRESS_V1";
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
            if (worldRoot == null || player == null) KeskeWorld.Build(this);
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
            scares = PlayerPrefs.GetInt("KESKE_SCARES", 1) == 1;
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
            if (scareRemaining > 0 && mode == ScreenMode.Playing)
            {
                scareRemaining -= Time.deltaTime;
                if (scareRemaining <= 0 && shadowFigure != null) shadowFigure.SetActive(false);
            }
        }

        public void SetMode(ScreenMode value)
        {
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
            preview = false;
            if (shadowFigure != null) shadowFigure.SetActive(false);
            scareRemaining = 0;
            player.Teleport(new Vector3(0, 0.08f, -8), 0);
            foreach (var item in objects) item.ResetDoor();
            ApplyState();
            SetMode(ScreenMode.Playing);
            transition = 0.65f;
            Save();
            if (!resume)
                ShowNote(T("KEŞKE / Ev 07", "IF ONLY / House 07"), T(
                    "Burası terk edilmiş bir ev görünümünde hazırlanmış bir afet tatbikatı. Eski sakinlerin bıraktığı notları oku; hazırlık eksiklerini bul.\n\nQ ile HAZIRLIK ve SONUÇ arasında geçebilirsin. SONUÇ, sarsıntı bittikten sonraki kurgusal bir önizlemedir. Eşyaların durumunu incele, hazırlığa dön ve değiştirmeyi dene.\n\nAltı hazırlık adımını tamamla, son düzenini Q ile kontrol et ve bahçedeki turkuaz terminale git. TAB: günlük. H: sıradaki ipucu.",
                    "This apparently abandoned house is a disaster-preparedness training scenario. Read the former residents' notes and find what was left unprepared.\n\nQ switches between PREPARATION and OUTCOME. OUTCOME is a fictional preview after shaking has stopped. Inspect what happened, return and change your preparations.\n\nComplete six preparation steps, test your latest setup with Q, then reach the teal terminal in the garden. TAB: journal. H: next hint."));
            else Notify(T("Kayıt yüklendi. Hazırlıkların korundu; girişten devam ediyorsun.", "Progress loaded. Your preparations are restored; continue from the entrance."), 7);
        }

        public void MenuView()
        {
            player.Teleport(new Vector3(-10, 0.08f, -14), 38);
            SetMode(ScreenMode.Menu);
        }

        public void ReturnToMenu() { Save(); MenuView(); }

        public void SwitchWorld()
        {
            bool upstairs = player.transform.position.y > 2;
            preview = !preview;
            if (preview)
            {
                progress.previewSeen = true;
                progress.testedCurrentPreparation = true;
            }
            // Fixed observation spots prevent a moved cupboard from enclosing the player.
            player.Teleport(new Vector3(0.4f, upstairs ? 3.28f : 0.08f, -2.8f), 0);
            if (shadowFigure != null) shadowFigure.SetActive(false);
            scareRemaining = 0;
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
                sun.color = preview ? new Color(0.4f, 0.62f, 0.85f) : new Color(0.9f, 0.77f, 0.57f);
                sun.intensity = preview ? 0.35f : 0.65f;
            }
            if (houseLights != null)
                foreach (var light in houseLights) if (light != null)
                {
                    light.intensity = preview ? 0.4f : 1.15f;
                    light.color = preview ? new Color(0.4f, 0.65f, 0.8f) : new Color(1, 0.75f, 0.48f);
                }
            RenderSettings.ambientLight = preview ? new Color(0.2f, 0.26f, 0.32f) : new Color(0.37f, 0.34f, 0.29f);
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
                Notify(T("Hazırlık adımları: ", "Preparation steps: ") + progress.Completed + "/6. " + T("TAB ile eksikleri gör.", "TAB shows what remains."), 6);
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

        public void StartScare()
        {
            if (!scares || shadowFigure == null) return;
            progress.scareSeen = true;
            // A brief non-colliding memory silhouette, never a chasing enemy.
            shadowFigure.transform.position = new Vector3(0.4f, 3.2f, 1.8f);
            shadowFigure.transform.LookAt(new Vector3(player.transform.position.x, 3.2f, player.transform.position.z));
            shadowFigure.SetActive(true);
            scareRemaining = 1.1f;
            sound.Play("scare", 0.5f);
            Save();
        }

        public string Hint()
        {
            if (!progress.previewSeen) return T("Q ile sonuç önizlemesine bak. İncelenebilir eşyalarda el simgesi belirir.", "Press Q to see the outcome. A hand icon marks objects you can inspect.");
            if (preview && !progress.Ready) return T("Önlem almak ve eşya toplamak için Q ile hazırlığa dön.", "Return to preparation with Q to collect items and reduce risks.");
            if (!progress.Has("tool")) return T("Alt katta sağdaki alet masasında turuncu sabitleme setini bul.", "Find the orange fastening kit on the downstairs workbench to the right.");
            if (!progress.Fixed("bookcase")) return T("Alt kat salonundaki uzun kitaplığı sabitle.", "Secure the tall bookcase in the downstairs living room.");
            if (!progress.Fixed("route")) return T("Girişte geçişi daraltan kutuları düzenle.", "Move the crates narrowing the passage near the entrance.");
            if (!progress.Fixed("wardrobe")) return T("Üst kattaki yatak odasında gardırobu sabitle.", "Secure the wardrobe in the upstairs bedroom.");
            if (!progress.Fixed("heavy")) return T("Üst kat sahanlığındaki ağır seramik eşyayı alt rafa indir.", "Move the heavy ceramic object to the lower shelf on the upstairs landing.");
            if (!progress.Has("key")) return T("Çalışma odasının anahtarı alt kattaki alet masasında.", "The study key is on the downstairs workbench.");
            foreach (var id in KeskeProgress.SupplyIds) if (!progress.Has(id)) return SupplyHint(id);
            if (!progress.bagPacked) return T("Topladığın malzemeleri girişteki turkuaz çantaya koy.", "Pack your supplies into the teal bag by the entrance.");
            if (!progress.familyPlan) return T("Üst kat çalışma odasındaki aile planı panosunu oku.", "Read the family-plan board in the upstairs study.");
            if (!preview || !progress.testedCurrentPreparation) return T("Hazırlık tamam. Q ile son düzeninin sonuçlarını kontrol et.", "Preparation complete. Press Q to test your latest setup.");
            return T("Bahçede, evin önündeki yolun sağında turkuaz terminale git.", "Reach the teal terminal to the right of the front garden path.");
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
                case "water": return T("İçme suyu: alt kat mutfak tezgâhı.", "Drinking water: downstairs kitchen counter.");
                case "food": return T("Dayanıklı gıda: mutfak yemek masası.", "Shelf-stable food: kitchen dining table.");
                case "aid": return T("İlk yardım çantası: üst kat sahanlığındaki küçük masa.", "First-aid kit: small table on the upstairs landing.");
                case "blanket": return T("Battaniye: üst kat yatak odası.", "Blanket: upstairs bedroom.");
                case "radio": return T("Pilli radyo: kilitli çalışma odasındaki masa.", "Battery radio: desk in the locked study.");
                case "torch": return T("Çantaya konacak el feneri: alt kat salonundaki sehpa.", "Flashlight for the bag: downstairs living-room coffee table.");
                case "batteries": return T("Yedek piller: alt kat alet masasının yanındaki raf.", "Spare batteries: shelf beside the downstairs workbench.");
                case "whistle": return T("Düdük: bahçedeki bank, giriş yolunun solu.", "Whistle: garden bench, left of the entrance path.");
                default: return T("Belge kopyaları: üst kat çalışma odasındaki yan sehpa.", "Document copies: side table in the upstairs study.");
            }
        }

        public void Preferences()
        {
            PlayerPrefs.SetInt("KESKE_EN", english ? 1 : 0);
            PlayerPrefs.SetInt("KESKE_SCARES", scares ? 1 : 0);
            PlayerPrefs.SetFloat("KESKE_VOLUME", volume);
            PlayerPrefs.SetFloat("KESKE_SENS", player.sensitivity);
            AudioListener.volume = volume;
            PlayerPrefs.Save();
        }
        public void Save()
        {
            if (mode == ScreenMode.End || !started) return;
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
