using IBApi;

namespace CTrader.Services.Trading;

/// <summary>
/// Factory for creating IB API Contract objects for common instruments.
/// </summary>
public static class IbContractFactory
{
    public static Contract CreateUsStock(string symbol)
    {
        return new Contract
        {
            Symbol = symbol,
            SecType = "STK",
            Exchange = "SMART",
            Currency = "USD"
        };
    }

    public static Contract CreateVixIndex()
    {
        return new Contract
        {
            Symbol = "VIX",
            SecType = "IND",
            Exchange = "CBOE",
            Currency = "USD"
        };
    }
}
