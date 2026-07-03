using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SharedModels.Models;

namespace ClientApp
{
    public partial class AuthContainerForm : Form
    {
        private Panel mainPanel;
        private LoginControl loginControl;
        private ForgotPasswordControl forgotControl;
        private System.Windows.Forms.Timer transitionTimer;
        private UserControl currentControl;
        private UserControl nextControl;
        private int transitionStep = 0;
        private const int TransitionSteps = 20;
        private NetworkClient _networkClient;

        public AuthContainerForm()
        {
            InitializeComponent();
            this.Load += AuthContainerForm_Load;
        }

        private async void AuthContainerForm_Load(object sender, EventArgs e)
        {
            await SetupUI();
            ShowLogin();
        }

        private async System.Threading.Tasks.Task SetupUI()
        {
            this.BackColor = Color.FromArgb(30, 30, 46); // Deep Navy
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            this.Controls.Add(mainPanel);

            // Initialize network client
            _networkClient = new NetworkClient();
            bool connected = await _networkClient.ConnectAsync("127.0.0.1", 5000);
            if (!connected)
            {
                MessageBox.Show("Không thể kết nối đến Server!", "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }

            loginControl = new LoginControl(_networkClient);
            forgotControl = new ForgotPasswordControl();

            loginControl.OnLoginSuccess += (user) => 
            { 
                this.Hide(); 
                new DashboardForm().Show(); 
            };
            forgotControl.OnBackToLogin += () => TransitionTo(loginControl);
            loginControl.OnForgotClick += () => TransitionTo(forgotControl);

            transitionTimer = new System.Windows.Forms.Timer { Interval = 20 };
            transitionTimer.Tick += TransitionTimer_Tick;
        }

        private void ShowLogin()
        {
            mainPanel.Controls.Clear();
            mainPanel.Controls.Add(loginControl);
            loginControl.Dock = DockStyle.None;
            loginControl.Size = mainPanel.ClientSize;
            loginControl.Location = Point.Empty;
            currentControl = loginControl;
        }

        private void TransitionTo(UserControl newControl)
        {
            if (currentControl == newControl) return;

            nextControl = newControl;
            transitionStep = 0;
            transitionTimer.Start();
        }

        private void TransitionTimer_Tick(object sender, EventArgs e)
        {
            transitionStep++;
            float progress = (float)transitionStep / TransitionSteps;

            if (currentControl != null)
            {
                currentControl.Location = new Point((int)(-mainPanel.Width * progress), 0);
            }

            if (nextControl != null)
            {
                if (!mainPanel.Controls.Contains(nextControl))
                {
                    mainPanel.Controls.Add(nextControl);
                    nextControl.Dock = DockStyle.None;
                    nextControl.Size = mainPanel.ClientSize;
                    nextControl.Location = new Point(mainPanel.Width, 0);
                }
                nextControl.Location = new Point((int)(mainPanel.Width * (1 - progress)), 0);
            }

            if (transitionStep >= TransitionSteps)
            {
                transitionTimer.Stop();
                if (currentControl != null && mainPanel.Controls.Contains(currentControl))
                    mainPanel.Controls.Remove(currentControl);
                currentControl = nextControl;
                nextControl = null;
            }

            mainPanel.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Glassmorphism effect
            using (var brush = new SolidBrush(Color.FromArgb(100, 255, 255, 255)))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Name = "AuthContainerForm";
            this.Text = "Authentication";
            this.ResumeLayout(false);
        }
    }
}