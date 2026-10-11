using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;

/// <summary>Reusable query filters for orders.</summary>
public static class OrderQueries
{
    /// <summary>"Table 5" -> "5".</summary>
    public static string CleanTableNumber(string table) => table.Replace("Table", "").Trim();

    /// <summary>Accepts "5" or "Table 5"; only whole numbers from 1 to <see cref="Limits.MaxTableNumber"/> are valid.</summary>
    public static bool TryParseTable(string? input, out string cleanTable)
    {
        cleanTable = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (!int.TryParse(CleanTableNumber(input), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var n)) return false;
        if (n < 1 || n > Limits.MaxTableNumber) return false;
        cleanTable = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Matches a table however it was stored: "5", "Table 5", or exactly as typed.</summary>
    public static IQueryable<Order> ForTable(this IQueryable<Order> orders, string table)
    {
        var clean = CleanTableNumber(table);
        return orders.Where(o => o.TableNumber == table
                              || o.TableNumber == clean
                              || o.TableNumber == "Table " + clean);
    }

    /// <summary>Orders that are neither settled nor cancelled (out of stock, or voided by the cashier).</summary>
    public static IQueryable<Order> NotCompleted(this IQueryable<Order> orders) =>
        orders.Where(o => o.Status != OrderStatus.Completed
                       && o.Status != OrderStatus.OutOfStock
                       && o.Status != OrderStatus.Voided);

    /// <summary>Only the discount lines (stored as negative-price lines with an ADJ- code).</summary>
    public static IQueryable<Order> Adjustments(this IQueryable<Order> orders) =>
        orders.Where(o => o.OrderCode.StartsWith(OrderCodes.AdjustmentPrefix));

    /// <summary>Only real dishes (no discount lines).</summary>
    public static IQueryable<Order> RealItems(this IQueryable<Order> orders) =>
        orders.Where(o => !o.OrderCode.StartsWith(OrderCodes.AdjustmentPrefix));

    public static bool IsAdjustment(this Order order) =>
        order.OrderCode.StartsWith(OrderCodes.AdjustmentPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Who is allowed to order at a table right now?
    /// "free"  - nobody has unsettled items there.
    /// "yours" - the caller owns an active order at the table (they hold one of its secret order codes).
    /// "taken" - another guest is using it.
    /// </summary>
    public static async Task<(string State, bool AwaitingPayment)> TableStateAsync(
        AppDbContext db, string cleanTable, IEnumerable<string> callerCodes)
    {
        var active = await db.Orders.ForTable(cleanTable).NotCompleted().RealItems().ToListAsync();
        if (active.Count == 0) return ("free", false);

        var mine = new HashSet<string>(callerCodes);
        bool isOccupant = active.Any(o => mine.Contains(o.OrderCode));
        if (!isOccupant) return ("taken", false);

        return ("yours", active.Any(o => o.PaymentStatus == PaymentState.AwaitingVerification));
    }
}
