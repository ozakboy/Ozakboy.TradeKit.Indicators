namespace Ozakboy.TradeKit.Indicators;

/// <summary>
/// 指數平滑異同移動平均(MACD)的增量版本,含訊號線與柱狀圖。
/// Streaming implementation of the Moving Average Convergence Divergence (MACD), including signal line and histogram.
/// </summary>
/// <remarks>
/// <para>
/// <b>公式。</b><c>MACD 線 = EMA(快線期數) − EMA(慢線期數)</c>、<c>訊號線 = EMA(訊號期數) of MACD 線</c>、
/// <c>柱狀圖 = MACD 線 − 訊號線</c>。Appel 原始參數為 12 / 26 / 9。
/// <b>Formula.</b> <c>MACD = EMA(fast) - EMA(slow)</c>, <c>signal = EMA(signalPeriod) of the MACD line</c>,
/// and <c>histogram = MACD - signal</c>. Appel's original parameters are 12 / 26 / 9.
/// </para>
/// <para>
/// <b>暖身期。</b>採用預設的 SMA 種子時,MACD 線在第 <c>慢線期數</c> 筆才出現,訊號線還要再累積 <c>訊號期數</c> 筆,
/// 因此總共需要 <c>慢線期數 + 訊號期數 − 1</c> 筆(12 / 26 / 9 即 34 筆)。
/// <b>Warm-up.</b> With the default SMA seeding the MACD line appears at input <c>slowPeriod</c> and the signal
/// line needs a further <c>signalPeriod</c> MACD values, so <c>slowPeriod + signalPeriod - 1</c> inputs are
/// required in total (34 for 12 / 26 / 9).
/// </para>
/// <para>
/// <b>實作。</b>組合三個 <see cref="EmaIndicator" />,訊號線 EMA 只在 MACD 線存在時才餵入,狀態大小為常數。
/// <b>Implementation.</b> Composes three <see cref="EmaIndicator" /> instances; the signal EMA is fed only once
/// the MACD line exists. State size is constant.
/// </para>
/// <para>
/// <b>典型用法。</b>柱狀圖由負轉正視為多方動能轉強,由正轉負視為空方動能轉強;也常用 MACD 線與價格的背離。
/// <b>Typical use.</b> A histogram crossing from negative to positive marks strengthening upside momentum and
/// vice versa; divergence between the MACD line and price is also widely used.
/// </para>
/// </remarks>
public sealed class MacdIndicator : IIndicator<decimal, MacdResult>, IIndicator<Candle, MacdResult>
{
    private readonly EmaIndicator _fast;
    private readonly EmaIndicator _slow;
    private readonly EmaIndicator _signal;

    /// <summary>
    /// 建立 MACD 指標。
    /// Creates a MACD indicator.
    /// </summary>
    /// <param name="fastPeriod">快線 EMA 期數,必須大於 0 且小於 <paramref name="slowPeriod" />。Fast EMA period; must be greater than 0 and less than <paramref name="slowPeriod" />.</param>
    /// <param name="slowPeriod">慢線 EMA 期數,必須大於 <paramref name="fastPeriod" />。Slow EMA period; must be greater than <paramref name="fastPeriod" />.</param>
    /// <param name="signalPeriod">訊號線 EMA 期數,必須大於 0。Signal EMA period; must be greater than 0.</param>
    /// <param name="seedMode">三條 EMA 共用的起始值取法。Seeding convention shared by all three EMAs.</param>
    /// <exception cref="ArgumentOutOfRangeException">任一期數小於或等於 0,或 <paramref name="fastPeriod" /> 未小於 <paramref name="slowPeriod" />。Any period is less than or equal to 0, or <paramref name="fastPeriod" /> is not less than <paramref name="slowPeriod" />.</exception>
    public MacdIndicator(
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9,
        EmaSeedMode seedMode = EmaSeedMode.SimpleMovingAverage)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fastPeriod);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(slowPeriod);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(signalPeriod);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(fastPeriod, slowPeriod);

        FastPeriod = fastPeriod;
        SlowPeriod = slowPeriod;
        SignalPeriod = signalPeriod;
        SeedMode = seedMode;
        Name = FormattableString.Invariant($"MACD({fastPeriod},{slowPeriod},{signalPeriod})");

        _fast = new EmaIndicator(fastPeriod, seedMode);
        _slow = new EmaIndicator(slowPeriod, seedMode);
        _signal = new EmaIndicator(signalPeriod, seedMode);
    }

    /// <summary>快線 EMA 期數。Fast EMA period.</summary>
    public int FastPeriod { get; }

    /// <summary>慢線 EMA 期數。Slow EMA period.</summary>
    public int SlowPeriod { get; }

    /// <summary>訊號線 EMA 期數。Signal EMA period.</summary>
    public int SignalPeriod { get; }

    /// <summary>三條 EMA 共用的起始值取法。Seeding convention shared by all three EMAs.</summary>
    public EmaSeedMode SeedMode { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public int WarmupPeriod => SlowPeriod + SignalPeriod - 1;

    /// <inheritdoc />
    public bool IsReady => _signal.IsReady;

    /// <inheritdoc />
    public long ProcessedCount { get; private set; }

    /// <inheritdoc />
    public MacdResult? Current { get; private set; }

    /// <summary>
    /// 餵入一筆價格並回傳當下的 MACD 三元組;資料量不足時回傳 <see langword="null" />。
    /// Feeds one price and returns the current MACD triple; returns <see langword="null" /> until warmed up.
    /// </summary>
    /// <param name="input">價格。The price.</param>
    /// <returns>當下的 MACD 結果,未就緒時為 <see langword="null" />。The current MACD result, or <see langword="null" /> when not ready.</returns>
    public MacdResult? Update(decimal input)
    {
        ProcessedCount++;

        decimal? fast = _fast.Update(input);
        decimal? slow = _slow.Update(input);

        if (fast is null || slow is null)
        {
            Current = null;
            return null;
        }

        decimal macdLine = fast.Value - slow.Value;
        decimal? signal = _signal.Update(macdLine);

        if (signal is null)
        {
            Current = null;
            return null;
        }

        Current = new MacdResult(macdLine, signal.Value, macdLine - signal.Value);
        return Current;
    }

    /// <summary>
    /// 以 K 線的收盤價餵入一筆資料。
    /// Feeds one candle, using its close price.
    /// </summary>
    /// <param name="input">K 線。The candle.</param>
    /// <returns>當下的 MACD 結果,未就緒時為 <see langword="null" />。The current MACD result, or <see langword="null" /> when not ready.</returns>
    public MacdResult? Update(Candle input) => Update(input.Close);

    /// <inheritdoc />
    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
        _signal.Reset();
        ProcessedCount = 0;
        Current = null;
    }
}
