using System;
using UnityEngine;
using UnityEngine.Rendering;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class HarborMenu
    {
        private enum ScreenPage { Deploy, Loadout, Options, Jobs, Online }
        private enum SettingsPage { Controls, Graphics, Display, Language }

        private static readonly Color Paper = new(0.99f, 0.96f, 0.91f);
        private static readonly Color Grey = new(0.76f, 0.84f, 0.85f);
        private static readonly Color Copper = new(0.98f, 0.66f, 0.34f);
        private static readonly Color Coral = new(0.95f, 0.42f, 0.44f);
        private static readonly Color Turquoise = new(0.18f, 0.82f, 0.80f);
        private static readonly Color Carbon = new(0.055f, 0.082f, 0.125f, 0.90f);
        private static readonly Color Line = new(0.28f, 0.48f, 0.54f, 0.65f);
        private static readonly Color Hover = new(0.11f, 0.24f, 0.32f, 0.96f);
        private static readonly Color Rail = new(0.038f, 0.058f, 0.100f, 0.86f);
        private static readonly GUIContent MeasureContent = new();

        private readonly struct MenuFrame
        {
            internal readonly Rect Safe;
            internal readonly float Scale, Width, Height;
            internal readonly bool Compact, Portrait;

            internal MenuFrame(Rect safe)
            {
                Safe = safe;
                float aspect = safe.width / safe.height;
                Compact = Application.isMobilePlatform || aspect < 1.5f;
                Portrait = aspect < 1.15f;
                Scale = Compact ? Mathf.Min(safe.width / 650f, safe.height / 482f, 3f)
                    : Mathf.Min(safe.width / 1010f, safe.height / 570f, 2.25f);
                Width = safe.width / Scale;
                Height = safe.height / Scale;
            }

            internal static MenuFrame Current
            {
                get
                {
                    Rect safe = Screen.safeArea;
                    if (safe.width <= 0 || safe.height <= 0)
                        safe = new Rect(0, 0, Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
                    return new MenuFrame(safe);
                }
            }
        }

        internal Vector2 PreviewViewport
        {
            get
            {
                MenuFrame frame = MenuFrame.Current;
                bool stacked = frame.Portrait && page == ScreenPage.Deploy;
                float x = stacked ? 0.53f : 0.77f;
                float y = stacked ? 0.68f : 0.49f;
                return new Vector2((frame.Safe.x + frame.Safe.width * x) / Mathf.Max(1, Screen.width),
                    (frame.Safe.y + frame.Safe.height * y) / Mathf.Max(1, Screen.height));
            }
        }

        private readonly Texture2D gradient;
        private readonly Texture2D accentBar;
        private ScreenPage page;
        private float pageChanged = -2f;
        private SettingsPage settings;
        private bool editTouch;
        private readonly HarborGraphics graphics;
        private int frameCap = PlayerPrefs.GetInt("CostaFrameCap", 60);
        private bool vsync = PlayerPrefs.GetInt("CostaVSync", 0) != 0;
        private readonly CostaBindings bindings;

        private readonly CostaPadBindings padBindings;
        private bool showPadBindings;
        private int padPage;
        private int jobPage;
        private const int JobsPerPage = 6;

        internal void SaveGraphicsIfDue() => graphics.SaveIfDue();
        internal int AdaptiveWorldTier => graphics.WorldTier;
        internal void UpdateAdaptiveResolution(bool inGameplay) => graphics.UpdateAdaptiveResolution(inGameplay);
        internal void FlushGraphics() => graphics.Flush();
        internal void SetWorldHour(float? hour, HarborWeather.Conditions weather) => graphics.SetWorldHour(hour, weather);
        internal int GraphicsPreset => graphics.Preset;
        internal HarborHud.Mode HudMode { get; private set; } =
            (HarborHud.Mode)Mathf.Clamp(PlayerPrefs.GetInt("CostaHudMode", 1), 0, 2);
        private int titleBackground = Mathf.Clamp(PlayerPrefs.GetInt("CostaCityTitle", -1), -1, 1);
        internal bool LiveTitle => titleBackground < 0 ? GraphicsPreset >= 2 : titleBackground == 1;
        internal bool LoadoutPage => page == ScreenPage.Loadout;
        internal int MirrorQuality { get; private set; } = Mathf.Clamp(PlayerPrefs.GetInt("CostaMirrorQuality", 0), 0, 4);

        internal int StreetLightBudget => graphics.StreetLightBudget;
        internal bool Ultra => graphics.Preset == HarborGraphics.MaxPreset;
        internal bool FullTextures => graphics.TextureQuality == 3;
        internal bool CapturingKey => bindings.Waiting || padBindings.Waiting;
        internal void ReadBinding()
        {
            if (padBindings.Waiting)
                padBindings.Tick();
            else
                bindings.Tick();
        }
        internal void ShowDeploy() => OpenPage(ScreenPage.Deploy);
        internal void ShowJobs() => OpenPage(ScreenPage.Jobs);
        internal void ShowOnline() => OpenPage(ScreenPage.Online);
        internal bool DeployPage => page == ScreenPage.Deploy;
        internal bool OnlinePage => page == ScreenPage.Online;
        private GUIStyle headline;
        private GUIStyle pauseHeadline;
        private GUIStyle hero;
        private GUIStyle subhead;
        private GUIStyle label;
        private GUIStyle button;
        private GUIStyle centeredButton;
        internal int SelectedWeapon { get; private set; }
        internal int SelectionVersion { get; private set; }
        internal float Sensitivity { get; private set; } = PlayerPrefs.GetFloat("HarborSensitivity", 1f);
        internal float MusicVolume { get; private set; } = Mathf.Clamp01(PlayerPrefs.GetFloat("CostaMusicVolume", .58f));
        internal float CameraShake { get; private set; } = Mathf.Clamp01(PlayerPrefs.GetFloat("CostaCameraShake", 0.65f));
        internal float Volume { get; private set; } = PlayerPrefs.GetFloat("HarborVolume", 0.85f);

        internal HarborMenu(CostaBindings bindings, CostaPadBindings padBindings, Camera camera, Light sun, Volume volume)
        {
            this.bindings = bindings;
            this.padBindings = padBindings;
            graphics = new HarborGraphics(camera, sun, volume);
            ApplyFrameSettings();
            int fullscreen = PlayerPrefs.GetInt("CostaFullscreen", -1);
            if (!Application.isMobilePlatform && fullscreen >= 0)
                Screen.fullScreenMode = fullscreen > 0 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            gradient = new Texture2D(64, 32, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var colors = new Color32[gradient.width * gradient.height];
            for (int y = 0; y < gradient.height; y++)
            {
                float vertical = y / (gradient.height - 1f);
                float edge = Mathf.Pow(Mathf.Abs(vertical * 2f - 1f), 3f) * 0.45f;
                float warmBottom = Mathf.Clamp01(1f - vertical * 2.1f);
                for (int x = 0; x < gradient.width; x++)
                {
                    float horizontal = x / (gradient.width - 1f);
                    float shade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.24f, 0.80f, horizontal));
                    Color baseTint = Color.Lerp(new Color(0.042f, 0.068f, 0.125f), new Color(0.12f, 0.065f, 0.11f), warmBottom * 0.65f);
                    baseTint.a = Mathf.Lerp(edge, 0.92f, shade);
                    colors[y * gradient.width + x] = baseTint;
                }
            }
            gradient.SetPixels32(colors);
            gradient.Apply(false, true);

            accentBar = new Texture2D(64, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var barPixels = new Color32[accentBar.width];
            for (int x = 0; x < accentBar.width; x++)
            {
                float t = x / (accentBar.width - 1f);
                Color c = t < 0.5f
                    ? Color.Lerp(Coral, Copper, t * 2f)
                    : Color.Lerp(Copper, Turquoise, (t - 0.5f) * 2f);
                barPixels[x] = c;
            }
            accentBar.SetPixels32(barPixels);
            accentBar.Apply(false, true);
            AudioListener.volume = Volume;
        }

        internal void Draw(bool resume, Action deploy, Action continueRun, Action quit, CostaJobs jobs, Action<int> selectJob,
            CostaOnlinePanel online, Func<int, bool> ownsWeapon, bool sessionActive, CostaTouch touch)
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
            MenuFrame frame = MenuFrame.Current;
            float width = frame.Width;
            float height = frame.Height;
            float left = frame.Safe.x;
            float topInset = Screen.height - frame.Safe.yMax;
            bool stacked = frame.Portrait && page == ScreenPage.Deploy;
            float x = frame.Compact ? 24 : 242;
            float top = frame.Compact ? 88 : Mathf.Max(105, height * 0.18f);
            if (stacked)
                top = Mathf.Max(146, height * 0.55f);

            if (stacked)
            {
                Matrix4x4 rotate = Matrix4x4.TRS(new Vector3(0, Screen.height, 0),
                    Quaternion.Euler(0, 0, -90), Vector3.one);
                GUI.matrix = original * rotate;
                GUI.DrawTexture(new Rect(0, 0, Screen.height, Screen.width), gradient);
                GUI.matrix = original;
            }
            else
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), gradient);

            GUI.matrix = original * Matrix4x4.TRS(new Vector3(left, topInset, 0),
                Quaternion.identity, new Vector3(frame.Scale, frame.Scale, 1));
            DrawNavigation(frame, sessionActive, quit);

            top += PageOffset();
            if (DeployPage)
                DrawDeploy(x, stacked ? top : top + (height - top) * 0.21f, resume, sessionActive, deploy, continueRun);
            else
                DrawPage(x, top, height, jobs, selectJob, online, ownsWeapon, sessionActive, continueRun);

            GUI.matrix = original;
        }

        internal void DrawPause(bool resume, bool sessionActive, Action deploy, Action continueRun,
            Action home, CostaJobs jobs, Action<int> selectJob, CostaOnlinePanel online, Func<int, bool> ownsWeapon, CostaTouch touch)
        {
            if (editTouch)
            {
                if (touch.DrawEditor())
                    editTouch = false;
                return;
            }

            if (headline == null)
                CreateStyles();

            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.015f, 0.032f, 0.045f, 0.64f));
            MenuFrame frame = MenuFrame.Current;
            Matrix4x4 original = GUI.matrix;
            GUI.matrix = original * Matrix4x4.TRS(new Vector3(frame.Safe.x, Screen.height - frame.Safe.yMax, 0),
                Quaternion.identity, new Vector3(frame.Scale, frame.Scale, 1));

            float cardWidth = DeployPage ? 440 : 624;
            float cardHeight = DeployPage ? 361 : 459;
            float left = (frame.Width - cardWidth) * 0.5f;
            float top = (frame.Height - cardHeight) * 0.5f + PageOffset();
            float x = left + 16;
            Fill(new Rect(left, top, cardWidth, cardHeight), new Color(0.042f, 0.068f, 0.115f, 0.95f));
            GUI.DrawTexture(new Rect(left, top, cardWidth, 3), accentBar);

            if (DeployPage)
            {
                Text(new Rect(x + 3, top + 18, cardWidth - 35, 64),
                    sessionActive ? L("Match menu", "Menu da partida") : L("Paused", "Pausa"), pauseHeadline, Paper);
                Text(new Rect(x + 4, top + 83, cardWidth - 35, 27),
                    sessionActive ? L("Online session is still running", "Sessão multiplayer em andamento") : "Santa Luz", label, Grey);
                Rect primary = new(x, top + 123, cardWidth - 32, 51);
                if (MenuButton(primary, resume ? L("Resume", "Continuar") : L("Try again", "Tentar de novo"), true))
                    (resume ? continueRun : deploy)();

                if (MenuButton(new Rect(x, top + 188, 199, 42), L("Options", "Opções"), false))
                    OpenPage(ScreenPage.Options);
                if (MenuButton(new Rect(x + 209, top + 188, 199, 42), L("Loadout", "Equipamento"), false))
                    OpenPage(ScreenPage.Loadout);
                if (!sessionActive && MenuButton(new Rect(x, top + 237, 199, 42), L("Jobs", "Missões"), false))
                    ShowJobs();
                if (MenuButton(new Rect(x + 209, top + 237, 199, 42), "Multiplayer", false))
                    ShowOnline();
                if (MenuButton(new Rect(x, top + 293, cardWidth - 32, 42), L("Main menu", "Menu principal"), false))
                    home();
            }
            else
            {
                if (MenuButton(new Rect(x, top + 12, 150, 35), L("Back to pause", "Voltar à pausa"), false))
                    ShowDeploy();
                else
                    DrawPage(x, top + 50, frame.Height, jobs, selectJob, online, ownsWeapon, sessionActive, continueRun);
            }

            GUI.matrix = original;
        }

        private void DrawPage(float x, float top, float height, CostaJobs jobs, Action<int> selectJob,
            CostaOnlinePanel online, Func<int, bool> ownsWeapon, bool sessionActive, Action continueRun)
        {
            switch (page)
            {
                case ScreenPage.Loadout:
                    Text(new Rect(x, top, 465, 63), L("Loadout", "Equipamento"), headline, Paper);
                    int owned = 0;
                    for (int i = 0; i < HarborArsenal.WeaponNames.Length; i++)
                    {
                        if (!ownsWeapon(i))
                            continue;
                        if (MenuButton(new Rect(x, top + 77 + owned * 43, 248, 38), Weapon(i), i == SelectedWeapon))
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
                    online.Draw(x, top, height, continueRun);
                    break;
            }
        }

        private void DrawNavigation(MenuFrame frame, bool sessionActive, Action quit)
        {
            float end = frame.Width - 24;
            if (frame.Compact)
            {
                Fill(new Rect(0, 0, frame.Width, 78), Rail);
                GUI.DrawTexture(new Rect(0, 0, frame.Width, 3), accentBar);
                Fill(new Rect(24, 38, end - 24, 1), Line);
                Text(new Rect(24, 7, 290, 29), "COSTA LIVRE 2", subhead, Paper);
                if (MenuButton(new Rect(end - 82, 5, 82, 30), L("Quit", "Sair"), false))
                    quit();

                float gap = 6;
                int count = sessionActive ? 4 : 5;
                float width = Mathf.Min(152, (end - 24 - (count - 1) * gap) / count);
                float x = 24;
                Tab(new Rect(x, 43, width, 34), ScreenPage.Deploy, L("Play", "Jogar"));
                x += width + gap;
                Tab(new Rect(x, 43, width, 34), ScreenPage.Loadout, L("Loadout", "Equipamento"));
                x += width + gap;
                Tab(new Rect(x, 43, width, 34), ScreenPage.Options, L("Options", "Opções"));
                x += width + gap;
                if (!sessionActive)
                {
                    Tab(new Rect(x, 43, width, 34), ScreenPage.Jobs, L("Jobs", "Missões"));
                    x += width + gap;
                }
                Tab(new Rect(x, 43, width, 34), ScreenPage.Online, "Multiplayer");
                return;
            }

            Fill(new Rect(24, 24, 194, frame.Height - 48), Rail);
            GUI.DrawTexture(new Rect(24, 24, 194, 3), accentBar);
            Fill(new Rect(217, 24, 1, frame.Height - 48), Line);
            Text(new Rect(40, 42, 166, 30), "COSTA LIVRE 2", subhead, Paper);
            Text(new Rect(40, 68, 166, 20), "SANTA LUZ · BR", label, Turquoise);
            float rail = Mathf.Max(118, frame.Height * 0.22f);
            Tab(new Rect(38, rail, 162, 43), ScreenPage.Deploy, L("Play", "Jogar"));
            Tab(new Rect(38, rail + 50, 162, 43), ScreenPage.Loadout, L("Loadout", "Equipamento"));
            Tab(new Rect(38, rail + 100, 162, 43), ScreenPage.Options, L("Options", "Opções"));
            if (!sessionActive)
                Tab(new Rect(38, rail + 150, 162, 43), ScreenPage.Jobs, L("Jobs", "Missões"));
            Tab(new Rect(38, rail + (sessionActive ? 150 : 200), 162, 43), ScreenPage.Online, "Multiplayer");
            if (MenuButton(new Rect(38, frame.Height - 84, 162, 42), L("Quit", "Sair"), false))
                quit();
        }

        private void DrawDeploy(float x, float y, bool resume, bool sessionActive,
            Action deploy, Action continueRun)
        {
            GUI.DrawTexture(new Rect(x, y - 8, 92, 4), accentBar);
            string tag = sessionActive
                ? L("ONLINE SESSION · SANTA LUZ", "SESSÃO ONLINE · SANTA LUZ")
                : L("BRAZILIAN COAST · OPEN WORLD", "LITORAL BRASILEIRO · MUNDO ABERTO");
            Text(new Rect(x, y + 4, 460, 22), tag, label, Turquoise);

            string title = sessionActive ? "Multiplayer" : "Santa Luz";
            Text(new Rect(x, y + 29, 520, 88), title, hero, new Color(0f, 0f, 0f, 0.55f));
            Text(new Rect(x - 2, y + 26, 520, 88), title, hero, Paper);

            string action = resume ? L("Resume", "Continuar") : L("Enter the city", "Entrar na cidade");
            Rect enter = new(x, y + 124, 280, 58);
            if (MenuButton(enter, action, true))
                (resume ? continueRun : deploy)();
            Text(new Rect(enter.xMax - 32, enter.y, 28, enter.height), "›", subhead, Rail);
            if (resume && !sessionActive &&
                MenuButton(new Rect(x, enter.yMax + 10, 280, 42), L("Restart", "Recomeçar"), false))
                deploy();

            float hintY = resume && !sessionActive ? enter.yMax + 62 : enter.yMax + 16;
            string hint = CostaTouch.Available
                ? L("Tap to explore Santa Luz", "Toque para explorar Santa Luz")
                : L("ENTER  Play   ·   ESC  Pause   ·   M  Map", "ENTER  Jogar   ·   ESC  Pausar   ·   M  Mapa");
            Text(new Rect(x, hintY, 440, 22), hint, label, Grey);
        }

        private void DrawJobs(float x, float top, CostaJobs jobs, Action<int> select)
        {
            Text(new Rect(x, top - 6, 415, 66), L("Jobs board", "Missões"), headline, Paper);
            Text(new Rect(x, top + 52, 355, 24), Money(jobs.Credits), label, Copper);
            float y = top + 78;
            if (MenuButton(new Rect(x, y, 349, 30), L("Harbor cargo", "Carga do porto"), false))
                select(-2);
            int first = jobPage * JobsPerPage;
            int last = Mathf.Min(first + JobsPerPage, CostaJobs.List.Length);
            for (int i = first; i < last; i++)
            {
                var job = CostaJobs.List[i];
                float row = y + (i - first + 1) * 35;
                string status = jobs.Completed(i) ? L("Done", "Concluída") : Money(job.Reward);
                if (MenuButton(new Rect(x, row, 274, 30), job.Title, jobs.Active == i))
                    select(i);
                Text(new Rect(x + 282, row, 109, 30), status, label, Grey);
            }

            if (MenuButton(new Rect(x, y + 7 * 35, 274, 29), L("Explore freely", "Explorar livremente"), false))
                select(-1);

            float nav = y + 8 * 35;
            int pages = (CostaJobs.List.Length + JobsPerPage - 1) / JobsPerPage;
            if (jobPage > 0 && MenuButton(new Rect(x, nav, 105, 29), L("Previous", "Anterior"), false))
                jobPage--;
            Text(new Rect(x + 122, nav, 42, 29), $"{jobPage + 1} / {pages}", label, Grey);
            if (jobPage < pages - 1 && MenuButton(new Rect(x + 169, nav, 105, 29), L("Next", "Próxima"), false))
                jobPage++;
        }

        private void DrawOptions(float x, float top)
        {
            Text(new Rect(x, top - 5, 380, 62), L("Options", "Opções"), headline, Paper);
            float tab = top + 63;
            if (MenuButton(new Rect(x, tab, 110, 39), L("Controls", "Controles"), settings == SettingsPage.Controls))
                settings = SettingsPage.Controls;
            if (MenuButton(new Rect(x + 115, tab, 105, 39), L("Graphics", "Gráficos"), settings == SettingsPage.Graphics))
                settings = SettingsPage.Graphics;
            if (MenuButton(new Rect(x + 225, tab, 142, 39), L("Display & sound", "Tela e som"), settings == SettingsPage.Display))
                settings = SettingsPage.Display;
            if (MenuButton(new Rect(x + 372, tab, 98, 39), L("Language", "Idioma"), settings == SettingsPage.Language))
                settings = SettingsPage.Language;

            if (CostaTouch.Available && MenuButton(new Rect(x + 475, tab, 113, 39), L("Touch layout", "Toque"), false))
                editTouch = true;

            float y = tab + 51;
            switch (settings)
            {
                case SettingsPage.Controls:
                    DrawControls(x, y);
                    break;
                case SettingsPage.Graphics:
                    DrawGraphics(x, y);
                    break;
                case SettingsPage.Display:
                    DrawDisplay(x, y);
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
            Text(new Rect(x, y + 248, 380, 22), L("M Map   ·   I Backpack   ·   V Swap shoulder",
                "M Mapa   ·   I Mochila   ·   V Trocar ombro"), label, Grey);
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
                "Menu", L("Jobs", "Missões"), L("Swap shoulder", "Trocar ombro") };
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
            Text(new Rect(x, y, 588, 23), graphics.IsCustom
                ? L("Preset   /   Custom settings", "Predefinição   /   Personalizada")
                : L("Preset", "Predefinição"), label, Grey);
            if (MenuButton(new Rect(x, y + 25, 150, 34), L("Super Low (Potato)", "Super baixa (Batata)"),
                    graphics.Preset == 0 && !graphics.IsCustom && !graphics.Extreme))
                SetGraphicsPreset(0);
            for (int i = 1; i <= HarborGraphics.MaxPreset; i++)
            {
                string preset = i == 4 ? "Ultra" : QualityLabel(i);
                if (MenuButton(new Rect(x + 156 + (i - 1) * 81, y + 25, 76, 34), preset,
                        graphics.Preset == i && !graphics.IsCustom))
                    SetGraphicsPreset(i);
            }

            float right = x + 302;
            Fill(new Rect(x, y + 68, 286, 183), Rail);
            Fill(new Rect(right, y + 68, 286, 183), Rail);
            float inset = x + 12;
            float options = right + 12;

            Text(new Rect(inset, y + 78, 262, 23),
                L($"3D resolution   {graphics.ResolutionPercent}%", $"Resolução 3D   {graphics.ResolutionPercent}%"), label, Paper);
            graphics.SetScale(GraphicsSlider(new Rect(inset, y + 104, 262, 31), graphics.ResolutionPercent, 0, 100));
            int percent = graphics.ActualPercent;
            string scaleNote;
            if (graphics.AdaptiveResolution && graphics.AdaptiveSupported)
                scaleNote = L($"Rendering at {percent}%", $"Renderizando a {percent}%");
            else if (percent != graphics.ResolutionPercent)
                scaleNote = L($"Engine minimum   {percent}%", $"Mínimo da Unity   {percent}%");
            else if (percent == 100)
                scaleNote = graphics.StpEnabled && graphics.StpSupported ?
                    L("Native resolution + STP", "Resolução nativa + STP") : L("Native resolution", "Resolução nativa");
            else
                scaleNote = $"{Screen.width * percent / 100} × {Screen.height * percent / 100}";
            Text(new Rect(inset, y + 136, 262, 22), scaleNote, label, Grey);
            Fill(new Rect(inset, y + 164, 262, 1), Line);
            Text(new Rect(inset, y + 174, 262, 23), L($"View distance   {graphics.Distance} m",
                $"Distância de visão   {graphics.Distance} m"), label, Paper);
            graphics.SetDistance(GraphicsSlider(new Rect(inset, y + 202, 262, 32), graphics.Distance,
                HarborGraphics.MinDistance, HarborGraphics.MaxDistance));

            Text(new Rect(options, y + 78, 262, 23), L("Shadows", "Sombras"), label, Paper);
            for (int i = 0; i <= HarborGraphics.MaxPreset; i++)
            {
                string name = i == 0 ? L("Off", "Sem") : i == 4 ? "Ultra" : QualityLabel(i);
                if (MenuButton(new Rect(options + i * 53, y + 105, 50, 32), name, graphics.ShadowQuality == i, true))
                    graphics.SetShadows(i);
            }

            Text(new Rect(options, y + 144, 262, 22), L("Textures", "Texturas"), label, Paper);
            for (int i = 0; i < 4; i++)
            {
                if (MenuButton(new Rect(options + i * 66, y + 169, 63, 32), QualityLabel(i),
                        graphics.TextureQuality == i, true))
                    graphics.SetTextures(i);
            }

            bool compatible = graphics.StpSupported;
            string stp = !compatible ? L("Unavailable", "Indisponível") :
                graphics.StpEnabled ? L("On", "Ligado") : L("Off", "Desligado");
            Text(new Rect(options, y + 211, 145, 32), "Unity STP", label, Paper);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && compatible;
            if (MenuButton(new Rect(options + 147, y + 211, 115, 32), stp, graphics.StpEnabled && compatible, true))
                graphics.ToggleStp();
            GUI.enabled = enabled;
            string adaptive = !graphics.AdaptiveSupported ? L("Unavailable", "Indisponível") :
                graphics.AdaptiveResolution ? L("On", "Ligada") : L("Off", "Desligada");
            Text(new Rect(inset, y + 252, 147, 32), L("Adaptive resolution", "Resolução adaptativa"), label, Paper);
            GUI.enabled = enabled && graphics.AdaptiveSupported;
            if (MenuButton(new Rect(inset + 147, y + 252, 115, 32), adaptive, graphics.AdaptiveResolution, true))
                graphics.ToggleAdaptiveResolution();
            GUI.enabled = enabled;
            if (MenuButton(new Rect(options,y+252,157,32),L("EXTREME POTATO","BATATA EXTREMO"),graphics.Extreme,true))
            {
                graphics.SetExtreme();
                frameCap=60; vsync=false;
                ApplyFrameSettings();
            }
            if (MenuButton(new Rect(inset,y+289,124,29),L("Lock scale","Travar escala"),graphics.ScaleLocked,true))
                graphics.ToggleScaleLock();
            if (MenuButton(new Rect(inset+135,y+289,135,29),L("Lock world","Travar mundo"),graphics.WorldLocked,true))
                graphics.ToggleWorldLock();
            Text(new Rect(options,y+291,271,26),$"{graphics.MeanFrame:0.0} ms · P95 {graphics.Frame95:0.0} · 1% {graphics.LowOne:0} FPS",label,Grey);
            Text(new Rect(options,y+312,275,21),$"{graphics.AdaptiveStatus} · {graphics.AllocatedMiB:0} MiB · GC {graphics.ManagedCollections}",label,Grey);
            string cpu = graphics.CpuMs > 0 ? $"{graphics.CpuMs:0.0} ms" : "—";
            string gpu = graphics.GpuMs > 0 ? $"{graphics.GpuMs:0.0} ms" : "—";
            Text(new Rect(inset,y+319,260,22),$"CPU {cpu}  ·  GPU {gpu}",label,Grey);
        }

        private void SetGraphicsPreset(int preset)
        {
            graphics.SetPreset(preset);
            ApplyFrameSettings();
        }

        private static string QualityLabel(int quality) => quality switch
        {
            0 => L("Min", "Mínima"),
            1 => L("Low", "Baixa"),
            2 => L("Medium", "Média"),
            _ => L("High", "Alta")
        };

        private static float GraphicsSlider(Rect bounds, float value, float min, float max)
        {
            Color old = GUI.color;
            GUI.color = Copper;
            float result = CostaUi.Slider(bounds, value, min, max);
            GUI.color = old;
            return result;
        }

        private void DrawDisplay(float x, float y)
        {
            if (!Application.isMobilePlatform)
            {
                Text(new Rect(x, y, 115, 32), L("Display", "Tela"), label, Paper);
                if (MenuButton(new Rect(x + 113, y, 115, 34), L("Windowed", "Janela"), !Screen.fullScreen))
                    SetFullscreen(false);
                if (MenuButton(new Rect(x + 234, y, 126, 34), L("Fullscreen", "Tela cheia"), Screen.fullScreen))
                    SetFullscreen(true);
            }

            float frame = y + (Application.isMobilePlatform ? 0 : 48);
            Text(new Rect(x, frame, 108, 34), L("Frame limit", "Limite de FPS"), label, Paper);
            if (MenuButton(new Rect(x + 113, frame, 56, 34), "30", frameCap == 30, true))
            {
                frameCap = 30;
                ApplyFrameSettings();
            }
            if (MenuButton(new Rect(x + 175, frame, 56, 34), "60", frameCap == 60, true))
            {
                frameCap = 60;
                ApplyFrameSettings();
            }
            if (MenuButton(new Rect(x+237,frame,56,34),"90",frameCap==90,true))
            { frameCap=90; ApplyFrameSettings(); }
            if (MenuButton(new Rect(x+299,frame,56,34),"120",frameCap==120,true))
            { frameCap=120; ApplyFrameSettings(); }
            if (!Application.isMobilePlatform && MenuButton(new Rect(x + 361, frame, 86, 34), "VSync", vsync))
            {
                vsync = !vsync;
                ApplyFrameSettings();
            }

            Text(new Rect(x, frame + 53, 280, 24), L($"Master volume   {Volume:P0}", $"Volume geral   {Volume:P0}"), label, Paper);
            float volume = GraphicsSlider(new Rect(x, frame + 82, 229, 27), Volume, 0f, 1f);
            if (!Mathf.Approximately(Volume, volume))
            {
                Volume = volume;
                AudioListener.volume = Volume;
                PlayerPrefs.SetFloat("HarborVolume", volume);
            }

            Text(new Rect(x + 245, frame + 53, 170, 24),
                L($"Camera shake {CameraShake:P0}", $"Tremor da câmera {CameraShake:P0}"), label, Paper);
            float shake = GraphicsSlider(new Rect(x + 245, frame + 82, 147, 27), CameraShake, 0f, 1f);
            if (!Mathf.Approximately(CameraShake, shake))
            {
                CameraShake = shake;
                PlayerPrefs.SetFloat("CostaCameraShake", shake);
            }

            Text(new Rect(x, frame + 113, 215, 20), L($"Music  {MusicVolume:P0}", $"Música  {MusicVolume:P0}"), label, Paper);
            float music = GraphicsSlider(new Rect(x, frame + 135, 229, 23), MusicVolume, 0, 1);
            if (!Mathf.Approximately(MusicVolume, music))
            {
                MusicVolume = music;
                PlayerPrefs.SetFloat("CostaMusicVolume", music);
            }

            float hudY = frame + 168;
            Text(new Rect(x, hudY, 108, 32), "HUD", label, Paper);
            SetHud(new Rect(x + 113, hudY, 103, 32), L("Full", "Completo"), HarborHud.Mode.Full);
            SetHud(new Rect(x + 223, hudY, 112, 32), L("Contextual", "Contextual"), HarborHud.Mode.Contextual);
            SetHud(new Rect(x + 342, hudY, 105, 32), L("Minimal", "Mínimo"), HarborHud.Mode.Minimal);

            string mirrors = MirrorQuality switch
            {
                1 => L("Off", "Desligados"), 2 => "128 px", 3 => "256 px", 4 => "512 px", _ => L("Auto", "Automático")
            };
            if (MenuButton(new Rect(x + 367, hudY + 40, 128, 32), L("Mirrors  ", "Espelhos  ") + mirrors, false))
            {
                MirrorQuality = (MirrorQuality + 1) % 5;
                PlayerPrefs.SetInt("CostaMirrorQuality", MirrorQuality);
            }

            float titleY = hudY + 40;
            Text(new Rect(x, titleY, 110, 32), L("Title scene", "Fundo do menu"), label, Paper);
            if (MenuButton(new Rect(x + 113, titleY, 120, 32), L("Live city", "Cidade ao vivo"), LiveTitle))
            {
                titleBackground = 1;
                PlayerPrefs.SetInt("CostaCityTitle", titleBackground);
            }
            if (MenuButton(new Rect(x + 240, titleY, 118, 32), L("Lightweight", "Leve"), !LiveTitle))
            {
                titleBackground = 0;
                PlayerPrefs.SetInt("CostaCityTitle", titleBackground);
            }
        }

        private void SetHud(Rect rect, string label, HarborHud.Mode mode)
        {
            if (MenuButton(rect, label, HudMode == mode))
            {
                HudMode = mode;
                PlayerPrefs.SetInt("CostaHudMode", (int)mode);
            }
        }

        private void ApplyFrameSettings()
        {
            QualitySettings.vSyncCount = vsync ? 1 : 0;
            Application.targetFrameRate = frameCap;
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
            graphics.Release();
            PlayerPrefs.Save();
            UnityEngine.Object.Destroy(gradient);
            UnityEngine.Object.Destroy(accentBar);
        }

        private void Tab(Rect bounds, ScreenPage id, string name)
        {
            if (MenuButton(bounds, name, page == id))
                OpenPage(id);
        }

        private void OpenPage(ScreenPage target)
        {
            if (page == target)
                return;
            page = target;
            pageChanged = Time.unscaledTime;
        }

        private float PageOffset() => 7 * (1 - Mathf.SmoothStep(0, 1, (Time.unscaledTime - pageChanged) / .17f));

        private bool MenuButton(Rect bounds, string name, bool selected, bool centered = false)
        {
            bool hover = bounds.Contains(Event.current.mousePosition) || CostaUi.Hovering(bounds);
            if (selected)
            {
                GUI.DrawTexture(bounds, accentBar);
                if (hover)
                    Fill(bounds, new Color(1f, 1f, 1f, 0.10f));
            }
            else
            {
                Fill(bounds, hover ? Hover : Carbon);
                Fill(new Rect(bounds.x, bounds.yMax - 1, bounds.width, 1), hover ? Turquoise : Line);
                if (hover)
                    Fill(new Rect(bounds.x, bounds.y, 3, bounds.height), Turquoise);
            }
            Color before = GUI.contentColor;
            GUI.contentColor = selected ? new Color(0.04f, 0.06f, 0.09f) : Paper;
            GUIStyle style = centered ? centeredButton : button;
            int fontSize = style.fontSize;
            float available = bounds.width - 10;
            MeasureContent.text = name;
            float required = style.CalcSize(MeasureContent).x;
            if (required > available)
                style.fontSize = Mathf.Max(11, Mathf.FloorToInt(fontSize * available / required));
            bool pressed = CostaUi.Button(bounds, name, style);
            style.fontSize = fontSize;
            GUI.contentColor = before;
            return pressed;
        }

        private void CreateStyles()
        {
            hero = Style(78, FontStyle.Bold);
            headline = Style(52, FontStyle.Bold);
            pauseHeadline = Style(43, FontStyle.Bold);
            subhead = Style(18, FontStyle.Bold);
            label = Style(15, FontStyle.Bold);
            button = Style(16, FontStyle.Bold);
            button.padding = new RectOffset(12, 6, 0, 0);
            centeredButton = new GUIStyle(button)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(3, 3, 0, 0)
            };
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
