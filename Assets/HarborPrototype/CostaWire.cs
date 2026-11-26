using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CostaLivre
{
    internal sealed class CostaWire : IDisposable
    {
        private const int Limit = 32768;
        private readonly TcpClient client;
        private readonly NetworkStream stream;
        private readonly ClientWebSocket websocket;
        private readonly ConcurrentQueue<string> incoming = new();
        private readonly ConcurrentQueue<byte[]> outgoing = new();
        private readonly SemaphoreSlim pending = new(0);
        private readonly CancellationTokenSource cancel = new();
        private int inCount;
        private int outCount;
        private byte[] latestState;
        private int closed;

        internal bool Closed => Volatile.Read(ref closed) != 0;

        internal CostaWire(TcpClient client)
        {
            this.client = client;
            client.NoDelay = true;
            stream = client.GetStream();
            _ = ReadLoop();
            _ = WriteLoop();
        }

        internal CostaWire(ClientWebSocket websocket)
        {
            this.websocket = websocket;
            _ = ReadLoop();
            _ = WriteLoop();
        }

        internal static async Task<CostaWire> ConnectTcp(IPAddress address, int port, CancellationToken token)
        {
            var client = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                using (token.Register(client.Close))
                    await client.ConnectAsync(address, port).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return new CostaWire(client);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        internal bool Receive(out string frame)
        {
            if (!incoming.TryDequeue(out frame))
                return false;
            Interlocked.Decrement(ref inCount);
            return true;
        }

        internal void Send(string text)
        {
            if (Closed)
                return;

            int length = Encoding.UTF8.GetByteCount(text);
            if (length > Limit || Interlocked.Increment(ref outCount) > 96)
            {
                Dispose();
                return;
            }

            outgoing.Enqueue(Encode(text, length));
            pending.Release();
        }

        internal void SendState(byte[] shared, string personal)
        {
            int length = shared.Length - 1 + Encoding.UTF8.GetByteCount(personal);
            if (Closed)
                return;
            if (length > Limit)
            {
                Dispose();
                return;
            }

            byte[] frame = AllocateFrame(length);
            Buffer.BlockCopy(shared, 0, frame, 4, shared.Length - 1);
            frame[shared.Length + 3] = (byte)',';
            Encoding.UTF8.GetBytes(personal, 1, personal.Length - 1, frame, shared.Length + 4);
            // One queue marker represents the newest state waiting to be sent.
            if (Interlocked.Exchange(ref latestState, frame) == null)
            {
                outgoing.Enqueue(null);
                pending.Release();
            }
        }

        internal async Task Reject(string text)
        {
            try
            {
                await WriteFrame(Encode(text, Encoding.UTF8.GetByteCount(text))).ConfigureAwait(false);
            }
            catch (Exception exception) when (NetworkException(exception))
            {
            }
            finally
            {
                Dispose();
            }
        }

        private Task WriteFrame(byte[] frame) => websocket == null
            ? stream.WriteAsync(frame, 0, frame.Length, cancel.Token)
            : websocket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, cancel.Token);

        internal static bool NetworkException(Exception error) => error is IOException || error is SocketException
            || error is WebSocketException || error is OperationCanceledException || error is ObjectDisposedException;

        private static byte[] Encode(string text, int length)
        {
            byte[] frame = AllocateFrame(length);
            Encoding.UTF8.GetBytes(text, 0, text.Length, frame, 4);
            return frame;
        }

        private static byte[] AllocateFrame(int length)
        {
            byte[] frame = new byte[length + 4];
            frame[0] = (byte)(length >> 24);
            frame[1] = (byte)(length >> 16);
            frame[2] = (byte)(length >> 8);
            frame[3] = (byte)length;
            return frame;
        }

        private async Task ReadLoop()
        {
            try
            {
                byte[] header = new byte[4];
                while (!Closed)
                {
                    await ReadExact(header).ConfigureAwait(false);
                    int length = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
                    if (length <= 0 || length > Limit || Interlocked.Increment(ref inCount) > 96)
                        break;

                    byte[] bytes = new byte[length];
                    await ReadExact(bytes).ConfigureAwait(false);
                    incoming.Enqueue(Encoding.UTF8.GetString(bytes));
                }
            }
            catch (Exception exception) when (NetworkException(exception))
            {
            }
            finally
            {
                Dispose();
            }
        }

        private async Task ReadExact(byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                if (websocket != null)
                {
                    var chunk = new ArraySegment<byte>(buffer, offset, buffer.Length - offset);
                    var result = await websocket.ReceiveAsync(chunk, cancel.Token).ConfigureAwait(false);
                    if (result.MessageType != WebSocketMessageType.Binary || (result.Count == 0 && result.EndOfMessage))
                        throw new EndOfStreamException();
                    offset += result.Count;
                }
                else
                {
                    int size = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancel.Token).ConfigureAwait(false);
                    if (size == 0)
                        throw new EndOfStreamException();
                    offset += size;
                }
            }
        }

        private async Task WriteLoop()
        {
            try
            {
                while (!Closed)
                {
                    await pending.WaitAsync(cancel.Token).ConfigureAwait(false);
                    if (outgoing.TryDequeue(out byte[] frame))
                    {
                        if (frame == null)
                            frame = Interlocked.Exchange(ref latestState, null);
                        else
                            Interlocked.Decrement(ref outCount);
                        if (frame != null)
                            await WriteFrame(frame).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception) when (NetworkException(exception))
            {
            }
            finally
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref closed, 1) == 0)
            {
                cancel.Cancel();
                client?.Close();
                websocket?.Dispose();
            }
        }
    }
}
