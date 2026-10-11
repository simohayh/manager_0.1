using Microsoft.AspNetCore.Builder;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Logging: console + a daily file in the "logs" folder next to the database ---
builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(DataPaths.DataDirectory(builder.Configuration), "logs")));

// --- Services ---
// In production point this at the persistent volume, e.g. ConnectionStrings__DefaultConnection="Data Source=/data/restaurant.db"
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=restaurant.db"));

builder.Services.AddSingleton<StaffAuth>();      // staff PIN login (settings are checked at startup, see below)
builder.Services.AddSingleton<LoginLimiter>();
builder.Services.AddSingleton<SettingsStore>();     // owner-editable settings (settings.json)
builder.Services.AddHostedService<DbBackupService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Fail fast: in Production the app won't start without the staff PINs and Auth:Secret.
app.Services.GetRequiredService<StaffAuth>();

// --- Database: apply migrations and seed the menu on startup ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.EnsureSeededAsync(db, scope.ServiceProvider.GetRequiredService<SettingsStore>());
}

// --- Middleware ---
// Any unexpected error: log it (with details) and tell the browser only "something went wrong".
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { success = false, message = "Something went wrong. Please try again." });
}));

// Behind Fly's proxy: learn that the original request was HTTPS (needed for the Secure cookie flag).
var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto };
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    await next();
});

// Staff APIs: deny by default. /api/kitchen needs a kitchen login, /api/cashier a cashier login, /api/admin the owner login.
// (The device-lock endpoints under /api/session check the role in the request themselves.)
app.Use(async (context, next) =>
{
    string? role =
        context.Request.Path.StartsWithSegments("/api/kitchen") ? StaffRoles.Kitchen :
        context.Request.Path.StartsWithSegments("/api/cashier") ? StaffRoles.Cashier :
        context.Request.Path.StartsWithSegments("/api/admin") ? StaffRoles.Owner : null;

    if (role != null && !context.RequestServices.GetRequiredService<StaffAuth>().IsAuthenticated(context, role))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { message = "Staff login required." });
        return;
    }

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- Endpoints ---
app.MapAuthEndpoints();     // staff PIN login / logout
app.MapMenuEndpoints();     // menu + table checks
app.MapOrderEndpoints();    // customer ordering, tracking, payment
app.MapStaffEndpoints();    // kitchen + cashier dashboards
app.MapAdminEndpoints();    // owner: menu editor, settings, reports, backup download (+ public /api/config)
app.MapSessionEndpoints();  // single-device lock + handover codes

app.Run();
