using CTrader.Data;
using CTrader.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CTrader.Services.Logging;

public class ActivityLogger : IActivityLogger
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<ActivityLogger> _logger;

    public ActivityLogger(IDbContextFactory<AppDbContext> contextFactory, ILogger<ActivityLogger> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task LogAsync(string level, string category, string message, string? details = null, string? source = null)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.ActivityLogs.Add(new ActivityLog
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Category = category,
                Message = message,
                Details = details,
                Source = source
            });
            await context.SaveChangesAsync();

            // Also log to standard logger
            var logLevel = level.ToLower() switch
            {
                "error" => LogLevel.Error,
                "warning" => LogLevel.Warning,
                "success" => LogLevel.Information,
                _ => LogLevel.Information
            };
            _logger.Log(logLevel, "[{Category}] {Message}", category, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write activity log");
        }
    }

    public Task LogInfoAsync(string category, string message, string? details = null, string? source = null)
        => LogAsync("Info", category, message, details, source);

    public Task LogWarningAsync(string category, string message, string? details = null, string? source = null)
        => LogAsync("Warning", category, message, details, source);

    public Task LogErrorAsync(string category, string message, string? details = null, string? source = null)
        => LogAsync("Error", category, message, details, source);

    public Task LogSuccessAsync(string category, string message, string? details = null, string? source = null)
        => LogAsync("Success", category, message, details, source);

    public async Task<IEnumerable<ActivityLog>> GetLogsAsync(int count = 100, string? category = null, string? level = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.ActivityLogs.AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(l => l.Category == category);

        if (!string.IsNullOrEmpty(level))
            query = query.Where(l => l.Level == level);

        return await query
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<ActivityLog>> GetLogsSinceAsync(DateTime since, string? category = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.ActivityLogs.Where(l => l.Timestamp >= since);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(l => l.Category == category);

        return await query
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task ClearLogsAsync(int olderThanDays = 30)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);

        var oldLogs = await context.ActivityLogs
            .Where(l => l.Timestamp < cutoff)
            .ToListAsync();

        context.ActivityLogs.RemoveRange(oldLogs);
        await context.SaveChangesAsync();

        _logger.LogInformation("Cleared {Count} activity logs older than {Days} days", oldLogs.Count, olderThanDays);
    }
}
