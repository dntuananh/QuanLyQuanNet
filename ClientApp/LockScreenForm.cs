using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClientApp;

public partial class LockScreenForm : Form
{
    // Tài khoản admin để thoát khỏi chế độ khóa khi cần bảo trì.
    private const string AdminUsername = "admin";
    private const string AdminPassword = "admin123";

    // Bảng tài khoản demo cho client để chạy UI độc lập server.
    private readonly Dictionary<string, string> _demoAccounts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["user1"] = "pass123",
        ["vip1"] = "vip123",
    };

    // Màu sắc chủ đạo phong cách gaming dark + neon.
    private readonly Color _colorDarkBase = Color.FromArgb(10, 13, 18);
    private readonly Color _colorOverlay = Color.FromArgb(165, 0, 0, 0);
    private readonly Color _colorPanel = Color.FromArgb(210, 20, 24, 30);
    private readonly Color _colorText = Color.FromArgb(225, 230, 238);
    private readonly Color _colorNeon = Color.FromArgb(57, 255, 20);
    private readonly Color _colorNeonOrange = Color.FromArgb(255, 137, 41);
    private readonly Color _colorError = Color.FromArgb(255, 107, 107);

    private Panel _overlayPanel = null!;
    private Panel _loginPanel = null!;
    private TextBox _txtUsername = null!;
    private TextBox _txtPassword = null!;
    private Label _lblError = null!;
    private Button _btnLogin = null!;

    private KeyboardBlocker? _keyboardBlocker;

    public LockScreenForm()
    {
        InitializeComponent();
        InitializeForm();
        BuildBackground();
        BuildOverlayAndLogin();
    }

    private void InitializeForm()
    {
        // Thiết lập form chạy toàn màn hình, không viền và luôn nằm trên cùng.
        Text = "Lock Screen - Quan Ly Quan Net";
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen?.Bounds ?? Bounds;
        BackColor = _colorDarkBase;
        KeyPreview = true;

        // Chặn Alt+F4 ở mức form để tránh đóng ứng dụng ngoài ý muốn.
        FormClosing += LockScreenForm_FormClosing;
        KeyDown += LockScreenForm_KeyDown;
        Resize += (_, _) => CenterLoginPanel();
        Shown += (_, _) => _keyboardBlocker = KeyboardBlocker.Start();
    }

    private void BuildBackground()
    {
        // Lớp nền chính để tạo cảm giác "gaming room".
        var backgroundPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _colorDarkBase,
        };

        // Banner giả lập game hot bên trái.
        var pubgBanner = CreateBanner("PUBG", "SURVIVAL MODE", _colorNeonOrange);
        pubgBanner.Location = new Point(40, 120);

        // Banner giả lập game hot bên phải.
        var lolBanner = CreateBanner("LEAGUE OF LEGENDS", "RANKED IS LIVE", _colorNeon);
        lolBanner.Location = new Point(Math.Max(Width - lolBanner.Width - 40, 40), 220);
        lolBanner.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        backgroundPanel.Controls.Add(pubgBanner);
        backgroundPanel.Controls.Add(lolBanner);
        Controls.Add(backgroundPanel);
    }

    private Panel CreateBanner(string title, string subtitle, Color accent)
    {
        // Card banner tái sử dụng cho vùng trống màn hình.
        var banner = new Panel
        {
            Size = new Size(320, 160),
            BackColor = Color.FromArgb(35, 42, 55),
            BorderStyle = BorderStyle.FixedSingle,
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = accent,
            Dock = DockStyle.Top,
            Height = 52,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var lblSub = new Label
        {
            Text = subtitle,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = _colorText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        banner.Controls.Add(lblSub);
        banner.Controls.Add(lblTitle);
        return banner;
    }

    private void BuildOverlayAndLogin()
    {
        // Lớp phủ mờ để tăng độ tương phản cho khối đăng nhập.
        _overlayPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _colorOverlay,
        };
        Controls.Add(_overlayPanel);

        // Khối đăng nhập ở chính giữa màn hình.
        _loginPanel = new Panel
        {
            Size = new Size(460, 340),
            BackColor = _colorPanel,
            Padding = new Padding(24),
        };

        var lblHeader = new Label
        {
            Text = "CLIENT LOGIN",
            Dock = DockStyle.Top,
            Height = 52,
            ForeColor = _colorNeon,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var lblMachine = new Label
        {
            Text = $"May: {Environment.MachineName}",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Color.FromArgb(185, 192, 204),
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        _txtUsername = CreateInput("Username");
        _txtPassword = CreateInput("Password", true);
        _btnLogin = CreatePrimaryButton();
        _lblError = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            ForeColor = _colorError,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        // Sử dụng panel stack để dễ bảo trì thứ tự control.
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(8, 8, 8, 0),
        };

        stack.Controls.Add(lblHeader);
        stack.Controls.Add(lblMachine);
        stack.Controls.Add(CreateGap(12));
        stack.Controls.Add(_txtUsername);
        stack.Controls.Add(CreateGap(10));
        stack.Controls.Add(_txtPassword);
        stack.Controls.Add(CreateGap(16));
        stack.Controls.Add(_btnLogin);
        stack.Controls.Add(CreateGap(8));
        stack.Controls.Add(_lblError);

        _loginPanel.Controls.Add(stack);
        _overlayPanel.Controls.Add(_loginPanel);
        CenterLoginPanel();
    }

    private TextBox CreateInput(string placeholder, bool isPassword = false)
    {
        // TextBox giao diện dark, đồng bộ cho cả username và password.
        var input = new TextBox
        {
            Width = 390,
            Height = 40,
            Font = new Font("Segoe UI", 11, FontStyle.Regular),
            BackColor = Color.FromArgb(30, 38, 50),
            ForeColor = _colorText,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = placeholder,
            UseSystemPasswordChar = isPassword,
            Margin = new Padding(0),
        };

        input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AttemptLogin();
            }
        };
        return input;
    }

    private Button CreatePrimaryButton()
    {
        // Nút đăng nhập nổi bật bằng màu neon để tạo điểm nhấn hành động chính.
        var button = new Button
        {
            Text = "DANG NHAP",
            Width = 390,
            Height = 44,
            BackColor = _colorNeon,
            ForeColor = Color.FromArgb(20, 24, 28),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0),
        };

        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => AttemptLogin();
        return button;
    }

    private static Control CreateGap(int height)
    {
        // Khoảng trống tái sử dụng để canh layout rõ ràng.
        return new Panel { Width = 390, Height = height, Margin = new Padding(0) };
    }

    private void CenterLoginPanel()
    {
        if (_overlayPanel.IsDisposed || _loginPanel.IsDisposed)
        {
            return;
        }

        // Căn giữa login panel theo kích thước client hiện tại.
        _loginPanel.Left = (ClientSize.Width - _loginPanel.Width) / 2;
        _loginPanel.Top = (ClientSize.Height - _loginPanel.Height) / 2;
    }

    private void AttemptLogin()
    {
        // Xóa thông báo cũ trước khi kiểm tra dữ liệu mới.
        _lblError.Text = string.Empty;
        var username = _txtUsername.Text.Trim();
        var password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _lblError.Text = "Vui long nhap day du username va password.";
            return;
        }

        // Đường lui cho admin: đăng nhập đúng sẽ thoát ứng dụng để truy cập OS.
        if (string.Equals(username, AdminUsername, StringComparison.OrdinalIgnoreCase) && password == AdminPassword)
        {
            _keyboardBlocker?.Dispose();
            _keyboardBlocker = null;
            Application.Exit();
            return;
        }

        if (!ValidateClientLogin(username, password))
        {
            _lblError.Text = "Tai khoan hoac mat khau khong dung.";
            _txtPassword.Clear();
            _txtPassword.Focus();
            return;
        }

        // Đăng nhập client thành công: ẩn lock screen và mở widget thu gọn.
        Hide();
        var widget = new WidgetForm
        {
            Username = username,
            Balance = 250_000,
            TimeRemainingSeconds = 5 * 60 * 60,
        };

        widget.FormClosed += (_, _) =>
        {
            // Khi widget đóng thì quay lại màn hình khóa để tiếp tục vòng đời kiosk.
            Show();
            Activate();
            _txtPassword.Clear();
            _txtUsername.Focus();
        };

        widget.Show();
    }

    private bool ValidateClientLogin(string username, string password)
    {
        // Hàm xác thực demo, có thể thay bằng gọi server thực tế ở bước tích hợp backend.
        return _demoAccounts.TryGetValue(username, out var expectedPassword) && expectedPassword == password;
    }

    private void LockScreenForm_KeyDown(object? sender, KeyEventArgs e)
    {
        // Dự phòng chặn thêm Alt+F4 trong trường hợp hook hệ thống chưa bắt kịp.
        if (e.Alt && e.KeyCode == Keys.F4)
        {
            e.SuppressKeyPress = true;
        }
    }

    private void LockScreenForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // Không cho đóng form trừ khi app đang thoát theo luồng admin.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            return;
        }

        _keyboardBlocker?.Dispose();
        _keyboardBlocker = null;
    }

    /// <summary>
    /// Keyboard hook chặn các tổ hợp phím phổ biến dùng để thoát kiosk.
    /// Lưu ý: không thể chặn tuyệt đối mọi phím hệ thống ở mọi phiên Windows.
    /// </summary>
    private sealed class KeyboardBlocker : IDisposable
    {
        private static readonly LowLevelKeyboardProc HookProcDelegate = HookProc;
        private static IntPtr _hookId = IntPtr.Zero;

        private KeyboardBlocker()
        {
        }

        public static KeyboardBlocker Start()
        {
            if (_hookId == IntPtr.Zero)
            {
                _hookId = SetWindowsHookEx(WhKeyboardLl, HookProcDelegate, GetModuleHandle(null), 0);
            }

            return new KeyboardBlocker();
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var key = Marshal.ReadInt32(lParam);
                var altPressed = (GetAsyncKeyState(VkMenu) & 0x8000) != 0;

                // Chặn Alt+Tab, Alt+Esc, Ctrl+Esc và phím Windows.
                if ((key == VkTab && altPressed)
                    || (key == VkEscape && altPressed)
                    || (key == VkEscape && (GetAsyncKeyState(VkControl) & 0x8000) != 0)
                    || key == VkLwin
                    || key == VkRwin)
                {
                    return (IntPtr)1;
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private const int WhKeyboardLl = 13;
        private const int VkTab = 0x09;
        private const int VkEscape = 0x1B;
        private const int VkControl = 0x11;
        private const int VkMenu = 0x12;
        private const int VkLwin = 0x5B;
        private const int VkRwin = 0x5C;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hmod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }
}
