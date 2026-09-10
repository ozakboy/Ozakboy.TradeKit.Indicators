namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 從 K 線序列抽出單一價格序列的輔助方法,供批次指標把 <see cref="Candle" /> 輸入轉成價格輸入。
/// Helpers that project a candle series onto a single price series, letting batch indicators accept
/// <see cref="Candle" /> input where a price series is expected.
/// </summary>
public static class CandleSeries
{
    /// <summary>
    /// 取出收盤價序列。
    /// Extracts the close prices.
    /// </summary>
    /// <param name="candles">K 線序列。The candle series.</param>
    /// <returns>與輸入等長的收盤價序列。A close-price list the same length as the input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candles" /> 為 <see langword="null" />。<paramref name="candles" /> is <see langword="null" />.</exception>
    public static IReadOnlyList<decimal> Closes(IReadOnlyList<Candle> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);

        decimal[] closes = new decimal[candles.Count];
        for (int i = 0; i < candles.Count; i++)
        {
            closes[i] = candles[i].Close;
        }

        return closes;
    }
}
