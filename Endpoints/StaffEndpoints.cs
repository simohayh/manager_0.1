using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
<<<<<<< HEAD
using System.Security.Cryptography;
=======
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Endpoints used by the kitchen and cashier dashboards.
/// Everything under /api/kitchen requires a kitchen login and everything under /api/cashier a cashier login;
/// that check lives in one middleware in Program.cs (see StaffAuth).
/// </summary>
public static class StaffEndpoints
{
    public static void MapStaffEndpoints(this WebApplication app)
    {
        // Kitchen
        app.MapGet("/api/kitchen/orders", GetKitchenOrders);
        app.MapPost("/api/kitchen/start-cooking", StartCooking);
        app.MapPost("/api/kitchen/out-of-stock", MarkOutOfStock);
        app.MapPost("/api/kitchen/update-status", UpdateStatus);

        // Cashier
        app.MapGet("/api/cashier/orders", GetCashierOrders);
        app.MapPost("/api/cashier/void-item", VoidItem);
        app.MapPost("/api/cashier/discount", ApplyDiscount);
        app.MapPost("/api/cashier/remove-discount", RemoveDiscount);

        // Three aliases for the same action (lambdas keep the auto-generated endpoint names unique).
        foreach (var route in new[] { "/api/cashier/verify-payment", "/api/cashier/verify", "/api/cashier/settle" })
<<<<<<< HEAD
            app.MapPost(route, (AppDbContext db, HttpContext context, SettingsStore settings, ILogger<Program> log) => SettleTable(db, context, settings, log));
=======
            app.MapPost(route, (AppDbContext db, HttpContext context, ILogger<Program> log) => SettleTable(db, context, log));
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
    }

    // ---------- Kitchen ----------

    private static async Task<IResult> GetKitchenOrders(AppDbContext db)
    {
        var orders = await db.Orders
            .Where(o => o.Status != OrderStatus.Delivered
                     && o.Status != OrderStatus.Completed
                     && o.Status != OrderStatus.OutOfStock
                     && o.Status != OrderStatus.Voided)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Results.Ok(orders.Select(o => o.ToView()).ToList());
    }

    // What the kitchen may change, and from which states. Anything else is rejected.
    private static readonly string[] KitchenWorkStates = { OrderStatus.Pending, OrderStatus.Cooking, OrderStatus.Ready };

    private static Task<IResult> StartCooking(AppDbContext db, KitchenActionRequest request) =>
        UpdateKitchenOrder(db, request?.OrderCode, new { success = true }, order =>
        {
            if (order.Status != OrderStatus.Pending) return "Only a waiting order can be started.";
            if (request!.PrepMinutes < 1 || request.PrepMinutes > Limits.MaxPrepMinutes) return "Invalid prep time.";

            order.Status = OrderStatus.Cooking;
            order.PrepMinutes = request.PrepMinutes;
            order.CookingStartedAt = DateTime.UtcNow;
            return null;
        });

    private static Task<IResult> MarkOutOfStock(AppDbContext db, KitchenActionRequest request) =>
        UpdateKitchenOrder(db, request?.OrderCode, new { success = true, message = "Item marked as out of stock." }, order =>
        {
            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Cooking)
                return "This order can't be marked out of stock any more.";

            order.Status = OrderStatus.OutOfStock;
            return null;
        });

    /// <summary>Kitchen undo / ready buttons. Delivered and Completed are set by the customer and cashier flows, never here.</summary>
    private static Task<IResult> UpdateStatus(AppDbContext db, ConfirmDeliveryRequest request) =>
        UpdateKitchenOrder(db, request?.OrderCode, new { success = true }, order =>
        {
            if (!KitchenWorkStates.Contains(request!.NewStatus)) return "Invalid status.";
            if (!KitchenWorkStates.Contains(order.Status)) return "This order can no longer be changed by the kitchen.";

            order.Status = request.NewStatus;
            return null;
        });

    /// <summary>Shared flow: validate the code, load the order, apply a change (returns an error text, or null), save.</summary>
    private static async Task<IResult> UpdateKitchenOrder(AppDbContext db, string? orderCode, object successBody, Func<Order, string?> applyChange)
    {
        if (string.IsNullOrEmpty(orderCode))
            return Results.BadRequest(new { message = "Invalid request payload." });

        var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderCode == orderCode);
        if (order == null)
            return Results.NotFound(new { message = "Order not found." });

        string? error = applyChange(order);
        if (error != null)
            return Results.Json(new { success = false, message = error }, statusCode: 409);

        await db.SaveChangesAsync();

        return Results.Ok(successBody);
    }

    // ---------- Cashier ----------

    private static async Task<IResult> GetCashierOrders(AppDbContext db)
    {
        var orders = await db.Orders
            .NotCompleted()
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Results.Ok(orders.Select(o => o.ToView()).ToList());
    }

    /// <summary>Marks a table's (or token's) awaiting-verification orders as completed and paid.</summary>
<<<<<<< HEAD
    private static async Task<IResult> SettleTable(AppDbContext db, HttpContext context, SettingsStore settings, ILogger<Program> log)
=======
    private static async Task<IResult> SettleTable(AppDbContext db, HttpContext context, ILogger<Program> log)
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
    {
        try
        {
            var body = await context.Request.ReadFromJsonAsync<Dictionary<string, object>>();
            var orders = body == null
                ? new List<Order>()
                : await FindOrdersAwaitingVerificationAsync(db, body);

            if (!orders.Any())
                return Results.BadRequest(new { success = false, message = "No orders awaiting verification found for this table or token." });

            foreach (var order in orders)
            {
                order.Status = OrderStatus.Completed;
                order.PaymentStatus = PaymentState.Paid;
                order.SettledAt = DateTime.UtcNow;
            }

            // Discount lines on these tables are settled together with the items (and share their payment token for reports).
            string? billToken = orders.Select(o => o.PaymentToken).FirstOrDefault(t => !string.IsNullOrEmpty(t));
            var included = new HashSet<int>(orders.Select(o => o.Id));
            foreach (var tableNo in orders.Select(o => o.TableNumber).Distinct().ToList())
            {
                var discountLines = await db.Orders.ForTable(tableNo).NotCompleted().Adjustments().ToListAsync();
                foreach (var line in discountLines)
                {
                    if (!included.Add(line.Id)) continue;
                    line.Status = OrderStatus.Completed;
                    line.PaymentStatus = PaymentState.Paid;
                    line.PaymentToken = billToken;
                    line.SettledAt = DateTime.UtcNow;
                    orders.Add(line);
                }
            }

            await db.SaveChangesAsync();

            // Receipt numbers go 1, 2, 3... (kept in settings.json). They are printed on the receipt and written to the log.
            int receiptNo = 0;
            try
            {
                settings.Update(s => { receiptNo = s.NextReceiptNumber; s.NextReceiptNumber = receiptNo + 1; });
            }
            catch (Exception ex)
            {
                receiptNo = 0; // the table IS settled; only the receipt number could not be saved
                log.LogError(ex, "Could not save the next receipt number to settings.json");
            }
            log.LogInformation("Receipt {Receipt}: table {Table} settled, {Count} item line(s)", receiptNo, orders[0].TableNumber, orders.Count);

            return Results.Ok(new { success = true, message = "Table settled successfully!", receiptNumber = receiptNo });
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { success = false, message = "Invalid request." });
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { success = false, message = "Invalid request." });
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Settling a table failed");
            return Results.Json(new { success = false, message = "Something went wrong while settling. Please try again." }, statusCode: 500);
        }
    }

    // ---------- Void an item / discounts ----------

    private static IResult Fail(int status, string message, bool needsOwnerPin = false) =>
        Results.Json(new { success = false, message, needsOwnerPin }, statusCode: status);

    /// <summary>One line, trimmed, short enough for the log and the receipt.</summary>
    private static string CleanReason(string? text)
    {
        string t = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return t.Length > Limits.MaxReasonLength ? t[..Limits.MaxReasonLength] : t;
    }

    /// <summary>Removes ONE item line from a table's bill (wrong item, complaint...). A reason is required and is written to the log.</summary>
    private static async Task<IResult> VoidItem(AppDbContext db, ILogger<Program> log, VoidItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.OrderCode) || request.OrderCode.StartsWith(OrderCodes.AdjustmentPrefix, StringComparison.Ordinal))
            return Fail(400, "Invalid order code.");

        string reason = CleanReason(request.Reason);
        if (reason.Length < 3) return Fail(400, "Please type a short reason for removing this item.");

        var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderCode == request.OrderCode);
        if (order == null) return Fail(404, "Order not found.");
        if (order.Status == OrderStatus.Completed || order.PaymentStatus == PaymentState.Paid)
            return Fail(409, "This item is already paid and settled.");
        if (order.Status == OrderStatus.Voided || order.Status == OrderStatus.OutOfStock)
            return Fail(409, "This item was already removed.");

        string table = order.TableNumber;
        order.Status = OrderStatus.Voided;
        await db.SaveChangesAsync();

        // If nothing real is left on the table, drop its discount line too so the table is free again.
        bool anyItemsLeft = await db.Orders.ForTable(table).NotCompleted().RealItems().AnyAsync();
        if (!anyItemsLeft)
        {
            var leftovers = await db.Orders.ForTable(table).NotCompleted().Adjustments().ToListAsync();
            if (leftovers.Count > 0)
            {
                db.Orders.RemoveRange(leftovers);
                await db.SaveChangesAsync();
            }
        }

        log.LogWarning("VOID: {Qty}x {Item} ({Amount}) removed from table {Table}, order {Code}. Reason: {Reason}",
            order.Quantity, order.ItemName, (order.Price * order.Quantity).ToString("0.00", CultureInfo.InvariantCulture), table, order.OrderCode, reason);

        return Results.Ok(new { success = true, message = "Item removed from the bill." });
    }

    private static async Task<string> NewAdjustmentCodeAsync(AppDbContext db)
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        string code;
        do
        {
            var chars = new char[8];
            for (int i = 0; i < chars.Length; i++) chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            code = OrderCodes.AdjustmentPrefix + new string(chars);
        }
        while (await db.Orders.AnyAsync(o => o.OrderCode == code));
        return code;
    }

    /// <summary>
    /// Gives the table a discount (replacing any earlier one). It is stored as one extra bill line with a negative price.
    /// Up to the owner's limit the cashier can do it alone; bigger discounts need the owner PIN. Always logged with a reason.
    /// </summary>
    private static async Task<IResult> ApplyDiscount(HttpContext context, AppDbContext db, SettingsStore settings, StaffAuth auth,
        LoginLimiter limiter, ILogger<Program> log, DiscountRequest request)
    {
        if (request == null || !OrderQueries.TryParseTable(request.Table, out string table))
            return Fail(400, "Invalid table.");

        string type = (request.Type ?? string.Empty).Trim().ToLowerInvariant();
        if (type != "percent" && type != "amount") return Fail(400, "Choose percent or amount.");

        string reason = CleanReason(request.Reason);
        if (reason.Length < 3) return Fail(400, "Please type a short reason for the discount.");

        if (request.Value <= 0) return Fail(400, "Enter a discount above 0.");
        if (type == "percent" && request.Value > 100) return Fail(400, "A discount can't be more than 100%.");

        var active = await db.Orders.ForTable(table).NotCompleted().ToListAsync();
        var items = active.Where(o => !o.IsAdjustment()).ToList();
        if (items.Count == 0) return Fail(409, "This table has no items to discount.");

        decimal subtotal = items.Sum(o => o.Price * o.Quantity);
        decimal amount = type == "percent"
            ? decimal.Round(subtotal * request.Value / 100m, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(request.Value, 2, MidpointRounding.AwayFromZero);

        if (amount <= 0) return Fail(400, "That discount is too small to apply.");
        if (amount > subtotal) return Fail(400, "The discount can't be more than the bill.");

        decimal limit = settings.Current.MaxCashierDiscountPercent;
        bool needsOwner = amount > decimal.Round(subtotal * limit / 100m, 2, MidpointRounding.AwayFromZero);
        if (needsOwner)
        {
            string key = LoginLimiter.KeyFor(context, "owner");
            if (limiter.IsLocked(key))
                return Fail(429, "Too many wrong PINs. Please wait 15 minutes and try again.", needsOwnerPin: true);

            if (!auth.CheckPin(StaffRoles.Owner, request.OwnerPin))
            {
                if (!string.IsNullOrEmpty(request.OwnerPin)) limiter.RecordFailure(key);
                return Fail(403, string.IsNullOrEmpty(request.OwnerPin)
                    ? $"Discounts above {limit.ToString("0.##", CultureInfo.InvariantCulture)}% of the bill need the owner's PIN."
                    : "Wrong owner PIN.", needsOwnerPin: true);
            }
            limiter.Reset(key);
        }

        var first = items[0];
        db.Orders.RemoveRange(active.Where(o => o.IsAdjustment()));   // one discount per table: the new one replaces the old
        db.Orders.Add(new Order
        {
            OrderCode = await NewAdjustmentCodeAsync(db),
            ItemName = type == "percent" ? $"Discount {request.Value.ToString("0.##", CultureInfo.InvariantCulture)}%" : "Discount",
            Price = -amount,
            Quantity = 1,
            TableNumber = table,
            Notes = reason,
            Status = OrderStatus.Delivered,
            PaymentStatus = first.PaymentStatus,
            PaymentToken = first.PaymentToken,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        log.LogWarning("DISCOUNT: {Amount} off table {Table} (bill {Subtotal}){Owner}. Reason: {Reason}",
            amount.ToString("0.00", CultureInfo.InvariantCulture), table, subtotal.ToString("0.00", CultureInfo.InvariantCulture),
            needsOwner ? " [owner PIN used]" : string.Empty, reason);

        return Results.Ok(new { success = true, amount, message = "Discount applied." });
    }

    private static async Task<IResult> RemoveDiscount(AppDbContext db, ILogger<Program> log, TableRequest request)
    {
        if (request == null || !OrderQueries.TryParseTable(request.Table, out string table))
            return Fail(400, "Invalid table.");

        var lines = await db.Orders.ForTable(table).NotCompleted().Adjustments().ToListAsync();
        if (lines.Count == 0) return Results.Ok(new { success = true });

        db.Orders.RemoveRange(lines);
        await db.SaveChangesAsync();
        log.LogWarning("DISCOUNT REMOVED from table {Table}", table);
        return Results.Ok(new { success = true });
    }

    private static async Task<List<Order>> FindOrdersAwaitingVerificationAsync(AppDbContext db, Dictionary<string, object> body)
    {
        // The dashboards send the table as "query"; older callers used "tableNumber" / "table".
        string? table = FirstValue(body, "tableNumber", "table", "query");
        string? token = FirstValue(body, "paymentToken", "token");

        var awaiting = db.Orders
            .NotCompleted()
            .Where(o => o.PaymentStatus == PaymentState.AwaitingVerification);

        if (!string.IsNullOrEmpty(table))
            return await awaiting.ForTable(table).ToListAsync();

        if (!string.IsNullOrEmpty(token))
            return await awaiting.Where(o => o.PaymentToken == token).ToListAsync();

        return new List<Order>();
    }

    private static string? FirstValue(Dictionary<string, object> body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (body.TryGetValue(key, out var value) && value != null)
                return value.ToString();
        }
        return null;
    }
}
