using CTrader.Data.Entities;

namespace CTrader.Services.Configuration;

public interface IParameterService
{
    Task<IEnumerable<Parameter>> GetAllAsync();
    Task<IEnumerable<Parameter>> GetByCategoryAsync(string category);
    Task<Parameter?> GetAsync(string category, string key);
    Task<T> GetValueAsync<T>(string category, string key, T defaultValue);
    Task<T?> GetValueAsync<T>(string category, string key) where T : class;
    Task SetAsync(string category, string key, object value, string dataType, string? description = null);
    Task DeleteAsync(string category, string key);
    Task<IEnumerable<string>> GetCategoriesAsync();
}
