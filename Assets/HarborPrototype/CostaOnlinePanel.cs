using System;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaOnlinePanel
    {
        private CostaSession session;
        private readonly Action<string, string, int, bool, string> connect;
        private string name = PlayerPrefs.GetString("CostaMultiplayerName", "Jogador");
        private string ip = "127.0.0.1";
        private string port = "7777";
        private bool internet = true;
        private const string DefaultRelay = "https://costalivre2-mibel10-relay.onrender.com";
        private string relay = DefaultRelay;
        private string invite = "";
        private float copiedUntil;
        private static readonly int[] Powers = { 1, 3, 9, 27, 81 };
        private static readonly Color Text = new(0.90f, 0.91f, 0.84f);
        private static readonly Color Muted = new(0.67f, 0.74f, 0.69f);
        private static readonly Color Gold = new(0.91f, 0.65f, 0.40f);
        private static readonly Color Board = new(0.052f, 0.080f, 0.085f, 0.94f);
        private GUIStyle labels;
        private GUIStyle title;
        private GUIStyle buttons;
        private TouchScreenKeyboard keyboard;
        private int editing;

        internal int Outfit { get; private set; } = Mathf.Clamp(PlayerPrefs.GetInt("CostaOutfit", 0), 0, 242);
        internal CostaOnlinePanel(Action<string, string, int, bool, string> connect) => this.connect = connect;

        internal void AttachSession(CostaSession readySession) => session = readySession;

        internal void Draw(float x, float y, float height, Action play)
        {
            ReadKeyboard();
            if (labels == null)
            {
                labels = Style(14, TextAnchor.MiddleLeft);
                title = Style(34, TextAnchor.MiddleLeft);
                buttons = Style(14, TextAnchor.MiddleCenter);
            }
            Fill(new Rect(x - 9, y, 415, Mathf.Min(height - y - 16, 377)), Board);
            Label(new Rect(x + 8, y + 2, 395, 45), "Multiplayer", title, Text);
            float row = y + 54;
            if (session?.Online != true)
            {
                bool connecting = session?.Connecting == true;
                Label(new Rect(x + 8, row, 350, 22), L("PLAYER NAME", "NOME DO JOGADOR"), labels, Muted);
                name = Input(new Rect(x + 8, row + 25, 362, 34), name, 20, 1, TouchScreenKeyboardType.Default);
                if (Button(new Rect(x + 8, row + 65, 174, 32), "Internet", !connecting))
                    internet = true;
                if (Button(new Rect(x + 195, row + 65, 175, 32), "LAN", !connecting))
                    internet = false;
                if (internet)
                {
                    Label(new Rect(x + 8, row + 112, 350, 20), L("ROOM CODE OR INVITE", "CÓDIGO OU CONVITE DA SALA"), labels, Muted);
                    invite = Input(new Rect(x + 8, row + 136, 362, 36), invite, 210, 5, TouchScreenKeyboardType.URL);
                }
                else
                {
                    Label(new Rect(x + 8, row + 104, 230, 20), L("HOST IPV4", "IPV4 DO ANFITRIÃO"), labels, Muted);
                    Label(new Rect(x + 286, row + 104, 76, 20), L("PORT", "PORTA"), labels, Muted);
                    ip = Input(new Rect(x + 8, row + 126, 264, 34), ip, 15, 2, TouchScreenKeyboardType.NumbersAndPunctuation);
                    port = Input(new Rect(x + 286, row + 126, 84, 34), port, 5, 3, TouchScreenKeyboardType.NumberPad);
                }
                float actions = row + (internet ? 180 : 172);
                if (Button(new Rect(x + 8, actions, 174, 36), internet ? L("Create room", "Criar sala") : L("Host LAN", "Hospedar LAN"), !connecting))
                    Start(true);
                if (Button(new Rect(x + 195, actions, 175, 36), internet ? L("Join room", "Entrar na sala") : L("Join by IP", "Entrar por IP"), !connecting))
                    Start(false);
                if (connecting && Button(new Rect(x + 250, actions + 40, 120, 29), L("Cancel", "Cancelar"), true))
                    session.Dispose();
                Label(new Rect(x + 8, actions + 39, connecting ? 236 : 372, 30), session?.Status ?? "", labels, Gold);
                return;
            }

            bool police = session.Local.police;
            string role = police ? L("Police", "Policial") : L("Criminal", "Criminoso");
            Label(new Rect(x + 8, row - 4, 369, 33),
                $"{role}   ·   {L("Police", "Policiais")} {session.PoliceCount}/{session.PoliceLimit}   ·   {session.Players.Length} {L("players", "jogadores")}",
                labels, Gold);
            if (Button(new Rect(x + 8, row + 31, 213, 31),
                police ? L("Become criminal", "Virar criminoso") : L("Join police", "Virar policial"),
                police || session.PoliceCount < session.PoliceLimit))
                session.Request("police");
            if (Button(new Rect(x + 232, row + 31, 138, 31), L("Leave", "Sair da sessão"), true))
            {
                session.Dispose();
                return;
            }
            if (session.RoomCode.Length > 0)
            {
                Label(new Rect(x + 8, row + 70, 210, 25), L("ROOM ", "SALA ") + session.RoomCode, labels, Gold);
                if (Button(new Rect(x + 222, row + 69, 148, 28),
                    Time.unscaledTime < copiedUntil ? L("Copied", "Copiado") : L("Copy invite", "Copiar convite"), true))
                {
                    GUIUtility.systemCopyBuffer = session.RelayAddress + "/join/" + session.RoomCode;
                    copiedUntil = Time.unscaledTime + 2f;
                }
            }
            else
                Label(new Rect(x + 8, row + 70, 369, 25), L("WARDROBE", "VESTUÁRIO"), labels, Muted);
            for (int i = 0; i < Powers.Length; i++)
            {
                int index = Outfit / Powers[i] % 3;
                float yy = row + 101 + i * 32;
                Label(new Rect(x + 8, yy, 244, 26), OutfitLabel(i, index), labels, Text);
                if (Button(new Rect(x + 263, yy, 49, 30), "‹", !police))
                    Change(i, index, -1);
                if (Button(new Rect(x + 322, yy, 48, 30), "›", !police))
                    Change(i, index, 1);
            }
            if (Button(new Rect(x + 8, row + 270, 174, 40), L("Play", "Jogar"), true))
                play();
            Label(new Rect(x + 190, row + 266, 186, 38), session.CurrentNotice, labels, Gold);
        }

        private string Input(Rect rect, string value, int limit, int field, TouchScreenKeyboardType type)
        {
            if (!TouchScreenKeyboard.isSupported)
                return GUI.TextField(rect, value, limit);
            if (CostaUi.Button(rect, value, GUI.skin.textField))
            {
                keyboard = TouchScreenKeyboard.Open(value, type, false, false, false, false, "", limit);
                editing = field;
            }
            return value;
        }

        private void ReadKeyboard()
        {
            if (keyboard == null || keyboard.status == TouchScreenKeyboard.Status.Visible)
                return;
            if (keyboard.status == TouchScreenKeyboard.Status.Done)
            {
                switch (editing)
                {
                    case 1: name = keyboard.text; break;
                    case 2: ip = keyboard.text; break;
                    case 3: port = keyboard.text; break;
                    case 4: relay = keyboard.text; break;
                    case 5: invite = keyboard.text; break;
                }
            }
            keyboard = null;
        }

        private void Start(bool host)
        {
            if (!int.TryParse(port, out int number))
                number = 0;
            string address = internet ? invite.Trim() : ip;
            if (internet && !host && Uri.TryCreate(address, UriKind.Absolute, out var link)
                && link.Query == "" && link.Fragment == "" && link.UserInfo == ""
                && link.AbsolutePath.StartsWith("/join/", StringComparison.Ordinal))
            {
                relay = link.GetLeftPart(UriPartial.Authority);
                address = link.AbsolutePath.Substring(6);
            }
            PlayerPrefs.SetString("CostaMultiplayerName", name);
            PlayerPrefs.Save();
            connect(name, address, number, host, internet ? DefaultRelay : null);
        }

        private void Change(int category, int option, int offset)
        {
            int next = (option + offset + 3) % 3;
            Outfit += (next - option) * Powers[category];
            PlayerPrefs.SetInt("CostaOutfit", Outfit);
            PlayerPrefs.Save();
            session.Request("outfit", Outfit);
        }

        private static string OutfitLabel(int category, int option)
        {
            switch (category)
            {
                case 0: return L("Top   ", "Roupa   ") + (option == 0 ? L("Gray", "Cinza") : option == 1 ? L("Olive", "Oliva") : L("Wine", "Vinho"));
                case 1: return L("Pants   ", "Calça   ") + (option == 0 ? "Jeans" : option == 1 ? L("Dark", "Escura") : L("Sand", "Areia"));
                case 2: return L("Sneakers   ", "Tênis   ") + (option == 0 ? L("White", "Branco") : option == 1 ? L("Black", "Preto") : L("Tan", "Caramelo"));
                case 3: return L("Hat   ", "Chapéu   ") + (option == 0 ? L("None", "Nenhum") : option == 1 ? L("Cap", "Boné") : L("Beanie", "Gorro"));
                default: return L("Accessory   ", "Acessório   ") + (option == 0 ? L("None", "Nenhum") : option == 1 ? L("Glasses", "Óculos") : L("Bag", "Bolsa"));
            }
        }

        private static GUIStyle Style(int size, TextAnchor anchor) => new(GUI.skin.label)
        {
            fontSize = size, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip,
            alignment = anchor, wordWrap = false, padding = new RectOffset()
        };

        private bool Button(Rect area, string value, bool active)
        {
            Fill(area, active ? new Color(0.23f, 0.33f, 0.31f) : new Color(0.10f, 0.16f, 0.17f));
            bool old = GUI.enabled;
            GUI.enabled = old && active;
            buttons.normal.textColor = active ? Text : Muted;
            bool result = CostaUi.Button(area, value, buttons);
            GUI.enabled = old;
            return result;
        }

        private static void Label(Rect area, string value, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(area, value, style);
        }

        private static void Fill(Rect area, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
