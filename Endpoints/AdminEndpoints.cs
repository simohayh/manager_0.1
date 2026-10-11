using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class MenuItemRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public string Category { get; set; } = "Food";
    public string Dietary { get; set; } = "Non-Vegan";
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public class SettingsRequest
{
    public string RestaurantName { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = string.Empty;
    public bool StoreIpAddresses { get; set; }
    public bool RequireTableKey { get; set; } = true;
    public decimal MaxCashierDiscountPercent { get; set; } = 10;
    public decimal TaxPercent { get; set; }
    public string? TaxId { get; set; }
    public string? ReceiptFooter { get; set; }
}

/// <summary>
/// Owner-only admin API: menu editor, settings, sales report, backup download.
/// Everything under /api/admin requires the owner login (checked by one middleware in Program.cs).
/// /api/config is public: it only holds the restaurant name and currency symbol.
/// </summary>
public static class AdminEndpoints
{
    // These match the tabs/filters on the customer page.
    private static readonly string[] Categories = { "Food", "Beverages" };
    private static readonly string[] DietaryOptions = { "Vegan", "Vegetarian", "Non-Vegan" };

    private const int MaxImageBytes = 2 * 1024 * 1024;
    private const int MaxReportDays = 366;
    private const int MaxReportRows = 50000;

    public static void MapAdminEndpoints(this WebApplication app)
    {
        app.MapGet("/api/config", GetConfig);

        app.MapGet("/api/admin/menu", ListMenu);
        app.MapPost("/api/admin/menu", CreateItem);
        app.MapPut("/api/admin/menu/{id:int}", UpdateItem);
        app.MapDelete("/api/admin/menu/{id:int}", DeleteItem);
        app.MapDelete("/api/admin/menu", DeleteAllItems);
        app.MapPost("/api/admin/menu/image", UploadImage);

        app.MapGet("/api/admin/settings", GetSettings);
        app.MapPut("/api/admin/settings", SaveSettings);
        app.MapGet("/api/admin/table-keys", TableKeys);

        app.MapGet("/api/admin/report", Report);
        app.MapGet("/api/admin/report.csv", ReportCsv);
        app.MapGet("/api/admin/backup", DownloadBackup);
    }

    private static IResult Fail(int status, string message) =>
        Results.Json(new { success = false, message }, statusCode: status);

    // ---------- Public config ----------

    private static IResult GetConfig(SettingsStore settings)
    {
        var s = settings.Current;
        return Results.Ok(new
        {
            restaurantName = s.RestaurantName,
            currencySymbol = s.CurrencySymbol,
            requireTableKey = s.RequireTableKey,
            maxDiscountPercent = s.MaxCashierDiscountPercent,
            taxPercent = s.TaxPercent,
            taxId = s.TaxId,
            receiptFooter = s.ReceiptFooter
        });
    }

    // ---------- Menu editor ----------

    private static async Task<IResult> ListMenu(AppDbContext db) =>
        Results.Ok(await db.MenuItems.OrderBy(m => m.Category).ThenBy(m => m.Name).ToListAsync());

    /// <summary>Null when valid; otherwise the message to show the owner.</summary>
    private static string? Validate(MenuItemRequest? r)
    {
        if (r == null) return "Invalid request.";

        string name = (r.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > 100) return "Name is required (up to 100 characters).";
        if (r.Price <= 0 || r.Price > 100000) return "Price must be more than 0.";
        if ((r.Description ?? string.Empty).Length > 500) return "Description is too long (500 characters max).";
        if (!Categories.Contains(r.Category)) return "Pick a category.";
        if (!DietaryOptions.Contains(r.Dietary)) return "Pick a dietary type.";

        string image = (r.ImageUrl ?? string.Empty).Trim();
        if (image.Length > 255) return "Image address is too long.";
        bool okImage = image.Length == 0
            || (image.StartsWith("/uploads/", StringComparison.Ordinal) && !image.Contains("..") && !image.Contains('\\'))
            || image.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || image.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        if (!okImage) return "Image must be an uploaded file or an http(s) address.";

        return null;
    }

    private static void Apply(MenuItem item, MenuItemRequest r)
    {
        item.Name = r.Name.Trim();
        item.Price = decimal.Round(r.Price, 2);
        item.Description = (r.Description ?? string.Empty).Trim();
        item.Category = r.Category;
        item.Dietary = r.Dietary;
        item.ImageUrl = (r.ImageUrl ?? string.Empty).Trim();
        item.IsAvailable = r.IsAvailable;
    }

    private static async Task<IResult> CreateItem(AppDbContext db, MenuItemRequest request)
    {
        if (Validate(request) is { } error) return Fail(400, error);

        var item = new MenuItem();
        Apply(item, request);
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }

    private static async Task<IResult> UpdateItem(AppDbContext db, int id, MenuItemRequest request)
    {
        if (Validate(request) is { } error) return Fail(400, error);

        var item = await db.MenuItems.FindAsync(id);
        if (item == null) return Fail(404, "Item not found.");

        Apply(item, request);
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }

    /// <summary>Orders keep their own copy of the name and price, so past orders and reports are unaffected.</summary>
    private static async Task<IResult> DeleteItem(AppDbContext db, int id)
    {
        var item = await db.MenuItems.FindAsync(id);
        if (item == null) return Results.Ok(new { success = true });

        db.MenuItems.Remove(item);
        await db.SaveChangesAsync();
        return Results.Ok(new { success = true });
    }

    private static async Task<IResult> DeleteAllItems(AppDbContext db)
    {
        db.MenuItems.RemoveRange(db.MenuItems);
        await db.SaveChangesAsync();
        return Results.Ok(new { success = true });
    }

    /// <summary>The browser sends the raw image as the request body. The type is decided from the file's own bytes.</summary>
    private static async Task<IResult> UploadImage(HttpContext context, IWebHostEnvironment env)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await context.Request.Body.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > MaxImageBytes) return Fail(413, "Image is too big (2 MB max).");
            buffer.Write(chunk, 0, read);
        }

        byte[] bytes = buffer.ToArray();
        string? extension = DetectImageExtension(bytes);
        if (extension == null) return Fail(400, "Please upload a PNG, JPG or WebP image.");

        string webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        string dir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(dir);

        string fileName = Guid.NewGuid().ToString("N") + extension;
        await File.WriteAllBytesAsync(Path.Combine(dir, fileName), bytes);

        return Results.Ok(new { url = "/uploads/" + fileName });
    }

    private static string? DetectImageExtension(byte[] b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
                           && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return ".webp";
        return null;
    }

    // ---------- Settings ----------

    private static IResult GetSettings(SettingsStore settings)
    {
        var s = settings.Current;
        return Results.Ok(new
        {
            restaurantName = s.RestaurantName,
            currencySymbol = s.CurrencySymbol,
            storeIpAddresses = s.StoreIpAddresses,
            requireTableKey = s.RequireTableKey,
            maxDiscountPercent = s.MaxCashierDiscountPercent,
            taxPercent = s.TaxPercent,
            taxId = s.TaxId,
            receiptFooter = s.ReceiptFooter
        });
    }

    /// <summary>The secret part of each table's QR code, for tables 1..count. The admin page turns these into printable QR codes.</summary>
    private static IResult TableKeys(StaffAuth auth, int? count)
    {
        int n = Math.Clamp(count ?? 10, 1, Limits.MaxTableNumber);
        var list = Enumerable.Range(1, n)
            .Select(t => new { table = t, key = auth.TableKey(t.ToString(CultureInfo.InvariantCulture)) })
            .ToList();
        return Results.Ok(list);
    }

    private static async Task<IResult> SaveSettings(AppDbContext db, SettingsStore settings, ILogger<SettingsStore> log, SettingsRequest request)
    {
        string name = (request?.RestaurantName ?? string.Empty).Trim();
        string currency = (request?.CurrencySymbol ?? string.Empty).Trim();

        if (name.Length == 0 || name.Length > 60) return Fail(400, "Restaurant name is required (up to 60 characters).");
        if (currency.Length == 0 || currency.Length > 8) return Fail(400, "Currency symbol is required (up to 8 characters, e.g. $ or KSh).");

        if (request!.TaxPercent < 0 || request.TaxPercent > 50) return Fail(400, "Tax percent must be between 0 and 50.");
        if (request.MaxCashierDiscountPercent < 0 || request.MaxCashierDiscountPercent > 100) return Fail(400, "Cashier discount limit must be between 0 and 100.");
        string taxId = (request.TaxId ?? string.Empty).Trim();
        string footer = (request.ReceiptFooter ?? string.Empty).Trim();
        if (taxId.Length > 40) return Fail(400, "Tax number is too long (40 characters max).");
        if (footer.Length > 120) return Fail(400, "Receipt footer is too long (120 characters max).");

        bool wasStoringIps = settings.Current.StoreIpAddresses;

        settings.Update(s =>
        {
            s.RestaurantName = name;
            s.CurrencySymbol = currency;
            s.StoreIpAddresses = request.StoreIpAddresses;
            s.RequireTableKey = request.RequireTableKey;
            s.MaxCashierDiscountPercent = decimal.Round(request.MaxCashierDiscountPercent, 2);
            s.TaxPercent = decimal.Round(request.TaxPercent, 2);
            s.TaxId = taxId;
            s.ReceiptFooter = footer;
        });

        // Turning IP storage off also wipes the addresses already saved.
        if (wasStoringIps && !request!.StoreIpAddresses)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync("UPDATE Orders SET IpAddress = '', IpAddressV4 = NULL, IpAddressV6 = NULL");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Could not clear stored IP addresses");
                return Fail(500, "Settings were saved, but the stored IP addresses could not be cleared. See the log.");
            }
        }

        return Results.Ok(new { success = true });
    }

    // ---------- Sales report ----------

    /// <summary>
    /// "from" and "to" are the restaurant's own calendar days (inclusive). tzOffset is the browser's offset from UTC
    /// in minutes (Nairobi = 180) so that a "day" means the local day, not the UTC day.
    /// </summary>
    private static bool TryParseRange(string? from, string? to, int tzOffset, out DateTime fromUtc, out DateTime toUtc, out string error)
    {
        fromUtc = toUtc = default;
        error = string.Empty;

        if (!DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ||
            !DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
        {
            error = "Pick a start and end date.";
            return false;
        }

        if (t < f) { error = "The end date is before the start date."; return false; }
        if ((t - f).TotalDays >= MaxReportDays) { error = "Please pick a range of up to one year."; return false; }

        fromUtc = DateTime.SpecifyKind(f.AddMinutes(-tzOffset), DateTimeKind.Unspecified);
        toUtc = DateTime.SpecifyKind(t.AddDays(1).AddMinutes(-tzOffset), DateTimeKind.Unspecified);
        return true;
    }

    private static async Task<List<Order>> LoadSettledOrders(AppDbContext db, DateTime fromUtc, DateTime toUtc) =>
        await db.Orders
            .Where(o => o.Status == OrderStatus.Completed && o.SettledAt != null && o.SettledAt >= fromUtc && o.SettledAt < toUtc)
            .OrderBy(o => o.SettledAt)
            .Take(MaxReportRows)
            .ToListAsync();

    private static string LocalDay(Order o, int tzOffset) => o.SettledAt!.Value.AddMinutes(tzOffset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<IResult> Report(AppDbContext db, string? from, string? to, int tzOffset = 0)
    {
        tzOffset = Math.Clamp(tzOffset, -840, 840);
        if (!TryParseRange(from, to, tzOffset, out var fromUtc, out var toUtc, out var error)) return Fail(400, error);

        var orders = await LoadSettledOrders(db, fromUtc, toUtc);

        // One "bill" = one table settling together (same table, same payment token, same day).
        string BillKey(Order o) => LocalDay(o, tzOffset) + "|" + o.TableNumber + "|" + (o.PaymentToken ?? string.Empty);

        // Discount lines (negative price, ADJ- code) reduce revenue but are not dishes: keep them out of item counts, best sellers and bill counts.
        var dishes = orders.Where(o => !o.IsAdjustment()).ToList();
        decimal discounts = -orders.Where(o => o.IsAdjustment()).Sum(o => o.Price * o.Quantity);

        decimal revenue = orders.Sum(o => o.Price * o.Quantity);
        int bills = dishes.Select(BillKey).Distinct().Count();

        var daily = orders
            .GroupBy(o => LocalDay(o, tzOffset))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                date = g.Key,
                revenue = g.Sum(o => o.Price * o.Quantity),
                bills = g.Where(o => !o.IsAdjustment()).Select(BillKey).Distinct().Count(),
                items = g.Where(o => !o.IsAdjustment()).Sum(o => o.Quantity)
            })
            .ToList();

        var top = dishes
            .GroupBy(o => o.ItemName)
            .Select(g => new { name = g.Key, quantity = g.Sum(o => o.Quantity), revenue = g.Sum(o => o.Price * o.Quantity) })
            .OrderByDescending(x => x.quantity).ThenByDescending(x => x.revenue)
            .Take(10)
            .ToList();

        return Results.Ok(new
        {
            from,
            to,
            revenue,
            bills,
            items = dishes.Sum(o => o.Quantity),
            discounts,
            averageBill = bills == 0 ? 0m : decimal.Round(revenue / bills, 2),
            daily,
            top,
            truncated = orders.Count >= MaxReportRows
        });
    }

    private static async Task<IResult> ReportCsv(AppDbContext db, string? from, string? to, int tzOffset = 0)
    {
        tzOffset = Math.Clamp(tzOffset, -840, 840);
        if (!TryParseRange(from, to, tzOffset, out var fromUtc, out var toUtc, out var error)) return Fail(400, error);

        var orders = await LoadSettledOrders(db, fromUtc, toUtc);

        var sb = new StringBuilder();
        sb.AppendLine("Settled at,Table,Order code,Item,Quantity,Unit price,Line total,Notes,Payment token");
        foreach (var o in orders)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                Csv(o.SettledAt!.Value.AddMinutes(tzOffset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                Csv(o.TableNumber),
                Csv(o.OrderCode),
                Csv(o.ItemName),
                o.Quantity.ToString(CultureInfo.InvariantCulture),
                o.Price.ToString("0.00", CultureInfo.InvariantCulture),
                (o.Price * o.Quantity).ToString("0.00", CultureInfo.InvariantCulture),
                Csv(o.Notes),
                Csv(o.PaymentToken ?? string.Empty)
            }));
        }

        // The BOM makes Excel read accents and symbols correctly.
        byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return Results.File(bytes, "text/csv; charset=utf-8", $"sales-{from}-to-{to}.csv");
    }

    /// <summary>Quotes a CSV cell, and neutralises "=cmd|..." style formulas that spreadsheets would otherwise run.</summary>
    private static string Csv(string value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // ---------- Backup download ----------

    private static async Task<IResult> DownloadBackup(AppDbContext db, ILogger<SettingsStore> log, CancellationToken ct)
    {
        string temp = Path.Combine(Path.GetTempPath(), "restaurant-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            await DatabaseBackup.WriteAsync(db, temp, ct);

            // The temp copy is deleted as soon as the download finishes.
            var stream = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
            return Results.File(stream, "application/octet-stream", $"restaurant-backup-{DateTime.Now:yyyyMMdd-HHmm}.db");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Backup download failed");
            try { if (File.Exists(temp)) File.Delete(temp); } catch { /* ignore */ }
            return Fail(500, "Could not create a backup. See the log.");
        }
    }
}
