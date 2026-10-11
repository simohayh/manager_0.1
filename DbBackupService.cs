using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Writes a consistent copy of the SQLite database every few hours (VACUUM INTO, safe while the app is running)
/// and keeps only the newest few. Copies go next to the database file, in a "backups" folder.
///
/// Settings: Backup:Enabled (default true), Backup:Directory, Backup:IntervalHours (default 6), Backup:Keep (default 28).
///
/// These copies protect against bad deploys, accidental deletes and corrupt data. They sit on the same disk,
/// so they do NOT protect against losing the disk itself: also keep Fly's volume snapshots and/or copy
/// a backup off-site now and then (see DEPLOY.md).
/// </summary>
public sealed class DbBackupService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DbBackupService> _log;

    public DbBackupService(IConfiguration config, IServiceScopeFactory scopes, ILogger<DbBackupService> log)
    {
        _config = config;
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("Backup:Enabled", true)) return;

        var interval = TimeSpan.FromHours(Math.Max(1, _config.GetValue("Backup:IntervalHours", 6)));

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // let startup finish first

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await BackupOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogError(ex, "Database backup failed");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private async Task BackupOnceAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        string? dbFile = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        if (string.IsNullOrWhiteSpace(dbFile) || dbFile.Contains(":memory:", StringComparison.OrdinalIgnoreCase)) return;

<<<<<<< HEAD
        string dir = _config["Backup:Directory"] ?? Path.Combine(DataPaths.DataDirectory(_config), "backups");
=======
        string dir = _config["Backup:Directory"]
                     ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbFile)) ?? ".", "backups");
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
        Directory.CreateDirectory(dir);

        string target = Path.Combine(dir, $"restaurant-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db");

<<<<<<< HEAD
        await DatabaseBackup.WriteAsync(db, target, ct);

        _log.LogInformation("Database backup written to {Path}", target);

        int keep = Math.Max(1, _config.GetValue("Backup:Keep", 28));
        foreach (var old in new DirectoryInfo(dir).GetFiles("restaurant-*.db")
                     .OrderByDescending(f => f.Name).Skip(keep))
        {
            old.Delete();
        }
    }
}

/// <summary>Writes a consistent copy of the live database to <c>target</c> (SQLite VACUUM INTO).</summary>
public static class DatabaseBackup
{
    public static async Task WriteAsync(AppDbContext db, string target, CancellationToken ct)
    {
=======
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
        var connection = db.Database.GetDbConnection();
        bool wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) await connection.OpenAsync(ct);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "VACUUM INTO $target";
            var p = cmd.CreateParameter();
            p.ParameterName = "$target";
            p.Value = target;
            cmd.Parameters.Add(p);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }
<<<<<<< HEAD
=======

        _log.LogInformation("Database backup written to {Path}", target);

        int keep = Math.Max(1, _config.GetValue("Backup:Keep", 28));
        foreach (var old in new DirectoryInfo(dir).GetFiles("restaurant-*.db")
                     .OrderByDescending(f => f.Name).Skip(keep))
        {
            old.Delete();
        }
>>>>>>> 6ffc69fa9b33e4c09f1a7d56bcfa855b2bc6f70c
    }
}
