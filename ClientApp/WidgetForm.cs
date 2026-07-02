using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Text.Json;
using SharedModels.Models;

namespace ClientApp;

public partial class WidgetForm : Form
{
    // Bộ màu dark mode + neon cho widget nổi.
    private readonly Color _colorPanelBg = Color.FromArgb(228, 18, 22, 30);
    private readonly Color _colorText = Color.FromArgb(233, 239, 246);
    private readonly Color _colorNeon = Color.FromArgb(57, 255, 20);
    private readonly Color _colorNeonOrange = Color.FromArgb(255, 137, 41);
    private readonly Color _colorDanger = Color.FromArgb(220, 53, 69);

    private Label _lblUsername = null!;
    private Label _lblBalance = null!;
    private Label _lblTimeRemaining = null!;
    private Label _lblReconnectStatus = null!;
    private System.Windows.Forms.Timer _timerCountdown = null!;
    private NetworkClient _client;
    private User _currentUser;
    private bool _isDisconnected;

    // Dữ liệu hiển thị để đồng bộ với mã cũ.
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Username { get; set; } = "Người dùng";

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public long Balance { get; set; } = 250000;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int TimeRemainingSeconds { get; set; } = 3600; // Default 1 hour

    // Constructor mặc định.
    public WidgetForm()
    {
        InitializeComponent();
        InitializeForm();
        SetupUI();
        StartCountdown();
    }

    // Constructor giữ tương thích với luồng cũ trong Form1.
    public WidgetForm(object client, object user)
    {
        InitializeComponent();

        _client = client as NetworkClient;
        _currentUser = user as User;

        if (_client != null)
        {
            _client.OnDisconnected += HandleDisconnection;
            _client.OnReconnecting += HandleReconnecting;
            _client.OnReconnectSuccess += HandleReconnectSuccess;
            _client.OnMessageReceived += HandleServerMessage;
        }

        if (_currentUser != null)
        {
            Username = _currentUser.Username;
            Balance = (long)_currentUser.Balance;
        }

        InitializeForm();
        SetupUI();
        StartCountdown();
    }

    private void InitializeForm()
    {
        // Cấu hình form thu gọn, không viền, luôn nổi trên mọi cửa sổ.
        Text = "Client Widget";
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        Size = new Size(360, 240);
        BackColor = _colorPanelBg;
        StartPosition = FormStartPosition.Manual;
        Location = GetTopRightLocation();

        // Luôn bám góc trên bên phải kể cả khi thay đổi độ phân giải.
        Resize += (_, _) => Location = GetTopRightLocation();
        Shown += (_, _) => Location = GetTopRightLocation();

        FormClosing += (_, _) =>
        {
            _timerCountdown?.Stop();
            _timerCountdown?.Dispose();
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
        // Khối container bo tròn giả lập bằng border nhẹ và padding.
        var container = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            BackColor = _colorPanelBg,
        };

        // Tiêu đề người dùng đăng nhập.
        _lblUsername = new Label
        {
            Text = $"Nguoi choi: {Username}",
            Dock = DockStyle.Top,
            Height = 26,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = _colorText,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        // Label số dư tài khoản.
        _lblBalance = new Label
        {
            Text = $"So du: {Balance:N0} VND",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = _colorNeon,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        // Reconnection status label
        _lblReconnectStatus = new Label
        {
            Text = "",
            Dock = DockStyle.Top,
            Height = 20,
            Font = new Font("Segoe UI", 8, FontStyle.Regular),
            ForeColor = Color.Orange,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        // Đồng hồ đếm ngược HH:mm:ss đặt ở trung tâm để dễ nhìn khi chơi game.
        _lblTimeRemaining = new Label
        {
            Text = FormatTime(TimeRemainingSeconds),
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("Consolas", 22, FontStyle.Bold),
            ForeColor = _colorNeonOrange,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        // Dải nút icon chức năng: gọi món, nhắn tin, đổi mật khẩu.
        var actionStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0),
            BackColor = Color.Transparent,
        };

        var btnService = CreateIconButton("🍔 Goi mon", (_, _) => OpenServiceWindow());
        var btnChat = CreateIconButton("💬 Nhan tin", (_, _) => OpenChat());
        var btnChangePassword = CreateIconButton("🔐 Doi MK", (_, _) => ChangePassword());
        actionStrip.Controls.Add(btnService);
        actionStrip.Controls.Add(btnChat);
        actionStrip.Controls.Add(btnChangePassword);

        // Nút đăng xuất đỏ để cảnh báo hành động quan trọng.
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
        container.Controls.Add(_lblUsername);

        Controls.Clear();
        Controls.Add(container);
    }

    private Button CreateIconButton(string text, EventHandler onClick)
    {
        // Hàm tạo nhanh nút icon đồng nhất style để tái sử dụng.
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

    private void StartCountdown()
    {
        // Timer giảm thời gian còn lại theo từng giây và cập nhật label ngay lập tức.
        _timerCountdown = new System.Windows.Forms.Timer { Interval = 1000 };
        _timerCountdown.Tick += TimerCountdown_Tick;
        _timerCountdown.Start();
    }

    private void TimerCountdown_Tick(object? sender, EventArgs e)
    {
        // Don't decrement if reconnecting
        if (_isDisconnected)
            return;

        if (TimeRemainingSeconds > 0)
        {
            TimeRemainingSeconds--;
            _lblTimeRemaining.Text = FormatTime(TimeRemainingSeconds);
        }
        else
        {
            _timerCountdown.Stop();
            MessageBox.Show("Da het gio choi. Vui long nap them tien.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LogoutUser();
        }
    }

    private string FormatTime(int seconds)
    {
        TimeSpan ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    public void UpdateBalance(decimal newBalance)
    {
        Balance = (long)newBalance;
        _lblBalance.Text = $"So du: {Balance:N0} VND";
        _lblBalance.Refresh();
    }

    public void UpdateTimeRemaining(int seconds)
    {
        if (_lblTimeRemaining.InvokeRequired)
        {
            _lblTimeRemaining.Invoke(new Action(() => UpdateTimeRemaining(seconds)));
            return;
        }

        TimeRemainingSeconds = seconds;
        _lblTimeRemaining.Text = FormatTime(TimeRemainingSeconds);
        _lblTimeRemaining.Refresh();
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

        // Save current session state for recovery
        if (_currentUser != null)
        {
            SessionManager.SaveSession(_currentUser, TimeRemainingSeconds, Environment.MachineName);
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

    private async void RestoreSessionAsync()
    {
        if (_client == null || _currentUser == null)
            return;

        try
        {
            var restorePayload = new
            {
                UserId = _currentUser.Id,
                ComputerId = GetComputerIdFromEnvironment(), // Helper to get computer ID or 0 if unknown
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

    private void HandleServerMessage(NetworkMessage message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => HandleServerMessage(message)));
            return;
        }

        switch (message.Action)
        {
            case "SessionRestore":
                // Server confirmed session recovery
                var sessionData = JsonSerializer.Deserialize<SessionRecoveryData>(message.Payload);
                if (sessionData != null)
                {
                    TimeRemainingSeconds = sessionData.TimeRemainingSeconds;
                    UpdateBalance(sessionData.Balance);
                    _lblTimeRemaining.Text = FormatTime(TimeRemainingSeconds);
                }
                break;
            case "BalanceUpdate":
                if (long.TryParse(message.Payload, out long newBalance))
                {
                    UpdateBalance(newBalance);
                }
                break;
            case "TimeUpdate":
                if (int.TryParse(message.Payload, out int newTime))
                {
                    UpdateTimeRemaining(newTime);
                }
                break;
            case "OrderResponse":
            case "ChatResponse":
            case "ChangePasswordResponse":
            case "LogoutResponse":
                MessageBox.Show(message.Payload, "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;
            case "Error":
                MessageBox.Show(message.Payload, "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                break;
        }
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

    private async void OpenChat()
    {
        var message = InputBox.Show("Gui tin nhan toi may chu:", "Chat voi Admin", "");
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (_client == null)
        {
            MessageBox.Show("Chua ket noi den may chu.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        await _client.SendMessageAsync(new NetworkMessage
        {
            Action = "Chat",
            Payload = JsonSerializer.Serialize(new { UserId = _currentUser?.Id ?? 0, Message = message })
        });
        MessageBox.Show("Da gui tin nhan thanh cong.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void ChangePassword()
    {
        var newPassword = InputBox.Show("Nhap mat khau moi:", "Doi mat khau", string.Empty);
        if (string.IsNullOrWhiteSpace(newPassword) || _client == null)
        {
            return;
        }

        await _client.SendMessageAsync(new NetworkMessage
        {
            Action = "ChangePassword",
            Payload = JsonSerializer.Serialize(new { UserId = _currentUser?.Id ?? 0, NewPassword = newPassword })
        });
        MessageBox.Show("Yeu cau doi mat khau da duoc gui.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void LogoutUser()
    {
        _timerCountdown.Stop();

        if (_client != null)
        {
            await _client.SendMessageAsync(new NetworkMessage
            {
                Action = "Logout",
                Payload = JsonSerializer.Serialize(new { UserId = _currentUser?.Id ?? 0, ComputerId = 0 })
            });
        }

        SessionManager.ClearSession();
        Hide();
        Close();
    }

    public void SetQuickProducts(IEnumerable<(string Name, decimal Price)> products)
    {
        // Hook mở rộng: có thể dùng để nạp dữ liệu sản phẩm nhanh cho popup gọi món.
        _ = products;
    }
}
