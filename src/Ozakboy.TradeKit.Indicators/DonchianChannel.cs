namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 唐奇安通道(Donchian Channel)的批次計算。
/// Batch calculation of the Donchian Channel.
/// </summary>
/// <remarks>
/// 通道包含當根 K 線,與 <see cref="DonchianChannelIndicator" /> 一致;突破策略請比對前一根的通道值。
/// The channel includes the current candle, matching <see cref="DonchianChannelIndicator" />; breakout rules
/// should compare against the previous bar's channel.
/// </remarks>
/// <seealso cref="DonchianChannelIndicator" />
public static class DonchianChannel
{
    /// <summary>
    /// 計算整段 K 線序列的唐奇安通道。
    /// Calculates the Donchian Channel over an entire candle series.
    /// </summary>
    /// <param name="candles">依時間先後排列的 K 線。Candles in chronological order.</param>
    /// <param name="period">期數,必須大於 0。Number of periods; must be greater than 0.</param>
    /// <returns>與輸入等長、索引對齊的結果;暖身期為 <see langword="null" />。An index-aligned list the same length as the input, with <see langword="null" /> during warm-up.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candles" /> 為 <see langword="null" />。<paramref name="candles" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public static IReadOnlyList<DonchianChannelResult?> Calculate(IReadOnlyList<Candle> candles, int period = 20)
    {
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        DonchianChannelResult?[] results = new DonchianChannelResult?[candles.Count];

        for (int i = period - 1; i < candles.Count; i++)
        {
            decimal upper = candles[i].High;
            decimal lower = candles[i].Low;

            for (int offset = i - period + 1; offset < i; offset++)
            {
                Candle candle = candles[offset];

                if (candle.High > upper)
                {
                    upper = candle.High;
                }

                if (candle.Low < lower)
                {
                    lower = candle.Low;
                }
            }

            results[i] = new DonchianChannelResult(upper, (upper + lower) / 2m, lower);
        }

        return results;
    }
}
