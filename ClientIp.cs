using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;

/// <summary>The caller's first IPv4 and first IPv6 address (either may be missing).</summary>
public record ClientIpInfo(string? V4, string? V6)
{
    /// <summary>Single-string form stored in <see cref="Order.IpAddress"/>.</summary>
    public string Combined => (V4, V6) switch
    {
        (not null, not null) => $"v4: {V4} | v6: {V6}",
        (not null, null) => V4!,
        (null, not null) => V6!,
        _ => "Unknown"
    };
}

public static class ClientIp
{
    /// <summary>Reads X-Forwarded-For first, then falls back to the socket address.</summary>
    public static ClientIpInfo Resolve(HttpContext context)
    {
        string? v4 = null;
        string? v6 = null;

        foreach (var candidate in GetCandidates(context))
        {
            if (!IPAddress.TryParse(candidate, out var ip)) continue;

            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

            if (ip.AddressFamily == AddressFamily.InterNetwork && v4 == null)
                v4 = ip.ToString();
            else if (ip.AddressFamily == AddressFamily.InterNetworkV6 && v6 == null)
                v6 = ip.ToString();
        }

        return new ClientIpInfo(v4, v6);
    }

    private static IEnumerable<string> GetCandidates(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            foreach (var ip in forwardedFor.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
                yield return ip.Trim();
        }

        if (context.Connection.RemoteIpAddress is { } remoteIp)
            yield return remoteIp.ToString();
    }
}
