using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CostaLivre
{
    internal sealed class CostaInternetRoom : IDisposable
    {
        private readonly Uri relay;
        private readonly ClientWebSocket control = new();
        private readonly CancellationTokenSource close = new();
        private readonly Queue<CostaWire> incoming = new();
        private readonly object gate = new();
        private bool disposed;
        private int ended;

        private readonly SemaphoreSlim guestSlots = new(CostaSession.MaxPlayers - 1);
        private readonly SemaphoreSlim controlSend = new(1, 1);

        [Serializable]
        private sealed class RoomMeta
        {
            public string name;
            public string host;
            public bool @public;
            public bool locked;
            public int players;
            public int maxPlayers;
        }

        internal string Code { get; private set; } = "";
        internal bool Ended => Volatile.Read(ref ended) != 0;

        internal CostaInternetRoom(Uri relay) => this.relay = relay;

        internal static bool TryEndpoint(string address, out Uri endpoint)
        {
            endpoint = null;
            if (string.IsNullOrWhiteSpace(address) || !Uri.TryCreate(address.Trim(), UriKind.Absolute, out Uri url) ||
                (url.Scheme != "https" && url.Scheme != "wss" && !(url.Scheme == "ws" && url.IsLoopback)) ||
                string.IsNullOrEmpty(url.Host) || url.AbsolutePath != "/" || url.Query != "" ||
                url.Fragment != "" || url.UserInfo != "")
                return false;
            endpoint = url.Scheme == "https" ? new UriBuilder(url) { Scheme = "wss" }.Uri : url;
            return true;
        }

        internal static bool TryInvite(string value, string defaultEndpoint, out string endpoint, out string code)
        {
            endpoint = null;
            code = (value ?? "").Trim();
            string candidate = defaultEndpoint;
            if (Uri.TryCreate(code, UriKind.Absolute, out var link))
            {
                if (link.Query.Length != 0 || link.Fragment.Length != 0 || link.UserInfo.Length != 0
                    || !link.AbsolutePath.StartsWith("/join/", StringComparison.Ordinal))
                    return false;
                candidate = link.GetLeftPart(UriPartial.Authority);
                code = link.AbsolutePath.Substring(6);
            }
            code = code.ToUpperInvariant();
            if (!ValidCode(code, 10, UpperHex) || !TryEndpoint(candidate, out _))
                return false;
            endpoint = candidate;
            return true;
        }

        internal static bool ValidCode(string value, int size, string alphabet)
        {
            if (value.Length != size)
                return false;
            foreach (char character in value)
                if (alphabet.IndexOf(character) < 0)
                    return false;
            return true;
        }

        internal const string UpperHex = "0123456789ABCDEF";
        private const string LowerHex = "0123456789abcdef";

        internal Task<CostaWire> Host(int port, CancellationToken token) =>
            OpenHost(new Uri(relay, "host"), port, token);

        internal Task<CostaWire> Claim(int port, string existingCode, CancellationToken token) =>
            OpenHost(new Uri(relay, "claim/" + existingCode), port, token);

        private async Task<CostaWire> OpenHost(Uri target, int port, CancellationToken token)
        {
            await control.ConnectAsync(target, token).ConfigureAwait(false);
            var buffer = new byte[64];
            string message = await ReadControl(buffer, token).ConfigureAwait(false);
            if (!message.StartsWith("room ", StringComparison.Ordinal) || !ValidCode(message.Substring(5), 10, UpperHex))
                throw new WebSocketException("Invalid relay room response.");

            Code = message.Substring(5);
            _ = AcceptGuests(buffer);
            return await CostaWire.ConnectTcp(IPAddress.Loopback, port, token).ConfigureAwait(false);
        }

        internal static async Task<CostaWire> Join(Uri relay, string code, CancellationToken token) =>
            new CostaWire(await Connect(new Uri(relay, "join/" + code), token).ConfigureAwait(false));

        private static async Task<ClientWebSocket> Connect(Uri uri, CancellationToken token)
        {
            var socket = new ClientWebSocket();
            try
            {
                await socket.ConnectAsync(uri, token).ConfigureAwait(false);
                return socket;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private async Task<string> ReadControl(byte[] buffer, CancellationToken token)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                var chunk = new ArraySegment<byte>(buffer, offset, buffer.Length - offset);
                var result = await control.ReceiveAsync(chunk, token).ConfigureAwait(false);
                if (result.MessageType != WebSocketMessageType.Text)
                    break;
                offset += result.Count;
                if (result.EndOfMessage)
                    return Encoding.ASCII.GetString(buffer, 0, offset);
            }
            throw new WebSocketException("Invalid relay control message.");
        }

        private async Task AcceptGuests(byte[] buffer)
        {
            try
            {
                while (!close.IsCancellationRequested)
                {
                    string message = await ReadControl(buffer, close.Token).ConfigureAwait(false);
                    if (!message.StartsWith("peer ", StringComparison.Ordinal) || !ValidCode(message.Substring(5), 32, LowerHex))
                        throw new WebSocketException("Invalid relay guest response.");

                    if (guestSlots.Wait(0))
                        _ = ConnectGuest(message.Substring(5));
                }
            }
            catch (Exception exception) when (CostaWire.NetworkException(exception))
            {
            }
            finally
            {
                Volatile.Write(ref ended, 1);
            }
        }

        private async Task ConnectGuest(string ticket)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(close.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                var wire = new CostaWire(await Connect(new Uri(relay, "peer/" + ticket), timeout.Token).ConfigureAwait(false));
                lock (gate)
                {
                    if (disposed || incoming.Count >= CostaSession.MaxPlayers)
                        wire.Dispose();
                    else
                        incoming.Enqueue(wire);
                }
            }
            catch (Exception exception) when (CostaWire.NetworkException(exception))
            {
                // A guest can close before the host opens its side.
            }
            finally
            {
                guestSlots.Release();
            }
        }

        internal bool TryAccept(out CostaWire wire)
        {
            lock (gate)
                return incoming.TryDequeue(out wire);
        }

        internal void UpdateListing(string roomName, string hostName, bool isPublic, int players, int maxPlayers, bool locked = false)
        {
            if (disposed || Ended || control.State != WebSocketState.Open)
                return;
            string json = UnityEngine.JsonUtility.ToJson(new RoomMeta
            {
                name = string.IsNullOrWhiteSpace(roomName) ? "Santa Luz" : roomName.Trim(),
                host = string.IsNullOrWhiteSpace(hostName) ? "Jogador" : hostName.Trim(),
                @public = isPublic,
                locked = locked,
                players = Math.Max(1, players),
                maxPlayers = Math.Max(2, maxPlayers)
            });
            byte[] payload = Encoding.UTF8.GetBytes("meta " + json);
            _ = SendControlAsync(payload);
        }

        private async Task SendControlAsync(byte[] payload)
        {
            try
            {
                await controlSend.WaitAsync(close.Token).ConfigureAwait(false);
                try
                {
                    if (!disposed && control.State == WebSocketState.Open)
                        await control.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, close.Token).ConfigureAwait(false);
                }
                finally
                {
                    controlSend.Release();
                }
            }
            catch (Exception exception) when (CostaWire.NetworkException(exception) || exception is ObjectDisposedException)
            {
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                    return;
                disposed = true;
                while (incoming.TryDequeue(out var wire))
                    wire.Dispose();
            }
            close.Cancel();
            control.Dispose();
        }
    }
}
