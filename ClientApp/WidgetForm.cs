using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

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
    private System.Windows.Forms.Timer _timerCountdown = null!;

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
            _timerCountdown.Stop();
            _timerCountdown.Dispose();
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

    public void UpdateBalance(long newBalance)
    {
        Balance = newBalance;
        _lblBalance.Text = $"So du: {Balance:N0} VND";
        _lblBalance.Refresh();
    }

    public void UpdateTimeRemaining(int seconds)
    {
        TimeRemainingSeconds = seconds;
        _lblTimeRemaining.Text = FormatTime(TimeRemainingSeconds);
        _lblTimeRemaining.Refresh();
    }

    private Point GetTopRightLocation()
    {
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        return new Point(workingArea.Right - Width - 16, workingArea.Top + 16);
    }

    private void OpenServiceWindow()
    {
        // Mở form gọi món ở dạng dialog để user thao tác rồi quay lại game.
        using var serviceWindow = new ServiceWindowForm();
        serviceWindow.ShowDialog(this);
    }

    private void OpenChat()
    {
        // Event gửi tin nhắn cơ bản, phần gửi server sẽ tích hợp sau.
        var message = InputBox.Show("Gui tin nhan toi may chu:", "Chat voi Admin", "");
        if (!string.IsNullOrWhiteSpace(message))
        {
            MessageBox.Show("Da gui tin nhan thanh cong.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ChangePassword()
    {
        // Event đổi mật khẩu cơ bản, chưa gọi API thật.
        var newPassword = InputBox.Show("Nhap mat khau moi:", "Doi mat khau", string.Empty);
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            MessageBox.Show("Yeu cau doi mat khau da duoc ghi nhan.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void LogoutUser()
    {
        // Đăng xuất sẽ đóng widget để quay về lock screen trong form cha.
        _timerCountdown.Stop();
        Hide();
        Close();
    }

    public void SetQuickProducts(IEnumerable<(string Name, decimal Price)> products)
    {
        // Hook mở rộng: có thể dùng để nạp dữ liệu sản phẩm nhanh cho popup gọi món.
        _ = products;
    }
}

