using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClientApp
{
    public partial class RegisterControl : UserControl
    {
        private TextBox txtName;
        private TextBox txtPassword;
        private TextBox txtConfirmPassword;
        private TextBox txtEmail;
        private CheckBox chkTerms;
        private Button btnRegister;
        private LinkLabel lnkLogin;

        public event Action OnLoginClick;

        public RegisterControl()
        {
            InitializeComponent();
            SetupUI();
        }

        private void SetupUI()
        {
            this.BackColor = Color.Transparent;
            this.Size = new Size(400, 500);

            // Name
            txtName = new TextBox
            {
                Location = new Point(50, 50),
                Size = new Size(300, 30),
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10)
            };
            txtName.GotFocus += (s, e) => { if (txtName.Text == "Tên tài khoản") txtName.Text = ""; };
            txtName.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtName.Text)) txtName.Text = "Tên tài khoản"; };
            txtName.Text = "Tên tài khoản";
            this.Controls.Add(txtName);

            // Password
            txtPassword = new TextBox
            {
                Location = new Point(50, 100),
                Size = new Size(300, 30),
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10),
                UseSystemPasswordChar = true
            };
            txtPassword.GotFocus += (s, e) => { if (txtPassword.Text == "Mật khẩu") txtPassword.Text = ""; };
            txtPassword.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtPassword.Text)) txtPassword.Text = "Mật khẩu"; };
            txtPassword.Text = "Mật khẩu";
            this.Controls.Add(txtPassword);

            // Confirm Password
            txtConfirmPassword = new TextBox
            {
                Location = new Point(50, 150),
                Size = new Size(300, 30),
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10),
                UseSystemPasswordChar = true
            };
            txtConfirmPassword.GotFocus += (s, e) => { if (txtConfirmPassword.Text == "Nhập lại mật khẩu") txtConfirmPassword.Text = ""; };
            txtConfirmPassword.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtConfirmPassword.Text)) txtConfirmPassword.Text = "Nhập lại mật khẩu"; };
            txtConfirmPassword.Text = "Nhập lại mật khẩu";
            this.Controls.Add(txtConfirmPassword);

            // Email/Phone
            txtEmail = new TextBox
            {
                Location = new Point(50, 200),
                Size = new Size(300, 30),
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10)
            };
            txtEmail.GotFocus += (s, e) => { if (txtEmail.Text == "Email/Số điện thoại") txtEmail.Text = ""; };
            txtEmail.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtEmail.Text)) txtEmail.Text = "Email/Số điện thoại"; };
            txtEmail.Text = "Email/Số điện thoại";
            this.Controls.Add(txtEmail);

            // Checkbox
            chkTerms = new CheckBox
            {
                Text = "Tôi đồng ý với điều khoản sử dụng",
                Location = new Point(50, 250),
                AutoSize = true,
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            this.Controls.Add(chkTerms);

            // Register Button
            btnRegister = new Button
            {
                Text = "Đăng ký",
                Location = new Point(50, 300),
                Size = new Size(300, 40),
                BackColor = Color.FromArgb(0, 255, 255), // Neon Cyan
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            btnRegister.FlatAppearance.BorderSize = 0;
            btnRegister.MouseEnter += (s, e) => btnRegister.BackColor = Color.FromArgb(0, 200, 200);
            btnRegister.MouseLeave += (s, e) => btnRegister.BackColor = Color.FromArgb(0, 255, 255);
            this.Controls.Add(btnRegister);

            // Link to Login
            lnkLogin = new LinkLabel
            {
                Text = "Đã có tài khoản? Đăng nhập",
                Location = new Point(50, 360),
                AutoSize = true,
                LinkColor = Color.Cyan,
                VisitedLinkColor = Color.Cyan
            };
            lnkLogin.Click += (s, e) => OnLoginClick?.Invoke();
            this.Controls.Add(lnkLogin);
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
            this.Name = "RegisterControl";
            this.Size = new System.Drawing.Size(400, 500);
        }
    }
}