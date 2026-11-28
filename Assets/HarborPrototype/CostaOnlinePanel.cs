using System;
using System.Collections.Generic;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaOnlinePanel
    {
        private enum LobbyTab { Matches, Create, Manual }

        internal delegate void ConnectHandler(string playerName, string address, int port, bool host,
            string relay, string roomName, bool publicRoom);

        private CostaSession session;
        private readonly ConnectHandler connect;
        private readonly CostaRoomBrowser browser = new();
        private readonly List<CostaRoomBrowser.RoomEntry> discovered = new();
        private LobbyTab tab = LobbyTab.Matches;
        private string name = PlayerPrefs.GetString("CostaMultiplayerName", "Jogador");
        private string roomName = PlayerPrefs.GetString("CostaRoomName", "Santa Luz Livre");
        private bool publicRoom = true;
        private string ip = "127.0.0.1";
        private string port = "7777";
        private bool internet = true;
        private const string DefaultRelay = "https://costalivre2-mibel10-relay.onrender.com";
        private string relay = PlayerPrefs.GetString("CostaRelayAddress", DefaultRelay);
        private string ConfiguredRelay => string.IsNullOrWhiteSpace(relay) ? DefaultRelay : relay.Trim();
        private bool showServer;
        private string inputError = "";
        private string invite = "";
        private float copiedUntil;
        private int listPage;
        private const int RoomsPerPage = 4;
        private static readonly int[] Powers = { 1, 3, 9, 27, 81 };
        private static readonly Color Text = new(0.96f, 0.95f, 0.90f);
        private static readonly Color Muted = new(0.70f, 0.78f, 0.80f);
        private static readonly Color Gold = new(0.98f, 0.70f, 0.36f);
        private static readonly Color Turquoise = new(0.18f, 0.82f, 0.80f);
        private static readonly Color Board = new(0.045f, 0.072f, 0.105f, 0.94f);
        private static readonly Color RowBg = new(0.08f, 0.13f, 0.17f, 0.92f);
        private GUIStyle labels;
        private GUIStyle title;
        private GUIStyle buttons;
        private TouchScreenKeyboard keyboard;
        private int editing;

        internal int Outfit { get; private set; } = Mathf.Clamp(PlayerPrefs.GetInt("CostaOutfit", 0), 0, 242);
        internal CostaOnlinePanel(ConnectHandler connect) => this.connect = connect;

        internal void AttachSession(CostaSession readySession) => session = readySession;
        internal void Release() => browser.Dispose();

        internal void Draw(float x, float y, float height, Action play)
        {
            ReadKeyboard();
            if (labels == null)
            {
                labels = Style(14, TextAnchor.MiddleLeft);
                title = Style(32, TextAnchor.MiddleLeft);
                buttons = Style(14, TextAnchor.MiddleCenter);
            }
            float panelHeight = Mathf.Min(height - y - 16, 412);
            Fill(new Rect(x - 9, y, 424, panelHeight), Board);
            Fill(new Rect(x - 9, y, 424, 3), Turquoise);
            Label(new Rect(x + 8, y + 4, 395, 42), "Multiplayer", title, Text);
            float row = y + 50;

            if (session?.Online != true)
            {
                bool connecting = session?.Connecting == true;

                // Top tabs: Active Matches | Create Room | Code / IP
                if (TabButton(new Rect(x + 8, row, 126, 30), L("Matches", "Partidas"), tab == LobbyTab.Matches, !connecting))
                {
                    tab = LobbyTab.Matches;
                    inputError = "";
                    browser.Poll(ConfiguredRelay, force: true);
                }
                if (TabButton(new Rect(x + 140, row, 126, 30), L("Create room", "Criar sala"), tab == LobbyTab.Create, !connecting))
                {
                    tab = LobbyTab.Create;
                    inputError = "";
                }
                if (TabButton(new Rect(x + 272, row, 126, 30), L("Code / IP", "Código / IP"), tab == LobbyTab.Manual, !connecting))
                {
                    tab = LobbyTab.Manual;
                    inputError = "";
                }

                float bodyY = row + 38;
                switch (tab)
                {
                    case LobbyTab.Matches:
                        DrawMatchesBrowser(x, bodyY, connecting);
                        break;
                    case LobbyTab.Create:
                        DrawCreateRoom(x, bodyY, connecting);
                        break;
                    case LobbyTab.Manual:
                        DrawManualConnect(x, bodyY, connecting);
                        break;
                }

                float footerY = y + panelHeight - 40;
                if (connecting && Button(new Rect(x + 278, footerY, 120, 30), L("Cancel", "Cancelar"), true))
                    session.Dispose();
                string feedback = inputError.Length > 0 ? inputError : session?.Status ?? "";
                Label(new Rect(x + 8, footerY, connecting ? 262 : 390, 30), feedback, labels, Gold);
                return;
            }

            // Connected lobby UI
            bool police = session.Local.police;
            string role = police ? L("Police", "Policial") : L("Criminal", "Criminoso");
            Label(new Rect(x + 8, row - 2, 388, 30),
                $"{role}   ·   {L("Police", "Policiais")} {session.PoliceCount}/{session.PoliceLimit}   ·   {session.HumanCount} {L("players", "jogadores")}",
                labels, Gold);
            if (Button(new Rect(x + 8, row + 32, 224, 31),
                police ? L("Become criminal", "Virar criminoso") : L("Join police", "Virar policial"),
                police || session.PoliceCount < session.PoliceLimit))
                session.Request("police");
            if (Button(new Rect(x + 242, row + 32, 156, 31), L("Leave", "Sair da sessão"), true))
            {
                session.Dispose();
                return;
            }
            if (session.NetworkRoundTrip > 0)
                Label(new Rect(x + 8, row + 134, 386, 21),
                    $"RTT {session.NetworkRoundTrip:0} ms · {session.NetworkBytesPerSecond / 1024f:0.0} KiB/s · "
                    + $"{session.NetworkEntities} {L("entities", "entidades")}", labels, Gold);
            if (session.RoomCode.Length > 0)
            {
                Label(new Rect(x + 8, row + 70, 224, 25), L("ROOM ", "SALA ") + session.RoomCode, labels, Gold);
                if (Button(new Rect(x + 242, row + 69, 156, 28),
                    Time.unscaledTime < copiedUntil ? L("Copied", "Copiado") : L("Copy invite", "Copiar convite"), true))
                {
                    GUIUtility.systemCopyBuffer = session.RoomInvite;
                    copiedUntil = Time.unscaledTime + 2f;
                }
            }
            else
                Label(new Rect(x + 8, row + 70, 388, 25), L("WARDROBE", "VESTUÁRIO"), labels, Muted);
            for (int i = 0; i < Powers.Length; i++)
            {
                int index = Outfit / Powers[i] % 3;
                float yy = row + 101 + i * 32;
                Label(new Rect(x + 8, yy, 260, 26), OutfitLabel(i, index), labels, Text);
                if (Button(new Rect(x + 280, yy, 54, 30), "‹", !police))
                    Change(i, index, -1);
                if (Button(new Rect(x + 344, yy, 54, 30), "›", !police))
                    Change(i, index, 1);
            }
            if (Button(new Rect(x + 8, row + 272, 184, 40), L("Play", "Jogar"), true))
                play();
            Label(new Rect(x + 200, row + 270, 198, 40), session.CurrentNotice, labels, Gold);
        }

        private void DrawMatchesBrowser(float x, float y, bool connecting)
        {
            browser.Poll(ConfiguredRelay);
            browser.GetRooms(discovered);

            Label(new Rect(x + 8, y, 120, 22), L("PLAYER NAME", "SEU NOME"), labels, Muted);
            name = Input(new Rect(x + 118, y - 2, 174, 28), name, 20, 1, TouchScreenKeyboardType.Default);
            if (Button(new Rect(x + 298, y - 2, 100, 28),
                browser.RefreshingInternet ? L("Searching...", "Buscando...") : L("Refresh", "Atualizar"), !connecting))
            {
                browser.Poll(ConfiguredRelay, force: true);
            }

            float listY = y + 36;
            if (discovered.Count == 0)
            {
                Fill(new Rect(x + 8, listY, 390, 168), RowBg);
                Label(new Rect(x + 18, listY + 34, 370, 26),
                    L("No active matches found yet.", "Nenhuma partida ativa encontrada."), labels, Text);
                Label(new Rect(x + 18, listY + 66, 370, 24),
                    L("Scanning Wi-Fi/LAN automatically...", "Procurando salas na rede Wi-Fi/LAN..."), labels, Muted);
                string serverHint = ConfiguredRelay.Length == 0
                    ? L("Tip: Set a Relay Server in Code/IP to browse Internet rooms.", "Dica: Configure um Servidor em Código/IP para salas online.")
                    : browser.InternetStatus == "offline"
                        ? L("Internet lobby server offline. Showing LAN only.", "Servidor online indisponível. Exibindo apenas LAN.")
                        : L("Or create a Public Room so friends can join!", "Ou crie uma Sala Pública para outros entrarem!");
                Label(new Rect(x + 18, listY + 98, 370, 24), serverHint, labels, Gold);
                if (Button(new Rect(x + 18, listY + 128, 172, 30), L("Create public room", "Criar sala pública"), !connecting))
                {
                    tab = LobbyTab.Create;
                    publicRoom = true;
                }
                return;
            }

            int pages = Mathf.Max(1, (discovered.Count + RoomsPerPage - 1) / RoomsPerPage);
            listPage = Mathf.Clamp(listPage, 0, pages - 1);
            int start = listPage * RoomsPerPage;
            int end = Mathf.Min(discovered.Count, start + RoomsPerPage);

            for (int i = start; i < end; i++)
            {
                var room = discovered[i];
                float ry = listY + (i - start) * 48;
                Fill(new Rect(x + 8, ry, 390, 42), RowBg);
                Fill(new Rect(x + 8, ry, 4, 42), room.IsLan ? Turquoise : Gold);
                Label(new Rect(x + 18, ry + 2, 210, 20), room.Name, labels, Text);
                Label(new Rect(x + 18, ry + 21, 210, 18),
                    $"{room.HostName}  ·  {(room.IsLan ? "LAN" : "NET")}", labels, Muted);
                Label(new Rect(x + 236, ry + 10, 62, 22), $"{room.Players}/{room.MaxPlayers}", labels, Gold);
                if (Button(new Rect(x + 306, ry + 6, 84, 30), L("Join", "Entrar"), !connecting))
                    JoinDiscoveredRoom(room);
            }

            if (pages > 1)
            {
                float navY = listY + RoomsPerPage * 48 + 4;
                if (listPage > 0 && Button(new Rect(x + 8, navY, 92, 26), L("Prev", "Anterior"), true))
                    listPage--;
                Label(new Rect(x + 166, navY, 70, 26), $"{listPage + 1} / {pages}", labels, Muted);
                if (listPage < pages - 1 && Button(new Rect(x + 306, navY, 92, 26), L("Next", "Próxima"), true))
                    listPage++;
            }
        }

        private void DrawCreateRoom(float x, float y, bool connecting)
        {
            Label(new Rect(x + 8, y, 360, 20), L("PLAYER NAME", "SEU NOME"), labels, Muted);
            name = Input(new Rect(x + 8, y + 22, 390, 32), name, 20, 1, TouchScreenKeyboardType.Default);

            Label(new Rect(x + 8, y + 60, 360, 20), L("ROOM NAME", "NOME DA SALA"), labels, Muted);
            roomName = Input(new Rect(x + 8, y + 82, 390, 32), roomName, 32, 6, TouchScreenKeyboardType.Default);

            Label(new Rect(x + 8, y + 120, 360, 20), L("VISIBILITY & NETWORK", "VISIBILIDADE E REDE"), labels, Muted);
            if (TabButton(new Rect(x + 8, y + 144, 190, 30), L("Public (listed)", "Pública (na lista)"), publicRoom, !connecting))
                publicRoom = true;
            if (TabButton(new Rect(x + 208, y + 144, 190, 30), L("Private (code)", "Privada (código)"), !publicRoom, !connecting))
                publicRoom = false;

            if (TabButton(new Rect(x + 8, y + 180, 190, 30), L("Local Wi-Fi (LAN)", "Rede Local (LAN)"), !internet, !connecting))
                internet = false;
            if (TabButton(new Rect(x + 208, y + 180, 190, 30), L("Internet", "Internet"), internet, !connecting))
                internet = true;

            if (Button(new Rect(x + 8, y + 222, 390, 38),
                internet ? L("Create Internet room", "Criar sala na Internet") : L("Create LAN room", "Criar sala LAN"), !connecting))
                StartHost();
        }

        private void DrawManualConnect(float x, float y, bool connecting)
        {
            if (TabButton(new Rect(x + 8, y, 190, 28), L("Room code / Relay", "Código / Servidor"), internet, !connecting))
                internet = true;
            if (TabButton(new Rect(x + 208, y, 190, 28), L("Direct IP (LAN)", "IP Direto (LAN)"), !internet, !connecting))
                internet = false;

            if (internet)
            {
                Label(new Rect(x + 8, y + 38, 240, 22), showServer
                    ? L("RELAY SERVER URL", "URL DO SERVIDOR RELAY")
                    : L("ROOM CODE OR INVITE", "CÓDIGO OU CONVITE DA SALA"), labels, Muted);
                if (Button(new Rect(x + 268, y + 36, 130, 25), showServer
                    ? L("Back to code", "Voltar ao código") : L("Server", "Servidor"), !connecting))
                {
                    showServer = !showServer;
                    inputError = "";
                }
                if (showServer)
                    relay = Input(new Rect(x + 8, y + 66, 390, 34), relay, 240, 4, TouchScreenKeyboardType.URL);
                else
                {
                    invite = Input(new Rect(x + 8, y + 66, 300, 34), invite, 270, 5, TouchScreenKeyboardType.URL);
                    if (Button(new Rect(x + 316, y + 66, 82, 34), L("Paste", "Colar"), !connecting))
                    {
                        string clipboard = (GUIUtility.systemCopyBuffer ?? "").Trim();
                        if (clipboard.Length <= 270)
                            invite = clipboard;
                        inputError = "";
                    }
                }
                if (Button(new Rect(x + 8, y + 114, 390, 36),
                    showServer ? L("Save server", "Salvar servidor") : L("Join by code", "Entrar por código"), !connecting))
                {
                    if (showServer) SaveServer();
                    else StartManualJoin();
                }
            }
            else
            {
                Label(new Rect(x + 8, y + 38, 250, 20), L("HOST IPV4", "IPV4 DO ANFITRIÃO"), labels, Muted);
                Label(new Rect(x + 306, y + 38, 86, 20), L("PORT", "PORTA"), labels, Muted);
                ip = Input(new Rect(x + 8, y + 62, 286, 34), ip, 15, 2, TouchScreenKeyboardType.NumbersAndPunctuation);
                port = Input(new Rect(x + 306, y + 62, 92, 34), port, 5, 3, TouchScreenKeyboardType.NumberPad);
                if (Button(new Rect(x + 8, y + 110, 390, 36), L("Join by IP", "Entrar por IP"), !connecting))
                    StartManualJoin();
            }
        }

        private void JoinDiscoveredRoom(CostaRoomBrowser.RoomEntry room)
        {
            inputError = "";
            SavePlayerPrefs();
            if (room.IsLan)
                connect(name, room.Address, room.Port, false, null, room.Name, true);
            else
                connect(name, room.Address, 0, false, room.RelayEndpoint, room.Name, true);
        }

        private void StartHost()
        {
            inputError = "";
            int.TryParse(port, out int number);
            if (number <= 0) number = 7777;
            string endpoint = null;
            if (internet)
            {
                if (!SaveServer())
                    return;
                endpoint = ConfiguredRelay;
            }
            SavePlayerPrefs();
            connect(name, "", number, true, endpoint, roomName, publicRoom);
        }

        private void StartManualJoin()
        {
            inputError = "";
            int.TryParse(port, out int number);
            string address = ip;
            string endpoint = null;
            if (internet)
            {
                if (CostaNorayRoom.TryCode(invite.Trim(), out _))
                {
                    endpoint = CostaNorayRoom.Service;
                    address = invite.Trim();
                }
                else if (!CostaInternetRoom.TryInvite(invite, ConfiguredRelay, out endpoint, out address))
                {
                    inputError = L("Paste a valid room invitation.", "Cole um convite de sala válido.");
                    return;
                }
            }
            SavePlayerPrefs();
            connect(name, address, number, false, endpoint, "", false);
        }

        private void SavePlayerPrefs()
        {
            PlayerPrefs.SetString("CostaMultiplayerName", name);
            PlayerPrefs.SetString("CostaRoomName", roomName);
            PlayerPrefs.Save();
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
                    case 6: roomName = keyboard.text; break;
                }
            }
            keyboard = null;
        }

        private bool SaveServer()
        {
            string address = ConfiguredRelay;
            if (address.Length > 0 && !CostaInternetRoom.TryEndpoint(address, out _))
            {
                inputError = L("Enter a valid HTTPS server address.", "Informe um endereço HTTPS válido.");
                return false;
            }
            relay = address;
            PlayerPrefs.SetString("CostaRelayAddress", relay);
            PlayerPrefs.Save();
            inputError = "";
            showServer = false;
            browser.Poll(ConfiguredRelay, force: true);
            return true;
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
                case 3: return L("Hat   ", "Chapéu   ") + (option == 0 ? L("None", "Nenhum") : option == 1 ? L("Cap", "Boné") : L("Bucket", "Bucket"));
                default: return L("Accessory   ", "Acessório   ") + (option == 0 ? L("None", "Nenhum") : option == 1 ? L("Glasses", "Óculos") : L("Bag", "Bolsa"));
            }
        }

        private static GUIStyle Style(int size, TextAnchor anchor) => new(GUI.skin.label)
        {
            fontSize = size, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip,
            alignment = anchor, wordWrap = false, padding = new RectOffset()
        };

        private bool TabButton(Rect area, string value, bool selected, bool active)
        {
            Fill(area, selected ? Turquoise : active ? new Color(0.16f, 0.24f, 0.28f) : new Color(0.09f, 0.14f, 0.17f));
            bool old = GUI.enabled;
            GUI.enabled = old && active;
            buttons.normal.textColor = selected ? new Color(0.04f, 0.07f, 0.10f) : active ? Text : Muted;
            bool result = CostaUi.Button(area, value, buttons);
            GUI.enabled = old;
            return result;
        }

        private bool Button(Rect area, string value, bool active)
        {
            Fill(area, active ? new Color(0.18f, 0.30f, 0.34f) : new Color(0.10f, 0.15f, 0.18f));
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
