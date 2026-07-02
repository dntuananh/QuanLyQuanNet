# SessionRestore Mismatch Fix

## Problem
The `SessionRestore` protocol had a mismatch between what the server returned and what the client expected:
- **Server** (`NetworkServer.ProcessMessage`): Returned a serialized `Session` object containing `Id`, `UserId`, `ComputerId`, `StartTime`, `EndTime`, `Cost`
- **Client** (`WidgetForm.HandleServerMessage`): Expected a `SessionRecoveryData` object containing `TimeRemainingSeconds` and `Balance`

This caused deserialization failures when the client tried to restore a session after reconnection.

## Solution
Created a typed DTO contract shared between client and server to ensure compatibility.

### Changes Made

#### 1. **SharedModels/Models.cs**
Added a new shared DTO class:
```csharp
public class SessionRecoveryData
{
    public int TimeRemainingSeconds { get; set; }
    public decimal Balance { get; set; }
}
```
This ensures both client and server use the exact same structure for session recovery.

#### 2. **ServerAdmin/NetworkServer.cs**
Updated the `SessionRestore` message handler to:
- Parse flexible incoming payloads (supports both old and new formats)
- Extract `UserId`, `ComputerId`, and optionally `TimeRemainingSeconds`
- Validate user and computer ownership against database
- Return a properly typed `SessionRecoveryData` response containing:
  - `TimeRemainingSeconds`: The time remaining from the client's request
  - `Balance`: The current user's balance from the database

**Key improvement**: The server now returns a compact, strongly-typed response instead of a full `Session` object.

#### 3. **ClientApp/WidgetForm.cs**
Updated to:
- Removed duplicate `SessionRecoveryData` class definition (now uses the one from SharedModels)
- Updated `UpdateBalance()` method to accept `decimal` (matching the server's User.Balance type)
- Added `RestoreSessionAsync()` method to send session restore requests after reconnection
- Updated `HandleReconnectSuccess()` to automatically restore session when reconnection succeeds

## Protocol Contract

### Request (Client ? Server)
```json
{
  "Action": "SessionRestore",
  "Payload": "{\"UserId\": 1, \"ComputerId\": 5, \"TimeRemainingSeconds\": 3600}"
}
```

### Response (Server ? Client)
```json
{
  "Action": "SessionRestore",
  "Payload": "{\"TimeRemainingSeconds\": 3600, \"Balance\": 350000}"
}
```

## Benefits
1. **Type Safety**: Both sides now use the same strongly-typed DTO, eliminating deserialization mismatches
2. **Backward Compatibility**: Server parser is flexible and accepts multiple payload formats
3. **Clear Contract**: The `SessionRecoveryData` class explicitly defines the session recovery interface
4. **Maintainability**: Changes to the protocol are now centralized in `SharedModels/Models.cs`
5. **Automatic Recovery**: Client now automatically restores session state on successful reconnection

## Testing Recommendations
1. Test session restore after intentional server disconnection
2. Verify TimeRemainingSeconds and Balance are correctly restored
3. Test with various reconnection scenarios (network hiccup, process restart, etc.)
4. Verify old-format requests still work (backward compatibility)
