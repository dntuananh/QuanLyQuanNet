using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SharedModels.Models;
using Dapper;

namespace ServerAdmin
{
    public class NetworkServer
    {
        private TcpListener? _listener;
        private bool _isRunning;
        private ConcurrentDictionary<int, TcpClient> _connectedClients;
        private readonly ConcurrentDictionary<int, ActiveSession> _activeSessions = new();
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _clientWriteLocks = new();
        private System.Threading.Timer? _cleanupTimer;
        private System.Threading.Timer? _dbSyncTimer;
        private const int HeartbeatTimeoutSeconds = 45;
        private const int DbSyncIntervalSeconds = 60;
        private const int HourlyRate = 5000;

        public event Action<string>? OnLogMessage;
        public event Action<int, string>? OnComputerStatusChanged;
        public event Action<ChatMessagePayload>? OnChatMessageReceived;
        public event Action? OnOrderReceived;

        private class ActiveSession
        {
            public int ComputerId { get; set; }
            public int UserId { get; set; }
            public string Username { get; set; } = string.Empty;
            public decimal Balance { get; set; }
            public double RemainingSeconds { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime LastHeartbeat { get; set; }
            public bool Dirty { get; set; }
            public int SessionDbId { get; set; }
        }

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

            RecoverActiveSessions();
            StartBackgroundTimers();
            Task.Run(() => AcceptClientsAsync());
        }

        public void Stop()
        {
            _isRunning = false;
            _cleanupTimer?.Dispose();
            _dbSyncTimer?.Dispose();

            FlushAllSessionsToDb().Wait();

            _listener?.Stop();
            foreach (var client in _connectedClients.Values)
            {
                client.Close();
            }
            _connectedClients.Clear();
            OnLogMessage?.Invoke("Server stopped.");
        }

        private void StartBackgroundTimers()
        {
            _cleanupTimer = new System.Threading.Timer(_ =>
            {
                var now = DateTime.Now;
                foreach (var kvp in _activeSessions)
                {
                    if ((now - kvp.Value.LastHeartbeat).TotalSeconds > HeartbeatTimeoutSeconds)
                    {
                        LockSession(kvp.Value, "Timeout");
                        OnLogMessage?.Invoke($"Session timeout: Computer {kvp.Key}, User {kvp.Value.Username}");
                    }
                }
            }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));

            _dbSyncTimer = new System.Threading.Timer(_ =>
            {
                FlushDirtySessionsToDb().Wait();
            }, null, TimeSpan.FromSeconds(DbSyncIntervalSeconds), TimeSpan.FromSeconds(DbSyncIntervalSeconds));
        }

        private void RecoverActiveSessions()
        {
            try
            {
                using var db = DatabaseHelper.GetConnection();
                var activeComputers = db.Query<(int Id, int UserId)>(
                    "SELECT Id, CurrentUserId FROM Computers WHERE Status = 'InUse' AND CurrentUserId IS NOT NULL");

                foreach (var (compId, userId) in activeComputers)
                {
                    var session = db.QueryFirstOrDefault<dynamic>(
                        @"SELECT Id, UserId, ComputerId, StartTime, RemainingSecondsAtCheckpoint, LastCheckpointTime
                          FROM Sessions WHERE ComputerId = @CompId AND EndTime IS NULL
                          ORDER BY Id DESC LIMIT 1",
                        new { CompId = compId });

                    if (session != null)
                    {
                        var user = db.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Id = @Id", new { Id = userId });
                        if (user == null) continue;

                        double remainingAtCheckpoint = session.RemainingSecondsAtCheckpoint ?? 0;
                        DateTime checkpointTime = DateTime.MinValue;
                        if (session.LastCheckpointTime != null)
                        {
                            DateTime.TryParse(session.LastCheckpointTime, out checkpointTime);
                        }

                        if (checkpointTime > DateTime.MinValue)
                        {
                            double elapsedSinceCheckpoint = (DateTime.Now - checkpointTime).TotalSeconds;
                            remainingAtCheckpoint -= elapsedSinceCheckpoint;
                        }

                        if (remainingAtCheckpoint <= 0)
                        {
                            db.Execute("UPDATE Computers SET Status = 'Available', CurrentUserId = NULL WHERE Id = @Id",
                                new { Id = compId });
                            db.Execute("UPDATE Sessions SET EndTime = @Now WHERE Id = @Id",
                                new { Now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Id = session.Id });
                            OnLogMessage?.Invoke($"Recovered session {session.Id} had 0 time, ended.");
                            continue;
                        }

                        var asession = new ActiveSession
                        {
                            ComputerId = compId,
                            UserId = userId,
                            Username = user.Username,
                            Balance = user.Balance,
                            RemainingSeconds = remainingAtCheckpoint,
                            StartTime = DateTime.Parse(session.StartTime),
                            LastHeartbeat = DateTime.Now,
                            Dirty = false,
                            SessionDbId = session.Id
                        };
                        _activeSessions[compId] = asession;
                        OnLogMessage?.Invoke($"Recovered session: Computer {compId}, User {user.Username}, {remainingAtCheckpoint:F0}s remaining");
                    }
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Session recovery error: {ex.Message}");
            }
        }

        private async Task FlushDirtySessionsToDb()
        {
            try
            {
                using var db = DatabaseHelper.GetConnection();
                foreach (var kvp in _activeSessions)
                {
                    var s = kvp.Value;
                    if (!s.Dirty) continue;

                    decimal costSoFar = s.Balance;
                    db.Execute("UPDATE Users SET Balance = @Balance WHERE Id = @Id",
                        new { Balance = costSoFar, Id = s.UserId });

                    string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    db.Execute(
                        "UPDATE Sessions SET RemainingSecondsAtCheckpoint = @Rem, LastCheckpointTime = @Time WHERE Id = @Id",
                        new { Rem = s.RemainingSeconds, Time = nowStr, Id = s.SessionDbId });

                    s.Dirty = false;
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"DB sync error: {ex.Message}");
            }
        }

        public async Task FlushAllSessionsToDb()
        {
            foreach (var kvp in _activeSessions)
            {
                kvp.Value.Dirty = true;
            }
            await FlushDirtySessionsToDb();
        }

        private void LockSession(ActiveSession session, string reason)
        {
            try
            {
                using var db = DatabaseHelper.GetConnection();

                decimal elapsedHours = (decimal)(DateTime.Now - session.StartTime).TotalHours;
                decimal cost = elapsedHours * HourlyRate;
                if (cost > session.Balance) cost = session.Balance;
                decimal newBalance = session.Balance - cost;

                db.Execute("UPDATE Users SET Balance = @Balance WHERE Id = @Id",
                    new { Balance = newBalance, Id = session.UserId });
                db.Execute("UPDATE Computers SET Status = 'Available', CurrentUserId = NULL WHERE Id = @Id",
                    new { Id = session.ComputerId });
                db.Execute(
                    "UPDATE Sessions SET EndTime = @EndTime, Cost = @Cost, RemainingSecondsAtCheckpoint = @Rem, LastCheckpointTime = @Time WHERE Id = @Id",
                    new
                    {
                        EndTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Cost = cost,
                        Rem = 0,
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Id = session.SessionDbId
                    });

                _activeSessions.TryRemove(session.ComputerId, out _);
                OnLogMessage?.Invoke($"Session locked: Computer {session.ComputerId}, User {session.Username}, reason: {reason}");

                SendMessageToClient(session.ComputerId, new NetworkMessage
                {
                    Action = "TimeUp",
                    Payload = "Het gio choi. Vui long nap them tien."
                }).Wait();
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Error locking session: {ex.Message}");
            }
        }

        private async Task AcceptClientsAsync()
        {
            while (_isRunning)
            {
                try
                {
                    if (_listener == null) break;

                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    OnLogMessage?.Invoke($"Client connected: {client.Client.RemoteEndPoint}");
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (ObjectDisposedException) { }
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
            {
                try
                {
                    while (_isRunning && client.Connected)
                    {
                        string? line = await reader.ReadLineAsync();
                        if (line == null) break;

                        var message = JsonSerializer.Deserialize<NetworkMessage>(line);
                        if (message != null)
                        {
                            var response = ProcessMessage(message, client, ref currentComputerId);
                            if (response != null)
                            {
                                var sem = currentComputerId.HasValue
                                    ? _clientWriteLocks.GetOrAdd(currentComputerId.Value, _ => new SemaphoreSlim(1, 1))
                                    : null;
                                if (sem != null) await sem.WaitAsync();
                                try
                                {
                                    string responseJson = JsonSerializer.Serialize(response) + "\n";
                                    byte[] data = Encoding.UTF8.GetBytes(responseJson);
                                    await stream.WriteAsync(data, 0, data.Length);
                                }
                                finally
                                {
                                    sem?.Release();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnLogMessage?.Invoke($"Client read error: {ex.Message}");
                }
            }

            if (currentComputerId.HasValue)
            {
                _connectedClients.TryRemove(currentComputerId.Value, out _);
                if (_clientWriteLocks.TryRemove(currentComputerId.Value, out var writeSem))
                    writeSem.Dispose();

                if (_activeSessions.TryGetValue(currentComputerId.Value, out var session))
                {
                    if ((DateTime.Now - session.LastHeartbeat).TotalSeconds < HeartbeatTimeoutSeconds)
                    {
                        session.LastHeartbeat = DateTime.Now;
                        OnLogMessage?.Invoke($"Computer {currentComputerId.Value} disconnected, session kept alive for reconnect.");
                    }
                    else
                    {
                        LockSession(session, "Disconnect timeout");
                    }
                }

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
                        var compName = request.Payload;
                        if (string.IsNullOrWhiteSpace(compName))
                        {
                            return new NetworkMessage { Action = "IdentifyResponse", Payload = "Error: Missing computer name" };
                        }

                        using (var db = DatabaseHelper.GetConnection())
                        {
                            var compId = db.ExecuteScalar<int?>("SELECT Id FROM Computers WHERE Name = @Name", new { Name = compName });
                            if (!compId.HasValue)
                            {
                                db.Execute("INSERT INTO Computers (Name, Status) VALUES (@Name, 'Available')", new { Name = compName });
                                compId = db.ExecuteScalar<int>("SELECT last_insert_rowid()");
                            }
                            computerId = compId.Value;
                            _connectedClients[compId.Value] = client;
                            OnComputerStatusChanged?.Invoke(compId.Value, "Online");
                            return new NetworkMessage { Action = "IdentifyResponse", Payload = JsonSerializer.Serialize(new { ComputerId = compId.Value }) };
                        }

                    case "Login":
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

                                double remainingSeconds = (double)(user.Balance / HourlyRate * 3600);

                                if (computerId.HasValue)
                                {
                                    db.Execute("UPDATE Computers SET Status = 'InUse', CurrentUserId = @UserId WHERE Id = @CompId",
                                        new { UserId = user.Id, CompId = computerId.Value });
                                    OnComputerStatusChanged?.Invoke(computerId.Value, "InUse");

                                    string startTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                    db.Execute(
                                        "INSERT INTO Sessions (UserId, ComputerId, StartTime, RemainingSecondsAtCheckpoint, LastCheckpointTime) VALUES (@UserId, @ComputerId, @StartTime, @Rem, @Time)",
                                        new { UserId = user.Id, ComputerId = computerId.Value, StartTime = startTime, Rem = remainingSeconds, Time = startTime });

                                    int sessionId = db.ExecuteScalar<int>("SELECT last_insert_rowid()");

                                    _activeSessions[computerId.Value] = new ActiveSession
                                    {
                                        ComputerId = computerId.Value,
                                        UserId = user.Id,
                                        Username = user.Username,
                                        Balance = user.Balance,
                                        RemainingSeconds = remainingSeconds,
                                        StartTime = DateTime.Now,
                                        LastHeartbeat = DateTime.Now,
                                        Dirty = true,
                                        SessionDbId = sessionId
                                    };
                                }

                                var loginCompName = "";
                                if (computerId.HasValue)
                                {
                                    var comp = db.QueryFirstOrDefault<Computer>("SELECT * FROM Computers WHERE Id = @Id", new { Id = computerId.Value });
                                    loginCompName = comp?.Name ?? $"Máy {computerId.Value}";
                                }

                                var loginResp = new LoginResponse
                                {
                                    User = user,
                                    RemainingSeconds = remainingSeconds,
                                    Balance = user.Balance,
                                    ComputerName = loginCompName
                                };

                                return new NetworkMessage { Action = "LoginResponse", Payload = JsonSerializer.Serialize(loginResp) };
                            }
                        }
                        return new NetworkMessage { Action = "LoginResponse", Payload = "Error: Invalid credentials" };

                    case "Heartbeat":
                        if (!computerId.HasValue)
                        {
                            return new NetworkMessage { Action = "HeartbeatResponse", Payload = "Error: Not identified" };
                        }

                        if (!_activeSessions.TryGetValue(computerId.Value, out var hbSession))
                        {
                            return new NetworkMessage { Action = "HeartbeatResponse", Payload = "Error: No active session" };
                        }

                        var now = DateTime.Now;
                        double elapsed = (now - hbSession.LastHeartbeat).TotalSeconds;
                        if (elapsed < 0) elapsed = 0;

                        hbSession.RemainingSeconds -= elapsed;
                        hbSession.LastHeartbeat = now;
                        hbSession.Dirty = true;

                        if (hbSession.RemainingSeconds <= 0)
                        {
                            LockSession(hbSession, "Time ran out");
                            return new NetworkMessage
                            {
                                Action = "HeartbeatResponse",
                                Payload = JsonSerializer.Serialize(new HeartbeatResponse
                                {
                                    RemainingSeconds = 0,
                                    Balance = hbSession.Balance,
                                    TimeUp = true
                                })
                            };
                        }

                        return new NetworkMessage
                        {
                            Action = "HeartbeatResponse",
                            Payload = JsonSerializer.Serialize(new HeartbeatResponse
                            {
                                RemainingSeconds = hbSession.RemainingSeconds,
                                Balance = hbSession.Balance,
                                TimeUp = false
                            })
                        };

                    case "SessionRestore":
                        if (string.IsNullOrWhiteSpace(request.Payload))
                        {
                            return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Missing session data" };
                        }

                        int? reqUserId = null;
                        int? reqComputerId = null;

                        try
                        {
                            using var doc = JsonDocument.Parse(request.Payload);
                            var root = doc.RootElement;

                            if (root.TryGetProperty("UserId", out var pUserId) && pUserId.TryGetInt32(out var uid))
                                reqUserId = uid;
                            if (root.TryGetProperty("ComputerId", out var pCompId) && pCompId.TryGetInt32(out var cid))
                                reqComputerId = cid;
                        }
                        catch (JsonException)
                        {
                            return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Invalid JSON" };
                        }

                        var effectiveCompId = reqComputerId ?? computerId;

                        if (!reqUserId.HasValue || !effectiveCompId.HasValue)
                        {
                            return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Invalid session data" };
                        }

                        if (_activeSessions.TryGetValue(effectiveCompId.Value, out var existingSession) &&
                            existingSession.UserId == reqUserId.Value)
                        {
                            existingSession.LastHeartbeat = DateTime.Now;

                            return new NetworkMessage
                            {
                                Action = "SessionRestore",
                                Payload = JsonSerializer.Serialize(new SessionRestoreResponse
                                {
                                    RemainingSeconds = existingSession.RemainingSeconds,
                                    Balance = existingSession.Balance,
                                    SessionFound = true
                                })
                            };
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
                                new { ComputerId = effectiveCompId.Value, UserId = user.Id });

                            if (computer == null)
                            {
                                return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Session expired on server" };
                            }

                            var savedSession = db.QueryFirstOrDefault<dynamic>(
                                "SELECT Id, RemainingSecondsAtCheckpoint, LastCheckpointTime FROM Sessions WHERE ComputerId = @CompId AND UserId = @UserId AND EndTime IS NULL ORDER BY Id DESC LIMIT 1",
                                new { CompId = effectiveCompId.Value, UserId = user.Id });

                            double recoveredSeconds = 0;
                            if (savedSession != null)
                            {
                                recoveredSeconds = savedSession.RemainingSecondsAtCheckpoint ?? 0;
                                string? cpStr = savedSession.LastCheckpointTime;
                                if (cpStr != null && DateTime.TryParse(cpStr, out DateTime cpTime))
                                {
                                    recoveredSeconds -= (DateTime.Now - cpTime).TotalSeconds;
                                }
                                if (recoveredSeconds < 0) recoveredSeconds = 0;

                                var newSession = new ActiveSession
                                {
                                    ComputerId = effectiveCompId.Value,
                                    UserId = user.Id,
                                    Username = user.Username,
                                    Balance = user.Balance,
                                    RemainingSeconds = recoveredSeconds,
                                    StartTime = DateTime.Now,
                                    LastHeartbeat = DateTime.Now,
                                    Dirty = true,
                                    SessionDbId = savedSession.Id
                                };
                                _activeSessions[effectiveCompId.Value] = newSession;

                                db.Execute("UPDATE Computers SET Status = 'InUse' WHERE Id = @Id",
                                    new { Id = effectiveCompId.Value });
                            }

                            return new NetworkMessage
                            {
                                Action = "SessionRestore",
                                Payload = JsonSerializer.Serialize(new SessionRestoreResponse
                                {
                                    RemainingSeconds = recoveredSeconds,
                                    Balance = user.Balance,
                                    SessionFound = recoveredSeconds > 0
                                })
                            };
                        }

                    case "Order":
                        try
                        {
                            if (string.IsNullOrWhiteSpace(request.Payload))
                            {
                                return new NetworkMessage { Action = "OrderResponse", Payload = "Error: Missing order data" };
                            }

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

                            var notes = root.TryGetProperty("Notes", out var pNotes) ? pNotes.GetString() : null;
                            var paymentMethod = root.TryGetProperty("PaymentMethod", out var pPm) ? pPm.GetString() : "Account";

                            decimal totalCost = 0;
                            using var dbOrder = DatabaseHelper.GetConnection();
                            foreach (var item in itemsElement.EnumerateArray())
                            {
                                var productId = item.TryGetProperty("ProductId", out var pProductId) && pProductId.TryGetInt32(out var pid) ? pid : 0;
                                var quantity = item.TryGetProperty("Quantity", out var pQty) && pQty.TryGetInt32(out var qty) ? qty : 0;
                                if (productId <= 0 || quantity <= 0) continue;

                                var product = dbOrder.QueryFirstOrDefault<Product>("SELECT * FROM Products WHERE Id = @Id", new { Id = productId });
                                if (product != null) totalCost += product.Price * quantity;

                                dbOrder.Execute(
                                    "INSERT INTO Orders (UserId, ComputerId, ProductId, Quantity, Status, Time, Notes) VALUES (@UserId, @ComputerId, @ProductId, @Quantity, 'Pending', @Time, @Notes)",
                                    new { UserId = userId, ComputerId = effectiveOrderComputerId, ProductId = productId, Quantity = quantity, Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Notes = notes });
                            }

                            if (paymentMethod != "Cash" && totalCost > 0 && _activeSessions.TryGetValue(effectiveOrderComputerId, out var orderSession) && orderSession.UserId == userId)
                            {
                                orderSession.Balance -= totalCost;
                                orderSession.Dirty = true;
                            }

                            OnOrderReceived?.Invoke();
                            return new NetworkMessage { Action = "OrderResponse", Payload = "Đơn hàng đã được ghi nhận." };
                        }
                        catch (Exception ex)
                        {
                            OnLogMessage?.Invoke($"Order error: {ex.Message}");
                            return new NetworkMessage { Action = "OrderResponse", Payload = $"Error: {ex.Message}" };
                        }

                    case "Logout":
                        if (computerId.HasValue)
                        {
                            if (_activeSessions.TryRemove(computerId.Value, out var logoutSession))
                            {
                                decimal cost = 0;
                                using (var dbLogout = DatabaseHelper.GetConnection())
                                {
                                    decimal elapsedHours = (decimal)(DateTime.Now - logoutSession.StartTime).TotalHours;
                                    cost = elapsedHours * HourlyRate;
                                    if (cost > logoutSession.Balance) cost = logoutSession.Balance;
                                    decimal newBalance = logoutSession.Balance - cost;

                                    dbLogout.Execute("UPDATE Users SET Balance = @Balance WHERE Id = @Id",
                                        new { Balance = newBalance, Id = logoutSession.UserId });
                                    dbLogout.Execute("UPDATE Computers SET Status = 'Available', CurrentUserId = NULL WHERE Id = @ComputerId",
                                        new { ComputerId = computerId.Value });
                                    dbLogout.Execute(
                                        "UPDATE Sessions SET EndTime = @EndTime, Cost = @Cost, RemainingSecondsAtCheckpoint = @Rem, LastCheckpointTime = @Time WHERE Id = @Id",
                                        new
                                        {
                                            EndTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                            Cost = cost,
                                            Rem = 0,
                                            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                            Id = logoutSession.SessionDbId
                                        });
                                }
                                OnLogMessage?.Invoke($"User {logoutSession.Username} logged out from computer {computerId.Value}. Cost: {cost:N0} VND");
                                OnComputerStatusChanged?.Invoke(computerId.Value, "Available");
                            }
                            else
                            {
                                using (var dbLogout = DatabaseHelper.GetConnection())
                                {
                                    dbLogout.Execute("UPDATE Computers SET Status = 'Available', CurrentUserId = NULL WHERE Id = @ComputerId",
                                        new { ComputerId = computerId.Value });
                                }
                                OnComputerStatusChanged?.Invoke(computerId.Value, "Available");
                            }
                        }
                        return new NetworkMessage { Action = "LogoutResponse", Payload = "Đăng xuất thành công." };

                    case "ClientChat":
                        if (string.IsNullOrWhiteSpace(request.Payload) || !computerId.HasValue)
                        {
                            return new NetworkMessage { Action = "ClientChatResponse", Payload = "Error" };
                        }

                        try
                        {
                            var chatMsg = JsonSerializer.Deserialize<ChatMessagePayload>(request.Payload);
                            if (chatMsg == null || string.IsNullOrWhiteSpace(chatMsg.Message))
                            {
                                return new NetworkMessage { Action = "ClientChatResponse", Payload = "Error: Empty message" };
                            }

                            chatMsg.ComputerId = computerId.Value;

                            using (var dbChat = DatabaseHelper.GetConnection())
                            {
                                var comp = dbChat.QueryFirstOrDefault<Computer>("SELECT * FROM Computers WHERE Id = @Id", new { Id = computerId.Value });
                                chatMsg.ComputerName = comp?.Name ?? $"Máy {computerId.Value}";

                                var user = dbChat.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Id = @Id", new { Id = chatMsg.UserId });
                                chatMsg.Username = user?.Username ?? "Unknown";

                                dbChat.Execute(
                                    "INSERT INTO ChatMessages (ComputerId, UserId, Message, IsFromAdmin, Timestamp) VALUES (@ComputerId, @UserId, @Message, 0, @Timestamp)",
                                    new { ComputerId = computerId.Value, UserId = chatMsg.UserId, Message = chatMsg.Message, Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
                            }

                            OnChatMessageReceived?.Invoke(chatMsg);
                            return new NetworkMessage { Action = "ClientChatResponse", Payload = "OK" };
                        }
                        catch (JsonException)
                        {
                            return new NetworkMessage { Action = "ClientChatResponse", Payload = "Error: Invalid payload" };
                        }

                    case "AdminChatReply":
                        if (string.IsNullOrWhiteSpace(request.Payload))
                        {
                            return new NetworkMessage { Action = "AdminChatReplyResponse", Payload = "Error: Missing data" };
                        }

                        try
                        {
                            var reply = JsonSerializer.Deserialize<AdminChatReplyPayload>(request.Payload);
                            if (reply == null || string.IsNullOrWhiteSpace(reply.Message) || reply.ComputerId <= 0)
                            {
                                return new NetworkMessage { Action = "AdminChatReplyResponse", Payload = "Error: Invalid reply data" };
                            }

                            _ = SendAdminChatReply(reply.ComputerId, reply.Message);
                            return new NetworkMessage { Action = "AdminChatReplyResponse", Payload = "OK" };
                        }
                        catch (JsonException)
                        {
                            return new NetworkMessage { Action = "AdminChatReplyResponse", Payload = "Error: Invalid payload" };
                        }

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

                            using var dbCp = DatabaseHelper.GetConnection();
                            var affected = dbCp.Execute("UPDATE Users SET Password = @Password WHERE Id = @UserId",
                                new { Password = newPassword, UserId = userId });
                            return new NetworkMessage { Action = "ChangePasswordResponse", Payload = affected > 0 ? "Đổi mật khẩu thành công." : "Error: User not found" };
                        }
                        catch (JsonException)
                        {
                            return new NetworkMessage { Action = "ChangePasswordResponse", Payload = "Error: Invalid password payload" };
                        }

                    case "AddFund":
                        try
                        {
                            using var doc = JsonDocument.Parse(request.Payload);
                            var root = doc.RootElement;
                            var fundUserId = root.TryGetProperty("UserId", out var pFuId) && pFuId.TryGetInt32(out var fuid) ? fuid : 0;
                            var amount = root.TryGetProperty("Amount", out var pAmt) && pAmt.TryGetDecimal(out var amt) ? amt : 0;

                            if (fundUserId <= 0 || amount <= 0)
                            {
                                return new NetworkMessage { Action = "AddFundResponse", Payload = "Error: Invalid fund data" };
                            }

                            using var dbFund = DatabaseHelper.GetConnection();
                            dbFund.Execute("UPDATE Users SET Balance = Balance + @Amount WHERE Id = @Id",
                                new { Amount = amount, Id = fundUserId });

                            var updatedUser = dbFund.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Id = @Id",
                                new { Id = fundUserId });

                            if (updatedUser != null && _activeSessions.TryGetValue(computerId ?? 0, out var fundSession) &&
                                fundSession.UserId == fundUserId)
                            {
                                fundSession.Balance = updatedUser.Balance;
                                double additionalSeconds = (double)(amount / HourlyRate * 3600);
                                fundSession.RemainingSeconds += additionalSeconds;
                                fundSession.Dirty = true;

                                return new NetworkMessage
                                {
                                    Action = "AddFundResponse",
                                    Payload = JsonSerializer.Serialize(new { Balance = updatedUser.Balance, RemainingSeconds = fundSession.RemainingSeconds })
                                };
                            }

                            return new NetworkMessage { Action = "AddFundResponse", Payload = JsonSerializer.Serialize(new { Balance = updatedUser?.Balance ?? 0 }) };
                        }
                        catch (JsonException)
                        {
                            return new NetworkMessage { Action = "AddFundResponse", Payload = "Error: Invalid fund payload" };
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

        public async Task SendMessageToClient(int computerId, NetworkMessage message)
        {
            var sem = _clientWriteLocks.GetOrAdd(computerId, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync();
            try
            {
                if (_connectedClients.TryGetValue(computerId, out var client) && client.Connected)
                {
                    string json = JsonSerializer.Serialize(message) + "\n";
                    byte[] data = Encoding.UTF8.GetBytes(json);
                    var stream = client.GetStream();
                    await stream.WriteAsync(data, 0, data.Length);
                    await stream.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Error sending to computer {computerId}: {ex.Message}");
            }
            finally
            {
                sem.Release();
            }
        }

        public async Task BroadcastAnnouncement(string message, int durationSeconds)
        {
            var payload = JsonSerializer.Serialize(new AnnouncementPayload
            {
                Message = message,
                DurationSeconds = durationSeconds
            });

            foreach (var kvp in _connectedClients)
            {
                await SendMessageToClient(kvp.Key, new NetworkMessage
                {
                    Action = "Announcement",
                    Payload = payload
                });
            }

            OnLogMessage?.Invoke($"Announcement broadcast to {_connectedClients.Count} clients: {message}");
        }

        public async Task SendAdminChatReply(int computerId, string message)
        {
            string compName = $"Máy {computerId}";

            using (var db = DatabaseHelper.GetConnection())
            {
                var comp = db.QueryFirstOrDefault<Computer>("SELECT * FROM Computers WHERE Id = @Id", new { Id = computerId });
                compName = comp?.Name ?? compName;

                db.Execute(
                    "INSERT INTO ChatMessages (ComputerId, UserId, Message, IsFromAdmin, Timestamp) VALUES (@ComputerId, NULL, @Message, 1, @Timestamp)",
                    new { ComputerId = computerId, Message = message, Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
            }

            await SendMessageToClient(computerId, new NetworkMessage
            {
                Action = "ChatMessage",
                Payload = JsonSerializer.Serialize(new { From = "Admin", Message = message, ComputerName = compName })
            });

            OnLogMessage?.Invoke($"Admin replied to {compName}: {message}");
        }

        public void RefundUser(int userId, int computerId, decimal amount)
        {
            if (amount <= 0) return;

            using var db = DatabaseHelper.GetConnection();
            db.Execute("UPDATE Users SET Balance = Balance + @Amount WHERE Id = @Id",
                new { Amount = amount, Id = userId });

            if (_activeSessions.TryGetValue(computerId, out var session) && session.UserId == userId)
            {
                session.Balance += amount;
                session.Dirty = true;
            }

            OnLogMessage?.Invoke($"Refunded {amount:N0} VND to user {userId} (computer {computerId})");
        }

        public bool IsComputerConnected(int computerId)
        {
            return _connectedClients.ContainsKey(computerId);
        }

        public bool IsUserOnline(int userId)
        {
            var session = _activeSessions.Values.FirstOrDefault(s => s.UserId == userId);
            if (session == null) return false;
            return _connectedClients.ContainsKey(session.ComputerId);
        }

        public void AddUserFund(int userId, decimal amount)
        {
            using var db = DatabaseHelper.GetConnection();
            db.Execute("UPDATE Users SET Balance = Balance + @Amount WHERE Id = @Id",
                new { Amount = amount, Id = userId });

            var session = _activeSessions.Values.FirstOrDefault(s => s.UserId == userId);
            if (session == null) return;

            session.Balance += amount;
            double additionalSeconds = (double)(amount / HourlyRate * 3600);
            session.RemainingSeconds += additionalSeconds;
            session.Dirty = true;

            _ = SendMessageToClient(session.ComputerId, new NetworkMessage
            {
                Action = "AddFundResponse",
                Payload = JsonSerializer.Serialize(new
                {
                    balance = session.Balance,
                    remainingSeconds = session.RemainingSeconds
                })
            });
        }

        public void ShutdownComputer(int computerId)
        {
            _ = SendMessageToClient(computerId, new NetworkMessage
            {
                Action = "Shutdown",
                Payload = "Máy tính sẽ tắt sau 10 giây theo yêu cầu của quản trị viên."
            });
            OnLogMessage?.Invoke($"Sent shutdown command to computer {computerId}");
        }

        public void RestartComputer(int computerId)
        {
            _ = SendMessageToClient(computerId, new NetworkMessage
            {
                Action = "Restart",
                Payload = "Máy tính sẽ khởi động lại sau 10 giây theo yêu cầu của quản trị viên."
            });
            OnLogMessage?.Invoke($"Sent restart command to computer {computerId}");
        }

        public void AdminLockComputer(int computerId)
        {
            try
            {
                if (_activeSessions.TryRemove(computerId, out var session))
                {
                    using var db = DatabaseHelper.GetConnection();

                    decimal elapsedHours = (decimal)(DateTime.Now - session.StartTime).TotalHours;
                    decimal cost = elapsedHours * HourlyRate;
                    if (cost > session.Balance) cost = session.Balance;
                    decimal newBalance = session.Balance - cost;

                    db.Execute("UPDATE Users SET Balance = @Balance WHERE Id = @Id",
                        new { Balance = newBalance, Id = session.UserId });
                    db.Execute("UPDATE Computers SET Status = 'Maintenance', CurrentUserId = NULL WHERE Id = @Id",
                        new { Id = computerId });
                    db.Execute(
                        "UPDATE Sessions SET EndTime = @EndTime, Cost = @Cost, RemainingSecondsAtCheckpoint = @Rem, LastCheckpointTime = @Time WHERE Id = @Id",
                        new
                        {
                            EndTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            Cost = cost,
                            Rem = 0,
                            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            Id = session.SessionDbId
                        });

                    OnLogMessage?.Invoke($"Admin locked computer {computerId}, user {session.Username} logged out. Cost: {cost:N0} VND");
                }
                else
                {
                    using var db = DatabaseHelper.GetConnection();
                    db.Execute("UPDATE Computers SET Status = 'Maintenance', CurrentUserId = NULL WHERE Id = @Id",
                        new { Id = computerId });
                    OnLogMessage?.Invoke($"Admin locked computer {computerId} (idle)");
                }

                _ = SendMessageToClient(computerId, new NetworkMessage
                {
                    Action = "AdminLock",
                    Payload = "Máy của bạn đã bị khóa bởi quản trị viên."
                });

                OnComputerStatusChanged?.Invoke(computerId, "Maintenance");
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Error locking computer {computerId}: {ex.Message}");
            }
        }
    }
}
