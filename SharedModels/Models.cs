namespace SharedModels.Models
{
    public class NetworkMessage
    {
        public string? Action { get; set; }
        public string? Payload { get; set; }
    }

    public class LoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public decimal Balance { get; set; }
        public string Role { get; set; } // "Admin" or "Client"
    }

    public class Computer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Status { get; set; } // "Available", "InUse", "Offline"
        public int? CurrentUserId { get; set; }
    }

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
    }

    public class Order
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ComputerId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } // "Pending", "Delivered", "Cancelled"
        public string Time { get; set; }
    }

    public class OrderItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderRequest
    {
        public int UserId { get; set; }
        public int ComputerId { get; set; }
        public List<OrderItemRequest> Items { get; set; } = new();
    }

    public class ChatRequest
    {
        public int UserId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PasswordChangeRequest
    {
        public int UserId { get; set; }
        public string NewPassword { get; set; } = string.Empty;
    }

    public class Session
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ComputerId { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public decimal Cost { get; set; }
    }

    public class SessionRecoveryData
    {
        public int TimeRemainingSeconds { get; set; }
        public decimal Balance { get; set; }
    }
}
