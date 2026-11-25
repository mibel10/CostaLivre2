using System;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class HarborMenu
    {
        private enum ScreenPage { Deploy, Loadout, Options, Jobs, Online }
        private enum SettingsPage { Controls, Graphics, Language }

        private static readonly Color Paper = new(0.87f, 0.87f, 0.81f);
        private static readonly Color Grey = new(0.60f, 0.65f, 0.63f);
        private static readonly Color Copper = new(0.83f, 0.56f, 0.37f);
        private static readonly Color Carbon = new(0.047f, 0.063f, 0.066f, 0.94f);
        private static readonly Color Line = new(0.25f, 0.32f, 0.31f, 0.92f);

        private readonly Texture2D gradient;
        private ScreenPage page;
        private SettingsPage settings;
        private bool editTouch;
        private const string PresetKey = "CostaGraphicsPreset";
        private static readonly float[] ViewDistances = { 95f, 155f, 245f, 360f, 540f };
        private int quality = Mathf.Clamp(PlayerPrefs.GetInt(PresetKey,
            PlayerPrefs.GetInt("CostaQuality", Application.isMobilePlatform ? 0 : 1) == 0 ? 1 : 3), 0, 4);
        private readonly Camera camera;
        private int frameCap = PlayerPrefs.GetInt("CostaFrameCap", 60);
        private bool vsync = PlayerPrefs.GetInt("CostaVSync", 0) != 0;
        private readonly CostaBindings bindings;

        private readonly CostaPadBindings padBindings;
        private bool showPadBindings;
        private int padPage;

        internal bool CapturingKey => bindings.Waiting || padBindings.Waiting;
        internal void ReadBinding()
        {
            if (padBindings.Waiting)
                padBindings.Tick();
            else
                bindings.Tick();
        }
        internal void ShowJobs() => page = ScreenPage.Jobs;
        internal void ShowOnline() => page = ScreenPage.Online;
        internal bool OnlinePage => page == ScreenPage.Online;
        private GUIStyle headline;
        private GUIStyle subhead;
        private GUIStyle label;
        private GUIStyle button;
        internal int SelectedWeapon { get; private set; }
        internal int SelectionVersion { get; private set; }
        internal float Sensitivity { get; private set; } = PlayerPrefs.GetFloat("HarborSensitivity", 1f);
        internal float Volume { get; private set; } = PlayerPrefs.GetFloat("HarborVolume", 0.85f);

        internal HarborMenu(CostaBindings bindings, CostaPadBindings padBindings, Camera camera)
        {
            this.bindings = bindings;
            this.padBindings = padBindings;
            this.camera = camera;
            ApplyGraphics();
            int fullscreen = PlayerPrefs.GetInt("CostaFullscreen", -1);
            if (fullscreen >= 0)
                Screen.fullScreenMode = fullscreen > 0 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            gradient = new Texture2D(32, 1, TextureFormat.RGBA32, false);
            gradient.wrapMode = TextureWrapMode.Clamp;
            for (int i = 0; i < gradient.width; i++)
            {
                float t = i / (gradient.width - 1f);
                gradient.SetPixel(i, 0, new Color(0.025f, 0.041f, 0.046f, Mathf.Pow(1f - t, 1.14f) * 0.99f));
            }
            gradient.Apply(false, true);
            AudioListener.volume = Volume;
        }

        internal void Draw(bool resume, Action deploy, Action continueRun, Action quit, CostaJobs jobs, Action<int> selectJob,
            CostaOnlinePanel online, HarborArsenal arsenal, bool sessionActive, CostaTouch touch)
        {
            if (editTouch)
            {
                if (touch.DrawEditor())
                    editTouch = false;
                return;
            }

            if (headline == null)
                CreateStyles();

            Matrix4x4 original = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 924f, Screen.height / 544f, 1.65f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.DrawTexture(new Rect(0, 0, width * 0.79f, height), gradient);
            Text(new Rect(29, 21, 260, 26), "COSTA LIVRE 2", subhead, Paper);

            float x = 173;
            float top = Mathf.Max(99, height * 0.18f);
            float rail = top + 33;
            Tab(new Rect(28, rail, 124, 37), ScreenPage.Deploy, L("Play", "Jogar"));
            Tab(new Rect(28, rail + 43, 124, 37), ScreenPage.Loadout, L("Loadout", "Equipamento"));
            Tab(new Rect(28, rail + 86, 124, 37), ScreenPage.Options, L("Options", "Opções"));
            if (!sessionActive)
                Tab(new Rect(28, rail + 129, 124, 37), ScreenPage.Jobs, L("Jobs", "Missões"));
            Tab(new Rect(28, rail + (sessionActive ? 129 : 172), 124, 37), ScreenPage.Online, "Multiplayer");
            if (MenuButton(new Rect(28, height - 91, 124, 34), L("Quit", "Sair"), false))
                quit();

            switch (page)
            {
                case ScreenPage.Deploy:
                    Text(new Rect(x, top + 12, 417, 74), sessionActive ? "Multiplayer" : "Santa Luz", headline, Paper);
                    if (MenuButton(new Rect(x, top + 101, 202, 48), resume ? L("Resume", "Continuar") : L("Enter the city", "Entrar na cidade"), true))
                        (resume ? continueRun : deploy)();
                    if (resume && !sessionActive && MenuButton(new Rect(x, top + 161, 202, 34), L("Restart", "Recomeçar"), false))
                        deploy();
                    break;
                case ScreenPage.Loadout:
                    Text(new Rect(x, top, 300, 63), L("Loadout", "Equipamento"), headline, Paper);
                    int owned = 0;
                    for (int i = 0; i < HarborArsenal.WeaponNames.Length; i++)
                    {
                        if (!arsenal.Owned(i))
                            continue;
                        if (MenuButton(new Rect(x, top + 77 + owned * 38, 222, 33), Weapon(i), i == SelectedWeapon))
                        {
                            SelectedWeapon = i;
                            SelectionVersion++;
                        }
                        owned++;
                    }
                    if (owned == 0)
                        Text(new Rect(x, top + 75, 372, 32), L("Buy equipment at the gun shop.", "Compre equipamento na armaria."), label, Grey);
                    break;
                case ScreenPage.Options:
                    DrawOptions(x, top);
                    break;
                case ScreenPage.Jobs:
                    if (!sessionActive)
                        DrawJobs(x, top, jobs, selectJob);
                    break;
                case ScreenPage.Online:
                    online.Draw(x, top - 9, height, continueRun);
                    break;
            }

            GUI.matrix = original;
        }

        private void DrawJobs(float x, float top, CostaJobs jobs, Action<int> select)
        {
            Text(new Rect(x, top - 14, 400, 69), L("Jobs board", "Missões"), headline, Paper);
            Text(new Rect(x, top + 52, 355, 24), Money(jobs.Credits), label, Copper);
            float y = top + 85;
            if (MenuButton(new Rect(x, y, 349, 30), L("Harbor cargo", "Carga do porto"), false))
                select(-2);
            for (int i = 0; i < CostaJobs.List.Length; i++)
            {
                var job = CostaJobs.List[i];
                string status = jobs.Completed(i) ? L("Done", "Concluída") : Money(job.Reward);
                if (MenuButton(new Rect(x, y + (i + 1) * 36, 274, 30), job.Title, jobs.Active == i))
                    select(i);
                Text(new Rect(x + 282, y + (i + 1) * 36, 109, 30), status, label, Grey);
            }

            if (MenuButton(new Rect(x, y + 7 * 36, 274, 29), L("Explore freely", "Explorar livremente"), false))
                select(-1);
        }

        private void DrawOptions(float x, float top)
        {
            Text(new Rect(x, top - 20, 380, 70), L("Options", "Opções"), headline, Paper);
            float tab = top + 50;
            if (MenuButton(new Rect(x, tab, 111, 33), L("Controls", "Controles"), settings == SettingsPage.Controls))
                settings = SettingsPage.Controls;
            if (MenuButton(new Rect(x + 116, tab, 109, 33), L("Graphics", "Gráficos"), settings == SettingsPage.Graphics))
                settings = SettingsPage.Graphics;
            if (MenuButton(new Rect(x + 230, tab, 120, 33), L("Language", "Idioma"), settings == SettingsPage.Language))
                settings = SettingsPage.Language;

            if (CostaTouch.Available && MenuButton(new Rect(x + 355, tab, 135, 33), L("Touch layout", "Toque"), false))
                editTouch = true;

            float y = tab + 48;
            switch (settings)
            {
                case SettingsPage.Controls:
                    DrawControls(x, y);
                    break;
                case SettingsPage.Graphics:
                    DrawGraphics(x, y);
                    break;
                case SettingsPage.Language:
                    if (MenuButton(new Rect(x, y, 155, 38), "English", !Portuguese))
                        Language(false);
                    if (MenuButton(new Rect(x + 161, y, 172, 38), "Português (Brasil)", Portuguese))
                        Language(true);
                    break;
            }
        }

        private void DrawControls(float x, float y)
        {
            bool old = GUI.enabled;
            GUI.enabled = old && !CapturingKey;
            if (MenuButton(new Rect(x, y, 120, 25), L("Keyboard", "Teclado"), !showPadBindings))
                showPadBindings = false;
            if (MenuButton(new Rect(x + 130, y, 120, 25), L("Gamepad", "Controle"), showPadBindings))
                showPadBindings = true;
            GUI.enabled = old;
            if (showPadBindings)
            {
                DrawPadBindings(x, y);
                return;
            }
            string[] names = { L("Run", "Correr"), L("Jump / brake", "Pular / frear"), L("Roll", "Rolar"),
                L("Interact / enter car", "Interagir / entrar no carro"), L("Reload", "Recarregar") };
            GUI.enabled = old && !bindings.Waiting;
            for (int i = 0; i < names.Length; i++)
            {
                float row = y + 30 + i * 28;
                Text(new Rect(x, row, 215, 25), names[i], label, Grey);
                if (MenuButton(new Rect(x + 211, row, 128, 24), bindings.Name(i), false))
                    bindings.Begin(i);
            }

            GUI.enabled = old;
            if (bindings.Waiting)
            {
                Text(new Rect(x, y + 175, 405, 22), bindings.Error.Length > 0 ? bindings.Error : L("Press a key or Esc to cancel.", "Pressione uma tecla ou Esc para cancelar."), label, Copper);
                if (MenuButton(new Rect(x + 213, y + 204, 127, 26), L("Cancel", "Cancelar"), false))
                    bindings.Cancel();
            }
            else if (MenuButton(new Rect(x + 213, y + 204, 127, 26), L("Reset keys", "Restaurar"), false))
                bindings.Reset();
            Text(new Rect(x, y + 248, 380, 22), L("M Map   ·   I Backpack   ·   1 / 2 Weapons",
                "M Mapa   ·   I Mochila   ·   1 / 2 Armas"), label, Grey);
            float sensitivity = CostaUi.Slider(new Rect(x, y + 217, 188, 18), Sensitivity, 0.35f, 2.1f);
            Text(new Rect(x, y + 194, 206, 22), L($"Mouse   {sensitivity:0.00}", $"Sensibilidade   {sensitivity:0.00}"), label, Paper);
            if (!Mathf.Approximately(Sensitivity, sensitivity))
            {
                Sensitivity = sensitivity;
                PlayerPrefs.SetFloat("HarborSensitivity", sensitivity);
            }
        }

        private void DrawPadBindings(float x, float y)
        {
            string[] actions = { L("Run", "Correr"), L("Jump / brake", "Pular / frear"), L("Roll", "Rolar"),
                L("Interact / vehicle", "Interagir / veículo"), L("Reload", "Recarregar"), L("Fire", "Atirar"),
                L("Aim", "Mirar"), L("Next weapon", "Próxima arma"), L("Previous weapon", "Arma anterior"),
                L("Crouch", "Agachar"), L("Map", "Mapa"), L("Backpack", "Mochila"),
                "Menu", L("Jobs", "Missões") };
            bool old = GUI.enabled;
            GUI.enabled = old && !padBindings.Waiting;
            const int pageSize = 5;
            for (int i = padPage * pageSize; i < Mathf.Min((padPage + 1) * pageSize, padBindings.Count); i++)
            {
                float row = y + 34 + (i % pageSize) * 28;
                Text(new Rect(x, row, 205, 24), actions[i], label, Grey);
                if (MenuButton(new Rect(x + 211, row, 128, 24), padBindings.Name(i), false))
                    padBindings.Begin(i);
            }
            if (padPage > 0 && MenuButton(new Rect(x, y + 204, 42, 26), "‹", false))
                padPage--;
            if ((padPage + 1) * pageSize < padBindings.Count && MenuButton(new Rect(x + 53, y + 204, 42, 26), "›", false))
                padPage++;
            if (MenuButton(new Rect(x + 213, y + 204, 127, 26), L("Reset gamepad", "Restaurar"), false))
                padBindings.Reset();
            GUI.enabled = old;
            if (padBindings.Waiting)
            {
                Text(new Rect(x, y + 175, 425, 24), padBindings.Error.Length > 0 ? padBindings.Error :
                    L("Press a button. Menu / Esc cancels.", "Pressione um botão. Menu / Esc cancela."), label, Copper);
                if (MenuButton(new Rect(x + 213, y + 204, 127, 26), L("Cancel", "Cancelar"), false))
                    padBindings.Cancel();
            }
        }

        private void DrawGraphics(float x, float y)
        {
            string[] presets = { L("Super Low (Potato)", "Super baixa (Batata)"), L("Low", "Baixa"),
                L("Medium", "Média"), L("High", "Alta"), "Ultra" };
            Text(new Rect(x, y, 115, 24), L("Quality", "Qualidade"), label, Paper);
            for (int i = 0; i < presets.Length; i++)
            {
                float left = i == 0 ? x + 105 : x + 250 + (i - 1) * 72;
                if (!MenuButton(new Rect(left, y, i == 0 ? 139 : 66, 26), presets[i], quality == i) || quality == i)
                    continue;
                quality = i;
                ApplyGraphics();
                PlayerPrefs.Save();
            }

            Text(new Rect(x, y + 39, 115, 26), L("Display", "Tela"), label, Paper);
            if (MenuButton(new Rect(x + 105, y + 39, 108, 27), L("Windowed", "Janela"), !Screen.fullScreen))
                SetFullscreen(false);
            if (MenuButton(new Rect(x + 219, y + 39, 118, 27), L("Fullscreen", "Tela cheia"), Screen.fullScreen))
                SetFullscreen(true);

            Text(new Rect(x, y + 78, 105, 24), L("Frame limit", "Limite de FPS"), label, Paper);
            if (MenuButton(new Rect(x + 105, y + 78, 57, 26), "30", frameCap == 30))
            {
                frameCap = 30;
                ApplyGraphics();
            }
            if (MenuButton(new Rect(x + 168, y + 78, 57, 26), "60", frameCap == 60))
            {
                frameCap = 60;
                ApplyGraphics();
            }
            if (MenuButton(new Rect(x + 231, y + 78, 106, 26), "VSync", vsync))
            {
                vsync = !vsync;
                ApplyGraphics();
            }

            Text(new Rect(x, y + 120, 280, 24), L($"Master volume   {Volume:P0}", $"Volume geral   {Volume:P0}"), label, Paper);
            float volume = CostaUi.Slider(new Rect(x, y + 149, 229, 20), Volume, 0f, 1f);
            if (!Mathf.Approximately(Volume, volume))
            {
                Volume = volume;
                AudioListener.volume = Volume;
                PlayerPrefs.SetFloat("HarborVolume", volume);
            }
        }

        private void ApplyGraphics()
        {
            int level = Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != level)
                QualitySettings.SetQualityLevel(level, true);
            camera.farClipPlane = ViewDistances[quality];
            QualitySettings.vSyncCount = vsync ? 1 : 0;
            Application.targetFrameRate = frameCap;
            PlayerPrefs.SetInt(PresetKey, quality);
            PlayerPrefs.SetInt("CostaFrameCap", frameCap);
            PlayerPrefs.SetInt("CostaVSync", vsync ? 1 : 0);
        }

        private static void SetFullscreen(bool fullscreen)
        {
            Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            PlayerPrefs.SetInt("CostaFullscreen", fullscreen ? 1 : 0);
        }

        internal void Release()
        {
            PlayerPrefs.Save();
            UnityEngine.Object.Destroy(gradient);
        }

        private void Tab(Rect bounds, ScreenPage id, string name)
        {
            if (MenuButton(bounds, name, page == id))
                page = id;
        }

        private bool MenuButton(Rect bounds, string name, bool selected)
        {
            bool hover = bounds.Contains(Event.current.mousePosition);
            Fill(bounds, selected ? Copper : hover ? Line : Carbon);
            if (!selected)
                Fill(new Rect(bounds.x, bounds.y, 2, bounds.height), Line);
            Color before = GUI.contentColor;
            GUI.contentColor = selected ? Carbon : Paper;
            int fontSize = button.fontSize;
            float available = bounds.width - 6;
            float required = button.CalcSize(new GUIContent(name)).x;
            if (required > available)
                button.fontSize = Mathf.Max(11, Mathf.FloorToInt(fontSize * available / required));
            bool pressed = CostaUi.Button(bounds, name, button);
            button.fontSize = fontSize;
            GUI.contentColor = before;
            return pressed;
        }

        private void CreateStyles()
        {
            headline = Style(56, FontStyle.Bold);
            subhead = Style(17, FontStyle.Normal, true);
            label = Style(13, FontStyle.Bold);
            button = Style(16, FontStyle.Bold);
            button.padding = new RectOffset(13, 0, 0, 0);
        }

        private static GUIStyle Style(int size, FontStyle weight, bool wrap = false)
        {
            return new GUIStyle
            {
                font = GUI.skin.font,
                fontSize = size,
                fontStyle = weight,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = wrap,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.white }
            };
        }

        private static void Text(Rect bounds, string value, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(bounds, value, style);
        }

        private static void Fill(Rect bounds, Color color)
        {
            Color before = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = before;
        }
    }
}
