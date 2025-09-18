using Blockchain.Services;
using Blockchain.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Sockets;
using System.Xml.Linq;

var builder = WebApplication.CreateBuilder(args);

Console.Write("Выбери адрес ноды (Enter если не надо): ");
string IP = Console.ReadLine();
if(IP == "") IP = Utils.GetLocalIpAddress();

Console.Write("Выбери порт для запуска ноды: ");
int p2pPort = int.Parse(Console.ReadLine());

Console.Write("Выбери порт для HTTP API (по умолчанию 5000): ");
string httpPortInput = Console.ReadLine();
int httpPort = string.IsNullOrEmpty(httpPortInput) ? 5000 : int.Parse(httpPortInput);

builder.Services.AddSingleton<Blockchain.Models.Blockchain>();
builder.Services.AddSingleton<PeerManager>();
builder.Services.AddSingleton<P2PNode>(provider =>
{
    var port = p2pPort;
    return new P2PNode(port);
});
builder.Services.AddControllers();
builder.Services.AddLogging();
var app = builder.Build();

var p2pnode = app.Services.GetRequiredService<P2PNode>();

string p2pAddress = IP + ":" + p2pPort;

await Task.Run(p2pnode.StartNodeAsync);

await Task.Delay(2000);


Utils.AddLog("=== Node Addresses ===");
Utils.AddLog($"HTTP API (для кошелька): http://{IP}:{httpPort}");
Utils.AddLog($"P2P Server (для других нод): {p2pAddress}");
Utils.AddLog("======================");

app.MapControllers();
app.Run($"http://{IP}:{httpPort}");
