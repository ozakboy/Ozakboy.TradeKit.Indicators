namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 指數平滑異同移動平均(MACD)的批次計算。
/// Batch calculation of the Moving Average Convergence Divergence (MACD).
/// </summary>
/// <seealso cref="MacdIndicator" />
public static class Macd
{
    /// <summary>
    /// 計算整段價格序列的 MACD。
    /// Calculates MACD over an entire price series.
    /// </summary>
    /// <param name="values">依時間先後排列的價格序列。Prices in chronological order.</param>
    /// <param name="fastPeriod">快線 EMA 期數,必須大於 0 且小於 <paramref name="slowPeriod" />。Fast EMA period; must be greater than 0 and less than <paramref name="slowPeriod" />.</param>
    /// <param name="slowPeriod">慢線 EMA 期數。Slow EMA period.</param>
    /// <param name="signalPeriod">訊號線 EMA 期數,必須大於 0。Signal EMA period; must be greater than 0.</param>
    /// <param name="seedMode">三條 EMA 共用的起始值取法。Seeding convention shared by all three EMAs.</param>
    /// <returns>與輸入等長、索引對齊的結果;暖身期為 <see langword="null" />。An index-aligned list the same length as the input, with <see langword="null" /> during warm-up.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values" /> 為 <see langword="null" />。<paramref name="values" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">任一期數小於或等於 0,或 <paramref name="fastPeriod" /> 未小於 <paramref name="slowPeriod" />。Any period is less than or equal to 0, or <paramref name="fastPeriod" /> is not less than <paramref name="slowPeriod" />.</exception>
    public static IReadOnlyList<MacdResult?> Calculate(
        IReadOnlyList<decimal> values,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9,
        EmaSeedMode seedMode = EmaSeedMode.SimpleMovingAverage)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fastPeriod);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(slowPeriod);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(signalPeriod);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(fastPeriod, slowPeriod);

        MacdResult?[] results = new MacdResult?[values.Count];

        IReadOnlyList<decimal?> fast = Ema.Calculate(values, fastPeriod, seedMode);
        IReadOnlyList<decimal?> slow = Ema.Calculate(values, slowPeriod, seedMode);

        // MACD 線只在快慢線都就緒的位置存在;訊號線是對這段「密集序列」再取一次 EMA。
        // The MACD line exists only where both EMAs are ready; the signal line is an EMA over that dense series.
        List<decimal> macdLine = new(values.Count);
        List<int> macdIndexes = new(values.Count);

        for (int i = 0; i < values.Count; i++)
        {
            if (fast[i] is decimal fastValue && slow[i] is decimal slowValue)
            {
                macdLine.Add(fastValue - slowValue);
                macdIndexes.Add(i);
            }
        }

        IReadOnlyList<decimal?> signalLine = Ema.Calculate(macdLine, signalPeriod, seedMode);

        for (int j = 0; j < macdLine.Count; j++)
        {
            if (signalLine[j] is decimal signal)
            {
                decimal line = macdLine[j];
                results[macdIndexes[j]] = new MacdResult(line, signal, line - signal);
            }
        }

        return results;
    }

    /// <summary>
    /// 以 K 線收盤價計算整段 MACD。
    /// Calculates MACD over a candle series, using close prices.
    /// </summary>
    /// <param name="candles">依時間先後排列的 K 線。Candles in chronological order.</param>
    /// <param name="fastPeriod">快線 EMA 期數。Fast EMA period.</param>
    /// <param name="slowPeriod">慢線 EMA 期數。Slow EMA period.</param>
    /// <param name="signalPeriod">訊號線 EMA 期數。Signal EMA period.</param>
    /// <param name="seedMode">三條 EMA 共用的起始值取法。Seeding convention shared by all three EMAs.</param>
    /// <returns>與輸入等長、索引對齊的結果。An index-aligned list the same length as the input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candles" /> 為 <see langword="null" />。<paramref name="candles" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">任一期數不合法。Any period is invalid.</exception>
    public static IReadOnlyList<MacdResult?> Calculate(
        IReadOnlyList<Candle> candles,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9,
        EmaSeedMode seedMode = EmaSeedMode.SimpleMovingAverage)
    {
        ArgumentNullException.ThrowIfNull(candles);
        return Calculate(CandleSeries.Closes(candles), fastPeriod, slowPeriod, signalPeriod, seedMode);
    }
}
