using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SharedModels.Models;
using Dapper;

namespace ServerAdmin;

public partial class Form1 : Form
{
    private NetworkServer? _server;

    // UI Components
    private Panel? pnlMain;
    private Panel? pnlHeader;
    private Panel? pnlSidebar;
    private Label? lblTitle;
    private TableLayoutPanel? tblComputers;
    
    // Sidebar Buttons
    private Button? btnSettings;
    private Button? btnComputerMgmt;
    private Button? btnAccounts;
    private Button? btnServices;

    // Colors
    private readonly Color ColorMainBg = Color.FromArgb(243, 244, 246);    // #F3F4F6
    private readonly Color ColorHeaderBg = Color.White;
    private readonly Color ColorSidebar = Color.FromArgb(44, 62, 80);      // #2C3E50
    private readonly Color ColorSidebarLogo = Color.FromArgb(26, 37, 47);  // #1A252F
    private readonly Color ColorButtonNormal = Color.FromArgb(52, 73, 94);
    private readonly Color ColorButtonHover = Color.FromArgb(75, 100, 130);
    private readonly Color ColorText = Color.White;
    private readonly Color ColorAvailable = Color.FromArgb(39, 174, 96);   // #27AE60
    private readonly Color ColorInUse = Color.FromArgb(231, 76, 60);       // #E74C3C
    private readonly Color ColorOffline = Color.FromArgb(189, 195, 199);   // #BDC3C7

    // Mock Data
    private List<ComputerModel>? mockComputers;

    public Form1()
    {
        InitializeComponent();
        
        this.Text = "ServerAdmin - Quản Lý Quán Network";
        this.Size = new Size(1024, 768);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Font = new Font("Segoe UI", 10);
        this.BackColor = ColorMainBg;
        this.DoubleBuffered = true;

        InitializeMockData();
        SetupUI();
        
        this.Load += Form1_Load;
        this.FormClosing += Form1_FormClosing;
    }

    private void InitializeMockData()
    {
        mockComputers = new List<ComputerModel>();
        
        // 12 máy trạm mẫu
        var computers = new[]
        {
            ("Máy 01", ComputerStatus.Available, "", 700000),
            ("Máy 02", ComputerStatus.Available, "", 500000),
            ("Máy 03", ComputerStatus.InUse, "User_Admin", 1200000),
            ("Máy 04", ComputerStatus.Offline, "", 0),
            ("Máy 05", ComputerStatus.Offline, "", 0),
            ("Máy 06", ComputerStatus.InUse, "User_Test", 800000),
            ("Máy 07", ComputerStatus.Available, "", 550000),
            ("Máy 08", ComputerStatus.Available, "", 450000),
            ("Máy 09", ComputerStatus.Available, "", 400000),
            ("Máy 10", ComputerStatus.Available, "", 1000000),
            ("Máy 11", ComputerStatus.Available, "", 600000),
            ("Máy 12", ComputerStatus.Available, "", 600000)
        };

        for (int i = 0; i < computers.Length; i++)
        {
            var (name, status, user, balance) = computers[i];
            mockComputers.Add(new ComputerModel
            {
                ComputerId = i + 1,
                ComputerName = name,
                IpAddress = $"192.168.1.{100 + i}",
                Status = status,
                CurrentUser = user,
                Balance = balance,
                IsActive = status != ComputerStatus.Offline,
                LastSeen = DateTime.Now.AddMinutes(-Random.Shared.Next(0, 120))
            });
        }
    }

    private void SetupUI()
    {
        this.Controls.Clear();

        // ============ BƯỚC 1: TẠO MAIN PANEL (Dock = Fill) ============
        pnlMain = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorMainBg,
            AutoScroll = false
        };
        this.Controls.Add(pnlMain);

        // ============ BƯỚC 2: TẠO HEADER PANEL (Dock = Top) ============
        pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = ColorHeaderBg,
            Padding = new Padding(20)
        };

        lblTitle = new Label
        {
            Text = "Quản Lý Máy Trạm",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 51, 51),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        pnlHeader.Controls.Add(lblTitle);
        this.Controls.Add(pnlHeader);

        // ============ BƯỚC 3: TẠO SIDEBAR PANEL (Dock = Left) ============
        pnlSidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 230,
            BackColor = ColorSidebar,
            Padding = new Padding(0)
        };

        // ======== 3.1: NÚT SETTINGS (ở dưới cùng) - Add trước ========
        btnSettings = CreateSidebarButton("⚙ Cài Đặt", DockStyle.Bottom);
        pnlSidebar.Controls.Add(btnSettings);

        // ======== 3.2: LOGO PANEL ========
        Panel pnlLogo = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = ColorSidebarLogo
        };
        Label lblLogo = new Label
        {
            Text = "ServerAdmin",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = ColorText,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
        pnlLogo.Controls.Add(lblLogo);
        pnlSidebar.Controls.Add(pnlLogo);

        // ======== 3.3: MENU BUTTONS (Dock = Top, thêm từ dưới lên để thứ tự chuẩn) ========
        
        btnServices = CreateSidebarButton("📦 Dịch Vụ", DockStyle.Top);
        pnlSidebar.Controls.Add(btnServices);

        btnAccounts = CreateSidebarButton("👤 Tài Khoản", DockStyle.Top);
        pnlSidebar.Controls.Add(btnAccounts);

        btnComputerMgmt = CreateSidebarButton("🖥 Quản Lý Máy Trạm", DockStyle.Top);
        btnComputerMgmt.Click += (s, e) => ShowComputerManagement();
        pnlSidebar.Controls.Add(btnComputerMgmt);

        // Add Sidebar và BringToFront
        this.Controls.Add(pnlSidebar);
        pnlHeader.BringToFront();
        pnlSidebar.BringToFront();

        // ============ BƯỚC 4: TẠO TABLELAYOUTPANEL 2 CỘT ============
        tblComputers = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = ColorMainBg,
            Padding = new Padding(20),
            ColumnCount = 2,
            RowCount = 0
        };

        tblComputers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tblComputers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        pnlMain.Controls.Add(tblComputers);

        // Show Computer Management
        ShowComputerManagement();
    }

    private Button CreateSidebarButton(string text, DockStyle dockStyle)
    {
        Button btn = new Button
        {
            Text = text,
            AutoSize = false,
            Dock = dockStyle,
            Height = 50,
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
        btn.MouseLeave += (s, e) => btn.BackColor = ColorButtonNormal;

        return btn;
    }

    private void ShowComputerManagement()
    {
        if (tblComputers == null) return;

        tblComputers.Controls.Clear();
        tblComputers.RowCount = 0;

        if (mockComputers != null)
        {
            int row = 0;
            for (int i = 0; i < mockComputers.Count; i++)
            {
                ComputerCard card = new ComputerCard(mockComputers[i]);

                int col = i % 2;
                if (col == 0)
                {
                    tblComputers.RowCount++;
                    row = tblComputers.RowCount - 1;
                }

                tblComputers.Controls.Add(card, col, row);
            }
        }
    }

    private void Form1_Load(object sender, EventArgs e)
    {
        try
        {
            _server = new NetworkServer();
            _server.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khởi động server: {ex.Message}");
        }
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
        _server?.Stop();
    }
}
