using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using SharedModels.Models;

namespace ClientApp;

public sealed class ServiceWindowForm : Form
{
    private readonly Color _colorBase = Color.FromArgb(20, 24, 32);
    private readonly Color _colorSidebar = Color.FromArgb(26, 31, 42);
    private readonly Color _colorCard = Color.FromArgb(31, 39, 52);
    private readonly Color _colorCardDisabled = Color.FromArgb(45, 45, 55);
    private readonly Color _colorText = Color.FromArgb(232, 237, 245);
    private readonly Color _colorTextDim = Color.FromArgb(140, 150, 165);
    private readonly Color _colorNeon = Color.FromArgb(57, 255, 20);
    private readonly Color _colorOrange = Color.FromArgb(255, 137, 41);
    private readonly Color _colorAccent = Color.FromArgb(0, 150, 255);

    private readonly FlowLayoutPanel _productFlow;
    private readonly FlowLayoutPanel _cartPanel;
    private readonly Label _lblTotal;
    private readonly TextBox _txtSearch;
    private readonly Dictionary<string, Button> _categoryButtons = new();
    private readonly Button _btnPlaceOrder;

    private readonly List<ProductItem> _products;
    private readonly Dictionary<int, int> _cart;
    private readonly NetworkClient? _client;
    private readonly User? _currentUser;
    private readonly int _computerId;
    private string _selectedCategory = "Tất cả";
    private bool _useAccountBalance = true;

    public ServiceWindowForm(NetworkClient? client, User? currentUser, int computerId = 0)
    {
        _products = BuildSampleProducts();
        _cart = new Dictionary<int, int>();
        _client = client;
        _currentUser = currentUser;
        _computerId = computerId;

        Text = "Dịch vụ";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = false;
        BackColor = _colorBase;
        Size = new Size(1150, 700);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = _colorBase,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(12),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));

        root.Controls.Add(BuildSidebar(), 0, 0);
        var mainContent = BuildMainContent(out _productFlow, out _txtSearch);
        root.Controls.Add(mainContent, 1, 0);
        root.Controls.Add(BuildCartColumn(out _cartPanel, out _lblTotal, out _btnPlaceOrder), 2, 0);

        Controls.Add(root);
        RenderProducts();
    }

    private Control BuildSidebar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _colorSidebar,
            Padding = new Padding(10),
        };

        panel.Controls.Add(new Label
        {
            Text = "DANH MỤC",
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = _colorNeon,
            TextAlign = ContentAlignment.MiddleCenter,
        });

        var btnContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = _colorSidebar,
        };

        foreach (var cat in new[] { "Tất cả", "Đồ ăn", "Đồ uống", "Thẻ cào" })
        {
            var btn = CreateCategoryButton(cat);
            _categoryButtons[cat] = btn;
            btnContainer.Controls.Add(btn);
        }

        SetActiveCategory("Tất cả");
        panel.Controls.Add(btnContainer);
        return panel;
    }

    private Button CreateCategoryButton(string category)
    {
        var btn = new Button
        {
            Text = category,
            Width = 170,
            Height = 44,
            Margin = new Padding(0, 0, 0, 10),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            BackColor = Color.FromArgb(36, 46, 60),
            ForeColor = _colorText,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(60, 75, 95);
        btn.FlatAppearance.BorderSize = 1;
        btn.Click += (_, _) =>
        {
            _selectedCategory = category;
            SetActiveCategory(category);
            RenderProducts();
        };
        return btn;
    }

    private void SetActiveCategory(string category)
    {
        foreach (var kvp in _categoryButtons)
        {
            bool active = kvp.Key == category;
            kvp.Value.BackColor = active ? _colorAccent : Color.FromArgb(36, 46, 60);
            kvp.Value.ForeColor = active ? Color.White : _colorText;
            kvp.Value.FlatAppearance.BorderColor = active ? Color.FromArgb(0, 200, 255) : Color.FromArgb(60, 75, 95);
        }
    }

    private Control BuildMainContent(out FlowLayoutPanel productFlow, out TextBox txtSearch)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(22, 27, 36),
            Padding = new Padding(12),
        };

        panel.Controls.Add(new Label
        {
            Text = "SẢN PHẨM",
            Dock = DockStyle.Top,
            Height = 40,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = _colorOrange,
            TextAlign = ContentAlignment.MiddleLeft,
        });

        txtSearch = new TextBox
        {
            Text = "🔍 Tìm kiếm...",
            Dock = DockStyle.Top,
            Height = 36,
            Font = new Font("Segoe UI", 10),
            BackColor = Color.FromArgb(36, 46, 60),
            ForeColor = _colorTextDim,
            BorderStyle = BorderStyle.FixedSingle,
        };
        var search = txtSearch;
        search.GotFocus += (_, _) =>
        {
            if (search.Text == "🔍 Tìm kiếm...") { search.Text = ""; search.ForeColor = _colorText; }
        };
        search.LostFocus += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(search.Text)) { search.Text = "🔍 Tìm kiếm..."; search.ForeColor = _colorTextDim; }
        };
        search.TextChanged += (_, _) => RenderProducts();

        productFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(22, 27, 36),
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(2, 8, 2, 2),
        };

        panel.Controls.Add(productFlow);
        panel.Controls.Add(txtSearch);
        return panel;
    }

    private Control BuildCartColumn(out FlowLayoutPanel cartPanel, out Label lblTotal, out Button btnPlaceOrder)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(26, 31, 42),
            Padding = new Padding(12),
        };

        panel.Controls.Add(new Label
        {
            Text = "GIỎ HÀNG",
            Dock = DockStyle.Top,
            Height = 40,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = _colorNeon,
            TextAlign = ContentAlignment.MiddleLeft,
        });

        var paymentPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            BackColor = Color.FromArgb(26, 31, 42),
        };

        var rbAccount = new RadioButton
        {
            Text = "Trừ tài khoản",
            Location = new Point(8, 6),
            Size = new Size(180, 24),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = _colorText,
            Checked = true,
        };
        rbAccount.CheckedChanged += (_, _) => { if (rbAccount.Checked) _useAccountBalance = true; };

        var rbCash = new RadioButton
        {
            Text = "Tiền mặt (COD)",
            Location = new Point(8, 32),
            Size = new Size(180, 24),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = _colorText,
        };
        rbCash.CheckedChanged += (_, _) => { if (rbCash.Checked) _useAccountBalance = false; };

        paymentPanel.Controls.Add(rbAccount);
        paymentPanel.Controls.Add(rbCash);

        btnPlaceOrder = new Button
        {
            Text = "ĐẶT HÀNG",
            Dock = DockStyle.Bottom,
            Height = 48,
            FlatStyle = FlatStyle.Flat,
            BackColor = _colorNeon,
            ForeColor = Color.FromArgb(16, 20, 28),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        btnPlaceOrder.FlatAppearance.BorderSize = 0;
        btnPlaceOrder.Click += BtnPlaceOrder_Click;

        lblTotal = new Label
        {
            Text = "Tổng cộng: 0đ",
            Dock = DockStyle.Bottom,
            Height = 36,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = _colorOrange,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        cartPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.FromArgb(20, 24, 32),
            Padding = new Padding(4),
        };

        panel.Controls.Add(cartPanel);
        panel.Controls.Add(lblTotal);
        panel.Controls.Add(btnPlaceOrder);
        panel.Controls.Add(paymentPanel);
        return panel;
    }

    private void RenderProducts()
    {
        _productFlow.SuspendLayout();
        _productFlow.Controls.Clear();

        string? filter = _txtSearch?.Text.Trim();
        if (filter == "🔍 Tìm kiếm...") filter = null;

        var filtered = _products.AsEnumerable();

        if (_selectedCategory != "Tất cả")
            filtered = filtered.Where(p => p.Category.Equals(_selectedCategory, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(filter))
            filtered = filtered.Where(p => p.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

        foreach (var product in filtered)
            _productFlow.Controls.Add(CreateProductCard(product));

        _productFlow.ResumeLayout();
    }

    private Control CreateProductCard(ProductItem product)
    {
        bool isOutOfStock = product.SoLuongTon <= 0;

        var card = new Panel
        {
            Width = 185,
            Height = 250,
            Margin = new Padding(6),
            BackColor = isOutOfStock ? _colorCardDisabled : _colorCard,
            BorderStyle = BorderStyle.FixedSingle,
        };

        var picture = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 110,
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = isOutOfStock ? Color.FromArgb(55, 55, 65) : Color.FromArgb(45, 58, 76),
            Image = CreatePlaceholderImage(product.Name),
        };

        if (isOutOfStock)
        {
            picture.Controls.Add(new Label
            {
                Text = "HẾT HÀNG",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 50, 50),
                BackColor = Color.FromArgb(120, 0, 0, 0),
            });
        }

        var btnAdd = new Button
        {
            Text = "Thêm",
            Dock = DockStyle.Bottom,
            Height = 36,
            BackColor = isOutOfStock ? Color.FromArgb(60, 60, 70) : _colorNeon,
            ForeColor = isOutOfStock ? _colorTextDim : Color.FromArgb(16, 20, 28),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = isOutOfStock ? Cursors.Default : Cursors.Hand,
            Enabled = !isOutOfStock,
        };
        btnAdd.FlatAppearance.BorderSize = 0;
        btnAdd.Click += (_, _) => AddToCart(product);
        card.Controls.Add(btnAdd);

        card.Controls.Add(new Label
        {
            Text = $"{product.Price:N0}đ",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = isOutOfStock ? _colorTextDim : _colorOrange,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
        });

        card.Controls.Add(new Label
        {
            Text = product.Name,
            Dock = DockStyle.Top,
            Height = 44,
            ForeColor = isOutOfStock ? _colorTextDim : _colorText,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
        });

        card.Controls.Add(picture);
        return card;
    }

    private static Bitmap CreatePlaceholderImage(string text)
    {
        var bmp = new Bitmap(160, 90);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(60, 76, 98));
        using var font = new Font("Segoe UI", 9, FontStyle.Bold);
        using var brush = new SolidBrush(Color.WhiteSmoke);
        var rect = new RectangleF(0, 0, bmp.Width, bmp.Height);
        var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, rect, format);
        return bmp;
    }

    private void AddToCart(ProductItem product)
    {
        if (_cart.TryGetValue(product.Id, out var qty))
            _cart[product.Id] = qty + 1;
        else
            _cart[product.Id] = 1;
        RefreshCartUI();
    }

    private void RemoveFromCart(int productId)
    {
        if (_cart.TryGetValue(productId, out var qty))
        {
            if (qty <= 1) _cart.Remove(productId);
            else _cart[productId] = qty - 1;
        }
        RefreshCartUI();
    }

    private void RefreshCartUI()
    {
        _cartPanel.SuspendLayout();
        _cartPanel.Controls.Clear();

        decimal total = 0;
        foreach (var kvp in _cart)
        {
            var product = _products.FirstOrDefault(p => p.Id == kvp.Key);
            if (product == null) continue;

            int qty = kvp.Value;
            decimal lineTotal = product.Price * qty;
            total += lineTotal;
            _cartPanel.Controls.Add(CreateCartRow(product, qty, lineTotal));
        }

        _lblTotal.Text = $"Tổng cộng: {total:N0}đ";
        _cartPanel.ResumeLayout();
    }

    private Control CreateCartRow(ProductItem product, int quantity, decimal lineTotal)
    {
        var row = new Panel
        {
            Width = 270,
            Height = 44,
            Margin = new Padding(0, 0, 0, 4),
            BackColor = Color.FromArgb(26, 31, 42),
        };

        row.Controls.Add(new Label
        {
            Text = product.Name,
            Location = new Point(4, 2),
            Size = new Size(120, 20),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = _colorText,
        });

        var btnMinus = new Button
        {
            Text = "-",
            Location = new Point(126, 4),
            Size = new Size(28, 22),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 58, 72),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        btnMinus.FlatAppearance.BorderSize = 0;
        btnMinus.Click += (_, _) => RemoveFromCart(product.Id);
        row.Controls.Add(btnMinus);

        row.Controls.Add(new Label
        {
            Text = quantity.ToString(),
            Location = new Point(156, 4),
            Size = new Size(24, 22),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = _colorText,
        });

        var btnPlus = new Button
        {
            Text = "+",
            Location = new Point(182, 4),
            Size = new Size(28, 22),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 58, 72),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        btnPlus.FlatAppearance.BorderSize = 0;
        btnPlus.Click += (_, _) => AddToCart(product);
        row.Controls.Add(btnPlus);

        row.Controls.Add(new Label
        {
            Text = $"{lineTotal:N0}đ",
            Location = new Point(214, 4),
            Size = new Size(56, 22),
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = _colorOrange,
        });

        return row;
    }

    private async void BtnPlaceOrder_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_cart.Count == 0)
            {
                MessageBox.Show("Giỏ hàng đang trống.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var popup = new Form
            {
                Text = "Xác nhận đặt hàng",
                Size = new Size(420, 240),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                BackColor = _colorBase,
                ForeColor = _colorText,
            };

            popup.Controls.Add(new Label
            {
                Text = "Ghi chú cho nhân viên (không bắt buộc):",
                Location = new Point(16, 14),
                Size = new Size(380, 20),
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = _colorText,
            });

            var txtNotes = new TextBox
            {
                Location = new Point(16, 40),
                Size = new Size(376, 80),
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(36, 46, 60),
                ForeColor = _colorText,
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                MaxLength = 200,
            };
            popup.Controls.Add(txtNotes);

            var btnCancel = new Button
            {
                Text = "Hủy",
                Location = new Point(200, 140),
                Size = new Size(90, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = _colorText,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel,
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            popup.Controls.Add(btnCancel);

            var btnConfirm = new Button
            {
                Text = "Xác nhận",
                Location = new Point(300, 140),
                Size = new Size(100, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = _colorNeon,
                ForeColor = Color.FromArgb(16, 20, 28),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK,
            };
            btnConfirm.FlatAppearance.BorderSize = 0;
            popup.Controls.Add(btnConfirm);

            if (popup.ShowDialog(this) != DialogResult.OK) return;

            string? notes = txtNotes.Text.Trim();
            if (string.IsNullOrEmpty(notes)) notes = null;

            if (_client == null || _currentUser == null)
            {
                MessageBox.Show("Chưa kết nối đến máy chủ.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var orderPayload = new
            {
                UserId = _currentUser.Id,
                ComputerId = _computerId > 0 ? _computerId : 0,
                Items = _cart.Select(kv => new { ProductId = kv.Key, Quantity = kv.Value }).ToList(),
                PaymentMethod = _useAccountBalance ? "Account" : "Cash",
                Notes = notes
            };

            await _client.SendMessageAsync(new NetworkMessage
            {
                Action = "Order",
                Payload = JsonSerializer.Serialize(orderPayload)
            });

            MessageBox.Show("Đơn hàng đã được ghi nhận!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _cart.Clear();
            RefreshCartUI();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi đặt hàng: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static List<ProductItem> BuildSampleProducts()
    {
        return new List<ProductItem>
        {
            new(1, "Coca Cola", 15000, "Đồ uống", 10),
            new(2, "Pepsi", 15000, "Đồ uống", 5),
            new(3, "Trà Đào", 22000, "Đồ uống", 0),
            new(4, "Sting dâu", 12000, "Đồ uống", 20),
            new(5, "Mì Xào Bò", 35000, "Đồ ăn", 8),
            new(6, "Phở Bò", 40000, "Đồ ăn", 0),
            new(7, "Mì tôm trứng", 25000, "Đồ ăn", 15),
            new(8, "Cơm Sườn", 45000, "Đồ ăn", 6),
            new(9, "Cơm Gà", 42000, "Đồ ăn", 0),
            new(10, "Trà sữa", 20000, "Đồ uống", 0),
            new(11, "Thẻ Garena 50K", 50000, "Thẻ cào", 20),
            new(12, "Thẻ Zing 100K", 100000, "Thẻ cào", 10),
            new(13, "Thẻ Steam 200K", 200000, "Thẻ cào", 0),
        };
    }

    private sealed record ProductItem(int Id, string Name, decimal Price, string Category, int SoLuongTon);
}