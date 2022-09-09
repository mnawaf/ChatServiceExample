using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetCoreServer;

namespace TcpChatServer
{
    class ChatSession : TcpSession
    {
        long timestamp=0;
        Boolean didWarned = false;
        public ChatSession(TcpServer server) : base(server) {  }

        protected override void OnConnected()
        {
            if (ChatServer.blockedClients.Contains(this.Id.ToString()))
            {
                Server.FindSession(Id).SendAsync("You have been blocked due to sending more than one message/sec");
                Server.FindSession(Id).Disconnect();
                return;
            }

            Console.WriteLine($"Chat TCP session with Id {Id} connected!");

            // Send invite message
            string message = "Hello from TCP chat! Please send a message or '!' to disconnect the client!";
            SendAsync(message);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat TCP session with Id {Id} disconnected!");
        }

        public static long GetTS() {
            return (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;;
        }
        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            long ts = GetTS();
            
            if (ts - timestamp < 1000)
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
                    string warning = "Warning: you are trying to send more than one message/sec";
                    Server.FindSession(Id).SendAsync(warning);
                }
            }
            
            timestamp = ts;
            
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            Console.WriteLine("Incoming: " + message+ " Time stamp (ms): " + GetTS());

            // Multicast message to all connected sessions
            Server.Multicast(message);

            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Disconnect();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat TCP session caught an error with code {error}");
        }
    }
    class ChatServer : TcpServer
    {
        public static List<string> blockedClients = new List<string>();

        public ChatServer(IPAddress address, int port) : base(address, port) {}

        protected override TcpSession CreateSession() {
            return new ChatSession(this);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat TCP server caught an error with code {error}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // TCP server port
            int port = 1111;
            if (args.Length > 0)
                port = int.Parse(args[0]);


            Console.WriteLine($"TCP server port: {port}");

            Console.WriteLine();

            // Create a new TCP chat server
            var server = new ChatServer(IPAddress.Any, port);

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
                    continue;
                }

                // Multicast admin message to all sessions
                line = "(admin) " + line;
                server.Multicast(line);
            }

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
