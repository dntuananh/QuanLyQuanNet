using System;
using System.Drawing;
using System.Windows.Forms;

namespace ServerAdmin;

/// <summary>
/// Card hiển thị thông tin máy trạm với viền trái 5px theo trạng thái
/// Size: 220x120, nền trắng, padding 10px
/// </summary>
public class ComputerCard : Panel
{
    private ComputerModel _computer;
    private Label? _lblName;
    private Label? _lblStatus;
    private Label? _lblUser;
    private Label? _lblBalance;
    private Panel? _pnlLeftBorder;
    private ContextMenuStrip? _contextMenu;

    // Colors
    private readonly Color ColorCardBg = Color.White;      // #FFFFFF
    private readonly Color ColorTextDark = Color.FromArgb(51, 51, 51);
    private readonly Color ColorTextLight = Color.FromArgb(128, 128, 128);
    private readonly Color ColorAvailable = Color.FromArgb(39, 174, 96);   // #27AE60 - Green
    private readonly Color ColorInUse = Color.FromArgb(231, 76, 60);       // #E74C3C - Red
    private readonly Color ColorOffline = Color.FromArgb(158, 158, 158);   // #9E9E9E - Gray
    private readonly Color ColorMaintenance = Color.FromArgb(189, 189, 189); // #BDBDBD

    public ComputerCard(ComputerModel computer)
    {
        _computer = computer;
        this.Tag = computer;

        // Card: Nền trắng, kích thước sẽ được set từ Form1.cs
        this.BackColor = ColorCardBg;
        this.BorderStyle = BorderStyle.FixedSingle;
        this.Cursor = Cursors.Hand;
        this.Padding = new Padding(0);

        InitializeCard();
        CreateContextMenu();
        UpdateStatus(_computer);
    }

    private void InitializeCard()
    {
        // ===== VIỀN TRÁI 5PX (Panel nhỏ) - Đại diện Trạng Thái =====
        _pnlLeftBorder = new Panel
        {
            Dock = DockStyle.Left,
            Width = 5,
            BackColor = GetStatusColor()
        };
        this.Controls.Add(_pnlLeftBorder);

        // ===== CONTENT PANEL (Chứa labels, Padding = 10) =====
        Panel pnlContent = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorCardBg,
            Padding = new Padding(0)
        };

        // Computer Name (Bold, 10pt) - dùng Location tĩnh
        _lblName = new Label
        {
            Text = _computer.ComputerName ?? "Máy ?",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = ColorTextDark,
            AutoSize = true,
            Location = new Point(15, 10)
        };
        pnlContent.Controls.Add(_lblName);

        // Status Indicator (Bold 8pt) - dùng Location tĩnh
        _lblStatus = new Label
        {
            Text = GetStatusBadge(),
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = GetStatusColor(),
            AutoSize = true,
            Location = new Point(15, 35)
        };
        pnlContent.Controls.Add(_lblStatus);

        // User Info (8pt) - dùng Location tĩnh
        _lblUser = new Label
        {
            Text = string.IsNullOrEmpty(_computer.CurrentUser)
                ? "Trống"
                : $"👤 {_computer.CurrentUser.Substring(0, Math.Min(12, _computer.CurrentUser.Length))}",
            Font = new Font("Segoe UI", 8),
            ForeColor = ColorTextLight,
            AutoSize = true,
            Location = new Point(15, 55)
        };
        pnlContent.Controls.Add(_lblUser);

        // Balance Info (8pt) - dùng Location tĩnh
        string balanceText = _computer.Balance > 0
            ? $"💰 {FormatBalance()}"
            : "0đ";
        _lblBalance = new Label
        {
            Text = balanceText,
            Font = new Font("Segoe UI", 8),
            ForeColor = ColorTextLight,
            AutoSize = true,
            Location = new Point(15, 72)
        };
        pnlContent.Controls.Add(_lblBalance);

        this.Controls.Add(pnlContent);
    }

    private void CreateContextMenu()
    {
        _contextMenu = new ContextMenuStrip();

        var topUpMenu = new ToolStripMenuItem("💳 Nạp Tiền");
        topUpMenu.Click += (s, e) => MessageBox.Show($"Nạp tiền cho {_computer.ComputerName}", "Nạp Tiền");

        var messageMenu = new ToolStripMenuItem("💬 Nhắn Tin");
        messageMenu.Click += (s, e) => MessageBox.Show($"Gửi tin nhắn đến {_computer.ComputerName}", "Nhắn Tin");

        var lockMenu = new ToolStripMenuItem("🔒 Khóa Máy");
        lockMenu.Click += (s, e) => MessageBox.Show($"Khóa máy {_computer.ComputerName}", "Khóa Máy");

        var restartMenu = new ToolStripMenuItem("🔄 Khởi Động Lại");
        restartMenu.Click += (s, e) => MessageBox.Show($"Khởi động lại {_computer.ComputerName}", "Khởi Động Lại");

        _contextMenu.Items.Add(topUpMenu);
        _contextMenu.Items.Add(messageMenu);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(lockMenu);
        _contextMenu.Items.Add(restartMenu);

        this.ContextMenuStrip = _contextMenu;
    }

    private string GetStatusBadge()
    {
        return _computer.Status switch
        {
            ComputerStatus.Available => "✓ Trống",
            ComputerStatus.InUse => "● Đang dùng",
            ComputerStatus.Offline => "✕ Ngoại tuyến",
            ComputerStatus.Maintenance => "⚙ Bảo trì",
            _ => "?"
        };
    }

    private Color GetStatusColor()
    {
        return _computer.Status switch
        {
            ComputerStatus.Available => ColorAvailable,
            ComputerStatus.InUse => ColorInUse,
            ComputerStatus.Offline => ColorOffline,
            ComputerStatus.Maintenance => ColorMaintenance,
            _ => ColorTextLight
        };
    }

    private string FormatBalance()
    {
        if (_computer.Balance >= 1000000)
            return $"{_computer.Balance / 1000000.0:F1}M";
        if (_computer.Balance >= 1000)
            return $"{_computer.Balance / 1000.0:F0}K";
        return _computer.Balance.ToString();
    }

    public void UpdateStatus(ComputerModel computer)
    {
        _computer = computer;

        if (_lblName != null)
            _lblName.Text = _computer.ComputerName ?? "Máy ?";

        if (_lblStatus != null)
        {
            _lblStatus.Text = GetStatusBadge();
            _lblStatus.ForeColor = GetStatusColor();
        }

        if (_lblUser != null)
            _lblUser.Text = string.IsNullOrEmpty(_computer.CurrentUser)
                ? "Trống"
                : $"👤 {_computer.CurrentUser.Substring(0, Math.Min(12, _computer.CurrentUser.Length))}";

        if (_lblBalance != null)
            _lblBalance.Text = _computer.Balance > 0
                ? $"💰 {FormatBalance()}"
                : "0đ";

        // Update left border color
        if (_pnlLeftBorder != null)
            _pnlLeftBorder.BackColor = GetStatusColor();

        this.Refresh();
    }
}
