using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClientApp
{
    public partial class LoginControl : UserControl
    {
        private TextBox txtUsername;
        private TextBox txtPassword;
        private Button btnLogin;
        private Button btnShowPassword;
        private LinkLabel lnkForgot;
        private LinkLabel lnkRegister;
        private PictureBox picUsername;
        private PictureBox picPassword;
        public event Action OnRegisterClick;
        public event Action OnForgotClick;
        public event Action OnLoginSuccess;

        public LoginControl()
        {
            InitializeComponent();
            SetupUI();
        }

        private void SetupUI()
        {
            this.BackColor = Color.Transparent;
            this.Size = new Size(400, 500);

            // Username
            picUsername = new PictureBox
            {
                Image = SystemIcons.Information.ToBitmap(), // Placeholder icon
                Size = new Size(24, 24),
                Location = new Point(50, 100)
            };
            this.Controls.Add(picUsername);

            txtUsername = new TextBox
            {
                Location = new Point(80, 95),
                Size = new Size(270, 30),
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10)
            };
            txtUsername.GotFocus += (s, e) => { if (txtUsername.Text == "Username") txtUsername.Text = ""; };
            txtUsername.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtUsername.Text)) txtUsername.Text = "Username"; };
            txtUsername.Text = "Username";
            this.Controls.Add(txtUsername);

            // Password
            picPassword = new PictureBox
            {
                Image = SystemIcons.Shield.ToBitmap(), // Placeholder icon
                Size = new Size(24, 24),
                Location = new Point(50, 150)
            };
            this.Controls.Add(picPassword);

            txtPassword = new TextBox
            {
                Location = new Point(80, 145),
                Size = new Size(270, 30),
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10),
                UseSystemPasswordChar = true
            };
            txtPassword.GotFocus += (s, e) => { if (txtPassword.Text == "Password") txtPassword.Text = ""; };
            txtPassword.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtPassword.Text)) txtPassword.Text = "Password"; };
            txtPassword.Text = "Password";
            this.Controls.Add(txtPassword);

            btnShowPassword = new Button
            {
                Text = "👁",
                Location = new Point(350, 145),
                Size = new Size(30, 30),
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnShowPassword.FlatAppearance.BorderSize = 0;
            btnShowPassword.Click += (s, e) => txtPassword.UseSystemPasswordChar = !txtPassword.UseSystemPasswordChar;
            this.Controls.Add(btnShowPassword);

            // Login Button
            btnLogin = new Button
            {
                Text = "Đăng nhập",
                Location = new Point(80, 200),
                Size = new Size(270, 40),
                BackColor = Color.FromArgb(0, 255, 255), // Neon Cyan
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.MouseEnter += (s, e) => btnLogin.BackColor = Color.FromArgb(0, 200, 200);
            btnLogin.MouseLeave += (s, e) => btnLogin.BackColor = Color.FromArgb(0, 255, 255);
            btnLogin.Click += (s, e) => HandleLogin();
            this.Controls.Add(btnLogin);

            // Links
            lnkForgot = new LinkLabel
            {
                Text = "Quên mật khẩu?",
                Location = new Point(80, 260),
                AutoSize = true,
                LinkColor = Color.Cyan,
                VisitedLinkColor = Color.Cyan
            };
            lnkForgot.Click += (s, e) => OnForgotClick?.Invoke();
            this.Controls.Add(lnkForgot);

            lnkRegister = new LinkLabel
            {
                Text = "Tạo tài khoản mới",
                Location = new Point(80, 280),
                AutoSize = true,
                LinkColor = Color.Cyan,
                VisitedLinkColor = Color.Cyan
            };
            lnkRegister.Click += (s, e) => OnRegisterClick?.Invoke();
            this.Controls.Add(lnkRegister);
        }

        private void HandleLogin()
        {
            var username = txtUsername.Text?.Trim() ?? string.Empty;
            var password = txtPassword.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Vui lòng nhập tài khoản và mật khẩu.", "Lỗi đăng nhập", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ValidateCredentials(username, password))
            {
                MessageBox.Show("Tài khoản hoặc mật khẩu không đúng.", "Lỗi đăng nhập", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtPassword.Clear();
                txtPassword.Focus();
                return;
            }

            OnLoginSuccess?.Invoke();
        }

        private bool ValidateCredentials(string username, string password)
        {
            return validAccounts.TryGetValue(username, out var expectedPassword) && expectedPassword == password;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Rounded corners and shadow
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddRoundedRectangle(new Rectangle(0, 0, Width, Height), 20);
                this.Region = new Region(path);
            }
        }

        private void InitializeComponent()
        {
            this.Name = "LoginControl";
            this.Size = new System.Drawing.Size(400, 500);
        }
    }

    public static class GraphicsExtensions
    {
        public static void AddRoundedRectangle(this GraphicsPath path, Rectangle rect, int radius)
        {
            path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            path.AddArc(rect.X + rect.Width - radius, rect.Y, radius, radius, 270, 90);
            path.AddArc(rect.X + rect.Width - radius, rect.Y + rect.Height - radius, radius, radius, 0, 90);
            path.AddArc(rect.X, rect.Y + rect.Height - radius, radius, radius, 90, 90);
            path.CloseFigure();
        }
    }
}