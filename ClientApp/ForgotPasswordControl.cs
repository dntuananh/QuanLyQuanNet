using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClientApp
{
    public partial class ForgotPasswordControl : UserControl
    {
        private TextBox txtEmail;
        private Button btnSend;
        private ProgressBar progressBar;
        private LinkLabel lnkBack;

        public event Action OnBackToLogin;

        public ForgotPasswordControl()
        {
            InitializeComponent();
            SetupUI();
        }

        private void SetupUI()
        {
            this.BackColor = Color.FromArgb(30, 30, 50);
            this.Size = new Size(400, 500);

            // Email/Username
            txtEmail = new TextBox
            {
                Location = new Point(50, 100),
                Size = new Size(300, 30),
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10)
            };
            txtEmail.GotFocus += (s, e) => { if (txtEmail.Text == "Email hoặc Username") txtEmail.Text = ""; };
            txtEmail.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtEmail.Text)) txtEmail.Text = "Email hoặc Username"; };
            txtEmail.Text = "Email hoặc Username";
            this.Controls.Add(txtEmail);

            // Send Button
            btnSend = new Button
            {
                Text = "Gửi yêu cầu",
                Location = new Point(50, 150),
                Size = new Size(300, 40),
                BackColor = Color.FromArgb(0, 255, 255), // Neon Cyan
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.MouseEnter += (s, e) => btnSend.BackColor = Color.FromArgb(0, 200, 200);
            btnSend.MouseLeave += (s, e) => btnSend.BackColor = Color.FromArgb(0, 255, 255);
            btnSend.Click += BtnSend_Click;
            this.Controls.Add(btnSend);

            // Progress Bar
            progressBar = new ProgressBar
            {
                Location = new Point(50, 200),
                Size = new Size(300, 20),
                Visible = false,
                Style = ProgressBarStyle.Marquee
            };
            this.Controls.Add(progressBar);

            // Back to Login
            lnkBack = new LinkLabel
            {
                Text = "Quay lại đăng nhập",
                Location = new Point(50, 250),
                AutoSize = true,
                LinkColor = Color.Cyan,
                VisitedLinkColor = Color.Cyan
            };
            lnkBack.Click += (s, e) => OnBackToLogin?.Invoke();
            this.Controls.Add(lnkBack);
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            progressBar.Visible = true;
            btnSend.Enabled = false;
            // Simulate sending
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 2000 };
            timer.Tick += (s, args) =>
            {
                progressBar.Visible = false;
                btnSend.Enabled = true;
                timer.Stop();
                MessageBox.Show("Yêu cầu đã được gửi!");
            };
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Rounded corners
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddRoundedRectangle(new Rectangle(0, 0, Width, Height), 20);
                this.Region = new Region(path);
            }
        }

        private void InitializeComponent()
        {
            this.Name = "ForgotPasswordControl";
            this.Size = new System.Drawing.Size(400, 500);
        }
    }
}