using System.Net;
using System.Net.Sockets;

namespace LinuxServer
{
    public class Server
    {
        private readonly int _port = 10000;
        private readonly List<GameSession> _sessions = new List<GameSession>();
        private readonly object _lock = new object();

        public void Start()
        {
            var listener = new TcpListener(IPAddress.Any, _port);
            listener.Start();
            Console.WriteLine($"Server started at port {_port}.");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("New client connected.");
                HandleNewClient(client);
            }
        }

        private void HandleNewClient(TcpClient client)
        {
            lock (_lock)
            {
                GameSession waitingSession = _sessions.FirstOrDefault(s => !s.IsFull);
                if (waitingSession != null)
                {
                    waitingSession.AddClient(client);
                    Console.WriteLine("Client added as second client to an existing session.");
                    _ = waitingSession.StartSessionAsync();
                }
                else
                {
                    GameSession newSession = new GameSession(client);
                    _sessions.Add(newSession);
                    Console.WriteLine("Created new session, waiting for second client.");
                    newSession.DisconnectClients += DisconnectSession;
                    Task.Run(() => CheckSessionTimeout(newSession));
                }
            }
        }

        private async Task CheckSessionTimeout(GameSession session)
        {
            await Task.Delay(TimeSpan.FromMinutes(1));
            if (!session.IsFull)
            {
                Console.WriteLine("Session timed out waiting for a second client. Closing session.");
                await session.SendMessageAsync(session.Client1, "Timeout");
                DisconnectSession(session);
            }
        }

        private void DisconnectSession(GameSession session)
        {
            Console.WriteLine("Session removing...");
            session.Client1?.Close();
            session.Client2?.Close();
            session.Client1?.Dispose();
            session.Client2?.Dispose();
            lock (_lock)
            {
                _sessions.Remove(session);
            }
            Console.WriteLine("Session removed.");
        }

        static void Main()
        {
            new Server().Start();
        }
    }
}
