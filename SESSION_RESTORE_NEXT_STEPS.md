# SessionRestore Fix - Next Steps & Recommendations

## What's Been Fixed ?
1. **Shared DTO**: `SessionRecoveryData` now in `SharedModels/Models.cs` (single source of truth)
2. **Server Handler**: `NetworkServer.SessionRestore` returns properly typed response
3. **Client Handler**: `WidgetForm` deserializes response correctly and updates UI
4. **Automatic Recovery**: Client now automatically restores session on reconnection success
5. **Type Safety**: Decimal `Balance` properly handled end-to-end

## Current Limitations & TODOs

### 1. ComputerId Not Persisted ??
**Issue**: `WidgetForm.GetComputerIdFromEnvironment()` returns 0 by default.
- Client cannot restore session if it doesn't know its ComputerId
- SessionRestore validation on server fails because computer not found

**Solution**: Store ComputerId when available:
```csharp
// In AuthContainerForm or LoginControl, after successful login:
public int? CurrentComputerId { get; set; }

// Pass to WidgetForm constructor
var widget = new WidgetForm(_client, user, currentComputerId);

// In WidgetForm, update GetComputerIdFromEnvironment():
private int GetComputerIdFromEnvironment()
{
    return _currentComputerId ?? 0;
}
```

### 2. Session Cost Calculation ??
**Issue**: No logic to calculate actual session cost or duration
- `Sessions` table cost field not updated
- No billing/deduction from balance

**TODO**: Implement session cost calculation:
```csharp
// Add to NetworkServer or new SessionManager service:
private decimal CalculateSessionCost(DateTime startTime, int? endTime = null)
{
    TimeSpan duration = endTime.HasValue 
        ? TimeSpan.FromSeconds(endTime.Value - startTime.Ticks)
        : DateTime.Now - startTime;

    decimal costPerHour = 50000; // VND/hour example
    return (decimal)duration.TotalHours * costPerHour;
}
```

### 3. Balance Deduction on Logout ??
**Issue**: When session ends, balance should be deducted but isn't

**TODO**: Add "Logout" message handler:
```csharp
case "Logout":
    // Deserialize to get ComputerId/UserId
    // Calculate session cost
    // Deduct from Users.Balance
    // Update Computers.Status to Available
    // Insert record to Sessions table
    // Return success response
```

### 4. Server-Side Session Tracking ??
**Issue**: Server doesn't actively track session state or time limits
- No session expiry enforcement
- No automatic idle timeout

**TODO**: Implement session tracking:
```csharp
// Add to NetworkServer:
private class ActiveSession
{
    public int UserId { get; set; }
    public int ComputerId { get; set; }
    public DateTime StartTime { get; set; }
    public decimal CostPerHour { get; set; }
}

private ConcurrentDictionary<int, ActiveSession> _activeSessions;

// Periodically check expiration and notify clients
private async Task MonitorSessionsAsync()
{
    while (_isRunning)
    {
        // Check for timeouts
        // Send "SessionExpired" or "BalanceWarning" messages
        await Task.Delay(TimeSpan.FromMinutes(1));
    }
}
```

### 5. Time Computation from Database ??
**Issue**: `TimeRemainingSeconds` is always from client, server never computes it
- On server restart, no way to recover exact remaining time
- Client's local time could be manipulated

**Solution**: Store initial session duration or compute from session records:
```csharp
// Option A: Store initial purchase duration in Sessions table
// Option B: Compute from balance + rate: RemainingSeconds = Balance / (CostPerSecond)
```

### 6. Testing & Validation ??
**TODO**: Add integration tests:
```
? Test normal session restore
? Test with invalid UserId
? Test with invalid ComputerId
? Test mismatch (user doesn't own computer session)
? Test TimeRemainingSeconds preservation
? Test Balance comes from current DB value
? Test backward compatibility with old payload format
```

## Recommended Implementation Order
1. **HIGH PRIORITY**: Store and pass ComputerId through login flow (blocks SessionRestore from working)
2. **HIGH PRIORITY**: Implement "Logout" handler with balance deduction
3. **MEDIUM PRIORITY**: Add server-side session monitoring/expiry
4. **MEDIUM PRIORITY**: Implement session cost calculation
5. **LOW PRIORITY**: Add comprehensive test coverage

## Files That Need Updates
- ? `ClientApp/AuthContainerForm.cs` - pass ComputerId to WidgetForm
- ? `ClientApp/LoginControl.cs` - extract ComputerId from server during login
- ? `ServerAdmin/NetworkServer.cs` - add "Logout" handler
- ? `ServerAdmin/NetworkServer.cs` - add session monitoring background task
- ? Tests (create new test project or add to existing)

## Quick Checklist for Next Session
```
[ ] Store ComputerId in WidgetForm
[ ] Implement Logout handler
[ ] Test SessionRestore with actual reconnection
[ ] Verify Balance is current from DB
[ ] Verify TimeRemainingSeconds is preserved
[ ] Document any new protocol messages
[ ] Update README with session lifecycle
```
