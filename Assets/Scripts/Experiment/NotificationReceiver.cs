using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Experiment1
{
    // TCP framing only. This class has no knowledge of intervention states or UI.
    public sealed class NotificationReceiver : MonoBehaviour
    {
        [SerializeField, Range(1024, 65535)] private int port = 5055;
        public int Port => port;
        public bool IsListening => running;
        public event Action<string, DateTimeOffset> CommandReceived;
        private const int MaxLineBytes = 256;
        private const int MaxPendingCommands = 64;
        private readonly ConcurrentQueue<ReceivedCommand> commands = new ConcurrentQueue<ReceivedCommand>();
        private readonly ConcurrentQueue<string> errors = new ConcurrentQueue<string>();
        private TcpListener listener;
        private TcpClient activeClient;
        private readonly object clientLock = new object();
        private Thread worker;
        private volatile bool running;
        private int pendingCount;

        private struct ReceivedCommand
        {
            public string Name;
            public DateTimeOffset ReceivedAt;
        }

        private void OnEnable()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start(8);
                running = true;
                worker = new Thread(Listen) { IsBackground = true, Name = "Experiment1 TCP Receiver" };
                worker.Start();
                Debug.Log("[NotificationReceiver] Listening on TCP port " + port, this);
            }
            catch (SocketException exception)
            {
                running = false;
                listener?.Stop();
                Debug.LogError("[NotificationReceiver] Cannot listen: " + exception.Message, this);
            }
        }

        private void Listen()
        {
            while (running)
            {
                try
                {
                    using (var client = listener.AcceptTcpClient())
                    {
                        lock (clientLock)
                        {
                            if (!running) return;
                            activeClient = client;
                        }
                        client.NoDelay = true;
                        client.ReceiveTimeout = 3000;
                        client.SendTimeout = 3000;
                        ReadCommands(client.GetStream());
                    }
                }
                catch (IOException) { /* Disconnect or timeout: accept the next connection. */ }
                catch (SocketException exception)
                {
                    if (running) errors.Enqueue(exception.Message);
                }
                catch (ObjectDisposedException) { if (!running) return; }
                finally { lock (clientLock) activeClient = null; }
            }
        }

        private void ReadCommands(NetworkStream stream)
        {
            var line = new StringBuilder();
            while (running)
            {
                int value = stream.ReadByte();
                if (value < 0) return; // An unterminated line is never a command.
                if (value == '\n')
                {
                    DateTimeOffset receivedAt = DateTimeOffset.Now;
                    string name = line.ToString().TrimEnd('\r');
                    line.Clear();
                    if (!IsCommandName(name)) { Reply(stream, "ERROR INVALID_COMMAND"); continue; }
                    if (Interlocked.Increment(ref pendingCount) > MaxPendingCommands)
                    {
                        Interlocked.Decrement(ref pendingCount);
                        Reply(stream, "ERROR QUEUE_FULL");
                        continue;
                    }
                    commands.Enqueue(new ReceivedCommand { Name = name, ReceivedAt = receivedAt });
                    // This confirms transport queuing, not that an intervention was displayed.
                    Reply(stream, "QUEUED " + name);
                }
                else
                {
                    if (line.Length >= MaxLineBytes) { Reply(stream, "ERROR LINE_TOO_LONG"); return; }
                    line.Append((char)value);
                }
            }
        }

        private static bool IsCommandName(string name)
        {
            if (name.Length == 0 || name.Length > 64) return false;
            foreach (char character in name)
                if (!(character >= 'A' && character <= 'Z') && character != '_' &&
                    !(character >= '0' && character <= '9')) return false;
            return true;
        }

        private static void Reply(NetworkStream stream, string response)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(response + "\n");
            stream.Write(bytes, 0, bytes.Length);
        }

        private void Update()
        {
            for (int i = 0; i < 16 && commands.TryDequeue(out var command); i++)
            {
                Interlocked.Decrement(ref pendingCount);
                CommandReceived?.Invoke(command.Name, command.ReceivedAt);
            }
            while (errors.TryDequeue(out var error)) Debug.LogWarning("[NotificationReceiver] " + error, this);
        }

        private void OnDisable()
        {
            running = false;
            listener?.Stop();
            lock (clientLock) activeClient?.Close();
            if (worker != null && !worker.Join(1000))
                Debug.LogError("[NotificationReceiver] Receiver thread did not stop in time.", this);
            worker = null;
            listener = null;
            while (commands.TryDequeue(out _)) { }
            Interlocked.Exchange(ref pendingCount, 0);
            while (errors.TryDequeue(out _)) { }
        }
    }
}
