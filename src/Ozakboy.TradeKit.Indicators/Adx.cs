namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 平均趨向指標(ADX,Wilder)的批次計算。
/// Batch calculation of Wilder's Average Directional Index (ADX).
/// </summary>
/// <remarks>
/// 平滑方式、暖身期與除以零的替代值皆與 <see cref="AdxIndicator" /> 相同,詳見該類別的說明。
/// The smoothing scheme, warm-up length and zero-denominator fallbacks all match <see cref="AdxIndicator" />;
/// see that type for the details.
/// </remarks>
/// <seealso cref="AdxIndicator" />
public static class Adx
{
    /// <summary>
    /// 計算整段 K 線序列的 ADX。
    /// Calculates the ADX over an entire candle series.
    /// </summary>
    /// <param name="candles">依時間先後排列的 K 線。Candles in chronological order.</param>
    /// <param name="period">期數,必須大於 0。Number of periods; must be greater than 0.</param>
    /// <returns>與輸入等長、索引對齊的結果;暖身期為 <see langword="null" />。An index-aligned list the same length as the input, with <see langword="null" /> during warm-up.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candles" /> 為 <see langword="null" />。<paramref name="candles" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="period" /> 小於或等於 0。<paramref name="period" /> is less than or equal to 0.</exception>
    public static IReadOnlyList<AdxResult?> Calculate(IReadOnlyList<Candle> candles, int period = 14)
    {
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        AdxResult?[] results = new AdxResult?[candles.Count];

        // 第一根沒有前一根可比,方向動能從索引 1 開始;要湊滿 N 個才有第一組平滑值,
        // 再湊滿 N 個 DX 才有第一個 ADX,合計 2N 根。
        // Directional movement starts at index 1 because the first candle has no predecessor; N of them are
        // needed for the first smoothed values and a further N DX readings for the first ADX, hence 2N candles.
        if (candles.Count < 2 * period)
        {
            return results;
        }

        decimal smoothedTrueRange = 0m;
        decimal smoothedPlusDm = 0m;
        decimal smoothedMinusDm = 0m;

        for (int i = 1; i <= period; i++)
        {
            (decimal trueRange, decimal plusDm, decimal minusDm) = DirectionalMovement(candles, i);
            smoothedTrueRange += trueRange;
            smoothedPlusDm += plusDm;
            smoothedMinusDm += minusDm;
        }

        decimal dxSum = 0m;
        decimal adx = 0m;
        bool adxSeeded = false;

        for (int i = period; i < candles.Count; i++)
        {
            if (i > period)
            {
                (decimal trueRange, decimal plusDm, decimal minusDm) = DirectionalMovement(candles, i);
                smoothedTrueRange = smoothedTrueRange - (smoothedTrueRange / period) + trueRange;
                smoothedPlusDm = smoothedPlusDm - (smoothedPlusDm / period) + plusDm;
                smoothedMinusDm = smoothedMinusDm - (smoothedMinusDm / period) + minusDm;
            }

            decimal plusDi = DecimalMath.TryDivide(100m * smoothedPlusDm, smoothedTrueRange, out decimal computedPlusDi)
                ? computedPlusDi
                : 0m;
            decimal minusDi = DecimalMath.TryDivide(100m * smoothedMinusDm, smoothedTrueRange, out decimal computedMinusDi)
                ? computedMinusDi
                : 0m;

            decimal directionalIndex = DecimalMath.TryDivide(100m * Math.Abs(plusDi - minusDi), plusDi + minusDi, out decimal computedDx)
                ? computedDx
                : 0m;

            if (!adxSeeded)
            {
                dxSum += directionalIndex;

                if (i < (2 * period) - 1)
                {
                    continue;
                }

                adx = dxSum / period;
                adxSeeded = true;
            }
            else
            {
                adx = ((adx * (period - 1)) + directionalIndex) / period;
            }

            results[i] = new AdxResult(adx, plusDi, minusDi);
        }

        return results;
    }

    /// <summary>
    /// 算出指定索引(必須大於 0)的真實區間與正負方向動能。
    /// Computes the true range and the directional movements at the given index, which must be greater than 0.
    /// </summary>
    private static (decimal TrueRange, decimal PlusDm, decimal MinusDm) DirectionalMovement(
        IReadOnlyList<Candle> candles,
        int index)
    {
        Candle current = candles[index];
        Candle previous = candles[index - 1];

        decimal trueRange = AtrIndicator.ComputeTrueRange(current, previous.Close);
        decimal upMove = current.High - previous.High;
        decimal downMove = previous.Low - current.Low;

        decimal plusDm = upMove > downMove && upMove > 0m ? upMove : 0m;
        decimal minusDm = downMove > upMove && downMove > 0m ? downMove : 0m;

        return (trueRange, plusDm, minusDm);
    }
}
