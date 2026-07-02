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
    // Bảng màu dark + neon thống nhất với giao diện widget.
    private readonly Color _colorBase = Color.FromArgb(20, 24, 32);
    private readonly Color _colorSidebar = Color.FromArgb(26, 31, 42);
    private readonly Color _colorCard = Color.FromArgb(31, 39, 52);
    private readonly Color _colorText = Color.FromArgb(232, 237, 245);
    private readonly Color _colorNeon = Color.FromArgb(57, 255, 20);
    private readonly Color _colorOrange = Color.FromArgb(255, 137, 41);

    private readonly FlowLayoutPanel _productFlow;
    private readonly ListBox _cartList;
    private readonly Label _lblTotal;

    private readonly List<ProductItem> _products;
    private readonly Dictionary<int, int> _cart;
    private readonly NetworkClient? _client;
    private readonly User? _currentUser;

    private string _selectedCategory = "Nuoc giai khat";

    public ServiceWindowForm(NetworkClient? client, User? currentUser)
    {
        _products = BuildSampleProducts();
        _cart = new Dictionary<int, int>();
        _client = client;
        _currentUser = currentUser;

        // Thiết lập thuộc tính dialog popup.
        Text = "Dich vu & Goi mon";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = _colorBase;
        Size = new Size(1150, 700);

        // Dựng layout 3 cột: sidebar - content - cart.
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = _colorBase,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(12),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 295));

        var sidebar = BuildSidebar();
        var mainContent = BuildMainContent(out _productFlow);
        var cartPanel = BuildCartPanel(out _cartList, out _lblTotal);

        root.Controls.Add(sidebar, 0, 0);
        root.Controls.Add(mainContent, 1, 0);
        root.Controls.Add(cartPanel, 2, 0);

        Controls.Add(root);

        // Tải danh sách sản phẩm mặc định khi mở dialog.
        RenderProductsByCategory(_selectedCategory);
    }

    private Control BuildSidebar()
    {
        // Sidebar chứa danh mục món theo yêu cầu bài toán.
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _colorSidebar,
            Padding = new Padding(10),
        };

        var lblTitle = new Label
        {
            Text = "DANH MUC",
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = _colorNeon,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var buttonContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = Color.Transparent,
        };

        buttonContainer.Controls.Add(CreateCategoryButton("Nuoc giai khat"));
        buttonContainer.Controls.Add(CreateCategoryButton("Mi/Pho"));
        buttonContainer.Controls.Add(CreateCategoryButton("Com"));
        buttonContainer.Controls.Add(CreateCategoryButton("The game"));

        panel.Controls.Add(buttonContainer);
        panel.Controls.Add(lblTitle);
        return panel;
    }

    private Button CreateCategoryButton(string category)
    {
        // Nút danh mục tái sử dụng để thay đổi tập sản phẩm ở vùng giữa.
        var button = new Button
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
        };

        button.FlatAppearance.BorderColor = Color.FromArgb(60, 75, 95);
        button.FlatAppearance.BorderSize = 1;
        button.Click += (_, _) =>
        {
            _selectedCategory = category;
            RenderProductsByCategory(_selectedCategory);
        };

        return button;
    }

    private Control BuildMainContent(out FlowLayoutPanel productFlow)
    {
        // Khu vực chính hiển thị card sản phẩm theo dạng wrap.
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(22, 27, 36),
            Padding = new Padding(12),
        };

        var lblTitle = new Label
        {
            Text = "MENU SAN PHAM",
            Dock = DockStyle.Top,
            Height = 40,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = _colorOrange,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        productFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.Transparent,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(2, 8, 2, 2),
        };

        panel.Controls.Add(productFlow);
        panel.Controls.Add(lblTitle);
        return panel;
    }

    private Control BuildCartPanel(out ListBox cartList, out Label lblTotal)
    {
        // Panel phải làm vai trò giỏ hàng và xác nhận đơn.
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(26, 31, 42),
            Padding = new Padding(12),
        };

        var lblTitle = new Label
        {
            Text = "GIO HANG",
            Dock = DockStyle.Top,
            Height = 40,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = _colorNeon,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        cartList = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 24, 32),
            ForeColor = _colorText,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            BorderStyle = BorderStyle.FixedSingle,
        };

        lblTotal = new Label
        {
            Text = "Tong tien: 0 VND",
            Dock = DockStyle.Bottom,
            Height = 38,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = _colorOrange,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var btnConfirm = new Button
        {
            Text = "XAC NHAN DAT HANG",
            Dock = DockStyle.Bottom,
            Height = 48,
            FlatStyle = FlatStyle.Flat,
            BackColor = _colorNeon,
            ForeColor = Color.FromArgb(16, 20, 28),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        btnConfirm.FlatAppearance.BorderSize = 0;
        btnConfirm.Click += BtnConfirm_Click;

        panel.Controls.Add(cartList);
        panel.Controls.Add(lblTotal);
        panel.Controls.Add(btnConfirm);
        panel.Controls.Add(lblTitle);
        return panel;
    }

    private void RenderProductsByCategory(string category)
    {
        // Vẽ lại danh sách card sản phẩm khi user chọn danh mục mới.
        _productFlow.SuspendLayout();
        _productFlow.Controls.Clear();

        var filtered = _products.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        foreach (var product in filtered)
        {
            _productFlow.Controls.Add(CreateProductCard(product));
        }

        _productFlow.ResumeLayout();
    }

    private Control CreateProductCard(ProductItem product)
    {
        // Mỗi card gồm ảnh mô phỏng, tên món, giá và nút + Thêm.
        var card = new Panel
        {
            Width = 185,
            Height = 230,
            Margin = new Padding(6),
            BackColor = _colorCard,
            BorderStyle = BorderStyle.FixedSingle,
        };

        var picture = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 110,
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.FromArgb(45, 58, 76),
            Image = CreatePlaceholderImage(product.Name),
        };

        var lblName = new Label
        {
            Text = product.Name,
            Dock = DockStyle.Top,
            Height = 44,
            ForeColor = _colorText,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var lblPrice = new Label
        {
            Text = $"{product.Price:N0} VND",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = _colorOrange,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var btnAdd = new Button
        {
            Text = "+ Them",
            Dock = DockStyle.Bottom,
            Height = 36,
            BackColor = _colorNeon,
            ForeColor = Color.FromArgb(16, 20, 28),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        btnAdd.FlatAppearance.BorderSize = 0;
        btnAdd.Click += (_, _) => AddToCart(product);

        card.Controls.Add(btnAdd);
        card.Controls.Add(lblPrice);
        card.Controls.Add(lblName);
        card.Controls.Add(picture);
        return card;
    }

    private static Bitmap CreatePlaceholderImage(string text)
    {
        // Sinh ảnh placeholder đơn giản để card có vùng hình trực quan.
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
        // Tăng số lượng món trong giỏ và cập nhật ngay danh sách hiển thị.
        if (_cart.TryGetValue(product.Id, out var quantity))
        {
            _cart[product.Id] = quantity + 1;
        }
        else
        {
            _cart[product.Id] = 1;
        }

        RefreshCartUI();
    }

    private void RefreshCartUI()
    {
        // Đồng bộ ListBox và tổng tiền theo giỏ hàng hiện tại.
        _cartList.Items.Clear();

        decimal total = 0;
        foreach (var item in _cart)
        {
            var product = _products.First(p => p.Id == item.Key);
            var quantity = item.Value;
            var lineTotal = product.Price * quantity;
            total += lineTotal;
            _cartList.Items.Add($"{product.Name} x{quantity} - {lineTotal:N0} VND");
        }

        _lblTotal.Text = $"Tong tien: {total:N0} VND";
    }

    private async void BtnConfirm_Click(object? sender, EventArgs e)
    {
        if (_cart.Count == 0)
        {
            MessageBox.Show("Gio hang dang trong.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_client == null || _currentUser == null)
        {
            MessageBox.Show("Chua ket noi den may chu.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var orderPayload = new
        {
            UserId = _currentUser.Id,
            ComputerId = 0,
            Items = _cart.Select(kv => new { ProductId = kv.Key, Quantity = kv.Value }).ToList()
        };

        await _client.SendMessageAsync(new NetworkMessage
        {
            Action = "Order",
            Payload = JsonSerializer.Serialize(orderPayload)
        });

        MessageBox.Show("Don hang da duoc gui toi may chu.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _cart.Clear();
        RefreshCartUI();
    }

    private static List<ProductItem> BuildSampleProducts()
    {
        // Dữ liệu mẫu để dựng giao diện ngay cả khi chưa kết nối backend.
        return new List<ProductItem>
        {
            new(1, "Coca Cola", 15000, "Nuoc giai khat"),
            new(2, "Pepsi", 15000, "Nuoc giai khat"),
            new(3, "Tra Dao", 22000, "Nuoc giai khat"),
            new(4, "Mi Xao Bo", 35000, "Mi/Pho"),
            new(5, "Pho Bo", 40000, "Mi/Pho"),
            new(6, "Mi Tom Trung", 25000, "Mi/Pho"),
            new(7, "Com Suon", 45000, "Com"),
            new(8, "Com Ga", 42000, "Com"),
            new(9, "The Garena 50K", 50000, "The game"),
            new(10, "The Zing 100K", 100000, "The game"),
            new(11, "The Steam 200K", 200000, "The game"),
        };
    }

    private sealed record ProductItem(int Id, string Name, decimal Price, string Category);
}
