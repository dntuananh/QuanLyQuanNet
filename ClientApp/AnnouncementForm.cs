using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClientApp;

public sealed class AnnouncementForm : Form
{
    private readonly System.Windows.Forms.Timer _closeTimer;

    public AnnouncementForm(string message, int durationSeconds)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(44, 62, 80);
        Width = Screen.PrimaryScreen?.WorkingArea.Width ?? 800;
        Height = 60;
        Location = new Point(0, 0);
        Opacity = 0.95;

        var lbl = new Label
        {
            Text = $"📢 {message}",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(40, 0, 40, 0),
            AutoEllipsis = true,
        };

        var closeBtn = new Button
        {
            Text = "✕",
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            ForeColor = Color.FromArgb(200, 200, 200),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Size = new Size(28, 28),
            Location = new Point(Width - 36, 16),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        closeBtn.Click += (_, _) => Close();

        Controls.Add(lbl);
        Controls.Add(closeBtn);

        _closeTimer = new System.Windows.Forms.Timer { Interval = durationSeconds * 1000 };
        _closeTimer.Tick += (_, _) => { _closeTimer.Stop(); Close(); };
        _closeTimer.Start();

        Shown += (_, _) => BringToFront();
    }
}
