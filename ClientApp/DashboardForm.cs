using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClientApp
{
    public partial class DashboardForm : Form
    {
        private Panel topBar;
        private Panel sidebar;
        private Panel mainArea;
        private Panel floatingWidget;
        private Label lblBalance;
        private Label lblTimeUsed;
        private Label lblTimeLeft;
        private ProgressBar pbTime;
        private Button[] menuButtons;
        private FlowLayoutPanel gameCardsPanel;

        public DashboardForm()
        {
            InitializeComponent();
            SetupUI();
        }

        private void SetupUI()
        {
            this.BackColor = Color.FromArgb(20, 20, 30); // Dark Charcoal
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.KeyDown += DashboardForm_KeyDown;

            // Top Bar
            topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(100, 30, 30, 46)
            };
            this.Controls.Add(topBar);

            lblBalance = new Label
            {
                Text = "Số dư: 50.000 VND",
                Location = new Point(20, 15),
                AutoSize = true,
                ForeColor = Color.Cyan,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(30, 30, 46),
            };
            topBar.Controls.Add(lblBalance);

            lblTimeUsed = new Label
            {
                Text = "Đã dùng: 2h 30m",
                Location = new Point(200, 15),
                AutoSize = true,
                ForeColor = Color.Orange,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(30, 30, 46),
            };
            topBar.Controls.Add(lblTimeUsed);

            lblTimeLeft = new Label
            {
                Text = "Còn lại: 1h 30m",
                Location = new Point(350, 15),
                AutoSize = true,
                ForeColor = Color.Orange,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(30, 30, 46),
            };
            topBar.Controls.Add(lblTimeLeft);

            pbTime = new ProgressBar
            {
                Location = new Point(500, 15),
                Size = new Size(300, 20),
                Value = 60, // 60% used
                ForeColor = Color.Orange
            };
            topBar.Controls.Add(pbTime);

            // Sidebar
            sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 80,
                BackColor = Color.FromArgb(100, 30, 30, 46)
            };
            this.Controls.Add(sidebar);

            string[] menuItems = { "🏠", "🎮", "🍔", "🆘", "👤" };
            menuButtons = new Button[menuItems.Length];
            for (int i = 0; i < menuItems.Length; i++)
            {
                menuButtons[i] = new Button
                {
                    Text = menuItems[i],
                    Size = new Size(60, 60),
                    Location = new Point(10, 10 + i * 70),
                    BackColor = Color.FromArgb(50, 50, 70),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 20)
                };
                menuButtons[i].FlatAppearance.BorderSize = 0;
                menuButtons[i].MouseEnter += (s, e) => ((Button)s).BackColor = Color.FromArgb(0, 255, 255);
                menuButtons[i].MouseLeave += (s, e) => ((Button)s).BackColor = Color.FromArgb(50, 50, 70);
                sidebar.Controls.Add(menuButtons[i]);
            }

            // Main Area
            mainArea = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(100, 20, 20, 30)
            };
            this.Controls.Add(mainArea);

            gameCardsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(100, 20, 20, 30)
            };
            mainArea.Controls.Add(gameCardsPanel);

            // Add some game cards
            for (int i = 0; i < 10; i++)
            {
                Panel card = new Panel
                {
                    Size = new Size(200, 150),
                    BackColor = Color.FromArgb(100, 50, 50, 70),
                    Margin = new Padding(10)
                };
                card.MouseEnter += (s, e) => card.BackColor = Color.FromArgb(150, 0, 255, 255);
                card.MouseLeave += (s, e) => card.BackColor = Color.FromArgb(100, 50, 50, 70);

                Label gameName = new Label
                {
                    Text = $"Game {i + 1}",
                    Location = new Point(10, 10),
                    AutoSize = true,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold)
                };
                card.Controls.Add(gameName);

                gameCardsPanel.Controls.Add(card);
            }

            // Floating Widget
            floatingWidget = new Panel
            {
                Size = new Size(250, 100),
                Location = new Point(this.Width - 270, this.Height - 150),
                BackColor = Color.FromArgb(150, 0, 255, 255),
                BorderStyle = BorderStyle.None
            };
            floatingWidget.MouseEnter += (s, e) => floatingWidget.BackColor = Color.FromArgb(200, 0, 255, 255);
            floatingWidget.MouseLeave += (s, e) => floatingWidget.BackColor = Color.FromArgb(150, 0, 255, 255);

            Label lblPending = new Label
            {
                Text = "Đồ ăn chờ: 2 món",
                Location = new Point(10, 10),
                AutoSize = true,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            floatingWidget.Controls.Add(lblPending);

            this.Controls.Add(floatingWidget);
            floatingWidget.BringToFront();
        }

        private void DashboardForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Glassmorphism effect
            using (var brush = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(1200, 800);
            this.Name = "DashboardForm";
            this.Text = "Dashboard";
            this.ResumeLayout(false);
        }
    }
}