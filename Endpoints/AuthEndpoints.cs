using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

/// <summary>Staff PIN login / logout and a "who am I" check used by the dashboards.</summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/staff/login", Login);
        app.MapPost("/api/staff/logout", Logout);
        app.MapGet("/api/staff/me", Me);
    }

    private static IResult Login(HttpContext context, StaffAuth auth, LoginLimiter limiter, StaffLoginRequest? req)
    {
        string role = (req?.Role ?? string.Empty).Trim().ToLowerInvariant();
        if (!StaffRoles.IsValid(role))
            return Results.BadRequest(new { success = false, message = "Unknown role." });

        string key = LoginLimiter.KeyFor(context, role);
        if (limiter.IsLocked(key))
            return Results.Json(new { success = false, message = "Too many wrong PINs. Please wait 15 minutes and try again." }, statusCode: 429);

        if (!auth.CheckPin(role, req?.Pin))
        {
            limiter.RecordFailure(key);
            return Results.Json(new { success = false, message = "Incorrect PIN." }, statusCode: 401);
        }

        limiter.Reset(key);
        auth.SignIn(context, role);
        return Results.Ok(new { success = true, role });
    }

    private static IResult Logout(HttpContext context, StaffAuth auth, StaffLogoutRequest? req)
    {
        auth.SignOut(context, (req?.Role ?? string.Empty).Trim().ToLowerInvariant());
        return Results.Ok(new { success = true });
    }

    private static IResult Me(HttpContext context, StaffAuth auth, string? role)
    {
        string r = (role ?? string.Empty).Trim().ToLowerInvariant();
        return auth.IsAuthenticated(context, r)
            ? Results.Ok(new { authenticated = true, role = r })
            : Results.Json(new { authenticated = false }, statusCode: 401);
    }
}
