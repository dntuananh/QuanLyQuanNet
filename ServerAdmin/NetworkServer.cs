using System;
using System.Collections.Concurrent;
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
                        // Parse flexible restore payload to extract UserId, ComputerId, and optional time/balance
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

                            // Extract UserId and ComputerId from flexible payload
                            if (root.TryGetProperty("UserId", out var pUserId) && pUserId.TryGetInt32(out var uid))
                                reqUserId = uid;
                            if (root.TryGetProperty("ComputerId", out var pCompId) && pCompId.TryGetInt32(out var cid))
                                reqComputerId = cid;

                            // Extract TimeRemainingSeconds if present
                            if (root.TryGetProperty("TimeRemainingSeconds", out var pTime) && pTime.TryGetInt32(out var tsec))
                                timeRemainingSeconds = tsec;
                        }
                        catch (JsonException)
                        {
                            return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Invalid JSON session data" };
                        }

                        if (!reqUserId.HasValue || !reqComputerId.HasValue || reqUserId <= 0 || reqComputerId <= 0)
                        {
                            return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Invalid session data" };
                        }

                        using (var db = DatabaseHelper.GetConnection())
                        {
                            // Validate user exists
                            var user = db.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Id = @Id", new { Id = reqUserId.Value });
                            if (user == null)
                            {
                                return new NetworkMessage { Action = "SessionRestore", Payload = "Error: User not found" };
                            }

                            // Validate computer exists and user owns current session on it
                            var computer = db.QueryFirstOrDefault<Computer>(
                                "SELECT * FROM Computers WHERE Id = @ComputerId AND CurrentUserId = @UserId",
                                new { ComputerId = reqComputerId.Value, UserId = user.Id });

                            if (computer == null)
                            {
                                return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Invalid session" };
                            }

                            // Return typed recovery payload with current balance and preserved time
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
                    // Additional cases: Order, Chat, Logout, etc.

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

    public class NetworkMessage
    {
        public string? Action { get; set; }
        public string? Payload { get; set; }
    }

    public class LoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
