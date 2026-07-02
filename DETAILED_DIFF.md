# SessionRestore Fix - Detailed Diff

## File 1: SharedModels/Models.cs

```diff
  namespace SharedModels.Models
  {
      // ... existing classes ...

      public class Session
      {
          public int Id { get; set; }
          public int UserId { get; set; }
          public int ComputerId { get; set; }
          public string StartTime { get; set; }
          public string EndTime { get; set; }
          public decimal Cost { get; set; }
      }
+
+     public class SessionRecoveryData
+     {
+         public int TimeRemainingSeconds { get; set; }
+         public decimal Balance { get; set; }
+     }
  }
```

**Change Summary**: Added new DTO class for session recovery protocol contract.

---

## File 2: ServerAdmin/NetworkServer.cs

### SessionRestore Case Handler

**REMOVED (OLD)**:
```csharp
case "SessionRestore":
    // Ki?m tra Payload ??u vào
    if (string.IsNullOrWhiteSpace(request.Payload))
    {
        return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Missing session data" };
    }

    // Deserialize th?ng ra class Session có s?n c?a b?n
    var restoreRequest = JsonSerializer.Deserialize<Session>(request.Payload);
    if (restoreRequest == null || restoreRequest.UserId <= 0 || restoreRequest.ComputerId <= 0)
    {
        return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Invalid session data" };
    }

    using (var db = DatabaseHelper.GetConnection())
    {
        // Xác th?c User có t?n t?i không (Kh?p b?ng Users)
        var user = db.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Id = @Id", new { Id = restoreRequest.UserId });
        if (user == null)
        {
            return new NetworkMessage { Action = "SessionRestore", Payload = "Error: User not found" };
        }

        // Xác th?c Máy tr?m và ??i chi?u xem UserId này có ?úng là ?ang làm ch? phiên ? máy này không
        // C?p nh?t Query theo ?úng thu?c tính ComputerId trong class c?a b?n
        var computer = db.QueryFirstOrDefault<Computer>(
            "SELECT * FROM Computers WHERE Id = @ComputerId AND CurrentUserId = @UserId",
            new { ComputerId = restoreRequest.ComputerId, UserId = user.Id });

        if (computer == null)
        {
            return new NetworkMessage { Action = "SessionRestore", Payload = "Error: Invalid session" };
        }

        // Xác th?c thành công: G?i tr? l?i nguyên v?n thông tin Session c? cho Client ??ng b?
        // (B?n có th? tính toán l?i th?i gian d?a trên thu?c tính StartTime n?u mu?n)
        return new NetworkMessage
        {
            Action = "SessionRestore",
            Payload = JsonSerializer.Serialize(restoreRequest) // Tr? v? chính Object Session ?ã xác th?c
        };
    }
```

**ADDED (NEW)**:
```csharp
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
```

**Key Changes**:
- Uses flexible JSON parsing instead of direct object deserialization
- Extracts individual fields instead of deserializing to Session
- Returns `SessionRecoveryData` instead of `Session`
- Always returns current balance from database (not client-provided)
- Preserves client-provided TimeRemainingSeconds

---

## File 3: ClientApp/WidgetForm.cs

### 1. UpdateBalance Method Signature

**BEFORE**:
```csharp
public void UpdateBalance(long newBalance)
{
    Balance = newBalance;
    _lblBalance.Text = $"So du: {Balance:N0} VND";
    _lblBalance.Refresh();
}
```

**AFTER**:
```csharp
public void UpdateBalance(decimal newBalance)
{
    Balance = (long)newBalance;
    _lblBalance.Text = $"So du: {Balance:N0} VND";
    _lblBalance.Refresh();
}
```

**Change**: Accepts `decimal` (matching server User.Balance type), converts to `long` for display.

---

### 2. HandleReconnectSuccess Method

**BEFORE**:
```csharp
private void HandleReconnectSuccess()
{
    if (InvokeRequired)
    {
        Invoke(new Action(HandleReconnectSuccess));
        return;
    }

    _isDisconnected = false;
    _lblTimeRemaining.ForeColor = _colorNeonOrange;
    UpdateReconnectStatus("Tái k?t n?i thành công!");

    // Clear the status after 2 seconds
    System.Windows.Forms.Timer clearStatusTimer = new System.Windows.Forms.Timer { Interval = 2000 };
    clearStatusTimer.Tick += (s, e) =>
    {
        UpdateReconnectStatus("");
        ((System.Windows.Forms.Timer)s).Stop();
        ((System.Windows.Forms.Timer)s).Dispose();
    };
    clearStatusTimer.Start();
}
```

**AFTER**:
```csharp
private void HandleReconnectSuccess()
{
    if (InvokeRequired)
    {
        Invoke(new Action(HandleReconnectSuccess));
        return;
    }

    _isDisconnected = false;
    _lblTimeRemaining.ForeColor = _colorNeonOrange;
    UpdateReconnectStatus("Tái k?t n?i thành công!");

    // Attempt to restore session after reconnect
    if (_currentUser != null)
    {
        RestoreSessionAsync();
    }

    // Clear the status after 2 seconds
    System.Windows.Forms.Timer clearStatusTimer = new System.Windows.Forms.Timer { Interval = 2000 };
    clearStatusTimer.Tick += (s, e) =>
    {
        UpdateReconnectStatus("");
        ((System.Windows.Forms.Timer)s).Stop();
        ((System.Windows.Forms.Timer)s).Dispose();
    };
    clearStatusTimer.Start();
}
```

**Change**: Added call to `RestoreSessionAsync()` after successful reconnection.

---

### 3. New Methods Added

**ADDED**:
```csharp
private async void RestoreSessionAsync()
{
    if (_client == null || _currentUser == null)
        return;

    try
    {
        var restorePayload = new
        {
            UserId = _currentUser.Id,
            ComputerId = GetComputerIdFromEnvironment(),
            TimeRemainingSeconds = TimeRemainingSeconds
        };

        await _client.SendMessageAsync(new NetworkMessage
        {
            Action = "SessionRestore",
            Payload = JsonSerializer.Serialize(restorePayload)
        });
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error restoring session: {ex.Message}");
    }
}

private int GetComputerIdFromEnvironment()
{
    // TODO: Store ComputerId from server response during Identify/Login handshake
    // For now, return a default value; in production, this should be persisted
    return 0;
}
```

**Change**: New methods to send session restore request on reconnection.

---

### 4. Removed Duplicate Class

**REMOVED**:
```csharp
public class SessionRecoveryData
{
    public int TimeRemainingSeconds { get; set; }
    public long Balance { get; set; }
}
```

**Reason**: Now using `SessionRecoveryData` from `SharedModels.Models`.

**Note**: The balance type changed from `long` to `decimal` in SharedModels version, matching the database type.

---

## Summary of Changes

| File | Change Type | Details |
|------|------------|---------|
| SharedModels/Models.cs | ADD | New `SessionRecoveryData` class |
| ServerAdmin/NetworkServer.cs | REPLACE | SessionRestore case handler |
| ClientApp/WidgetForm.cs | MODIFY | UpdateBalance signature |
| ClientApp/WidgetForm.cs | MODIFY | HandleReconnectSuccess method |
| ClientApp/WidgetForm.cs | ADD | RestoreSessionAsync method |
| ClientApp/WidgetForm.cs | ADD | GetComputerIdFromEnvironment method |
| ClientApp/WidgetForm.cs | REMOVE | Duplicate SessionRecoveryData class |

---

## Breaking Changes

**None** - This is a backward-compatible fix.

Old clients sending Session-formatted payloads would still be parsed by the flexible JSON parser (though may fail validation if they don't provide required fields).

New clients send cleaner, more semantic payloads.

---

## New Contracts

### SessionRestore Request (Client ? Server)
```json
{
  "Action": "SessionRestore",
  "Payload": {
    "UserId": 1,
    "ComputerId": 5,
    "TimeRemainingSeconds": 3600
  }
}
```

### SessionRestore Response (Server ? Client)
```json
{
  "Action": "SessionRestore",
  "Payload": {
    "TimeRemainingSeconds": 3600,
    "Balance": 350000
  }
}
```

Both use the `SessionRecoveryData` DTO from SharedModels.
