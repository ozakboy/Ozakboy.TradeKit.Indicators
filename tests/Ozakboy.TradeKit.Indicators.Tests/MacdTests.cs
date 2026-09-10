namespace Ozakboy.TradeKit.Indicators.Tests;

/// <summary>
/// MACD 的黃金向量、批次與增量一致性、組合關係、暖身期與參數驗證測試。
/// Golden vectors, batch-versus-streaming consistency, compositional identities, warm-up and parameter
/// validation tests for MACD.
/// </summary>
[TestClass]
public sealed class MacdTests
{
    private static decimal?[] Project(IReadOnlyList<MacdResult?> results, Func<MacdResult, decimal> selector)
        => [.. results.Select(value => value is MacdResult result ? selector(result) : (decimal?)null)];

    [TestMethod]
    public void Calculate_SyntheticCloses_MatchesGoldenVector()
    {
        IReadOnlyList<MacdResult?> batch = Macd.Calculate(GoldenVectors.Closes);

        IndicatorAssert.SequenceMatches(GoldenVectors.MacdLine, Project(batch, r => r.Macd), "MACD 主線 批次", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.MacdSignal, Project(batch, r => r.Signal), "MACD 訊號線 批次", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.MacdHistogram, Project(batch, r => r.Histogram), "MACD 柱狀圖 批次", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Update_SyntheticCloses_MatchesGoldenVector()
    {
        IIndicator<decimal, MacdResult> streaming = new MacdIndicator();

        IReadOnlyList<MacdResult?> actual = IndicatorAssert.RunStreaming(streaming, GoldenVectors.Closes);

        IndicatorAssert.SequenceMatches(GoldenVectors.MacdLine, Project(actual, r => r.Macd), "MACD 主線 增量", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.MacdSignal, Project(actual, r => r.Signal), "MACD 訊號線 增量", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(GoldenVectors.MacdHistogram, Project(actual, r => r.Histogram), "MACD 柱狀圖 增量", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    [DataRow(12, 26, 9, EmaSeedMode.SimpleMovingAverage)]
    [DataRow(2, 3, 2, EmaSeedMode.SimpleMovingAverage)]
    [DataRow(5, 10, 4, EmaSeedMode.SimpleMovingAverage)]
    [DataRow(12, 26, 9, EmaSeedMode.FirstValue)]
    [DataRow(3, 7, 5, EmaSeedMode.FirstValue)]
    public void BatchAndStreaming_ProduceIdenticalResults(int fast, int slow, int signal, EmaSeedMode seedMode)
    {
        IReadOnlyList<MacdResult?> batch = Macd.Calculate(GoldenVectors.Closes, fast, slow, signal, seedMode);
        IReadOnlyList<MacdResult?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<decimal, MacdResult>)new MacdIndicator(fast, slow, signal, seedMode),
            GoldenVectors.Closes);

        IndicatorAssert.SequencesAreIdentical(batch, streaming, $"MACD({fast},{slow},{signal},{seedMode})");
    }

    /// <summary>
    /// 組合關係:MACD 主線在定義上就等於快線 EMA 減慢線 EMA,拿本函式庫自己的 EMA 交叉驗證。
    /// </summary>
    [TestMethod]
    public void MacdLine_EqualsFastEmaMinusSlowEma()
    {
        IReadOnlyList<MacdResult?> macd = Macd.Calculate(GoldenVectors.Closes);
        IReadOnlyList<decimal?> fast = Ema.Calculate(GoldenVectors.Closes, 12);
        IReadOnlyList<decimal?> slow = Ema.Calculate(GoldenVectors.Closes, 26);

        for (int i = 0; i < macd.Count; i++)
        {
            if (macd[i] is not MacdResult result)
            {
                continue;
            }

            Assert.AreEqual(fast[i]!.Value - slow[i]!.Value, result.Macd, $"索引 {i} 的主線不等於 EMA(12) − EMA(26)");
        }
    }

    /// <summary>
    /// 組合關係:柱狀圖必須恰好是主線減訊號線。
    /// </summary>
    [TestMethod]
    public void Histogram_EqualsMacdMinusSignal()
    {
        IReadOnlyList<MacdResult?> macd = Macd.Calculate(GoldenVectors.Closes);

        foreach (MacdResult? value in macd)
        {
            if (value is MacdResult result)
            {
                Assert.AreEqual(result.Macd - result.Signal, result.Histogram);
            }
        }
    }

    [TestMethod]
    public void Update_BeforeWarmupCompletes_ReportsNotReadyAndReturnsNull()
    {
        // 慢線 26 筆才出主線,訊號線再要 9 筆,合計 26 + 9 − 1 = 34。
        MacdIndicator indicator = new();
        Assert.AreEqual(34, indicator.WarmupPeriod);

        for (int i = 0; i < 33; i++)
        {
            Assert.IsNull(indicator.Update(GoldenVectors.Closes[i]), $"第 {i + 1} 筆不應該有值");
            Assert.IsFalse(indicator.IsReady);
        }

        Assert.IsNotNull(indicator.Update(GoldenVectors.Closes[33]));
        Assert.IsTrue(indicator.IsReady);
    }

    [TestMethod]
    public void Calculate_EmptyOrTooShortSeries_ReturnsAllNull()
    {
        Assert.IsEmpty(Macd.Calculate(Array.Empty<decimal>()));

        IReadOnlyList<MacdResult?> tooShort = Macd.Calculate(GoldenVectors.Closes.Take(33).ToArray());
        Assert.HasCount(33, tooShort);
        Assert.IsTrue(tooShort.All(value => value is null));
    }

    [TestMethod]
    public void Update_FedWithCandles_UsesClosePrice()
    {
        MacdIndicator fromCandles = new();
        MacdIndicator fromPrices = new();

        for (int i = 0; i < GoldenVectors.Candles.Length; i++)
        {
            MacdResult? a = fromCandles.Update(GoldenVectors.Candles[i]);
            MacdResult? b = fromPrices.Update(GoldenVectors.Closes[i]);
            Assert.AreEqual(b, a);
        }
    }

    [TestMethod]
    public void Reset_ClearsAllState()
    {
        MacdIndicator indicator = new(2, 3, 2);
        foreach (decimal close in GoldenVectors.Closes)
        {
            indicator.Update(close);
        }

        Assert.IsTrue(indicator.IsReady);
        indicator.Reset();

        Assert.IsFalse(indicator.IsReady);
        Assert.IsNull(indicator.Current);
        Assert.AreEqual(0L, indicator.ProcessedCount);
    }

    [TestMethod]
    public void Constructor_InvalidPeriods_Throw()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MacdIndicator(0, 26, 9));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MacdIndicator(12, 0, 9));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MacdIndicator(12, 26, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MacdIndicator(26, 26, 9));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MacdIndicator(30, 26, 9));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Macd.Calculate(GoldenVectors.Closes, 30, 26, 9));
    }

    [TestMethod]
    public void Calculate_NullSeries_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Macd.Calculate((IReadOnlyList<decimal>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Macd.Calculate((IReadOnlyList<Candle>)null!));
    }
}
