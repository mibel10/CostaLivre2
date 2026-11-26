using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
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
        private readonly ConcurrentQueue<string> incoming = new();
        private readonly ConcurrentQueue<byte[]> outgoing = new();
        private readonly SemaphoreSlim pending = new(0);
        private readonly CancellationTokenSource cancel = new();
        private int inCount;
        private int outCount;
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

        internal static async Task Reject(TcpClient client, string text)
        {
            using (client)
            {
                try
                {
                    byte[] response = Encode(text, Encoding.UTF8.GetByteCount(text));
                    await client.GetStream().WriteAsync(response, 0, response.Length).ConfigureAwait(false);
                }
                catch (Exception e) when (e is System.IO.IOException || e is SocketException || e is ObjectDisposedException)
                {
                }
            }
        }

        private static byte[] Encode(string text, int length)
        {
            byte[] frame = new byte[length + 4];
            frame[0] = (byte)(length >> 24);
            frame[1] = (byte)(length >> 16);
            frame[2] = (byte)(length >> 8);
            frame[3] = (byte)length;
            Encoding.UTF8.GetBytes(text, 0, text.Length, frame, 4);
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
            catch (Exception exception) when (exception is System.IO.IOException || exception is SocketException
                || exception is OperationCanceledException || exception is ObjectDisposedException)
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
                int size = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancel.Token).ConfigureAwait(false);
                if (size == 0)
                    throw new System.IO.EndOfStreamException();
                offset += size;
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
                        Interlocked.Decrement(ref outCount);
                        await stream.WriteAsync(frame, 0, frame.Length, cancel.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is SocketException
                || exception is OperationCanceledException || exception is ObjectDisposedException)
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
                client.Close();
            }
        }
    }
}
