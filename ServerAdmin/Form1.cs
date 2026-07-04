using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Dapper;
using SharedModels.Models;

namespace ServerAdmin;

public partial class Form1 : Form
{
    private NetworkServer? _server;
    private DateTime _lastRefreshTime = DateTime.MinValue;
    private const double RefreshCooldownSeconds = 2;

    // Sidebar Buttons
    private Button? btnSettings;
    private Button? btnComputerMgmt;
    private Button? btnAccounts;
    private Button? btnServices;
    private Button? btnChat;
    private Button? btnChatCustomer;
    private Button? btnChatAnnouncement;

    // Layout roots
    private TableLayoutPanel? masterLayout;
    private Panel? sidebarPanel;
    private Panel? rightContentHost;
    private Panel? pnlQuanLyMay;
    private Panel? pnlTaiKhoan;
    private Panel? pnlDichVu;
    private Panel? pnlChat;
    private Panel? pnlChatCustomer;
    private Panel? pnlChatAnnouncement;
    private Panel? headerPanel;
    private Panel? footerPanel;
    private TableLayoutPanel? cardTableLayout;
    private Label? lblTitle;
    private Label? lblFooterStats;

    // Account management controls
    private DataGridView? dgvAccounts;
    private Button? btnCreateAccount;
    private Button? btnDeleteAccount;
    private Button? btnAddFund;
    private Label? lblAccountFooter;
    private int _roleFilterState; // 0 = all, 1 = admin, 2 = client
    private string _searchFilter = "";
    private const int HourlyRate = 5000; // VND per hour (make this customizable in the future)

    // Chat controls
    private ListBox? _chatMessageList;
    private TextBox? _chatReplyInput;
    private Button? _chatSendBtn;
    private Label? _chatTargetLabel;
    private int _chatTargetComputerId;
    private string _chatTargetComputerName = "";

    private class ChatEntry
    {
        public int ComputerId { get; set; }
        public string ComputerName { get; set; } = "";
        public string DisplayText { get; set; } = "";
        public override string ToString() => DisplayText;
    }

    // Order panel controls
    private DataGridView? dgvOrders;
    private Button? btnFilterAll;
    private Button? btnFilterPending;
    private Button? btnFilterProcessing;
    private Button? btnFilterCompleted;
    private Button? btnAddComputer;
    private Button? btnRemoveComputer;
    private CheckBox? chkAutoPlayAlarm;
    private string _orderFilter = "";
    private System.Windows.Forms.Timer? _orderTimer;
    private System.Windows.Forms.Timer? _computerTimer;
    private System.Windows.Forms.Timer? _alertTimer;
    private int _lastPendingCount = 0;

    private class OrderGroupTag
    {
        public int ComputerId { get; set; }
        public string Time { get; set; } = "";
        public string Status { get; set; } = "";
    }

    // Colors
    private readonly Color ColorMainBg = Color.FromArgb(229, 231, 235);    // #E5E7EB
    private readonly Color ColorHeaderBg = Color.White;
    private readonly Color ColorSidebar = Color.FromArgb(44, 62, 80);      // #2C3E50
    private readonly Color ColorButtonNormal = Color.FromArgb(52, 73, 94);
    private readonly Color ColorButtonHover = Color.FromArgb(75, 100, 130);
    private readonly Color ColorButtonActive = Color.FromArgb(26, 188, 156); // #1ABC9C
    private readonly Color ColorText = Color.White;
    private readonly Color ColorAvailable = Color.FromArgb(39, 174, 96);   // #27AE60
    private readonly Color ColorInUse = Color.FromArgb(231, 76, 60);       // #E74C3C
    private readonly Color ColorMaintenance = Color.FromArgb(243, 156, 18); // #F39C12

    public Form1()
    {
        InitializeComponent();
        
        this.Text = "ServerAdmin - Quản Lý Quán Network";
        this.Size = new Size(1024, 768);
        this.WindowState = FormWindowState.Maximized;
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Font = new Font("Segoe UI", 10);
        this.BackColor = ColorMainBg;
        this.DoubleBuffered = true;

        SetupUI();
        
        this.Load += Form1_Load;
        this.FormClosing += Form1_FormClosing;
    }

    private void SetupUI()
    {
        this.Controls.Clear();

        // [1] Master layout: chia ranh giới sidebar/content tuyệt đối
        masterLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = ColorMainBg
        };
        masterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
        masterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        masterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // [2] Sidebar panel (cot 0)
        sidebarPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = ColorSidebar
        };

        // Nut cai dat rieng, dock bottom (khong nam trong flow)
        btnSettings = CreateSidebarButton("⚙ Cài Đặt");
        btnSettings.Dock = DockStyle.Bottom;

        // Flow chua logo + menu theo dung thu tu
        var sidebarFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = ColorSidebar
        };

        var logoPanel = new Panel
        {
            Width = 230,
            Height = 80,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = ColorSidebar,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };

        var lblLogo = new Label
        {
            Text = "ServerAdmin",
            Dock = DockStyle.Fill,
            ForeColor = ColorText,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        logoPanel.Controls.Add(lblLogo);

        btnComputerMgmt = CreateSidebarButton("🖥 Quản Lý Máy Trạm");
        btnAccounts = CreateSidebarButton("👤 Tài Khoản");
        btnServices = CreateSidebarButton("📦 Dịch Vụ");
        btnChat = CreateSidebarButton("💬 Chat");
        btnChatCustomer = CreateSidebarButton("  📩 Nhắn với khách hàng");
        btnChatAnnouncement = CreateSidebarButton("  📢 Thông báo");
        btnChatCustomer.Visible = false;
        btnChatAnnouncement.Visible = false;

        btnAccounts.Click += (s, e) => { ShowPage(pnlTaiKhoan, btnAccounts); LoadAccounts(); };
        btnComputerMgmt.Click += (s, e) => ShowPage(pnlQuanLyMay, btnComputerMgmt);
        btnServices.Click += (s, e) => ShowPage(pnlDichVu, btnServices);
        btnChat.Click += (s, e) =>
        {
            bool expanded = !btnChatCustomer.Visible;
            btnChatCustomer.Visible = expanded;
            btnChatAnnouncement.Visible = expanded;
            if (expanded)
                ShowPage(pnlChat, btnChat);
        };
        btnChatCustomer.Click += (s, e) => ShowPage(pnlChatCustomer, btnChatCustomer);
        btnChatAnnouncement.Click += (s, e) => ShowPage(pnlChatAnnouncement, btnChatAnnouncement);

        sidebarFlow.Controls.Add(logoPanel);
        sidebarFlow.Controls.Add(btnComputerMgmt);
        sidebarFlow.Controls.Add(btnAccounts);
        sidebarFlow.Controls.Add(btnServices);
        sidebarFlow.Controls.Add(btnChat);
        sidebarFlow.Controls.Add(btnChatCustomer);
        sidebarFlow.Controls.Add(btnChatAnnouncement);

        sidebarPanel.Controls.Add(sidebarFlow);
        sidebarPanel.Controls.Add(btnSettings);

        // [3] Host cot 1 de quan ly panel swapping on dinh
        rightContentHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = ColorMainBg
        };

        // 4 trang chuc nang nam trong rightContentHost
        pnlQuanLyMay = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = ColorMainBg,
            Padding = Padding.Empty
        };

        pnlTaiKhoan = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = ColorMainBg
        };

        pnlDichVu = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = ColorMainBg
        };

        pnlChat = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = ColorMainBg
        };

        pnlChatCustomer = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = ColorMainBg
        };

        pnlChatAnnouncement = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = ColorMainBg
        };

        BuildChatPanel();
        BuildAnnouncementPanel();
        BuildOrderPanel();

        // Chi ve UI quan ly may tren pnlQuanLyMay
        headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 88,
            Margin = Padding.Empty,
            BackColor = ColorHeaderBg,
            Padding = new Padding(20, 12, 20, 12),
            MinimumSize = new Size(0, 88)
        };

        lblTitle = new Label
        {
            Text = "Quản Lý Máy Trạm",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = ColorTranslator.FromHtml("#0984E3"),
            Font = new Font("Segoe UI", 18, FontStyle.Bold)
        };
        headerPanel.Controls.Add(lblTitle);

        var computerBtnPanel = new Panel { Dock = DockStyle.Right, Width = 220, BackColor = ColorHeaderBg };
        computerBtnPanel.Padding = new Padding(0, 22, 0, 22);

        btnAddComputer = new Button
        {
            Text = "+ Thêm máy",
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.FromArgb(39, 174, 96),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Width = 100,
            Height = 36,
            Cursor = Cursors.Hand
        };
        btnAddComputer.Click += BtnAddComputer_Click;
        computerBtnPanel.Controls.Add(btnAddComputer);

        btnRemoveComputer = new Button
        {
            Text = "✕ Xóa máy",
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.FromArgb(231, 76, 60),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Left = 110,
            Width = 100,
            Height = 36,
            Cursor = Cursors.Hand
        };
        btnRemoveComputer.Click += BtnRemoveComputer_Click;
        computerBtnPanel.Controls.Add(btnRemoveComputer);

        headerPanel.Controls.Add(computerBtnPanel);

        footerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 44,
            Margin = Padding.Empty,
            BackColor = ColorHeaderBg,
            Padding = new Padding(16, 10, 16, 10),
            MinimumSize = new Size(0, 44)
        };

        lblFooterStats = new Label
        {
            Text = "Đang sử dụng: 0 | Sẵn sàng: 0 | Bảo trì: 0",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(107, 114, 128),
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        footerPanel.Controls.Add(lblFooterStats);

        // [4] Luoi may tram 2 cot
        cardTableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(20, 20, 20, 64),
            AutoScroll = true,
            ColumnCount = 3,
            RowCount = 0,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        cardTableLayout.AutoScrollMargin = new Size(0, 32);

        cardTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        cardTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        cardTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));

        var computerPageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = ColorMainBg
        };
        computerPageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        computerPageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
        computerPageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        computerPageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

        computerPageLayout.Controls.Add(headerPanel, 0, 0);
        computerPageLayout.Controls.Add(cardTableLayout, 0, 1);
        computerPageLayout.Controls.Add(footerPanel, 0, 2);

        pnlQuanLyMay.Controls.Add(computerPageLayout);

        // ---- Account Management page (right content) ----
        var accHeader = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 88,
            Margin = Padding.Empty,
            BackColor = ColorHeaderBg,
            Padding = new Padding(20, 12, 20, 12),
            MinimumSize = new Size(0, 88)
        };
        accHeader.Controls.Add(new Label
        {
            Text = "Quản Lý Tài Khoản",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = ColorTranslator.FromHtml("#0984E3"),
            Font = new Font("Segoe UI", 18, FontStyle.Bold)
        });

        dgvAccounts = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10),
            Margin = new Padding(0)
        };
        dgvAccounts.Columns.Add("Id", "ID");
        dgvAccounts.Columns.Add("Username", "Tên đăng nhập");
        dgvAccounts.Columns.Add("Password", "Mật khẩu");
        dgvAccounts.Columns.Add("Balance", "Số dư (VNĐ)");
        dgvAccounts.Columns.Add("Role", "Vai trò");
        dgvAccounts.Columns.Add("Time", "Thời gian");
        dgvAccounts.Columns.Add("Status", "Trạng thái");
        dgvAccounts.Columns["Status"]!.Width = 100;
        dgvAccounts.Columns["Status"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvAccounts.Columns["Id"]!.Width = 50;
        dgvAccounts.Columns["Password"]!.Visible = false;
        dgvAccounts.Columns["Balance"]!.DefaultCellStyle.Format = "N0";
        dgvAccounts.Columns["Balance"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        dgvAccounts.Columns["Time"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        var txtSearch = new TextBox
        {
            Text = "🔍 Tìm kiếm tài khoản...",
            ForeColor = Color.Gray,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        txtSearch.GotFocus += (s, e) => { if (txtSearch.Text == "🔍 Tìm kiếm tài khoản...") { txtSearch.Text = ""; txtSearch.ForeColor = Color.Black; } };
        txtSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtSearch.Text)) { txtSearch.Text = "🔍 Tìm kiếm tài khoản..."; txtSearch.ForeColor = Color.Gray; } };
        txtSearch.TextChanged += (s, e) =>
        {
            _searchFilter = txtSearch.Text.Trim();
            if (string.Equals(_searchFilter, "🔍 Tìm kiếm tài khoản...", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(_searchFilter))
                _searchFilter = "";
            ApplyAccountFilters();
        };

        var searchPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 40,
            BackColor = Color.White,
            Padding = new Padding(12, 6, 12, 6)
        };
        searchPanel.Controls.Add(txtSearch);

        dgvAccounts.ColumnHeaderMouseClick += (s, e) =>
        {
            var col = dgvAccounts.Columns[e.ColumnIndex];
            string colName = col.Name;

            if (colName == "Role")
            {
                _roleFilterState = (_roleFilterState + 1) % 3;
                ApplyAccountFilters();
                return;
            }

            var menu = new ContextMenuStrip();
            if (colName == "Id" || colName == "Balance")
            {
                menu.Items.Add("Thấp → Cao", null, (_, __) =>
                    dgvAccounts.Sort(dgvAccounts.Columns[colName], System.ComponentModel.ListSortDirection.Ascending));
                menu.Items.Add("Cao → Thấp", null, (_, __) =>
                    dgvAccounts.Sort(dgvAccounts.Columns[colName], System.ComponentModel.ListSortDirection.Descending));
            }
            else
            {
                menu.Items.Add("A → Z", null, (_, __) =>
                    dgvAccounts.Sort(dgvAccounts.Columns[colName], System.ComponentModel.ListSortDirection.Ascending));
                menu.Items.Add("Z → A", null, (_, __) =>
                    dgvAccounts.Sort(dgvAccounts.Columns[colName], System.ComponentModel.ListSortDirection.Descending));
            }
            menu.Show(dgvAccounts, dgvAccounts.PointToClient(Cursor.Position));
        };

        var accBtnPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 100,
            BackColor = ColorHeaderBg,
            Padding = new Padding(16, 8, 16, 8)
        };

        btnCreateAccount = new Button
        {
            Text = "➕ Tạo",
            Location = new Point(16, 10), Size = new Size(100, 36),
            BackColor = Color.FromArgb(39, 174, 96), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 },
            Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
        };
        btnCreateAccount.Click += BtnCreateAccount_Click;

        btnDeleteAccount = new Button
        {
            Text = "🗑 Xóa",
            Location = new Point(126, 10), Size = new Size(100, 36),
            BackColor = Color.FromArgb(231, 76, 60), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 },
            Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
        };
        btnDeleteAccount.Click += BtnDeleteAccount_Click;

        btnAddFund = new Button
        {
            Text = "💰 Nạp tiền",
            Location = new Point(236, 10), Size = new Size(110, 36),
            BackColor = Color.FromArgb(52, 152, 219), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 },
            Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
        };
        btnAddFund.Click += BtnAddFund_Click;

        var btnRefund = new Button
        {
            Text = "💳 Hoàn tiền",
            Location = new Point(16, 54), Size = new Size(110, 36),
            BackColor = Color.FromArgb(243, 156, 18), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 },
            Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
        };
        btnRefund.Click += BtnRefund_Click;

        var btnRefresh = new Button
        {
            Text = "🔄 Làm mới",
            Location = new Point(136, 54), Size = new Size(100, 36),
            BackColor = Color.FromArgb(149, 165, 166), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 },
            Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
        };
        btnRefresh.Click += BtnRefresh_Click;

        var btnChangePassword = new Button
        {
            Text = "🔑 Đổi MK",
            Location = new Point(246, 54), Size = new Size(110, 36),
            BackColor = Color.FromArgb(155, 89, 182), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 },
            Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
        };
        btnChangePassword.Click += BtnChangePassword_Click;

        accBtnPanel.Controls.Add(btnCreateAccount);
        accBtnPanel.Controls.Add(btnDeleteAccount);
        accBtnPanel.Controls.Add(btnAddFund);
        accBtnPanel.Controls.Add(btnRefund);
        accBtnPanel.Controls.Add(btnRefresh);
        accBtnPanel.Controls.Add(btnChangePassword);

        lblAccountFooter = new Label
        {
            Text = "Tổng số tài khoản: 0",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(107, 114, 128),
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };

        var accFooter = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 44,
            Margin = Padding.Empty,
            BackColor = ColorHeaderBg,
            Padding = new Padding(16, 10, 16, 10),
            MinimumSize = new Size(0, 44)
        };
        accFooter.Controls.Add(lblAccountFooter);

        var accContentArea = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty, Padding = Padding.Empty,
            ColumnCount = 1, RowCount = 3,
            BackColor = ColorMainBg
        };
        accContentArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        accContentArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        accContentArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        accContentArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
        accContentArea.Controls.Add(searchPanel, 0, 0);
        accContentArea.Controls.Add(dgvAccounts, 0, 1);
        accContentArea.Controls.Add(accBtnPanel, 0, 2);

        var accPageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty, Padding = Padding.Empty,
            ColumnCount = 1, RowCount = 3,
            BackColor = ColorMainBg
        };
        accPageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        accPageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
        accPageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        accPageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        accPageLayout.Controls.Add(accHeader, 0, 0);
        accPageLayout.Controls.Add(accContentArea, 0, 1);
        accPageLayout.Controls.Add(accFooter, 0, 2);

        pnlTaiKhoan.Controls.Add(accPageLayout);

        rightContentHost.Controls.Add(pnlQuanLyMay);
        rightContentHost.Controls.Add(pnlTaiKhoan);
        rightContentHost.Controls.Add(pnlDichVu);
        rightContentHost.Controls.Add(pnlChat);
        rightContentHost.Controls.Add(pnlChatCustomer);
        rightContentHost.Controls.Add(pnlChatAnnouncement);

        masterLayout.Controls.Add(sidebarPanel, 0, 0);
        masterLayout.Controls.Add(rightContentHost, 1, 0);
        this.Controls.Add(masterLayout);

        // Mac dinh mo trang quan ly may
        ShowComputerManagement();
        ShowPage(pnlQuanLyMay, btnComputerMgmt);
    }

    private void ShowPage(Panel? panel, Button? activeButton)
    {
        panel?.BringToFront();
        SetActiveMenu(activeButton);
    }

    private void SetActiveMenu(Button? activeButton)
    {
        var menuButtons = new[] { btnComputerMgmt, btnAccounts, btnServices, btnChat, btnChatCustomer, btnChatAnnouncement };

        foreach (var button in menuButtons)
        {
            if (button == null) continue;
            button.BackColor = button == activeButton ? ColorButtonActive : ColorButtonNormal;
        }
    }

    private Button CreateSidebarButton(string text)
    {
        Button btn = new Button
        {
            Text = text,
            AutoSize = false,
            Width = 230,
            Height = 50,
            Margin = Padding.Empty,
            BackColor = ColorButtonNormal,
            ForeColor = ColorText,
            Font = new Font("Segoe UI", 10),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 0, 0, 0)
        };

        btn.MouseEnter += (s, e) => btn.BackColor = ColorButtonHover;
        btn.MouseLeave += (s, e) =>
        {
            if (btn.BackColor != ColorButtonActive)
            {
                btn.BackColor = ColorButtonNormal;
            }
        };

        return btn;
    }

    private void BtnAddComputer_Click(object? sender, EventArgs e)
    {
        var input = Microsoft.VisualBasic.Interaction.InputBox("Nhập tên máy tính mới:", "Thêm máy", "");
        if (string.IsNullOrWhiteSpace(input)) return;

        var name = input.Trim();
        using var db = DatabaseHelper.GetConnection();
        try
        {
            db.Execute("INSERT INTO Computers (Name, Status) VALUES (@Name, 'Available')", new { Name = name });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể thêm máy: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        ShowComputerManagement();
    }

    private void BtnRemoveComputer_Click(object? sender, EventArgs e)
    {
        var input = Microsoft.VisualBasic.Interaction.InputBox("Nhập tên máy cần xóa:", "Xóa máy", "");
        if (string.IsNullOrWhiteSpace(input)) return;

        var name = input.Trim();
        var confirm = MessageBox.Show($"Xóa máy {name}? Thao tác này không thể hoàn tác.",
            "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        using var db = DatabaseHelper.GetConnection();
        var existing = db.QueryFirstOrDefault<Computer>("SELECT * FROM Computers WHERE Name = @Name", new { Name = name });
        if (existing == null)
        {
            MessageBox.Show($"Không tìm thấy máy '{name}'", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (existing.Status == "InUse")
        {
            MessageBox.Show($"Không thể xóa máy '{name}' vì đang có người sử dụng.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        db.Execute("DELETE FROM Computers WHERE Name = @Name", new { Name = name });
        ShowComputerManagement();
    }

    private void ShowComputerManagement()
    {
        if (cardTableLayout == null) return;

        List<ComputerModel> models;
        try
        {
            using var db = DatabaseHelper.GetConnection();
            var rows = db.Query<dynamic>(@"
                SELECT c.Id, c.Name, c.Status, c.CurrentUserId,
                       u.Username, u.Balance
                FROM Computers c
                LEFT JOIN Users u ON c.CurrentUserId = u.Id
                ORDER BY c.Id").ToList();

            models = rows.Select(r =>
            {
                int id = (int)r.Id;
                string name = (string)r.Name;
                string dbStatus = (string)r.Status;
                int? currentUserId = (int?)r.CurrentUserId;
                string? currentUser = (string?)r.Username;
                decimal? balance = (decimal?)r.Balance;

                bool isConnected = _server?.IsComputerConnected(id) ?? false;

                ComputerStatus status;
                if (dbStatus == "InUse")
                    status = ComputerStatus.InUse;
                else if (dbStatus == "Maintenance")
                    status = ComputerStatus.Maintenance;
                else if (isConnected)
                    status = ComputerStatus.Available;
                else
                    status = ComputerStatus.Offline;

                return new ComputerModel
                {
                    ComputerId = id,
                    ComputerName = name,
                    Status = status,
                    CurrentUser = currentUser,
                    Balance = (long)(balance ?? 0),
                    IsActive = isConnected
                };
            }).ToList();
        }
        catch
        {
            return;
        }

        int inUseCount = 0, availableCount = 0, maintenanceCount = 0;

        cardTableLayout.SuspendLayout();
        cardTableLayout.Controls.Clear();
        cardTableLayout.RowStyles.Clear();
        cardTableLayout.RowCount = 0;

        var scale = (float)DeviceDpi / 96f;
        int padX = Math.Max(1, (int)Math.Round(10 * scale));
        int padY = Math.Max(1, (int)Math.Round(12 * scale));

        for (int i = 0; i < models.Count; i++)
        {
            var model = models[i];

            switch (model.Status)
            {
                case ComputerStatus.InUse: inUseCount++; break;
                case ComputerStatus.Available: availableCount++; break;
                default: maintenanceCount++; break;
            }

            int row = i / 3;
            int col = i % 3;

            if (col == 0)
            {
                cardTableLayout.RowCount++;
                cardTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140F));
            }

            var card = new ComputerCard(model)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(padX, padY, padX, padY)
            };

            if (card.ContextMenuStrip != null)
            {
                card.ContextMenuStrip = new ContextMenuStrip();

                var shutdownItem = new ToolStripMenuItem("Tắt Máy");
                shutdownItem.Click += (_, _) =>
                {
                    if (MessageBox.Show($"Tắt {model.ComputerName}?",
                        "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        _server?.ShutdownComputer(model.ComputerId);
                    }
                };
                card.ContextMenuStrip.Items.Add(shutdownItem);

                var restartItem = new ToolStripMenuItem("Khởi Động Lại");
                restartItem.Click += (_, _) =>
                {
                    if (MessageBox.Show($"Khởi động lại {model.ComputerName}?",
                        "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        _server?.RestartComputer(model.ComputerId);
                    }
                };
                card.ContextMenuStrip.Items.Add(restartItem);

                card.ContextMenuStrip.Items.Add(new ToolStripSeparator());

                var lockItem = new ToolStripMenuItem("Khóa Máy");
                lockItem.Click += (_, _) =>
                {
                    if (MessageBox.Show($"Khóa {model.ComputerName}?",
                        "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        _server?.AdminLockComputer(model.ComputerId);
                        BeginInvoke(new Action(ShowComputerManagement));
                    }
                };
                card.ContextMenuStrip.Items.Add(lockItem);
            }

            cardTableLayout.Controls.Add(card, col, row);
        }

        int spacerRow = cardTableLayout.RowCount;
        cardTableLayout.RowCount++;
        cardTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));

        var bottomSpacer = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = Color.FromArgb(20, 25, 35)
        };
        cardTableLayout.Controls.Add(bottomSpacer, 0, spacerRow);
        cardTableLayout.SetColumnSpan(bottomSpacer, 3);

        cardTableLayout.ResumeLayout();
        cardTableLayout.PerformLayout();

        if (lblFooterStats != null)
        {
            lblFooterStats.Text = $"Đang sử dụng: {inUseCount} | Sẵn sàng: {availableCount} | Bảo trì: {maintenanceCount}";
        }
    }

    private void ApplyAccountFilters()
    {
        if (dgvAccounts == null) return;
        string searchLower = _searchFilter.ToLower();
        dgvAccounts.CurrentCell = null;

        foreach (DataGridViewRow row in dgvAccounts.Rows)
        {
            var username = row.Cells["Username"].Value?.ToString() ?? "";
            var role = row.Cells["Role"].Value?.ToString() ?? "";

            bool matchesSearch = string.IsNullOrEmpty(searchLower) ||
                username.ToLower().Contains(searchLower) || role.ToLower().Contains(searchLower);

            bool matchesRole = _roleFilterState == 0 ||
                (_roleFilterState == 1 && role == "Quản trị") ||
                (_roleFilterState == 2 && role == "Khách hàng");

            row.Visible = matchesSearch && matchesRole;
        }
    }

    private void BtnRefresh_Click(object? sender, EventArgs e)
    {
        if ((DateTime.Now - _lastRefreshTime).TotalSeconds < RefreshCooldownSeconds)
            return;
        _lastRefreshTime = DateTime.Now;
        LoadAccounts();
    }

    private void BtnChangePassword_Click(object? sender, EventArgs e)
    {
        if (dgvAccounts?.SelectedRows.Count == 0)
        {
            MessageBox.Show("Vui lòng chọn một tài khoản.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var row = dgvAccounts.SelectedRows[0];
        int id = Convert.ToInt32(row.Cells["Id"].Value);
        string username = (string)row.Cells["Username"].Value;

        var newPassword = ShowInputDialog($"Nhập mật khẩu mới cho \"{username}\":", "Đổi mật khẩu", "123456");
        if (string.IsNullOrWhiteSpace(newPassword)) return;

        try
        {
            using var db = DatabaseHelper.GetConnection();
            var affected = db.Execute("UPDATE Users SET Password = @Password WHERE Id = @Id",
                new { Password = newPassword, Id = id });
            if (affected > 0)
                MessageBox.Show($"Đổi mật khẩu cho \"{username}\" thành công.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show("Không tìm thấy tài khoản.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            LoadAccounts();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi đổi mật khẩu: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadAccounts()
    {
        try
        {
            _server?.FlushAllSessionsToDb().GetAwaiter().GetResult();
            using var db = DatabaseHelper.GetConnection();
            var users = db.Query<dynamic>("SELECT Id, Username, Password, Balance, Role FROM Users ORDER BY Id");

            dgvAccounts?.Rows.Clear();

            int total = 0;
            foreach (var user in users)
            {
                string roleText = user.Role == "Admin" ? "Quản trị" : "Khách hàng";
                decimal balance = (decimal)user.Balance;
                int totalMinutes = (int)(balance / HourlyRate * 60);
                string timeText = totalMinutes >= 60 ? $"{totalMinutes / 60}h {totalMinutes % 60}m" : $"{totalMinutes}m";
                string statusText = _server?.IsUserOnline(user.Id) == true ? "● Đang chơi" : "○ Ngoại tuyến";
                dgvAccounts?.Rows.Add(user.Id, user.Username, user.Password, balance, roleText, timeText, statusText);
                total++;
            }

            if (lblAccountFooter != null)
                lblAccountFooter.Text = $"Tổng số tài khoản: {total}";

            ApplyAccountFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải danh sách tài khoản: {ex.Message}");
        }
    }

    private static string? ShowInputDialog(string prompt, string title, string defaultValue = "")
    {
        var form = new Form
        {
            Text = title,
            Size = new Size(400, 180),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false
        };

        var lbl = new Label { Text = prompt, Left = 16, Top = 16, Width = 352, Height = 30 };
        var txt = new TextBox { Left = 16, Top = 52, Width = 352, Height = 30, Text = defaultValue };
        var btnOk = new Button { Text = "OK", Left = 200, Top = 100, Width = 80, Height = 30, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Hủy", Left = 288, Top = 100, Width = 80, Height = 30, DialogResult = DialogResult.Cancel };

        form.Controls.Add(lbl);
        form.Controls.Add(txt);
        form.Controls.Add(btnOk);
        form.Controls.Add(btnCancel);
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        return form.ShowDialog() == DialogResult.OK ? txt.Text : null;
    }

    private void BtnCreateAccount_Click(object? sender, EventArgs e)
    {
        var username = ShowInputDialog("Nhập tên đăng nhập:", "Tạo tài khoản mới");
        if (string.IsNullOrWhiteSpace(username)) return;

        var password = ShowInputDialog("Nhập mật khẩu:", "Tạo tài khoản mới", "123456");
        if (string.IsNullOrWhiteSpace(password)) return;

        try
        {
            using var db = DatabaseHelper.GetConnection();
            db.Execute("INSERT INTO Users (Username, Password, Role) VALUES (@u, @p, 'Client')",
                new { u = username.Trim(), p = password });
            LoadAccounts();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tạo tài khoản: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnDeleteAccount_Click(object? sender, EventArgs e)
    {
        if (dgvAccounts?.SelectedRows.Count == 0)
        {
            MessageBox.Show("Vui lòng chọn một tài khoản.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var row = dgvAccounts.SelectedRows[0];
        int id = Convert.ToInt32(row.Cells["Id"].Value);
        string username = (string)row.Cells["Username"].Value;

        if (username == "admin")
        {
            MessageBox.Show("Không thể xóa tài khoản admin mặc định.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show($"Xóa tài khoản \"{username}\"?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        var code = ShowInputDialog($"Nhập \"XÁC NHẬN\" để xóa tài khoản \"{username}\":", "Xác nhận lần cuối");
        if (code?.Trim().ToUpper() != "XÁC NHẬN") return;

        try
        {
            using var db = DatabaseHelper.GetConnection();
            db.Execute("DELETE FROM Users WHERE Id = @id", new { id });
            LoadAccounts();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi xóa tài khoản: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnAddFund_Click(object? sender, EventArgs e)
    {
        if (dgvAccounts?.SelectedRows.Count == 0)
        {
            MessageBox.Show("Vui lòng chọn một tài khoản.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var row = dgvAccounts.SelectedRows[0];
        int id = Convert.ToInt32(row.Cells["Id"].Value);
        string username = (string)row.Cells["Username"].Value;

        var input = ShowInputDialog($"Nhập số tiền (VNĐ) cần nạp cho \"{username}\":", "Nạp tiền", "50000");
        if (string.IsNullOrWhiteSpace(input)) return;

        if (!decimal.TryParse(input, out decimal amount) || amount <= 0)
        {
            MessageBox.Show("Số tiền không hợp lệ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _server?.AddUserFund(id, amount);
            LoadAccounts();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi nạp tiền: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnRefund_Click(object? sender, EventArgs e)
    {
        if (dgvAccounts?.SelectedRows.Count == 0)
        {
            MessageBox.Show("Vui lòng chọn một tài khoản.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var row = dgvAccounts.SelectedRows[0];
        int id = Convert.ToInt32(row.Cells["Id"].Value);
        string username = (string)row.Cells["Username"].Value;
        decimal currentBalance = Convert.ToDecimal(row.Cells["Balance"].Value);

        var input = ShowInputDialog($"Nhập số tiền (VNĐ) cần hoàn cho \"{username}\" (Số dư hiện tại: {currentBalance:N0}):", "Hoàn tiền", "0");
        if (string.IsNullOrWhiteSpace(input)) return;

        if (!decimal.TryParse(input, out decimal amount) || amount <= 0)
        {
            MessageBox.Show("Số tiền không hợp lệ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (amount > currentBalance)
        {
            MessageBox.Show("Số tiền hoàn không được lớn hơn số dư hiện tại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            using var db = DatabaseHelper.GetConnection();
            db.Execute("UPDATE Users SET Balance = Balance - @amount WHERE Id = @id", new { amount, id });
            LoadAccounts();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi hoàn tiền: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BuildChatPanel()
    {
        if (pnlChatCustomer == null) return;

        pnlChatCustomer.BackColor = ColorMainBg;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0),
            ColumnCount = 1,
            RowCount = 3,
            BackColor = ColorMainBg,
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));

        var headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(44, 62, 80),
            Padding = new Padding(16, 0, 16, 0),
        };

        var lblHeader = new Label
        {
            Text = "💬 NHẮN VỚI KHÁCH HÀNG",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        headerPanel.Controls.Add(lblHeader);

        var messagePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 247, 250),
            Padding = new Padding(8),
        };

        _chatMessageList = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 247, 250),
            ForeColor = Color.FromArgb(44, 62, 80),
            Font = new Font("Segoe UI", 10),
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
        };
        _chatMessageList.SelectedIndexChanged += (_, _) =>
        {
            if (_chatMessageList?.SelectedItem is ChatEntry entry)
            {
                _chatTargetComputerId = entry.ComputerId;
                _chatTargetComputerName = entry.ComputerName;
                UpdateChatTargetLabel();
            }
        };
        messagePanel.Controls.Add(_chatMessageList);

        var replyPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
            Padding = new Padding(16, 8, 16, 8),
        };
        replyPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        replyPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _chatTargetLabel = new Label
        {
            Text = "Chọn tin nhắn từ danh sách để trả lời",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = Color.FromArgb(120, 130, 145),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.White,
        };

        var inputRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White,
        };
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        _chatReplyInput = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10),
            Margin = new Padding(0, 4, 8, 4),
            BorderStyle = BorderStyle.FixedSingle,
        };

        _chatSendBtn = new Button
        {
            Text = "Gửi",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(26, 188, 156),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 4, 0, 4),
        };
        _chatSendBtn.FlatAppearance.BorderSize = 0;

        _chatSendBtn.Click += async (_, _) =>
        {
            var msg = _chatReplyInput?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(msg) || _server == null)
                return;

            if (_chatTargetComputerId <= 0)
            {
                MessageBox.Show("Chọn tin nhắn từ danh sách bên trái để trả lời.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await _server.SendAdminChatReply(_chatTargetComputerId, msg);

            var entry = new ChatEntry
            {
                ComputerId = _chatTargetComputerId,
                ComputerName = _chatTargetComputerName,
                DisplayText = $"[Admin → {_chatTargetComputerName}] {msg}"
            };
            _chatMessageList?.Items.Add(entry);
            _chatMessageList?.TopIndex = _chatMessageList.Items.Count - 1;
            _chatReplyInput!.Clear();
        };

        inputRow.Controls.Add(_chatReplyInput, 0, 0);
        inputRow.Controls.Add(_chatSendBtn, 1, 0);

        replyPanel.Controls.Add(_chatTargetLabel, 0, 0);
        replyPanel.Controls.Add(inputRow, 0, 1);

        mainLayout.Controls.Add(headerPanel, 0, 0);
        mainLayout.Controls.Add(messagePanel, 0, 1);
        mainLayout.Controls.Add(replyPanel, 0, 2);

        pnlChatCustomer.Controls.Add(mainLayout);
    }

    private void BuildAnnouncementPanel()
    {
        if (pnlChatAnnouncement == null) return;

        pnlChatAnnouncement.BackColor = ColorMainBg;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 4,
            BackColor = ColorMainBg,
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(44, 62, 80),
            Padding = new Padding(16, 0, 16, 0),
        };
        headerPanel.Controls.Add(new Label
        {
            Text = "📢 GỬI THÔNG BÁO",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
        });

        var txtAnnouncement = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12),
            Margin = new Padding(0, 8, 0, 8),
            BorderStyle = BorderStyle.FixedSingle,
            Multiline = true,
            Height = 100,
        };

        var durationRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = ColorMainBg,
        };
        durationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        durationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        durationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        durationRow.Controls.Add(new Label
        {
            Text = "Thời gian:",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        var cmbDuration = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10),
            Margin = new Padding(0, 4, 8, 4),
        };
        for (int i = 1; i <= 10; i++)
            cmbDuration.Items.Add($"{i} giây");
        cmbDuration.SelectedIndex = 0;
        durationRow.Controls.Add(cmbDuration, 1, 0);

        var btnSend = new Button
        {
            Text = "Gửi",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(26, 188, 156),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 4, 0, 4),
            Enabled = false,
        };
        btnSend.FlatAppearance.BorderSize = 0;

        txtAnnouncement.TextChanged += (_, _) =>
        {
            btnSend.Enabled = !string.IsNullOrWhiteSpace(txtAnnouncement.Text);
        };

        btnSend.Click += async (_, _) =>
        {
            var msg = txtAnnouncement.Text.Trim();
            if (string.IsNullOrWhiteSpace(msg) || _server == null)
                return;

            int duration = cmbDuration.SelectedIndex + 1;
            await _server.BroadcastAnnouncement(msg, duration);
            txtAnnouncement.Clear();
        };

        txtAnnouncement.KeyDown += (s, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                btnSend.PerformClick();
            }
        };

        durationRow.Controls.Add(btnSend, 2, 0);

        mainLayout.Controls.Add(headerPanel, 0, 0);
        mainLayout.Controls.Add(txtAnnouncement, 0, 1);
        mainLayout.Controls.Add(durationRow, 0, 2);

        var spacer = new Panel { Dock = DockStyle.Fill, BackColor = ColorMainBg };
        mainLayout.Controls.Add(spacer, 0, 3);

        pnlChatAnnouncement.Controls.Add(mainLayout);
    }

    #region Order Panel

    private Button MakeFilterBtn(string text, bool active)
    {
        return new Button
        {
            Text = text,
            AutoSize = false,
            Width = Math.Max(110, TextRenderer.MeasureText(text, new Font("Segoe UI", 9, FontStyle.Bold)).Width + 24),
            Height = 30,
            Margin = new Padding(2, 0, 2, 0),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            BackColor = active ? ColorButtonActive : ColorButtonNormal,
            ForeColor = Color.White,
        };
    }

    private void SetActiveFilter(Button? active)
    {
        foreach (var btn in new[] { btnFilterAll, btnFilterPending, btnFilterProcessing, btnFilterCompleted })
        {
            if (btn != null) btn.BackColor = btn == active ? ColorButtonActive : ColorButtonNormal;
        }
    }

    private void BuildOrderPanel()
    {
        if (pnlDichVu == null) return;
        pnlDichVu.Padding = new Padding(0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = ColorMainBg,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

        // Header
        var hdr = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20, 12, 20, 12) };
        hdr.Controls.Add(new Label
        {
            Text = "Quản Lý Đơn Hàng",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = ColorTranslator.FromHtml("#0984E3"),
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
        });

        // Filter tabs
        var filterBar = new Panel { Dock = DockStyle.Fill, BackColor = ColorMainBg, Padding = new Padding(8, 4, 8, 4) };
        var filterFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = ColorMainBg };

        btnFilterAll = MakeFilterBtn("Tất cả", true);
        btnFilterPending = MakeFilterBtn("Đang chờ", false);
        btnFilterProcessing = MakeFilterBtn("Đang chế biến", false);
        btnFilterCompleted = MakeFilterBtn("Hoàn thành", false);

        btnFilterAll.Click += (_, _) => { _orderFilter = ""; SetActiveFilter(btnFilterAll); LoadOrders(); };
        btnFilterPending.Click += (_, _) => { _orderFilter = "Pending"; SetActiveFilter(btnFilterPending); LoadOrders(); };
        btnFilterProcessing.Click += (_, _) => { _orderFilter = "Processing"; SetActiveFilter(btnFilterProcessing); LoadOrders(); };
        btnFilterCompleted.Click += (_, _) => { _orderFilter = "Completed"; SetActiveFilter(btnFilterCompleted); LoadOrders(); };

        filterFlow.Controls.AddRange(new Control[] { btnFilterAll, btnFilterPending, btnFilterProcessing, btnFilterCompleted });
        filterBar.Controls.Add(filterFlow);

        // Data grid
        dgvOrders = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10),
            RowTemplate = { Height = 42 },
        };

        dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID", Width = 60, ReadOnly = true });
        dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "Computer", HeaderText = "PC", Width = 80, ReadOnly = true });
        dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "Details", HeaderText = "Chi tiết", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "Total", HeaderText = "Tổng tiền", Width = 110, ReadOnly = true });
        dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "Payment", HeaderText = "Thanh toán", Width = 130, ReadOnly = true });
        dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "Ghi chú", Width = 120, ReadOnly = true });
        dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "Accept", HeaderText = "Duyệt", Width = 100, ReadOnly = true });
        dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cancel", HeaderText = "Hủy", Width = 80, ReadOnly = true });

        dgvOrders.Columns["Total"]!.DefaultCellStyle.Format = "N0";
        dgvOrders.Columns["Total"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        dgvOrders.CellFormatting += OrdersGrid_CellFormatting;
        dgvOrders.CellClick += OrdersGrid_CellClick;

        // Audio checkbox
        var audioPnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        chkAutoPlayAlarm = new CheckBox
        {
            Text = "Phát âm thanh khi có đơn hàng mới",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 0, 0, 0),
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(80, 80, 80),
            Checked = true,
        };
        audioPnl.Controls.Add(chkAutoPlayAlarm);

        layout.Controls.Add(hdr, 0, 0);
        layout.Controls.Add(filterBar, 0, 1);
        layout.Controls.Add(dgvOrders, 0, 2);
        layout.Controls.Add(audioPnl, 0, 3);
        pnlDichVu.Controls.Add(layout);

        LoadOrders();
    }

    private void OrdersGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || dgvOrders == null) return;

        if (e.ColumnIndex == dgvOrders.Columns["Computer"]!.Index && e.Value != null)
        {
            e.CellStyle.Font = new Font("Segoe UI", 13, FontStyle.Bold);
            e.CellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        if (e.ColumnIndex == dgvOrders.Columns["Payment"]!.Index)
        {
            var val = e.Value?.ToString();
            if (val == "Account")
            {
                e.CellStyle.BackColor = Color.FromArgb(212, 239, 223);
                e.CellStyle.ForeColor = Color.FromArgb(39, 174, 96);
                e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                e.Value = "Account (PAID)";
            }
        }

        if (e.ColumnIndex == dgvOrders.Columns["Accept"]!.Index)
        {
            var tag = dgvOrders.Rows[e.RowIndex].Tag as OrderGroupTag;
            if (tag == null) return;
            if (tag.Status == "Pending")
            {
                e.CellStyle.BackColor = Color.FromArgb(39, 174, 96);
                e.CellStyle.ForeColor = Color.White;
                e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                e.Value = "Chế biến";
            }
            else if (tag.Status == "Processing")
            {
                e.CellStyle.BackColor = Color.FromArgb(52, 152, 219);
                e.CellStyle.ForeColor = Color.White;
                e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                e.Value = "Hoàn thành";
            }
            else
            {
                e.CellStyle.BackColor = Color.White;
                e.Value = "";
            }
        }

        if (e.ColumnIndex == dgvOrders.Columns["Cancel"]!.Index)
        {
            var tag = dgvOrders.Rows[e.RowIndex].Tag as OrderGroupTag;
            if (tag == null) return;
            if (tag.Status == "Pending")
            {
                e.CellStyle.BackColor = Color.FromArgb(231, 76, 60);
                e.CellStyle.ForeColor = Color.White;
                e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                e.Value = "Hủy";
            }
            else
            {
                e.CellStyle.BackColor = Color.White;
                e.Value = "";
            }
        }
    }

    private void OrdersGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || dgvOrders == null) return;
        var row = dgvOrders.Rows[e.RowIndex];
        var tag = row.Tag as OrderGroupTag;
        if (tag == null) return;

        if (e.ColumnIndex == dgvOrders.Columns["Accept"]!.Index)
        {
            if (tag.Status == "Pending")
            {
                UpdateOrderStatus(tag, "Processing");
                LoadOrders();
            }
            else if (tag.Status == "Processing")
            {
                UpdateOrderStatus(tag, "Completed");
                LoadOrders();
            }
        }
        else if (e.ColumnIndex == dgvOrders.Columns["Cancel"]!.Index)
        {
            if (tag.Status == "Pending")
            {
                int orderId = Convert.ToInt32(row.Cells["Id"].Value);
                var confirm = MessageBox.Show($"Hủy đơn hàng #{orderId}?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes)
                {
                    UpdateOrderStatus(tag, "Cancelled");
                    LoadOrders();
                }
            }
        }
    }

    private void UpdateOrderStatus(OrderGroupTag tag, string newStatus)
    {
        using var db = DatabaseHelper.GetConnection();
        db.Execute("UPDATE Orders SET Status = @Status WHERE ComputerId = @ComputerId AND Time = @Time",
            new { Status = newStatus, ComputerId = tag.ComputerId, Time = tag.Time });

        if (newStatus == "Cancelled")
        {
            var orders = db.Query<dynamic>(
                @"SELECT o.UserId, o.ProductId, o.Quantity, p.Price
                  FROM Orders o
                  LEFT JOIN Products p ON o.ProductId = p.Id
                  WHERE o.ComputerId = @ComputerId AND o.Time = @Time",
                new { ComputerId = tag.ComputerId, Time = tag.Time }).ToList();

            if (orders.Count > 0)
            {
                int userId = (int)orders[0].UserId;
                decimal total = orders.Sum(o => (int)o.Quantity * (o.Price != null ? (decimal)o.Price : 0));
                if (total > 0)
                    _server?.RefundUser(userId, tag.ComputerId, total);
            }

            _ = _server?.SendMessageToClient(tag.ComputerId, new NetworkMessage
            {
                Action = "Notification",
                Payload = "Đơn hàng của bạn đã bị từ chối."
            });
        }
    }

    private void LoadOrders()
    {
        try
        {
            using var db = DatabaseHelper.GetConnection();
            var raw = db.Query<dynamic>(@"
                SELECT o.Id, o.ComputerId, o.ProductId, o.Quantity, o.Status, o.Time, o.Notes,
                       p.Name AS ProductName, p.Price AS ProductPrice,
                       c.Name AS ComputerName
                FROM Orders o
                LEFT JOIN Products p ON o.ProductId = p.Id
                LEFT JOIN Computers c ON o.ComputerId = c.Id
                ORDER BY o.Id DESC
                LIMIT 200").ToList();

            var typed = raw.Select(r => new
            {
                Id = (int)r.Id,
                ComputerId = (int)r.ComputerId,
                ProductId = (int)r.ProductId,
                Quantity = (int)r.Quantity,
                Status = (string)r.Status,
                Time = (string)r.Time,
                Notes = (string?)r.Notes,
                ProductName = (string)r.ProductName,
                ProductPrice = (decimal)r.ProductPrice,
                ComputerName = (string)r.ComputerName
            }).ToList();

            var groups = typed.GroupBy(r => new { r.ComputerId, r.Time }).ToList();

            dgvOrders!.Rows.Clear();
            int pendingCount = 0;

            foreach (var grp in groups)
            {
                var items = grp.ToList();
                var first = items[0];
                string status = first.Status;

                if (!string.IsNullOrEmpty(_orderFilter) && status != _orderFilter) continue;
                if (status == "Pending") pendingCount++;

                string details = string.Join(", ", items.Select(i => $"{i.Quantity}x {i.ProductName}"));
                decimal total = items.Sum(i => i.Quantity * i.ProductPrice);
                string notes = items.FirstOrDefault(i => !string.IsNullOrEmpty(i.Notes))?.Notes ?? "";

                int idx = dgvOrders.Rows.Add(first.Id, first.ComputerName, details, total, "Account", notes, "", "");
                dgvOrders.Rows[idx].Tag = new OrderGroupTag
                {
                    ComputerId = grp.Key.ComputerId,
                    Time = grp.Key.Time,
                    Status = status,
                };
            }

            btnFilterPending!.Text = $"Đang chờ ({pendingCount})";

            if (pendingCount > _lastPendingCount && chkAutoPlayAlarm?.Checked == true)
                PlayOrderAlert();
            _lastPendingCount = pendingCount;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải đơn hàng: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PlayOrderAlert()
    {
        try
        {
            System.Media.SystemSounds.Asterisk.Play();
            if (_alertTimer == null)
            {
                _alertTimer = new System.Windows.Forms.Timer();
                _alertTimer.Tick += (_, _) => System.Media.SystemSounds.Asterisk.Play();
            }
            _alertTimer.Interval = 2000;
            _alertTimer.Start();

            var stopper = new System.Windows.Forms.Timer { Interval = 30000 };
            stopper.Tick += (_, _) => { _alertTimer?.Stop(); stopper.Stop(); };
            stopper.Start();
        }
        catch { }
    }

    #endregion

    private void Form1_Load(object? sender, EventArgs e)
    {
        try
        {
            _server = new NetworkServer();
            _server.OnChatMessageReceived += OnChatReceived;
            _server.OnOrderReceived += OnOrderReceivedFromServer;
            _server.OnComputerStatusChanged += OnComputerStatusChanged;
            _server.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khởi động server: {ex.Message}");
        }

        _orderTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _orderTimer.Tick += (_, _) =>
        {
            if (IsDisposed) return;
            if (pnlDichVu != null && pnlDichVu.Visible)
                LoadOrders();
        };
        _orderTimer.Start();

        _computerTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _computerTimer.Tick += (_, _) =>
        {
            if (IsDisposed) return;
            if (pnlQuanLyMay != null && pnlQuanLyMay.Visible)
                ShowComputerManagement();
        };
        _computerTimer.Start();
    }

    private void OnOrderReceivedFromServer()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(OnOrderReceivedFromServer));
            return;
        }
        LoadOrders();
    }

    private void OnComputerStatusChanged(int computerId, string status)
    {
        if (IsDisposed) return;
        if (pnlQuanLyMay != null && pnlQuanLyMay.Visible)
            BeginInvoke(new Action(ShowComputerManagement));
    }

    private void OnChatReceived(ChatMessagePayload msg)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => OnChatReceived(msg)));
            return;
        }

        var entry = new ChatEntry
        {
            ComputerId = msg.ComputerId,
            ComputerName = msg.ComputerName,
            DisplayText = $"[{msg.ComputerName} - {msg.Username}] {msg.Message}"
        };
        _chatMessageList?.Items.Add(entry);
        _chatMessageList?.TopIndex = _chatMessageList.Items.Count - 1;
        _chatMessageList?.SelectedItem = entry;

        _chatTargetComputerId = msg.ComputerId;
        _chatTargetComputerName = msg.ComputerName;
        UpdateChatTargetLabel();
    }

    private void UpdateChatTargetLabel()
    {
        if (_chatTargetLabel == null) return;
        if (_chatTargetComputerId > 0)
        {
            _chatTargetLabel.Text = $"Trả lời: {_chatTargetComputerName}";
            _chatTargetLabel.ForeColor = Color.FromArgb(26, 188, 156);
            _chatTargetLabel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        }
        else
        {
            _chatTargetLabel.Text = "Chọn tin nhắn từ danh sách để trả lời";
            _chatTargetLabel.ForeColor = Color.FromArgb(120, 130, 145);
            _chatTargetLabel.Font = new Font("Segoe UI", 9, FontStyle.Regular);
        }
    }

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _orderTimer?.Stop();
        _computerTimer?.Stop();
        _alertTimer?.Stop();
        _server?.Stop();
    }
}

// Computer Model for UI display
public class ComputerModel
{
    public int ComputerId { get; set; }
    public string? ComputerName { get; set; }
    public string? IpAddress { get; set; }
    public ComputerStatus Status { get; set; }
    public string? CurrentUser { get; set; }
    public long Balance { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastSeen { get; set; }
}

public enum ComputerStatus
{
    Available,
    InUse,
    Offline,
    Maintenance
}
