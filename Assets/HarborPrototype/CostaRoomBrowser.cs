using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CostaLivre
{
    /// <summary>
    /// Discovers active Costa Livre 2 matches on LAN (UDP broadcast) and Internet (Relay /rooms endpoint).
    /// </summary>
    internal sealed class CostaRoomBrowser : IDisposable
    {
        internal const int DiscoveryPort = 47777;
        private const string Magic = "CL2ROOM";
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
        private static UdpClient sharedBroadcaster;

        internal sealed class RoomEntry
        {
            internal string Key;
            internal string Name;
            internal string HostName;
            internal int Players;
            internal int MaxPlayers;
            internal bool IsLan;
            internal string Address;       // IPv4 for LAN, room code for Internet
            internal int Port;
            internal string RelayEndpoint; // HTTPS/WSS server URL for Internet rooms
            internal long LastSeenMs;
        }

        [Serializable]
        private sealed class LanBeacon
        {
            public string magic;
            public int protocol;
            public string name;
            public string host;
            public int port;
            public int players;
            public int maxPlayers;
        }

        [Serializable]
        private sealed class RelayRoomDto
        {
            public string code;
            public string name;
            public string host;
            public int players;
            public int maxPlayers;
        }

        [Serializable]
        private sealed class RelayRoomList
        {
            public RelayRoomDto[] rooms;
        }

        private readonly object gate = new();
        private readonly Dictionary<string, RoomEntry> lanRooms = new(StringComparer.Ordinal);
        private readonly List<RoomEntry> internetRooms = new();
        private UdpClient listener;
        private CancellationTokenSource cancel;
        private bool listening;
        private bool refreshingInternet;
        private float nextInternetPoll;
        private string lastInternetStatus = "";

        internal bool RefreshingInternet => refreshingInternet;
        internal string InternetStatus
        {
            get { lock (gate) return lastInternetStatus; }
        }

        internal static void BroadcastHost(string roomName, string hostName, int tcpPort, int players, int maxPlayers, int protocol)
        {
            if (tcpPort <= 0 || tcpPort > 65535)
                return;
            try
            {
                if (sharedBroadcaster == null)
                {
                    sharedBroadcaster = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
                }
                string json = JsonUtility.ToJson(new LanBeacon
                {
                    magic = Magic,
                    protocol = protocol,
                    name = string.IsNullOrWhiteSpace(roomName) ? "Santa Luz" : roomName.Trim(),
                    host = string.IsNullOrWhiteSpace(hostName) ? "Jogador" : hostName.Trim(),
                    port = tcpPort,
                    players = Mathf.Max(1, players),
                    maxPlayers = Mathf.Max(2, maxPlayers)
                });
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                sharedBroadcaster.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException or InvalidOperationException)
            {
            }
        }

        internal void EnsureListening()
        {
            if (listening)
                return;
            listening = true;
            cancel = new CancellationTokenSource();
            try
            {
                listener = new UdpClient();
                listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.ExclusiveAddressUse = false;
                listener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                _ = ReceiveLanLoop(cancel.Token);
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
                listener?.Dispose();
                listener = null;
            }
        }

        internal void Poll(string relayUrl, bool force = false)
        {
            EnsureListening();
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            PruneStaleLan(nowMs);

            float now = Time.unscaledTime;
            if (!force && now < nextInternetPoll)
                return;
            nextInternetPoll = now + 4.0f;

            if (string.IsNullOrWhiteSpace(relayUrl))
            {
                lock (gate)
                {
                    internetRooms.Clear();
                    lastInternetStatus = "";
                }
                return;
            }
            if (!refreshingInternet)
                _ = FetchInternetRoomsAsync(relayUrl.Trim());
        }

        internal void GetRooms(List<RoomEntry> output)
        {
            output.Clear();
            lock (gate)
            {
                foreach (var entry in lanRooms.Values)
                    output.Add(entry);
                for (int i = 0; i < internetRooms.Count; i++)
                    output.Add(internetRooms[i]);
            }
            output.Sort((a, b) =>
            {
                int lanCmp = b.IsLan.CompareTo(a.IsLan);
                return lanCmp != 0 ? lanCmp : b.Players.CompareTo(a.Players);
            });
        }

        private void PruneStaleLan(long nowMs)
        {
            lock (gate)
            {
                if (lanRooms.Count == 0)
                    return;
                List<string> stale = null;
                foreach (var kv in lanRooms)
                {
                    if (nowMs - kv.Value.LastSeenMs > 5500)
                    {
                        stale ??= new List<string>();
                        stale.Add(kv.Key);
                    }
                }
                if (stale != null)
                    for (int i = 0; i < stale.Count; i++)
                        lanRooms.Remove(stale[i]);
            }
        }

        private async Task ReceiveLanLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && listener != null)
            {
                try
                {
                    UdpReceiveResult result = await listener.ReceiveAsync().ConfigureAwait(false);
                    if (result.Buffer == null || result.Buffer.Length == 0 || result.Buffer.Length > 512)
                        continue;
                    string text = Encoding.UTF8.GetString(result.Buffer);
                    LanBeacon beacon = JsonUtility.FromJson<LanBeacon>(text);
                    if (beacon == null || beacon.magic != Magic || beacon.port is < 1 or > 65535)
                        continue;
                    string ip = result.RemoteEndPoint.Address.ToString();
                    string key = $"lan:{ip}:{beacon.port}";
                    lock (gate)
                    {
                        lanRooms[key] = new RoomEntry
                        {
                            Key = key,
                            Name = string.IsNullOrWhiteSpace(beacon.name) ? "Santa Luz" : beacon.name,
                            HostName = string.IsNullOrWhiteSpace(beacon.host) ? "Jogador" : beacon.host,
                            Players = Mathf.Max(1, beacon.players),
                            MaxPlayers = Mathf.Max(2, beacon.maxPlayers),
                            IsLan = true,
                            Address = ip,
                            Port = beacon.port,
                            RelayEndpoint = null,
                            LastSeenMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        };
                    }
                }
                catch (Exception ex) when (ex is SocketException or ObjectDisposedException or ArgumentException)
                {
                    if (token.IsCancellationRequested)
                        break;
                    await Task.Delay(250).ConfigureAwait(false);
                }
            }
        }

        private async Task FetchInternetRoomsAsync(string relayUrl)
        {
            if (!CostaInternetRoom.TryEndpoint(relayUrl, out Uri wsUri))
                return;
            refreshingInternet = true;
            try
            {
                var builder = new UriBuilder(wsUri)
                {
                    Scheme = wsUri.Scheme == "ws" ? "http" : "https",
                    Path = "/rooms"
                };
                string json = await Http.GetStringAsync(builder.Uri).ConfigureAwait(false);
                RelayRoomList parsed = JsonUtility.FromJson<RelayRoomList>(json);
                var fresh = new List<RoomEntry>();
                if (parsed?.rooms != null)
                {
                    foreach (var dto in parsed.rooms)
                    {
                        if (dto == null || string.IsNullOrEmpty(dto.code))
                            continue;
                        fresh.Add(new RoomEntry
                        {
                            Key = "net:" + dto.code,
                            Name = string.IsNullOrWhiteSpace(dto.name) ? "Santa Luz" : dto.name,
                            HostName = string.IsNullOrWhiteSpace(dto.host) ? "Jogador" : dto.host,
                            Players = Mathf.Max(1, dto.players),
                            MaxPlayers = Mathf.Max(2, dto.maxPlayers),
                            IsLan = false,
                            Address = dto.code,
                            Port = 0,
                            RelayEndpoint = relayUrl,
                            LastSeenMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        });
                    }
                }
                lock (gate)
                {
                    internetRooms.Clear();
                    internetRooms.AddRange(fresh);
                    lastInternetStatus = "";
                }
            }
            catch
            {
                lock (gate)
                {
                    internetRooms.Clear();
                    lastInternetStatus = "offline";
                }
            }
            finally
            {
                refreshingInternet = false;
            }
        }

        public void Dispose()
        {
            listening = false;
            try { cancel?.Cancel(); } catch { }
            cancel?.Dispose();
            cancel = null;
            listener?.Dispose();
            listener = null;
        }
    }
}
