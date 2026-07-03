using System;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using SharedModels.Models;

namespace ClientApp;

public sealed class ChatForm : Form
{
    private readonly NetworkClient _client;
    private readonly User _user;
    private readonly int _computerId;

    private ListBox _messageList;
    private TextBox _inputBox;
    private Button _sendBtn;

    public ChatForm(NetworkClient client, User user, int computerId)
    {
        _client = client;
        _user = user;
        _computerId = computerId;

        Text = "Nhan tin";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(20, 24, 32);
        Size = new Size(500, 550);
        MinimumSize = new Size(350, 400);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.FromArgb(20, 24, 32),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var lblHeader = new Label
        {
            Text = "💬 Tro chuyen voi Admin",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(57, 255, 20),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.FromArgb(20, 24, 32),
        };

        _messageList = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(26, 31, 42),
            ForeColor = Color.FromArgb(232, 237, 245),
            Font = new Font("Segoe UI", 10),
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
        };

        var inputPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(20, 24, 32),
        };
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

        _inputBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10),
            BackColor = Color.FromArgb(36, 46, 60),
            ForeColor = Color.FromArgb(232, 237, 245),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 8, 6, 6),
        };

        _sendBtn = new Button
        {
            Text = "Gui",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(57, 255, 20),
            ForeColor = Color.FromArgb(16, 20, 28),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 8, 0, 6),
        };
        _sendBtn.FlatAppearance.BorderSize = 0;
        _sendBtn.Click += SendMessage;

        _inputBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendMessage(s, e);
            }
        };

        inputPanel.Controls.Add(_inputBox, 0, 0);
        inputPanel.Controls.Add(_sendBtn, 1, 0);

        layout.Controls.Add(lblHeader, 0, 0);
        layout.Controls.Add(_messageList, 0, 1);
        layout.Controls.Add(inputPanel, 0, 2);

        Controls.Add(layout);
    }

    public void AddMessage(string from, string message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => AddMessage(from, message)));
            return;
        }
        _messageList.Items.Add($"[{from}] {message}");
        _messageList.TopIndex = _messageList.Items.Count - 1;
    }

    private async void SendMessage(object? sender, EventArgs e)
    {
        var text = _inputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        _inputBox.Clear();

        try
        {
            await _client.SendMessageAsync(new NetworkMessage
            {
                Action = "ClientChat",
                Payload = JsonSerializer.Serialize(new { ComputerId = _computerId, UserId = _user.Id, Message = text })
            });

            AddMessage("Ban", text);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Loi gui tin nhan: {ex.Message}", "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
