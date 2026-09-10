namespace Ozakboy.TradeKit.Indicators.Tests;

/// <summary>
/// 簡單移動平均的黃金向量、批次與增量一致性、暖身期與邊界測試。
/// Golden vectors, batch-versus-streaming consistency, warm-up and boundary tests for the SMA.
/// </summary>
[TestClass]
public sealed class SmaTests
{
    /// <summary>
    /// 手算案例:[1,2,3,4,5] 的 SMA(3) 依序為 (1+2+3)/3=2、(2+3+4)/3=3、(3+4+5)/3=4,
    /// 前兩個位置因為視窗未滿必須是 null。
    /// </summary>
    [TestMethod]
    public void Calculate_HandCheckedSeries_MatchesArithmeticByHand()
    {
        decimal[] values = [1m, 2m, 3m, 4m, 5m];

        IReadOnlyList<decimal?> batch = Sma.Calculate(values, 3);

        CollectionAssert.AreEqual(new decimal?[] { null, null, 2m, 3m, 4m }, batch.ToArray());
    }

    [TestMethod]
    public void Calculate_WilderCloses_MatchesGoldenVector()
    {
        IReadOnlyList<decimal?> batch = Sma.Calculate(GoldenVectors.WilderCloses, 5);

        IndicatorAssert.SequenceMatches(GoldenVectors.Sma5, batch, "SMA(5) 批次", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Update_WilderCloses_MatchesGoldenVector()
    {
        IIndicator<decimal, decimal> streaming = new SmaIndicator(5);

        IReadOnlyList<decimal?> actual = IndicatorAssert.RunStreaming(streaming, GoldenVectors.WilderCloses);

        IndicatorAssert.SequenceMatches(GoldenVectors.Sma5, actual, "SMA(5) 增量", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(5)]
    [DataRow(20)]
    [DataRow(59)]
    [DataRow(60)]
    public void BatchAndStreaming_ProduceIdenticalResults(int period)
    {
        IReadOnlyList<decimal?> batch = Sma.Calculate(GoldenVectors.Closes, period);
        IReadOnlyList<decimal?> streaming =
            IndicatorAssert.RunStreaming((IIndicator<decimal, decimal>)new SmaIndicator(period), GoldenVectors.Closes);

        IndicatorAssert.SequencesAreIdentical(batch, streaming, $"SMA({period})");
    }

    [TestMethod]
    public void Update_BeforeWarmupCompletes_ReportsNotReadyAndReturnsNull()
    {
        SmaIndicator indicator = new(5);

        for (int i = 0; i < 4; i++)
        {
            decimal? value = indicator.Update(10m + i);

            Assert.IsNull(value, $"第 {i + 1} 筆不應該有值");
            Assert.IsFalse(indicator.IsReady, $"第 {i + 1} 筆不應該就緒");
        }

        decimal? fifth = indicator.Update(14m);

        Assert.IsTrue(indicator.IsReady);
        Assert.AreEqual(12m, fifth);
        Assert.AreEqual(5, indicator.WarmupPeriod);
        Assert.AreEqual(5L, indicator.ProcessedCount);
    }

    [TestMethod]
    public void Calculate_EmptySeries_ReturnsEmptyResult()
    {
        IReadOnlyList<decimal?> batch = Sma.Calculate(Array.Empty<decimal>(), 14);

        Assert.IsEmpty(batch);
    }

    [TestMethod]
    public void Calculate_SingleValueWithLongerPeriod_ReturnsOnlyNull()
    {
        IReadOnlyList<decimal?> batch = Sma.Calculate([42m], 14);

        Assert.HasCount(1, batch);
        Assert.IsNull(batch[0]);
    }

    [TestMethod]
    public void Calculate_LengthExactlyEqualsPeriod_ProducesOneValueAtTheLastIndex()
    {
        decimal[] values = [1m, 2m, 3m];

        IReadOnlyList<decimal?> batch = Sma.Calculate(values, 3);

        Assert.IsNull(batch[0]);
        Assert.IsNull(batch[1]);
        Assert.AreEqual(2m, batch[2]);
    }

    [TestMethod]
    public void Update_LargeMagnitudeValues_StaysExact()
    {
        // 滾動總和在極端量級下仍必須與逐一相加一致,否則 decimal 的有效位數被吃掉了。
        decimal[] values = [1_000_000_000_000m, 2_000_000_000_000m, 3_000_000_000_000m, 4_000_000_000_000m];

        IReadOnlyList<decimal?> batch = Sma.Calculate(values, 2);
        IReadOnlyList<decimal?> streaming =
            IndicatorAssert.RunStreaming((IIndicator<decimal, decimal>)new SmaIndicator(2), values);

        Assert.AreEqual(1_500_000_000_000m, batch[1]);
        Assert.AreEqual(3_500_000_000_000m, batch[3]);
        IndicatorAssert.SequencesAreIdentical(batch, streaming, "SMA 極端量級");
    }

    [TestMethod]
    public void Update_FedWithCandles_UsesClosePrice()
    {
        SmaIndicator indicator = new(2);

        indicator.Update(Candle.Of(1m, 9m, 0m, 10m));
        decimal? value = indicator.Update(Candle.Of(1m, 9m, 0m, 20m));

        Assert.AreEqual(15m, value);
    }

    [TestMethod]
    public void Reset_ClearsAllState()
    {
        SmaIndicator indicator = new(2);
        indicator.Update(10m);
        indicator.Update(20m);
        Assert.IsTrue(indicator.IsReady);

        indicator.Reset();

        Assert.IsFalse(indicator.IsReady);
        Assert.IsNull(indicator.Current);
        Assert.AreEqual(0L, indicator.ProcessedCount);
        Assert.IsNull(indicator.Update(100m));
        Assert.AreEqual(150m, indicator.Update(200m));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Constructor_NonPositivePeriod_Throws(int period)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SmaIndicator(period));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Sma.Calculate(GoldenVectors.Closes, period));
    }

    [TestMethod]
    public void Calculate_NullSeries_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Sma.Calculate((IReadOnlyList<decimal>)null!, 5));
        Assert.ThrowsExactly<ArgumentNullException>(() => Sma.Calculate((IReadOnlyList<Candle>)null!, 5));
    }

    [TestMethod]
    public void Name_IncludesPeriod()
    {
        Assert.AreEqual("SMA(20)", new SmaIndicator(20).Name);
    }
}
