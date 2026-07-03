using System;
using System.Collections.Generic;

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

    public class LoginResponse
    {
        public User User { get; set; } = null!;
        public double RemainingSeconds { get; set; }
        public decimal Balance { get; set; }
        public string ComputerName { get; set; } = string.Empty;
    }

    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string Role { get; set; } = string.Empty;
    }

    public class Computer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? CurrentUserId { get; set; }
    }

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class Order
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ComputerId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
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
        public string StartTime { get; set; } = string.Empty;
        public string? EndTime { get; set; }
        public decimal Cost { get; set; }
        public double RemainingSecondsAtCheckpoint { get; set; }
        public string? LastCheckpointTime { get; set; }
    }

    public class HeartbeatPayload
    {
        public int ComputerId { get; set; }
    }

    public class HeartbeatResponse
    {
        public double RemainingSeconds { get; set; }
        public decimal Balance { get; set; }
        public bool TimeUp { get; set; }
    }

    public class SessionRestoreRequest
    {
        public int UserId { get; set; }
        public int ComputerId { get; set; }
    }

    public class SessionRestoreResponse
    {
        public double RemainingSeconds { get; set; }
        public decimal Balance { get; set; }
        public bool SessionFound { get; set; }
    }

    public class ChatMessagePayload
    {
        public int ComputerId { get; set; }
        public int UserId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ComputerName { get; set; } = string.Empty;
    }

    public class AdminChatReplyPayload
    {
        public int ComputerId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class AnnouncementPayload
    {
        public string Message { get; set; } = string.Empty;
        public int DurationSeconds { get; set; }
    }
}
