using CTrader.Data.Entities;

namespace CTrader.Services.Logging;

public interface IActivityLogger
{
    Task LogAsync(string level, string category, string message, string? details = null, string? source = null);
    Task LogInfoAsync(string category, string message, string? details = null, string? source = null);
    Task LogWarningAsync(string category, string message, string? details = null, string? source = null);
    Task LogErrorAsync(string category, string message, string? details = null, string? source = null);
    Task LogSuccessAsync(string category, string message, string? details = null, string? source = null);

    Task<IEnumerable<ActivityLog>> GetLogsAsync(int count = 100, string? category = null, string? level = null);
    Task<IEnumerable<ActivityLog>> GetLogsSinceAsync(DateTime since, string? category = null);
    Task ClearLogsAsync(int olderThanDays = 30);
}
