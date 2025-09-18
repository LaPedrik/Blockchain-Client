using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Blockchain.Services
{
    public class P2PNode
    {
        private TcpListener _listener;
        private List<TcpClient> _connectedNodes;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly int _port;
        private string NodeId { get; }
        public List<string> KnownNodes { get; }

        public P2PNode(int port)
        {
            _port = port;
            NodeId = Guid.NewGuid().ToString()[..8];
            _connectedNodes = new List<TcpClient>();
            _cancellationTokenSource = new CancellationTokenSource();
            KnownNodes = new List<string>();
        }

        /// <summary>
        /// Запускает узел по указанному порту
        /// </summary>
        public async Task StartNodeAsync()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();

                Utils.Utils.AddLog($"Нода {NodeId} запущена на порту {_port}");

                // Запускаем прослушивание входящих соединений
                _ = Task.Run(AcceptConnections, _cancellationTokenSource.Token);

                // Запускаем поддержание соединений
                _ = Task.Run(MaintainConnections, _cancellationTokenSource.Token);

            }
            catch (Exception ex)
            {
                Utils.Utils.AddLog($"❌ Ошибка запуска ноды: {ex.Message}");
            }
        }

        /// <summary>
        /// Принятие входящих соединений
        /// </summary>
        private async Task AcceptConnections()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(_cancellationTokenSource.Token);
                    Utils.Utils.AddLog($"🔗 Принято входящее соединение от: {client.Client.RemoteEndPoint}");

                    // Добавляем в список подключенных нод
                    _connectedNodes.Add(client);

                    // Обрабатываем в фоне
                    _ = Task.Run(() => HandleClientAsync(client, _cancellationTokenSource.Token));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Utils.Utils.AddLog($"❌ Ошибка AcceptTcpClient: {ex.Message}");
                    await Task.Delay(1000);
                }
            }
        }
        private bool IsAlreadyConnected(string host, int port)
        {
            return _connectedNodes.Any(client =>
                client.Connected &&
                client.Client.RemoteEndPoint is IPEndPoint endPoint &&
                endPoint.Address.ToString() == host &&
                endPoint.Port == port);
        }
        /// <summary>
        /// Обработка клиентского соединения
        /// </summary>
        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            string clientInfo = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    try
                    {
                        // Читаем сообщение с таймаутом
                        var readTask = reader.ReadLineAsync();
                        var timeoutTask = Task.Delay(5000, cancellationToken);

                        var completedTask = await Task.WhenAny(readTask, timeoutTask);

                        if (completedTask == timeoutTask)
                        {
                            // Таймаут - отправляем PING для проверки соединения
                            await writer.WriteLineAsync("PING");
                            continue;
                        }

                        var message = await readTask;
                        if (string.IsNullOrEmpty(message)) continue;

                        Utils.Utils.AddLog($"📨 Получено от {clientInfo}: {message}");

                        // Обрабатываем сообщение
                        var response = ProcessMessage(message);

                        if (!string.IsNullOrEmpty(response))
                        {
                            await writer.WriteLineAsync(response);
                            Utils.Utils.AddLog($"📤 Отправлено {clientInfo}: {response}");
                        }
                    }
                    catch (IOException ex) when (ex.InnerException is SocketException)
                    {
                        Utils.Utils.AddLog($"🔌 Соединение с {clientInfo} разорвано");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Utils.AddLog($"❌ Ошибка обработки {clientInfo}: {ex.Message}");
            }
            finally
            {
                // Удаляем из списка подключенных
                _connectedNodes.Remove(client);
                client.Close();
                Utils.Utils.AddLog($"🏁 Завершено соединение с {clientInfo}");
            }
        }

        /// <summary>
        /// Подключение к другой ноде
        /// </summary>
        public async Task<bool> ConnectToNodeAsync(string host, int port)
        {
            if (IsAlreadyConnected(host, port))
            {
                Utils.Utils.AddLog($"⚠️ Уже подключены к {host}:{port}");
                return true;
            }
            try
            {
                Utils.Utils.AddLog($"🔄 Подключение к {host}:{port}");

                var client = new TcpClient();
                await client.ConnectAsync(host, port);

                if (!client.Connected)
                {
                    Utils.Utils.AddLog($"❌ Не удалось подключиться к {host}:{port}");
                    return false;
                }

                // Добавляем в список подключенных
                _connectedNodes.Add(client);

                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                // Отправляем приветствие
                await writer.WriteLineAsync($"HELLO|{NodeId}|{_port}");
                Utils.Utils.AddLog($"✅ Подключились к {host}:{port} + отправили HELLO");

                // Запускаем обработчик для этого соединения
                _ = Task.Run(() => HandleClientAsync(client, _cancellationTokenSource.Token));

                return true;
            }
            catch (Exception ex)
            {
                Utils.Utils.AddLog($"❌ Ошибка подключения к {host}:{port}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Поддержание активных соединений
        /// </summary>
        private async Task MaintainConnections()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await PingAllNodes();
                    await Task.Delay(10000, _cancellationTokenSource.Token); // Каждые 10 секунд
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Utils.Utils.AddLog($"❌ Ошибка в MaintainConnections: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Пинг всех подключенных нод
        /// </summary>
        private async Task PingAllNodes()
        {
            foreach (var client in _connectedNodes.ToArray())
            {
                if (!client.Connected)
                {
                    _connectedNodes.Remove(client);
                    continue;
                }

                try
                {
                    using var stream = client.GetStream();
                    using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                    await writer.WriteLineAsync("PING");
                    Utils.Utils.AddLog($"📡 Отправлен PING к {client.Client.RemoteEndPoint}");
                }
                catch
                {
                    _connectedNodes.Remove(client);
                    client.Close();
                }
            }
        }

        /// <summary>
        /// Обработка входящих сообщений
        /// </summary>
        private string ProcessMessage(string message)
        {
            try
            {
                var parts = message.Split('|');
                var command = parts[0];

                switch (command)
                {
                    case "PING":
                        return "PONG";

                    case "PONG":
                        Utils.Utils.AddLog("💚 Получен PONG - соединение активно");
                        return null;

                    case "HELLO":
                        if (parts.Length >= 3)
                        {
                            var nodeId = parts[1];
                            var nodePort = parts[2];
                            KnownNodes.Add($"{nodeId}:{nodePort}");
                            return $"HELLO_ACK|{NodeId}|{_port}";
                        }
                        return "ERROR|Invalid HELLO format";

                    case "HELLO_ACK":
                        Utils.Utils.AddLog("🤝 Handshake completed");
                        return null;

                    default:
                        return $"ERROR|Unknown command: {command}";
                }
            }
            catch (Exception ex)
            {
                return $"ERROR|{ex.Message}";
            }
        }

        /// <summary>
        /// Остановка ноды
        /// </summary>
        public async Task StopNodeAsync()
        {
            try
            {
                _cancellationTokenSource.Cancel();

                // Закрываем все соединения
                foreach (var client in _connectedNodes.ToArray())
                {
                    client.Close();
                }
                _connectedNodes.Clear();

                _listener?.Stop();
                Utils.Utils.AddLog("🛑 Нода остановлена");
            }
            catch (Exception ex)
            {
                Utils.Utils.AddLog($"❌ Ошибка остановки ноды: {ex.Message}");
            }
        }
    }
}
