namespace Ozakboy.TradeKit.Indicators.Tests;

/// <summary>
/// 唐奇安通道的黃金向量、手算案例、批次與增量一致性、暖身期與邊界測試。
/// Golden vectors, hand-checkable cases, batch-versus-streaming consistency, warm-up and boundary tests
/// for the Donchian Channel.
/// </summary>
[TestClass]
public sealed class DonchianChannelTests
{
    private static decimal?[] Project(IReadOnlyList<DonchianChannelResult?> results, Func<DonchianChannelResult, decimal> selector)
        => [.. results.Select(value => value is DonchianChannelResult result ? selector(result) : (decimal?)null)];

    /// <summary>
    /// 手算案例:三根 K 線的高點 12、15、11,低點 8、10、5。
    /// 週期 3 的上軌 = 15、下軌 = 5、中軌 = (15+5)/2 = 10。
    /// </summary>
    [TestMethod]
    public void Calculate_HandCheckedCandles_MatchesExtremesByHand()
    {
        Candle[] candles =
        [
            Candle.Of(10m, 12m, 8m, 11m),
            Candle.Of(11m, 15m, 10m, 14m),
            Candle.Of(14m, 11m, 5m, 6m),
        ];

        IReadOnlyList<DonchianChannelResult?> batch = DonchianChannel.Calculate(candles, 3);

        Assert.IsNull(batch[0]);
        Assert.IsNull(batch[1]);
        Assert.AreEqual(new DonchianChannelResult(15m, 10m, 5m), batch[2]);
    }

    /// <summary>
    /// 手算案例:視窗滑動時,已經滑出視窗的極值必須被淘汰。
    /// 高點序列 20、10、11 與低點序列 1、4、6,在週期 2 下最後一根的視窗只涵蓋後兩根:
    /// 上軌 = max(10, 11) = 11(不是 20),下軌 = min(4, 6) = 4(不是 1),中軌 = (11+4)/2 = 7.5。
    /// </summary>
    [TestMethod]
    public void Calculate_WhenExtremeSlidesOutOfWindow_ItIsEvicted()
    {
        Candle[] candles =
        [
            Candle.Of(15m, 20m, 1m, 18m),
            Candle.Of(18m, 10m, 4m, 9m),
            Candle.Of(9m, 11m, 6m, 10m),
        ];

        IReadOnlyList<DonchianChannelResult?> batch = DonchianChannel.Calculate(candles, 2);
        IReadOnlyList<DonchianChannelResult?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<Candle, DonchianChannelResult>)new DonchianChannelIndicator(2),
            candles);

        Assert.AreEqual(new DonchianChannelResult(11m, 7.5m, 4m), batch[2]);
        Assert.AreEqual(new DonchianChannelResult(11m, 7.5m, 4m), streaming[2]);
    }

    [TestMethod]
    public void Calculate_SyntheticCandles_MatchesGoldenVector()
    {
        IReadOnlyList<DonchianChannelResult?> batch = DonchianChannel.Calculate(GoldenVectors.Candles, 20);

        IndicatorAssert.SequenceMatches(GoldenVectors.DonchianUpper, Project(batch, r => r.Upper), "DC 上軌 批次", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.DonchianMiddle, Project(batch, r => r.Middle), "DC 中軌 批次", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.DonchianLower, Project(batch, r => r.Lower), "DC 下軌 批次", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Update_SyntheticCandles_MatchesGoldenVector()
    {
        IIndicator<Candle, DonchianChannelResult> streaming = new DonchianChannelIndicator(20);

        IReadOnlyList<DonchianChannelResult?> actual = IndicatorAssert.RunStreaming(streaming, GoldenVectors.Candles);

        IndicatorAssert.SequenceMatches(GoldenVectors.DonchianUpper, Project(actual, r => r.Upper), "DC 上軌 增量", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.DonchianMiddle, Project(actual, r => r.Middle), "DC 中軌 增量", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.DonchianLower, Project(actual, r => r.Lower), "DC 下軌 增量", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(20)]
    [DataRow(55)]
    [DataRow(60)]
    public void BatchAndStreaming_ProduceIdenticalResults(int period)
    {
        IReadOnlyList<DonchianChannelResult?> batch = DonchianChannel.Calculate(GoldenVectors.Candles, period);
        IReadOnlyList<DonchianChannelResult?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<Candle, DonchianChannelResult>)new DonchianChannelIndicator(period),
            GoldenVectors.Candles);

        IndicatorAssert.SequencesAreIdentical(batch, streaming, $"DC({period})");
    }

    /// <summary>
    /// 單調佇列容易在「連續同值」與「連續遞增/遞減」兩種極端下寫錯,這裡直接對打暴力法。
    /// </summary>
    [TestMethod]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(7)]
    public void Update_MonotonicAndRepeatedSequences_MatchBruteForce(int period)
    {
        List<Candle> candles = [];

        for (int i = 0; i < 15; i++)
        {
            candles.Add(Candle.Of(i, i + 1, i, i));          // 嚴格遞增
        }

        for (int i = 0; i < 15; i++)
        {
            candles.Add(Candle.Of(50m, 50m, 50m, 50m));      // 完全持平
        }

        for (int i = 15; i > 0; i--)
        {
            candles.Add(Candle.Of(i, i + 1, i, i));          // 嚴格遞減
        }

        IReadOnlyList<DonchianChannelResult?> batch = DonchianChannel.Calculate(candles, period);
        IReadOnlyList<DonchianChannelResult?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<Candle, DonchianChannelResult>)new DonchianChannelIndicator(period),
            candles);

        IndicatorAssert.SequencesAreIdentical(batch, streaming, $"DC({period}) 單調與重複序列");
    }

    [TestMethod]
    public void Update_BeforeWarmupCompletes_ReportsNotReadyAndReturnsNull()
    {
        DonchianChannelIndicator indicator = new(20);
        Assert.AreEqual(20, indicator.WarmupPeriod);

        for (int i = 0; i < 19; i++)
        {
            Assert.IsNull(indicator.Update(GoldenVectors.Candles[i]), $"第 {i + 1} 根不應該有值");
            Assert.IsFalse(indicator.IsReady);
        }

        Assert.IsNotNull(indicator.Update(GoldenVectors.Candles[19]));
        Assert.IsTrue(indicator.IsReady);
    }

    [TestMethod]
    public void Calculate_EmptyOrTooShortSeries_ReturnsAllNull()
    {
        Assert.IsEmpty(DonchianChannel.Calculate(Array.Empty<Candle>(), 20));

        IReadOnlyList<DonchianChannelResult?> tooShort = DonchianChannel.Calculate(GoldenVectors.Candles.Take(19).ToArray(), 20);
        Assert.HasCount(19, tooShort);
        Assert.IsTrue(tooShort.All(value => value is null));
    }

    [TestMethod]
    public void Calculate_PeriodOne_ReturnsTheCandleItself()
    {
        IReadOnlyList<DonchianChannelResult?> batch = DonchianChannel.Calculate(GoldenVectors.Candles, 1);

        for (int i = 0; i < GoldenVectors.Candles.Length; i++)
        {
            Assert.AreEqual(GoldenVectors.Candles[i].High, batch[i]!.Value.Upper);
            Assert.AreEqual(GoldenVectors.Candles[i].Low, batch[i]!.Value.Lower);
        }
    }

    [TestMethod]
    public void Reset_ClearsAllState()
    {
        DonchianChannelIndicator indicator = new(2);
        indicator.Update(GoldenVectors.Candles[0]);
        indicator.Update(GoldenVectors.Candles[1]);
        Assert.IsTrue(indicator.IsReady);

        indicator.Reset();

        Assert.IsFalse(indicator.IsReady);
        Assert.IsNull(indicator.Current);
        Assert.AreEqual(0L, indicator.ProcessedCount);

        // 重設之後不得殘留舊視窗的極值。
        indicator.Update(Candle.Of(1m, 2m, 0m, 1m));
        DonchianChannelResult? afterReset = indicator.Update(Candle.Of(1m, 3m, 1m, 2m));
        Assert.AreEqual(new DonchianChannelResult(3m, 1.5m, 0m), afterReset);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-4)]
    public void Constructor_NonPositivePeriod_Throws(int period)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DonchianChannelIndicator(period));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DonchianChannel.Calculate(GoldenVectors.Candles, period));
    }

    [TestMethod]
    public void Calculate_NullSeries_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => DonchianChannel.Calculate(null!, 20));
}
