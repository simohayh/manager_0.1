using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class MenuItem
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Dietary { get; set; } = string.Empty;

    [MaxLength(255)]
    public string ImageUrl { get; set; } = string.Empty;

    public bool IsAvailable { get; set; } = true;
}

public class Order
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string OrderCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ItemName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }

    [Required, MaxLength(20)]
    public string TableNumber { get; set; } = string.Empty;

    [MaxLength(255)]
    public string ImageUrl { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Status { get; set; } = "Pending";

    [MaxLength(30)]
    public string PaymentStatus { get; set; } = "Unpaid";

    [MaxLength(50)]
    public string? PaymentToken { get; set; }

    [MaxLength(100)]
    public string IpAddress { get; set; } = string.Empty; // Combined/fallback representation

    [MaxLength(45)]
    public string? IpAddressV4 { get; set; }

    [MaxLength(45)]
    public string? IpAddressV6 { get; set; }

    public int? PrepMinutes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CookingStartedAt { get; set; }
    public DateTime? SettledAt { get; set; }
}