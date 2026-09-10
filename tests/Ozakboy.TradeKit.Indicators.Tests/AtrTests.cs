namespace Ozakboy.TradeKit.Indicators.Tests;

/// <summary>
/// 平均真實區間的黃金向量、批次與增量一致性、真實區間定義與邊界測試。
/// Golden vectors, batch-versus-streaming consistency, true-range semantics and boundary tests for the ATR.
/// </summary>
[TestClass]
public sealed class AtrTests
{
    /// <summary>
    /// 手算案例:第一根沒有前收盤價,依 Wilder 原著 TR 取「高 − 低」。
    /// </summary>
    [TestMethod]
    public void TrueRange_FirstCandle_IsHighMinusLow()
    {
        Candle[] candles = [Candle.Of(10m, 12m, 9m, 11m)];

        IReadOnlyList<decimal> trueRanges = Atr.TrueRange(candles);

        Assert.AreEqual(3m, trueRanges[0]);
    }

    /// <summary>
    /// 手算案例:向上跳空。前收 10,今日 高 20 低 18,則
    /// 高−低 = 2、|高−前收| = 10、|前收−低| = 8,TR 必須取最大的 10。
    /// 只用「高 − 低」會嚴重低估跳空行情的波動,這正是 Wilder 發明真實區間的原因。
    /// </summary>
    [TestMethod]
    public void TrueRange_GapUp_UsesHighMinusPreviousClose()
    {
        Candle[] candles =
        [
            Candle.Of(9m, 11m, 8m, 10m),
            Candle.Of(19m, 20m, 18m, 19m),
        ];

        IReadOnlyList<decimal> trueRanges = Atr.TrueRange(candles);

        Assert.AreEqual(10m, trueRanges[1]);
    }

    /// <summary>
    /// 手算案例:向下跳空。前收 100,今日 高 92 低 90,則 |前收−低| = 10 為最大值。
    /// </summary>
    [TestMethod]
    public void TrueRange_GapDown_UsesPreviousCloseMinusLow()
    {
        Candle[] candles =
        [
            Candle.Of(99m, 101m, 98m, 100m),
            Candle.Of(91m, 92m, 90m, 91m),
        ];

        IReadOnlyList<decimal> trueRanges = Atr.TrueRange(candles);

        Assert.AreEqual(10m, trueRanges[1]);
    }

    [TestMethod]
    public void Calculate_SyntheticCandles_MatchesGoldenVector()
    {
        IReadOnlyList<decimal?> batch = Atr.Calculate(GoldenVectors.Candles, 14);

        IndicatorAssert.SequenceMatches(GoldenVectors.Atr14, batch, "ATR(14) 批次", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Update_SyntheticCandles_MatchesGoldenVector()
    {
        IIndicator<Candle, decimal> streaming = new AtrIndicator(14);

        IReadOnlyList<decimal?> actual = IndicatorAssert.RunStreaming(streaming, GoldenVectors.Candles);

        IndicatorAssert.SequenceMatches(GoldenVectors.Atr14, actual, "ATR(14) 增量", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(14)]
    [DataRow(20)]
    [DataRow(60)]
    public void BatchAndStreaming_ProduceIdenticalResults(int period)
    {
        IReadOnlyList<decimal?> batch = Atr.Calculate(GoldenVectors.Candles, period);
        IReadOnlyList<decimal?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<Candle, decimal>)new AtrIndicator(period),
            GoldenVectors.Candles);

        IndicatorAssert.SequencesAreIdentical(batch, streaming, $"ATR({period})");
    }

    [TestMethod]
    public void Update_BeforeWarmupCompletes_ReportsNotReadyAndReturnsNull()
    {
        AtrIndicator indicator = new(14);
        Assert.AreEqual(14, indicator.WarmupPeriod);

        for (int i = 0; i < 13; i++)
        {
            Assert.IsNull(indicator.Update(GoldenVectors.Candles[i]), $"第 {i + 1} 根不應該有值");
            Assert.IsFalse(indicator.IsReady);
        }

        Assert.IsNotNull(indicator.Update(GoldenVectors.Candles[13]));
        Assert.IsTrue(indicator.IsReady);
    }

    [TestMethod]
    public void Update_ZeroRangeCandles_ReturnsZeroWithoutFailing()
    {
        // 完全沒有波動的極端輸入(停牌、極度冷門的合約)不得讓計算炸掉。
        AtrIndicator indicator = new(3);
        Candle flat = Candle.Of(50m, 50m, 50m, 50m);
        decimal? value = null;

        for (int i = 0; i < 10; i++)
        {
            value = indicator.Update(flat);
        }

        Assert.AreEqual(0m, value);
    }

    [TestMethod]
    public void Calculate_EmptyOrTooShortSeries_ReturnsAllNull()
    {
        Assert.IsEmpty(Atr.Calculate(Array.Empty<Candle>(), 14));

        IReadOnlyList<decimal?> tooShort = Atr.Calculate(GoldenVectors.Candles.Take(13).ToArray(), 14);
        Assert.HasCount(13, tooShort);
        Assert.IsTrue(tooShort.All(value => value is null));
    }

    [TestMethod]
    public void Calculate_LengthExactlyEqualsPeriod_ProducesOneValueAtTheLastIndex()
    {
        Candle[] candles = GoldenVectors.Candles.Take(14).ToArray();

        IReadOnlyList<decimal?> batch = Atr.Calculate(candles, 14);

        Assert.IsNull(batch[12]);
        Assert.IsNotNull(batch[13]);
    }

    [TestMethod]
    public void Reset_ClearsAllState()
    {
        AtrIndicator indicator = new(2);
        indicator.Update(GoldenVectors.Candles[0]);
        indicator.Update(GoldenVectors.Candles[1]);
        Assert.IsTrue(indicator.IsReady);

        indicator.Reset();

        Assert.IsFalse(indicator.IsReady);
        Assert.IsNull(indicator.Current);
        Assert.AreEqual(0L, indicator.ProcessedCount);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Constructor_NonPositivePeriod_Throws(int period)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AtrIndicator(period));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Atr.Calculate(GoldenVectors.Candles, period));
    }

    [TestMethod]
    public void Calculate_NullSeries_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Atr.Calculate(null!, 14));
        Assert.ThrowsExactly<ArgumentNullException>(() => Atr.TrueRange(null!));
    }
}
