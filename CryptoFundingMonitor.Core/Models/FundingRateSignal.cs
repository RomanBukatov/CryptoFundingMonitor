namespace CryptoFundingMonitor.Core.Models
{
    /// <summary>
    /// Модель сигнала ставки финансирования
    /// </summary>
    public record FundingRateSignal(
        string ExchangeName,
        string Symbol,
        string Pair,
        decimal CurrentPrice,
        decimal FundingRate,
        decimal? TakeProfitPrice,
        DateTime Timestamp
    );
}