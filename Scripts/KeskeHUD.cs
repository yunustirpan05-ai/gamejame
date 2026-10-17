using UnityEngine;

namespace Keske
{
    public class KeskeHUD : MonoBehaviour
    {
        public KeskeGame game;
        readonly Color ink = new Color(0.035f, 0.065f, 0.08f, 0.96f);
        readonly Color white = new Color(0.92f, 0.94f, 0.91f);
        readonly Color muted = new Color(0.56f, 0.66f, 0.66f);
        readonly Color teal = new Color(0.32f, 0.85f, 0.77f);
        readonly Color amber = new Color(1, 0.68f, 0.4f);
        GUIStyle button;
        GUIStyle text;
        Texture2D hand;
        Vector2 scroll;
        ScreenMode lastMode;

        string T(string tr, string en) { return game.T(tr, en); }

        void Initialize()
        {
            if (text != null) return;
            text = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, padding = new RectOffset(0, 0, 0, 0) };
            button = new GUIStyle(GUI.skin.button) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(18, 18, 0, 0), wordWrap = true };
            if (game.interfaceFont != null) { text.font = game.interfaceFont; button.font = game.interfaceFont; }
            foreach (var state in new[] { button.normal, button.hover, button.active, button.focused, button.onNormal, button.onHover, button.onActive, button.onFocused })
            { state.background = null; state.textColor = white; }
            hand = MakeHand();
        }

        void OnGUI()
        {
            if (game == null || game.player == null) return;
            Initialize();
            if (lastMode != game.mode) { scroll = Vector2.zero; lastMode = game.mode; }
            Matrix4x4 old = GUI.matrix;
            Color oldColor = GUI.color;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Vector3 offset = new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2, 0);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1));
            switch (game.mode)
            {
                case ScreenMode.Menu: Menu(); break;
                case ScreenMode.Playing: Hud(); break;
                case ScreenMode.Pause: Pause(); break;
                case ScreenMode.Journal: Journal(); break;
                case ScreenMode.Info: Info(); break;
                case ScreenMode.Note: Note(); break;
                case ScreenMode.Plan: Plan(); break;
                case ScreenMode.End: End(); break;
            }
            if (game.noticeRemaining > 0 && game.mode != ScreenMode.Menu && game.mode != ScreenMode.End)
            {
                Rect(110, 604, 1060, 64, new Color(0.02f, 0.05f, 0.065f, 0.97f));
                Rect(110, 604, 3, 64, teal);
                Label(132, 617, 1015, 45, game.notice, 17, white);
            }
            if (game.transition > 0 && game.mode == ScreenMode.Playing)
                Rect(0, 0, 1280, 720, new Color(0.05f, 0.12f, 0.14f, game.transition * 0.8f));
            GUI.matrix = old;
            GUI.color = oldColor;
        }

        void Rect(float x, float y, float w, float h, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void Label(float x, float y, float w, float h, string value, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            text.fontSize = size;
            text.normal.textColor = color;
            text.fontStyle = style;
            GUI.Label(new Rect(x, y, w, h), value, text);
        }

        bool Button(float x, float y, float w, float h, string value, bool primary = false)
        {
            var bounds = new Rect(x, y, w, h);
            bool hover = bounds.Contains(Event.current.mousePosition);
            Rect(x, y, w, h, primary ? (hover ? new Color(0.19f, 0.49f, 0.45f) : new Color(0.12f, 0.32f, 0.31f)) : (hover ? new Color(0.13f, 0.22f, 0.25f) : new Color(0.07f, 0.13f, 0.16f)));
            Rect(x, y, primary ? 3 : 1, h, primary ? teal : new Color(0.25f, 0.34f, 0.36f));
            return GUI.Button(bounds, value, button);
        }

        void Backdrop()
        {
            Rect(0, 0, 1280, 720, new Color(0.015f, 0.035f, 0.045f, 0.82f));
        }

        void Menu()
        {
            for (int i = 0; i < 32; i++) Rect(i * 27, 0, 27, 720, new Color(0.015f, 0.035f, 0.045f, Mathf.Lerp(0.98f, 0.08f, i / 31f)));
            Label(64, 54, 650, 24, T("EV 07   /   BİR HAZIRLIK HİKÂYESİ", "HOUSE 07   /   A STORY OF PREPARATION"), 14, teal);
            Rect(64, 108, 50, 3, teal);
            Label(57, 132, 730, 130, "KEŞKE", 110, white, FontStyle.Bold);
            Label(66, 271, 510, 78, T("Bazı şeyleri geri alamazsın.\nBazılarına bugünden hazırlanabilirsin.", "Some things cannot be undone.\nSome things can be prepared for today."), 23, white);
            float y = 382;
            if (game.HasSave)
            {
                if (Button(64, y, 365, 48, T("Devam et", "Continue"), true)) game.Begin(true);
                y += 58;
            }
            if (Button(64, y, 365, 48, T("Yeni oyun", "New game"), !game.HasSave)) game.Begin(false);
            y += 58;
            if (Button(64, y, 365, 48, T("Nasıl oynanır / Bilgi", "How to play / About"))) game.OpenInfo();
            y += 58;
            if (Button(64, y, 365, 48, T("Çıkış", "Quit"))) Quit();
            Preferences(852, 357);
            Label(64, 674, 700, 22, T("3D GERİLİM • AFETE HAZIRLIK • TEK OYUNCULU", "3D SUSPENSE • DISASTER PREPAREDNESS • SINGLE PLAYER"), 12, muted);
            Label(1035, 674, 190, 22, "PROTOTYPE 01", 12, muted);
        }

        void Preferences(float x, float y)
        {
            Rect(x - 20, y - 20, 344, 290, ink);
            Label(x, y, 300, 22, T("AYARLAR", "SETTINGS"), 13, teal);
            if (Button(x, y + 34, 143, 36, "Türkçe", !game.english)) { game.english = false; game.Preferences(); }
            if (Button(x + 153, y + 34, 143, 36, "English", game.english)) { game.english = true; game.Preferences(); }
            if (Button(x, y + 82, 296, 36, T("Ani korkutma: ", "Startle effect: ") + (game.scares ? T("Açık", "On") : T("Kapalı", "Off"))))
            { game.scares = !game.scares; game.Preferences(); }
            Label(x, y + 133, 296, 22, T("Ses düzeyi", "Volume"), 15, muted);
            float vol = GUI.HorizontalSlider(new Rect(x, y + 160, 296, 20), game.volume, 0, 1);
            Label(x, y + 191, 296, 22, T("Fare hassasiyeti", "Mouse sensitivity"), 15, muted);
            float sens = GUI.HorizontalSlider(new Rect(x, y + 218, 296, 20), game.player.sensitivity, 0.45f, 3.5f);
            if (Mathf.Abs(vol - game.volume) > 0.001f || Mathf.Abs(sens - game.player.sensitivity) > 0.001f)
            { game.volume = vol; game.player.sensitivity = sens; game.Preferences(); }
        }

        void Hud()
        {
            Rect(24, 24, 340, 88, ink);
            Rect(24, 24, 3, 88, game.preview ? amber : teal);
            Label(43, 37, 300, 20, game.preview ? T("SONUÇ / SARSINTI SONRASI", "OUTCOME / AFTER SHAKING") : T("HAZIRLIK / BUGÜN", "PREPARATION / TODAY"), 14, game.preview ? amber : teal);
            Label(43, 67, 300, 24, game.progress.Completed + "/6  " + T("hazırlık adımı", "preparation steps"), 20, white);
            Rect(938, 24, 318, 65, ink);
            Label(956, 37, 285, 43, T("Q  Dünyayı değiştir\nTAB  Günlük     H  İpucu", "Q  Switch state\nTAB  Journal     H  Hint"), 16, white);
            Rect(638, 358, 4, 4, new Color(0.9f, 0.98f, 0.95f, 0.8f));
            var target = game.player.target;
            if (target != null)
            {
                GUI.DrawTexture(new Rect(620, 405, 40, 40), hand);
                Rect(310, 459, 660, 54, ink);
                Label(330, 475, 620, 31, "[ E ]  " + target.Prompt(game), 17, white);
            }
            Label(30, 686, 1200, 22, T("WASD  Hareket     F  Eğitim feneri     SHIFT  Hızlı yürü     ESC  Duraklat", "WASD  Move     F  Training light     SHIFT  Walk faster     ESC  Pause"), 13, muted);
        }

        void Pause()
        {
            Backdrop();
            Label(100, 100, 620, 70, T("Bir nefes.", "Take a breath."), 54, white, FontStyle.Bold);
            Label(104, 185, 520, 50, T("Hazırlıkların otomatik kaydedildi.", "Your preparations have been saved."), 19, muted);
            if (Button(104, 282, 390, 54, T("Devam et", "Resume"), true)) game.SetMode(ScreenMode.Playing);
            if (Button(104, 350, 390, 54, T("Günlük", "Journal"))) game.SetMode(ScreenMode.Journal);
            if (Button(104, 418, 390, 54, T("Nasıl oynanır / Bilgi", "How to play / About"))) game.OpenInfo();
            if (Button(104, 486, 390, 54, T("Ana menü", "Main menu"))) game.ReturnToMenu();
            Preferences(810, 230);
        }

        void Objective(float y, bool done, string tr, string en)
        {
            Rect(85, y, 550, 46, new Color(0.07f, 0.12f, 0.14f));
            Label(101, y + 10, 35, 28, done ? "+" : "—", 22, done ? teal : muted);
            Label(144, y + 11, 470, 28, T(tr, en), 17, done ? white : muted);
        }

        void Journal()
        {
            Backdrop();
            Label(83, 51, 760, 58, T("Hazırlık günlüğü", "Preparation journal"), 42, white, FontStyle.Bold);
            Label(85, 118, 1100, 30, T("Amaç: ev içindeki altı hazırlık adımını tamamla ve sonuçlarını kontrol et.", "Goal: complete six indoor preparation steps and inspect their outcomes."), 18, muted);
            Objective(175, game.progress.Fixed("bookcase"), "Salondaki kitaplığı sabitle", "Secure the living-room bookcase");
            Objective(229, game.progress.Fixed("wardrobe"), "Yatak odasındaki gardırobu sabitle", "Secure the bedroom wardrobe");
            Objective(283, game.progress.Fixed("heavy"), "Ağır eşyayı alt rafa indir", "Move the heavy object to a low shelf");
            Objective(337, game.progress.Fixed("route"), "Girişteki geçişi aç", "Clear the entrance passage");
            Objective(391, game.progress.bagPacked, "Afet çantasını hazırla", "Pack the emergency bag");
            Objective(445, game.progress.familyPlan, "Aile buluşma planını tamamla", "Agree on the family meeting plan");
            Label(686, 174, 450, 28, T("ÇANTA MALZEMELERİ", "BAG SUPPLIES"), 14, teal);
            int i = 0;
            foreach (var id in KeskeProgress.SupplyIds)
            {
                bool has = game.progress.Has(id);
                Label(687, 212 + i * 29, 470, 27, (has ? "+  " : "—  ") + game.SupplyName(id), 17, has ? white : muted);
                i++;
            }
            Label(85, 520, 1080, 34,
                T("Sabitleme seti: ", "Fastening kit: ") + (game.progress.Has("tool") ? T("var", "yes") : T("yok", "no")) + "     /     " +
                T("Çalışma odası anahtarı: ", "Study key: ") + (game.progress.Has("key") ? T("var", "yes") : T("yok", "no")) + "     /     " +
                T("Bulunan not: ", "Notes found: ") + game.progress.memories.Count + "/3", 16, muted);
            if (Button(85, 564, 290, 42, T("Oyuna dön  [TAB]", "Return  [TAB]"), true)) game.SetMode(ScreenMode.Playing);
            if (Button(390, 564, 245, 42, T("İpucu göster", "Show hint"))) game.Notify(game.Hint(), 10);
        }

        void Note()
        {
            Backdrop();
            Rect(230, 92, 820, 495, ink);
            Rect(230, 92, 3, 495, teal);
            Label(266, 116, 730, 24, T("EV 07 / KAYIT", "HOUSE 07 / RECORD"), 13, teal);
            Label(266, 157, 730, 62, game.noteTitle, 34, white, FontStyle.Bold);
            text.fontSize = 20;
            text.normal.textColor = white;
            text.fontStyle = FontStyle.Normal;
            float height = Mathf.Max(265, text.CalcHeight(new GUIContent(game.noteBody), 696));
            scroll = GUI.BeginScrollView(new Rect(266, 235, 740, 260), scroll, new Rect(0, 0, 696, height));
            GUI.Label(new Rect(0, 0, 696, height), game.noteBody, text);
            GUI.EndScrollView();
            if (Button(266, 520, 320, 45, T("Devam et  [ESC]", "Continue  [ESC]"), true)) game.SetMode(ScreenMode.Playing);
        }

        void Plan()
        {
            Backdrop();
            Label(150, 62, 1020, 60, T("Birbirimizi nasıl bulacağız?", "How will we find each other?"), 38, white, FontStyle.Bold);
            Label(153, 145, 955, 100, game.noteBody, 20, muted);
            if (Button(153, 273, 975, 74, T("A  ·  Herkes eve geri dönsün, içeride buluşuruz.", "A  ·  Everyone returns to the house; we will meet inside."))) game.ChoosePlan(0);
            if (Button(153, 363, 975, 74, T("B  ·  Önceden belirlenmiş buluşma yeri ve bölge dışı iletişim kişisi.", "B  ·  An agreed meeting place and an out-of-area contact."))) game.ChoosePlan(1);
            if (Button(153, 453, 975, 74, T("C  ·  Bir plan yapmayalım; o anda birbirimizi ararız.", "C  ·  No advance plan; we will call one another at the time."))) game.ChoosePlan(2);
            if (Button(153, 548, 250, 42, T("Şimdilik kapat", "Close for now"))) game.SetMode(ScreenMode.Playing);
        }

        void Info()
        {
            Backdrop();
            Label(110, 52, 1050, 60, T("Başlamadan önce", "Before you begin"), 42, white, FontStyle.Bold);
            string body = T(
                "OYUN\nKEŞKE, aynı evin hazırlık ve sonuç hâlleri arasında geçiş yaptığın birinci şahıs bir çevre bulmacasıdır. Hazırlıkta eşyaları topla ve risklere önlem al; sonuçta kararlarının etkisini gör.\n\nKONTROLLER\nWASD / yön tuşları: hareket · Fare: bakış · E: etkileşim · Q: hazırlık/sonuç geçişi · F: eğitim feneri · TAB: günlük · H: ipucu · SHIFT: hızlı yürü · ESC: duraklat.\n\nAKIŞ\nAltı adımı tamamla: kitaplık, gardırop, ağır eşya, geçiş yolu, afet çantası ve aile planı. Q ile son hazırlıklarını test et. Sonuç modundayken bahçedeki terminalden provayı bitir. Ekrandaki el simgesi, E ile etkileşim kurabileceğini gösterir. Q geçişleri seni bulunduğun katın sabit gözlem noktasına taşır.\n\nTATBİKAT ÇERÇEVESİ\nSonuç modu, sarsıntı durduktan sonraki kurgusal bir önizlemedir. Sarsıntı sırasında evde eşya toplamayı veya çıkışa koşmayı öğretmez. Gerçek sarsıntı sırasında AFAD'ın Çök–Kapan–Tutun yönlendirmesi geçerlidir. Binanın yapısal güvenliği bu oyunda değerlendirilmez.\n\nHAZIRLIK\nÇantadaki dokuz malzeme örnek bir seçkidir; kişi sayısı ve özel ihtiyaçlara göre gerçek içerik değişir. Sabitleme işlemi oyunda soyutlanmıştır; gerçek uygulamada uygun malzeme ve gerektiğinde uzman desteği gerekir. Aile planı birlikte konuşulmalı ve uygulanmalıdır.\n\nATMOSFER VE KAYIT\nKısa bir gölge efekti ve gerilimli sesler bulunur. Ani korkutmayı ayarlardan kapatabilirsin. İlerleme otomatik kaydedilir; Devam et seçeneği eşyaları ve tamamlanan hazırlıkları geri yükler, oyuncuyu girişe getirir. Yeni oyun önceki ilerlemeyi sıfırlar.\n\nKAYNAK\nHazırlık bilgileri AFAD'ın afet çantası, aile planı ve ev içi risk azaltma önerileri temel alınarak sadeleştirilmiştir. Kaynak bağlantıları paket içindeki KURULUM.html dosyasındadır. Oyun resmî AFAD ürünü veya mühendislik simülasyonu değildir.",
                "THE GAME\nIF ONLY is a first-person environmental puzzle about switching between a house's preparation and outcome states. Gather supplies and address risks in preparation; inspect the results in outcome.\n\nCONTROLS\nWASD / arrows: move · Mouse: look · E: interact · Q: switch preparation/outcome · F: training light · TAB: journal · H: hint · SHIFT: walk faster · ESC: pause.\n\nFLOW\nComplete six steps: bookcase, wardrobe, heavy object, passage, emergency bag and family plan. Use Q to test your latest preparations. Finish at the garden terminal while in outcome mode. The hand icon means you can interact with E. Q takes you to a fixed observation spot on your current floor.\n\nTRAINING CONTEXT\nOutcome mode is a fictional preview after the shaking has stopped. It does not teach collecting objects or running to exits during shaking. Follow AFAD's Drop–Cover–Hold On guidance during actual shaking. This game does not assess a building's structural safety.\n\nPREPARATION\nThe nine bag items are an example selection; actual contents depend on household size and individual needs. Fastening is abstracted; real installation requires suitable materials and qualified help where needed. Discuss and practise a family plan together.\n\nATMOSPHERE AND SAVING\nThere is a brief shadow effect and suspenseful audio. Disable the startle effect in settings if preferred. Progress saves automatically. Continue restores items and preparations and places you at the entrance. New game resets previous progress.\n\nSOURCES\nPreparedness content is simplified from AFAD guidance on emergency bags, family plans and indoor risk reduction. Source links are in KURULUM.html in the package. This is not an official AFAD product or an engineering simulation.");
            text.fontSize = 19; text.normal.textColor = white; text.fontStyle = FontStyle.Normal;
            float h = text.CalcHeight(new GUIContent(body), 1010);
            scroll = GUI.BeginScrollView(new Rect(110, 145, 1060, 420), scroll, new Rect(0, 0, 1010, h));
            GUI.Label(new Rect(0, 0, 1010, h), body, text);
            GUI.EndScrollView();
            if (Button(110, 590, 300, 48, T("Geri dön", "Back"), true)) game.CloseInfo();
        }

        void End()
        {
            Backdrop();
            Label(110, 60, 1040, 25, T("EV 07 / PROVA TAMAMLANDI", "HOUSE 07 / TRAINING COMPLETE"), 14, teal);
            Label(104, 108, 1100, 85, T("Keşke demeden önce.", "Before saying if only."), 60, white, FontStyle.Bold);
            Label(110, 210, 1050, 80, T("Altı hazırlık adımını tamamladın ve son düzenini kontrol ettin.\nKüçük hazırlıkların farkını aynı evde gördün.", "You completed six preparation steps and tested your final setup.\nIn the same house, you saw the difference those preparations made."), 23, white);
            Rect(110, 330, 1060, 135, ink);
            Label(140, 351, 310, 58, "6 / 6", 46, teal, FontStyle.Bold);
            Label(140, 416, 340, 30, T("Hazırlık adımı", "Preparation steps"), 17, muted);
            Label(500, 351, 320, 58, Mathf.FloorToInt(game.progress.seconds / 60) + ":" + Mathf.FloorToInt(game.progress.seconds % 60).ToString("00"), 46, white, FontStyle.Bold);
            Label(500, 416, 300, 30, T("Oyun süresi", "Play time"), 17, muted);
            Label(865, 351, 270, 58, game.progress.memories.Count + " / 3", 46, white, FontStyle.Bold);
            Label(865, 416, 270, 30, T("Bulunan not", "Notes found"), 17, muted);
            Label(110, 497, 1060, 60, T("Bu sonuç ev içi hazırlık adımlarını gösterir; binanın güvenli olduğunu göstermez. Gerçek hazırlığını hane ihtiyaçların ve resmî yönlendirmelerle tamamla.", "This result reflects indoor preparation steps, not a building safety assessment. Complete real preparations using household needs and official guidance."), 17, muted);
            if (Button(110, 591, 310, 50, T("Yeniden oyna", "Play again"), true)) game.Begin(false);
            if (Button(438, 591, 310, 50, T("Ana menü", "Main menu"))) game.MenuView();
        }

        static Texture2D MakeHand()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            texture.name = "Keske_HandIcon";
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                bool filled = (x >= 21 && x <= 44 && y >= 14 && y <= 36) ||
                    (x >= 23 && x <= 39 && y >= 6 && y <= 20) ||
                    (x >= 18 && x <= 24 && y >= 30 && y <= 48) ||
                    (x >= 26 && x <= 32 && y >= 31 && y <= 57) ||
                    (x >= 34 && x <= 40 && y >= 31 && y <= 53) ||
                    (x >= 42 && x <= 48 && y >= 27 && y <= 44) ||
                    (x >= 10 && x <= 24 && y >= 21 && y <= 30 && y <= x + 13);
                pixels[y * 64 + x] = filled ? new Color(0.9f, 1, 0.96f, 1) : Color.clear;
            }
            texture.SetPixels(pixels); texture.Apply(); texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        void Quit()
        {
            game.Preferences();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        void OnDestroy() { if (hand != null) Destroy(hand); }
    }
}
