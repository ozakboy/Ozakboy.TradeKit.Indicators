namespace Ozakboy.TradeKit.Indicators.Tests;

/// <summary>
/// 相對強弱指標的黃金向量、公開對照值、批次與增量一致性、除以零與邊界測試。
/// Golden vectors, the published anchor value, batch-versus-streaming consistency, zero-denominator and
/// boundary tests for the RSI.
/// </summary>
[TestClass]
public sealed class RsiTests
{
    /// <summary>
    /// 公開對照錨點:Wilder (1978) 的 RSI 範例資料集,第 15 筆(索引 14)的 14 期 RSI 廣為轉載為 70.53。
    /// 這是本測試專案唯一取自外部已發表數字的斷言,其餘期望值來自獨立第二實作(見 GoldenVectorProvenance.cs)。
    /// Published anchor: for Wilder's own RSI example data, the 14-period RSI at the fifteenth close is
    /// widely republished as 70.53.
    /// </summary>
    [TestMethod]
    public void Rsi14_FirstValue_MatchesPublishedReferenceValue()
    {
        IReadOnlyList<decimal?> batch = Rsi.Calculate(GoldenVectors.WilderCloses, 14);

        Assert.IsNotNull(batch[14]);
        Assert.AreEqual(70.53m, Math.Round(batch[14]!.Value, 2), "與公開轉載的 70.53 不符");
    }

    [TestMethod]
    public void Calculate_WilderCloses_MatchesGoldenVector()
    {
        IReadOnlyList<decimal?> batch = Rsi.Calculate(GoldenVectors.WilderCloses, 14);

        IndicatorAssert.SequenceMatches(GoldenVectors.Rsi14, batch, "RSI(14) 批次", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Update_WilderCloses_MatchesGoldenVector()
    {
        IIndicator<decimal, decimal> streaming = new RsiIndicator(14);

        IReadOnlyList<decimal?> actual = IndicatorAssert.RunStreaming(streaming, GoldenVectors.WilderCloses);

        IndicatorAssert.SequenceMatches(GoldenVectors.Rsi14, actual, "RSI(14) 增量", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(14)]
    [DataRow(30)]
    [DataRow(59)]
    public void BatchAndStreaming_ProduceIdenticalResults(int period)
    {
        IReadOnlyList<decimal?> batch = Rsi.Calculate(GoldenVectors.Closes, period);
        IReadOnlyList<decimal?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<decimal, decimal>)new RsiIndicator(period),
            GoldenVectors.Closes);

        IndicatorAssert.SequencesAreIdentical(batch, streaming, $"RSI({period})");
    }

    [TestMethod]
    public void Update_BeforeWarmupCompletes_ReportsNotReadyAndReturnsNull()
    {
        // N 個價差需要 N+1 個價格,所以 RSI(14) 要餵滿 15 筆。
        RsiIndicator indicator = new(14);
        Assert.AreEqual(15, indicator.WarmupPeriod);

        for (int i = 0; i < 14; i++)
        {
            Assert.IsNull(indicator.Update(100m + i), $"第 {i + 1} 筆不應該有值");
            Assert.IsFalse(indicator.IsReady);
        }

        Assert.IsNotNull(indicator.Update(114m));
        Assert.IsTrue(indicator.IsReady);
    }

    /// <summary>
    /// 除以零情境一:連續上漲讓平均跌幅變成 0,RS 無定義,依公認慣例 RSI = 100。
    /// </summary>
    [TestMethod]
    public void Update_UnbrokenAdvance_ReturnsOneHundredWithoutDividingByZero()
    {
        RsiIndicator indicator = new(14);
        decimal? value = null;

        for (int i = 0; i < 30; i++)
        {
            value = indicator.Update(100m + i);
        }

        Assert.AreEqual(100m, value);
    }

    /// <summary>
    /// 除以零情境二:連續下跌讓平均漲幅變成 0,RSI = 0。
    /// </summary>
    [TestMethod]
    public void Update_UnbrokenDecline_ReturnsZero()
    {
        RsiIndicator indicator = new(14);
        decimal? value = null;

        for (int i = 0; i < 30; i++)
        {
            value = indicator.Update(100m - i);
        }

        Assert.AreEqual(0m, value);
    }

    /// <summary>
    /// 除以零情境三:完全沒有價格變動時漲跌幅同時為 0,本函式庫回傳中性的 50,
    /// 而不是把「毫無波動」誤報成「極度超買」。
    /// </summary>
    [TestMethod]
    public void Update_CompletelyFlatSeries_ReturnsNeutralFifty()
    {
        RsiIndicator indicator = new(14);
        decimal? value = null;

        for (int i = 0; i < 30; i++)
        {
            value = indicator.Update(100m);
        }

        Assert.AreEqual(50m, value);
    }

    [TestMethod]
    public void Update_NeverReturnsZeroDuringWarmup()
    {
        // 暖身期回傳 0 會讓上層策略把它當成「極度超賣」而進場,這是本函式庫最不能犯的錯。
        RsiIndicator indicator = new(14);

        for (int i = 0; i < 14; i++)
        {
            decimal? value = indicator.Update(100m - i);
            Assert.IsNull(value, "暖身期必須是 null,絕不能是 0");
        }
    }

    [TestMethod]
    public void Calculate_EmptyOrTooShortSeries_ReturnsAllNull()
    {
        Assert.IsEmpty(Rsi.Calculate(Array.Empty<decimal>(), 14));

        IReadOnlyList<decimal?> tooShort = Rsi.Calculate([1m, 2m, 3m], 14);
        Assert.HasCount(3, tooShort);
        Assert.IsTrue(tooShort.All(value => value is null));
    }

    [TestMethod]
    public void Calculate_LengthExactlyPeriodPlusOne_ProducesOneValue()
    {
        decimal[] values = [1m, 2m, 3m, 4m];

        IReadOnlyList<decimal?> batch = Rsi.Calculate(values, 3);

        Assert.IsNull(batch[2]);
        Assert.AreEqual(100m, batch[3], "全程上漲,平均跌幅為 0");
    }

    [TestMethod]
    public void Reset_ClearsAllState()
    {
        RsiIndicator indicator = new(2);
        for (int i = 0; i < 5; i++)
        {
            indicator.Update(100m + i);
        }

        Assert.IsTrue(indicator.IsReady);
        indicator.Reset();

        Assert.IsFalse(indicator.IsReady);
        Assert.IsNull(indicator.Current);
        Assert.AreEqual(0L, indicator.ProcessedCount);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    public void Constructor_NonPositivePeriod_Throws(int period)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RsiIndicator(period));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Rsi.Calculate(GoldenVectors.Closes, period));
    }

    [TestMethod]
    public void Calculate_NullSeries_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Rsi.Calculate((IReadOnlyList<decimal>)null!, 14));
        Assert.ThrowsExactly<ArgumentNullException>(() => Rsi.Calculate((IReadOnlyList<Candle>)null!, 14));
    }
}
