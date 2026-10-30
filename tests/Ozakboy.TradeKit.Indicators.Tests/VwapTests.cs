namespace Ozakboy.TradeKit.Indicators.Tests;

/// <summary>
/// VWAP 的黃金向量、批次與增量一致性、時段重設、零量處理、混合 Amount 與參數驗證測試。
/// 黃金向量的手算過程與來源見 GoldenVectorProvenance.cs 的「VWAP」一節。
/// Golden vectors, batch-versus-streaming consistency, session reset, zero-volume handling, mixed Amount
/// and parameter validation tests for VWAP. The hand calculations behind the golden vectors are in the
/// "VWAP" section of GoldenVectorProvenance.cs.
/// </summary>
[TestClass]
public sealed class VwapTests
{
    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);

    /// <summary>台股情境的時段切法:台北日期。Session rule for Taiwan stocks: the Taipei calendar date.</summary>
    private static DateOnly TaipeiDate(DateTimeOffset time)
        => DateOnly.FromDateTime(time.ToOffset(TaipeiOffset).DateTime);

    private static DateTimeOffset Taipei(int day, int hour, int minute)
        => new(2026, 10, day, hour, minute, 0, TaipeiOffset);

    private static VwapBar Bar(DateTimeOffset time, decimal high, decimal low, decimal close, decimal volume, decimal? amount = null)
        => new(time, high, low, close, volume, amount);

    // ---- 黃金向量 1:台股,每根都有 Amount,volumeMultiplier = 1000 ----

    private static readonly VwapBar[] TaiwanStockBars =
    [
        // 價格 2490、1859 張:金額 = 2490 × 1859 × 1000。High / Low / Close 故意與成交價不同,證明沒有誤用典型價。
        Bar(Taipei(30, 9, 0), 2500m, 2480m, 2490m, 1859m, 4_628_910_000m),
        Bar(Taipei(30, 9, 1), 2505m, 2490m, 2495m, 60m, 149_700_000m),
        Bar(Taipei(30, 9, 2), 2510m, 2495m, 2500m, 219m, 547_500_000m),
    ];

    private static readonly decimal?[] TaiwanStockExpected =
    [
        2490m,
        2490.156331422616m,
        2491.164639850327m,
    ];

    [TestMethod]
    public void Calculate_TaiwanStockWithAmount_MatchesGoldenVector()
    {
        IReadOnlyList<decimal?> batch = Vwap.Calculate(TaiwanStockBars, TaipeiDate, 1000m);

        IndicatorAssert.SequenceMatches(TaiwanStockExpected, batch, "VWAP 台股 批次", IndicatorAssert.GoldenTolerance);
        Assert.AreEqual(2490m, batch[0], "第一根的 VWAP 必須恰好等於成交價");
    }

    [TestMethod]
    public void Update_TaiwanStockWithAmount_MatchesGoldenVector()
    {
        IReadOnlyList<decimal?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<VwapBar, decimal>)new VwapIndicator(TaipeiDate, 1000m),
            TaiwanStockBars);

        IndicatorAssert.SequenceMatches(TaiwanStockExpected, streaming, "VWAP 台股 增量", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Calculate_AmountPresent_TakesPrecedenceOverTypicalPrice()
    {
        // 典型價 = (110 + 90 + 100) / 3 = 100,若誤用典型價結果會是 100;Amount 說這根實際均價是 105。
        VwapBar[] bars = [Bar(Taipei(30, 9, 0), 110m, 90m, 100m, 2m, 210m)];

        Assert.AreEqual(105m, Vwap.Calculate(bars, TaipeiDate)[0]);
    }

    // ---- 黃金向量 2:全部沒有 Amount,退回典型價 ----

    private static readonly VwapBar[] TypicalPriceBars =
    [
        Bar(Taipei(30, 9, 0), 10.5m, 9.5m, 10m, 100m),
        Bar(Taipei(30, 9, 1), 12m, 10m, 11m, 200m),
        Bar(Taipei(30, 9, 2), 13m, 11m, 12.6m, 100m),
    ];

    private static readonly decimal?[] TypicalPriceExpected =
    [
        10m,
        10.666666666666667m,
        11.05m,
    ];

    [TestMethod]
    public void Calculate_WithoutAmount_FallsBackToTypicalPrice()
    {
        IReadOnlyList<decimal?> batch = Vwap.Calculate(TypicalPriceBars, TaipeiDate);

        IndicatorAssert.SequenceMatches(TypicalPriceExpected, batch, "VWAP 典型價 批次", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Update_WithoutAmount_FallsBackToTypicalPrice()
    {
        IReadOnlyList<decimal?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<VwapBar, decimal>)new VwapIndicator(TaipeiDate),
            TypicalPriceBars);

        IndicatorAssert.SequenceMatches(TypicalPriceExpected, streaming, "VWAP 典型價 增量", IndicatorAssert.GoldenTolerance);
    }

    /// <summary>
    /// 沒有 Amount 時分子與分母同乘 volumeMultiplier,結果與係數無關(僅差 decimal 末位進位)。
    /// </summary>
    [TestMethod]
    [DataRow(1000)]
    [DataRow(7)]
    public void Calculate_WithoutAmount_IsIndependentOfTheMultiplier(int multiplier)
    {
        IReadOnlyList<decimal?> baseline = Vwap.Calculate(TypicalPriceBars, TaipeiDate);
        IReadOnlyList<decimal?> scaled = Vwap.Calculate(TypicalPriceBars, TaipeiDate, multiplier);

        IndicatorAssert.SequenceMatches(baseline, scaled, $"VWAP 係數 {multiplier}", IndicatorAssert.GoldenTolerance);
    }

    // ---- 黃金向量 3:跨時段重設 + 時段內零量 + 時段開頭零量 ----

    private static readonly VwapBar[] SessionBars =
    [
        // 10/30 時段:開頭一根零量(High/Low/Close 故意離譜,零量不得有任何貢獻)。
        Bar(Taipei(30, 9, 0), 50m, 50m, 50m, 0m),
        Bar(Taipei(30, 9, 1), 21m, 19m, 20m, 10m),
        // 時段中間零量:沿用上一個值 20,而且這根的價格不得影響結果。
        Bar(Taipei(30, 9, 2), 99m, 1m, 50m, 0m),
        Bar(Taipei(30, 9, 3), 31m, 29m, 30m, 10m),
        // 10/31 時段:開頭兩根零量必須是 null,不能沿用昨天的 25。
        Bar(Taipei(31, 9, 0), 60m, 60m, 60m, 0m),
        Bar(Taipei(31, 9, 1), 70m, 70m, 70m, 0m),
        Bar(Taipei(31, 9, 2), 41m, 39m, 40m, 5m),
        Bar(Taipei(31, 9, 3), 80m, 80m, 80m, 0m),
    ];

    private static readonly decimal?[] SessionExpected =
    [
        null,
        20m,
        20m,
        25m,
        null,
        null,
        40m,
        40m,
    ];

    [TestMethod]
    public void Calculate_SessionResetAndZeroVolumeBars_MatchesGoldenVector()
    {
        IReadOnlyList<decimal?> batch = Vwap.Calculate(SessionBars, TaipeiDate);

        IndicatorAssert.SequenceMatches(SessionExpected, batch, "VWAP 時段 批次", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Update_SessionResetAndZeroVolumeBars_MatchesGoldenVector()
    {
        IReadOnlyList<decimal?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<VwapBar, decimal>)new VwapIndicator(TaipeiDate),
            SessionBars);

        IndicatorAssert.SequenceMatches(SessionExpected, streaming, "VWAP 時段 增量", IndicatorAssert.GoldenTolerance);
    }

    /// <summary>
    /// 時段內已有值之後量為 0,輸出必須沿用上一個值——不是 null、不是 0。
    /// </summary>
    [TestMethod]
    public void Update_ZeroVolumeAfterValue_RepeatsThePreviousValue()
    {
        VwapIndicator indicator = new(TaipeiDate);

        decimal? first = indicator.Update(Bar(Taipei(30, 9, 0), 21m, 19m, 20m, 10m));
        decimal? zero = indicator.Update(Bar(Taipei(30, 9, 1), 99m, 1m, 50m, 0m));

        Assert.AreEqual(20m, first);
        Assert.IsNotNull(zero, "零量不得讓已有的值消失");
        Assert.AreNotEqual(0m, zero, "零量不得輸出 0");
        Assert.AreEqual(first, zero);
        Assert.IsTrue(indicator.IsReady);
    }

    [TestMethod]
    public void IsReady_FollowsTheCurrentSession()
    {
        VwapIndicator indicator = new(TaipeiDate);
        Assert.AreEqual(1, indicator.WarmupPeriod);
        Assert.IsFalse(indicator.IsReady, "剛建立不得就緒");

        indicator.Update(Bar(Taipei(30, 9, 0), 50m, 50m, 50m, 0m));
        Assert.IsFalse(indicator.IsReady, "WarmupPeriod 雖為 1,但還沒有成交量就不算就緒");

        indicator.Update(Bar(Taipei(30, 9, 1), 21m, 19m, 20m, 10m));
        Assert.IsTrue(indicator.IsReady);

        indicator.Update(Bar(Taipei(31, 9, 0), 60m, 60m, 60m, 0m));
        Assert.IsFalse(indicator.IsReady, "新時段開頭零量:前一個時段的值不得帶過來");
        Assert.IsNull(indicator.Current);

        indicator.Update(Bar(Taipei(31, 9, 1), 41m, 39m, 40m, 5m));
        Assert.IsTrue(indicator.IsReady);
        Assert.AreEqual(4L, indicator.ProcessedCount);
    }

    /// <summary>
    /// 時段由 sessionOf 決定,不是由日期差決定:期貨夜盤可映射到下一個交易日,
    /// 因此夜盤與隔天日盤共用同一個時段、不重設。
    /// </summary>
    [TestMethod]
    public void Calculate_SessionOfMapsNightSessionToNextTradingDay_DoesNotResetBetweenThem()
    {
        // 15:00(含)之後歸入下一個日曆日的時段,模擬期貨夜盤。
        static DateOnly FuturesSession(DateTimeOffset time)
        {
            DateTime taipei = time.ToOffset(TimeSpan.FromHours(8)).DateTime;
            return DateOnly.FromDateTime(taipei.Hour >= 15 ? taipei.AddDays(1) : taipei);
        }

        VwapBar[] bars =
        [
            Bar(Taipei(30, 21, 0), 101m, 99m, 100m, 10m),
            Bar(Taipei(31, 9, 0), 111m, 109m, 110m, 10m),
            Bar(Taipei(31, 16, 0), 201m, 199m, 200m, 10m),
        ];

        IReadOnlyList<decimal?> result = Vwap.Calculate(bars, FuturesSession);

        Assert.AreEqual(100m, result[0]);
        Assert.AreEqual(105m, result[1], "夜盤與隔天日盤同一時段,應連續累計");
        Assert.AreEqual(200m, result[2], "16:00 起進入下一個時段,應重設");
    }

    // ---- 黃金向量 4:同一時段部分根有 Amount、部分沒有 ----

    private static readonly VwapBar[] MixedBars =
    [
        // 有 Amount:價 1000 × 1 張 × 1000 = 1,000,000。
        Bar(Taipei(30, 9, 0), 1000m, 1000m, 1000m, 1m, 1_000_000m),
        // 沒有 Amount:典型價 1100 × 2 張 × 1000 = 2,200,000。
        Bar(Taipei(30, 9, 1), 1110m, 1090m, 1100m, 2m),
        // 有 Amount:價 1100 × 3 張 × 1000 = 3,300,000。
        Bar(Taipei(30, 9, 2), 1100m, 1100m, 1100m, 3m, 3_300_000m),
    ];

    private static readonly decimal?[] MixedExpected =
    [
        1000m,
        1066.666666666667m,
        1083.333333333333m,
    ];

    [TestMethod]
    public void Calculate_MixedAmountAndNoAmountInOneSession_MatchesGoldenVector()
    {
        IReadOnlyList<decimal?> batch = Vwap.Calculate(MixedBars, TaipeiDate, 1000m);

        IndicatorAssert.SequenceMatches(MixedExpected, batch, "VWAP 混合 批次", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Update_MixedAmountAndNoAmountInOneSession_MatchesGoldenVector()
    {
        IReadOnlyList<decimal?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<VwapBar, decimal>)new VwapIndicator(TaipeiDate, 1000m),
            MixedBars);

        IndicatorAssert.SequenceMatches(MixedExpected, streaming, "VWAP 混合 增量", IndicatorAssert.GoldenTolerance);
    }

    // ---- 批次與增量一致 ----

    /// <summary>以固定種子的線性同餘產生器合成多時段資料,含零量根、時段開頭零量與 Amount 時有時無。</summary>
    private static VwapBar[] BuildSyntheticSeries(decimal multiplier)
    {
        List<VwapBar> bars = [];
        uint seed = 20261031;
        uint Next()
        {
            seed = unchecked((seed * 1664525u) + 1013904223u);
            return seed >> 8;
        }

        decimal price = 100m;
        for (int day = 1; day <= 5; day++)
        {
            for (int minute = 0; minute < 40; minute++)
            {
                price += ((int)(Next() % 41u) - 20) / 100m;
                decimal high = price + ((Next() % 30u) / 100m);
                decimal low = price - ((Next() % 30u) / 100m);
                decimal volume = Next() % 5u == 0u || (minute < 2 && day % 2 == 0) ? 0m : Next() % 500u;
                decimal? amount = Next() % 3u == 0u ? null : price * volume * multiplier;

                bars.Add(Bar(Taipei(day, 9, minute), high, low, price, volume, amount));
            }
        }

        return [.. bars];
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(1000)]
    public void BatchAndStreaming_ProduceIdenticalResults(int multiplier)
    {
        VwapBar[] bars = BuildSyntheticSeries(multiplier);

        IReadOnlyList<decimal?> batch = Vwap.Calculate(bars, TaipeiDate, multiplier);
        IReadOnlyList<decimal?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<VwapBar, decimal>)new VwapIndicator(TaipeiDate, multiplier),
            bars);

        Assert.IsTrue(batch.Any(value => value is null), "測試資料應包含 null 的位置");
        Assert.IsTrue(batch.Any(value => value is not null), "測試資料應包含有值的位置");
        IndicatorAssert.SequencesAreIdentical(batch, streaming, $"VWAP(×{multiplier})");
    }

    [TestMethod]
    public void BatchAndStreaming_GoldenSeries_ProduceIdenticalResults()
    {
        VwapBar[][] series = [TaiwanStockBars, TypicalPriceBars, SessionBars, MixedBars];

        foreach (VwapBar[] bars in series)
        {
            IReadOnlyList<decimal?> batch = Vwap.Calculate(bars, TaipeiDate, 1000m);
            IReadOnlyList<decimal?> streaming = IndicatorAssert.RunStreaming(
                (IIndicator<VwapBar, decimal>)new VwapIndicator(TaipeiDate, 1000m),
                bars);

            IndicatorAssert.SequencesAreIdentical(batch, streaming, "VWAP 黃金序列");
        }
    }

    [TestMethod]
    public void Calculate_EmptySeries_ReturnsEmpty()
        => Assert.IsEmpty(Vwap.Calculate([], TaipeiDate));

    // ---- Reset ----

    [TestMethod]
    public void Reset_ThenReplay_BehavesLikeAFreshInstance()
    {
        VwapBar[] bars = BuildSyntheticSeries(1000m);
        VwapIndicator reused = new(TaipeiDate, 1000m);
        foreach (VwapBar bar in bars)
        {
            reused.Update(bar);
        }

        reused.Reset();

        Assert.IsFalse(reused.IsReady);
        Assert.IsNull(reused.Current);
        Assert.AreEqual(0L, reused.ProcessedCount);

        IReadOnlyList<decimal?> afterReset = IndicatorAssert.RunStreaming((IIndicator<VwapBar, decimal>)reused, bars);
        IReadOnlyList<decimal?> fresh = IndicatorAssert.RunStreaming(
            (IIndicator<VwapBar, decimal>)new VwapIndicator(TaipeiDate, 1000m),
            bars);

        IndicatorAssert.SequencesAreIdentical(fresh, afterReset, "VWAP 重設後");
    }

    /// <summary>
    /// 在時段中途重設,接著餵「同一個時段」的資料:舊的累計值與時段記憶都必須清掉,
    /// 否則新資料會被當成前一段的延續而算錯。
    /// </summary>
    [TestMethod]
    public void Reset_MidSession_ThenSameSessionBar_StartsFromScratch()
    {
        VwapIndicator indicator = new(TaipeiDate);
        indicator.Update(Bar(Taipei(30, 9, 0), 1000m, 1000m, 1000m, 100m));

        indicator.Reset();
        decimal? value = indicator.Update(Bar(Taipei(30, 9, 1), 21m, 19m, 20m, 10m));

        Assert.AreEqual(20m, value);
        Assert.AreEqual(1L, indicator.ProcessedCount);
    }

    // ---- 描述性屬性 ----

    [TestMethod]
    public void Name_IncludesTheVolumeMultiplier()
    {
        Assert.AreEqual("VWAP(1)", new VwapIndicator(TaipeiDate).Name);
        Assert.AreEqual("VWAP(1000)", new VwapIndicator(TaipeiDate, 1000m).Name);
        Assert.AreEqual(1000m, new VwapIndicator(TaipeiDate, 1000m).VolumeMultiplier);
    }

    // ---- 參數驗證 ----

    [TestMethod]
    public void Constructor_InvalidArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new VwapIndicator(null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new VwapIndicator(TaipeiDate, 0m));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new VwapIndicator(TaipeiDate, -1m));
    }

    [TestMethod]
    public void Calculate_InvalidArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Vwap.Calculate(null!, TaipeiDate));
        Assert.ThrowsExactly<ArgumentNullException>(() => Vwap.Calculate(TypicalPriceBars, null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Vwap.Calculate(TypicalPriceBars, TaipeiDate, 0m));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Vwap.Calculate(TypicalPriceBars, TaipeiDate, -1000m));
    }

    [TestMethod]
    public void Calculate_NegativeVolumeOrAmount_Throws()
    {
        VwapBar negativeVolume = Bar(Taipei(30, 9, 0), 10m, 10m, 10m, -1m);
        VwapBar negativeAmount = Bar(Taipei(30, 9, 0), 10m, 10m, 10m, 1m, -10m);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Vwap.Calculate([negativeVolume], TaipeiDate));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Vwap.Calculate([negativeAmount], TaipeiDate));
    }

    [TestMethod]
    public void Update_NegativeVolumeOrAmount_ThrowsAndLeavesStateUntouched()
    {
        VwapIndicator indicator = new(TaipeiDate);
        indicator.Update(Bar(Taipei(30, 9, 0), 21m, 19m, 20m, 10m));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => indicator.Update(Bar(Taipei(30, 9, 1), 10m, 10m, 10m, -1m)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => indicator.Update(Bar(Taipei(30, 9, 1), 10m, 10m, 10m, 1m, -10m)));

        // 例外不得改動任何狀態:筆數沒增加、目前值沒變、接下來的計算像沒發生過。
        Assert.AreEqual(1L, indicator.ProcessedCount);
        Assert.AreEqual(20m, indicator.Current);
        Assert.AreEqual(25m, indicator.Update(Bar(Taipei(30, 9, 2), 31m, 29m, 30m, 10m)));
    }

    [TestMethod]
    public void Update_ZeroAmountWithVolume_IsValidAndPullsTheAverageDown()
    {
        // Amount = 0 是合法輸入(例如來源填 0 而非 null),照公式累加而不是當成「沒提供」。
        VwapIndicator indicator = new(TaipeiDate);

        indicator.Update(Bar(Taipei(30, 9, 0), 10m, 10m, 10m, 1m, 10m));
        decimal? value = indicator.Update(Bar(Taipei(30, 9, 1), 10m, 10m, 10m, 1m, 0m));

        Assert.AreEqual(5m, value);
    }
}
