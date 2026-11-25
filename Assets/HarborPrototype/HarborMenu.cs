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
        private int quality = PlayerPrefs.GetInt("CostaQuality", 1);
        private int frameCap = PlayerPrefs.GetInt("CostaFrameCap", 60);
        private bool vsync = PlayerPrefs.GetInt("CostaVSync", 0) != 0;
        private readonly CostaBindings bindings;

        internal bool CapturingKey => bindings.Waiting;
        internal void ReadBinding() => bindings.Tick();
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

        internal HarborMenu(CostaBindings bindings)
        {
            this.bindings = bindings;
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
            CostaOnlinePanel online, HarborArsenal arsenal, bool sessionActive)
        {
            if (headline == null)
                CreateStyles();

            Matrix4x4 original = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 924f, Screen.height / 544f, 1.65f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.DrawTexture(new Rect(0, 0, width * 0.79f, height), gradient);
            Fill(new Rect(0, 0, width, 4), Copper);
            Fill(new Rect(0, height - 40, width, 40), Carbon);
            Text(new Rect(28, height - 40, 390, 40), "COSTA LIVRE 2   /   PORTO DE SANTA LUZ", label, Grey);
            Text(new Rect(width - 218, height - 40, 195, 40), L("ENTER / START   PLAY", "ENTER / START   JOGAR"), label, Grey);
            Text(new Rect(29, 21, 260, 26), "COSTA LIVRE 2", subhead, Paper);

            float x = 173;
            float top = Mathf.Max(99, height * 0.18f);
            float rail = top + 33;
            Tab(new Rect(28, rail, 124, 37), ScreenPage.Deploy, L("Explore", "Explorar"));
            Tab(new Rect(28, rail + 43, 124, 37), ScreenPage.Loadout, L("Loadout", "Equipamento"));
            Tab(new Rect(28, rail + 86, 124, 37), ScreenPage.Options, L("Options", "Opções"));
            if (!sessionActive)
                Tab(new Rect(28, rail + 129, 124, 37), ScreenPage.Jobs, L("Jobs", "Missões"));
            Tab(new Rect(28, rail + 172, 124, 37), ScreenPage.Online, "Multiplayer");
            if (MenuButton(new Rect(28, height - 91, 124, 34), L("Quit", "Sair"), false))
                quit();

            switch (page)
            {
                case ScreenPage.Deploy:
                    Text(new Rect(x, top, 394, 21), L("PORTO DE SANTA LUZ  /  SINGLE PLAYER", "PORTO DE SANTA LUZ  /  UM JOGADOR"), label, Copper);
                    Text(new Rect(x, top + 19, 417, 75), L("Santa Luz", "Santa Luz"), headline, Paper);
                    Text(new Rect(x, top + 94, 365, 57), L("Explore the waterfront and the city. Pick a contract from the jobs board when you want one.", "Explore a orla e a cidade. Escolha um contrato no quadro de missões quando quiser."), subhead, Paper);
                    if (MenuButton(new Rect(x, top + 176, 202, 49), resume ? L("Resume", "Continuar") : L("Enter the city", "Entrar na cidade"), true))
                        (resume ? continueRun : deploy)();
                    if (resume && !sessionActive && MenuButton(new Rect(x, top + 236, 202, 36), L("Restart", "Recomeçar"), false))
                        deploy();
                    break;
                case ScreenPage.Loadout:
                    Text(new Rect(x, top, 300, 63), L("Loadout", "Equipamento"), headline, Paper);
                    Text(new Rect(x, top + 62, 370, 29), L("Buy weapons at the Santa Luz Gun Shop.", "Compre suas armas na Armaria Santa Luz."), label, Grey);
                    for (int i = 0; i < HarborArsenal.WeaponNames.Length; i++)
                    {
                        if (arsenal.Owned(i))
                        {
                            if (MenuButton(new Rect(x, top + 102 + i * 34, 222, 30), Weapon(i), i == SelectedWeapon))
                            {
                                SelectedWeapon = i;
                                SelectionVersion++;
                            }
                        }
                        else
                        {
                            Text(new Rect(x + 12, top + 102 + i * 34, 258, 30),
                                Weapon(i) + L("  ·  Locked", "  ·  Bloqueada"), label, Grey);
                        }
                    }
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
            Text(new Rect(x, y, 396, 23), L("WASD Move  ·  M Map  ·  TAB Jobs  ·  Mouse Aim / Fire",
                "WASD Mover  ·  M Mapa  ·  TAB Missões"), label, Paper);
            string[] names = { L("Run", "Correr"), L("Jump / brake", "Pular / frear"), L("Roll", "Rolar"),
                L("Interact / enter car", "Interagir / entrar no carro"), L("Reload", "Recarregar") };
            for (int i = 0; i < names.Length; i++)
            {
                float row = y + 30 + i * 28;
                Text(new Rect(x, row, 215, 25), names[i], label, Grey);
                if (MenuButton(new Rect(x + 211, row, 128, 24), bindings.Name(i), false))
                    bindings.Begin(i);
            }

            if (bindings.Waiting)
                Text(new Rect(x, y + 175, 405, 22), bindings.Error.Length > 0 ? bindings.Error : L("Press a key or Esc to cancel.", "Pressione uma tecla ou Esc para cancelar."), label, Copper);

            if (MenuButton(new Rect(x + 213, y + 204, 127, 26), L("Reset keys", "Restaurar"), false))
                bindings.Reset();
            float sensitivity = GUI.HorizontalSlider(new Rect(x, y + 217, 188, 18), Sensitivity, 0.35f, 2.1f);
            Text(new Rect(x, y + 194, 206, 22), L($"Mouse   {sensitivity:0.00}", $"Sensibilidade   {sensitivity:0.00}"), label, Paper);
            if (!Mathf.Approximately(Sensitivity, sensitivity))
            {
                Sensitivity = sensitivity;
                PlayerPrefs.SetFloat("HarborSensitivity", sensitivity);
            }
        }

        private void DrawGraphics(float x, float y)
        {
            Text(new Rect(x, y, 115, 24), L("Quality", "Qualidade"), label, Paper);
            if (MenuButton(new Rect(x + 105, y, 108, 26), L("Performance", "Desempenho"), quality == 0))
            {
                quality = 0;
                ApplyGraphics();
            }
            if (MenuButton(new Rect(x + 219, y, 118, 26), L("High", "Alta"), quality == 1))
            {
                quality = 1;
                ApplyGraphics();
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
            float volume = GUI.HorizontalSlider(new Rect(x, y + 149, 229, 20), Volume, 0f, 1f);
            if (!Mathf.Approximately(Volume, volume))
            {
                Volume = volume;
                AudioListener.volume = Volume;
                PlayerPrefs.SetFloat("HarborVolume", volume);
            }
        }

        private void ApplyGraphics()
        {
            QualitySettings.SetQualityLevel(Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1), true);
            QualitySettings.vSyncCount = vsync ? 1 : 0;
            Application.targetFrameRate = frameCap;
            PlayerPrefs.SetInt("CostaQuality", quality);
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
            bool pressed = GUI.Button(bounds, name, button);
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
