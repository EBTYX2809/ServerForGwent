using System.Net.Sockets;
using System.Text;

namespace LinuxServer
{
    public class GameSession
    {
        public TcpClient Client1 { get; }
        public TcpClient Client2 { get; private set; }
        private string Client1Name { get; set; }
        private string Client2Name { get; set; }
        private bool Client1RequestedToss { get; set; }
        private bool Client2RequestedToss { get; set; }
        private bool IsClient1Connected { get; set; }
        private bool IsClient2Connected { get; set; }
        public Action<GameSession> DisconnectClients;

        public GameSession(TcpClient client1)
        {
            Client1 = client1;
        }

        public bool IsFull => Client2 != null;
        public void AddClient(TcpClient client2) => Client2 = client2;

        public async Task StartSessionAsync()
        {
            var task1 = HandleClientMessagesAsync(Client1, Client2, isClient1: true);
            var task2 = HandleClientMessagesAsync(Client2, Client1, isClient1: false);
            await Task.WhenAll(task1, task2);
        }

        private async Task HandleClientMessagesAsync(TcpClient sender, TcpClient receiver, bool isClient1)
        {
            NetworkStream stream = sender.GetStream();
            byte[] buffer = new byte[1024];
            while (true)
            {
                int bytesRead;
                try
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("One of the clients disconnected.");

                        DisconnectClients?.Invoke(this);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while reading data: {ex.Message}");
                    DisconnectClients?.Invoke(this);
                    break;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                Console.WriteLine($"Message received: {message}");

                if (message.Contains("Toss coin"))
                {
                    if (isClient1)
                        Client1RequestedToss = true;
                    else
                        Client2RequestedToss = true;

                    if (Client1RequestedToss && Client2RequestedToss)
                    {
                        string tossResult = TossCoin();
                        if (tossResult == "player1")
                        {
                            await SendMessageAsync(Client1, "active");
                            await SendMessageAsync(Client2, "passive");
                        }
                        else if (tossResult == "player2")
                        {
                            await SendMessageAsync(Client1, "passive");
                            await SendMessageAsync(Client2, "active");
                        }
                        Client1RequestedToss = Client2RequestedToss = false;
                    }
                }               
                else if (message.Contains("Ready"))
                {
                    if (isClient1)
                    {
                        IsClient1Connected = true;
                        Client1Name = message.Replace("Ready|", "");
                    }
                    else
                    {
                        IsClient2Connected = true;
                        Client2Name = message.Replace("Ready|", "");
                    }

                    if (IsClient1Connected && IsClient2Connected)
                    {
                        await SendMessageAsync(Client1, "Ready|" + Client2Name);
                        await SendMessageAsync(Client2, "Ready|" + Client1Name);
                    }                    
                }
                else if (message.Contains("Disconnect"))
                {
                    TcpClient otherClient;
                    if (isClient1)
                    {
                        otherClient = Client2;
                        IsClient1Connected = false;
                    }
                    else
                    {
                        otherClient = Client1;
                        IsClient2Connected = false;
                    }

                    await SendMessageAsync(otherClient, message);

                    if (!IsClient1Connected && !IsClient2Connected)
                    {
                        DisconnectClients?.Invoke(this);
                        break;
                    }
                }

                else
                {
                    await SendMessageAsync(receiver, message);
                }
            }
        }

        public async Task SendMessageAsync(TcpClient client, string message)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] data = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(data, 0, data.Length);
                Console.WriteLine($"Message delivered: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erorr while delivering message: {ex.Message}");
            }
        }

        private string TossCoin()
        {
            Random rnd = new Random();
            return rnd.Next(2) == 0 ? "player1" : "player2";
        }
    }
}
