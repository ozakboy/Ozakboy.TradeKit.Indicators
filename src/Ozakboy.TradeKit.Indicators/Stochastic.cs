namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// KD 隨機指標(Stochastic,台股慣用平滑法)的批次計算。
/// Batch calculation of the Stochastic oscillator (KD) using the smoothing convention common in Taiwan.
/// </summary>
/// <remarks>
/// RSV 的定義、分母為 0 的替代值、K / D 的種子與平滑方式,與 <see cref="StochasticIndicator" /> 完全相同,詳見該類別的說明。
/// The RSV definition, the zero-denominator fallback, and the K / D seeding and smoothing are identical to
/// <see cref="StochasticIndicator" />; see that type for the details.
/// </remarks>
/// <seealso cref="StochasticIndicator" />
public static class Stochastic
{
    /// <summary>
    /// 計算整段 K 線序列的 KD 值。
    /// Calculates the KD values over an entire candle series.
    /// </summary>
    /// <param name="candles">依時間先後排列的 K 線。Candles in chronological order.</param>
    /// <param name="period">RSV 期數,必須大於 0,台股慣用 9。RSV look-back period; must be greater than 0. Taiwan charts typically use 9.</param>
    /// <param name="kSmoothing">K 值的平滑期數,必須大於 0,慣用 3。Smoothing period for K; must be greater than 0. Typically 3.</param>
    /// <param name="dSmoothing">D 值的平滑期數,必須大於 0,慣用 3。Smoothing period for D; must be greater than 0. Typically 3.</param>
    /// <returns>與輸入等長、索引對齊的結果;暖身期為 <see langword="null" />。An index-aligned list the same length as the input, with <see langword="null" /> during warm-up.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candles" /> 為 <see langword="null" />。<paramref name="candles" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" />、<paramref name="kSmoothing" /> 或 <paramref name="dSmoothing" /> 小於或等於 0。<paramref name="period" />, <paramref name="kSmoothing" /> or <paramref name="dSmoothing" /> is less than or equal to 0.</exception>
    public static IReadOnlyList<StochasticResult?> Calculate(
        IReadOnlyList<Candle> candles,
        int period = 9,
        int kSmoothing = 3,
        int dSmoothing = 3)
    {
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(kSmoothing);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dSmoothing);

        StochasticResult?[] results = new StochasticResult?[candles.Count];

        decimal k = StochasticIndicator.SeedValue;
        decimal d = StochasticIndicator.SeedValue;

        for (int i = period - 1; i < candles.Count; i++)
        {
            decimal highest = candles[i].High;
            decimal lowest = candles[i].Low;

            for (int offset = i - period + 1; offset < i; offset++)
            {
                Candle candle = candles[offset];

                if (candle.High > highest)
                {
                    highest = candle.High;
                }

                if (candle.Low < lowest)
                {
                    lowest = candle.Low;
                }
            }

            decimal rsv = StochasticIndicator.ComputeRsv(candles[i].Close, highest, lowest);
            k = StochasticIndicator.Smooth(k, rsv, kSmoothing);
            d = StochasticIndicator.Smooth(d, k, dSmoothing);
            results[i] = new StochasticResult(k, d);
        }

        return results;
    }
}
