using Blockchain.Models;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using Blockchain.Models;

namespace Blockchain.Services
{
     public class PeerManager
     {
        public List<string> KnownPeers { get; private set; } = new List<string>();
        private TcpListener _listener;
        private bool _isRunning;
        private readonly Blockchain.Models.Blockchain _blockchain;

        public PeerManager(Blockchain.Models.Blockchain blockchain)
        {
            _blockchain = blockchain;          
        }

        public void StartServer(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _isRunning = true;

            Console.WriteLine($"Нода слушает на {GetLocalIpAddress()}...");

            while (_isRunning)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    Task.Run(() => HandlePeerConnection(client));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Server error: {ex.Message}");
                }
            }
        }
        public void HandlePeerConnection(TcpClient client)
        {
            string peerAddress = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            Console.WriteLine($"New connection from: {peerAddress}");
            if (!KnownPeers.Contains(peerAddress))
            {
                KnownPeers.Add(peerAddress);
            }
            try
            {
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream))
                using (var writer = new StreamWriter(stream))
                {
                    string? message;
                    while ((message = reader.ReadLine()) != null)
                    {
                        ProcessMessage(message, writer, peerAddress);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error with peer {peerAddress}: {ex.Message}");
                KnownPeers.Remove(peerAddress);
            }
        }
        private void ProcessMessage(string message, StreamWriter writer, string peerAddress)
        {
            try
            {
                dynamic? msg = JsonConvert.DeserializeObject(message);
                if (msg == null) return;

                string? type = msg.Type;
                if (type == null) return;

                switch (type)
                {
                    case "new_transaction":
                        var transaction = msg.Data.ToObject<Transaction>();
                        if (_blockchain.ValidateTransaction(transaction))
                        {
                            _blockchain.AddPendingTransaction(transaction);
                            //BroadcastMessage(message, peerAddress);
                        }
                        break;

                    case "new_block":
                        var block = msg.Data.ToObject<Block>();
                        if (_blockchain.ValidateNewBlock(block))
                        {
                            _blockchain.AddBlock(block);
                            //BroadcastMessage(message, peerAddress);
                        }
                        break;

                    case "request_peers":
                        writer.WriteLine(JsonConvert.SerializeObject(new
                        {
                            Type = "response_peers",
                            Peers = KnownPeers
                        }));
                        writer.Flush();
                        break;

                    case "response_peers":
                        var newPeers = msg.Peers.ToObject<List<string>>();
                        foreach (var peer in newPeers)
                        {
                            if (!KnownPeers.Contains(peer) && peer != GetLocalIpAddress())
                            {
                                KnownPeers.Add(peer);
                            }
                        }
                        break;

                    case "request_blockchain":
                        writer.WriteLine(JsonConvert.SerializeObject(new
                        {
                            Type = "response_blockchain",
                            Blockchain = _blockchain.Chain
                        }));
                        writer.Flush();
                        break;

                    case "response_blockchain":
                        var receivedChain = msg.Blockchain.ToObject<List<Block>>();
                        if (receivedChain.Count > _blockchain.Chain.Count && _blockchain.ValidateChain(receivedChain))
                        {
                            _blockchain.ReplaceChain(receivedChain);
                            Console.WriteLine("Blockchain synchronized with longer chain");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        private string GetLocalIpAddress()
        {
                 var host = Dns.GetHostEntry(Dns.GetHostName());
                 foreach (var ip in host.AddressList)
                 { 
                     if (ip.AddressFamily == AddressFamily.InterNetwork)
                     {
                         return ip.ToString();
                     }
                 }
            return "127.0.0.1";
            }
   }
}
