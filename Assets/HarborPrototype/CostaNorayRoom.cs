using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using kcp2k;

namespace CostaLivre
{
    internal sealed class CostaNorayRoom : IDisposable
    {
        internal const string Service = "noray";
        private const string PublicHost = "foxssake.studio";
        private const int ControlPort = 8890;
        private const int RegistrationPort = 8809;
        private const int Mtu = 1200;
        private static readonly byte[] Handshake = Encoding.ASCII.GetBytes("$rwx");

        private readonly TcpClient control = new(AddressFamily.InterNetwork);
        private readonly UdpClient udp = new(new IPEndPoint(IPAddress.Any, 0));
        private readonly ConcurrentQueue<CostaWire> guests = new();
        private readonly ConcurrentQueue<int> announced = new();
        private readonly Dictionary<int, Peer> peers = new();
        private readonly List<int> expired = new();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly byte[] payload = new byte[CostaWire.Limit + 4];
        private IPAddress address;
        private NetworkStream stream;
        private int ended;
        private int disposed;
        internal bool Ended => Volatile.Read(ref ended) != 0;
        internal string Code { get; private set; } = "";

        private sealed class Peer
        {
            internal Kcp Channel;
            internal CostaWire Wire;
            internal long LastPacket;
            internal long NextHandshake;
        }

        internal static bool TryCode(string invite, out string id)
        {
            id = "";
            if (invite == null || !invite.StartsWith("CL2N-", StringComparison.OrdinalIgnoreCase))
                return false;
            string candidate = invite.Substring(5);
            if (!ValidId(candidate))
                return false;
            id = candidate;
            return true;
        }

        private static bool ValidId(string value)
        {
            if (value.Length is < 1 or > 128)
                return false;
            foreach (char c in value)
                if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_'))
                    return false;
            return true;
        }

        internal async Task<CostaWire> Connect(bool host, string target, int localPort, CancellationToken token)
        {
            using (token.Register(Dispose))
            {
                await control.ConnectAsync(PublicHost, ControlPort).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                address = ((IPEndPoint)control.Client.RemoteEndPoint).Address;
                stream = control.GetStream();
                await SendCommand("register-host", token).ConfigureAwait(false);
                var reader = new StreamReader(stream, Encoding.ASCII, false, 256, leaveOpen: true);
                string id = "", privateId = "";
                while (id.Length == 0 || privateId.Length == 0)
                {
                    string line = await ReadCommand(reader).ConfigureAwait(false);
                    if (line.StartsWith("set-oid ", StringComparison.Ordinal))
                        id = line.Substring(8);
                    if (line.StartsWith("set-pid ", StringComparison.Ordinal))
                        privateId = line.Substring(8);
                }
                if (!ValidId(id) || !ValidId(privateId))
                    throw new IOException("Invalid Noray registration.");

                await RegisterUdp(privateId, token).ConfigureAwait(false);
                if (host)
                {
                    CostaWire local = await CostaWire.ConnectTcp(IPAddress.Loopback, localPort, token).ConfigureAwait(false);
                    Code = "CL2N-" + id;
                    // Transfer ownership of the reader to the room loop.
                    _ = ReadPorts(reader);
                    _ = Run();
                    return local;
                }

                await SendCommand("connect-relay " + target, token).ConfigureAwait(false);
                int port;
                while ((port = RelayPort(await ReadCommand(reader).ConfigureAwait(false))) == 0)
                    token.ThrowIfCancellationRequested();
                reader.Dispose();
                Peer peer = AddPeer(port);
                _ = Run();
                return peer.Wire;
            }
        }

        private async Task RegisterUdp(string privateId, CancellationToken token)
        {
            byte[] registration = Encoding.ASCII.GetBytes(privateId);
            var registry = new IPEndPoint(address, RegistrationPort);
            while (true)
            {
                token.ThrowIfCancellationRequested();
                udp.Send(registration, registration.Length, registry);
                await Task.Delay(160, token).ConfigureAwait(false);
                while (udp.Available > 0)
                {
                    IPEndPoint origin = null;
                    byte[] response = udp.Receive(ref origin);
                    if (registry.Equals(origin) && response.Length == 2 && response[0] == 'O' && response[1] == 'K')
                        return;
                }
            }
        }

        private Task SendCommand(string command, CancellationToken token)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(command + "\n");
            return stream.WriteAsync(bytes, 0, bytes.Length, token);
        }

        private static async Task<string> ReadCommand(StreamReader reader)
        {
            string line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null || line.Length > 256 || line.StartsWith("error ", StringComparison.Ordinal))
                throw new IOException("Noray could not complete the connection.");
            return line;
        }

        private static int RelayPort(string line) => line.StartsWith("connect-relay ", StringComparison.Ordinal)
            && int.TryParse(line.Substring(14), out int port) && port is > 0 and <= 65535 ? port : 0;

        private async Task ReadPorts(StreamReader reader)
        {
            using (reader)
            {
                try
                {
                    while (!Ended)
                    {
                        int port = RelayPort(await ReadCommand(reader).ConfigureAwait(false));
                        if (port != 0 && announced.Count < CostaSession.MaxPlayers)
                            announced.Enqueue(port);
                    }
                }
                catch (Exception error) when (CostaWire.NetworkException(error))
                {
                    Dispose();
                }
            }
        }

        private Peer AddPeer(int port)
        {
            var endpoint = new IPEndPoint(address, port);
            var channel = new Kcp(0x434C324E, (bytes, length) => udp.Send(bytes, length, endpoint));
            channel.SetMtu(Mtu);
            channel.SetNoDelay(1, 20, 2, false);
            channel.SetWindowSize(64, 64);
            var peer = new Peer { Channel = channel, Wire = new CostaWire(), LastPacket = clock.ElapsedMilliseconds };
            peers.Add(port, peer);
            return peer;
        }

        private async Task Run()
        {
            try
            {
                while (!Ended)
                {
                    long now = clock.ElapsedMilliseconds;
                    while (announced.TryDequeue(out int port))
                    {
                        if (peers.Count >= CostaSession.MaxPlayers - 1 || peers.ContainsKey(port))
                            continue;
                        guests.Enqueue(AddPeer(port).Wire);
                    }
                    for (int count = 0; count < 128 && udp.Available > 0; count++)
                    {
                        IPEndPoint origin = null;
                        byte[] packet = udp.Receive(ref origin);
                        if (!origin.Address.Equals(address) || !peers.TryGetValue(origin.Port, out Peer peer))
                            continue;
                        if (packet.AsSpan().SequenceEqual(Handshake))
                        {
                            peer.LastPacket = now;
                            continue;
                        }
                        if (packet.Length is >= 24 and <= Mtu && peer.Channel.Input(packet, 0, packet.Length) == 0)
                        {
                            peer.LastPacket = now;
                            peer.NextHandshake = long.MaxValue;
                        }
                    }

                    expired.Clear();
                    foreach (var entry in peers)
                    {
                        Peer peer = entry.Value;
                        if (peer.Wire.Closed || now - peer.LastPacket > 12000 || peer.Channel.WaitSnd > 256)
                        {
                            peer.Wire.Dispose();
                            expired.Add(entry.Key);
                            continue;
                        }
                        if (now >= peer.NextHandshake)
                        {
                            udp.Send(Handshake, Handshake.Length, new IPEndPoint(address, entry.Key));
                            peer.NextHandshake = now + 125;
                        }
                        for (int i = 0; i < 8 && peer.Channel.WaitSnd < 64 && peer.Wire.TakeOutgoing(out byte[] frame); i++)
                            if (peer.Channel.Send(frame, 0, frame.Length) < 0)
                                peer.Wire.Dispose();
                        peer.Channel.Update((uint)now);
                        while (peer.Channel.PeekSize() > 0)
                        {
                            int size = peer.Channel.Receive(payload, payload.Length);
                            if (size <= 0 || !peer.Wire.ReceiveDatagram(payload, size))
                            {
                                peer.Wire.Dispose();
                                break;
                            }
                        }
                    }
                    foreach (int port in expired)
                        peers.Remove(port);
                    if (Code.Length == 0 && peers.Count == 0)
                        return;
                    await Task.Delay(12).ConfigureAwait(false);
                }
            }
            catch (Exception error) when (CostaWire.NetworkException(error))
            {
            }
            finally
            {
                foreach (Peer peer in peers.Values)
                    peer.Wire.Dispose();
                Dispose();
            }
        }

        internal bool TryAccept(out CostaWire wire) => guests.TryDequeue(out wire);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            Volatile.Write(ref ended, 1);
            control.Dispose();
            udp.Dispose();
        }
    }
}
