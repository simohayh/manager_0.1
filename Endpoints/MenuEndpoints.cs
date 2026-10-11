using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

public static class MenuEndpoints
{
    private const int MaxTableNumber = 50;

    public static void MapMenuEndpoints(this WebApplication app)
    {
        app.MapGet("/api/menu", GetMenu);
        app.MapGet("/api/tables/validate", ValidateTable);
        app.MapGet("/api/tables/status/{tableNumber}", GetTableStatus);
        app.MapPost("/api/tables/check", CheckTable);
    }

    private static async Task<IResult> GetMenu(AppDbContext db, string? search, string? category, string? dietary)
    {
        // Items the owner switched off are hidden from customers.
        var query = db.MenuItems.Where(m => m.IsAvailable);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(m => m.Name.ToLower().Contains(term) || m.Description.ToLower().Contains(term));
        }

        if (!string.IsNullOrEmpty(category) && category != "All")
        {
            var cat = category.ToLower();

            // "Hot Beverages" / "Cold Beverages" both match any beverage category.
            query = cat.Contains("beverage")
                ? query.Where(m => m.Category.ToLower().Contains("beverage"))
                : query.Where(m => m.Category.ToLower() == cat);
        }

        if (!string.IsNullOrEmpty(dietary) && dietary != "All")
        {
            var diet = dietary.ToLower();
            query = query.Where(m => m.Dietary.ToLower() == diet);
        }

        return Results.Ok(await query.ToListAsync());
    }

    private static IResult ValidateTable(int tableNumber)
    {
        if (tableNumber <= 0 || tableNumber > MaxTableNumber)
            return Results.BadRequest(new { success = false, message = "Invalid table number." });

        return Results.Ok(new { success = true, tableNumber });
    }

    /// <summary>Tells whether a table already has active (unsettled) orders.</summary>
    private static async Task<IResult> GetTableStatus(AppDbContext db, string tableNumber)
    {
        bool isTaken = await db.Orders.ForTable(tableNumber).NotCompleted().RealItems().AnyAsync();
        return Results.Ok(new { tableNumber = OrderQueries.CleanTableNumber(tableNumber), isTaken });
    }

    /// <summary>"Can I order at this table?" for the customer page: free, yours, or taken (by another guest).</summary>
    private static async Task<IResult> CheckTable(AppDbContext db, TableCheckRequest request)
    {
        if (request == null || !OrderQueries.TryParseTable(request.Table, out string table))
            return Results.BadRequest(new { success = false, message = "Invalid table number." });

        var codes = (request.Codes ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c) && c.Length <= 20)
            .Take(Limits.MaxCodesPerRequest)
            .ToList();

        var (state, awaitingPayment) = await OrderQueries.TableStateAsync(db, table, codes);
        return Results.Ok(new { table, state, awaitingPayment });
    }
}
