namespace Ozakboy.TradeKit.Indicators.Tests;

/// <summary>
/// KD 隨機指標的黃金向量、批次與增量一致性、分母為 0、暖身期、重設與參數驗證測試。
/// 黃金向量的手算過程與來源見 GoldenVectorProvenance.cs 的「KD」一節。
/// Golden vectors, batch-versus-streaming consistency, zero-denominator, warm-up, reset and parameter validation
/// tests for the Stochastic oscillator (KD). The hand calculations behind the golden vectors are in the "KD"
/// section of GoldenVectorProvenance.cs.
/// </summary>
[TestClass]
public sealed class StochasticTests
{
    private static decimal?[] Project(IReadOnlyList<StochasticResult?> results, Func<StochasticResult, decimal> selector)
        => [.. results.Select(value => value is StochasticResult result ? selector(result) : (decimal?)null)];

    // ---- 手算序列:18 根 K 線(High, Low, Close),Open 不參與計算 ----
    // 每根右側註記 KD(9,3,3) 的 9 根視窗最高 HH、最低 LL 與 RSV = (C − LL) / (HH − LL) × 100。
    // 種子 K₀ = D₀ = 50,之後 K = K前 × 2/3 + RSV / 3、D = D前 × 2/3 + K / 3。

    private static readonly Candle[] HandCandles =
    [
        Candle.Of(103m, 105m, 100m, 103m),   // 索引 0
        Candle.Of(103m, 107m, 102m, 106m),   // 索引 1
        Candle.Of(106m, 108m, 104m, 105m),   // 索引 2
        Candle.Of(105m, 106m, 101m, 102m),   // 索引 3
        Candle.Of(102m, 104m, 99m, 100m),    // 索引 4
        Candle.Of(100m, 103m, 98m, 99m),     // 索引 5
        Candle.Of(99m, 105m, 100m, 104m),    // 索引 6
        Candle.Of(104m, 109m, 103m, 108m),   // 索引 7
        Candle.Of(108m, 110m, 106m, 109m),   // 索引 8   HH 110 LL 98  RSV 11/12 × 100 = 275/3
        Candle.Of(109m, 112m, 108m, 111m),   // 索引 9   HH 112 LL 98  RSV 13/14 × 100 = 650/7
        Candle.Of(111m, 111m, 105m, 106m),   // 索引 10  HH 112 LL 98  RSV  8/14 × 100 = 400/7
        Candle.Of(106m, 107m, 102m, 103m),   // 索引 11  HH 112 LL 98  RSV  5/14 × 100 = 250/7
        Candle.Of(103m, 106m, 100m, 101m),   // 索引 12  HH 112 LL 98  RSV  3/14 × 100 = 150/7
        Candle.Of(101m, 104m, 98m, 98m),     // 索引 13  HH 112 LL 98  RSV 0(收在最低)
        Candle.Of(98m, 103m, 97m, 102m),     // 索引 14  HH 112 LL 97  RSV  5/15 × 100 = 100/3
        Candle.Of(102m, 108m, 102m, 107m),   // 索引 15  HH 112 LL 97  RSV 10/15 × 100 = 200/3
        Candle.Of(107m, 113m, 107m, 113m),   // 索引 16  HH 113 LL 97  RSV 100(收在最高)
        Candle.Of(113m, 112m, 106m, 108m),   // 索引 17  HH 113 LL 97  RSV 11/16 × 100 = 275/4
    ];

    // KD(9,3,3) 期望值。括號內為精確分數,以 Python fractions.Fraction 複核。
    //   索引 8   K = 50×2/3 + (275/3)/3 = 575/9           D = 50×2/3 + (575/9)/3 = 1475/27
    //   索引 9   K = (575/9)×2/3 + (650/7)/3 = 13900/189  D = 34550/567
    //   索引 10  K = 38600/567     D = 35900/567
    //   索引 11  K = 97450/1701    D = 312850/5103
    //   索引 12  K = 33050/729     D = 857050/15309
    //   索引 13  K = 66100/2187    D = 725600/15309
    //   索引 14  K = 205100/6561   D = 5789300/137781
    //   索引 15  K = 847600/19683  D = 17511800/413343
    //   索引 16  K = 3663500/59049 D = 6740900/137781
    //   索引 17  K = 45546475/708588  D = 804170125/14880348

    private static readonly decimal?[] HandK933 =
    [
        null, null, null, null, null, null, null, null,
        63.888888888889m,
        73.544973544974m,
        68.077601410935m,
        57.289829512052m,
        45.336076817558m,
        30.224051211706m,
        31.260478585581m,
        43.062541279277m,
        62.041694186184m,
        64.277796124123m,
    ];

    private static readonly decimal?[] HandD933 =
    [
        null, null, null, null, null, null, null, null,
        54.629629629630m,
        60.934744268078m,
        63.315696649030m,
        61.307074270037m,
        55.983408452544m,
        47.396956038931m,
        42.018130221148m,
        42.366267240524m,
        48.924742889078m,
        54.042427300759m,
    ];

    // KD(5,2,4):K 與 D 的平滑期數刻意不同,參數若被對調一定會失敗。
    // 種子同為 50;K = K前 × 1/2 + RSV / 2、D = D前 × 3/4 + K / 4。
    // 5 根視窗 RSV:索引 4 起依序 100/9、10、60、1000/11、275/3、650/7、50、10、25/3、0、250/7、1000/11、100、275/4。

    private static readonly decimal?[] HandK524 =
    [
        null, null, null, null,
        30.555555555556m,
        20.277777777778m,
        40.138888888889m,
        65.523989898990m,
        78.595328282828m,
        85.726235569986m,
        67.863117784993m,
        38.931558892496m,
        23.632446112915m,
        11.816223056457m,
        23.765254385372m,
        57.337172647231m,
        78.668586323616m,
        73.709293161808m,
    ];

    private static readonly decimal?[] HandD524 =
    [
        null, null, null, null,
        45.138888888889m,
        38.923611111111m,
        39.227430555556m,
        45.801570391414m,
        54.000009864268m,
        61.931566290697m,
        63.414454164271m,
        57.293730346327m,
        48.878409287974m,
        39.612862730095m,
        35.650960643914m,
        41.072513644743m,
        50.471531814461m,
        56.280972151298m,
    ];

    [TestMethod]
    public void Calculate_HandCheckedCandles_MatchesGoldenVector()
    {
        IReadOnlyList<StochasticResult?> batch = Stochastic.Calculate(HandCandles);

        IndicatorAssert.SequenceMatches(HandK933, Project(batch, r => r.K), "KD(9,3,3) K 批次", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(HandD933, Project(batch, r => r.D), "KD(9,3,3) D 批次", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Update_HandCheckedCandles_MatchesGoldenVector()
    {
        IReadOnlyList<StochasticResult?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<Candle, StochasticResult>)new StochasticIndicator(),
            HandCandles);

        IndicatorAssert.SequenceMatches(HandK933, Project(streaming, r => r.K), "KD(9,3,3) K 增量", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(HandD933, Project(streaming, r => r.D), "KD(9,3,3) D 增量", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Calculate_DistinctSmoothingPeriods_MatchesGoldenVector()
    {
        IReadOnlyList<StochasticResult?> batch = Stochastic.Calculate(HandCandles, 5, 2, 4);
        IReadOnlyList<StochasticResult?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<Candle, StochasticResult>)new StochasticIndicator(5, 2, 4),
            HandCandles);

        IndicatorAssert.SequenceMatches(HandK524, Project(batch, r => r.K), "KD(5,2,4) K 批次", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequenceMatches(HandD524, Project(batch, r => r.D), "KD(5,2,4) D 批次", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.SequencesAreIdentical(batch, streaming, "KD(5,2,4)");
    }

    /// <summary>
    /// 種子值 50 本身也要釘死:第一根輸出的 K = 50 × 2/3 + RSV / 3,
    /// 收在最高(RSV 100)時恰為 200/3、D = 50 × 2/3 + K / 3 = 500/9。
    /// </summary>
    [TestMethod]
    public void Calculate_FirstOutput_IsSeededFromFifty()
    {
        Candle[] candles =
        [
            Candle.Of(10m, 12m, 8m, 9m),
            Candle.Of(9m, 11m, 9m, 10m),
            Candle.Of(10m, 14m, 10m, 14m),
        ];

        StochasticResult first = Stochastic.Calculate(candles, 3)[2]!.Value;

        IndicatorAssert.ValueMatches(200m / 3m, first.K, "第一根 K", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.ValueMatches(500m / 9m, first.D, "第一根 D", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    [DataRow(1, 1, 1)]
    [DataRow(1, 3, 3)]
    [DataRow(3, 3, 3)]
    [DataRow(9, 3, 3)]
    [DataRow(14, 1, 5)]
    [DataRow(20, 5, 2)]
    [DataRow(60, 3, 3)]
    public void BatchAndStreaming_ProduceIdenticalResults(int period, int kSmoothing, int dSmoothing)
    {
        IReadOnlyList<StochasticResult?> batch = Stochastic.Calculate(GoldenVectors.Candles, period, kSmoothing, dSmoothing);
        IReadOnlyList<StochasticResult?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<Candle, StochasticResult>)new StochasticIndicator(period, kSmoothing, dSmoothing),
            GoldenVectors.Candles);

        IndicatorAssert.SequencesAreIdentical(batch, streaming, $"KD({period},{kSmoothing},{dSmoothing})");
    }

    /// <summary>
    /// 整段持平:最高 = 最低,RSV 以 50 代替;種子也是 50,所以 K、D 從頭到尾恰好都是 50。
    /// </summary>
    [TestMethod]
    public void Calculate_FlatSeries_RsvFallsBackToFiftyAndKdStayAtFifty()
    {
        Candle[] flat = [.. Enumerable.Repeat(Candle.Of(100m, 100m, 100m, 100m), 20)];

        IReadOnlyList<StochasticResult?> batch = Stochastic.Calculate(flat);
        IReadOnlyList<StochasticResult?> streaming = IndicatorAssert.RunStreaming(
            (IIndicator<Candle, StochasticResult>)new StochasticIndicator(),
            flat);

        for (int i = 8; i < flat.Length; i++)
        {
            Assert.AreEqual(new StochasticResult(50m, 50m), batch[i], $"批次第 {i} 根");
            Assert.AreEqual(new StochasticResult(50m, 50m), streaming[i], $"增量第 {i} 根");
        }
    }

    /// <summary>
    /// 先有波動、後轉持平:持平區的 RSV 為 50,K、D 必須逐步回歸 50,而不是除以零或卡在舊值。
    /// 週期 2 下最後兩根完全相同,RSV = 50;前一根 K = 50×2/3 + 100/3 = 200/3(收在最高),
    /// 下一根 K = (200/3)×2/3 + 50/3 = 550/9。
    /// </summary>
    [TestMethod]
    public void Update_WindowTurnsFlat_UsesFiftyAsRsv()
    {
        StochasticIndicator indicator = new(2, 3, 3);

        indicator.Update(Candle.Of(10m, 10m, 8m, 9m));
        StochasticResult? rising = indicator.Update(Candle.Of(10m, 10m, 10m, 10m));
        StochasticResult? flat = indicator.Update(Candle.Of(10m, 10m, 10m, 10m));

        IndicatorAssert.ValueMatches(200m / 3m, rising?.K, "收在最高的 K", IndicatorAssert.GoldenTolerance);
        IndicatorAssert.ValueMatches(550m / 9m, flat?.K, "持平後的 K", IndicatorAssert.GoldenTolerance);
    }

    [TestMethod]
    public void Update_BeforeWarmupCompletes_ReportsNotReadyAndReturnsNull()
    {
        StochasticIndicator indicator = new();
        Assert.AreEqual(9, indicator.WarmupPeriod);

        for (int i = 0; i < 8; i++)
        {
            Assert.IsNull(indicator.Update(GoldenVectors.Candles[i]), $"第 {i + 1} 根不應該有值");
            Assert.IsFalse(indicator.IsReady);
            Assert.IsNull(indicator.Current);
        }

        Assert.IsNotNull(indicator.Update(GoldenVectors.Candles[8]));
        Assert.IsTrue(indicator.IsReady);
    }

    [TestMethod]
    public void Calculate_FirstPeriodMinusOneEntries_AreNull()
    {
        IReadOnlyList<StochasticResult?> batch = Stochastic.Calculate(GoldenVectors.Candles, 9);

        Assert.HasCount(GoldenVectors.Candles.Length, batch);
        Assert.IsTrue(batch.Take(8).All(value => value is null), "前 8 根必須是 null");
        Assert.IsTrue(batch.Skip(8).All(value => value is not null), "第 9 根起必須有值");
    }

    [TestMethod]
    public void Calculate_EmptyOrTooShortSeries_ReturnsAllNull()
    {
        Assert.IsEmpty(Stochastic.Calculate(Array.Empty<Candle>()));

        IReadOnlyList<StochasticResult?> tooShort = Stochastic.Calculate(GoldenVectors.Candles.Take(8).ToArray());
        Assert.HasCount(8, tooShort);
        Assert.IsTrue(tooShort.All(value => value is null));
    }

    /// <summary>
    /// 重設必須連同 K、D 的遞迴狀態一起清掉,否則重新餵入時會從舊的 K、D 而不是 50 起算。
    /// </summary>
    [TestMethod]
    public void Reset_ThenRefeed_ReproducesTheSameResults()
    {
        StochasticIndicator indicator = new();
        IReadOnlyList<StochasticResult?> first = IndicatorAssert.RunStreaming(
            (IIndicator<Candle, StochasticResult>)indicator, GoldenVectors.Candles);

        indicator.Reset();

        Assert.IsFalse(indicator.IsReady);
        Assert.IsNull(indicator.Current);
        Assert.AreEqual(0L, indicator.ProcessedCount);

        IReadOnlyList<StochasticResult?> second = IndicatorAssert.RunStreaming(
            (IIndicator<Candle, StochasticResult>)indicator, GoldenVectors.Candles);

        IndicatorAssert.SequencesAreIdentical(first, second, "KD 重設後重新餵入");
    }

    [TestMethod]
    public void Name_IncludesAllThreeParameters()
    {
        Assert.AreEqual("KD(9,3,3)", new StochasticIndicator().Name);
        Assert.AreEqual("KD(14,1,5)", new StochasticIndicator(14, 1, 5).Name);
    }

    [TestMethod]
    [DataRow(0, 3, 3)]
    [DataRow(-1, 3, 3)]
    [DataRow(9, 0, 3)]
    [DataRow(9, -2, 3)]
    [DataRow(9, 3, 0)]
    [DataRow(9, 3, -5)]
    public void NonPositiveParameter_Throws(int period, int kSmoothing, int dSmoothing)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new StochasticIndicator(period, kSmoothing, dSmoothing));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Stochastic.Calculate(GoldenVectors.Candles, period, kSmoothing, dSmoothing));
    }

    [TestMethod]
    public void Calculate_NullSeries_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => Stochastic.Calculate(null!));
}
