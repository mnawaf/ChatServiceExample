using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetCoreServer;

namespace WsChatServer
{
    class ChatSession : WsSession
    {
        public ChatSession(WsServer server) : base(server) {}

        public override void OnWsConnected(HttpRequest request)
        {
            if (ChatServer.blockedClients.Contains(this.Id.ToString()))
            {
                Server.FindSession(Id).SendAsync("You have been blocked due to sending more than one message/sec");
                Server.FindSession(Id).Disconnect();
                return;
            }

            Console.WriteLine($"Chat WebSocket session with Id {Id} connected!");

            // Send invite message
            string message = "Hello from WebSocket chat! Please send a message or '!' to disconnect the client!";
            SendTextAsync(message);
        }

        public override void OnWsDisconnected()
        {
            Console.WriteLine($"Chat WebSocket session with Id {Id} disconnected!");
        }

        public override void OnWsReceived(byte[] buffer, long offset, long size)
        {

            long ts = GetTS();
            if (ts - timeStamp < 1000)
            {
                if (didWarned)
                {
                    Console.WriteLine($"Disconnecting {Id}  due to sending more than one message/sec");
                    Server.Multicast($"{Id} user has been kicked out due to sending more than one message/sec");
                    ChatServer.blockedClients.Add(Id.ToString());
                    Disconnect();
                    return;
                }
                else
                {
                    didWarned = true;
                    Console.WriteLine($"Warning: {Id} is trying to send more than one message/sec.");
                }
            }
            timeStamp = GetTS();

            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            Console.WriteLine("Incoming: " + message + " Time stamp (ms): " + GetTS());

            // Multicast message to all connected sessions
            ((WsServer)Server).MulticastText(message);

            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Close(1000);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat WebSocket session caught an error with code {error}");
        }
        public static long GetTS()
        {
            return (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds; ;
        }

        long timeStamp = 0;
        Boolean didWarned = false;


    }

    class ChatServer : WsServer
    {
        public static List<string> blockedClients = new List<string>();

        public ChatServer(IPAddress address, int port) : base(address, port) {}

        protected override TcpSession CreateSession() { return new ChatSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat WebSocket server caught an error with code {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // WebSocket server port
            int port = 8080;
            if (args.Length > 0)
                port = int.Parse(args[0]);
            // WebSocket server content path
            string www = "../../../../../www/ws";
            if (args.Length > 1)
                www = args[1];

            Console.WriteLine($"WebSocket server port: {port}");
            Console.WriteLine($"WebSocket server static content path: {www}");
            Console.WriteLine($"WebSocket server website: http://localhost:{port}/chat/index.html");

            Console.WriteLine();

            // Create a new WebSocket server
            var server = new ChatServer(IPAddress.Any, port);
            server.AddStaticContent(www, "/chat");

            // Start the server
            Console.Write("Server starting...");
            server.Start();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (;;)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Restart the server
                if (line == "!")
                {
                    Console.Write("Server restarting...");
                    server.Restart();
                    Console.WriteLine("Done!");
                }

                // Multicast admin message to all sessions
                line = "(admin) " + line;
                server.MulticastText(line);
            }

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
