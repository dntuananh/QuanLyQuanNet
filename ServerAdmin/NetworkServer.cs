using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SharedModels.Models;
using Dapper;

namespace ServerAdmin
{
    public class NetworkServer
    {
        private TcpListener? _listener;
        private bool _isRunning;
        private ConcurrentDictionary<int, TcpClient> _connectedClients; // ComputerId -> TcpClient

        public event Action<string>? OnLogMessage;
        public event Action<int, string>? OnComputerStatusChanged;

        public NetworkServer()
        {
            _connectedClients = new ConcurrentDictionary<int, TcpClient>();
        }

        public void Start(int port = 5000)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _isRunning = true;
            OnLogMessage?.Invoke($"Server started on port {port}.");

            Task.Run(() => AcceptClientsAsync());
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            foreach (var client in _connectedClients.Values)
            {
                client.Close();
            }
            _connectedClients.Clear();
            OnLogMessage?.Invoke("Server stopped.");
        }

        private async Task AcceptClientsAsync()
        {
            while (_isRunning)
            {
                try
                {
                    if (_listener == null)
                    {
                        break;
                    }

                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    OnLogMessage?.Invoke($"Client connected: {client.Client.RemoteEndPoint}");
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (ObjectDisposedException) { /* Listener stopped */ }
                catch (Exception ex)
                {
                    OnLogMessage?.Invoke($"Error accepting client: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            int? currentComputerId = null;
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
            {
                try
                {
                    while (_isRunning && client.Connected)
                    {
                        string? line = await reader.ReadLineAsync();
                        if (line == null) break;

                        // Expected JSON format: { "Action": "...", "Payload": "..." }
                        var message = JsonSerializer.Deserialize<NetworkMessage>(line);
                        if (message != null)
                        {
                            var response = ProcessMessage(message, client, ref currentComputerId);
                            if (response != null)
                            {
                                string responseJson = JsonSerializer.Serialize(response);
                                await writer.WriteLineAsync(responseJson);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnLogMessage?.Invoke($"Client read error: {ex.Message}");
                }
            }

            // Client disconnected
            if (currentComputerId.HasValue)
            {
                _connectedClients.TryRemove(currentComputerId.Value, out _);
                OnLogMessage?.Invoke($"Computer {currentComputerId.Value} disconnected.");
                OnComputerStatusChanged?.Invoke(currentComputerId.Value, "Offline");
            }
        }

        private NetworkMessage ProcessMessage(NetworkMessage request, TcpClient client, ref int? computerId)
        {
            try
            {
                switch (request.Action ?? string.Empty)
                {
                    case "Identify":
                        // Payload is ComputerName
                        var compName = request.Payload;
                        if (string.IsNullOrWhiteSpace(compName))
                        {
                            return new NetworkMessage { Action = "IdentifyResponse", Payload = "Error: Missing computer name" };
                        }

                        using (var db = DatabaseHelper.GetConnection())
                        {
                            var compId = db.ExecuteScalar<int?>("SELECT Id FROM Computers WHERE Name = @Name", new { Name = compName });
                            if (compId.HasValue)
                            {
                                computerId = compId.Value;
                                _connectedClients[compId.Value] = client;
                                OnComputerStatusChanged?.Invoke(compId.Value, "Online");
                                return new NetworkMessage { Action = "IdentifyResponse", Payload = "Success" };
                            }
                        }
                        return new NetworkMessage { Action = "IdentifyResponse", Payload = "Error: Computer not found" };

                    case "Login":
                        // { "Username": "...", "Password": "..." }
                        if (string.IsNullOrWhiteSpace(request.Payload))
                        {
                            return new NetworkMessage { Action = "LoginResponse", Payload = "Error: Missing login payload" };
                        }

                        var loginData = JsonSerializer.Deserialize<LoginRequest>(request.Payload);
                        if (loginData == null || string.IsNullOrWhiteSpace(loginData.Username) || string.IsNullOrWhiteSpace(loginData.Password))
                        {
                            return new NetworkMessage { Action = "LoginResponse", Payload = "Error: Invalid login payload" };
                        }

                        using (var db = DatabaseHelper.GetConnection())
                        {
                            var user = db.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Username = @Username AND Password = @Password", loginData);
                            if (user != null)
                            {
                                if (user.Balance <= 0 && user.Role != "Admin")
                                {
                                    return new NetworkMessage { Action = "LoginResponse", Payload = "Error: Insufficient balance" };
                                }
                                
                                if (computerId.HasValue)
                                {
                                    db.Execute("UPDATE Computers SET Status = 'InUse', CurrentUserId = @UserId WHERE Id = @CompId", 
                                        new { UserId = user.Id, CompId = computerId.Value });
                                    OnComputerStatusChanged?.Invoke(computerId.Value, "InUse");
                                }
                                
                                return new NetworkMessage { Action = "LoginResponse", Payload = JsonSerializer.Serialize(user) };
                            }
                        }
                        return new NetworkMessage { Action = "LoginResponse", Payload = "Error: Invalid credentials" };
                    case "SessionRestore":
                        if (string.IsNullOrWhiteSpace(request.Payload))
                        {
                            return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Missing session data" };
                        }

                        int? reqUserId = null;
                        int? reqComputerId = null;
                        int timeRemainingSeconds = 0;

                        try
                        {
                            using var doc = JsonDocument.Parse(request.Payload);
                            var root = doc.RootElement;

                            if (root.TryGetProperty("UserId", out var pUserId) && pUserId.TryGetInt32(out var uid))
                                reqUserId = uid;
                            if (root.TryGetProperty("ComputerId", out var pCompId) && pCompId.TryGetInt32(out var cid))
                                reqComputerId = cid;
                            if (root.TryGetProperty("TimeRemainingSeconds", out var pTime) && pTime.TryGetInt32(out var tsec))
                                timeRemainingSeconds = tsec;
                        }
                        catch (JsonException)
                        {
                            return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Invalid JSON session data" };
                        }

                        var effectiveComputerId = reqComputerId.HasValue && reqComputerId.Value > 0
                            ? reqComputerId.Value
                            : computerId;

                        if (!reqUserId.HasValue || reqUserId <= 0 || !effectiveComputerId.HasValue || effectiveComputerId <= 0)
                        {
                            return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Invalid session data" };
                        }

                        using (var db = DatabaseHelper.GetConnection())
                        {
                            var user = db.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Id = @Id", new { Id = reqUserId.Value });
                            if (user == null)
                            {
                                return new NetworkMessage { Action = "SessionRestore", Payload = "Error: User not found" };
                            }

                            var computer = db.QueryFirstOrDefault<Computer>(
                                "SELECT * FROM Computers WHERE Id = @ComputerId AND CurrentUserId = @UserId",
                                new { ComputerId = effectiveComputerId.Value, UserId = user.Id });

                            if (computer == null)
                            {
                                return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Invalid session" };
                            }

                            var recovery = new SessionRecoveryData
                            {
                                TimeRemainingSeconds = timeRemainingSeconds,
                                Balance = user.Balance
                            };

                            return new NetworkMessage
                            {
                                Action = "SessionRestore",
                                Payload = JsonSerializer.Serialize(recovery)
                            };
                        }

                    case "Order":
                        if (string.IsNullOrWhiteSpace(request.Payload))
                        {
                            return new NetworkMessage { Action = "OrderResponse", Payload = "Error: Missing order data" };
                        }

                        try
                        {
                            using var doc = JsonDocument.Parse(request.Payload);
                            var root = doc.RootElement;
                            var userId = root.TryGetProperty("UserId", out var pUserId) && pUserId.TryGetInt32(out var uid) ? uid : 0;
                            var effectiveOrderComputerId = root.TryGetProperty("ComputerId", out var pCompId) && pCompId.TryGetInt32(out var cid) && cid > 0
                                ? cid
                                : (computerId ?? 0);

                            if (userId <= 0 || effectiveOrderComputerId <= 0)
                            {
                                return new NetworkMessage { Action = "OrderResponse", Payload = "Error: Invalid order data" };
                            }

                            if (!root.TryGetProperty("Items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
                            {
                                return new NetworkMessage { Action = "OrderResponse", Payload = "Error: Missing order items" };
                            }

                            using var db = DatabaseHelper.GetConnection();
                            foreach (var item in itemsElement.EnumerateArray())
                            {
                                var productId = item.TryGetProperty("ProductId", out var pProductId) && pProductId.TryGetInt32(out var pid) ? pid : 0;
                                var quantity = item.TryGetProperty("Quantity", out var pQty) && pQty.TryGetInt32(out var qty) ? qty : 0;
                                if (productId <= 0 || quantity <= 0)
                                {
                                    continue;
                                }

                                db.Execute(
                                    "INSERT INTO Orders (UserId, ComputerId, ProductId, Quantity, Status, Time) VALUES (@UserId, @ComputerId, @ProductId, @Quantity, 'Pending', @Time)",
                                    new { UserId = userId, ComputerId = effectiveOrderComputerId, ProductId = productId, Quantity = quantity, Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
                            }

                            return new NetworkMessage { Action = "OrderResponse", Payload = "Đơn hàng đã được ghi nhận." };
                        }
                        catch (JsonException)
                        {
                            return new NetworkMessage { Action = "OrderResponse", Payload = "Error: Invalid order payload" };
                        }

                    case "Chat":
                        if (string.IsNullOrWhiteSpace(request.Payload))
                        {
                            return new NetworkMessage { Action = "ChatResponse", Payload = "Error: Missing chat data" };
                        }

                        try
                        {
                            using var doc = JsonDocument.Parse(request.Payload);
                            var root = doc.RootElement;
                            var message = root.ValueKind == JsonValueKind.String
                                ? root.GetString() ?? string.Empty
                                : (root.TryGetProperty("Message", out var pMsg) ? pMsg.GetString() ?? string.Empty : request.Payload);

                            OnLogMessage?.Invoke($"Chat message from computer {computerId ?? 0}: {message}");
                            return new NetworkMessage { Action = "ChatResponse", Payload = "Tin nhắn đã được gửi tới máy chủ." };
                        }
                        catch (JsonException)
                        {
                            return new NetworkMessage { Action = "ChatResponse", Payload = "Error: Invalid chat payload" };
                        }

                    case "Logout":
                        if (computerId.HasValue)
                        {
                            using (var db = DatabaseHelper.GetConnection())
                            {
                                db.Execute("UPDATE Computers SET Status = 'Available', CurrentUserId = NULL WHERE Id = @ComputerId", new { ComputerId = computerId.Value });
                            }
                        }
                        return new NetworkMessage { Action = "LogoutResponse", Payload = "Đăng xuất thành công." };

                    case "ChangePassword":
                        if (string.IsNullOrWhiteSpace(request.Payload))
                        {
                            return new NetworkMessage { Action = "ChangePasswordResponse", Payload = "Error: Missing password data" };
                        }

                        try
                        {
                            using var doc = JsonDocument.Parse(request.Payload);
                            var root = doc.RootElement;
                            var userId = root.TryGetProperty("UserId", out var pUserId) && pUserId.TryGetInt32(out var uid) ? uid : 0;
                            var newPassword = root.TryGetProperty("NewPassword", out var pPassword) ? pPassword.GetString() ?? string.Empty : string.Empty;

                            if (userId <= 0 || string.IsNullOrWhiteSpace(newPassword))
                            {
                                return new NetworkMessage { Action = "ChangePasswordResponse", Payload = "Error: Invalid password data" };
                            }

                            using var db = DatabaseHelper.GetConnection();
                            var affected = db.Execute("UPDATE Users SET Password = @Password WHERE Id = @UserId", new { Password = newPassword, UserId = userId });
                            return new NetworkMessage { Action = "ChangePasswordResponse", Payload = affected > 0 ? "Đổi mật khẩu thành công." : "Error: User not found" };
                        }
                        catch (JsonException)
                        {
                            return new NetworkMessage { Action = "ChangePasswordResponse", Payload = "Error: Invalid password payload" };
                        }

                    default:
                        return new NetworkMessage { Action = "Error", Payload = "Unknown action" };
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Error processing message: {ex.Message}");
                return new NetworkMessage { Action = "Error", Payload = "Internal error" };
            }
        }

        // Method to send message from Server to a specific Client
        public async Task SendMessageToClient(int computerId, NetworkMessage message)
        {
            if (_connectedClients.TryGetValue(computerId, out var client) && client.Connected)
            {
                try
                {
                    string json = JsonSerializer.Serialize(message);
                    var stream = client.GetStream();
                    var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                    await writer.WriteLineAsync(json);
                }
                catch (Exception ex)
                {
                    OnLogMessage?.Invoke($"Error sending to computer {computerId}: {ex.Message}");
                }
            }
        }
    }
}
