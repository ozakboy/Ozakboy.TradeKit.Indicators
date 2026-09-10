namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 簡單移動平均(SMA)的批次計算。
/// Batch calculation of the Simple Moving Average (SMA).
/// </summary>
/// <remarks>
/// 一次算完整段歷史序列。回傳結果與輸入等長且索引對齊,暖身期的位置為 <see langword="null" />,
/// 因此可以直接與原始資料並排使用而不需要偏移索引。
/// Computes an entire history in one pass. The returned list is the same length as the input and index-aligned
/// with it, with <see langword="null" /> in the warm-up positions, so results can sit alongside the source data
/// without index juggling.
/// </remarks>
/// <seealso cref="SmaIndicator" />
public static class Sma
{
    /// <summary>
    /// 計算整段價格序列的 SMA。
    /// Calculates the SMA over an entire price series.
    /// </summary>
    /// <param name="values">依時間先後排列的價格序列。Prices in chronological order.</param>
    /// <param name="period">平均的期數,必須大於 0。Number of periods to average; must be greater than 0.</param>
    /// <returns>與輸入等長、索引對齊的結果;暖身期為 <see langword="null" />。An index-aligned list the same length as the input, with <see langword="null" /> during warm-up.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values" /> 為 <see langword="null" />。<paramref name="values" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public static IReadOnlyList<decimal?> Calculate(IReadOnlyList<decimal> values, int period)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        decimal?[] results = new decimal?[values.Count];
        decimal sum = 0m;

        for (int i = 0; i < values.Count; i++)
        {
            // 先減掉被擠出的舊值再加新值,與 SmaIndicator 的滾動總和維持完全相同的運算順序。
            // Subtract the outgoing value before adding the incoming one, matching SmaIndicator's running sum exactly.
            if (i >= period)
            {
                sum -= values[i - period];
            }

            sum += values[i];

            if (i >= period - 1)
            {
                results[i] = sum / period;
            }
        }

        return results;
    }

    /// <summary>
    /// 以 K 線收盤價計算整段 SMA。
    /// Calculates the SMA over a candle series, using close prices.
    /// </summary>
    /// <param name="candles">依時間先後排列的 K 線。Candles in chronological order.</param>
    /// <param name="period">平均的期數,必須大於 0。Number of periods to average; must be greater than 0.</param>
    /// <returns>與輸入等長、索引對齊的結果。An index-aligned list the same length as the input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candles" /> 為 <see langword="null" />。<paramref name="candles" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public static IReadOnlyList<decimal?> Calculate(IReadOnlyList<Candle> candles, int period)
    {
        ArgumentNullException.ThrowIfNull(candles);
        return Calculate(CandleSeries.Closes(candles), period);
    }
}
