using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharedModels.Models;

namespace ClientApp
{
    public partial class AuthContainerForm : Form
    {
        private Panel mainPanel;
        private LoginControl loginControl;
        private NetworkClient _networkClient;
        private int _computerId;
        private KeyboardBlocker? _keyboardBlocker;

        private const string AdminUsername = "admin";
        private const string AdminPassword = "admin123";

        public AuthContainerForm()
        {
            InitializeComponent();
            this.Load += AuthContainerForm_Load;
        }

        private async void AuthContainerForm_Load(object sender, EventArgs e)
        {
            InitializeForm();
            await SetupUI();
            ShowLogin();
        }

        private void InitializeForm()
        {
            this.BackColor = Color.FromArgb(18, 22, 30);
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.Manual;
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Normal;
            this.Bounds = Screen.PrimaryScreen?.Bounds ?? Bounds;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;
            this.KeyPreview = true;

            _keyboardBlocker = KeyboardBlocker.Start();

            FormClosing += AuthContainerForm_FormClosing;
            KeyDown += AuthContainerForm_KeyDown;
        }

        private void AuthContainerForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                return;
            }
            _keyboardBlocker?.Dispose();
        }

        private void AuthContainerForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Alt && e.KeyCode == Keys.F4)
            {
                e.SuppressKeyPress = true;
            }
            if (e.Control && e.Shift && e.KeyCode == Keys.X)
            {
                _keyboardBlocker?.Dispose();
                Application.Exit();
            }
        }

        private async Task SetupUI()
        {
            mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 22, 30),
            };
            this.Controls.Add(mainPanel);

            _networkClient = new NetworkClient();
            bool connected = await _networkClient.ConnectAsync("127.0.0.1", 5000);
            if (!connected)
            {
                MessageBox.Show("Không thể kết nối đến Server!", "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            _computerId = await SendIdentifyAsync();

            loginControl = new LoginControl(_networkClient);

            loginControl.OnLoginSuccess += (user, remainingSeconds, balance, computerName) =>
            {
                _keyboardBlocker?.Dispose();
                _keyboardBlocker = null;
                var widget = new WidgetForm(_networkClient, user);
                widget.SetComputerId(_computerId, computerName);
                widget.UpdateTimeFromServer(remainingSeconds, balance);
                widget.FormClosed += (_, _) =>
                {
                    _keyboardBlocker?.Dispose();
                    _keyboardBlocker = KeyboardBlocker.Start();
                    ShowLogin();
                    this.Show();
                };
                widget.Show();
                this.Hide();
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var brush = new SolidBrush(Color.FromArgb(8, 0, 0, 0)))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        private async Task<int> SendIdentifyAsync()
        {
            var tcs = new TaskCompletionSource<int>();
            var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));

            Action<NetworkMessage> handler = null!;
            handler = (msg) =>
            {
                if (msg.Action == "IdentifyResponse")
                {
                    _networkClient.OnMessageReceived -= handler;
                    try
                    {
                        using var doc = JsonDocument.Parse(msg.Payload);
                        if (doc.RootElement.TryGetProperty("ComputerId", out var compProp))
                        {
                            tcs.TrySetResult(compProp.GetInt32());
                        }
                        else
                        {
                            tcs.TrySetResult(0);
                        }
                    }
                    catch
                    {
                        tcs.TrySetResult(0);
                    }
                }
            };

            _networkClient.OnMessageReceived += handler;
            cts.Token.Register(() => { _networkClient.OnMessageReceived -= handler; tcs.TrySetResult(0); });

            await _networkClient.SendMessageAsync(new NetworkMessage
            {
                Action = "Identify",
                Payload = Environment.MachineName
            });

            int computerId = await tcs.Task;
            return computerId;
        }

        private void ShowLogin()
        {
            mainPanel.Controls.Clear();

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 120,
                BackColor = Color.FromArgb(22, 27, 38),
            };

            var lblTitle = new Label
            {
                Text = "QUAN LY QUAN NET",
                Font = new Font("Segoe UI", 26, FontStyle.Bold),
                ForeColor = Color.FromArgb(57, 255, 20),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(22, 27, 38),
            };
            headerPanel.Controls.Add(lblTitle);

            var lblSubtitle = new Label
            {
                Text = "HE THONG QUAN LY TAI KHOAN",
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = Color.FromArgb(130, 140, 160),
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(22, 27, 38),
            };
            headerPanel.Controls.Add(lblSubtitle);

            var linePanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 3,
                BackColor = Color.FromArgb(57, 255, 20),
            };
            headerPanel.Controls.Add(linePanel);

            var btnExit = new Button
            {
                Text = "✕",
                Size = new Size(32, 32),
                Location = new Point(headerPanel.Width - 40, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.Click += (_, _) =>
            {
                _keyboardBlocker?.Dispose();
                Application.Exit();
            };
            headerPanel.Controls.Add(btnExit);

            mainPanel.Controls.Add(headerPanel);

            var centerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 22, 30),
            };

            loginControl.Size = new Size(400, 500);
            loginControl.Location = new Point(
                (centerPanel.Width - loginControl.Width) / 2,
                (centerPanel.Height - loginControl.Height) / 2);
            loginControl.Anchor = AnchorStyles.None;
            centerPanel.Controls.Add(loginControl);

            centerPanel.Resize += (_, _) =>
            {
                loginControl.Location = new Point(
                    Math.Max(0, (centerPanel.Width - loginControl.Width) / 2),
                    Math.Max(0, (centerPanel.Height - loginControl.Height) / 2));
            };

            mainPanel.Controls.Add(centerPanel);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Name = "AuthContainerForm";
            this.Text = "Authentication";
            this.ResumeLayout(false);
        }

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
}
