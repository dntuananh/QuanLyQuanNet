# SessionRestore Flow Diagram

## Before (Broken)
```
Client (WidgetForm)                    Server (NetworkServer)
       |                                      |
       |-- SessionRestore request ----------->|
       |  {UserId, ComputerId, ...}           |
       |                                      |
       |                    Validates & returns Session object
       |                    {Id, UserId, ComputerId, StartTime, EndTime, Cost}
       |<-- SessionRestore response -----------|
       |                                      |
       X HandleServerMessage fails!
         (Cannot deserialize Session as SessionRecoveryData)
```

## After (Fixed)
```
Client (WidgetForm)                    Server (NetworkServer)
       |                                      |
       |-- SessionRestore request ----------->|
       |  {UserId, ComputerId,                |
       |   TimeRemainingSeconds}              |
       |                                      |
       |                    Validates user & computer
       |                    Retrieves current balance from DB
       |                    Creates SessionRecoveryData response
       |                    {TimeRemainingSeconds, Balance}
       |<-- SessionRestore response -----------|
       |                                      |
       V HandleServerMessage succeeds!
       - TimeRemainingSeconds restored
       - Balance updated to current DB value
       - _lblTimeRemaining updated
       - _lblBalance updated
```

## Message Structure

### SharedModels/Models.cs (Shared Contract)
```csharp
public class SessionRecoveryData
{
    public int TimeRemainingSeconds { get; set; }
    public decimal Balance { get; set; }
}
```

### Client Behavior (ClientApp/WidgetForm.cs)
```
On Disconnection:
  ?? SaveSession() saves TimeRemainingSeconds locally

On Reconnect Success:
  ?? RestoreSessionAsync()
     ?? Sends SessionRestore request with current TimeRemainingSeconds
     ?? Receives SessionRecoveryData response
     ?? Updates UI with restored values
```

### Server Behavior (ServerAdmin/NetworkServer.cs)
```
On SessionRestore request:
  ?? Parse flexible JSON payload
  ?? Extract UserId, ComputerId, TimeRemainingSeconds (if present)
  ?? Validate user exists
  ?? Validate computer exists and user owns session
  ?? Read current balance from Users table
  ?? Return SessionRecoveryData
     ?? TimeRemainingSeconds (from client)
     ?? Balance (from DB, always current)
```

## Backward Compatibility
Server SessionRestore handler gracefully handles multiple payload formats:
- New format: `{UserId, ComputerId, TimeRemainingSeconds}`
- Old format: Attempts to deserialize as `Session` model as fallback
