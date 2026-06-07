using System.Security.Cryptography;
using System.Text;

namespace CTrader.Services.News;

/// <summary>
/// Builds process-stable identifiers for news articles. string.GetHashCode() is
/// randomized per process since .NET Core, so using it for article IDs produced
/// a different ID for the same article after every restart, defeating dedup.
/// </summary>
internal static class NewsId
{
    public static string Stable(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? string.Empty));
        return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant(); // 16 hex chars
    }
}
