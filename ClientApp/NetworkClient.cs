using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SharedModels.Models;

namespace ClientApp
{
    public class NetworkClient
    {
        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;
        private bool _isConnected;
        private bool _isReconnecting;
        private string _lastConnectedIp;
        private int _lastConnectedPort;
        private int _reconnectAttempts;
        private const int MaxReconnectAttempts = 5;
        private const int ReconnectDelayMs = 2500; // 2.5s

        public event Action<NetworkMessage> OnMessageReceived;
        public event Action OnDisconnected;
        public event Action OnReconnecting;
        public event Action OnReconnectSuccess;
        public event Action<int> OnReconnectAttempt; // Parameter: số lần thử

        public bool IsConnected => _isConnected;
        public bool IsReconnecting => _isReconnecting;

        public async Task<bool> ConnectAsync(string ipAddress, int port)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(ipAddress, port);

                var stream = _client.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                _isConnected = true;
                _isReconnecting = false;
                _lastConnectedIp = ipAddress;
                _lastConnectedPort = port;
                _reconnectAttempts = 0;

                _ = Task.Run(() => ReceiveMessagesAsync());
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _client?.Close();
        }

        public async Task SendMessageAsync(NetworkMessage message)
        {
            if (_isConnected && _writer != null)
            {
                try
                {
                    string json = JsonSerializer.Serialize(message);
                    await _writer.WriteLineAsync(json);
                }
                catch
                {
                    // Gửi thất bại, có thể do mất kết nối
                }
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            try
            {
                while (_isConnected && _client.Connected)
                {
                    string line = await _reader.ReadLineAsync();
                    if (line == null) break;

                    var message = JsonSerializer.Deserialize<NetworkMessage>(line);
                    if (message != null)
                    {
                        OnMessageReceived?.Invoke(message);
                    }
                }
            }
            catch
            {
                // Mất kất nối
            }
            finally
            {
                await HandleDisconnection();
            }
        }

        private async Task HandleDisconnection()
        {
            _isConnected = false;
            _client?.Close();

            // Thử kết nối lại, không thì call Disconnected event
            await AttemptReconnect();
            if (!_isConnected)
            {
                OnDisconnected?.Invoke();
            }
        }

        private async Task AttemptReconnect()
        {
            if (_isReconnecting)
                return;

            _isReconnecting = true;
            _reconnectAttempts = 0;

            while (_reconnectAttempts < MaxReconnectAttempts && !_isConnected)
            {
                _reconnectAttempts++;
                OnReconnectAttempt?.Invoke(_reconnectAttempts);
                OnReconnecting?.Invoke();

                await Task.Delay(ReconnectDelayMs);

                try
                {
                    _client = new TcpClient();
                    await _client.ConnectAsync(_lastConnectedIp, _lastConnectedPort);

                    var stream = _client.GetStream();
                    _reader = new StreamReader(stream, Encoding.UTF8);
                    _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                    _isConnected = true;
                    _isReconnecting = false;

                    OnReconnectSuccess?.Invoke();
                    _ = Task.Run(() => ReceiveMessagesAsync());
                    return;
                }
                catch
                {
                }
            }

            _isReconnecting = false;
        }
    }

    public class NetworkMessage
    {
        public string Action { get; set; }
        public string Payload { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class SessionRestoreRequest
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string ComputerName { get; set; }
    }
}
