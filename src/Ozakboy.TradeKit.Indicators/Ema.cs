namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 指數移動平均(EMA)的批次計算。
/// Batch calculation of the Exponential Moving Average (EMA).
/// </summary>
/// <remarks>
/// 結果與 <see cref="EmaIndicator" /> 逐筆餵入完全一致,包含起始值的取法。
/// Results match <see cref="EmaIndicator" /> fed one input at a time, seeding convention included.
/// </remarks>
/// <seealso cref="EmaIndicator" />
public static class Ema
{
    /// <summary>
    /// 計算整段價格序列的 EMA。
    /// Calculates the EMA over an entire price series.
    /// </summary>
    /// <param name="values">依時間先後排列的價格序列。Prices in chronological order.</param>
    /// <param name="period">期數,必須大於 0。Number of periods; must be greater than 0.</param>
    /// <param name="seedMode">起始值取法。Seeding convention.</param>
    /// <returns>與輸入等長、索引對齊的結果;暖身期為 <see langword="null" />。An index-aligned list the same length as the input, with <see langword="null" /> during warm-up.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values" /> 為 <see langword="null" />。<paramref name="values" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public static IReadOnlyList<decimal?> Calculate(
        IReadOnlyList<decimal> values,
        int period,
        EmaSeedMode seedMode = EmaSeedMode.SimpleMovingAverage)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        decimal?[] results = new decimal?[values.Count];

        if (values.Count == 0)
        {
            return results;
        }

        decimal smoothingFactor = 2m / (period + 1);
        decimal ema;
        int firstRecursionIndex;

        if (seedMode == EmaSeedMode.FirstValue)
        {
            ema = values[0];
            firstRecursionIndex = 1;
        }
        else
        {
            if (values.Count < period)
            {
                return results;
            }

            decimal seedSum = 0m;
            for (int i = 0; i < period; i++)
            {
                seedSum += values[i];
            }

            ema = seedSum / period;
            firstRecursionIndex = period;
        }

        if (firstRecursionIndex - 1 >= period - 1)
        {
            results[firstRecursionIndex - 1] = ema;
        }

        for (int i = firstRecursionIndex; i < values.Count; i++)
        {
            ema = ((values[i] - ema) * smoothingFactor) + ema;

            if (i >= period - 1)
            {
                results[i] = ema;
            }
        }

        return results;
    }

    /// <summary>
    /// 以 K 線收盤價計算整段 EMA。
    /// Calculates the EMA over a candle series, using close prices.
    /// </summary>
    /// <param name="candles">依時間先後排列的 K 線。Candles in chronological order.</param>
    /// <param name="period">期數,必須大於 0。Number of periods; must be greater than 0.</param>
    /// <param name="seedMode">起始值取法。Seeding convention.</param>
    /// <returns>與輸入等長、索引對齊的結果。An index-aligned list the same length as the input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candles" /> 為 <see langword="null" />。<paramref name="candles" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public static IReadOnlyList<decimal?> Calculate(
        IReadOnlyList<Candle> candles,
        int period,
        EmaSeedMode seedMode = EmaSeedMode.SimpleMovingAverage)
    {
        ArgumentNullException.ThrowIfNull(candles);
        return Calculate(CandleSeries.Closes(candles), period, seedMode);
    }
}
