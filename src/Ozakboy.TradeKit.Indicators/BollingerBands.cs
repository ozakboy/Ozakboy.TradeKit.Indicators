namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 布林通道(Bollinger Bands)的批次計算。
/// Batch calculation of Bollinger Bands.
/// </summary>
/// <remarks>
/// 標準差採母體公式(除以 N),與 <see cref="BollingerBandsIndicator" /> 一致。
/// The standard deviation is the population form (divided by N), matching <see cref="BollingerBandsIndicator" />.
/// </remarks>
/// <seealso cref="BollingerBandsIndicator" />
public static class BollingerBands
{
    /// <summary>
    /// 計算整段價格序列的布林通道。
    /// Calculates Bollinger Bands over an entire price series.
    /// </summary>
    /// <param name="values">依時間先後排列的價格序列。Prices in chronological order.</param>
    /// <param name="period">期數,必須大於 0。Number of periods; must be greater than 0.</param>
    /// <param name="standardDeviations">標準差倍數,不可為負。Standard deviation multiplier; must not be negative.</param>
    /// <returns>與輸入等長、索引對齊的結果;暖身期為 <see langword="null" />。An index-aligned list the same length as the input, with <see langword="null" /> during warm-up.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values" /> 為 <see langword="null" />。<paramref name="values" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0,或 <paramref name="standardDeviations" /> 為負。<paramref name="period" /> is less than or equal to 0, or <paramref name="standardDeviations" /> is negative.</exception>
    public static IReadOnlyList<BollingerBandsResult?> Calculate(
        IReadOnlyList<decimal> values,
        int period = 20,
        decimal standardDeviations = 2m)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        ArgumentOutOfRangeException.ThrowIfNegative(standardDeviations);

        BollingerBandsResult?[] results = new BollingerBandsResult?[values.Count];
        decimal sum = 0m;

        for (int i = 0; i < values.Count; i++)
        {
            if (i >= period)
            {
                sum -= values[i - period];
            }

            sum += values[i];

            if (i < period - 1)
            {
                continue;
            }

            decimal middle = sum / period;
            decimal squaredDeviationSum = 0m;

            // 依時間順序掃描視窗,與 BollingerBandsIndicator 的環形緩衝區走訪順序一致。
            // Scan the window chronologically, matching the ring-buffer traversal order in BollingerBandsIndicator.
            for (int offset = i - period + 1; offset <= i; offset++)
            {
                decimal deviation = values[offset] - middle;
                squaredDeviationSum += deviation * deviation;
            }

            decimal standardDeviation = DecimalMath.Sqrt(squaredDeviationSum / period);
            decimal band = standardDeviations * standardDeviation;

            results[i] = new BollingerBandsResult(middle, middle + band, middle - band, standardDeviation);
        }

        return results;
    }

    /// <summary>
    /// 以 K 線收盤價計算整段布林通道。
    /// Calculates Bollinger Bands over a candle series, using close prices.
    /// </summary>
    /// <param name="candles">依時間先後排列的 K 線。Candles in chronological order.</param>
    /// <param name="period">期數,必須大於 0。Number of periods; must be greater than 0.</param>
    /// <param name="standardDeviations">標準差倍數,不可為負。Standard deviation multiplier; must not be negative.</param>
    /// <returns>與輸入等長、索引對齊的結果。An index-aligned list the same length as the input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candles" /> 為 <see langword="null" />。<paramref name="candles" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">參數不合法。A parameter is invalid.</exception>
    public static IReadOnlyList<BollingerBandsResult?> Calculate(
        IReadOnlyList<Candle> candles,
        int period = 20,
        decimal standardDeviations = 2m)
    {
        ArgumentNullException.ThrowIfNull(candles);
        return Calculate(CandleSeries.Closes(candles), period, standardDeviations);
    }
}
