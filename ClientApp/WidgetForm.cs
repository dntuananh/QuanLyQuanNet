using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Text.Json;
using SharedModels.Models;

namespace ClientApp;

public partial class WidgetForm : Form
{
    private readonly Color _colorPanelBg = Color.FromArgb(18, 22, 30);
    private readonly Color _colorText = Color.FromArgb(233, 239, 246);
    private readonly Color _colorNeon = Color.FromArgb(57, 255, 20);
    private readonly Color _colorNeonOrange = Color.FromArgb(255, 137, 41);
    private readonly Color _colorDanger = Color.FromArgb(220, 53, 69);

    private Label _lblUsername = null!;
    private Label _lblBalance = null!;
    private Label _lblTimeRemaining = null!;
    private Label _lblReconnectStatus = null!;
    private Label _lblComputer = null!;
    private NetworkClient _client;
    private User _currentUser;
    private int _computerId;
    private string _computerName = "";
    private bool _isDisconnected;
    private bool _isLoggingOut;

    private System.Windows.Forms.Timer _heartbeatTimer = null!;
    private System.Windows.Forms.Timer _displayTimer = null!;
    private const int HeartbeatIntervalMs = 15000;

    private double _serverRemainingSeconds;
    private DateTime _lastServerUpdateTime;
    private decimal _currentBalance;
    private ChatForm? _chatForm;

    private NotifyIcon _trayIcon = null!;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Username { get; set; } = "Người dùng";

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public long Balance { get; set; } = 250000;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int TimeRemainingSeconds { get; set; } = 3600;

    public WidgetForm()
    {
        InitializeComponent();
        InitializeForm();
        SetupUI();
        _serverRemainingSeconds = TimeRemainingSeconds;
        _lastServerUpdateTime = DateTime.Now;
        StartDisplayOnly();
    }

    public WidgetForm(NetworkClient client, User user)
    {
        InitializeComponent();
        _client = client;
        _currentUser = user;
        _computerId = 0;

        if (_currentUser != null)
        {
            Username = _currentUser.Username;
            Balance = (long)_currentUser.Balance;
            _currentBalance = _currentUser.Balance;
        }

        _client.OnDisconnected += HandleDisconnection;
        _client.OnReconnecting += HandleReconnecting;
        _client.OnReconnectSuccess += HandleReconnectSuccess;
        _client.OnMessageReceived += HandleServerMessage;

        InitializeForm();
        SetupUI();

        _serverRemainingSeconds = TimeRemainingSeconds;
        _lastServerUpdateTime = DateTime.Now;
        StartHeartbeat();
        StartDisplayOnly();
    }

    public void SetComputerId(int computerId, string computerName = "")
    {
        _computerId = computerId;
        _computerName = computerName;
        if (_lblComputer != null)
            _lblComputer.Text = string.IsNullOrEmpty(_computerName) ? $"Máy: {_computerId}" : _computerName;
    }

    private void InitializeForm()
    {
        Text = "Client Widget";
        FormBorderStyle = FormBorderStyle.Sizable;
        TopMost = false;
        ShowInTaskbar = true;
        Size = new Size(360, 280);
        MinimumSize = new Size(300, 240);
        StartPosition = FormStartPosition.Manual;
        Location = GetTopRightLocation();
        MaximizeBox = true;
        MinimizeBox = true;

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Client Widget",
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; BringToFront(); };

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Hien thi", null, (_, _) => { Show(); WindowState = FormWindowState.Normal; BringToFront(); });
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Thoat", null, (_, _) => Application.Exit());
        _trayIcon.ContextMenuStrip = trayMenu;

        _trayIcon.ShowBalloonTip(1000, "Client Widget", "Ung dung dang chay o khu vuc he thong.", ToolTipIcon.Info);

        FormClosing += (s, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing && !_isLoggingOut)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _chatForm?.Close();
            _chatForm?.Dispose();
            StopTimers();
            if (_client != null)
            {
                _client.OnDisconnected -= HandleDisconnection;
                _client.OnReconnecting -= HandleReconnecting;
                _client.OnReconnectSuccess -= HandleReconnectSuccess;
                _client.OnMessageReceived -= HandleServerMessage;
            }
        };
    }

    private void SetupUI()
    {
        var container = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            BackColor = _colorPanelBg,
        };

        _lblUsername = new Label
        {
            Text = $"Nguoi choi: {Username}",
            Dock = DockStyle.Top,
            Height = 26,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = _colorText,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = _colorPanelBg,
        };

        _lblComputer = new Label
        {
            Text = string.IsNullOrEmpty(_computerName) ? $"Máy: {_computerId}" : _computerName,
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = Color.FromArgb(160, 170, 185),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = _colorPanelBg,
        };

        _lblBalance = new Label
        {
            Text = $"So du: {Balance:N0} VND",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = _colorNeon,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = _colorPanelBg,
        };

        _lblReconnectStatus = new Label
        {
            Text = "",
            Dock = DockStyle.Top,
            Height = 20,
            Font = new Font("Segoe UI", 8, FontStyle.Regular),
            ForeColor = Color.Orange,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = _colorPanelBg,
        };

        _lblTimeRemaining = new Label
        {
            Text = FormatTime(TimeRemainingSeconds),
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("Consolas", 22, FontStyle.Bold),
            ForeColor = _colorNeonOrange,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = _colorPanelBg,
        };

        var actionStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0),
            BackColor = _colorPanelBg,
        };

        var btnService = CreateIconButton("🍔 Goi mon", (_, _) => OpenServiceWindow());
        var btnChat = CreateIconButton("💬 Nhan tin", (_, _) =>
        {
            if (_chatForm == null || _chatForm.IsDisposed)
            {
                _chatForm = new ChatForm(_client, _currentUser, _computerId);
                _chatForm.Show();
            }
            else
            {
                _chatForm.BringToFront();
            }
        });
        actionStrip.Controls.Add(btnService);
        actionStrip.Controls.Add(btnChat);

        var btnLogout = new Button
        {
            Text = "Dang xuat",
            Dock = DockStyle.Bottom,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = _colorDanger,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        btnLogout.FlatAppearance.BorderSize = 0;
        btnLogout.Click += (_, _) => LogoutUser();

        container.Controls.Add(btnLogout);
        container.Controls.Add(actionStrip);
        container.Controls.Add(_lblTimeRemaining);
        container.Controls.Add(_lblReconnectStatus);
        container.Controls.Add(_lblBalance);
        container.Controls.Add(_lblComputer);
        container.Controls.Add(_lblUsername);

        Controls.Clear();
        Controls.Add(container);
    }

    private Button CreateIconButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(106, 38),
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            BackColor = Color.FromArgb(34, 42, 56),
            ForeColor = _colorText,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0),
        };

        button.FlatAppearance.BorderColor = Color.FromArgb(54, 68, 88);
        button.FlatAppearance.BorderSize = 1;
        button.MouseEnter += (_, _) => button.BackColor = Color.FromArgb(44, 58, 78);
        button.MouseLeave += (_, _) => button.BackColor = Color.FromArgb(34, 42, 56);
        button.Click += onClick;
        return button;
    }

    private void StartHeartbeat()
    {
        _heartbeatTimer = new System.Windows.Forms.Timer { Interval = HeartbeatIntervalMs };
        _heartbeatTimer.Tick += HeartbeatTimer_Tick;
        _heartbeatTimer.Start();
    }

    private async void HeartbeatTimer_Tick(object? sender, EventArgs e)
    {
        if (_isDisconnected || _client == null || !_client.IsConnected)
            return;

        await _client.SendMessageAsync(new NetworkMessage
        {
            Action = "Heartbeat",
            Payload = JsonSerializer.Serialize(new HeartbeatPayload { ComputerId = _computerId })
        });
    }

    private void StartDisplayOnly()
    {
        _displayTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _displayTimer.Tick += DisplayTimer_Tick;
        _displayTimer.Start();
    }

    private void DisplayTimer_Tick(object? sender, EventArgs e)
    {
        if (_serverRemainingSeconds > 0)
        {
            double elapsed = (DateTime.Now - _lastServerUpdateTime).TotalSeconds;
            double displayed = _serverRemainingSeconds - elapsed;
            if (displayed < 0) displayed = 0;

            TimeRemainingSeconds = (int)displayed;
            _lblTimeRemaining.Text = FormatTime((int)displayed);
        }
        else
        {
            _lblTimeRemaining.Text = FormatTime(0);
        }
    }

    private string FormatTime(int seconds)
    {
        TimeSpan ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    public void UpdateBalance(decimal newBalance)
    {
        _currentBalance = newBalance;
        Balance = (long)newBalance;
        _lblBalance.Text = $"So du: {Balance:N0} VND";
    }

    public void UpdateTimeFromServer(double remainingSeconds, decimal balance)
    {
        if (_lblTimeRemaining.InvokeRequired)
        {
            _lblTimeRemaining.Invoke(new Action(() => UpdateTimeFromServer(remainingSeconds, balance)));
            return;
        }

        _serverRemainingSeconds = remainingSeconds;
        _lastServerUpdateTime = DateTime.Now;
        _currentBalance = balance;
        Balance = (long)balance;
        _lblBalance.Text = $"So du: {Balance:N0} VND";
        _lblTimeRemaining.Text = FormatTime((int)remainingSeconds);
    }

    private void HandleDisconnection()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(HandleDisconnection));
            return;
        }

        _isDisconnected = true;
        _lblTimeRemaining.ForeColor = Color.Red;
        UpdateReconnectStatus("Mất kết nối...");

        if (_currentUser != null)
        {
            SessionManager.SaveSessionIdentity(_currentUser, Environment.MachineName);
        }
    }

    private void HandleReconnecting()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(HandleReconnecting));
            return;
        }

        UpdateReconnectStatus("Đang tái kết nối...");
    }

    private void HandleReconnectSuccess()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(HandleReconnectSuccess));
            return;
        }

        _isDisconnected = false;
        _lblTimeRemaining.ForeColor = _colorNeonOrange;
        UpdateReconnectStatus("Tái kết nối thành công!");

        if (_currentUser != null && _computerId > 0)
        {
            RestoreSessionAsync();
        }

        System.Windows.Forms.Timer clearStatusTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        clearStatusTimer.Tick += (s, e) =>
        {
            UpdateReconnectStatus("");
            ((System.Windows.Forms.Timer)s).Stop();
            ((System.Windows.Forms.Timer)s).Dispose();
        };
        clearStatusTimer.Start();
    }

    private async void RestoreSessionAsync()
    {
        if (_client == null || _currentUser == null || _computerId <= 0)
            return;

        try
        {
            var restorePayload = new SessionRestoreRequest
            {
                UserId = _currentUser.Id,
                ComputerId = _computerId
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

    private void HandleServerMessage(NetworkMessage message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => HandleServerMessage(message)));
            return;
        }

        switch (message.Action)
        {
            case "HeartbeatResponse":
                if (message.Payload.StartsWith("Error"))
                {
                    _lblTimeRemaining.ForeColor = Color.Red;
                }
                else
                {
                    var hbResp = JsonSerializer.Deserialize<HeartbeatResponse>(message.Payload);
                    if (hbResp != null)
                    {
                        if (hbResp.TimeUp)
                        {
                            _serverRemainingSeconds = 0;
                            StopTimers();
                            MessageBox.Show("Da het gio choi. Vui long nap them tien.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            LogoutUser();
                            return;
                        }
                        UpdateTimeFromServer(hbResp.RemainingSeconds, hbResp.Balance);
                    }
                }
                break;

            case "SessionRestore":
                var restoreResp = JsonSerializer.Deserialize<SessionRestoreResponse>(message.Payload);
                if (restoreResp != null && restoreResp.SessionFound)
                {
                    UpdateTimeFromServer(restoreResp.RemainingSeconds, restoreResp.Balance);
                }
                else
                {
                    UpdateReconnectStatus("Phien lam viec khong duoc phuc hoi, dang xuat...");
                    LogoutUser();
                }
                break;

            case "TimeUp":
                StopTimers();
                MessageBox.Show("Da het gio choi. Vui long nap them tien.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LogoutUser();
                break;

            case "BalanceUpdate":
                if (long.TryParse(message.Payload, out long newBalance))
                {
                    UpdateBalance(newBalance);
                }
                break;

            case "TimeUpdate":
                if (double.TryParse(message.Payload, out double newTime))
                {
                    UpdateTimeFromServer(newTime, _currentBalance);
                }
                break;

            case "ChatMessage":
                try
                {
                    using var doc = JsonDocument.Parse(message.Payload);
                    var root = doc.RootElement;
                    var from = root.TryGetProperty("From", out var pFrom) ? pFrom.GetString() ?? "Admin" : "Admin";
                    var msg = root.TryGetProperty("Message", out var pMsg) ? pMsg.GetString() ?? "" : "";

                    if (_chatForm == null || _chatForm.IsDisposed)
                    {
                        _chatForm = new ChatForm(_client, _currentUser, _computerId);
                        _chatForm.Show();
                    }
                    _chatForm.AddMessage(from, msg);
                    _chatForm.BringToFront();
                }
                catch { }
                break;

            case "AddFundResponse":
                try
                {
                    using var doc = JsonDocument.Parse(message.Payload);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("Balance", out var balProp))
                    {
                        decimal updatedBal = balProp.GetDecimal();
                        UpdateBalance(updatedBal);
                    }
                    if (root.TryGetProperty("RemainingSeconds", out var remProp))
                    {
                        double updatedRem = remProp.GetDouble();
                        UpdateTimeFromServer(updatedRem, _currentBalance);
                    }
                }
                catch { }
                break;

            case "OrderResponse":
            case "LogoutResponse":
                MessageBox.Show(message.Payload, "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;

            case "Error":
                MessageBox.Show(message.Payload, "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                break;
        }
    }

    private void StopTimers()
    {
        _heartbeatTimer?.Stop();
        _heartbeatTimer?.Dispose();
        _displayTimer?.Stop();
        _displayTimer?.Dispose();
    }

    private void UpdateReconnectStatus(string text)
    {
        if (_lblReconnectStatus.InvokeRequired)
        {
            _lblReconnectStatus.Invoke(new Action(() => UpdateReconnectStatus(text)));
            return;
        }
        _lblReconnectStatus.Text = text;
    }

    private Point GetTopRightLocation()
    {
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        return new Point(workingArea.Right - Width - 16, workingArea.Top + 16);
    }

    private void OpenServiceWindow()
    {
        using var serviceWindow = new ServiceWindowForm(_client, _currentUser);
        serviceWindow.ShowDialog(this);
    }

    private async void LogoutUser()
    {
        _isLoggingOut = true;
        StopTimers();

        if (_client != null)
        {
            await _client.SendMessageAsync(new NetworkMessage
            {
                Action = "Logout",
                Payload = JsonSerializer.Serialize(new { UserId = _currentUser?.Id ?? 0, ComputerId = _computerId })
            });
        }

        SessionManager.ClearSession();
        Hide();
        Close();
    }

    public void SetQuickProducts(IEnumerable<(string Name, decimal Price)> products)
    {
        _ = products;
    }
}
