using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Single-device lock for the kitchen and cashier dashboards.
/// One device per role at a time; another device can take over with a logout code.
/// Every call needs a staff login for that same role (this lock is not authentication by itself).
/// </summary>
public static class SessionEndpoints
{
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(10);

    // Keyed by role ("kitchen" / "cashier"). In-memory only: resets when the app restarts.
    private static readonly Dictionary<string, DeviceSessionState> Sessions =
        new(StringComparer.OrdinalIgnoreCase);

    public static void MapSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/session/register", Register);
        app.MapPost("/api/session/logout", Logout);
        app.MapPost("/api/session/claim", Claim);
    }

    /// <summary>Called every few seconds by an open dashboard; says whether it may stay active.</summary>
    private static IResult Register(HttpContext context, StaffAuth auth, SessionDeviceRequest req)
    {
        if (req == null || string.IsNullOrEmpty(req.Role) || string.IsNullOrEmpty(req.DeviceId))
            return Results.BadRequest(new { success = false, message = "Invalid registration parameters." });

        string role = req.Role.ToLowerInvariant();
        if (Denied(context, auth, role) is { } denied) return denied;

        lock (Sessions)
        {
            return RegisterLocked(role, req);
        }
    }

    private static IResult RegisterLocked(string role, SessionDeviceRequest req)
    {
        // Nobody active yet: this device takes the lock.
        if (!Sessions.TryGetValue(role, out var current))
        {
            Sessions[role] = new DeviceSessionState { DeviceId = req.DeviceId, LastHeartbeat = DateTime.UtcNow };
            return Results.Ok(new { allowed = true });
        }

        // Same device: refresh its heartbeat.
        if (current.DeviceId == req.DeviceId)
        {
            current.LastHeartbeat = DateTime.UtcNow;
            return Results.Ok(new { allowed = true });
        }

        // Other device went quiet: treat it as disconnected and take over.
        if (DateTime.UtcNow - current.LastHeartbeat > HeartbeatTimeout)
        {
            current.DeviceId = req.DeviceId;
            current.LastHeartbeat = DateTime.UtcNow;
            current.LogoutCode = null;
            return Results.Ok(new { allowed = true });
        }

        return Results.Ok(new { allowed = false, message = "Another device is currently active on this dashboard." });
    }

    /// <summary>Releases the lock and returns a one-time code for the next device.</summary>
    private static IResult Logout(HttpContext context, StaffAuth auth, SessionLogoutRequest req)
    {
        if (req == null || string.IsNullOrEmpty(req.Role))
            return Results.BadRequest(new { success = false });

        string role = req.Role.ToLowerInvariant();
        if (Denied(context, auth, role) is { } denied) return denied;

        string logoutCode = "OUT-" + RandomNumberGenerator.GetInt32(1000, 10000);

        lock (Sessions)
        {
            if (Sessions.TryGetValue(role, out var session))
            {
                session.LogoutCode = logoutCode;
                session.DeviceId = string.Empty;
            }
            else
            {
                Sessions[role] = new DeviceSessionState { DeviceId = string.Empty, LogoutCode = logoutCode };
            }
        }

        return Results.Ok(new { success = true, logoutCode });
    }

    /// <summary>Takes over a released dashboard using the logout code.</summary>
    private static IResult Claim(HttpContext context, StaffAuth auth, SessionClaimRequest req)
    {
        if (req == null || string.IsNullOrEmpty(req.Role) || string.IsNullOrEmpty(req.Code))
            return Results.BadRequest(new { success = false, message = "Invalid parameters." });

        string role = req.Role.ToLowerInvariant();
        if (Denied(context, auth, role) is { } denied) return denied;

        lock (Sessions)
        {
            if (Sessions.TryGetValue(role, out var session) && session.LogoutCode == req.Code)
            {
                session.DeviceId = req.DeviceId;
                session.LogoutCode = null;
                session.LastHeartbeat = DateTime.UtcNow;
                return Results.Ok(new { success = true });
            }
        }

        return Results.BadRequest(new { success = false, message = "Incorrect handover code." });
    }

    /// <summary>Null when the caller is logged in as <paramref name="role"/>; otherwise the error to return.</summary>
    private static IResult? Denied(HttpContext context, StaffAuth auth, string role)
    {
        if (!StaffRoles.IsValid(role))
            return Results.BadRequest(new { success = false, message = "Unknown role." });

        return auth.IsAuthenticated(context, role)
            ? null
            : Results.Json(new { success = false, message = "Staff login required." }, statusCode: 401);
    }
}
