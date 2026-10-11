using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Endpoints used by the customer page.
///
/// Customers don't log in. Their proof of ownership is the (unguessable) order code they were given,
/// and the server decides what each action may do: the browser never chooses a status.
/// </summary>
public static class OrderEndpoints
{
    // No 0/O/1/I/L so codes can be read out loud and typed without mix-ups. 8 characters ≈ 40 bits.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;

    public static void MapOrderEndpoints(this WebApplication app)
    {
        app.MapPost("/api/orders/batch", PlaceBatchOrder);
        app.MapPost("/api/orders/request-payment", RequestPayment);
        app.MapPost("/api/orders/confirm-delivery", ConfirmDelivery);
        app.MapPost("/api/customer/remove-order", RemoveOrder);

        // Same handler on two routes (lambdas keep the auto-generated endpoint names unique).
        app.MapPost("/api/orders/track-batch", (AppDbContext db, TrackBatchRequest request) => TrackOrders(db, request));
        app.MapPost("/api/customer/orders", (AppDbContext db, TrackBatchRequest request) => TrackOrders(db, request));
    }

    private static IResult Fail(int status, string message) =>
        Results.Json(new { success = false, message }, statusCode: status);

    /// <summary>Distinct, non-empty codes, capped so one request can't probe thousands of codes.</summary>
    private static List<string> CleanCodes(IEnumerable<string>? codes) =>
        (codes ?? Enumerable.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c) && c.Length <= 20)
            .Select(c => c.Trim())
            .Distinct()
            .Take(Limits.MaxCodesPerRequest)
            .ToList();

    // ---------- Place order ----------

    /// <summary>Places every cart item as its own order, all-or-nothing.</summary>
<<<<<<< HEAD
    private static async Task<IResult> PlaceBatchOrder(HttpContext httpContext, AppDbContext db, SettingsStore settings, ILogger<Program> log, CreateOrderRequest dto)
=======
    private static async Task<IResult> PlaceBatchOrder(HttpContext httpContext, AppDbContext db, ILogger<Program> log, CreateOrderRequest dto)
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
    {
        if (dto?.Items == null || dto.Items.Count == 0)
            return Fail(400, "Invalid order payload received.");
        if (dto.Items.Count > Limits.MaxItemsPerOrder)
            return Fail(400, "Too many items in one order.");

        if (!OrderQueries.TryParseTable(dto.TableNumber, out string cleanTable))
            return Fail(400, $"Please enter a valid table number (1 to {Limits.MaxTableNumber}).");

        // A table with active orders must be settled before a new customer can order.
        if (await db.Orders.ForTable(cleanTable).NotCompleted().AnyAsync())
        {
            return Fail(400,
                $"⚠️ Table {cleanTable} is currently occupied and has active orders. Please check with the cashier or settle previous bills first.");
<<<<<<< HEAD
=======
        }

        foreach (var item in dto.Items)
        {
            if (item.Quantity < 1 || item.Quantity > Limits.MaxQuantity)
                return Fail(400, $"Quantity must be between 1 and {Limits.MaxQuantity}.");
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
        }

        foreach (var item in dto.Items)
        {
            if (item.Quantity < 1 || item.Quantity > Limits.MaxQuantity)
                return Fail(400, $"Quantity must be between 1 and {Limits.MaxQuantity}.");
        }

        // Privacy: IP addresses are only stored if the owner switched that on in Settings.
        bool storeIp = settings.Current.StoreIpAddresses;
        var ip = storeIp ? ClientIp.Resolve(httpContext) : new ClientIpInfo(null, null);

        using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var createdCodes = new List<string>();

            foreach (var itemDto in dto.Items)
            {
                // Name, price and image always come from the menu, never from the browser.
                var menuItem = await db.MenuItems.FindAsync(itemDto.MenuItemId);
                if (menuItem == null || !menuItem.IsAvailable)
                {
                    await transaction.RollbackAsync();
                    return Fail(400, menuItem == null
                        ? "One of the items in your cart is no longer on the menu."
                        : $"\"{menuItem.Name}\" is not available right now.");
                }

                string notes = (itemDto.Notes ?? string.Empty).Trim();
                if (notes.Length > Limits.MaxNotesLength) notes = notes[..Limits.MaxNotesLength];

                string code = await GenerateUniqueOrderCodeAsync(db, createdCodes);

                db.Orders.Add(new Order
                {
                    OrderCode = code,
                    ItemName = menuItem.Name,
                    Price = menuItem.Price,
                    Quantity = itemDto.Quantity,
                    TableNumber = cleanTable,
                    ImageUrl = menuItem.ImageUrl,
                    Notes = notes,
                    PaymentStatus = PaymentState.Unpaid,
                    Status = OrderStatus.Pending,
                    IpAddress = storeIp ? ip.Combined : string.Empty,
                    IpAddressV4 = ip.V4,
                    IpAddressV6 = ip.V6,
                    CreatedAt = DateTime.UtcNow
                });
                createdCodes.Add(code);
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Results.Ok(new { orderCodes = createdCodes, message = "Cart orders placed successfully!" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            log.LogError(ex, "Placing order for table {Table} failed", cleanTable);
            return Fail(500, "We couldn't place your order. Please try again or ask a member of staff.");
        }
    }

    private static string RandomCode()
    {
        var sb = new StringBuilder("ORD-", 4 + CodeLength);
        for (int i = 0; i < CodeLength; i++)
            sb.Append(CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]);
        return sb.ToString();
    }

    private static async Task<string> GenerateUniqueOrderCodeAsync(AppDbContext db, List<string> codesInThisBatch)
    {
        string code;
        do
        {
            code = RandomCode();
        }
        while (codesInThisBatch.Contains(code) || await db.Orders.AnyAsync(o => o.OrderCode == code));

        return code;
    }

    // ---------- Track ----------

    private static async Task<IResult> TrackOrders(AppDbContext db, TrackBatchRequest request)
    {
        var codes = CleanCodes(request?.Codes);
        if (codes.Count == 0)
            return Results.Ok(new List<OrderView>());

        var orders = await db.Orders
            .Where(o => codes.Contains(o.OrderCode))
            .ToListAsync();

        return Results.Ok(orders.Select(o => o.ToView()).ToList());
    }

    // ---------- Payment request ----------

    /// <summary>
    /// The customer asks to pay: every active order must be delivered first, and all must be for one table.
    /// Repeating the request returns the same token.
    /// </summary>
    private static async Task<IResult> RequestPayment(AppDbContext db, TrackBatchRequest request)
    {
        var codes = CleanCodes(request?.Codes);
        if (codes.Count == 0)
            return Fail(400, "No codes provided");

        var active = await db.Orders
            .Where(o => codes.Contains(o.OrderCode))
            .NotCompleted()
            .ToListAsync();

        if (active.Count == 0)
            return Fail(404, "No active orders found.");

        if (active.Select(o => o.TableNumber).Distinct().Count() > 1)
            return Fail(400, "These orders belong to different tables.");

        if (active.Any(o => o.Status != OrderStatus.Delivered))
            return Fail(409, "You can request payment once every item has been delivered.");

        string token = active.Select(o => o.PaymentToken).FirstOrDefault(t => !string.IsNullOrEmpty(t))
                       ?? await GenerateUniquePaymentTokenAsync(db);

        foreach (var order in active.Where(o => o.PaymentStatus == PaymentState.Unpaid))
        {
            order.PaymentStatus = PaymentState.AwaitingVerification;
            order.PaymentToken = token;
        }

        await db.SaveChangesAsync();
        return Results.Ok(new { paymentToken = token });
    }

    private static async Task<string> GenerateUniquePaymentTokenAsync(AppDbContext db)
    {
        string token;
        do
        {
            token = "TOKEN-" + RandomNumberGenerator.GetInt32(1000, 10000);
        }
        while (await db.Orders.AnyAsync(o => o.PaymentToken == token && o.Status != OrderStatus.Completed));

        return token;
    }

    // ---------- Delivery / removal ----------

    /// <summary>The only thing a customer can do here is confirm that a READY order reached their table.</summary>
    private static async Task<IResult> ConfirmDelivery(AppDbContext db, OrderCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.OrderCode))
            return Fail(400, "Invalid order code.");

        var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderCode == request.OrderCode);
        if (order == null) return Fail(404, "Order not found.");

        if (order.Status == OrderStatus.Delivered)
            return Results.Ok(new { success = true });

        if (order.Status != OrderStatus.Ready)
            return Fail(409, "This order isn't ready yet.");

        order.Status = OrderStatus.Delivered;
        await db.SaveChangesAsync();

        return Results.Ok(new { success = true });
    }

    /// <summary>Customers may only dismiss orders the kitchen marked out of stock.</summary>
    private static async Task<IResult> RemoveOrder(AppDbContext db, OrderCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.OrderCode))
            return Fail(400, "Invalid order code.");

        var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderCode == request.OrderCode);
        if (order == null) return Results.Ok(new { success = true });

        if (order.Status != OrderStatus.OutOfStock)
            return Fail(409, "Only out-of-stock items can be removed.");

        db.Orders.Remove(order);
        await db.SaveChangesAsync();

        return Results.Ok(new { success = true });
    }
}
