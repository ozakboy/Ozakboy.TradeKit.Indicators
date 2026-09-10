namespace Ozakboy.TradeKit.Indicators.Tests;

/// <summary>
/// 布林通道的黃金向量、手算標準差、批次與增量一致性、暖身期與參數驗證測試。
/// Golden vectors, a hand-checkable standard deviation, batch-versus-streaming consistency, warm-up and
/// parameter validation tests for Bollinger Bands.
/// </summary>
[TestClass]
public sealed class BollingerBandsTests
{
    private static decimal?[] Project(IReadOnlyList<BollingerBandsResult?> results, Func<BollingerBandsResult, decimal> selector)
        => [.. results.Select(value => value is BollingerBandsResult result ? selector(result) : (decimal?)null)];

    /// <summary>
    /// 手算案例(教科書標準差範例):[2,4,4,4,5,5,7,9] 的平均為 40/8 = 5,
    /// 偏差平方和為 9+1+1+1+0+0+4+16 = 32,母體變異數 32/8 = 4,標準差恰為 2。
    /// 倍數取 2 時上軌 = 5 + 4 = 9、下軌 = 5 − 4 = 1。
    /// 這一組同時驗證了平均、母體標準差公式,以及 decimal 平方根在完全平方數上的精確性。
    /// </summary>
    [TestMethod]
    public void Calculate_TextbookStandardDeviationSample_MatchesArithmeticByHand()
    {
        decimal[] values = [2m, 4m, 4m, 4m, 5m, 5m, 7m, 9m];

        IReadOnlyList<BollingerBandsResult?> batch = BollingerBands.Calculate(values, 8, 2m);
        BollingerBandsResult result = batch[7]!.Value;

        Assert.AreEqual(5m, result.Middle);
        Assert.AreEqual(2m, result.StandardDeviation);
        Assert.AreEqual(9m, result.Upper);
        Assert.AreEqual(1m, result.Lower);
    }

    [TestMethod]
    public void Calculate_SyntheticCloses_MatchesGoldenVector()
    {
        IReadOnlyList<BollingerBandsResult?> batch = BollingerBands.Calculate(GoldenVectors.Closes, 20, 2m);

        IndicatorAssert.SequenceMatches(GoldenVectors.BollingerMiddle, Project(batch, r => r.Middle), "BB 中軌 批次", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.BollingerUpper, Project(batch, r => r.Upper), "BB 上軌 批次", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.BollingerLower, Project(batch, r => r.Lower), "BB 下軌 批次", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.BollingerStandardDeviation, Project(batch, r => r.StandardDeviation), "BB 標準差 批次", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Update_SyntheticCloses_MatchesGoldenVector()
    {
        IIndicator<decimal, BollingerBandsResult> streaming = new BollingerBandsIndicator(20, 2m);

        IReadOnlyList<BollingerBandsResult?> actual = IndicatorAssert.RunStreaming(streaming, GoldenVectors.Closes);

        IndicatorAssert.SequenceMatches(GoldenVectors.BollingerMiddle, Project(actual, r => r.Middle), "BB 中軌 增量", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.BollingerUpper, Project(actual, r => r.Upper), "BB 上軌 增量", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.BollingerLower, Project(actual, r => r.Lower), "BB 下軌 增量", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.BollingerStandardDeviation, Project(actual, r => r.StandardDeviation), "BB 標準差 增量", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    [DataRow(1, 2)]
    [DataRow(2, 2)]
    [DataRow(20, 2)]
    [DataRow(20, 1)]
    [DataRow(20, 3)]
    [DataRow(60, 2)]
    public void BatchAndStreaming_ProduceIdenticalResults(int period, int standardDeviations)
    {
        decimal multiplier = standardDeviations;

        IReadOnlyList<BollingerBandsResult?> batch = BollingerBands.Calculate(GoldenVectors.Closes, period, multiplier);
        IReadOnlyList<BollingerBandsResult?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<decimal, BollingerBandsResult>)new BollingerBandsIndicator(period, multiplier),
            GoldenVectors.Closes);

        IndicatorAssert.SequencesAreIdentical(batch, streaming, $"BB({period},{multiplier})");
    }

    [TestMethod]
    public void Update_BeforeWarmupCompletes_ReportsNotReadyAndReturnsNull()
    {
        BollingerBandsIndicator indicator = new(20, 2m);
        Assert.AreEqual(20, indicator.WarmupPeriod);

        for (int i = 0; i < 19; i++)
        {
            Assert.IsNull(indicator.Update(GoldenVectors.Closes[i]), $"第 {i + 1} 筆不應該有值");
            Assert.IsFalse(indicator.IsReady);
        }

        Assert.IsNotNull(indicator.Update(GoldenVectors.Closes[19]));
        Assert.IsTrue(indicator.IsReady);
    }

    [TestMethod]
    public void Update_ConstantSeries_CollapsesAllThreeBands()
    {
        // 完全沒有離散度時標準差為 0,三條軌必須重合,而不是因為開方誤差微微分開。
        BollingerBandsIndicator indicator = new(5, 2m);
        BollingerBandsResult? value = null;

        for (int i = 0; i < 10; i++)
        {
            value = indicator.Update(123.456m);
        }

        Assert.IsNotNull(value);
        Assert.AreEqual(0m, value!.Value.StandardDeviation);
        Assert.AreEqual(123.456m, value.Value.Middle);
        Assert.AreEqual(123.456m, value.Value.Upper);
        Assert.AreEqual(123.456m, value.Value.Lower);
    }

    [TestMethod]
    public void Calculate_ZeroMultiplier_CollapsesBandsOntoTheMiddle()
    {
        IReadOnlyList<BollingerBandsResult?> batch = BollingerBands.Calculate(GoldenVectors.Closes, 20, 0m);

        foreach (BollingerBandsResult? value in batch)
        {
            if (value is BollingerBandsResult result)
            {
                Assert.AreEqual(result.Middle, result.Upper);
                Assert.AreEqual(result.Middle, result.Lower);
            }
        }
    }

    [TestMethod]
    public void Calculate_MiddleBand_EqualsSimpleMovingAverage()
    {
        IReadOnlyList<BollingerBandsResult?> bands = BollingerBands.Calculate(GoldenVectors.Closes, 20, 2m);
        IReadOnlyList<decimal?> sma = Sma.Calculate(GoldenVectors.Closes, 20);

        for (int i = 0; i < bands.Count; i++)
        {
            Assert.AreEqual(sma[i], bands[i]?.Middle, $"索引 {i} 的中軌不等於 SMA(20)");
        }
    }

    [TestMethod]
    public void Calculate_EmptyOrTooShortSeries_ReturnsAllNull()
    {
        Assert.IsEmpty(BollingerBands.Calculate(Array.Empty<decimal>(), 20));

        IReadOnlyList<BollingerBandsResult?> tooShort = BollingerBands.Calculate([1m, 2m], 20);
        Assert.HasCount(2, tooShort);
        Assert.IsTrue(tooShort.All(value => value is null));
    }

    [TestMethod]
    public void Reset_ClearsAllState()
    {
        BollingerBandsIndicator indicator = new(2, 2m);
        indicator.Update(10m);
        indicator.Update(20m);
        Assert.IsTrue(indicator.IsReady);

        indicator.Reset();

        Assert.IsFalse(indicator.IsReady);
        Assert.IsNull(indicator.Current);
        Assert.AreEqual(0L, indicator.ProcessedCount);
    }

    [TestMethod]
    public void Constructor_InvalidParameters_Throw()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BollingerBandsIndicator(0, 2m));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BollingerBandsIndicator(-1, 2m));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BollingerBandsIndicator(20, -0.1m));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => BollingerBands.Calculate(GoldenVectors.Closes, 20, -2m));
    }

    [TestMethod]
    public void Calculate_NullSeries_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => BollingerBands.Calculate((IReadOnlyList<decimal>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => BollingerBands.Calculate((IReadOnlyList<Candle>)null!));
    }
}
