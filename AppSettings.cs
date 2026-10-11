using System;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>Settings the owner can change from the admin page. Saved as settings.json next to the database.</summary>
public class AppSettings
{
    public string RestaurantName { get; set; } = "Restaurant";

    /// <summary>Shown in front of every price, e.g. "$", "KSh", "€".</summary>
    public string CurrencySymbol { get; set; } = "$";

    /// <summary>Off by default: customers' IP addresses are not stored (privacy).</summary>
    public bool StoreIpAddresses { get; set; } = false;

    /// <summary>On by default: customers can only order by scanning the QR code on their table (stops remote/fake orders).</summary>
    public bool RequireTableKey { get; set; } = true;

    /// <summary>Biggest discount (percent of the bill) the cashier can give alone. Bigger ones need the owner's PIN.</summary>
    public decimal MaxCashierDiscountPercent { get; set; } = 10;

    /// <summary>Tax already included in the menu prices, shown on receipts (0 = no tax line). Check local rules.</summary>
    public decimal TaxPercent { get; set; } = 0;

    /// <summary>Tax / business registration number printed on receipts (optional).</summary>
    public string TaxId { get; set; } = string.Empty;

    /// <summary>Last line of the receipt.</summary>
    public string ReceiptFooter { get; set; } = "Thank you for dining with us!";

    /// <summary>Next receipt number; goes up by one every time the cashier settles a table.</summary>
    public int NextReceiptNumber { get; set; } = 1;

    /// <summary>Set once the sample menu was added (or a menu already existed), so deleting every item never brings it back.</summary>
    public bool MenuSeeded { get; set; } = false;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}

/// <summary>Where the app keeps its files (database, settings, backups, logs).</summary>
public static class DataPaths
{
    public static string DataDirectory(IConfiguration config)
    {
        string connection = config.GetConnectionString("DefaultConnection") ?? "Data Source=restaurant.db";
        string dataSource = new SqliteConnectionStringBuilder(connection).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(".");
        return Path.GetDirectoryName(Path.GetFullPath(dataSource)) ?? Path.GetFullPath(".");
    }
}

/// <summary>Thread-safe load/save of <see cref="AppSettings"/>. No database change needed.</summary>
public sealed class SettingsStore
{
    private readonly object _lock = new();
    private readonly string _path;
    private readonly ILogger<SettingsStore> _log;
    private AppSettings _current = new();

    public SettingsStore(IConfiguration config, ILogger<SettingsStore> log)
    {
        _log = log;
        _path = config["Settings:Path"] ?? Path.Combine(DataPaths.DataDirectory(config), "settings.json");

        try
        {
            if (File.Exists(_path))
                _current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Could not read {Path}; using default settings", _path);
        }
    }

    /// <summary>A copy, so callers can't change settings without going through <see cref="Update"/>.</summary>
    public AppSettings Current
    {
        get { lock (_lock) return _current.Clone(); }
    }

    public void Update(Action<AppSettings> change)
    {
        lock (_lock)
        {
            var next = _current.Clone();
            change(next);

            string temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, _path, overwrite: true);

            _current = next;
        }
    }
}
