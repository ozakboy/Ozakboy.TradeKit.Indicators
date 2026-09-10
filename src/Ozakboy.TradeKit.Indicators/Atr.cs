namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 平均真實區間(ATR,Wilder 平滑)的批次計算。
/// Batch calculation of the Average True Range (ATR) with Wilder smoothing.
/// </summary>
/// <seealso cref="AtrIndicator" />
public static class Atr
{
    /// <summary>
    /// 計算整段 K 線序列的 ATR。
    /// Calculates the ATR over an entire candle series.
    /// </summary>
    /// <param name="candles">依時間先後排列的 K 線。Candles in chronological order.</param>
    /// <param name="period">期數,必須大於 0。Number of periods; must be greater than 0.</param>
    /// <returns>與輸入等長、索引對齊的結果;暖身期為 <see langword="null" />。An index-aligned list the same length as the input, with <see langword="null" /> during warm-up.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candles" /> 為 <see langword="null" />。<paramref name="candles" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public static IReadOnlyList<decimal?> Calculate(IReadOnlyList<Candle> candles, int period)
    {
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        decimal?[] results = new decimal?[candles.Count];

        if (candles.Count < period)
        {
            return results;
        }

        decimal[] trueRanges = TrueRanges(candles);
        decimal seedSum = 0m;

        for (int i = 0; i < period; i++)
        {
            seedSum += trueRanges[i];
        }

        decimal atr = seedSum / period;
        results[period - 1] = atr;

        for (int i = period; i < candles.Count; i++)
        {
            atr = ((atr * (period - 1)) + trueRanges[i]) / period;
            results[i] = atr;
        }

        return results;
    }

    /// <summary>
    /// 計算整段 K 線序列的真實區間(未平滑)。第一根沒有前收盤價,依 Wilder 原著取 <c>高 − 低</c>。
    /// Calculates the unsmoothed true range for an entire candle series; the first candle has no previous
    /// close and uses <c>high - low</c> as in Wilder's original.
    /// </summary>
    /// <param name="candles">依時間先後排列的 K 線。Candles in chronological order.</param>
    /// <returns>與輸入等長的真實區間序列。A true-range list the same length as the input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candles" /> 為 <see langword="null" />。<paramref name="candles" /> is <see langword="null" />.</exception>
    public static IReadOnlyList<decimal> TrueRange(IReadOnlyList<Candle> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        return TrueRanges(candles);
    }

    private static decimal[] TrueRanges(IReadOnlyList<Candle> candles)
    {
        decimal[] trueRanges = new decimal[candles.Count];

        for (int i = 0; i < candles.Count; i++)
        {
            Candle candle = candles[i];
            trueRanges[i] = i == 0
                ? candle.High - candle.Low
                : AtrIndicator.ComputeTrueRange(candle, candles[i - 1].Close);
        }

        return trueRanges;
    }
}
