# Auto-Reconnection & Session Recovery Implementation

## Overview
This implementation adds automatic reconnection and session recovery capabilities to the QuanLyQuanNet client application. When the network connection drops for 2-3 seconds, the client automatically attempts to reconnect without forcing the user to log in again.

## Components Implemented

### 1. **Enhanced NetworkClient (ClientApp\NetworkClient.cs)**
- **Auto-Reconnect Mechanism**: 
  - Detects disconnection automatically
  - Attempts up to 5 reconnection tries
  - 2.5-second delay between each attempt
  - Respects original IP and port from initial connection

- **New Properties**:
  - `IsConnected`: Read-only property to check connection status
  - `IsReconnecting`: Check if currently attempting to reconnect

- **New Events**:
  - `OnReconnecting`: Fired when reconnection starts
  - `OnReconnectSuccess`: Fired when reconnection succeeds
  - `OnReconnectAttempt(int)`: Fired before each reconnection attempt

- **Session Recovery Protocol**:
  - `SessionRestoreRequest`: New message type for session recovery
  - Seamlessly resume after reconnection

### 2. **SessionManager (ClientApp\SessionManager.cs)**
- **Local Session Storage**: Persists session data to disk when disconnection detected
  - Location: `%APPDATA%\QuanLyQuanNet\session.json`
  - Stores: User info, remaining time, balance, session start time

- **Session Validation**: Only restores sessions within 30 minutes of disconnect
- **Methods**:
  - `SaveSession()`: Persist session before reconnection attempt
  - `LoadSession()`: Retrieve saved session
  - `ClearSession()`: Delete session on logout

### 3. **Enhanced WidgetForm (ClientApp\WidgetForm.cs)**
- **Reconnection UI Feedback**:
  - New label showing reconnection status in orange
  - "Mất kết nối..." when disconnected
  - "Đang tái kết nối..." during attempts
  - "Tái kết nối thành công!" on success
  - Status auto-clears after 2 seconds

- **Gameplay Pause During Disconnect**:
  - Timer stops counting down when disconnected
  - Timestamp turns red during disconnect
  - Resumes automatically when reconnected

- **Session Recovery**:
  - Registers for all reconnection events
  - Handles `SessionRestore`, `BalanceUpdate`, `TimeUpdate` messages
  - Maintains user data and session state

- **Clean Resource Cleanup**:
  - Unsubscribes from all NetworkClient events on form close
  - Properly disposes timers

### 4. **SessionRecoveryData Class**
- Serializable data class for session recovery
- Contains: `TimeRemainingSeconds`, `Balance`

## How It Works

### Disconnection Scenario (2-3 second gap):

1. **Detection** → Network connection drops
2. **Auto-Reconnect** → Waits ~2.5 seconds, then attempts to reconnect
3. **Retry Loop** → Up to 5 attempts with 2.5-second intervals (max 12.5 seconds total)
4. **UI Feedback** → User sees "Đang tái kết nối..." status
5. **Success** → Connection restored → Events fired → UI updates
6. **Resume** → Timer resumes, gameplay continues seamlessly

### Longer Disconnection (>5 attempts):

1. **Max Retries** → After 5 failed attempts
2. **Fallback** → `OnDisconnected` event fires
3. **User Intervention** → Application shows "Mất kết nối với Server!" 
4. **Session Saved** → User session persisted locally for recovery

## Server-Side Integration Required

The server should handle these new message types:

```csharp
case "SessionRestore":
	// Extract user ID and computer name from SessionRestoreRequest
	// Verify session is valid
	// Send back SessionRestore with TimeRemainingSeconds and Balance
	break;
```

## Usage in DashboardForm / Other Client Forms

To integrate reconnection support into other forms:

```csharp
// In form constructor after creating NetworkClient
_client.OnDisconnected += () => UpdateStatus("Disconnected", Color.Red);
_client.OnReconnecting += () => UpdateStatus("Reconnecting...", Color.Yellow);
_client.OnReconnectSuccess += () => UpdateStatus("Connected", Color.Green);
_client.OnMessageReceived += HandleServerMessage;
```

## Testing Recommendations

1. **Normal Reconnect**: Simulate a 2-3 second network outage
   - Expected: Auto-reconnect, UI shows status, gameplay resumes

2. **Max Retries**: Simulate network outage >12 seconds
   - Expected: After 5 attempts, OnDisconnected fires

3. **Session Recovery**: Disconnect, let it attempt reconnect, then restore network
   - Expected: Session data preserved, user returns to game state

4. **Form Close During Reconnect**: Close WidgetForm while reconnecting
   - Expected: Clean resource cleanup, no exceptions

## Configuration Constants

In `NetworkClient.cs`:
- `MaxReconnectAttempts = 5` - Number of reconnection attempts
- `ReconnectDelayMs = 2500` - 2.5-second delay between attempts

Modify these if needed for your network environment.

## Backward Compatibility

✅ All changes are **fully backward-compatible**:
- Old clients can still connect to old servers
- New events are optional (can be ignored)
- Existing message protocol unchanged
- No breaking changes to existing APIs
