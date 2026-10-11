using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Stops one device from flooding the kitchen with orders: at most <see cref="MaxOrders"/> order attempts
/// per <see cref="Window"/>. Kept in memory (resets when the app restarts).
/// </summary>
public sealed class OrderLimiter
{
    private const int MaxOrders = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, Queue<DateTime>> _hits = new();

    /// <summary>True if this attempt is allowed (and counts it); false if the device is over the limit.</summary>
    public bool TryAcquire(string key)
    {
        var now = DateTime.UtcNow;

        // Don't let the dictionary grow forever on a busy public server.
        if (_hits.Count > 5000)
        {
            foreach (var pair in _hits)
            {
                lock (pair.Value)
                {
                    if (pair.Value.Count == 0 || now - pair.Value.Peek() > Window)
                        _hits.TryRemove(pair.Key, out _);
                }
            }
        }

        var queue = _hits.GetOrAdd(key, _ => new Queue<DateTime>());
        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() > Window) queue.Dequeue();
            if (queue.Count >= MaxOrders) return false;
            queue.Enqueue(now);
            return true;
        }
    }

    /// <summary>Fly sets Fly-Client-IP to the real caller; elsewhere fall back to the socket address.</summary>
    public static string KeyFor(HttpContext context)
    {
        string ip = context.Request.Headers["Fly-Client-IP"].ToString();
        if (string.IsNullOrWhiteSpace(ip)) ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return ip;
    }
}
