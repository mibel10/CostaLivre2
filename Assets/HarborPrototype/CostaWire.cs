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

            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            if (textBytes.Length > Limit || Interlocked.Increment(ref outCount) > 96)
            {
                Dispose();
                return;
            }

            byte[] frame = new byte[textBytes.Length + 4];
            int length = textBytes.Length;
            frame[0] = (byte)(length >> 24);
            frame[1] = (byte)(length >> 16);
            frame[2] = (byte)(length >> 8);
            frame[3] = (byte)length;
            Buffer.BlockCopy(textBytes, 0, frame, 4, length);
            outgoing.Enqueue(frame);
            pending.Release();
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
