namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 相對強弱指標(RSI,Wilder 平滑)的批次計算。
/// Batch calculation of the Relative Strength Index (RSI) with Wilder smoothing.
/// </summary>
/// <remarks>
/// 平均漲跌幅的計算方式與除以零的替代值,與 <see cref="RsiIndicator" /> 完全相同,詳見該類別的說明。
/// The averaging scheme and the zero-denominator fallbacks are identical to <see cref="RsiIndicator" />;
/// see that type for the details.
/// </remarks>
/// <seealso cref="RsiIndicator" />
public static class Rsi
{
    /// <summary>
    /// 計算整段價格序列的 RSI。
    /// Calculates the RSI over an entire price series.
    /// </summary>
    /// <param name="values">依時間先後排列的價格序列。Prices in chronological order.</param>
    /// <param name="period">期數,必須大於 0。Number of periods; must be greater than 0.</param>
    /// <returns>與輸入等長、索引對齊的結果;暖身期為 <see langword="null" />。An index-aligned list the same length as the input, with <see langword="null" /> during warm-up.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values" /> 為 <see langword="null" />。<paramref name="values" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public static IReadOnlyList<decimal?> Calculate(IReadOnlyList<decimal> values, int period)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        decimal?[] results = new decimal?[values.Count];

        // N 個價差需要 N + 1 個價格。
        // N changes require N + 1 prices.
        if (values.Count <= period)
        {
            return results;
        }

        decimal seedGainSum = 0m;
        decimal seedLossSum = 0m;

        for (int i = 1; i <= period; i++)
        {
            decimal change = values[i] - values[i - 1];
            if (change > 0m)
            {
                seedGainSum += change;
            }
            else if (change < 0m)
            {
                seedLossSum -= change;
            }
        }

        decimal averageGain = seedGainSum / period;
        decimal averageLoss = seedLossSum / period;
        results[period] = RsiIndicator.ComputeRsi(averageGain, averageLoss);

        for (int i = period + 1; i < values.Count; i++)
        {
            decimal change = values[i] - values[i - 1];
            decimal gain = change > 0m ? change : 0m;
            decimal loss = change < 0m ? -change : 0m;

            averageGain = ((averageGain * (period - 1)) + gain) / period;
            averageLoss = ((averageLoss * (period - 1)) + loss) / period;
            results[i] = RsiIndicator.ComputeRsi(averageGain, averageLoss);
        }

        return results;
    }

    /// <summary>
    /// 以 K 線收盤價計算整段 RSI。
    /// Calculates the RSI over a candle series, using close prices.
    /// </summary>
    /// <param name="candles">依時間先後排列的 K 線。Candles in chronological order.</param>
    /// <param name="period">期數,必須大於 0。Number of periods; must be greater than 0.</param>
    /// <returns>與輸入等長、索引對齊的結果。An index-aligned list the same length as the input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candles" /> 為 <see langword="null" />。<paramref name="candles" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public static IReadOnlyList<decimal?> Calculate(IReadOnlyList<Candle> candles, int period)
    {
        ArgumentNullException.ThrowIfNull(candles);
        return Calculate(CandleSeries.Closes(candles), period);
    }
}
