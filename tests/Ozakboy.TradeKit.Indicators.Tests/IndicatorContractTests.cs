namespace Ozakboy.TradeKit.Indicators.Tests;

/// <summary>
/// 跨指標共通的契約測試:統一驅動、暖身期絕不回傳 0、重設語意、K 線型別。
/// Cross-indicator contract tests: uniform driving, never emitting 0 during warm-up, reset semantics and the candle type.
/// </summary>
[TestClass]
public sealed class IndicatorContractTests
{
    /// <summary>建立一組涵蓋全部九個以 K 線驅動之指標的實例。Builds one instance of each of the nine candle-driven indicators.</summary>
    private static List<IIndicator> BuildAll() =>
    [
        new SmaIndicator(20),
        new EmaIndicator(20),
        new RsiIndicator(14),
        new AtrIndicator(14),
        new MacdIndicator(),
        new BollingerBandsIndicator(20, 2m),
        new DonchianChannelIndicator(20),
        new AdxIndicator(14),
        new StochasticIndicator(),
    ];

    private static void Feed(IIndicator indicator, Candle candle)
    {
        switch (indicator)
        {
            case SmaIndicator sma: sma.Update(candle); break;
            case EmaIndicator ema: ema.Update(candle); break;
            case RsiIndicator rsi: rsi.Update(candle); break;
            case AtrIndicator atr: atr.Update(candle); break;
            case MacdIndicator macd: macd.Update(candle); break;
            case BollingerBandsIndicator bollinger: bollinger.Update(candle); break;
            case DonchianChannelIndicator donchian: donchian.Update(candle); break;
            case AdxIndicator adx: adx.Update(candle); break;
            case StochasticIndicator stochastic: stochastic.Update(candle); break;
            default: throw new InvalidOperationException($"未涵蓋的指標型別:{indicator.GetType().Name}");
        }
    }

    /// <summary>
    /// 統一驅動:一組異質指標可以只透過 <see cref="IIndicator" /> 管理暖身狀態,
    /// 而且每一個都必須在恰好第 <c>WarmupPeriod</c> 根 K 線轉為就緒——早一根或晚一根都是錯的。
    /// </summary>
    [TestMethod]
    public void AllIndicators_BecomeReadyExactlyAtTheirWarmupPeriod()
    {
        List<IIndicator> indicators = BuildAll();

        for (int i = 0; i < GoldenVectors.Candles.Length; i++)
        {
            foreach (IIndicator indicator in indicators)
            {
                Feed(indicator, GoldenVectors.Candles[i]);

                long fed = i + 1;
                bool shouldBeReady = fed >= indicator.WarmupPeriod;

                Assert.AreEqual(
                    shouldBeReady,
                    indicator.IsReady,
                    $"{indicator.Name}:餵入第 {fed} 根時 IsReady 應為 {shouldBeReady}(暖身期 {indicator.WarmupPeriod})");
                Assert.AreEqual(fed, indicator.ProcessedCount, $"{indicator.Name}:已處理筆數不對");
            }
        }

        // 資料集有 60 根,所有指標的暖身期都在 60 以內,結束時必須全部就緒。
        foreach (IIndicator indicator in indicators)
        {
            Assert.IsTrue(indicator.IsReady, $"{indicator.Name} 最終應就緒");
            Assert.IsLessThanOrEqualTo(60, indicator.WarmupPeriod, $"{indicator.Name} 的暖身期超出測試資料長度");
        }
    }

    /// <summary>
    /// 重設之後再跑同一段資料,結果必須與全新實例逐筆相同——殘留狀態是增量指標最典型的 bug。
    /// </summary>
    [TestMethod]
    public void AllIndicators_AfterReset_BehaveLikeFreshInstances()
    {
        List<IIndicator> reused = BuildAll();
        List<IIndicator> fresh = BuildAll();

        foreach (IIndicator indicator in reused)
        {
            foreach (Candle candle in GoldenVectors.Candles)
            {
                Feed(indicator, candle);
            }

            indicator.Reset();

            Assert.IsFalse(indicator.IsReady, $"{indicator.Name} 重設後不得就緒");
            Assert.AreEqual(0L, indicator.ProcessedCount, $"{indicator.Name} 重設後筆數應歸零");
        }

        for (int i = 0; i < GoldenVectors.Candles.Length; i++)
        {
            for (int j = 0; j < reused.Count; j++)
            {
                Feed(reused[j], GoldenVectors.Candles[i]);
                Feed(fresh[j], GoldenVectors.Candles[i]);

                Assert.AreEqual(
                    fresh[j].IsReady,
                    reused[j].IsReady,
                    $"{fresh[j].Name}:第 {i + 1} 根的就緒狀態與全新實例不同");
            }
        }

        Assert.AreEqual(reused[0].Current(), fresh[0].Current(), "SMA 重設後的最終值不同");
    }

    /// <summary>
    /// 暖身期的鐵律:未就緒時一律是 null,絕不能是 0 或任何看起來像數字的東西。
    /// 這條規則若被打破,上層策略會在啟動初期拿垃圾數字下單。
    /// </summary>
    [TestMethod]
    public void AllIndicators_DuringWarmup_ReturnNullRatherThanZero()
    {
        // 刻意用「真的會讓 0 看起來合理」的資料:價格全部相同,所有變化量與區間都是 0。
        Candle flat = Candle.Of(100m, 100m, 100m, 100m);

        SmaIndicator sma = new(5);
        EmaIndicator ema = new(5);
        RsiIndicator rsi = new(5);
        AtrIndicator atr = new(5);
        MacdIndicator macd = new(2, 5, 3);
        BollingerBandsIndicator bollinger = new(5, 2m);
        DonchianChannelIndicator donchian = new(5);
        AdxIndicator adx = new(5);
        StochasticIndicator stochastic = new(5);

        // VWAP 沒有週期,但「還沒有成交量」就是它的暖身期:Volume 預設為 0,同一根持平 K 線在這裡必須是 null 而不是 0。
        // VWAP has no period; "no volume yet" is its warm-up. Volume defaults to 0, so the same flat bar must give null, not 0.
        VwapIndicator vwap = new(time => DateOnly.FromDateTime(time.UtcDateTime));
        VwapBar flatBar = new(default, 100m, 100m, 100m, flat.Volume, null);

        for (int i = 0; i < 4; i++)
        {
            Assert.IsNull(sma.Update(flat), "SMA 暖身期");
            Assert.IsNull(ema.Update(flat), "EMA 暖身期");
            Assert.IsNull(rsi.Update(flat), "RSI 暖身期");
            Assert.IsNull(atr.Update(flat), "ATR 暖身期");
            Assert.IsNull(macd.Update(flat), "MACD 暖身期");
            Assert.IsNull(bollinger.Update(flat), "布林 暖身期");
            Assert.IsNull(donchian.Update(flat), "唐奇安 暖身期");
            Assert.IsNull(adx.Update(flat), "ADX 暖身期");
            Assert.IsNull(stochastic.Update(flat), "KD 暖身期");
            Assert.IsNull(vwap.Update(flatBar), "VWAP 尚無成交量");
            Assert.IsFalse(vwap.IsReady, "VWAP 尚無成交量時不得就緒");

            Assert.IsNull(sma.Current);
            Assert.IsNull(rsi.Current);
            Assert.IsNull(adx.Current);
        }
    }

    [TestMethod]
    public void AllIndicators_NameIncludesTheirParameters()
    {
        Assert.AreEqual("SMA(20)", new SmaIndicator(20).Name);
        Assert.AreEqual("EMA(20)", new EmaIndicator(20).Name);
        Assert.AreEqual("RSI(14)", new RsiIndicator(14).Name);
        Assert.AreEqual("ATR(14)", new AtrIndicator(14).Name);
        Assert.AreEqual("MACD(12,26,9)", new MacdIndicator().Name);
        Assert.AreEqual("BB(20,2)", new BollingerBandsIndicator(20, 2m).Name);
        Assert.AreEqual("DC(20)", new DonchianChannelIndicator(20).Name);
        Assert.AreEqual("ADX(14)", new AdxIndicator(14).Name);
        Assert.AreEqual("KD(9,3,3)", new StochasticIndicator().Name);
        Assert.AreEqual("VWAP(1000)", new VwapIndicator(time => DateOnly.FromDateTime(time.UtcDateTime), 1000m).Name);
    }

    [TestMethod]
    public void Candle_IsAnImmutableValueTypeWithStructuralEquality()
    {
        Candle first = Candle.Of(1m, 2m, 0.5m, 1.5m, 100m);
        Candle second = Candle.Of(1m, 2m, 0.5m, 1.5m, 100m);
        Candle different = Candle.Of(1m, 2m, 0.5m, 1.6m, 100m);

        Assert.AreEqual(first, second);
        Assert.AreNotEqual(first, different);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        Assert.AreEqual(default(DateTimeOffset), first.OpenTime);
    }

    [TestMethod]
    public void Candle_RetainsTheOpenTimeWhenSupplied()
    {
        DateTimeOffset when = new(2026, 9, 11, 8, 30, 0, TimeSpan.Zero);

        Candle candle = new(when, 1m, 2m, 0.5m, 1.5m, 10m);

        Assert.AreEqual(when, candle.OpenTime);
        Assert.AreEqual(1.5m, candle.Close);
    }

    [TestMethod]
    public void CandleSeries_Closes_ProjectsEveryClosePrice()
    {
        IReadOnlyList<decimal> closes = CandleSeries.Closes(GoldenVectors.Candles);

        Assert.HasCount(GoldenVectors.Candles.Length, closes);
        for (int i = 0; i < closes.Count; i++)
        {
            Assert.AreEqual(GoldenVectors.Candles[i].Close, closes[i]);
        }
    }

    [TestMethod]
    public void CandleSeries_Closes_NullInput_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => CandleSeries.Closes(null!));

    /// <summary>
    /// 每個價格型批次指標都有一個吃 K 線的多載,它必須與直接餵收盤價得到完全相同的結果。
    /// 這些多載很容易在改版時漏掉,而呼叫端多半只用它們。
    /// </summary>
    [TestMethod]
    public void PriceBasedBatchIndicators_CandleOverloads_MatchTheClosePriceOverloads()
    {
        Candle[] candles = GoldenVectors.Candles;
        decimal[] closes = GoldenVectors.Closes;

        IndicatorAssert.SequencesAreIdentical(
            Sma.Calculate(closes, 20), Sma.Calculate(candles, 20), "SMA K 線多載");
        IndicatorAssert.SequencesAreIdentical(
            Ema.Calculate(closes, 20), Ema.Calculate(candles, 20), "EMA K 線多載");
        IndicatorAssert.SequencesAreIdentical(
            Rsi.Calculate(closes, 14), Rsi.Calculate(candles, 14), "RSI K 線多載");
        IndicatorAssert.SequencesAreIdentical(
            Macd.Calculate(closes), Macd.Calculate(candles), "MACD K 線多載");
        IndicatorAssert.SequencesAreIdentical(
            BollingerBands.Calculate(closes, 20, 2m), BollingerBands.Calculate(candles, 20, 2m), "布林 K 線多載");
    }

    /// <summary>
    /// 環形緩衝區在「尚未裝滿就被清空、之後又重新填滿並繞回」時最容易錯位,
    /// 這裡用 SMA 把這條路徑走一遍。
    /// </summary>
    [TestMethod]
    public void RingBuffer_ClearedMidWindowThenRefilled_StaysAligned()
    {
        SmaIndicator indicator = new(3);

        indicator.Update(1m);
        indicator.Update(2m);
        indicator.Reset();

        Assert.IsNull(indicator.Update(10m));
        Assert.IsNull(indicator.Update(20m));
        Assert.AreEqual(20m, indicator.Update(30m));

        // 再繞過一整圈,確認最舊值被正確擠出。
        Assert.AreEqual(30m, indicator.Update(40m));
        Assert.AreEqual(40m, indicator.Update(50m));
        Assert.AreEqual(50m, indicator.Update(60m));
    }
}

/// <summary>把非泛型指標的目前值取出來比對用的小工具。Helper for reading the current value off a non-generic indicator handle.</summary>
internal static class IndicatorCurrentExtensions
{
    internal static decimal? Current(this IIndicator indicator)
        => indicator is IIndicator<decimal, decimal> typed ? typed.Current : null;
}
