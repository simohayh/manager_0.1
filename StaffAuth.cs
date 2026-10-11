using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Staff login with a PIN per role (kitchen / cashier).
///
/// A successful login sets an HttpOnly, SameSite=Strict cookie holding a signed token
/// (role + expiry + a fingerprint of the current PIN). Nothing is stored on the server,
/// so restarts and multiple servers don't log anyone out. Changing a PIN or the secret
/// logs everyone out.
///
/// Settings (environment variables use double underscores, e.g. Staff__KitchenPin):
<<<<<<< HEAD
///   Staff:KitchenPin   Staff:CashierPin   Staff:OwnerPin   Auth:Secret
=======
///   Staff:KitchenPin   Staff:CashierPin   Auth:Secret
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
/// In Production the app refuses to start unless all three are set.
/// </summary>
public sealed class StaffAuth
{
    private const int MinPinLength = 4;
    private const int MinSecretLength = 32;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);

    private readonly byte[] _key;
    private readonly string _kitchenPin;
    private readonly string _cashierPin;
<<<<<<< HEAD
    private readonly string _ownerPin;
=======
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c

    public StaffAuth(IConfiguration config, IHostEnvironment env, ILogger<StaffAuth> log)
    {
        string? kitchen = config["Staff:KitchenPin"];
        string? cashier = config["Staff:CashierPin"];
<<<<<<< HEAD
        string? owner = config["Staff:OwnerPin"];
=======
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
        string? secret = config["Auth:Secret"];

        if (env.IsDevelopment())
        {
            if (string.IsNullOrWhiteSpace(kitchen)) { kitchen = "1111"; log.LogWarning("DEV ONLY: kitchen PIN is 1111."); }
            if (string.IsNullOrWhiteSpace(cashier)) { cashier = "2222"; log.LogWarning("DEV ONLY: cashier PIN is 2222."); }
<<<<<<< HEAD
            if (string.IsNullOrWhiteSpace(owner)) { owner = "9999"; log.LogWarning("DEV ONLY: owner PIN is 9999."); }
=======
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
            if (string.IsNullOrWhiteSpace(secret)) secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        }

        if (string.IsNullOrWhiteSpace(kitchen) || kitchen.Length < MinPinLength ||
<<<<<<< HEAD
            string.IsNullOrWhiteSpace(cashier) || cashier.Length < MinPinLength ||
            string.IsNullOrWhiteSpace(owner) || owner.Length < MinPinLength)
        {
            throw new InvalidOperationException(
                $"Set Staff:KitchenPin, Staff:CashierPin and Staff:OwnerPin (at least {MinPinLength} characters; 6+ digits recommended).");
=======
            string.IsNullOrWhiteSpace(cashier) || cashier.Length < MinPinLength)
        {
            throw new InvalidOperationException(
                $"Set Staff:KitchenPin and Staff:CashierPin (at least {MinPinLength} characters; 6+ digits recommended).");
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
        }

        if (string.IsNullOrWhiteSpace(secret) || secret.Length < MinSecretLength)
        {
            throw new InvalidOperationException(
                $"Set Auth:Secret to a random string of at least {MinSecretLength} characters (e.g. `openssl rand -hex 32`).");
        }

        _kitchenPin = kitchen;
        _cashierPin = cashier;
<<<<<<< HEAD
        _ownerPin = owner;
=======
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
        _key = Encoding.UTF8.GetBytes(secret);
    }

    private static string CookieName(string role) => role + "_auth";

<<<<<<< HEAD
    private string PinFor(string role) => role switch
    {
        StaffRoles.Kitchen => _kitchenPin,
        StaffRoles.Cashier => _cashierPin,
        _ => _ownerPin
    };
=======
    private string PinFor(string role) => role == StaffRoles.Kitchen ? _kitchenPin : _cashierPin;
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c

    // ---------- PIN check ----------

    public bool CheckPin(string role, string? pin)
    {
        if (!StaffRoles.IsValid(role) || string.IsNullOrEmpty(pin)) return false;

        // Compare fixed-length hashes in constant time so timing reveals nothing about the PIN.
        byte[] given = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
        byte[] real = SHA256.HashData(Encoding.UTF8.GetBytes(PinFor(role)));
        return CryptographicOperations.FixedTimeEquals(given, real);
    }

    // ---------- Token issue / verify ----------

    private string Sign(string text) =>
        Base64Url(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(text)));

    /// <summary>Keyed fingerprint of the PIN: changes when the PIN changes, reveals nothing about it.</summary>
    private string PinFingerprint(string role) => Sign("pin:" + role + ":" + PinFor(role))[..16];

    private string IssueToken(string role)
    {
        long expires = DateTimeOffset.UtcNow.Add(SessionLifetime).ToUnixTimeSeconds();
        string payload = $"{role}|{expires.ToString(CultureInfo.InvariantCulture)}|{PinFingerprint(role)}";
        return Base64Url(Encoding.UTF8.GetBytes(payload)) + "." + Sign(payload);
    }

    private bool VerifyToken(string? token, string role)
    {
        if (string.IsNullOrEmpty(token)) return false;

        int dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1) return false;

        try
        {
            string payload = Encoding.UTF8.GetString(FromBase64Url(token[..dot]));
            byte[] givenSig = Encoding.UTF8.GetBytes(token[(dot + 1)..]);
            byte[] realSig = Encoding.UTF8.GetBytes(Sign(payload));
            if (!CryptographicOperations.FixedTimeEquals(givenSig, realSig)) return false;

            string[] parts = payload.Split('|');
            if (parts.Length != 3 || parts[0] != role) return false;
            if (!long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long expires)) return false;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expires) return false;

            return parts[2] == PinFingerprint(role);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // ---------- Cookie helpers ----------

    public bool IsAuthenticated(HttpContext context, string role) =>
        StaffRoles.IsValid(role) &&
        context.Request.Cookies.TryGetValue(CookieName(role), out var token) &&
        VerifyToken(token, role);

    public void SignIn(HttpContext context, string role)
    {
        context.Response.Cookies.Append(CookieName(role), IssueToken(role), new CookieOptions
        {
            HttpOnly = true,                    // not readable by page scripts
            SameSite = SameSiteMode.Strict,     // not sent on cross-site requests
            Secure = context.Request.IsHttps,   // HTTPS only in production (behind the proxy)
            Path = "/",
            MaxAge = SessionLifetime
        });
    }

    public void SignOut(HttpContext context, string role)
    {
        if (StaffRoles.IsValid(role)) context.Response.Cookies.Delete(CookieName(role), new CookieOptions { Path = "/" });
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        string s = text.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Convert.FromBase64String(s);
    }
}

/// <summary>
/// Slows down PIN guessing: after too many wrong PINs from one address, further attempts
/// (even correct ones) are refused for a while.
/// Note: guests on the restaurant's own Wi-Fi share one public address with the staff.
/// </summary>
public sealed class LoginLimiter
{
    private const int MaxFailures = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private sealed class Entry { public int Failures; public DateTime WindowStart; }

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public bool IsLocked(string key)
    {
        if (!_entries.TryGetValue(key, out var e)) return false;
        lock (e)
        {
            if (DateTime.UtcNow - e.WindowStart > Window) { _entries.TryRemove(key, out _); return false; }
            return e.Failures >= MaxFailures;
        }
    }

    public void RecordFailure(string key)
    {
        var e = _entries.GetOrAdd(key, _ => new Entry { WindowStart = DateTime.UtcNow });
        lock (e)
        {
            if (DateTime.UtcNow - e.WindowStart > Window) { e.WindowStart = DateTime.UtcNow; e.Failures = 0; }
            e.Failures++;
        }
    }

    public void Reset(string key) => _entries.TryRemove(key, out _);

    /// <summary>Fly sets Fly-Client-IP to the real caller; elsewhere fall back to the socket address.</summary>
    public static string KeyFor(HttpContext context, string role)
    {
        string ip = context.Request.Headers["Fly-Client-IP"].ToString();
        if (string.IsNullOrWhiteSpace(ip)) ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return role + "|" + ip;
    }
}
