using System;
using System.Collections.Generic;

// --- Request payloads ---

public class CreateOrderRequest
{
    public string TableNumber { get; set; } = string.Empty;

    /// <summary>The secret from the table's QR code (checked unless the owner turned that off).</summary>
    public string? TableKey { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();
    public string? PaymentToken { get; set; }

    /// <summary>
    /// Codes of orders this phone already placed. If the table is in use, only the guest who owns one of its
    /// active orders (proved by knowing the unguessable code) may add more. Everyone else is turned away.
    /// </summary>
    public List<string>? MyOrderCodes { get; set; }
}

public class TableCheckRequest
{
    public string Table { get; set; } = string.Empty;
    public List<string>? Codes { get; set; }
}

public class OrderItemDto
{
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}

public class TrackBatchRequest
{
    public List<string> Codes { get; set; } = new();
}

/// <summary>Just an order code (customer "confirm delivery" and "remove order").</summary>
public class OrderCodeRequest
{
    public string OrderCode { get; set; } = string.Empty;
}

/// <summary>Kitchen status change. NewStatus is validated against an allow-list on the server.</summary>
public class ConfirmDeliveryRequest
{
    public string OrderCode { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
}

public class StaffLoginRequest
{
    public string Role { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
}

public class StaffLogoutRequest
{
    public string Role { get; set; } = string.Empty;
}

/// <summary>What the browser is allowed to see of an order (no IP addresses, no internal id).</summary>
public record OrderView(
    string OrderCode,
    string ItemName,
    decimal Price,
    int Quantity,
    string TableNumber,
    string ImageUrl,
    string Notes,
    string Status,
    string PaymentStatus,
    string? PaymentToken,
    bool IsPaid,
    int? PrepMinutes,
    DateTime CreatedAt,
    DateTime? CookingStartedAt,
    DateTime? SettledAt);

public static class OrderViewExtensions
{
    public static OrderView ToView(this Order o) => new(
        o.OrderCode, o.ItemName, o.Price, o.Quantity, o.TableNumber, o.ImageUrl, o.Notes,
        o.Status, o.PaymentStatus, o.PaymentToken, o.PaymentStatus == PaymentState.Paid,
        o.PrepMinutes, o.CreatedAt, o.CookingStartedAt, o.SettledAt);
}

<<<<<<< HEAD
public class VoidItemRequest
{
    public string OrderCode { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class DiscountRequest
{
    public string Table { get; set; } = string.Empty;
    /// <summary>"percent" or "amount".</summary>
    public string Type { get; set; } = "percent";
    public decimal Value { get; set; }
    public string? Reason { get; set; }
    /// <summary>Needed when the discount is bigger than the limit set by the owner.</summary>
    public string? OwnerPin { get; set; }
}

public class TableRequest
{
    public string Table { get; set; } = string.Empty;
}

=======
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
public class KitchenActionRequest
{
    public string OrderCode { get; set; } = string.Empty;
    public int PrepMinutes { get; set; } = 15;
}

public class SessionDeviceRequest
{
    public string Role { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
}

public class SessionLogoutRequest
{
    public string Role { get; set; } = string.Empty;
}

public class SessionClaimRequest
{
    public string Role { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

// --- In-memory state ---

public class DeviceSessionState
{
    public string DeviceId { get; set; } = string.Empty;
    public string? LogoutCode { get; set; }
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
}
