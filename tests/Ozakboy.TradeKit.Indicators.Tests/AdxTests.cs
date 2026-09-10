namespace Ozakboy.TradeKit.Indicators.Tests;

/// <summary>
/// 平均趨向指標的黃金向量、批次與增量一致性、暖身期、除以零與邊界測試。
/// Golden vectors, batch-versus-streaming consistency, warm-up, zero-denominator and boundary tests for the ADX.
/// </summary>
[TestClass]
public sealed class AdxTests
{
    private static decimal?[] Project(IReadOnlyList<AdxResult?> results, Func<AdxResult, decimal> selector)
        => [.. results.Select(value => value is AdxResult result ? selector(result) : (decimal?)null)];

    [TestMethod]
    public void Calculate_SyntheticCandles_MatchesGoldenVector()
    {
        IReadOnlyList<AdxResult?> batch = Adx.Calculate(GoldenVectors.Candles, 14);

        IndicatorAssert.SequenceMatches(GoldenVectors.Adx14, Project(batch, r => r.Adx), "ADX(14) 批次", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Update_SyntheticCandles_MatchesGoldenVector()
    {
        IIndicator<Candle, AdxResult> streaming = new AdxIndicator(14);

        IReadOnlyList<AdxResult?> actual = IndicatorAssert.RunStreaming(streaming, GoldenVectors.Candles);

        IndicatorAssert.SequenceMatches(GoldenVectors.Adx14, Project(actual, r => r.Adx), "ADX(14) 增量", IndicatorAssert.GoldenTolerance);
    }

    /// <summary>
    /// +DI 與 −DI 比 ADX 早 13 根可用,所以要拿增量版早期公開的屬性去對照完整的方向指標黃金向量。
    /// </summary>
    [TestMethod]
    public void Update_DirectionalIndicators_MatchGoldenVectorIncludingTheEarlyBars()
    {
        AdxIndicator indicator = new(14);
        decimal?[] plus = new decimal?[GoldenVectors.Candles.Length];
        decimal?[] minus = new decimal?[GoldenVectors.Candles.Length];

        for (int i = 0; i < GoldenVectors.Candles.Length; i++)
        {
            indicator.Update(GoldenVectors.Candles[i]);
            plus[i] = indicator.PlusDi;
            minus[i] = indicator.MinusDi;
        }

        IndicatorAssert.SequenceMatches(GoldenVectors.PlusDi14, plus, "+DI(14) 增量", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.MinusDi14, minus, "-DI(14) 增量", IndicatorAssert.GoldenTolerance);
    }

    /// <summary>
    /// ADX 就緒之後,結果裡的 +DI / −DI 必須與同索引的方向指標黃金向量一致。
    /// </summary>
    [TestMethod]
    public void Calculate_ResultCarriesTheMatchingDirectionalIndicators()
    {
        IReadOnlyList<AdxResult?> batch = Adx.Calculate(GoldenVectors.Candles, 14);

        for (int i = 0; i < batch.Count; i++)
        {
            if (batch[i] is not AdxResult result)
            {
                continue;
            }

            IndicatorAssert.ValueMatches(GoldenVectors.PlusDi14[i], result.PlusDi, $"+DI 批次[{i}]", IndicatorAssert.GoldenTolerance);
            IndicatorAssert.ValueMatches(GoldenVectors.MinusDi14[i], result.MinusDi, $"-DI 批次[{i}]", IndicatorAssert.GoldenTolerance);
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(5)]
    [DataRow(14)]
    [DataRow(29)]
    [DataRow(30)]
    public void BatchAndStreaming_ProduceIdenticalResults(int period)
    {
        IReadOnlyList<AdxResult?> batch = Adx.Calculate(GoldenVectors.Candles, period);
        IReadOnlyList<AdxResult?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<Candle, AdxResult>)new AdxIndicator(period),
            GoldenVectors.Candles);

        IndicatorAssert.SequencesAreIdentical(batch, streaming, $"ADX({period})");
    }

    [TestMethod]
    public void Update_BeforeWarmupCompletes_ReportsNotReadyAndReturnsNull()
    {
        // 兩段 Wilder 平滑串接,ADX(14) 需要 28 根 K 線。
        AdxIndicator indicator = new(14);
        Assert.AreEqual(28, indicator.WarmupPeriod);

        for (int i = 0; i < 27; i++)
        {
            Assert.IsNull(indicator.Update(GoldenVectors.Candles[i]), $"第 {i + 1} 根不應該有 ADX");
            Assert.IsFalse(indicator.IsReady);
        }

        Assert.IsNotNull(indicator.Update(GoldenVectors.Candles[27]));
        Assert.IsTrue(indicator.IsReady);
    }

    [TestMethod]
    public void Update_DirectionalIndicators_BecomeAvailableBeforeAdx()
    {
        AdxIndicator indicator = new(14);

        for (int i = 0; i < 14; i++)
        {
            indicator.Update(GoldenVectors.Candles[i]);
            Assert.IsNull(indicator.PlusDi, $"第 {i + 1} 根還不該有 +DI");
        }

        indicator.Update(GoldenVectors.Candles[14]);

        Assert.IsNotNull(indicator.PlusDi, "第 15 根應該已有 +DI");
        Assert.IsNotNull(indicator.MinusDi);
        Assert.IsFalse(indicator.IsReady, "但 ADX 本身還要再等 13 根");
    }

    /// <summary>
    /// 除以零情境:完全沒有價格區間的 K 線會讓平滑 TR 為 0(+DI / −DI 的分母)、
    /// 也讓 +DI + −DI 為 0(DX 的分母)。兩個分母同時歸零時必須安靜地回報「沒有方向性」。
    /// </summary>
    [TestMethod]
    public void Update_ZeroRangeCandles_ReturnsZerosWithoutDividingByZero()
    {
        AdxIndicator indicator = new(3);
        Candle flat = Candle.Of(50m, 50m, 50m, 50m);
        AdxResult? value = null;

        for (int i = 0; i < 20; i++)
        {
            value = indicator.Update(flat);
        }

        Assert.IsNotNull(value);
        Assert.AreEqual(0m, value!.Value.Adx);
        Assert.AreEqual(0m, value.Value.PlusDi);
        Assert.AreEqual(0m, value.Value.MinusDi);
    }

    /// <summary>
    /// 單向強趨勢:一路走高時 +DI 必須明顯高於 −DI,ADX 必須攀升到高檔。
    /// 這是為了抓「方向動能正負號寫反」這類即使黃金向量對得上也可能被忽略的方向性錯誤。
    /// </summary>
    [TestMethod]
    public void Update_SustainedUptrend_ProducesPositiveDirectionalDominance()
    {
        AdxIndicator indicator = new(14);
        AdxResult? value = null;

        for (int i = 0; i < 60; i++)
        {
            decimal baseline = 100m + (i * 2m);
            value = indicator.Update(Candle.Of(baseline, baseline + 1m, baseline - 0.5m, baseline + 0.8m));
        }

        Assert.IsNotNull(value);
        Assert.IsGreaterThan(value!.Value.MinusDi, value.Value.PlusDi, "上升趨勢中 +DI 應大於 −DI");
        Assert.IsGreaterThan(50m, value.Value.Adx, $"持續上升趨勢的 ADX 應在高檔,實際為 {value.Value.Adx}");
    }

    /// <summary>
    /// 反向的強趨勢:一路走低時 −DI 必須明顯高於 +DI。
    /// </summary>
    [TestMethod]
    public void Update_SustainedDowntrend_ProducesNegativeDirectionalDominance()
    {
        AdxIndicator indicator = new(14);
        AdxResult? value = null;

        for (int i = 0; i < 60; i++)
        {
            decimal baseline = 500m - (i * 2m);
            value = indicator.Update(Candle.Of(baseline, baseline + 0.5m, baseline - 1m, baseline - 0.8m));
        }

        Assert.IsNotNull(value);
        Assert.IsGreaterThan(value!.Value.PlusDi, value.Value.MinusDi, "下降趨勢中 −DI 應大於 +DI");
        Assert.IsGreaterThan(50m, value.Value.Adx, $"持續下降趨勢的 ADX 應在高檔,實際為 {value.Value.Adx}");
    }

    [TestMethod]
    public void Calculate_EmptyOrTooShortSeries_ReturnsAllNull()
    {
        Assert.IsEmpty(Adx.Calculate(Array.Empty<Candle>(), 14));

        IReadOnlyList<AdxResult?> tooShort = Adx.Calculate(GoldenVectors.Candles.Take(27).ToArray(), 14);
        Assert.HasCount(27, tooShort);
        Assert.IsTrue(tooShort.All(value => value is null));
    }

    [TestMethod]
    public void Reset_ClearsAllState()
    {
        AdxIndicator indicator = new(2);
        foreach (Candle candle in GoldenVectors.Candles)
        {
            indicator.Update(candle);
        }

        Assert.IsTrue(indicator.IsReady);
        indicator.Reset();

        Assert.IsFalse(indicator.IsReady);
        Assert.IsNull(indicator.Current);
        Assert.IsNull(indicator.PlusDi);
        Assert.IsNull(indicator.MinusDi);
        Assert.AreEqual(0L, indicator.ProcessedCount);

        // 重設後重跑一次,結果必須與全新實例相同。
        IReadOnlyList<AdxResult?> afterReset = IndicatorAssert.RunStreaming(
            (IIndicator<Candle, AdxResult>)indicator,
            GoldenVectors.Candles);
        IReadOnlyList<AdxResult?> fresh = IndicatorAssert.RunStreaming(
            (IIndicator<Candle, AdxResult>)new AdxIndicator(2),
            GoldenVectors.Candles);

        IndicatorAssert.SequencesAreIdentical(fresh, afterReset, "ADX 重設後重跑");
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-2)]
    public void Constructor_NonPositivePeriod_Throws(int period)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AdxIndicator(period));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Adx.Calculate(GoldenVectors.Candles, period));
    }

    [TestMethod]
    public void Calculate_NullSeries_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => Adx.Calculate(null!, 14));
}
