namespace Ozakboy.TradeKit.Indicators.Tests;

/// <summary>
/// 指數移動平均的黃金向量、批次與增量一致性、兩種種子取法與邊界測試。
/// Golden vectors, batch-versus-streaming consistency, both seeding conventions and boundary tests for the EMA.
/// </summary>
[TestClass]
public sealed class EmaTests
{
    /// <summary>
    /// 手算案例:N=3 時 k = 2/(3+1) = 0.5。前三筆 [1,2,3] 的 SMA 種子為 (1+2+3)/3 = 2,
    /// 第四筆 4 進來後 EMA = (4 − 2) × 0.5 + 2 = 3。
    /// </summary>
    [TestMethod]
    public void Calculate_HandCheckedSeries_MatchesArithmeticByHand()
    {
        decimal[] values = [1m, 2m, 3m, 4m];

        IReadOnlyList<decimal?> batch = Ema.Calculate(values, 3);

        Assert.IsNull(batch[0]);
        Assert.IsNull(batch[1]);
        Assert.AreEqual(2m, batch[2]);
        Assert.AreEqual(3m, batch[3]);
    }

    [TestMethod]
    public void SmoothingFactor_IsTwoOverPeriodPlusOne()
    {
        Assert.AreEqual(0.5m, new EmaIndicator(3).SmoothingFactor);
        Assert.AreEqual(0.2m, new EmaIndicator(9).SmoothingFactor);
    }

    [TestMethod]
    public void Calculate_WilderCloses_MatchesGoldenVector()
    {
        IReadOnlyList<decimal?> batch = Ema.Calculate(GoldenVectors.WilderCloses, 10);

        IndicatorAssert.SequenceMatches(GoldenVectors.Ema10, batch, "EMA(10) 批次", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Update_WilderCloses_MatchesGoldenVector()
    {
        IIndicator<decimal, decimal> streaming = new EmaIndicator(10);

        IReadOnlyList<decimal?> actual = IndicatorAssert.RunStreaming(streaming, GoldenVectors.WilderCloses);

        IndicatorAssert.SequenceMatches(GoldenVectors.Ema10, actual, "EMA(10) 增量", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    [DataRow(1, EmaSeedMode.SimpleMovingAverage)]
    [DataRow(2, EmaSeedMode.SimpleMovingAverage)]
    [DataRow(12, EmaSeedMode.SimpleMovingAverage)]
    [DataRow(26, EmaSeedMode.SimpleMovingAverage)]
    [DataRow(60, EmaSeedMode.SimpleMovingAverage)]
    [DataRow(1, EmaSeedMode.FirstValue)]
    [DataRow(2, EmaSeedMode.FirstValue)]
    [DataRow(12, EmaSeedMode.FirstValue)]
    [DataRow(26, EmaSeedMode.FirstValue)]
    [DataRow(60, EmaSeedMode.FirstValue)]
    public void BatchAndStreaming_ProduceIdenticalResults(int period, EmaSeedMode seedMode)
    {
        IReadOnlyList<decimal?> batch = Ema.Calculate(GoldenVectors.Closes, period, seedMode);
        IReadOnlyList<decimal?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<decimal, decimal>)new EmaIndicator(period, seedMode),
            GoldenVectors.Closes);

        IndicatorAssert.SequencesAreIdentical(batch, streaming, $"EMA({period},{seedMode})");
    }

    [TestMethod]
    public void Update_FirstValueSeeding_StillWaitsForThePeriodBeforeReportingReady()
    {
        EmaIndicator indicator = new(4, EmaSeedMode.FirstValue);

        Assert.IsNull(indicator.Update(10m));
        Assert.IsNull(indicator.Update(11m));
        Assert.IsNull(indicator.Update(12m));
        Assert.IsFalse(indicator.IsReady, "第 4 筆之前不得就緒");

        Assert.IsNotNull(indicator.Update(13m));
        Assert.IsTrue(indicator.IsReady);
    }

    [TestMethod]
    public void Update_BeforeWarmupCompletes_ReportsNotReadyAndReturnsNull()
    {
        EmaIndicator indicator = new(10);

        for (int i = 0; i < 9; i++)
        {
            Assert.IsNull(indicator.Update(100m + i), $"第 {i + 1} 筆不應該有值");
            Assert.IsFalse(indicator.IsReady);
        }

        Assert.IsNotNull(indicator.Update(109m));
        Assert.IsTrue(indicator.IsReady);
        Assert.AreEqual(10, indicator.WarmupPeriod);
    }

    [TestMethod]
    public void Calculate_ShorterThanPeriodWithSmaSeeding_ReturnsAllNull()
    {
        IReadOnlyList<decimal?> batch = Ema.Calculate([1m, 2m, 3m], 10);

        Assert.HasCount(3, batch);
        Assert.IsTrue(batch.All(value => value is null));
    }

    [TestMethod]
    public void Calculate_EmptySeries_ReturnsEmptyResult()
    {
        Assert.IsEmpty(Ema.Calculate(Array.Empty<decimal>(), 10));
        Assert.IsEmpty(Ema.Calculate(Array.Empty<decimal>(), 10, EmaSeedMode.FirstValue));
    }

    [TestMethod]
    public void Calculate_PeriodOne_TracksTheInputExactly()
    {
        // N=1 時 k = 2/2 = 1,EMA 應完全等於當筆輸入。
        decimal[] values = [5m, 9m, 2m];

        IReadOnlyList<decimal?> batch = Ema.Calculate(values, 1);

        CollectionAssert.AreEqual(new decimal?[] { 5m, 9m, 2m }, batch.ToArray());
    }

    [TestMethod]
    public void Update_ConstantSeries_ConvergesToThatConstant()
    {
        EmaIndicator indicator = new(5);
        decimal? value = null;

        for (int i = 0; i < 50; i++)
        {
            value = indicator.Update(7m);
        }

        Assert.AreEqual(7m, value);
    }

    [TestMethod]
    public void Reset_ClearsAllState()
    {
        EmaIndicator indicator = new(2);
        indicator.Update(10m);
        indicator.Update(20m);

        indicator.Reset();

        Assert.IsFalse(indicator.IsReady);
        Assert.IsNull(indicator.Current);
        Assert.AreEqual(0L, indicator.ProcessedCount);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-3)]
    public void Constructor_NonPositivePeriod_Throws(int period)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new EmaIndicator(period));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Ema.Calculate(GoldenVectors.Closes, period));
    }

    [TestMethod]
    public void Calculate_NullSeries_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Ema.Calculate((IReadOnlyList<decimal>)null!, 5));
        Assert.ThrowsExactly<ArgumentNullException>(() => Ema.Calculate((IReadOnlyList<Candle>)null!, 5));
    }
}
