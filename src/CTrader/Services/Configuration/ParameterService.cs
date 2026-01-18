using System.Text.Json;
using CTrader.Data;
using CTrader.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CTrader.Services.Configuration;

public class ParameterService : IParameterService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<ParameterService> _logger;

    public ParameterService(IDbContextFactory<AppDbContext> contextFactory, ILogger<ParameterService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<Parameter>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Parameters
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Key)
            .ToListAsync();
    }

    public async Task<IEnumerable<Parameter>> GetByCategoryAsync(string category)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Parameters
            .Where(p => p.Category == category)
            .OrderBy(p => p.Key)
            .ToListAsync();
    }

    public async Task<Parameter?> GetAsync(string category, string key)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Parameters
            .FirstOrDefaultAsync(p => p.Category == category && p.Key == key);
    }

    public async Task<T> GetValueAsync<T>(string category, string key, T defaultValue)
    {
        var parameter = await GetAsync(category, key);
        if (parameter == null)
            return defaultValue;

        try
        {
            return ParseValue<T>(parameter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse parameter {Category}.{Key} as {Type}", category, key, typeof(T).Name);
            return defaultValue;
        }
    }

    public async Task<T?> GetValueAsync<T>(string category, string key) where T : class
    {
        var parameter = await GetAsync(category, key);
        if (parameter == null)
            return null;

        try
        {
            return ParseValue<T>(parameter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse parameter {Category}.{Key} as {Type}", category, key, typeof(T).Name);
            return null;
        }
    }

    private static T ParseValue<T>(Parameter parameter)
    {
        return parameter.DataType.ToLower() switch
        {
            "string" => (T)(object)parameter.Value,
            "int" => (T)(object)int.Parse(parameter.Value),
            "decimal" => (T)(object)decimal.Parse(parameter.Value),
            "bool" => (T)(object)bool.Parse(parameter.Value),
            "json" => JsonSerializer.Deserialize<T>(parameter.Value)!,
            _ => JsonSerializer.Deserialize<T>(parameter.Value)!
        };
    }

    public async Task SetAsync(string category, string key, object value, string dataType, string? description = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Parameters
            .FirstOrDefaultAsync(p => p.Category == category && p.Key == key);

        var stringValue = dataType.ToLower() switch
        {
            "json" => JsonSerializer.Serialize(value),
            _ => value.ToString() ?? string.Empty
        };

        if (existing != null)
        {
            existing.Value = stringValue;
            existing.DataType = dataType;
            existing.UpdatedAt = DateTime.UtcNow;
            if (description != null)
                existing.Description = description;
        }
        else
        {
            context.Parameters.Add(new Parameter
            {
                Category = category,
                Key = key,
                Value = stringValue,
                DataType = dataType,
                Description = description,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("Parameter {Category}.{Key} set to {Value}", category, key, stringValue);
    }

    public async Task DeleteAsync(string category, string key)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var parameter = await context.Parameters
            .FirstOrDefaultAsync(p => p.Category == category && p.Key == key);

        if (parameter != null)
        {
            context.Parameters.Remove(parameter);
            await context.SaveChangesAsync();
            _logger.LogInformation("Parameter {Category}.{Key} deleted", category, key);
        }
    }

    public async Task<IEnumerable<string>> GetCategoriesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Parameters
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }
}
